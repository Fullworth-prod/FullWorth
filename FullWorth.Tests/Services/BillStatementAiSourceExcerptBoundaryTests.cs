using FullWorth.API.Services.Statements;

namespace FullWorth.Tests.Services;

public sealed class BillStatementAiSourceExcerptBoundaryTests
{
    private static readonly string[] DateFacts =
    [
        BillStatementAiFactKeys.BillingPeriodStart, BillStatementAiFactKeys.BillingPeriodEnd,
        BillStatementAiFactKeys.StatementDate, BillStatementAiFactKeys.DueDate
    ];
    private static readonly string[] TextFacts =
    [
        BillStatementAiFactKeys.ProviderName, BillStatementAiFactKeys.AccountIdentifierSuffix,
        BillStatementAiFactKeys.PlanOrService, BillStatementAiFactKeys.UsageSummary,
        BillStatementAiFactKeys.CurrencyCode, BillStatementAiFactKeys.LineItemDescription(0)
    ];
    private static string Value(string factKey) => factKey switch
    {
        BillStatementAiFactKeys.BillingPeriodStart => "08/01/2026",
        BillStatementAiFactKeys.BillingPeriodEnd or BillStatementAiFactKeys.DueDate => "08/31/2026",
        BillStatementAiFactKeys.StatementDate => "08/10/2026",
        BillStatementAiFactKeys.ProviderName => "Acme",
        BillStatementAiFactKeys.AccountIdentifierSuffix => "3456",
        BillStatementAiFactKeys.PlanOrService => "Premium",
        BillStatementAiFactKeys.UsageSummary => "5GB",
        BillStatementAiFactKeys.CurrencyCode => "USD",
        _ => "Target charge"
    };

    public static TheoryData<string, string, string> CroppedSources
    {
        get
        {
            var data = new TheoryData<string, string, string>();
            foreach (var factKey in DateFacts.Concat(TextFacts))
            foreach (var edge in new[] { "0", "X", "_", "\u0301", "𝟙" })
            {
                var value = Value(factKey);
                data.Add(factKey, value + edge, value);
                // A real suffix intentionally permits an omitted prefix.
                if (factKey != BillStatementAiFactKeys.AccountIdentifierSuffix)
                    data.Add(factKey, edge + value, value);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CroppedSources))]
    public void CroppedDateWordOrIdentifier_DoesNotBecomeVerifiedEvidence(
        string factKey, string source, string excerpt)
    {
        var (document, candidate) = Create(factKey, source, excerpt);
        var converted = new BillStatementAiCandidateConversionService(new()).Convert(document, candidate);

        Assert.False(converted.IsAccepted);
        Assert.Null(converted.Extraction);
        Assert.Contains(converted.Errors, error => error.Contains(factKey, StringComparison.Ordinal));
        Assert.All(converted.Errors, error => Assert.DoesNotContain(source, error, StringComparison.Ordinal));
    }

    public static TheoryData<string, string, string> WholeSources
    {
        get
        {
            var data = new TheoryData<string, string, string>();
            foreach (var factKey in DateFacts.Concat(TextFacts))
            {
                var value = Value(factKey);
                data.Add(factKey, "(" + value + ")", value);
                data.Add(factKey, "Label: " + value, value);
                data.Add(factKey, "Label \t" + value, "Label " + value);
                data.Add(factKey, value + "0; actual " + value, value);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(WholeSources))]
    public void CompleteOrLaterWholeOccurrence_RemainsReady(
        string factKey, string source, string excerpt)
    {
        var (document, candidate) = Create(factKey, source, excerpt);
        var result = new BillStatementAiCandidateConversionService(new()).Convert(document, candidate);

        Assert.True(result.IsAccepted, string.Join("; ", result.Errors));
        Assert.NotNull(result.Extraction);
        Assert.True(result.Extraction.IsReadyForValidation);
        Assert.Equal(1.23m, result.Extraction.Statement.TotalAmount);
        Assert.Equal("USD", result.Extraction.Statement.CurrencyCode);
        if (factKey == BillStatementAiFactKeys.BillingPeriodStart)
            Assert.Equal(new DateOnly(2026, 8, 1), result.Extraction.Statement.BillingPeriodStart);
        if (factKey == BillStatementAiFactKeys.BillingPeriodEnd)
            Assert.Equal(new DateOnly(2026, 8, 31), result.Extraction.Statement.BillingPeriodEnd);
    }

    [Theory]
    [InlineData("Account 1234567890123456")]
    [InlineData("Account ABC3456")]
    [InlineData("Account ****3456")]
    public void BareActualAccountSuffix_RemainsSupported(string source)
    {
        var (document, candidate) = Create(BillStatementAiFactKeys.AccountIdentifierSuffix, source, "3456");
        var result = new BillStatementAiCandidateConversionService(new()).Convert(document, candidate);
        Assert.True(result.IsAccepted, string.Join("; ", result.Errors));
        Assert.NotNull(result.Extraction);
    }

    [Theory]
    [InlineData("_X")]
    [InlineData("\u0301")]
    [InlineData("𝟙")]
    [InlineData("0")]
    public void FullAccountExcerpt_DoesNotTurnAnInteriorFragmentIntoSuffix(string tail)
    {
        var source = "Account 1234563456" + tail;
        var (document, candidate) = Create(BillStatementAiFactKeys.AccountIdentifierSuffix, source, source);
        var result = new BillStatementAiCandidateConversionService(new()).Convert(document, candidate);
        Assert.False(result.IsAccepted);
        Assert.Null(result.Extraction);
    }

    [Theory]
    [InlineData("billingPeriodStart")]
    [InlineData("billingPeriodEnd")]
    [InlineData("statementDate")]
    [InlineData("dueDate")]
    public void FullDateExcerpt_PreservesIdentifierContinuation(string factKey)
    {
        var source = "Date " + Value(factKey) + "_1";
        var (document, candidate) = Create(factKey, source, source);
        var result = new BillStatementAiCandidateConversionService(new()).Convert(document, candidate);
        Assert.False(result.IsAccepted);
        Assert.Null(result.Extraction);
    }

    [Fact]
    public void MonetarySpanEvidenceStillAcceptsAnAdjacentLabel()
    {
        var (document, candidate) = Create(BillStatementAiFactKeys.TotalDue, "Paid$1.23", "$1.23");
        var result = new BillStatementAiCandidateConversionService(new()).Convert(document, candidate);
        Assert.True(result.IsAccepted, string.Join("; ", result.Errors));
        Assert.NotNull(result.Extraction);
        Assert.Equal(1.23m, result.Extraction.Statement.TotalAmount);
    }

    private static (string Document, BillStatementAiCandidate Candidate) Create(
        string factKey, string source, string excerpt)
    {
        const string start = "Period start August 1, 2026";
        const string end = "Period end August 31, 2026";
        const string total = "Total due $1.23";
        const string currency = "Currency: USD";
        var evidence = new List<BillStatementAiEvidence>();
        var lines = new List<string>();
        foreach (var (key, text) in new[]
        {
            (BillStatementAiFactKeys.BillingPeriodStart, start),
            (BillStatementAiFactKeys.BillingPeriodEnd, end),
            (BillStatementAiFactKeys.TotalDue, total),
            (BillStatementAiFactKeys.CurrencyCode, currency)
        })
        {
            if (key == factKey) continue;
            evidence.Add(new(key, text));
            lines.Add(text);
        }
        evidence.Add(new(factKey, excerpt));
        lines.Add(source);
        var isLine = factKey == BillStatementAiFactKeys.LineItemDescription(0);
        if (isLine) evidence.Add(new(BillStatementAiFactKeys.LineItemAmount(0), "$1.23"));

        return (string.Join("\n", lines), new BillStatementAiCandidate(
            ProviderName: factKey == BillStatementAiFactKeys.ProviderName ? Value(factKey) : null,
            AccountIdentifierSuffix: factKey == BillStatementAiFactKeys.AccountIdentifierSuffix ? Value(factKey) : null,
            BillingPeriodStart: new DateOnly(2026, 8, 1),
            BillingPeriodEnd: new DateOnly(2026, 8, 31),
            StatementDate: factKey == BillStatementAiFactKeys.StatementDate ? new DateOnly(2026, 8, 10) : null,
            DueDate: factKey == BillStatementAiFactKeys.DueDate ? new DateOnly(2026, 8, 31) : null,
            PreviousBalance: null,
            Payments: null,
            CurrentCharges: null,
            TotalDue: 1.23m,
            CurrencyCode: "USD",
            PlanOrService: factKey == BillStatementAiFactKeys.PlanOrService ? Value(factKey) : null,
            UsageSummary: factKey == BillStatementAiFactKeys.UsageSummary ? Value(factKey) : null,
            LineItems: isLine ? [new BillStatementAiLineItemCandidate(Value(factKey), 1.23m,
                BillStatementAiLineItemKind.Service)] : [],
            Evidence: evidence,
            ModelConfidence: BillStatementAiModelConfidence.Unknown));
    }
}
