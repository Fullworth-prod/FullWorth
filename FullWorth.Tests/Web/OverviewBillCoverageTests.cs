using FullWorth.Web.Services;

namespace FullWorth.Tests.Web;

public sealed class OverviewBillCoverageTests
{
    internal static OverviewBillFact Bill(decimal amount = 29m, decimal previous = 20m,
        string? currency = "USD", bool evidence = true, bool monthly = true, bool active = true) =>
        new(Guid.NewGuid(), "Provider", active, evidence, monthly, amount, previous, currency);

    [Fact]
    public void RecognizedCharges_AreNotAnInventedMonthlyBudget()
    {
        var result = OverviewBillCoverage.Create([Bill(29m, 0m), Bill(13.8m, 0m, monthly: false)]);
        Assert.Equal("$42.80", result.RecognizedTotal);
        Assert.Equal(2, result.KnownAmountCount);
        Assert.Equal(0, result.ComparableCount);
        Assert.Equal(2, result.UncomparedCount);
        Assert.Null(result.IncreaseTotal);
        Assert.Null(result.AnnualizedIncreaseTotal);
        Assert.Null(result.LargestIncrease);
    }

    [Fact]
    public void ComparedUnchangedOrDecreasedBills_HaveTrueZeroNotUnavailable()
    {
        var result = OverviewBillCoverage.Create([Bill(29m, 29m), Bill(13.8m, 15m)]);
        Assert.Equal("$0.00", result.IncreaseTotal);
        Assert.Equal(2, result.ComparableCount);
        Assert.Equal(0, result.IncreaseCount);
        Assert.Null(result.AnnualizedIncreaseTotal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("12$")]
    public void UnknownCurrency_IsExcludedAndCannotBecomeAnIncrease(string? currency)
    {
        var result = OverviewBillCoverage.Create([Bill(100m, 10m, currency)]);
        Assert.Equal(1, result.MissingAmountCount);
        Assert.Equal(1, result.UncomparedCount);
        Assert.Equal(0, result.ComparableCount);
        Assert.Equal(0, result.IncreaseCount);
        Assert.Null(result.RecognizedTotal);
        Assert.Null(result.IncreaseTotal);
        Assert.Null(result.LargestIncrease);
    }

    [Fact]
    public void MissingEvidenceOrNonpositiveCharge_CannotProduceFalseTotals()
    {
        var result = OverviewBillCoverage.Create([
            Bill(100m, 10m, evidence: false), Bill(0m), Bill(-30m),
            Bill(500m, active: false), Bill(29m, 0m)]);
        Assert.Equal(4, result.ActiveCount);
        Assert.Equal(1, result.KnownAmountCount);
        Assert.Equal(3, result.MissingAmountCount);
        Assert.Equal("$29.00", result.RecognizedTotal);
        Assert.Equal(0, result.IncreaseCount);
    }

    [Fact]
    public void PartialComparison_RemainsVisibleWithoutInventingZeroForUnknownBills()
    {
        var result = OverviewBillCoverage.Create([Bill(29m, 29m), Bill(13.8m, 0m)]);
        Assert.Equal(1, result.ComparableCount);
        Assert.Equal(1, result.UncomparedCount);
        Assert.Equal("$0.00", result.IncreaseTotal);
    }

    [Fact]
    public void CurrenciesRemainSeparateAndCannotBeRankedTogether()
    {
        var result = OverviewBillCoverage.Create([Bill(29m, 20m), Bill(50m, 40m, "EUR")]);
        Assert.Equal("EUR 50.00 · $29.00", result.RecognizedTotal);
        Assert.Equal("EUR 10.00 · $9.00", result.IncreaseTotal);
        Assert.Equal("EUR 120.00 · $108.00", result.AnnualizedIncreaseTotal);
        Assert.Null(result.LargestIncrease);
        Assert.Equal(2, result.IncreaseCount);
    }

    [Fact]
    public void CurrencyNormalization_DoesNotCreateFalseMixedCurrencyRanking()
    {
        var first = Bill(29m, 20m, " usd ");
        var second = Bill(50m, 40m, "USD");
        var result = OverviewBillCoverage.Create([first, second]);
        Assert.Equal("$19.00", result.IncreaseTotal);
        Assert.Same(second, result.LargestIncrease);
    }

    [Fact]
    public void OneNonmonthlyIncrease_PreventsPartialAnnualProjection()
    {
        var result = OverviewBillCoverage.Create([Bill(29m, 20m), Bill(50m, 40m, monthly: false)]);
        Assert.Equal("$19.00", result.IncreaseTotal);
        Assert.Null(result.AnnualizedIncreaseTotal);
    }

    [Fact]
    public void NonmonthlyUnchangedBill_DoesNotInvalidateSupportedIncreaseProjection()
    {
        var result = OverviewBillCoverage.Create([Bill(29m, 20m), Bill(50m, 50m, monthly: false)]);
        Assert.Equal("$108.00", result.AnnualizedIncreaseTotal);
    }

    [Fact]
    public void Decreases_DoNotNetAwayPositiveIncreases()
    {
        var result = OverviewBillCoverage.Create([Bill(29m, 20m), Bill(10m, 50m)]);
        Assert.Equal("$9.00", result.IncreaseTotal);
        Assert.Equal(1, result.IncreaseCount);
    }

    [Fact]
    public void Difference_RoundsConsistentlyAndDoesNotShowSubcentFalseAlert()
    {
        Assert.Equal(0m, OverviewBillCoverage.Difference(Bill(20.004m, 20m)));
        Assert.Equal(0.01m, OverviewBillCoverage.Difference(Bill(20.005m, 20m)));
        Assert.Equal(-0.01m, OverviewBillCoverage.Difference(Bill(19.995m, 20m)));
    }

    [Fact]
    public void EmptySnapshot_IsUnknownRatherThanInventedZeroCurrency()
    {
        var result = OverviewBillCoverage.Create([]);
        Assert.Equal(0, result.ActiveCount);
        Assert.Null(result.RecognizedTotal);
        Assert.Null(result.IncreaseTotal);
        Assert.Null(result.AnnualizedIncreaseTotal);
    }
}
