using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Bills;
using FullWorth.API.Services.Contracts;
using FullWorth.API.Services.Plaid;
using FullWorth.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.Tests.RelationalBankLifecycle;

// Runs only after the parent executable's explicit disposable loopback fixture
// guard and schema initialization. Each case uses distinct owners and records;
// no production access, external provider call or cleanup/delete command.
internal static class BillAmbiguousHistoryChecks
{
    private static readonly DateTimeOffset OriginalTime =
        new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    internal static async Task<int> RunAsync(DbContextOptions<FullWorthDbContext> options)
    {
        var count = 0;
        foreach (var kind in new[] {
            "same-unit-duplicate", "currency-conflict", "unknown-currency",
            "account-conflict", "unknown-account" })
        foreach (var active in new[] { true, false })
        {
            await RunCaseAsync(options, kind, active);
            count++;
        }
        await RunCaseAsync(options, "normal", true);
        count++;
        await RunCaseAsync(options, "foreign-duplicate", true);
        count++;
        Console.WriteLine($"PostgreSQL bill ambiguity retention passed: {count} cases; durable original links/state, no new alerts and owner isolation.");
        return count;
    }

    private static async Task RunCaseAsync(DbContextOptions<FullWorthDbContext> options,
        string kind, bool active)
    {
        await using var db = new FullWorthDbContext(options);
        var owner = new ApplicationUser { Id = Guid.NewGuid(), UserName = Guid.NewGuid().ToString("N") };
        var foreign = new ApplicationUser { Id = Guid.NewGuid(), UserName = Guid.NewGuid().ToString("N") };
        var protector = new PlaidTokenProtector(new EphemeralDataProtectionProvider());
        BankConnectionEntity Bank(Guid user) => new() {
            Id = Guid.NewGuid(), UserId = user, InstitutionName = "Synthetic retention bank",
            PlaidItemId = Guid.NewGuid().ToString("N"),
            ProtectedPlaidAccessToken = protector.Protect("synthetic-retention-fixture"),
            Status = BankConnectionStatus.Active
        };
        var bank = Bank(owner.Id);
        var foreignBank = Bank(foreign.Id);
        BankAccountEntity Account(Guid user, Guid connection) => new() {
            Id = Guid.NewGuid(), UserId = user, BankConnectionId = connection,
            PlaidAccountId = Guid.NewGuid().ToString("N"),
            Name = "Synthetic retention account", IsActive = true
        };
        var account = Account(owner.Id, bank.Id);
        var secondAccount = Account(owner.Id, bank.Id);
        var foreignAccount = Account(foreign.Id, foreignBank.Id);
        var stream = new BillStreamEntity {
            Id = Guid.NewGuid(), UserId = owner.Id, ProviderName = "Synthetic recurring service",
            Category = BillCategory.Other, Source = BillStreamSource.AutomaticDiscovery,
            IsActive = active, CreatedAtUtc = OriginalTime, UpdatedAtUtc = OriginalTime
        };
        db.AddRange(owner, foreign, bank, foreignBank, account, secondAccount, foreignAccount, stream);
        var originalIds = new List<Guid>();
        for (var month = 1; month <= 4; month++)
        {
            var charge = Charge(owner.Id, account.Id, new DateOnly(2026, month, 5));
            originalIds.Add(charge.Id);
            db.Add(charge);
            db.Add(new BillTransactionAssociationEntity {
                UserId = owner.Id, BillStreamId = stream.Id, BankTransactionId = charge.Id,
                CreatedAtUtc = OriginalTime, UpdatedAtUtc = OriginalTime
            });
        }
        Guid? unknownAccountTransaction = null;
        if (kind != "normal")
        {
            var extra = Charge(owner.Id, account.Id, new DateOnly(2026, 4, 5));
            if (kind == "currency-conflict") extra.IsoCurrencyCode = "EUR";
            if (kind == "unknown-currency") extra.IsoCurrencyCode = null;
            if (kind == "account-conflict") extra.BankAccountId = secondAccount.Id;
            if (kind == "unknown-account") unknownAccountTransaction = extra.Id;
            if (kind == "foreign-duplicate")
            {
                extra.UserId = foreign.Id;
                extra.BankAccountId = foreignAccount.Id;
                extra.IsoCurrencyCode = "EUR";
            }
            db.Add(extra);
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        IBankTransactionDiscoveryGateway gateway = unknownAccountTransaction is { } unknownId
            ? new UnknownAccountGateway(db, unknownId)
            : new PlaidBankTransactionDiscoveryGateway(db);
        var service = new RecurringBillDiscoveryPersistenceService(db,
            gateway, new BillTransactionAssociationGateway(db));
        var diagnostics = await service.GetDiagnosticsAsync(owner.Id);
        Require(!db.ChangeTracker.HasChanges(), "Read-only diagnostics staged a write.");
        var result = await service.DiscoverAndSaveAsync(owner.Id);
        var normal = kind is "normal" or "foreign-duplicate";
        Require(result.BillsDiscovered == (normal ? 1 : 0), "Unexpected discovery result.");
        Require(result.BillStreamsCreated == 0 && result.BillStreamsUpdated == 0 &&
            result.BillStreamsDeactivated == 0, "Ambiguity or foreign evidence changed the existing obligation.");
        Require(result.TransactionsLinked == 0 && result.TransactionsUnlinked == 0,
            "Original evidence was removed or a rejected transaction attached.");
        Require(diagnostics.DetectedMonthlyCandidates == (normal ? 1 : 0),
            "The case did not exercise the intended cadence gate.");
        db.ChangeTracker.Clear();

        await using var verify = new FullWorthDbContext(options);
        var saved = await verify.BillStreams.SingleAsync(x => x.Id == stream.Id && x.UserId == owner.Id);
        Require(saved.IsActive == active && saved.Source == BillStreamSource.AutomaticDiscovery &&
            saved.UpdatedAtUtc == OriginalTime, "Durable bill state/timestamp changed.");
        var links = await verify.BillTransactionAssociations.Where(x => x.UserId == owner.Id).ToListAsync();
        Require(links.Select(x => x.BankTransactionId).Order().SequenceEqual(originalIds.Order()),
            "Durable original evidence links changed.");
        Require(links.All(x => x.BillStreamId == stream.Id && x.UpdatedAtUtc == OriginalTime),
            "Durable link target/timestamp changed.");
        Require(!await verify.BillAlerts.AnyAsync(x => x.UserId == owner.Id),
            "Ambiguity or normal rediscovery generated an alert.");
        Require(await verify.BillStreams.CountAsync(x => x.UserId == owner.Id) == 1,
            "An extra obligation was created.");
    }

    // The persistence model requires a concrete FK. Unknown source-account
    // evidence belongs to the nullable gateway contract, not an invalid DB row.
    private sealed class UnknownAccountGateway(FullWorthDbContext db, Guid transactionId)
        : IBankTransactionDiscoveryGateway
    {
        public async Task<IReadOnlyList<BankTransactionDiscoveryRecord>> GetDiscoveryTransactionsAsync(
            Guid userId, CancellationToken cancellationToken = default)
        {
            var records = await new PlaidBankTransactionDiscoveryGateway(db)
                .GetDiscoveryTransactionsAsync(userId, cancellationToken);
            return records.Select(record => record.TransactionId == transactionId
                ? record with { BankAccountId = null } : record).ToArray();
        }
    }

    private static BankTransactionEntity Charge(Guid user, Guid account, DateOnly date) => new() {
        Id = Guid.NewGuid(), UserId = user, BankAccountId = account,
        PlaidTransactionId = Guid.NewGuid().ToString("N"),
        Name = "Synthetic recurring service", MerchantName = "Synthetic recurring service",
        Amount = 20m, IsoCurrencyCode = "USD", PostedDate = date,
        CategoryPrimary = "RENT_AND_UTILITIES",
        CategoryDetailed = "RENT_AND_UTILITIES_GAS_AND_ELECTRICITY"
    };

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
