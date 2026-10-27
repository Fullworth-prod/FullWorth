using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Bills;
using FullWorth.API.Services.Plaid;
using FullWorth.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.Tests.Services;

public sealed class DiscoveryCurrencyAmbiguityRetentionTests
{
    [Theory]
    [InlineData("EUR")]
    [InlineData(null)]
    [InlineData("?")]
    public async Task CurrencyConflictCannotDeactivateOrUnlinkAnExistingObligation(string? conflictingCurrency)
    {
        await using var db = new FullWorthDbContext(new DbContextOptionsBuilder<FullWorthDbContext>()
            .UseInMemoryDatabase("currency-retention-" + Guid.NewGuid()).Options);
        var owner = Guid.NewGuid();
        var account = Guid.NewGuid();
        var stream = new BillStreamEntity { UserId = owner, ProviderName = "Midco",
            Category = BillCategory.Internet, Source = BillStreamSource.AutomaticDiscovery, IsActive = true };
        db.BillStreams.Add(stream);
        BankTransactionEntity? conflicting = null;
        for (var month = 1; month <= 3; month++)
        {
            var charge = Charge(owner, account, new(2026, month, 5), "Midco", 100m,
                month == 3 ? conflictingCurrency : "USD");
            db.BankTransactions.Add(charge);
            db.BillTransactionAssociations.Add(new BillTransactionAssociationEntity {
                UserId = owner, BillStreamId = stream.Id, BankTransactionId = charge.Id });
            if (month == 3) conflicting = charge;
        }
        // A wide import window is not proof that contradictory units cancel a bill.
        db.BankTransactions.AddRange(
            Charge(owner, account, new(2025, 12, 1), "Unrelated start", 11m, "USD"),
            Charge(owner, account, new(2026, 4, 1), "Unrelated end", 12m, "USD"));
        await db.SaveChangesAsync();
        var originalLinks = await db.BillTransactionAssociations.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync();
        db.ChangeTracker.Clear();
        var service = new RecurringBillDiscoveryPersistenceService(db,
            new PlaidBankTransactionDiscoveryGateway(db), new BillTransactionAssociationGateway(db));
        var result = await service.DiscoverAndSaveAsync(owner);
        Assert.Equal(1, result.Diagnostics!.RejectedCurrencyAmbiguity);
        Assert.Equal(0, result.BillsDiscovered);
        Assert.Equal(0, result.BillStreamsDeactivated);
        Assert.Equal(0, result.TransactionsUnlinked);
        Assert.True((await db.BillStreams.SingleAsync()).IsActive);
        Assert.Equal(originalLinks, await db.BillTransactionAssociations.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
        Assert.Empty(await db.BillAlerts.ToListAsync());
        Assert.Equal(conflictingCurrency, (await db.BankTransactions.FindAsync(conflicting!.Id))!.IsoCurrencyCode);

        // Once actual source evidence becomes consistent, normal rediscovery resumes.
        (await db.BankTransactions.FindAsync(conflicting.Id))!.IsoCurrencyCode = "USD";
        await db.SaveChangesAsync();
        var repaired = await service.DiscoverAndSaveAsync(owner);
        Assert.Equal(1, repaired.BillsDiscovered);
        Assert.Equal(0, repaired.BillStreamsCreated);
        Assert.Equal(0, repaired.NewBillAlertsCreated);
        Assert.True((await db.BillStreams.SingleAsync()).IsActive);
        Assert.Equal(originalLinks, await db.BillTransactionAssociations.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
    }

    private static BankTransactionEntity Charge(Guid owner, Guid account, DateOnly date,
        string provider, decimal amount, string? currency) => new()
    {
        UserId = owner, BankAccountId = account,
        PlaidTransactionId = Guid.NewGuid().ToString("N"), PostedDate = date,
        Name = provider, MerchantName = provider, Amount = amount, IsoCurrencyCode = currency
    };
}
