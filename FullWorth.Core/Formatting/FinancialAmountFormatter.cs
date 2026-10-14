using System.Globalization;

namespace FullWorth.Core.Formatting;

/// <summary>Formats an amount in its stated currency without conversion.</summary>
public static class FinancialAmountFormatter
{
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
