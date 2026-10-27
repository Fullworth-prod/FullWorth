using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Bills;
using FullWorth.API.Services.Contracts;
using FullWorth.API.Services.Plaid;
using FullWorth.Core.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FullWorth.Tests.Services;

public sealed class DiscoverySourceAccountAmbiguityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SeparateSourceAccountsCannotSupplyOneMonthlyObligation(bool secondAccountUnknown)
    {
        await using var fixture = new Fixture();
        var first = fixture.Charge(fixture.Account, new(2026, 1, 5));
        var second = fixture.Charge(Guid.NewGuid(), new(2026, 2, 5));
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var service = fixture.Service(secondAccountUnknown ? second.Id : null);
        var diagnostics = await service.GetDiagnosticsAsync(fixture.Owner);
        Assert.Equal(1, diagnostics.DetectedMonthlyCandidates);
        Assert.Equal(0, diagnostics.AcceptedCandidates);
        Assert.Empty(fixture.Db.ChangeTracker.Entries());
        var result = await service.DiscoverAndSaveAsync(fixture.Owner);
        Assert.Equal(0, result.BillsDiscovered);
        Assert.Equal(0, result.BillStreamsCreated);
        Assert.Equal(0, result.TransactionsLinked);
        Assert.Empty(fixture.Db.BillStreams);
        Assert.Empty(fixture.Db.BillTransactionAssociations);
        Assert.Empty(fixture.Db.BillAlerts);
        Assert.Equal(2, await fixture.Db.BankTransactions.CountAsync());
        Assert.Equal(100m, (await fixture.Db.BankTransactions.FindAsync(first.Id))!.Amount);
        Assert.Equal(100m, (await fixture.Db.BankTransactions.FindAsync(second.Id))!.Amount);
    }

    [Fact]
    public async Task SameSourceAccountStillDiscoversAndForeignAccountsDoNotContaminateIt()
    {
        await using var fixture = new Fixture();
        fixture.Charge(fixture.Account, new(2026, 1, 5));
        fixture.Charge(fixture.Account, new(2026, 2, 5));
        fixture.Charge(Guid.NewGuid(), new(2026, 3, 5), owner: Guid.NewGuid());
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var service = fixture.Service();
        var diagnostics = await service.GetDiagnosticsAsync(fixture.Owner);
        Assert.Equal(1, diagnostics.AcceptedCandidates);
        var result = await service.DiscoverAndSaveAsync(fixture.Owner);
        Assert.Equal(1, result.BillStreamsCreated);
        Assert.Equal(2, result.TransactionsLinked);
        var repeat = await service.DiscoverAndSaveAsync(fixture.Owner);
        Assert.Equal(0, repeat.BillStreamsCreated);
        Assert.Equal(0, repeat.NewBillAlertsCreated);
        Assert.Equal(2, await fixture.Db.BillTransactionAssociations.CountAsync());
    }

    [Fact]
    public async Task ExistingAutomaticBillAndLinksSurviveAnAmbiguousCrossAccountCandidate()
    {
        await using var fixture = new Fixture();
        var other = Guid.NewGuid();
        var first = fixture.Charge(fixture.Account, new(2026, 1, 5));
        var second = fixture.Charge(other, new(2026, 2, 5));
        var bill = new BillStreamEntity { UserId = fixture.Owner, ProviderName = "Midco",
            Category = BillCategory.Internet, Source = BillStreamSource.AutomaticDiscovery,
            IsActive = true };
        fixture.Db.BillStreams.Add(bill);
        foreach (var payment in new[] { first, second })
            fixture.Db.BillTransactionAssociations.Add(new BillTransactionAssociationEntity {
                UserId = fixture.Owner, BankTransactionId = payment.Id, BillStreamId = bill.Id });
        // Both accounts have the old minimum deactivation coverage. Ambiguous
        // identity is still insufficient permission to remove existing evidence.
        foreach (var account in new[] { fixture.Account, other })
        {
            fixture.Charge(account, new(2025, 10, 1), name: "Unrelated start");
            fixture.Charge(account, new(2026, 3, 1), name: "Unrelated end");
        }
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var result = await fixture.Service().DiscoverAndSaveAsync(fixture.Owner);
        Assert.Equal(0, result.BillStreamsDeactivated);
        Assert.Equal(0, result.TransactionsUnlinked);
        Assert.True((await fixture.Db.BillStreams.SingleAsync()).IsActive);
        Assert.Equal(2, await fixture.Db.BillTransactionAssociations.CountAsync());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public FullWorthDbContext Db { get; } = new(new DbContextOptionsBuilder<FullWorthDbContext>()
            .UseInMemoryDatabase("source-account-ambiguity-" + Guid.NewGuid()).Options);
        public Guid Owner { get; } = Guid.NewGuid();
        public Guid Account { get; } = Guid.NewGuid();

        public BankTransactionEntity Charge(Guid account, DateOnly date,
            string name = "Midco", Guid? owner = null)
        {
            var transaction = new BankTransactionEntity { UserId = owner ?? Owner,
                BankAccountId = account, PlaidTransactionId = Guid.NewGuid().ToString("N"),
                Name = name, MerchantName = name, Amount = 100m,
                IsoCurrencyCode = "USD", PostedDate = date };
            Db.BankTransactions.Add(transaction);
            return transaction;
        }

        public RecurringBillDiscoveryPersistenceService Service(Guid? unknownAccountTransaction = null) =>
            new(Db, new AccountMetadataGateway(new PlaidBankTransactionDiscoveryGateway(Db),
                unknownAccountTransaction), new BillTransactionAssociationGateway(Db));
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class AccountMetadataGateway(IBankTransactionDiscoveryGateway inner, Guid? unknown)
        : IBankTransactionDiscoveryGateway
    {
        public async Task<IReadOnlyList<BankTransactionDiscoveryRecord>> GetDiscoveryTransactionsAsync(
            Guid userId, CancellationToken cancellationToken = default) =>
            (await inner.GetDiscoveryTransactionsAsync(userId, cancellationToken)).Select(record =>
                record.TransactionId == unknown ? record with { BankAccountId = null } : record).ToArray();
    }
}
