using FullWorth.Core.Services;

namespace FullWorth.Tests.Services;

public sealed class MonthlyStatementPeriodPolicyTests
{
    [Theory]
    [InlineData(2024, 1, 1)]
    [InlineData(2024, 1, 29)]
    [InlineData(2024, 1, 30)]
    [InlineData(2024, 1, 31)]
    [InlineData(2025, 1, 29)]
    [InlineData(2025, 1, 30)]
    [InlineData(2025, 1, 31)]
    [InlineData(2025, 2, 28)]
    [InlineData(2026, 12, 15)]
    [InlineData(1, 1, 1)]
    public void SharedMonthlyAnchor_IsAccepted(int year, int month, int day)
    {
        var start = new DateOnly(year, month, day);
        var current = start.AddMonths(1);
        Assert.True(MonthlyStatementPeriodPolicy.CanCompare(
            start, current.AddDays(-1), current, start.AddMonths(2).AddDays(-1)));
    }

    [Fact]
    public void CalendarSweep_PreservesClampedBillingDayAcrossLeapYears()
    {
        for (var year = 1999; year <= 2030; year++)
        for (var month = 1; month <= 12; month++)
        for (var day = 1; day <= DateTime.DaysInMonth(year, month); day++)
        {
            var start = new DateOnly(year, month, day);
            var next = start.AddMonths(1);
            Assert.True(MonthlyStatementPeriodPolicy.CanCompare(
                start, next.AddDays(-1), next, start.AddMonths(2).AddDays(-1)));
        }
    }

    [Fact]
    public void FourWeeklyPeriods_AreNotMonthlyCoverage()
    {
        var start = new DateOnly(2026, 1, 1);
        Assert.False(MonthlyStatementPeriodPolicy.CanCompare(
            start, start.AddDays(27), start.AddDays(28), start.AddDays(55)));
    }

    [Fact]
    public void ReversedOverlappingMissingAndExtremePeriods_AreRejected()
    {
        var jan = new DateOnly(2026, 1, 1);
        var feb = jan.AddMonths(1);
        Assert.False(MonthlyStatementPeriodPolicy.CanCompare(jan, jan.AddDays(-1), feb, feb.AddMonths(1).AddDays(-1)));
        Assert.False(MonthlyStatementPeriodPolicy.CanCompare(jan, feb, feb, feb.AddMonths(1).AddDays(-1)));
        Assert.False(MonthlyStatementPeriodPolicy.CanCompare(jan, feb.AddDays(-1), feb.AddMonths(1), feb.AddMonths(2).AddDays(-1)));
        Assert.False(MonthlyStatementPeriodPolicy.CanCompare(DateOnly.MaxValue, DateOnly.MaxValue, DateOnly.MaxValue, DateOnly.MaxValue));
        Assert.False(MonthlyStatementPeriodPolicy.CanCompare(DateOnly.MinValue, DateOnly.MaxValue, DateOnly.MinValue, DateOnly.MaxValue));
    }
}

