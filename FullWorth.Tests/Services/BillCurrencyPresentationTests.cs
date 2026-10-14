using System.Globalization;
using FullWorth.Core.Formatting;
using FullWorth.Core.Models;
using FullWorth.Core.Services;

namespace FullWorth.Tests.Services;

public sealed class BillCurrencyPresentationTests
{
    [Theory]
    [InlineData("USD", "$25.50")]
    [InlineData("EUR", "EUR 25.50")]
    [InlineData("GBP", "GBP 25.50")]
    [InlineData("CAD", "CAD 25.50")]
    public void AmountFormatting_IsExplicitAndCultureIndependent(string currency, string expected)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal(expected, FinancialAmountFormatter.Format(25.50m, currency));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Theory]
    [InlineData("")]
    [InlineData("12X")]
    [InlineData("US")]
    [InlineData("EURO")]
    [InlineData("ＵＳＤ")]
    public void InvalidCurrency_IsRejected(string currency) =>
        Assert.Throws<ArgumentException>(() => FinancialAmountFormatter.Format(1m, currency));

    [Fact]
    public void StatementCurrency_IsNormalizedWithoutConvertingMoney()
    {
        var statement = Statement(100m, " eur ");
        Assert.Equal("EUR", statement.CurrencyCode);
        Assert.Equal(100m, statement.TotalAmount.Amount);
    }

    [Fact]
    public void DifferentStatementCurrencies_CannotProduceAChange() =>
        Assert.Throws<ArgumentException>(() => new BillStatementComparisonService()
            .Compare(Statement(80m, "USD"), Statement(105m, "EUR")));

    [Theory]
    [InlineData(80, 105, "increased", "+EUR 25.00")]
    [InlineData(105, 80, "decreased", "-EUR 25.00")]
    public void ExplanationAndEvidence_UseTheOriginalCurrency(
        int previous, int current, string direction, string signedImpact)
    {
        var analysis = new BillAnalysisService().Analyze(
            Statement(previous, "EUR"), Statement(current, "EUR"));
        Assert.Equal("EUR", analysis.Comparison.CurrencyCode);
        Assert.Equal("EUR", analysis.Explanation.CurrencyCode);
        Assert.Contains(direction, analysis.Explanation.Summary);
        Assert.Contains("EUR 25.00 per month", analysis.Explanation.Summary);
        Assert.Contains("EUR 300.00 per year", analysis.Explanation.Summary);
        Assert.Contains(signedImpact, Assert.Single(analysis.Explanation.Changes).Description);
        Assert.DoesNotContain("$", analysis.Explanation.Summary);
    }

    private static BillStatement Statement(decimal amount, string currency) =>
        new("Example Provider", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
            new BillAmount(amount), [new BillLineItem("Service", amount)], currency);
}
