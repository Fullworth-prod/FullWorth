using System.Globalization;

namespace FullWorth.Core.Formatting;

/// <summary>Formats an amount in its stated currency without conversion.</summary>
public static class FinancialAmountFormatter
{
    public static bool TryNormalizeCurrency(string? currencyCode, out string currency)
    {
        currency = currencyCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (currency.Length == 3 && currency.All(character => character is >= 'A' and <= 'Z'))
            return true;
        currency = string.Empty;
        return false;
    }

    public static string FormatOrUnspecified(decimal amount, string? currencyCode,
        string unspecifiedLabel = "Currency unavailable") =>
        TryNormalizeCurrency(currencyCode, out var currency)
            ? Format(amount, currency)
            : amount.ToString("0.00", CultureInfo.InvariantCulture) + " (" + unspecifiedLabel + ")";

    // Unknown currencies are excluded. Each currency has its own total; no FX conversion.
    public static string? FormatTotals(IEnumerable<(decimal Amount, string? CurrencyCode)> amounts,
        decimal multiplier = 1m)
    {
        var totals = amounts
            .Where(item => TryNormalizeCurrency(item.CurrencyCode, out _))
            .GroupBy(item => item.CurrencyCode!.Trim().ToUpperInvariant())
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => Format(group.Sum(item => item.Amount) * multiplier, group.Key))
            .ToArray();
        return totals.Length == 0 ? null : string.Join(" · ", totals);
    }

    public static string NormalizeCurrency(string currencyCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);
        var normalized = currencyCode.Trim().ToUpperInvariant();
        if (normalized.Length != 3 ||
            normalized.Any(character => character is < 'A' or > 'Z'))
        {
            throw new ArgumentException("Currency must contain three ASCII letters.", nameof(currencyCode));
        }

        return normalized;
    }

    public static string Format(decimal amount, string currencyCode)
    {
        var currency = NormalizeCurrency(currencyCode);
        var number = amount.ToString("0.00", CultureInfo.InvariantCulture);
        return currency == "USD" ? "$" + number : currency + " " + number;
    }
}

