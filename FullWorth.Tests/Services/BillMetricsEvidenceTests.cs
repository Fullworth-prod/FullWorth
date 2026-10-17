using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Bills;
using FullWorth.API.Services.Plaid;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.Tests.Services;

public sealed class BillMetricsEvidenceTests
{
    [Theory]
    [InlineData(31, true)]
    [InlineData(7, false)]
    [InlineData(14, false)]
    [InlineData(90, false)]
    public async Task Metrics_RequirePositivePostedEvidence_AndMonthlyTiming(int interval, bool monthly)
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var foreign = Guid.NewGuid();
        var stream = new BillStreamEntity { UserId = owner, ProviderName = "Synthetic bill" };
        db.BillStreams.Add(stream);
        Add(db, owner, stream.Id, 10m, new DateOnly(2026, 1, 1));
        Add(db, owner, stream.Id, 20m, new DateOnly(2026, 1, 1).AddDays(interval));
        Add(db, owner, stream.Id, -500m, new DateOnly(2026, 12, 1));
        Add(db, owner, stream.Id, 0m, new DateOnly(2026, 12, 2));
        Add(db, owner, stream.Id, 900m, new DateOnly(2026, 12, 3), pending: true);
        Add(db, owner, stream.Id, 800m, new DateOnly(2026, 12, 4), removed: true);
        await db.SaveChangesAsync();

        var gateway = new BillTransactionMetricsGateway(db, new PlaidBankTransactionMetricFactsGateway(db));
        var metrics = (await gateway.GetMetricsAsync(owner, [stream.Id]))[stream.Id];
        Assert.Equal(20m, metrics.CurrentAmount);
        Assert.Equal(10m, metrics.PreviousAverage);
        Assert.Equal(monthly, metrics.HasMonthlyCadenceEvidence);
        Assert.Empty(await gateway.GetMetricsAsync(foreign, [stream.Id]));
    }

    [Fact]
    public async Task CreditsAndZeroAmountsAlone_DoNotProduceAmountEvidence()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var stream = new BillStreamEntity { UserId = owner, ProviderName = "Synthetic bill" };
        db.BillStreams.Add(stream);
        Add(db, owner, stream.Id, -10m, new DateOnly(2026, 1, 1));
        Add(db, owner, stream.Id, 0m, new DateOnly(2026, 2, 1));
        await db.SaveChangesAsync();
        var facts = new PlaidBankTransactionMetricFactsGateway(db);
        Assert.Empty(await facts.GetAsync(owner, db.BankTransactions.Select(t => t.Id).ToArray()));
        Assert.Empty(await new BillTransactionMetricsGateway(db, facts).GetMetricsAsync(owner, [stream.Id]));
    }

    [Fact]
    public async Task OnePayment_HasAnAmountButNoMonthlyEvidence()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var stream = new BillStreamEntity { UserId = owner, ProviderName = "Synthetic bill" };
        db.BillStreams.Add(stream);
        Add(db, owner, stream.Id, 10m, new DateOnly(2026, 1, 1));
        await db.SaveChangesAsync();
        var metrics = (await new BillTransactionMetricsGateway(db,
            new PlaidBankTransactionMetricFactsGateway(db)).GetMetricsAsync(owner, [stream.Id]))[stream.Id];
        Assert.Equal(10m, metrics.CurrentAmount);
        Assert.Equal(0m, metrics.PreviousAverage);
        Assert.False(metrics.HasMonthlyCadenceEvidence);
    }

    private static FullWorthDbContext CreateDb() => new(
        new DbContextOptionsBuilder<FullWorthDbContext>()
            .UseInMemoryDatabase($"metrics-evidence-{Guid.NewGuid():N}").Options);

    private static void Add(FullWorthDbContext db, Guid owner, Guid streamId,
        decimal amount, DateOnly date, bool pending = false, bool removed = false)
    {
        var transaction = new BankTransactionEntity
        {
            UserId = owner, BankAccountId = Guid.NewGuid(),
            PlaidTransactionId = Guid.NewGuid().ToString("N"),
            Name = "Synthetic bill", MerchantName = "Synthetic bill",
            IsoCurrencyCode = "USD", Amount = amount, PostedDate = date, IsPending = pending, IsRemoved = removed
        };
        db.BankTransactions.Add(transaction);
        db.BillTransactionAssociations.Add(new BillTransactionAssociationEntity
        {
            UserId = owner, BankTransactionId = transaction.Id, BillStreamId = streamId
        });
    }
}
