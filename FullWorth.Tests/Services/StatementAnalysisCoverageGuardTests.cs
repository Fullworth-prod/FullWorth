using FullWorth.Core.Models;
using FullWorth.Core.Services;
using Xunit;

namespace FullWorth.Tests.Services;

public sealed class StatementAnalysisCoverageGuardTests
{
    public static TheoryData<DateOnly, DateOnly, DateOnly, DateOnly> IncomparablePeriods => new()
    {
        { new(2026, 1, 1), new(2026, 3, 31), new(2026, 4, 1), new(2026, 6, 30) },
        { new(2025, 1, 1), new(2025, 12, 31), new(2026, 1, 1), new(2026, 12, 31) },
        { new(2026, 1, 1), new(2026, 1, 15), new(2026, 1, 16), new(2026, 1, 31) },
        { new(2026, 1, 1), new(2026, 1, 31), new(2026, 1, 31), new(2026, 2, 28) },
        { new(2026, 1, 1), new(2026, 1, 31), new(2026, 3, 1), new(2026, 3, 31) },
        { new(2026, 1, 1), new(2026, 1, 31), new(2026, 1, 1), new(2026, 1, 31) },
        { new(2026, 2, 1), new(2026, 2, 28), new(2026, 1, 1), new(2026, 1, 31) }
    };

    [Theory]
    [MemberData(nameof(IncomparablePeriods))]
    public void SharedComparisonCannotLabelUnsupportedCoverageAsMonthly(
        DateOnly previousStart, DateOnly previousEnd, DateOnly currentStart, DateOnly currentEnd)
    {
        var previous = Statement(previousStart, previousEnd, 100m);
        var current = Statement(currentStart, currentEnd, 120m);
        Assert.Throws<ArgumentException>(() => new BillStatementComparisonService().Compare(previous, current));
        Assert.Throws<ArgumentException>(() => new BillAnalysisService().Analyze(previous, current));
    }

    [Theory]
    [MemberData(nameof(IncomparablePeriods))]
    public void StreamAnalysisLeavesUnsupportedCoverageUnanalyzed(
        DateOnly previousStart, DateOnly previousEnd, DateOnly currentStart, DateOnly currentEnd)
    {
        var stream = new BillStream(Guid.NewGuid(), "Example", BillCategory.Internet,
            statements: [Statement(previousStart, previousEnd, 100m), Statement(currentStart, currentEnd, 120m)]);
        Assert.Null(new BillStreamAnalysisService().Analyze(stream));
    }

    [Theory]
    [InlineData(2026, 1, 1, 2026, 1, 31, 2026, 2, 1, 2026, 2, 28)]
    [InlineData(2026, 1, 31, 2026, 2, 27, 2026, 2, 28, 2026, 3, 30)]
    [InlineData(2024, 1, 31, 2024, 2, 28, 2024, 2, 29, 2024, 3, 30)]
    public void CompleteMonthlyCoverageRetainsDeterministicAmounts(
        int py, int pm, int pd, int pey, int pem, int ped,
        int cy, int cm, int cd, int cey, int cem, int ced)
    {
        var previous = Statement(new(py, pm, pd), new(pey, pem, ped), 100m);
        var current = Statement(new(cy, cm, cd), new(cey, cem, ced), 120m);
        var result = new BillStatementComparisonService().Compare(previous, current);
        Assert.Equal(20m, result.TotalComparison.MonthlyChange);
        Assert.Equal(240m, result.TotalComparison.AnnualChange);
        Assert.Equal("EUR", result.CurrencyCode);
        var stream = new BillStream(Guid.NewGuid(), "Example", BillCategory.Internet,
            statements: [previous, current]);
        Assert.NotNull(new BillStreamAnalysisService().Analyze(stream));
    }

    [Fact]
    public void StreamAnalysisDoesNotCompareDifferentCurrencies()
    {
        var previous = Statement(new(2026, 1, 1), new(2026, 1, 31), 100m);
        var current = new BillStatement("Example", new(2026, 2, 1), new(2026, 2, 28),
            new BillAmount(120m), [], "USD");
        var stream = new BillStream(Guid.NewGuid(), "Example", BillCategory.Internet,
            statements: [previous, current]);
        Assert.Null(new BillStreamAnalysisService().Analyze(stream));
    }

    private static BillStatement Statement(DateOnly start, DateOnly end, decimal amount) =>
        new("Example", start, end, new BillAmount(amount), [], "EUR");
}
