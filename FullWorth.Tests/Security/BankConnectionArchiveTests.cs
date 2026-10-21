using System.Net;
using System.Net.Http.Json;
using FullWorth.API.Controllers;
using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FullWorth.Tests.Security;

public sealed class BankConnectionArchiveTests : IDisposable
{
    // Each case has its own real authentication limiter and database.
    // Sharing a factory would exhaust the per-IP login budget during test setup.
    private readonly FullWorthApiFactory _factory = new();
    public void Dispose() => _factory.Dispose();

    [Fact]
    public Task OtherUser_CannotArchive() => AssertOtherUserAsync("archive", false);

    [Fact]
    public Task OtherUser_CannotRestore() => AssertOtherUserAsync("restore", true);

    private async Task AssertOtherUserAsync(string action, bool archived)
    {
        using var owner = _factory.CreateHttpsClient();
        using var attacker = _factory.CreateHttpsClient();
        var ownerSession = await TestUserAuthentication.RegisterAndLoginAsync(owner);
        var attackerSession = await TestUserAuthentication.RegisterAndLoginAsync(attacker);
        var id = await SeedAsync(ownerSession.Email, BankConnectionStatus.Disconnected, archived);
        TestUserAuthentication.Authorize(attacker, attackerSession);
        using var response = await attacker.PostAsync($"/api/bank-connections/{id}/{action}", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
        Assert.Equal(archived, (await db.BankConnections.AsNoTracking().SingleAsync(x => x.Id == id)).IsArchived);
    }

    [Theory]
    [InlineData("archive")]
    [InlineData("restore")]
    public async Task AnonymousCannotMutate(string action)
    {
        using var client = _factory.CreateHttpsClient();
        using var response = await client.PostAsync($"/api/bank-connections/{Guid.NewGuid()}/{action}", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(BankConnectionStatus.Active, null)]
    [InlineData(BankConnectionStatus.RequiresAttention, null)]
    [InlineData(BankConnectionStatus.Disconnected, "revocation-still-needed")]
    public async Task ArchiveRequiresCompletedDisconnect(BankConnectionStatus status, string? credential)
    {
        using var client = _factory.CreateHttpsClient();
        var session = await TestUserAuthentication.RegisterAndLoginAsync(client);
        var id = await SeedAsync(session.Email, status, false, credential);
        TestUserAuthentication.Authorize(client, session);
        using var response = await client.PostAsync($"/api/bank-connections/{id}/archive", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
        var connection = await db.BankConnections.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.False(connection.IsArchived);
        Assert.Equal(status, connection.Status);
        Assert.Equal(credential, connection.ProtectedPlaidAccessToken);
    }

    [Fact]
    public async Task ArchiveAndRestoreAreDurableIdempotentAndKeepFinancialEvidence()
    {
        using var client = _factory.CreateHttpsClient();
        var session = await TestUserAuthentication.RegisterAndLoginAsync(client);
        var id = await SeedAsync(session.Email, BankConnectionStatus.Disconnected, false);
        Guid accountId;
        Guid transactionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
            var connection = await db.BankConnections.SingleAsync(x => x.Id == id);
            var account = new BankAccountEntity {
                UserId = connection.UserId, BankConnectionId = id,
                Name = "Historical checking", PlaidAccountId = Guid.NewGuid().ToString(), IsActive = false };
            var transaction = new BankTransactionEntity {
                UserId = connection.UserId, BankAccountId = account.Id,
                Name = "Historical bill evidence", PlaidTransactionId = Guid.NewGuid().ToString(),
                Amount = 42m, PostedDate = new DateOnly(2026, 10, 1) };
            accountId = account.Id;
            transactionId = transaction.Id;
            db.BankAccounts.Add(account);
            db.BankTransactions.Add(transaction);
            await db.SaveChangesAsync();
        }
        TestUserAuthentication.Authorize(client, session);
        foreach (var archived in new[] { true, true, false, false })
        {
            using var response = await client.PostAsync(
                $"/api/bank-connections/{id}/{(archived ? "archive" : "restore")}", null);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            var list = await client.GetFromJsonAsync<BankConnectionResult[]>("/api/bank-connections");
            Assert.Equal(archived, Assert.Single(list!).IsArchived);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
            var connection = await db.BankConnections.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Equal(archived, connection.IsArchived);
            Assert.Equal(BankConnectionStatus.Disconnected, connection.Status);
            Assert.Null(connection.ProtectedPlaidAccessToken);
            Assert.False((await db.BankAccounts.AsNoTracking().SingleAsync(x => x.Id == accountId)).IsActive);
            var transaction = await db.BankTransactions.AsNoTracking().SingleAsync(x => x.Id == transactionId);
            Assert.Equal(42m, transaction.Amount);
            Assert.False(transaction.IsRemoved);
        }
    }

    [Fact]
    public async Task ActiveConnectionWithOldArchivePreferenceIsNeverHidden()
    {
        using var client = _factory.CreateHttpsClient();
        var session = await TestUserAuthentication.RegisterAndLoginAsync(client);
        await SeedAsync(session.Email, BankConnectionStatus.Active, true);
        TestUserAuthentication.Authorize(client, session);
        var connections = await client.GetFromJsonAsync<BankConnectionResult[]>("/api/bank-connections");
        Assert.False(Assert.Single(connections!).IsArchived);
    }

    [Theory]
    [InlineData("archive", "00000000-0000-0000-0000-000000000000")]
    [InlineData("restore", "00000000-0000-0000-0000-000000000000")]
    [InlineData("archive", "66666666-6666-6666-6666-666666666666")]
    [InlineData("restore", "66666666-6666-6666-6666-666666666666")]
    public async Task MissingOrEmptyIdentifierReturnsNotFound(string action, string id)
    {
        using var client = _factory.CreateHttpsClient();
        var session = await TestUserAuthentication.RegisterAndLoginAsync(client);
        TestUserAuthentication.Authorize(client, session);
        using var response = await client.PostAsync($"/api/bank-connections/{id}/{action}", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("archive", true)]
    [InlineData("restore", true)]
    [InlineData("archive", false)]
    [InlineData("restore", false)]
    public async Task BffRequiresAuthenticationAndAntiforgery(string action, bool anonymous)
    {
        using var factory = new FullWorthWebFactory();
        using var client = factory.CreateHttpsClient();
        if (anonymous) client.DefaultRequestHeaders.Add("X-FullWorth-Test-Anonymous", "true");
        using var response = await client.PostAsync($"/bff/bank-connections/{Guid.NewGuid()}/{action}", null);
        Assert.Equal(anonymous ? HttpStatusCode.Unauthorized : HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<Guid> SeedAsync(string email, BankConnectionStatus status, bool archived, string? credential = null)
    {
        using var scope = _factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email);
        Assert.NotNull(user);
        var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
        var connection = new BankConnectionEntity {
            UserId = user.Id, InstitutionName = "Archive test bank", Status = status,
            IsArchived = archived, ProtectedPlaidAccessToken = credential };
        db.BankConnections.Add(connection);
        await db.SaveChangesAsync();
        return connection.Id;
    }
}
