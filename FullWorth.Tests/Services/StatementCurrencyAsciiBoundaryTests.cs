using FullWorth.API.Services.Statements;

namespace FullWorth.Tests.Services;

public sealed class StatementCurrencyAsciiBoundaryTests
{
    [Theory]
    [InlineData("uſd")]
    [InlineData("UſD")]
    [InlineData("ſgd")]
    [InlineData("ſek")]
    [InlineData("ſar")]
    [InlineData("bſd")]
    [InlineData("ſſp")]
    [InlineData("lſl")]
    [InlineData("ſcr")]
    [InlineData("ſoſ")]
    public void UnicodeCasingCannotManufactureARecognizedCurrency(string rawCurrency)
    {
        var (document, candidate) = Create(rawCurrency);
        var parsed = new DeterministicBillStatementParser().Parse(document);
        var converted = new BillStatementAiCandidateConversionService(new()).Convert(document, candidate);

        Assert.False(parsed.IsReadyForPersistence);
        Assert.Equal(string.Empty, parsed.CurrencyCode);
        Assert.Contains(nameof(BillStatementStructuredData.CurrencyCode), parsed.MissingRequiredFields);
        Assert.False(converted.IsAccepted);
        Assert.Null(converted.Extraction);
    }

    [Theory]
    [InlineData("usd")]
    [InlineData("USD")]
    [InlineData("sGd")]
    [InlineData("sek")]
    [InlineData("sar")]
    [InlineData("bsd")]
    [InlineData("cad")]
    [InlineData("eur")]
    [InlineData("gbp")]
    [InlineData("aud")]
    public void AsciiCasingPreservesTheRecognizedUnitAndAmount(string rawCurrency)
    {
        var (document, candidate) = Create(rawCurrency);
        var parsed = new DeterministicBillStatementParser().Parse(document);
        var converted = new BillStatementAiCandidateConversionService(new()).Convert(document, candidate);

        Assert.True(parsed.IsReadyForPersistence);
        Assert.Equal(rawCurrency.ToUpperInvariant(), parsed.CurrencyCode);
        Assert.Equal(104.99m, parsed.TotalAmount);
        Assert.True(converted.IsAccepted, string.Join("; ", converted.Errors));
        Assert.NotNull(converted.Extraction);
        Assert.True(converted.Extraction.IsReadyForValidation);
        Assert.Equal(rawCurrency.ToUpperInvariant(), converted.Extraction.Statement.CurrencyCode);
        Assert.Equal(104.99m, converted.Extraction.Statement.TotalAmount);
    }

    private static (string Document, BillStatementAiCandidate Candidate) Create(string currency)
    {
        const string period = "Billing period August 1, 2026 through August 31, 2026";
        const string total = "Total due $104.99";
        var currencyLine = "Currency: " + currency;
        return (period + "\n" + total + "\n" + currencyLine,
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
                Evidence:
                [
                    new(BillStatementAiFactKeys.BillingPeriodStart, period),
                    new(BillStatementAiFactKeys.BillingPeriodEnd, period),
                    new(BillStatementAiFactKeys.TotalDue, total),
                    new(BillStatementAiFactKeys.CurrencyCode, currencyLine)
                ],
                ModelConfidence: BillStatementAiModelConfidence.Unknown));
    }
}
