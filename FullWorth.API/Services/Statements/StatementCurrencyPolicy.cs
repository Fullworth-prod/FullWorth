using System.Globalization;

namespace FullWorth.API.Services.Statements;

/// <summary>Conservative currency recognition shared by deterministic and AI extraction.</summary>
internal static class StatementCurrencyPolicy
{
    // Recognition is intentionally bounded to the framework's installed currency
    // metadata. Unrecognized explicit tokens stay in review rather than becoming USD.
    private static readonly HashSet<string> RecognizedCurrencyCodes =
        CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Select(TryGetRegionCurrencyCode)
            .OfType<string>()
            .Where(code => code.Length == 3 && code is not "XXX" and not "XTS" &&
                code.All(character => character is >= 'A' and <= 'Z'))
            .Append("USD")
            .ToHashSet(StringComparer.Ordinal);

    internal static bool IsRecognized(string? code)
    {
        if (code is null) return false;
        var raw = code.Trim();
        // Check the original alphabet before casing: e.g. Unicode long-s
        // uppercases to ASCII S and must not manufacture a recognized unit.
        return raw.Length == 3 &&
            raw.All(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z') &&
            RecognizedCurrencyCodes.Contains(raw.ToUpperInvariant());
    }

    private static string? TryGetRegionCurrencyCode(CultureInfo culture)
    {
        try { return new RegionInfo(culture.Name).ISOCurrencySymbol; }
        catch (ArgumentException) { return null; }
    }

}
