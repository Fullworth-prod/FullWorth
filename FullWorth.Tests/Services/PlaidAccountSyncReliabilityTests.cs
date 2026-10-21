using System.Net;
using System.Text;
using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Bills;
using FullWorth.API.Services.Plaid;
using FullWorth.Tests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace FullWorth.Tests.Services;

public sealed class PlaidAccountSyncReliabilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedSave_RestoresBatchAndCallerEdits_ThenAllowsRetry(bool cancel)
    {
        var failure = new FailNextSave { Cancel = cancel };
        await using var db = CreateDb(failure);
        var protector = new PlaidTokenProtector(new EphemeralDataProtectionProvider());
        var userId = Guid.NewGuid();
        var connection = new BankConnectionEntity
        {
            UserId = userId, ProtectedPlaidAccessToken = protector.Protect("synthetic-access-token"),
            Status = BankConnectionStatus.RequiresAttention, TransactionsCursor = "existing-cursor",
            LastSuccessfulSyncAtUtc = DateTimeOffset.UtcNow.AddDays(-2)
        };
        var retained = Account(userId, connection.Id, "retained");
        var missing = Account(userId, connection.Id, "missing");
        var unrelated = Account(Guid.NewGuid(), Guid.NewGuid(), "unrelated");
        db.BankConnections.Add(connection);
        db.BankAccounts.AddRange(retained, missing, unrelated);
        await db.SaveChangesAsync();
        var oldSync = connection.LastSuccessfulSyncAtUtc;
        var oldUpdated = connection.UpdatedAtUtc;
        var oldAccountUpdated = retained.UpdatedAtUtc;
        retained.Mask = "9876";
        unrelated.Name = "Caller edit";
        using var handler = new ScriptedHttpMessageHandler(Response(), Response());
        using var http = new HttpClient(handler);
        var service = Service(db, protector, http);
        failure.Armed = true;

        if (cancel)
            await Assert.ThrowsAsync<OperationCanceledException>(() => service.SyncAccountsAsync(userId, connection.Id));
        else
            await Assert.ThrowsAsync<DbUpdateException>(() => service.SyncAccountsAsync(userId, connection.Id));

        Assert.Equal(BankConnectionStatus.RequiresAttention, connection.Status);
        Assert.Equal(oldSync, connection.LastSuccessfulSyncAtUtc);
        Assert.Equal(oldUpdated, connection.UpdatedAtUtc);
        Assert.Equal("Original retained", retained.Name);
        Assert.Equal("9876", retained.Mask);
        Assert.Equal(oldAccountUpdated, retained.UpdatedAtUtc);
        Assert.True(missing.IsActive);
        Assert.DoesNotContain(db.ChangeTracker.Entries<BankAccountEntity>(),
            entry => entry.Entity.PlaidAccountId == "new-account");

        await db.SaveChangesAsync();
        Assert.Equal(3, await db.BankAccounts.CountAsync());
        Assert.True((await db.BankAccounts.AsNoTracking().SingleAsync(a => a.Id == missing.Id)).IsActive);
        var stored = await db.BankAccounts.AsNoTracking().SingleAsync(a => a.Id == retained.Id);
        Assert.Equal("Original retained", stored.Name);
        Assert.Equal("9876", stored.Mask);
        Assert.Equal("Caller edit",
            (await db.BankAccounts.AsNoTracking().SingleAsync(a => a.Id == unrelated.Id)).Name);

        Assert.Equal(2, await service.SyncAccountsAsync(userId, connection.Id));
        Assert.Equal(4, await db.BankAccounts.CountAsync());
        Assert.Equal("Provider retained", retained.Name);
        Assert.False(missing.IsActive);
        Assert.Equal(BankConnectionStatus.Active, connection.Status);
        Assert.Equal("existing-cursor", connection.TransactionsCursor);
        Assert.Equal(oldSync, connection.LastSuccessfulSyncAtUtc);
        Assert.Equal("synthetic-access-token", protector.Unprotect(connection.ProtectedPlaidAccessToken!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AccountSuccess_DoesNotHideStaleOrNeverSyncedTransactions(bool neverSynced)
    {
        await using var db = CreateDb(new FailNextSave());
        var protector = new PlaidTokenProtector(new EphemeralDataProtectionProvider());
        var oldSync = neverSynced ? (DateTimeOffset?)null : DateTimeOffset.UtcNow.AddDays(-2);
        var connection = new BankConnectionEntity
        {
            UserId = Guid.NewGuid(), ProtectedPlaidAccessToken = protector.Protect("synthetic-access-token"),
            LastSuccessfulSyncAtUtc = oldSync
        };
        db.BankConnections.Add(connection);
        await db.SaveChangesAsync();
        using var handler = new ScriptedHttpMessageHandler(Response());
        using var http = new HttpClient(handler);
        await Service(db, protector, http).SyncAccountsAsync(connection.UserId, connection.Id);

        Assert.Equal(oldSync, (await db.BankConnections.AsNoTracking().SingleAsync()).LastSuccessfulSyncAtUtc);
        Assert.Contains(connection.UserId,
            await new BillMonitoringRefreshScheduler(new PlaidBankConnectionReadGateway(db))
                .GetDueUserIdsAsync(DateTimeOffset.UtcNow, TimeSpan.FromHours(6), 25));
    }

    private static BankAccountEntity Account(Guid userId, Guid connectionId, string providerId) =>
        new() { UserId = userId, BankConnectionId = connectionId, PlaidAccountId = providerId,
            Name = "Original " + providerId, IsActive = true };

    private static FullWorthDbContext CreateDb(SaveChangesInterceptor failure) =>
        new(new DbContextOptionsBuilder<FullWorthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).AddInterceptors(failure).Options);

    private static PlaidAccountSyncService Service(FullWorthDbContext db, PlaidTokenProtector protector, HttpClient http) =>
        new(db, new PlaidApiClient(http, Options.Create(new PlaidOptions
        {
            ClientId = "test-client", Secret = "test-secret", Environment = PlaidOptions.SandboxEnvironment
        })), protector);

    private static HttpResponseMessage Response() =>
        new(HttpStatusCode.OK) { Content = new StringContent(
            """
            {"accounts":[
              {"account_id":"retained","name":"Provider retained","mask":"1234","type":"depository","subtype":"checking"},
              {"account_id":"new-account","name":"New account","mask":"5555","type":"depository","subtype":"savings"}
            ]}
            """, Encoding.UTF8, "application/json") };

    private sealed class FailNextSave : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public bool Cancel { get; init; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armed)
            {
                Armed = false;
                if (Cancel) throw new OperationCanceledException("Synthetic cancellation.");
                throw new DbUpdateException("Synthetic save failure.");
            }
            return ValueTask.FromResult(result);
        }
    }
}
