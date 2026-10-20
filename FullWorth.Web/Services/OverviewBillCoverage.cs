using FullWorth.Core.Formatting;

namespace FullWorth.Web.Services;

/// <summary>A read-only presentation snapshot; never discovers bills or changes evidence.</summary>
public sealed record OverviewBillFact(
    Guid Id, string ProviderName, bool IsActive, bool HasTransactionEvidence,
    bool HasMonthlyCadenceEvidence, decimal CurrentAmount, decimal PreviousAverage,
    string? CurrencyCode);

public sealed class OverviewBillCoverage
{
    public IReadOnlyList<OverviewBillFact> Bills { get; private init; } = [];
    public int ActiveCount => Bills.Count;
    public int KnownAmountCount { get; private init; }
    public int ComparableCount { get; private init; }
    public int MissingAmountCount => ActiveCount - KnownAmountCount;
    public int UncomparedCount => ActiveCount - ComparableCount;
    public int IncreaseCount { get; private init; }
    public string? RecognizedTotal { get; private init; }
    public string? IncreaseTotal { get; private init; }
    public string? AnnualizedIncreaseTotal { get; private init; }
    public OverviewBillFact? LargestIncrease { get; private init; }

    public static bool HasKnownAmount(OverviewBillFact bill) =>
        bill.HasTransactionEvidence && bill.CurrentAmount > 0m &&
        FinancialAmountFormatter.TryNormalizeCurrency(bill.CurrencyCode, out _);

    public static bool CanCompare(OverviewBillFact bill) =>
        HasKnownAmount(bill) && bill.PreviousAverage > 0m;

    public static decimal Difference(OverviewBillFact bill) =>
        CanCompare(bill)
            ? decimal.Round(bill.CurrentAmount - bill.PreviousAverage,
                2, MidpointRounding.AwayFromZero)
            : 0m;

    public static OverviewBillCoverage Create(IEnumerable<OverviewBillFact> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var bills = source.Where(bill => bill.IsActive).ToArray();
        var known = bills.Where(HasKnownAmount).ToArray();
        var comparable = bills.Where(CanCompare).ToArray();
        var increases = comparable.Where(bill => Difference(bill) > 0m).ToArray();
        var currencyCount = increases.Select(bill => bill.CurrencyCode!.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal).Count();

        return new()
        {
            Bills = bills,
            KnownAmountCount = known.Length,
            ComparableCount = comparable.Length,
            IncreaseCount = increases.Length,
            RecognizedTotal = FinancialAmountFormatter.FormatTotals(
                known.Select(bill => (bill.CurrentAmount, bill.CurrencyCode))),
            // Preserve true zero for each compared currency; never invent a currency.
            IncreaseTotal = FinancialAmountFormatter.FormatTotals(
                comparable.Select(bill => (Math.Max(Difference(bill), 0m), bill.CurrencyCode))),
            // Do not project a partial set or nonmonthly payment as an annual rate.
            AnnualizedIncreaseTotal = increases.Length > 0 &&
                increases.All(bill => bill.HasMonthlyCadenceEvidence)
                    ? FinancialAmountFormatter.FormatTotals(
                        increases.Select(bill => (Difference(bill), bill.CurrencyCode)), 12m)
                    : null,
            LargestIncrease = currencyCount == 1
                ? increases.OrderByDescending(Difference).ThenBy(bill => bill.Id).First()
                : null
        };
    }
}
