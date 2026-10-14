namespace FullWorth.Core.Services;

/// <summary>
/// Recognizes two complete, consecutive calendar-month coverage periods.
/// This establishes comparable statement coverage, not a promise that a charge recurs.
/// </summary>
public static class MonthlyStatementPeriodPolicy
{
    public static bool CanCompare(
        DateOnly previousStart,
        DateOnly previousEnd,
        DateOnly currentStart,
        DateOnly currentEnd)
    {
        if (previousEnd < previousStart || currentEnd < currentStart ||
            currentStart.DayNumber != previousEnd.DayNumber + 1)
        {
            return false;
        }

        var firstMonth = (previousStart.Year - 1) * 12 + previousStart.Month - 1;
        if (firstMonth > 9999 * 12 - 3)
        {
            return false;
        }

        // Use a shared billing-day anchor for all three boundaries. Repeated
        // AddMonths calls would lose day 29/30/31 after clamping in February.
        for (var anchor = previousStart.Day; anchor <= 31; anchor++)
        {
            if (Boundary(firstMonth, anchor) == previousStart &&
                Boundary(firstMonth + 1, anchor) == currentStart &&
                Boundary(firstMonth + 2, anchor).DayNumber == currentEnd.DayNumber + 1)
            {
                return true;
            }
        }

        return false;
    }

    private static DateOnly Boundary(int monthIndex, int anchor)
    {
        var year = monthIndex / 12 + 1;
        var month = monthIndex % 12 + 1;
        return new DateOnly(year, month, Math.Min(anchor, DateTime.DaysInMonth(year, month)));
    }
}
