using System.Net;
using System.Net.Http.Json;
using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Bills;
using FullWorth.Core.Models;
using FullWorth.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FullWorth.Tests.Security;

public sealed class BillStreamConfirmationRetentionTests
{
    [Theory]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    public async Task ExplicitConfirmationAndEmptyHistoryProtection_PreserveOwnedBillEvidence(
        bool confirm, int remainingPayments)
    {
        await using var factory = new FullWorthApiFactory();
        using var client = factory.CreateHttpsClient();
        var user = await TestUserAuthentication.RegisterAndLoginAsync(client);
        var userId = await TestUserAuthentication.GetUserIdAsync(factory, user.Email);
        TestUserAuthentication.Authorize(client, user);
        const string provider = "Synthetic Monthly Utility";
        Guid streamId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
            for (var month = 1; month <= 3; month++)
            {
                db.BankTransactions.Add(new BankTransactionEntity
                {
                    UserId = userId,
                    BankAccountId = Guid.NewGuid(),
                    PlaidTransactionId = Guid.NewGuid().ToString("N"),
                    Name = provider,
                    MerchantName = provider,
                    Amount = 20m,
                    IsoCurrencyCode = "USD",
                    PostedDate = new DateOnly(2026, month, 5),
                    CategoryPrimary = "RENT_AND_UTILITIES",
                    CategoryDetailed = "RENT_AND_UTILITIES_GAS_AND_ELECTRICITY"
                });
            }
            await db.SaveChangesAsync();

            var discovered = await scope.ServiceProvider
                .GetRequiredService<RecurringBillDiscoveryPersistenceService>()
                .DiscoverAndSaveAsync(userId);
            Assert.Equal(1, discovered.BillStreamsCreated);
            var stream = await db.BillStreams.SingleAsync(s => s.UserId == userId);
            Assert.Equal(BillStreamSource.AutomaticDiscovery, stream.Source);
            streamId = stream.Id;
            Assert.Equal(3, await db.BillTransactionAssociations.CountAsync(a => a.UserId == userId));
        }

        if (confirm)
        {
            using var response = await client.PostAsJsonAsync("/api/bill-streams",
                new { providerName = provider, category = "Utility" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var confirmed = await response.Content.ReadFromJsonAsync<StreamPayload>();
            Assert.NotNull(confirmed);
            Assert.Equal(streamId, confirmed.Id);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
            var transactions = await db.BankTransactions
                .Where(t => t.UserId == userId).OrderByDescending(t => t.PostedDate).ToListAsync();
            foreach (var transaction in transactions.Skip(remainingPayments))
            {
                transaction.IsRemoved = true;
            }
            await db.SaveChangesAsync();

            var result = await scope.ServiceProvider
                .GetRequiredService<RecurringBillDiscoveryPersistenceService>()
                .DiscoverAndSaveAsync(userId);
            Assert.Equal(0, result.BillsDiscovered);
            Assert.Equal(0, result.BillStreamsCreated);
            Assert.Equal(0, result.NewBillAlertsCreated);
            Assert.Equal(confirm || remainingPayments == 0 ? 0 : 1, result.BillStreamsDeactivated);
            Assert.Equal(confirm || remainingPayments == 0 ? 0 : remainingPayments, result.TransactionsUnlinked);
            var stream = await db.BillStreams.SingleAsync(s => s.Id == streamId);
            Assert.Equal(confirm || remainingPayments == 0, stream.IsActive);
            Assert.Equal(confirm ? BillStreamSource.Manual : BillStreamSource.AutomaticDiscovery,
                stream.Source);
            if (confirm || remainingPayments == 0)
            {
                Assert.Equal(3, await db.BillTransactionAssociations.CountAsync(a =>
                    a.UserId == userId && a.BillStreamId == streamId));
            }
        }

        using var listResponse = await client.GetAsync("/api/bill-streams");
        listResponse.EnsureSuccessStatusCode();
        var active = await listResponse.Content.ReadFromJsonAsync<List<StreamPayload>>();
        Assert.NotNull(active);
        if (confirm || remainingPayments == 0)
        {
            Assert.Equal(streamId, Assert.Single(active).Id);
        }
        else
        {
            Assert.Empty(active);
        }

        using var historyResponse = await client.GetAsync("/api/bill-streams?includeInactive=true");
        historyResponse.EnsureSuccessStatusCode();
        var history = await historyResponse.Content.ReadFromJsonAsync<List<StreamPayload>>();
        Assert.NotNull(history);
        Assert.Equal(streamId, Assert.Single(history).Id);
    }

    [Theory]
    [InlineData(BillStreamSource.AutomaticDiscovery, true)]
    [InlineData(BillStreamSource.AutomaticDiscovery, false)]
    [InlineData(BillStreamSource.Unknown, true)]
    [InlineData(BillStreamSource.Unknown, false)]
    [InlineData(BillStreamSource.Manual, true)]
    [InlineData(BillStreamSource.Manual, false)]
    public async Task ExplicitCreate_ConfirmsExistingStream_AndIsIdempotent(
        BillStreamSource source, bool isActive)
    {
        await using var factory = new FullWorthApiFactory();
        using var client = factory.CreateHttpsClient();
        var user = await TestUserAuthentication.RegisterAndLoginAsync(client);
        var userId = await TestUserAuthentication.GetUserIdAsync(factory, user.Email);
        TestUserAuthentication.Authorize(client, user);
        var id = await SeedStreamAsync(factory, userId, source, isActive);
        DateTimeOffset confirmedAt;

        using (var response = await client.PostAsJsonAsync("/api/bill-streams",
                   new { providerName = "  SYNTHETIC CONFIRMATION PROVIDER  ", category = "Utility" }))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var payload = await response.Content.ReadFromJsonAsync<StreamPayload>();
            Assert.NotNull(payload);
            Assert.Equal(id, payload.Id);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
            var stream = await db.BillStreams.SingleAsync(s => s.Id == id);
            Assert.Equal(BillStreamSource.Manual, stream.Source);
            Assert.True(stream.IsActive);
            Assert.Equal(BillCategory.Utility, stream.Category);
            confirmedAt = stream.UpdatedAtUtc;
        }

        using var repeated = await client.PostAsJsonAsync("/api/bill-streams",
            new { providerName = "Synthetic Confirmation Provider", category = "Utility" });
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);

        await using var verification = factory.Services.CreateAsyncScope();
        var verify = verification.ServiceProvider.GetRequiredService<FullWorthDbContext>();
        var saved = Assert.Single(await verify.BillStreams.Where(s => s.UserId == userId).ToListAsync());
        Assert.Equal(id, saved.Id);
        Assert.Equal(BillStreamSource.Manual, saved.Source);
        Assert.Equal(confirmedAt, saved.UpdatedAtUtc);
        Assert.Empty(await verify.BillAlerts.Where(a => a.UserId == userId).ToListAsync());
        Assert.Empty(await verify.BillTransactionAssociations.Where(a => a.UserId == userId).ToListAsync());
    }

    [Fact]
    public async Task Confirmation_CannotChangeAnotherOwnersMatchingProvider()
    {
        await using var factory = new FullWorthApiFactory();
        using var ownerClient = factory.CreateHttpsClient();
        using var otherClient = factory.CreateHttpsClient();
        var owner = await TestUserAuthentication.RegisterAndLoginAsync(ownerClient);
        var other = await TestUserAuthentication.RegisterAndLoginAsync(otherClient);
        var ownerId = await TestUserAuthentication.GetUserIdAsync(factory, owner.Email);
        var otherId = await TestUserAuthentication.GetUserIdAsync(factory, other.Email);
        var ownerStream = await SeedStreamAsync(factory, ownerId, BillStreamSource.AutomaticDiscovery, true);
        var otherStream = await SeedStreamAsync(factory, otherId, BillStreamSource.AutomaticDiscovery, false);
        TestUserAuthentication.Authorize(ownerClient, owner);

        using var response = await ownerClient.PostAsJsonAsync("/api/bill-streams",
            new { providerName = "Synthetic Confirmation Provider", category = "Utility" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<StreamPayload>();
        Assert.NotNull(payload);
        Assert.Equal(ownerStream, payload.Id);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
        var foreign = await db.BillStreams.SingleAsync(s => s.Id == otherStream);
        Assert.Equal(BillStreamSource.AutomaticDiscovery, foreign.Source);
        Assert.False(foreign.IsActive);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), foreign.UpdatedAtUtc);
        Assert.Equal(2, await db.BillStreams.CountAsync());

        using var foreignResponse = await ownerClient.GetAsync($"/api/bill-streams/{otherStream}");
        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
    }

    private static async Task<Guid> SeedStreamAsync(
        FullWorthApiFactory factory, Guid userId, BillStreamSource source, bool isActive)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
        var timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var stream = new BillStreamEntity
        {
            UserId = userId,
            ProviderName = "Synthetic Confirmation Provider",
            Category = BillCategory.Utility,
            Source = source,
            IsActive = isActive,
            CreatedAtUtc = timestamp,
            UpdatedAtUtc = timestamp
        };
        db.BillStreams.Add(stream);
        await db.SaveChangesAsync();
        return stream.Id;
    }

    private sealed record StreamPayload(Guid Id);
}
