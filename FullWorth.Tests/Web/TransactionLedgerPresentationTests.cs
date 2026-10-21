using System.Globalization;
using FullWorth.Web.Presentation;

namespace FullWorth.Tests.Web;

public sealed class TransactionLedgerPresentationTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("es-ES")]
    [InlineData("fr-FR")]
    public void Outflow_SeparatesCurrencies_ExcludesPendingCreditsAndUnknown(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var summary = TransactionLedgerPresentation.Summarize([
                (10m, "USD", false), (5m, " usd ", false), (20m, "EUR", false),
                (999m, "USD", true), (-50m, "USD", false), (0m, null, false),
                (8m, null, false), (9m, "US", false)
            ]);
            Assert.Equal("EUR 20.00 · $15.00", summary.Totals);
            Assert.Equal(2, summary.UnknownCurrencyCount);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void UnknownOrEmptyHistory_DoesNotInventDollarZero()
    {
        Assert.Null(TransactionLedgerPresentation.Summarize([]).Totals);
        var summary = TransactionLedgerPresentation.Summarize([(42m, null, false)]);
        Assert.Null(summary.Totals);
        Assert.Equal(1, summary.UnknownCurrencyCount);
    }

    [Theory]
    [InlineData(12.5, "EUR", "-EUR 12.50")]
    [InlineData(-12.5, "USD", "+$12.50")]
    [InlineData(0, "CAD", "CAD 0.00")]
    [InlineData(4, null, "-4.00 (Currency unavailable)")]
    public void Row_PreservesDirectionAndCurrency(decimal amount, string? currency, string expected) =>
        Assert.Equal(expected, TransactionLedgerPresentation.FormatSigned(amount, currency));

    [Fact]
    public void MinimumDecimalCredit_DoesNotOverflow() =>
        Assert.Equal("+EUR 79228162514264337593543950335.00",
            TransactionLedgerPresentation.FormatSigned(decimal.MinValue, "EUR"));
}
