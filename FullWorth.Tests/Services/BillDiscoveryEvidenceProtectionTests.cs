using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Bills;
using FullWorth.API.Services.Plaid;
using FullWorth.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.Tests.Services;

public sealed class BillDiscoveryEvidenceProtectionTests
{
    [Theory]
    [InlineData("USD", "EUR")]
    [InlineData("USD", null)]
    public async Task Discovery_DoesNotMergeIncompatibleCurrencyEvidence(string first, string? second)
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        SeedTransactions(db, owner, first, second);
        await db.SaveChangesAsync();
        var result = await Service(db).DiscoverAndSaveAsync(owner);
        Assert.Equal(0, result.BillsDiscovered);
        Assert.Equal(1, result.Diagnostics!.RejectedCurrencyAmbiguity);
        Assert.Empty(db.BillStreams);
        Assert.Empty(db.BillTransactionAssociations);
        Assert.Empty(db.BillAlerts);
    }

    [Theory]
    [InlineData("TRANSFER_OUT")]
    [InlineData("INCOME")]
    [InlineData("LOAN_DISBURSEMENTS")]
    [InlineData("BANK_FEES")]
    public async Task FamiliarProviderName_DoesNotOverrideExplicitNegativeCategory(string category)
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        SeedTransactions(db, owner, "USD", "USD", "Black Hills Energy");
        foreach (var transaction in db.ChangeTracker.Entries<BankTransactionEntity>())
            transaction.Entity.CategoryPrimary = category;
        await db.SaveChangesAsync();
        var result = await Service(db).DiscoverAndSaveAsync(owner);
        Assert.Equal(0, result.BillsDiscovered);
        Assert.Equal(1, result.Diagnostics!.RejectedExplicitFallbackCategories);
        Assert.Empty(db.BillStreams);
        Assert.Empty(db.BillAlerts);
    }

    [Theory]
    [InlineData("LOAN_PAYMENTS_MORTGAGE_PAYMENT", true)]
    [InlineData("LOAN_PAYMENTS_AUTO_PAYMENT", true)]
    [InlineData("LOAN_PAYMENTS_CASH_ADVANCES", false)]
    [InlineData("LOAN_PAYMENTS_EWA", false)]
    public async Task ActualLoanRepaymentsRemainEligible_ButAdvancesDoNot(
        string detailed, bool accepted)
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        SeedTransactions(db, owner, "USD", "USD", "Black Hills Energy");
        foreach (var entry in db.ChangeTracker.Entries<BankTransactionEntity>())
        {
            entry.Entity.CategoryPrimary = "LOAN_PAYMENTS";
            entry.Entity.CategoryDetailed = detailed;
        }
        await db.SaveChangesAsync();
        var result = await Service(db).DiscoverAndSaveAsync(owner);
        Assert.Equal(accepted ? 1 : 0, result.BillsDiscovered);
        Assert.Equal(accepted ? 1 : 0, await db.BillStreams.CountAsync());
    }

    [Fact]
    public async Task Discovery_NeverStealsEvidenceFromAnExistingManualBill()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var manual = new BillStreamEntity { UserId = owner, ProviderName = "Manual obligation",
            Source = BillStreamSource.Manual, IsActive = true };
        db.BillStreams.Add(manual);
        SeedTransactions(db, owner, "USD", "USD");
        foreach (var entry in db.ChangeTracker.Entries<BankTransactionEntity>().ToArray())
            db.BillTransactionAssociations.Add(new BillTransactionAssociationEntity
                { UserId = owner, BankTransactionId = entry.Entity.Id, BillStreamId = manual.Id });
        await db.SaveChangesAsync();
        var result = await Service(db).DiscoverAndSaveAsync(owner);
        Assert.Equal(0, result.BillsDiscovered);
        Assert.Equal(0, result.TransactionsLinked);
        Assert.Equal(0, result.TransactionsUnlinked);
        Assert.Equal(manual.Id, Assert.Single(db.BillStreams).Id);
        Assert.All(db.BillTransactionAssociations, link => Assert.Equal(manual.Id, link.BillStreamId));
        Assert.Empty(db.BillAlerts);
    }

    [Fact]
    public async Task DuplicateNormalizedProviders_ArePreservedWithoutArbitraryRelinking()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var first = new BillStreamEntity { UserId = owner, ProviderName = "Synthetic Subscription",
            Source = BillStreamSource.Manual, IsActive = true };
        var second = new BillStreamEntity { UserId = owner, ProviderName = "SYNTHETIC SUBSCRIPTION AUTOPAY",
            Source = BillStreamSource.AutomaticDiscovery, IsActive = false };
        db.BillStreams.AddRange(first, second);
        SeedTransactions(db, owner, "USD", "USD");
        await db.SaveChangesAsync();
        var result = await Service(db).DiscoverAndSaveAsync(owner);
        Assert.Equal(0, result.BillsDiscovered);
        Assert.Equal(0, result.BillStreamsUpdated);
        Assert.Equal(0, result.TransactionsLinked);
        Assert.True(first.IsActive);
        Assert.False(second.IsActive);
        Assert.Empty(db.BillTransactionAssociations);
        Assert.Empty(db.BillAlerts);
    }

    private static FullWorthDbContext CreateDb() => new(new DbContextOptionsBuilder<FullWorthDbContext>()
        .UseInMemoryDatabase($"discovery-protection-{Guid.NewGuid():N}").Options);
    private static RecurringBillDiscoveryPersistenceService Service(FullWorthDbContext db) =>
        new(db, new PlaidBankTransactionDiscoveryGateway(db), new BillTransactionAssociationGateway(db));
    private static void SeedTransactions(FullWorthDbContext db, Guid owner, string? first, string? second,
        string provider = "Synthetic Subscription")
    {
        for (var month = 1; month <= 3; month++)
            db.BankTransactions.Add(new BankTransactionEntity { UserId = owner, BankAccountId = owner,
                PlaidTransactionId = Guid.NewGuid().ToString("N"), Name = provider, MerchantName = provider,
                Amount = 20m, IsoCurrencyCode = month == 1 ? first : second,
                PostedDate = new DateOnly(2026, month, 5), CategoryPrimary = "RENT_AND_UTILITIES",
                CategoryDetailed = "RENT_AND_UTILITIES_GAS_AND_ELECTRICITY" });
    }
}
