using System.Globalization;
using FullWorth.API.Services.Statements;

namespace FullWorth.Tests.Services;

public sealed class AiCandidatePrecisionBoundaryTests
{
    public static IEnumerable<object[]> UnsupportedAmounts()
    {
        foreach (var field in new[] { "previousBalance", "payments", "currentCharges", "totalDue", "lineItem" })
        foreach (var value in new[] { "0.005", "1.005", "94.994" })
            yield return new object[] { field, value };
        yield return new object[] { "lineItem", "-0.005" };
        yield return new object[] { "lineItem", "-10.001" };
    }

    [Theory]
    [MemberData(nameof(UnsupportedAmounts))]
    public void EvidenceBackedFractionalCents_AreRejectedBeforeMapping(string field, string amount)
    {
        var (document, candidate) = Fixture(field, decimal.Parse(amount, CultureInfo.InvariantCulture));
        var validation = new BillStatementAiCandidateValidator().Validate(document, candidate);
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("decimal places", StringComparison.Ordinal));
        var result = new BillStatementAiCandidateConversionService(new()).Convert(document, candidate);
        Assert.False(result.IsAccepted);
        Assert.Null(result.Extraction);
        Assert.Contains(result.Errors, error => error.Contains("decimal places", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("totalDue", "0")]
    [InlineData("totalDue", "1.2300")]
    [InlineData("lineItem", "0")]
    [InlineData("lineItem", "-10.2300")]
    public void SupportedValuesAndTrailingZeros_RetainExactAmounts(string field, string text)
    {
        var amount = decimal.Parse(text, CultureInfo.InvariantCulture);
        var (document, candidate) = Fixture(field, amount);
        var result = new BillStatementAiCandidateConversionService(new()).Convert(document, candidate);
        Assert.True(result.IsAccepted, string.Join("|", result.Errors));
        Assert.NotNull(result.Extraction);
        Assert.True(result.Extraction.IsReadyForValidation);
        Assert.Equal(field == "totalDue" ? amount : 10m, result.Extraction.Statement.TotalAmount);
        if (field == "lineItem") Assert.Equal(amount, Assert.Single(result.Extraction.LineItems).Amount);
    }

    private static (string, BillStatementAiCandidate) Fixture(string field, decimal amount)
    {
        var candidate = new BillStatementAiCandidate(null, null, new(2026, 8, 1), new(2026, 8, 31),
            null, null, null, null, null, 10m, "USD", null, null, [], [], BillStatementAiModelConfidence.Unknown);
        candidate = field switch
        {
            "previousBalance" => candidate with { PreviousBalance = amount },
            "payments" => candidate with { Payments = amount },
            "currentCharges" => candidate with { CurrentCharges = amount },
            "totalDue" => candidate with { TotalDue = amount },
            "lineItem" => candidate with { LineItems = [new("Service charge", amount, BillStatementAiLineItemKind.Service)] },
            _ => throw new ArgumentException("Unknown synthetic test field.")
        };
        const string period = "Billing period August 1, 2026 through August 31, 2026";
        var evidence = new List<BillStatementAiEvidence>
        {
            new("billingPeriodStart", period), new("billingPeriodEnd", period), new("currencyCode", "USD"),
            new("totalDue", "Total due " + candidate.TotalDue!.Value.ToString(CultureInfo.InvariantCulture))
        };
        if (field == "lineItem")
        {
            var line = "Service charge " + amount.ToString(CultureInfo.InvariantCulture);
            evidence.Add(new("lineItems[0].description", line));
            evidence.Add(new("lineItems[0].amount", line));
        }
        else if (field != "totalDue")
            evidence.Add(new(field, field + " " + amount.ToString(CultureInfo.InvariantCulture)));
        return (string.Join("\n", evidence.Select(item => item.SourceExcerpt).Distinct()),
            candidate with { Evidence = evidence });
    }
}
