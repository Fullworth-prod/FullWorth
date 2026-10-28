using System.Globalization;
using System.Text.RegularExpressions;

namespace FullWorth.API.Services.Statements;

public sealed partial class DeterministicBillStatementParser
{
    private static readonly CultureInfo UsCulture =
        CultureInfo.GetCultureInfo(
            "en-US");

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

    private static readonly string[] AcceptedDateFormats =
    [
        "M/d/yyyy",
        "MM/dd/yyyy",
        "M/d/yy",
        "MM/dd/yy",

        "M-d-yyyy",
        "MM-dd-yyyy",
        "M-d-yy",
        "MM-dd-yy",

        "MMM d yyyy",
        "MMM dd yyyy",
        "MMM d, yyyy",
        "MMM dd, yyyy",

        "MMMM d yyyy",
        "MMMM dd yyyy",
        "MMMM d, yyyy",
        "MMMM dd, yyyy"
    ];

    public BillStatementStructuredData Parse(
        string extractedText)
    {
        ArgumentNullException.ThrowIfNull(
            extractedText);

        if (string.IsNullOrWhiteSpace(
                extractedText))
        {
            return CreateEmptyResult();
        }

        var conflicts = new List<string>();
        var totalAmount = TryExtractTotalAmount(extractedText, out var totalConflict);
        if (totalConflict) conflicts.Add(nameof(BillStatementStructuredData.TotalAmount));

        var billingPeriod = TryExtractBillingPeriod(extractedText, out var periodConflict);
        if (periodConflict)
        {
            conflicts.Add(nameof(BillStatementStructuredData.BillingPeriodStart));
            conflicts.Add(nameof(BillStatementStructuredData.BillingPeriodEnd));
        }

        var statementDate = TryExtractDate(
            StatementDateRegex(), extractedText, out var statementConflict);
        if (statementConflict) conflicts.Add(nameof(BillStatementStructuredData.StatementDate));

        var dueDate = TryExtractDate(
            DueDateRegex(), extractedText, out var dueConflict);
        if (dueConflict) conflicts.Add(nameof(BillStatementStructuredData.DueDate));

        var currencyCode = TryExtractCurrency(extractedText, out var currencyConflict);
        if (currencyConflict) conflicts.Add(nameof(BillStatementStructuredData.CurrencyCode));

        var missingRequiredFields =
            new List<string>(
                capacity:
                    4);

        if (!totalAmount.HasValue)
        {
            missingRequiredFields.Add(
                nameof(
                    BillStatementStructuredData.TotalAmount));
        }

        if (!billingPeriod.Start.HasValue)
        {
            missingRequiredFields.Add(
                nameof(
                    BillStatementStructuredData.BillingPeriodStart));
        }

        if (!billingPeriod.End.HasValue)
        {
            missingRequiredFields.Add(
                nameof(
                    BillStatementStructuredData.BillingPeriodEnd));
        }

        if (string.IsNullOrEmpty(currencyCode))
        {
            missingRequiredFields.Add(nameof(BillStatementStructuredData.CurrencyCode));
        }

        var confidence = conflicts.Count > 0
            ? BillStatementStructuredDataConfidence.InsufficientEvidence
            : DetermineConfidence(
                missingRequiredFields.Count,
                statementDate,
                dueDate);

        return new BillStatementStructuredData(
            TotalAmount:
                totalAmount,

            BillingPeriodStart:
                billingPeriod.Start,

            BillingPeriodEnd:
                billingPeriod.End,

            StatementDate:
                statementDate,

            DueDate:
                dueDate,

            CurrencyCode:
                currencyCode,

            Confidence:
                confidence,

            MissingRequiredFields:
                missingRequiredFields.AsReadOnly())
        {
            ConflictingFields = conflicts.AsReadOnly()
        };
    }

    private static string? TryGetRegionCurrencyCode(CultureInfo culture)
    {
        try { return new RegionInfo(culture.Name).ISOCurrencySymbol; }
        catch (ArgumentException) { return null; }
    }

    private static string TryExtractCurrency(string text, out bool conflicting)
    {
        var labeled = CurrencyCodeRegex().Matches(text).Cast<Match>()
            .Select(match => match.Groups["currency"].Value.Trim().ToUpperInvariant())
            .ToArray();
        var totalCurrencies = TotalAmountRegex().Matches(text).Cast<Match>()
            .Where(match => match.Groups["currency"].Success)
            .Select(match => match.Groups["currency"].Value.ToUpperInvariant());
        var currencies = labeled.Concat(totalCurrencies).Distinct(StringComparer.Ordinal).ToArray();
        conflicting = currencies.Length > 1;

        // Explicit malformed/competing currency evidence must not fall back to
        // this parser's legacy USD-style format assumption.
        if (currencies.Any(value => value.Length != 3 || !RecognizedCurrencyCodes.Contains(value)) ||
            conflicting)
        {
            return string.Empty;
        }

        return currencies.Length == 1 ? currencies[0] : "USD";
    }

    private static decimal? TryExtractTotalAmount(string text, out bool conflicting)
    {
        var ranked = TotalAmountRegex().Matches(text).Cast<Match>()
            .Select(match => new
            {
                Rank = RankAmountLabel(match.Groups["label"].Value),
                Amount = TryParseMoney(match.Groups["amount"].Value)
            })
            .ToArray();
        conflicting = false;
        if (ranked.Length == 0) return null;

        var bestRank = ranked.Min(candidate => candidate.Rank);
        var amounts = ranked.Where(candidate => candidate.Rank == bestRank)
            .Select(candidate => candidate.Amount).Distinct().ToArray();
        conflicting = amounts.Length > 1;
        return conflicting ? null : amounts[0];
    }

    private static (DateOnly? Start, DateOnly? End) TryExtractBillingPeriod(
        string text, out bool conflicting)
    {
        var periods = BillingPeriodRegex().Matches(text).Cast<Match>()
            .Select(match =>
            {
                var start = TryParseDate(match.Groups["start"].Value);
                var end = TryParseDate(match.Groups["end"].Value);
                return !start.HasValue || !end.HasValue || end < start
                    ? (Start: (DateOnly?)null, End: (DateOnly?)null)
                    : (Start: start, End: end);
            })
            .Distinct().ToArray();
        conflicting = periods.Length > 1;
        return periods.Length == 1 ? periods[0] : (null, null);
    }

    private static DateOnly? TryExtractDate(Regex regex, string text, out bool conflicting)
    {
        var dates = regex.Matches(text).Cast<Match>()
            .Select(match => TryParseDate(match.Groups["date"].Value))
            .Distinct().ToArray();
        conflicting = dates.Length > 1;
        return dates.Length == 1 ? dates[0] : null;
    }

    private static decimal? TryParseMoney(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        var normalized =
            value
                .Trim()
                .Replace(
                    "$",
                    string.Empty,
                    StringComparison.Ordinal)
                .Replace(
                    ",",
                    string.Empty,
                    StringComparison.Ordinal)
                .Trim();

        var isParenthesized =
            normalized.Length >=
                2 &&
            normalized[0] ==
                '(' &&
            normalized[^1] ==
                ')';

        if (isParenthesized)
        {
            normalized =
                normalized[1..^1]
                    .Trim();
        }

        if (!decimal.TryParse(
                normalized,
                NumberStyles.AllowDecimalPoint |
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            return null;
        }

        if (isParenthesized)
        {
            amount =
                amount > 0m ? -amount : amount;
        }

        if (amount <
            0m)
        {
            return null;
        }

        return decimal.Round(
            amount,
            2,
            MidpointRounding.AwayFromZero);
    }

    private static DateOnly? TryParseDate(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        var normalized =
            Regex.Replace(
                value.Trim(),
                @"\s+",
                " ");

        if (!DateOnly.TryParseExact(
                normalized,
                AcceptedDateFormats,
                UsCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsed))
        {
            return null;
        }

        return parsed;
    }

    private static int RankAmountLabel(
        string label)
    {
        var normalized =
            Regex.Replace(
                    label.Trim(),
                    @"\s+",
                    " ")
                .ToLowerInvariant();

        return normalized switch
        {
            "total amount due" =>
                0,

            "current amount due" =>
                1,

            "total due" =>
                2,

            "amount due" =>
                3,

            "balance due" =>
                4,

            _ =>
                int.MaxValue
        };
    }

    private static BillStatementStructuredDataConfidence
        DetermineConfidence(
            int missingRequiredFieldCount,
            DateOnly? statementDate,
            DateOnly? dueDate)
    {
        if (missingRequiredFieldCount ==
            0)
        {
            return
                BillStatementStructuredDataConfidence
                    .StrongEvidence;
        }

        if (missingRequiredFieldCount <=
                2 &&
            (statementDate.HasValue ||
             dueDate.HasValue))
        {
            return
                BillStatementStructuredDataConfidence
                    .Partial;
        }

        return
            BillStatementStructuredDataConfidence
                .InsufficientEvidence;
    }

    private static BillStatementStructuredData
        CreateEmptyResult()
    {
        return new BillStatementStructuredData(
            TotalAmount:
                null,

            BillingPeriodStart:
                null,

            BillingPeriodEnd:
                null,

            StatementDate:
                null,

            DueDate:
                null,

            CurrencyCode:
                "USD",

            Confidence:
                BillStatementStructuredDataConfidence
                    .InsufficientEvidence,

            MissingRequiredFields:
                Array.AsReadOnly(
                    new[]
                    {
                        nameof(
                            BillStatementStructuredData.TotalAmount),

                        nameof(
                            BillStatementStructuredData.BillingPeriodStart),

                        nameof(
                            BillStatementStructuredData.BillingPeriodEnd)
                    }));
    }

    [GeneratedRegex(
        @"(?im)^\s*(?<label>total\s+amount\s+due|current\s+amount\s+due|total\s+due|amount\s+due|balance\s+due)\s*:?\s*(?<amount>\$?\s*\(?-?(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d{1,2})?\)?)[\p{Zs}\t]*(?<currency>[A-Za-z]{3})?[\p{Zs}\t]*\r?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex
        TotalAmountRegex();

    [GeneratedRegex(
        @"(?im)^[\p{Zs}\t]*(?:statement[\p{Zs}\t]+currency|billing[\p{Zs}\t]+currency|currency[\p{Zs}\t]+code|currency)[\p{Zs}\t]*:[\p{Zs}\t]*(?<currency>[^\r\n]*)\r?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex
        CurrencyCodeRegex();

    [GeneratedRegex(
        @"(?im)^\s*(?:billing\s+period|service\s+period|billing\s+cycle|service\s+dates)\s*:?\s*(?<start>[A-Za-z]{3,9}\s+\d{1,2},?\s+\d{4}|\d{1,2}[/-]\d{1,2}[/-]\d{2,4})\s*(?:-|–|—|to|through)\s*(?<end>[A-Za-z]{3,9}\s+\d{1,2},?\s+\d{4}|\d{1,2}[/-]\d{1,2}[/-]\d{2,4})\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex
        BillingPeriodRegex();

    [GeneratedRegex(
        @"(?im)^\s*(?:statement\s+date|bill\s+date|invoice\s+date)\s*:?\s*(?<date>[A-Za-z]{3,9}\s+\d{1,2},?\s+\d{4}|\d{1,2}[/-]\d{1,2}[/-]\d{2,4})\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex
        StatementDateRegex();

    [GeneratedRegex(
        @"(?im)^\s*(?:due\s+date|payment\s+due|payment\s+due\s+date)\s*:?\s*(?<date>[A-Za-z]{3,9}\s+\d{1,2},?\s+\d{4}|\d{1,2}[/-]\d{1,2}[/-]\d{2,4})\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex
        DueDateRegex();
}

public sealed record BillStatementStructuredData(
    decimal? TotalAmount,
    DateOnly? BillingPeriodStart,
    DateOnly? BillingPeriodEnd,
    DateOnly? StatementDate,
    DateOnly? DueDate,
    string CurrencyCode,
    BillStatementStructuredDataConfidence Confidence,
    IReadOnlyList<string> MissingRequiredFields)
{
    // Field names only: never retain document text or competing raw values here.
    public IReadOnlyList<string> ConflictingFields { get; init; } = [];

    public bool IsReadyForPersistence =>
        ConflictingFields.Count == 0 &&
        TotalAmount.HasValue &&
        BillingPeriodStart.HasValue &&
        BillingPeriodEnd.HasValue &&
        !string.IsNullOrWhiteSpace(
            CurrencyCode);
}

public enum BillStatementStructuredDataConfidence
{
    InsufficientEvidence = 0,
    Partial = 1,
    StrongEvidence = 2
}
