using FullWorth.API.Services.Statements;

namespace FullWorth.Tests.Services;

public sealed class BillStatementAiCurrencyEvidenceTests
{
    [Theory]
    [InlineData("DUE", "Total due $104.99")]
    [InlineData("FEE", "Service fee $104.99")]
    [InlineData("TAX", "Sales tax $104.99")]
    [InlineData("due", "Total due $104.99")]
    [InlineData("XXX", "Currency: XXX")]
    [InlineData("XTS", "Currency: XTS")]
    [InlineData("ZZZ", "Currency: ZZZ")]
    [InlineData("ÅBC", "Currency: ÅBC")]
    [InlineData("ＵＳＤ", "Currency: ＵＳＤ")]
    [InlineData("US", "Currency: US")]
    [InlineData("USDD", "Currency: USDD")]
    [InlineData("U1D", "Currency: U1D")]
    [InlineData("US$", "Currency: US$")]
    [InlineData("USD CAD", "Currency: USD CAD")]
    public void UnknownOrNoncurrencyWord_IsRejectedBeforeConversion(
        string currency,
        string currencyEvidence)
    {
        var (document, candidate) = Create(currency, currencyEvidence);

        var validation = new BillStatementAiCandidateValidator().Validate(document, candidate);
        var converted = new BillStatementAiCandidateConversionService(new()).Convert(document, candidate);

        Assert.False(validation.IsValid);
        Assert.Contains("The currency code must be a recognized three-letter currency.", validation.Errors);
        Assert.False(converted.IsAccepted);
        Assert.Null(converted.Extraction);
    }

    [Theory]
    [InlineData("USD", "USD")]
    [InlineData(" usd ", "USD")]
    [InlineData("CAD", "CAD")]
    [InlineData("cad", "CAD")]
    [InlineData("EUR", "EUR")]
    [InlineData("GBP", "GBP")]
    [InlineData("JPY", "JPY")]
    [InlineData("AUD", "AUD")]
    [InlineData("CHF", "CHF")]
    [InlineData("INR", "INR")]
    public void RecognizedSourceCurrency_PreservesAmountAndNormalizesCode(
        string currency,
        string expected)
    {
        var (document, candidate) = Create(currency, "Currency: " + currency.Trim());
        var converted = new BillStatementAiCandidateConversionService(new()).Convert(document, candidate);

        Assert.True(converted.IsAccepted, string.Join("; ", converted.Errors));
        Assert.NotNull(converted.Extraction);
        Assert.True(converted.Extraction.IsReadyForValidation);
        Assert.Equal(expected, converted.Extraction.Statement.CurrencyCode);
        Assert.Equal(104.99m, converted.Extraction.Statement.TotalAmount);
        Assert.Empty(converted.Extraction.Statement.MissingRequiredFields);
    }

    [Theory]
    [InlineData("USDA")]
    [InlineData("XUSD")]
    [InlineData("USD1")]
    [InlineData("1USD")]
    public void RecognizedCodeInsideAnotherToken_IsNotEvidence(string source)
    {
        var (document, candidate) = Create("USD", source);
        var result = new BillStatementAiCandidateConversionService(new()).Convert(document, candidate);

        Assert.False(result.IsAccepted);
        Assert.Null(result.Extraction);
        Assert.Contains(result.Errors, error => error.Contains("currencyCode", StringComparison.Ordinal));
    }

    [Fact]
    public void RecognizedButInventedCurrencyExcerpt_IsRejected()
    {
        var (_, candidate) = Create("CAD", "Currency: CAD");
        var result = new BillStatementAiCandidateConversionService(new()).Convert(
            Period + "\nTotal due $104.99\nCurrency: USD", candidate);

        Assert.False(result.IsAccepted);
        Assert.Null(result.Extraction);
        Assert.Contains(result.Errors, error => error.Contains("was not found", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingCurrency_RemainsIncompleteWithoutInventedUnit(string? currency)
    {
        var (document, candidate) = Create(currency, null);
        var converted = new BillStatementAiCandidateConversionService(new()).Convert(document, candidate);

        Assert.True(converted.IsAccepted, string.Join("; ", converted.Errors));
        Assert.NotNull(converted.Extraction);
        Assert.False(converted.Extraction.IsReadyForValidation);
        Assert.Contains(nameof(BillStatementStructuredData.CurrencyCode),
            converted.Extraction.Statement.MissingRequiredFields);
    }

    [Theory]
    [InlineData("USDA", "USD")]
    [InlineData("XUSD", "USD")]
    [InlineData("USD1", "USD")]
    [InlineData("1USD", "USD")]
    [InlineData("Currency: USDA", "Currency: USD")]
    public void CroppedCurrencyExcerptInsideSourceToken_IsRejected(
        string actualSource, string croppedExcerpt)
    {
        var (_, candidate) = Create("USD", croppedExcerpt);
        var converted = new BillStatementAiCandidateConversionService(new()).Convert(
            Period + "\nTotal due $104.99\n" + actualSource, candidate);

        Assert.False(converted.IsAccepted);
        Assert.Null(converted.Extraction);
    }

    [Fact]
    public void WholeCurrencyOccurrenceAfterEmbeddedOne_IsAccepted()
    {
        var (_, candidate) = Create("USD", "USD");
        var converted = new BillStatementAiCandidateConversionService(new()).Convert(
            Period + "\nTotal due $104.99\nUSDA report\nCurrency: USD", candidate);

        Assert.True(converted.IsAccepted, string.Join("; ", converted.Errors));
        Assert.NotNull(converted.Extraction);
        Assert.Equal("USD", converted.Extraction.Statement.CurrencyCode);
    }

    private const string Period =
        "Billing period August 1, 2026 through August 31, 2026";

    private static (string Document, BillStatementAiCandidate Candidate) Create(
        string? currency,
        string? currencyEvidence)
    {
        var evidence = new List<BillStatementAiEvidence>
        {
            new(BillStatementAiFactKeys.BillingPeriodStart, Period),
            new(BillStatementAiFactKeys.BillingPeriodEnd, Period),
            new(BillStatementAiFactKeys.TotalDue, "Total due $104.99")
        };
        if (currencyEvidence is not null)
            evidence.Add(new(BillStatementAiFactKeys.CurrencyCode, currencyEvidence));

        return (Period + "\nTotal due $104.99\n" + currencyEvidence,
            new BillStatementAiCandidate(
                ProviderName: null,
                AccountIdentifierSuffix: null,
                BillingPeriodStart: new DateOnly(2026, 8, 1),
                BillingPeriodEnd: new DateOnly(2026, 8, 31),
                StatementDate: null,
                DueDate: null,
                PreviousBalance: null,
                Payments: null,
                CurrentCharges: null,
                TotalDue: 104.99m,
                CurrencyCode: currency,
                PlanOrService: null,
                UsageSummary: null,
                LineItems: [],
                Evidence: evidence,
                ModelConfidence: BillStatementAiModelConfidence.Unknown));
    }
}
