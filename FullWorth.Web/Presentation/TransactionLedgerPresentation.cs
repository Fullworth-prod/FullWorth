using FullWorth.Core.Formatting;

namespace FullWorth.Web.Presentation;

/// <summary>Presentation only: never infer recurring bills or convert currencies.</summary>
public static class TransactionLedgerPresentation
{
    public sealed record OutflowSummary(string? Totals, int UnknownCurrencyCount);

    public static OutflowSummary Summarize(
        IEnumerable<(decimal Amount, string? CurrencyCode, bool IsPending)> transactions)
    {
        var postedOutflow = transactions
            .Where(item => !item.IsPending && item.Amount > 0m)
            .ToArray();
        return new(
            FinancialAmountFormatter.FormatTotals(
                postedOutflow.Select(item => (item.Amount, item.CurrencyCode))),
            postedOutflow.Count(item =>
                !FinancialAmountFormatter.TryNormalizeCurrency(item.CurrencyCode, out _)));
    }

    public static string FormatSigned(decimal amount, string? currencyCode,
        string unspecifiedLabel = "Currency unavailable")
    {
        var sign = amount > 0m ? "-" : amount < 0m ? "+" : string.Empty;
        // Format first, then remove the negative sign: decimal.MinValue cannot be negated.
        var formatted = FinancialAmountFormatter.FormatOrUnspecified(amount, currencyCode, unspecifiedLabel);
        return sign + (amount < 0m ? formatted.Replace("-", string.Empty, StringComparison.Ordinal) : formatted);
    }
}
