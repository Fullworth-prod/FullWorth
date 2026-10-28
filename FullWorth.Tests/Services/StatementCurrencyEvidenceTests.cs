using FullWorth.API.Services.Statements;

namespace FullWorth.Tests.Services;

public sealed class StatementCurrencyEvidenceTests
{
    private const string Document = """
        Synthetic provider
        Billing Period: June 1, 2026 - June 30, 2026
        Statement Date: July 1, 2026
        Due Date: July 15, 2026
        Total Amount Due: 104.99
        """;
    private readonly DeterministicBillStatementParser _parser = new();

    [Theory]
    [InlineData("Currency: CAD", "CAD")]
    [InlineData("Statement Currency: aud", "AUD")]
    [InlineData("Billing currency: EUR", "EUR")]
    [InlineData("Currency code: GBP", "GBP")]
    [InlineData("Currency: USD", "USD")]
    [InlineData("Currency:  usd \t", "USD")]
    [InlineData("Billing\u00a0currency:\u00a0CAD", "CAD")]
    [InlineData("Statement\u2009Currency:\u2009CAD", "CAD")]
    public void ExplicitCurrency_PreservesDeclaredUnit(string declaration, string expected)
    {
        var result = _parser.Parse(Document + "\n" + declaration);
        Assert.Equal(expected, result.CurrencyCode);
        Assert.Equal(104.99m, result.TotalAmount);
        Assert.True(result.IsReadyForPersistence);
        Assert.Empty(result.ConflictingFields);
    }

    [Fact]
    public void CanadianDollarLabel_IsNotRelabeledAsUsd()
    {
        var result = _parser.Parse(Document.Replace("104.99", "$104.99") + "\nCurrency: CAD");
        Assert.Equal("CAD", result.CurrencyCode);
        Assert.Equal(104.99m, result.TotalAmount);
        Assert.True(result.IsReadyForPersistence);
    }

    [Theory]
    [InlineData("CAD")]
    [InlineData("USD")]
    [InlineData("eur")]
    public void SameLineAmountSuffix_PreservesDeclaredUnit(string code)
    {
        var result = _parser.Parse(Document.Replace("104.99", "104.99 " + code));
        Assert.Equal(code.ToUpperInvariant(), result.CurrencyCode);
        Assert.Equal(104.99m, result.TotalAmount);
        Assert.True(result.IsReadyForPersistence);
    }

    [Theory]
    [InlineData("Currency: CAD\nCurrency: USD")]
    [InlineData("Currency: USD\nCurrency: CAD")]
    [InlineData("Statement Currency: EUR\nBilling Currency: GBP")]
    [InlineData("Billing Currency: GBP\nStatement Currency: EUR")]
    public void ConflictingDeclarations_AreReviewableAndOrderIndependent(string declarations)
    {
        var result = _parser.Parse(Document + "\n" + declarations);
        Assert.Equal(string.Empty, result.CurrencyCode);
        Assert.False(result.IsReadyForPersistence);
        Assert.Contains(nameof(BillStatementStructuredData.CurrencyCode), result.ConflictingFields);
        Assert.Contains(nameof(BillStatementStructuredData.CurrencyCode), result.MissingRequiredFields);
        Assert.Equal(BillStatementStructuredDataConfidence.InsufficientEvidence, result.Confidence);
        Assert.Equal(104.99m, result.TotalAmount); // Preserve amount evidence without assigning a unit.
    }

    [Theory]
    [InlineData("CAD", "USD")]
    [InlineData("USD", "CAD")]
    [InlineData("EUR", "GBP")]
    [InlineData("GBP", "EUR")]
    public void DeclarationAndTotalSuffixConflict_DoNotPickOne(string declaration, string suffix)
    {
        var result = _parser.Parse(Document.Replace("104.99", "104.99 " + suffix) + "\nCurrency: " + declaration);
        Assert.False(result.IsReadyForPersistence);
        Assert.Equal(string.Empty, result.CurrencyCode);
        Assert.Contains(nameof(BillStatementStructuredData.CurrencyCode), result.ConflictingFields);
    }

    [Theory]
    [InlineData("CAD")]
    [InlineData("USD")]
    public void EquivalentRepeatedEvidence_RemainsUsable(string code)
    {
        var result = _parser.Parse(Document.Replace("104.99", "104.99 " + code.ToLowerInvariant()) +
            "\nCurrency: " + code + "\nStatement Currency: " + code);
        Assert.Equal(code, result.CurrencyCode);
        Assert.True(result.IsReadyForPersistence);
        Assert.Empty(result.ConflictingFields);
    }

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("12$")]
    [InlineData("CAD / USD")]
    public void ExplicitMalformedCurrency_DoesNotFallBackToUsd(string value)
    {
        var result = _parser.Parse(Document + "\nCurrency: " + value);
        Assert.Equal(string.Empty, result.CurrencyCode);
        Assert.False(result.IsReadyForPersistence);
        Assert.Contains(nameof(BillStatementStructuredData.CurrencyCode), result.MissingRequiredFields);
    }

    [Fact]
    public void EmptyDeclaration_DoesNotBorrowNextLineOrTotalSuffix()
    {
        var result = _parser.Parse("Currency:\n" + Document.Replace("104.99", "104.99 USD"));
        Assert.Equal(string.Empty, result.CurrencyCode);
        Assert.False(result.IsReadyForPersistence);
    }

    [Fact]
    public void CurrencySuffix_DoesNotBorrowThreeLetterWordOnNextLine()
    {
        var result = _parser.Parse(Document + "\nDUE");
        Assert.Equal("USD", result.CurrencyCode); // Existing USD-format fallback only; no fabricated DUE unit.
        Assert.Equal(104.99m, result.TotalAmount);
        Assert.True(result.IsReadyForPersistence);
    }

    [Fact]
    public void CrLfDocument_RetainsDeclaredCurrency()
    {
        var result = _parser.Parse((Document + "\nCurrency: CAD").Replace("\n", "\r\n"));
        Assert.Equal("CAD", result.CurrencyCode);
        Assert.Equal(104.99m, result.TotalAmount);
        Assert.True(result.IsReadyForPersistence);
    }

    [Theory]
    [InlineData("1.005")]
    [InlineData("94.994")]
    public void CurrencySuffix_DoesNotTruncateUnsupportedPrecision(string amount)
    {
        var result = _parser.Parse(Document.Replace("104.99", amount + " CAD"));
        Assert.Null(result.TotalAmount);
        Assert.False(result.IsReadyForPersistence);
    }

    [Fact]
    public void CurrencyEvidence_ReachesExistingValidationBoundaryWithoutConversion()
    {
        var valid = _parser.Parse(Document + "\nCurrency: CAD");
        var invalid = _parser.Parse(Document + "\nCurrency: CAD\nCurrency: USD");
        var validator = new BillStatementValidationService();
        BillStatementValidationResult Validate(BillStatementStructuredData parsed) => validator.Validate(
            new BillStatementValidationInput(parsed.TotalAmount, parsed.BillingPeriodStart, parsed.BillingPeriodEnd,
                parsed.StatementDate, parsed.DueDate, parsed.CurrencyCode), new DateOnly(2026, 7, 2));
        Assert.True(Validate(valid).IsValid);
        Assert.False(Validate(valid).RequiresReview);
        Assert.False(Validate(invalid).IsValid);
        Assert.Equal(104.99m, valid.TotalAmount);
    }

    [Fact]
    public void SameLineSuffix_WithNonbreakingSpaceRemainsExplicit()
    {
        var result = _parser.Parse(Document.Replace("104.99", "104.99\u00a0CAD"));
        Assert.Equal("CAD", result.CurrencyCode);
        Assert.Equal(104.99m, result.TotalAmount);
        Assert.True(result.IsReadyForPersistence);
    }

    [Fact]
    public void ExistingUsdFormat_RemainsCompatible()
    {
        var result = _parser.Parse(Document.Replace("104.99", "$104.99"));
        Assert.Equal("USD", result.CurrencyCode);
        Assert.True(result.IsReadyForPersistence);
    }
}
