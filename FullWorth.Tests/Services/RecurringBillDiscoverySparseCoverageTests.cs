using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Bills;
using FullWorth.API.Services.Plaid;
using FullWorth.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.Tests.Services;

public sealed class RecurringBillDiscoverySparseCoverageTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task PartialOwnerHistory_DoesNotDeactivateExistingBillOrUnlinkEvidence(
        int unrelatedPostedCharges)
    {
        await using var db = new FullWorthDbContext(
            new DbContextOptionsBuilder<FullWorthDbContext>()
                .UseInMemoryDatabase($"discovery-partial-coverage-{Guid.NewGuid():N}")
                .Options);

        var owner = Guid.NewGuid();
        var anotherOwner = Guid.NewGuid();
        var originalUpdatedAt = DateTimeOffset.UtcNow.AddDays(-90);
        var knownBill = new BillStreamEntity
        {
            UserId = owner,
            ProviderName = "Synthetic Internet",
            Category = BillCategory.Utility,
            Source = BillStreamSource.AutomaticDiscovery,
            IsActive = true,
            CreatedAtUtc = originalUpdatedAt,
            UpdatedAtUtc = originalUpdatedAt
        };
        db.BillStreams.Add(knownBill);

        var knownPayment = PostedCharge(
            owner, "Synthetic Internet", new DateOnly(2026, 1, 5),
            60m, "RENT_AND_UTILITIES");
        db.BankTransactions.Add(knownPayment);
        db.BillTransactionAssociations.Add(new BillTransactionAssociationEntity
        {
            UserId = owner,
            BankTransactionId = knownPayment.Id,
            BillStreamId = knownBill.Id,
            CreatedAtUtc = originalUpdatedAt,
            UpdatedAtUtc = originalUpdatedAt
        });

        var unrelatedDates = new[]
        {
            new DateOnly(2026, 1, 12),
            new DateOnly(2026, 1, 20),
            new DateOnly(2026, 2, 4)
        };
        for (var index = 0; index < unrelatedPostedCharges; index++)
        {
            db.BankTransactions.Add(PostedCharge(
                owner, $"Unrelated One-Time Purchase {index}", unrelatedDates[index],
                11m + index, "FOOD_AND_DRINK"));
        }

        // Another owner's complete history cannot supply this owner's missing
        // coverage, even if it contains a valid monthly recurring stream.
        foreach (var month in new[] { 1, 2, 3 })
        {
            db.BankTransactions.Add(PostedCharge(
                anotherOwner, "Another Owner's Utility",
                new DateOnly(2026, month, 5), 35m, "RENT_AND_UTILITIES"));
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = new RecurringBillDiscoveryPersistenceService(
            db,
            new PlaidBankTransactionDiscoveryGateway(db),
            new BillTransactionAssociationGateway(db));

        var result = await service.DiscoverAndSaveAsync(owner);

        Assert.Equal(unrelatedPostedCharges + 1, result.TransactionsAnalyzed);
        Assert.Equal(0, result.BillsDiscovered);
        Assert.Equal(0, result.BillStreamsDeactivated);
        Assert.Equal(0, result.TransactionsUnlinked);
        Assert.Equal(0, result.NewBillAlertsCreated);

        db.ChangeTracker.Clear();
        var retained = await db.BillStreams.SingleAsync();
        Assert.True(retained.IsActive);
        Assert.Equal(BillStreamSource.AutomaticDiscovery, retained.Source);
        Assert.Equal(originalUpdatedAt, retained.UpdatedAtUtc);

        var association = await db.BillTransactionAssociations.SingleAsync();
        Assert.Equal(owner, association.UserId);
        Assert.Equal(knownPayment.Id, association.BankTransactionId);
        Assert.Equal(knownBill.Id, association.BillStreamId);
        Assert.Empty(db.BillAlerts);
    }

    private static BankTransactionEntity PostedCharge(
        Guid owner, string merchant, DateOnly postedDate, decimal amount,
        string category) => new()
    {
        Id = Guid.NewGuid(),
        UserId = owner,
        BankAccountId = Guid.NewGuid(),
        PlaidTransactionId = Guid.NewGuid().ToString("N"),
        Name = merchant,
        MerchantName = merchant,
        Amount = amount,
        IsoCurrencyCode = "USD",
        PostedDate = postedDate,
        AuthorizedDate = postedDate,
        CategoryPrimary = category,
        IsPending = false,
        IsRemoved = false
    };
}
