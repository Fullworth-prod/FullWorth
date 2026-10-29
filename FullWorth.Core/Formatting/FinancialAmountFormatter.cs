using System.Globalization;

namespace FullWorth.Core.Formatting;

/// <summary>Formats an amount in its stated currency without conversion.</summary>
public static class FinancialAmountFormatter
{
    public static bool TryNormalizeCurrency(string? currencyCode, out string currency)
    {
        var raw = currencyCode?.Trim();
        // Validate before casing: Unicode long-s can otherwise fold into S and
        // turn an invalid stated unit into a real currency such as USD.
        if (raw is { Length: 3 } && raw.All(character =>
            character is >= 'A' and <= 'Z' or >= 'a' and <= 'z'))
        {
            currency = raw.ToUpperInvariant();
            return true;
        }
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
        if (!TryNormalizeCurrency(currencyCode, out var normalized))
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

