using System.Text.Json;
using FullWorth.Core.Models;
using FullWorth.Core.Services;

namespace FullWorth.Tests.Services;

public sealed class ZeroBaselineComparisonTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void ZeroBaseline_HasNoDefinedPercentage_WhileAmountChangesRemainKnown(int current)
    {
        var result = new BillComparisonService().Compare(new BillAmount(0), new BillAmount(current));
        Assert.Null(result.PercentageChange);
        Assert.Equal((decimal)current, result.MonthlyChange);
        Assert.Equal(current * 12m, result.AnnualChange);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result));
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("PercentageChange").ValueKind);
    }

    [Fact]
    public void PaidToFree_RetainsTheDefinedNegativeHundredPercent()
    {
        var result = new BillComparisonService().Compare(new BillAmount(10), new BillAmount(0));
        Assert.Equal(-100m, result.PercentageChange);
        Assert.Equal(-10m, result.MonthlyChange);
        Assert.Equal(-120m, result.AnnualChange);
    }

    [Fact]
    public void CompleteMonthlyStatements_PreserveDollarExplanationWithoutInventingAPercentage()
    {
        var previous = new BillStatement("Example", new(2026, 1, 1), new(2026, 1, 31),
            new BillAmount(0), [new BillLineItem("Service", 0)], "EUR");
        var current = new BillStatement("Example", new(2026, 2, 1), new(2026, 2, 28),
            new BillAmount(10), [new BillLineItem("Service", 10)], "EUR");
        var result = new BillAnalysisService().Analyze(previous, current);
        Assert.Null(result.Comparison.TotalComparison.PercentageChange);
        Assert.Equal(10m, result.Comparison.TotalComparison.MonthlyChange);
        Assert.Equal(120m, result.Comparison.TotalComparison.AnnualChange);
        Assert.Equal("EUR", result.Comparison.CurrencyCode);
        Assert.True(result.Comparison.IsFullyExplained);
        Assert.Equal(10m, result.Comparison.ExplainedChange);
    }
}
