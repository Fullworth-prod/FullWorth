using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Plaid;
using FullWorth.Tests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace FullWorth.Tests.Services;

public sealed class PlaidTransactionSyncFailureRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedSave_DiscardsOnlySyncChanges_AndRetriesOriginalCursor(bool cancel)
    {
        var failure = new FailNextSaveInterceptor();
        await using var dbContext = CreateDbContext(failure);
        var setup = await SeedConnectionAsync(dbContext);
        var connection = await dbContext.BankConnections.SingleAsync();
        connection.TransactionsCursor = "cursor-original";
        connection.LastSuccessfulSyncAtUtc = DateTimeOffset.Parse("2026-08-01T00:00:00Z");
        var modified = ExistingTransaction(setup.UserId, setup.AccountId, "modified");
        var removed = ExistingTransaction(setup.UserId, setup.AccountId, "removed");
        // Same provider ID for another owner must remain untouched.
        var foreign = ExistingTransaction(Guid.NewGuid(), Guid.NewGuid(), "removed");
        dbContext.BankTransactions.AddRange(modified, removed, foreign);
        await dbContext.SaveChangesAsync();
        var oldSync = connection.LastSuccessfulSyncAtUtc;
        var oldUpdated = connection.UpdatedAtUtc;
        var oldTransactionUpdated = modified.UpdatedAtUtc;
        dbContext.ChangeTracker.Clear();

        // Exercise entities materialized by sync, as well as already tracked ones.
        connection = await dbContext.BankConnections.SingleAsync();
        modified = await dbContext.BankTransactions.SingleAsync(t => t.Id == modified.Id);
        var account = await dbContext.BankAccounts.SingleAsync();
        account.Name = "Caller edit";
        modified.MerchantName = "Caller merchant edit";
        var page = JsonNode.Parse(TransactionPage(setup.PlaidAccountId, "added", "cursor-final", false))!;
        var changed = JsonNode.Parse(TransactionPage(setup.PlaidAccountId, "modified", "unused", false))!["added"]![0]!.DeepClone();
        page["modified"] = new JsonArray(changed);
        page["removed"] = new JsonArray(new JsonObject { ["transaction_id"] = "removed" });
        using var handler = new ScriptedHttpMessageHandler(
            JsonResponse(HttpStatusCode.OK, page.ToJsonString()),
            JsonResponse(HttpStatusCode.OK, page.ToJsonString()));
        using var httpClient = new HttpClient(handler);
        var service = CreateService(dbContext, setup.TokenProtector, httpClient);
        failure.Cancel = cancel;
        failure.Armed = true;

        if (cancel)
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                service.SyncConnectionAsync(setup.UserId, setup.ConnectionId));
        }
        else
        {
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                service.SyncConnectionAsync(setup.UserId, setup.ConnectionId));
        }

        Assert.Equal("cursor-original", connection.TransactionsCursor);
        Assert.Equal(oldSync, connection.LastSuccessfulSyncAtUtc);
        Assert.Equal(oldUpdated, connection.UpdatedAtUtc);
        Assert.Equal(10m, modified.Amount);
        Assert.Equal(oldTransactionUpdated, modified.UpdatedAtUtc);
        Assert.Equal("Caller merchant edit", modified.MerchantName);
        Assert.DoesNotContain(dbContext.ChangeTracker.Entries<BankTransactionEntity>(),
            entry => entry.Entity.PlaidTransactionId == "added");
        Assert.False((await dbContext.BankTransactions.SingleAsync(t => t.Id == removed.Id)).IsRemoved);

        // A different operation saves this shared context after the failure.
        // It must persist caller work without committing any failed sync state.
        await dbContext.SaveChangesAsync();
        var storedConnection = await dbContext.BankConnections.AsNoTracking().SingleAsync();
        Assert.Equal("cursor-original", storedConnection.TransactionsCursor);
        Assert.Equal(oldSync, storedConnection.LastSuccessfulSyncAtUtc);
        Assert.Equal("Caller edit", (await dbContext.BankAccounts.AsNoTracking().SingleAsync()).Name);
        var storedModified = await dbContext.BankTransactions.AsNoTracking().SingleAsync(t => t.Id == modified.Id);
        Assert.Equal(10m, storedModified.Amount);
        Assert.Equal("Caller merchant edit", storedModified.MerchantName);
        Assert.Equal(3, await dbContext.BankTransactions.CountAsync());
        Assert.False((await dbContext.BankTransactions.AsNoTracking().SingleAsync(t => t.Id == removed.Id)).IsRemoved);

        var result = await service.SyncConnectionAsync(setup.UserId, setup.ConnectionId);
        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Modified);
        Assert.Equal(1, result.Removed);
        Assert.All(handler.Requests, request =>
            Assert.Equal("cursor-original", JsonDocument.Parse(request.Body).RootElement.GetProperty("cursor").GetString()));
        Assert.Equal("cursor-final", connection.TransactionsCursor);
        Assert.Equal(79.99m, modified.Amount);
        Assert.True((await dbContext.BankTransactions.AsNoTracking().SingleAsync(t => t.Id == removed.Id)).IsRemoved);
        Assert.False((await dbContext.BankTransactions.AsNoTracking().SingleAsync(t => t.Id == foreign.Id)).IsRemoved);
        Assert.Equal(4, await dbContext.BankTransactions.CountAsync());
        Assert.Equal(1, await dbContext.BankTransactions.CountAsync(t => t.PlaidTransactionId == "added"));
    }

    private static BankTransactionEntity ExistingTransaction(Guid userId, Guid accountId, string providerId) =>
        new()
        {
            UserId = userId,
            BankAccountId = accountId,
            PlaidTransactionId = providerId,
            Name = "Original charge",
            Amount = 10m,
            IsoCurrencyCode = "USD",
            PostedDate = new DateOnly(2026, 8, 1)
        };

    private sealed class FailNextSaveInterceptor : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public bool Cancel { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Armed)
            {
                Armed = false;
                if (Cancel)
                {
                    throw new OperationCanceledException("Synthetic canceled save.");
                }

                throw new DbUpdateException("Synthetic failed save.");
            }

            return ValueTask.FromResult(result);
        }
    }

    private static PlaidTransactionSyncService CreateService(
        FullWorthDbContext dbContext,
        PlaidTokenProtector tokenProtector,
        HttpClient httpClient)
    {
        return new PlaidTransactionSyncService(
            dbContext,
            new PlaidApiClient(
                httpClient,
                Options.Create(
                    new PlaidOptions
                    {
                        ClientId =
                            "test-client-id",

                        Secret =
                            "test-secret",

                        Environment =
                            PlaidOptions.SandboxEnvironment
                    })),
            tokenProtector);
    }

    private static async Task<ConnectionSetup> SeedConnectionAsync(
        FullWorthDbContext dbContext)
    {
        var userId =
            Guid.NewGuid();

        var tokenProtector =
            new PlaidTokenProtector(
                new EphemeralDataProtectionProvider());

        var connection =
            new BankConnectionEntity
            {
                UserId =
                    userId,

                InstitutionName =
                    "Test Bank",

                PlaidItemId =
                    "item-test",

                ProtectedPlaidAccessToken =
                    tokenProtector.Protect(
                        "access-sandbox-test"),

                Status =
                    BankConnectionStatus.Active
            };

        var account =
            new BankAccountEntity
            {
                UserId =
                    userId,

                BankConnectionId =
                    connection.Id,

                PlaidAccountId =
                    "account-test",

                Name =
                    "Checking",

                AccountType =
                    BankAccountType.Checking,

                IsActive =
                    true
            };

        dbContext.BankConnections.Add(
            connection);

        dbContext.BankAccounts.Add(
            account);

        await dbContext.SaveChangesAsync();

        return new ConnectionSetup(
            userId,
            connection.Id,
            account.Id,
            account.PlaidAccountId,
            tokenProtector);
    }

    private static FullWorthDbContext CreateDbContext(SaveChangesInterceptor interceptor)
    {
        return new FullWorthDbContext(
            new DbContextOptionsBuilder<FullWorthDbContext>()
                .AddInterceptors(interceptor)
                .UseInMemoryDatabase(
                    Guid.NewGuid()
                        .ToString(
                            "N"))
                .Options);
    }

    private static string TransactionPage(
        string accountId,
        string transactionId,
        string nextCursor,
        bool hasMore)
    {
        return $$"""
                 {
                   "added": [
                     {
                       "transaction_id": "{{transactionId}}",
                       "account_id": "{{accountId}}",
                       "name": "Internet Service",
                       "merchant_name": "Example Telecom",
                       "amount": 79.99,
                       "iso_currency_code": "usd",
                       "date": "2026-08-30",
                       "authorized_date": null,
                       "pending": false,
                       "personal_finance_category": {
                         "primary": "GENERAL_SERVICES",
                         "detailed": "GENERAL_SERVICES_TELECOM"
                       }
                     }
                   ],
                   "modified": [],
                   "removed": [],
                   "next_cursor": "{{nextCursor}}",
                   "has_more": {{hasMore.ToString().ToLowerInvariant()}}
                 }
                 """;
    }

    private static string EmptyTransactionPage(
        string nextCursor,
        bool hasMore)
    {
        return $$"""
                 {
                   "added": [],
                   "modified": [],
                   "removed": [],
                   "next_cursor": "{{nextCursor}}",
                   "has_more": {{hasMore.ToString().ToLowerInvariant()}}
                 }
                 """;
    }

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        string json)
    {
        return new HttpResponseMessage(
            statusCode)
        {
            Content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json")
        };
    }

    private sealed record ConnectionSetup(
        Guid UserId,
        Guid ConnectionId,
        Guid AccountId,
        string PlaidAccountId,
        PlaidTokenProtector TokenProtector);
}
