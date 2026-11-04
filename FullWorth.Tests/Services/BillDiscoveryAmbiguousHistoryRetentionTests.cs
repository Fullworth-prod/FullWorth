using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Bills;
using FullWorth.API.Services.Plaid;
using FullWorth.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.Tests.Services;

public sealed class BillDiscoveryAmbiguousHistoryRetentionTests
{
    private static readonly DateTimeOffset OriginalTime =
        new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("same-unit-duplicate", true)]
    [InlineData("same-unit-duplicate", false)]
    [InlineData("currency-conflict", true)]
    [InlineData("currency-conflict", false)]
    [InlineData("unknown-currency", true)]
    [InlineData("unknown-currency", false)]
    [InlineData("account-conflict", true)]
    [InlineData("account-conflict", false)]
    [InlineData("unknown-account", true)]
    [InlineData("unknown-account", false)]
    public async Task DuplicateDateRejection_PreservesExistingBillAndOriginalEvidence(
        string ambiguity, bool active)
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var account = Guid.NewGuid();
        var stream = AddStream(db, owner, active);
        var originalIds = AddLinkedHistory(db, owner, account, stream.Id);
        var extra = Charge(owner, account, new DateOnly(2026, 4, 5));
        if (ambiguity == "currency-conflict") extra.IsoCurrencyCode = "EUR";
        if (ambiguity == "unknown-currency") extra.IsoCurrencyCode = null;
        if (ambiguity == "account-conflict") extra.BankAccountId = Guid.NewGuid();
        if (ambiguity == "unknown-account") extra.BankAccountId = null;
        db.BankTransactions.Add(extra);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = Service(db);
        var before = await service.GetDiagnosticsAsync(owner);
        Assert.Equal(0, before.DetectedMonthlyCandidates);
        Assert.Equal(1, before.GroupsWithoutSupportedMonthlyCadence);
        Assert.Equal(4, await db.BillTransactionAssociations.CountAsync());
        Assert.Empty(db.ChangeTracker.Entries());

        var result = await service.DiscoverAndSaveAsync(owner);
        Assert.Equal(0, result.BillStreamsDeactivated);
        Assert.Equal(0, result.TransactionsUnlinked);
        Assert.Equal(0, result.BillStreamsCreated);
        Assert.Equal(0, result.BillStreamsUpdated);
        Assert.Equal(0, result.TransactionsLinked);
        Assert.Equal(0, result.BillsDiscovered);
        db.ChangeTracker.Clear();

        var saved = Assert.Single(await db.BillStreams.ToListAsync());
        Assert.Equal(stream.Id, saved.Id);
        Assert.Equal(active, saved.IsActive);
        Assert.Equal(BillStreamSource.AutomaticDiscovery, saved.Source);
        Assert.Equal(OriginalTime, saved.UpdatedAtUtc);
        var links = await db.BillTransactionAssociations.ToListAsync();
        Assert.Equal(originalIds.Order(), links.Select(link => link.BankTransactionId).Order());
        Assert.All(links, link => {
            Assert.Equal(owner, link.UserId);
            Assert.Equal(stream.Id, link.BillStreamId);
            Assert.Equal(OriginalTime, link.UpdatedAtUtc);
        });
        Assert.DoesNotContain(links, link => link.BankTransactionId == extra.Id);
        Assert.Empty(await db.BillAlerts.ToListAsync());
    }

    [Theory]
    [InlineData("currency-conflict", true)]
    [InlineData("currency-conflict", false)]
    [InlineData("account-conflict", true)]
    [InlineData("account-conflict", false)]
    public async Task DirectMonthlyConflict_StillPreservesExistingBillAndEvidence(
        string ambiguity, bool active)
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var account = Guid.NewGuid();
        var stream = AddStream(db, owner, active);
        var originalIds = AddLinkedHistory(db, owner, account, stream.Id);
        var extra = Charge(owner, account, new DateOnly(2026, 5, 5));
        if (ambiguity == "currency-conflict") extra.IsoCurrencyCode = "EUR";
        else extra.BankAccountId = Guid.NewGuid();
        db.BankTransactions.Add(extra);
        await db.SaveChangesAsync();

        var result = await Service(db).DiscoverAndSaveAsync(owner);
        Assert.Equal(0, result.BillStreamsDeactivated);
        Assert.Equal(0, result.TransactionsUnlinked);
        Assert.Equal(0, result.BillsDiscovered);
        Assert.Equal(active, stream.IsActive);
        Assert.Equal(originalIds.Order(),
            (await db.BillTransactionAssociations.Select(link => link.BankTransactionId).ToListAsync()).Order());
        Assert.Empty(await db.BillAlerts.ToListAsync());
    }

    [Fact]
    public async Task ForeignDuplicateEvidence_DoesNotBlockNormalOwnedRediscovery()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var account = Guid.NewGuid();
        var stream = AddStream(db, owner, true);
        var originalIds = AddLinkedHistory(db, owner, account, stream.Id);
        var foreign = Charge(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 4, 5));
        foreign.IsoCurrencyCode = "EUR";
        db.BankTransactions.Add(foreign);
        await db.SaveChangesAsync();

        var result = await Service(db).DiscoverAndSaveAsync(owner);
        Assert.Equal(1, result.BillsDiscovered);
        Assert.Equal(0, result.BillStreamsDeactivated);
        Assert.Equal(0, result.TransactionsUnlinked);
        Assert.Equal(0, result.TransactionsLinked);
        Assert.True(stream.IsActive);
        Assert.Equal(originalIds.Order(),
            (await db.BillTransactionAssociations.Select(link => link.BankTransactionId).ToListAsync()).Order());
        Assert.Empty(await db.BillAlerts.ToListAsync());
    }

    [Fact]
    public async Task UnambiguousOwnedHistory_StillRediscoversExistingBill()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var stream = AddStream(db, owner, true);
        AddLinkedHistory(db, owner, Guid.NewGuid(), stream.Id);
        await db.SaveChangesAsync();

        var result = await Service(db).DiscoverAndSaveAsync(owner);
        Assert.Equal(1, result.BillsDiscovered);
        Assert.Equal(0, result.TransactionsUnlinked);
        Assert.Equal(4, await db.BillTransactionAssociations.CountAsync());
        Assert.True(stream.IsActive);
        Assert.Equal(OriginalTime, stream.UpdatedAtUtc);
        Assert.Empty(await db.BillAlerts.ToListAsync());
    }

    [Fact]
    public async Task UnambiguousCoveredStaleHistory_KeepsExistingRetirementPolicy()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var account = Guid.NewGuid();
        var stream = AddStream(db, owner, true);
        var old = Charge(owner, account, new DateOnly(2026, 1, 5));
        db.BankTransactions.Add(old);
        Link(db, owner, stream.Id, old.Id);
        for (var month = 1; month <= 4; month++)
        {
            var other = Charge(owner, account, new DateOnly(2026, month, 10));
            other.Name = other.MerchantName = "Synthetic other utility";
            db.BankTransactions.Add(other);
        }
        await db.SaveChangesAsync();

        var result = await Service(db).DiscoverAndSaveAsync(owner);
        Assert.Equal(1, result.BillStreamsDeactivated);
        Assert.Equal(1, result.TransactionsUnlinked);
        Assert.False(stream.IsActive);
        Assert.DoesNotContain(await db.BillTransactionAssociations.ToListAsync(),
            link => link.BankTransactionId == old.Id);
    }

    private static FullWorthDbContext CreateDb() => new(
        new DbContextOptionsBuilder<FullWorthDbContext>()
            .UseInMemoryDatabase($"ambiguous-history-retention-{Guid.NewGuid():N}").Options);

    private static RecurringBillDiscoveryPersistenceService Service(FullWorthDbContext db) =>
        new(db, new PlaidBankTransactionDiscoveryGateway(db), new BillTransactionAssociationGateway(db));

    private static BillStreamEntity AddStream(FullWorthDbContext db, Guid owner, bool active)
    {
        var stream = new BillStreamEntity {
            Id = Guid.NewGuid(), UserId = owner, ProviderName = "Synthetic recurring service",
            Category = BillCategory.Other, Source = BillStreamSource.AutomaticDiscovery,
            IsActive = active, CreatedAtUtc = OriginalTime, UpdatedAtUtc = OriginalTime
        };
        db.BillStreams.Add(stream);
        return stream;
    }

    private static Guid[] AddLinkedHistory(FullWorthDbContext db, Guid owner, Guid account, Guid stream)
    {
        var ids = new List<Guid>();
        for (var month = 1; month <= 4; month++)
        {
            var transaction = Charge(owner, account, new DateOnly(2026, month, 5));
            db.BankTransactions.Add(transaction);
            Link(db, owner, stream, transaction.Id);
            ids.Add(transaction.Id);
        }
        return ids.ToArray();
    }

    private static void Link(FullWorthDbContext db, Guid owner, Guid stream, Guid transaction) =>
        db.BillTransactionAssociations.Add(new BillTransactionAssociationEntity {
            UserId = owner, BillStreamId = stream, BankTransactionId = transaction,
            CreatedAtUtc = OriginalTime, UpdatedAtUtc = OriginalTime
        });

    private static BankTransactionEntity Charge(Guid owner, Guid? account, DateOnly date) => new() {
        Id = Guid.NewGuid(), UserId = owner, BankAccountId = account,
        PlaidTransactionId = Guid.NewGuid().ToString("N"),
        Name = "Synthetic recurring service", MerchantName = "Synthetic recurring service",
        Amount = 20m, IsoCurrencyCode = "USD", PostedDate = date,
        CategoryPrimary = "RENT_AND_UTILITIES",
        CategoryDetailed = "RENT_AND_UTILITIES_GAS_AND_ELECTRICITY"
    };
}
