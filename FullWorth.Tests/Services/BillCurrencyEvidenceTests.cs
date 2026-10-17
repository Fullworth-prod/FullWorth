using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Bills;
using FullWorth.API.Services.Plaid;
using FullWorth.Core.Formatting;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.Tests.Services;

public sealed class BillCurrencyEvidenceTests
{
    [Theory]
    [InlineData("EUR", "EUR", "EUR", true)]
    [InlineData(" usd ", "USD", "USD", true)]
    [InlineData("USD", "EUR", null, false)]
    [InlineData("USD", null, null, false)]
    [InlineData(null, null, null, true)]
    [InlineData("?", "?", null, true)]
    public async Task Metrics_NeverCompareDifferentOrUnknownUnits(
        string? first, string? second, string? expected, bool hasAmount)
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var stream = new BillStreamEntity { UserId = owner, ProviderName = "Synthetic bill" };
        db.BillStreams.Add(stream);
        Add(db, owner, stream.Id, first, 10m, 1);
        Add(db, owner, stream.Id, second, 30m, 2);
        await db.SaveChangesAsync();
        var metrics = await new BillTransactionMetricsGateway(db,
            new PlaidBankTransactionMetricFactsGateway(db)).GetMetricsAsync(owner, [stream.Id]);
        Assert.Equal(hasAmount, metrics.ContainsKey(stream.Id));
        if (hasAmount)
        {
            Assert.Equal(expected, metrics[stream.Id].CurrencyCode);
            Assert.Equal(30m, metrics[stream.Id].CurrentAmount);
            Assert.Equal(expected is null ? 0m : 10m, metrics[stream.Id].PreviousAverage);
            Assert.Equal(expected is not null, metrics[stream.Id].HasMonthlyCadenceEvidence);
        }
        var discovery = await new PlaidBankTransactionDiscoveryGateway(db).GetDiscoveryTransactionsAsync(owner);
        Assert.Equal(new[] { first, second }, discovery.Select(record => record.CurrencyCode));
    }

    [Fact]
    public void Totals_PreserveCurrencies_ExcludeUnknown_AndDoNotConvert()
    {
        Assert.Equal("EUR 5.00 · $30.00", FinancialAmountFormatter.FormatTotals(
            [(10m, "USD"), (20m, " usd "), (5m, "EUR"), (999m, null), (777m, "?")]));
        Assert.Equal("EUR 60.00 · $360.00", FinancialAmountFormatter.FormatTotals(
            [(30m, "USD"), (5m, "EUR")], 12m));
        Assert.Null(FinancialAmountFormatter.FormatTotals([(999m, null)]));
        Assert.Null(FinancialAmountFormatter.FormatTotals([]));
        Assert.Equal("0.00 (Currency unavailable)", FinancialAmountFormatter.FormatOrUnspecified(0m, null));
        Assert.Equal("EUR 12.50", FinancialAmountFormatter.FormatOrUnspecified(12.5m, " eur "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("12$")]
    [InlineData("ＵＳＤ")]
    public void CurrencyValidation_RejectsMissingAndMalformedCodes(string? code)
    {
        Assert.False(FinancialAmountFormatter.TryNormalizeCurrency(code, out var normalized));
        Assert.Equal(string.Empty, normalized);
    }

    private static FullWorthDbContext CreateDb() => new(new DbContextOptionsBuilder<FullWorthDbContext>()
        .UseInMemoryDatabase($"currency-evidence-{Guid.NewGuid():N}").Options);

    private static void Add(FullWorthDbContext db, Guid owner, Guid stream, string? currency, decimal amount, int month)
    {
        var transaction = new BankTransactionEntity { UserId = owner, BankAccountId = Guid.NewGuid(),
            PlaidTransactionId = Guid.NewGuid().ToString("N"), Name = "Synthetic bill", MerchantName = "Synthetic bill",
            Amount = amount, IsoCurrencyCode = currency, PostedDate = new DateOnly(2026, month, 5) };
        db.BankTransactions.Add(transaction);
        db.BillTransactionAssociations.Add(new BillTransactionAssociationEntity
            { UserId = owner, BillStreamId = stream, BankTransactionId = transaction.Id });
    }
}
