using FullWorth.API.Services.Statements;

namespace FullWorth.Tests.Services;

public sealed class BillStatementAiMoneySourceBoundaryTests
{
    private static readonly string[] Fields =
    [
        BillStatementAiFactKeys.PreviousBalance,
        BillStatementAiFactKeys.Payments,
        BillStatementAiFactKeys.CurrentCharges,
        BillStatementAiFactKeys.TotalDue,
        BillStatementAiFactKeys.LineItemAmount(0)
    ];

    public static TheoryData<string, string, string, decimal> CroppedAmounts
    {
        get
        {
            var data = new TheoryData<string, string, string, decimal>();
            foreach (var factKey in Fields)
            foreach (var row in new (string Source, string Excerpt, decimal Value)[]
            {
                ("Amount due $110.005", "10.00", 10.00m),
                ("Amount due -$104.99", "$104.99", 104.99m),
                ("Amount due ($104.99)", "$104.99", 104.99m),
                ("Amount due $1,104.99", "104.99", 104.99m),
                ("Amount due $104.995", "104.99", 104.99m),
                ("Amount due $1104.99", "104.99", 104.99m),
                ("Amount due $0.005", "0.00", 0m),
                ("Amount due $-104.99", "104.99", 104.99m),
                ("Amount due ($104.99", "$104.99", 104.99m),
                ("Amount due $104.99)", "$104.99", 104.99m),
                ("Statement date August 10, 2026", "2026", 2026m)
            })
                data.Add(factKey, row.Source, row.Excerpt, row.Value);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CroppedAmounts))]
    public void CroppedSourceCannotChangeSignedMoneyValue(
        string factKey, string source, string excerpt, decimal value)
    {
        var (document, candidate) = Create(factKey, source, excerpt, value);
        var validation = new BillStatementAiCandidateValidator().Validate(document, candidate);
        var result = new BillStatementAiCandidateConversionService(new()).Convert(document, candidate);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains(factKey, StringComparison.Ordinal));
        Assert.False(result.IsAccepted);
        Assert.Null(result.Extraction);
        Assert.All(result.Errors, error => Assert.DoesNotContain(source, error, StringComparison.Ordinal));
    }

    public static TheoryData<string, string, string, decimal> CompleteAmounts
    {
        get
        {
            var data = new TheoryData<string, string, string, decimal>();
            foreach (var factKey in Fields)
            foreach (var row in new (string Source, string Excerpt, decimal Value)[]
            {
                ("Amount $104.99", "$104.99", 104.99m),
                ("Amount -$104.99", "-$104.99", -104.99m),
                ("Amount ($104.99)", "($104.99)", -104.99m),
                ("Amount $104.990", "$104.99", 104.99m),
                ("Amount $1,104.99", "$1,104.99", 1104.99m),
                ("Amount $104.99", "Amount $104.99", 104.99m),
                ("Unrelated $1104.99; amount $104.99", "104.99", 104.99m),
                ("August 10, 2026 charge \t$104.99", "August 10, 2026 charge $104.99", 104.99m)
            })
            {
                if (factKey == BillStatementAiFactKeys.TotalDue && row.Value < 0m) continue;
                data.Add(factKey, row.Source, row.Excerpt, row.Value);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CompleteAmounts))]
    public void CompleteSourceValueRemainsUsableWithoutChangingAmount(
        string factKey, string source, string excerpt, decimal value)
    {
        var (document, candidate) = Create(factKey, source, excerpt, value);
        var result = new BillStatementAiCandidateConversionService(new()).Convert(document, candidate);

        Assert.True(result.IsAccepted, string.Join("; ", result.Errors));
        Assert.NotNull(result.Extraction);
        Assert.True(result.Extraction.IsReadyForValidation);
        Assert.Equal(factKey == BillStatementAiFactKeys.TotalDue ? value : 1.23m,
            result.Extraction.Statement.TotalAmount);
        if (factKey == BillStatementAiFactKeys.LineItemAmount(0))
            Assert.Equal(value, Assert.Single(result.Extraction.LineItems).Amount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepeatedMisleadingOccurrences_RequireACompleteMatchingSourceAmount(bool hasMatch)
    {
        var source = string.Join("; ", Enumerable.Repeat("Amount $1104.99", 5000)) +
            (hasMatch ? "; Amount $104.99" : "");
        var (document, candidate) = Create(BillStatementAiFactKeys.TotalDue,
            source, "104.99", 104.99m);
        var result = new BillStatementAiCandidateConversionService(new()).Convert(document, candidate);

        Assert.Equal(hasMatch, result.IsAccepted);
        if (!hasMatch) Assert.Null(result.Extraction);
    }

    private static (string Document, BillStatementAiCandidate Candidate) Create(
        string factKey, string source, string excerpt, decimal value)
    {
        const string period = "Billing period August 1, 2026 through August 31, 2026";
        var targetIsTotal = factKey == BillStatementAiFactKeys.TotalDue;
        var targetIsLine = factKey == BillStatementAiFactKeys.LineItemAmount(0);
        var document = period + "\nCurrency: USD\n" +
            (targetIsTotal ? "" : "Total due $1.23\n") +
            (targetIsLine ? "Target charge " : "") + source;
        var evidence = new List<BillStatementAiEvidence>
        {
            new(BillStatementAiFactKeys.BillingPeriodStart, period),
            new(BillStatementAiFactKeys.BillingPeriodEnd, period),
            new(BillStatementAiFactKeys.CurrencyCode, "Currency: USD"),
            new(factKey, excerpt)
        };
        if (!targetIsTotal)
            evidence.Add(new(BillStatementAiFactKeys.TotalDue, "Total due $1.23"));
        if (targetIsLine)
            evidence.Add(new(BillStatementAiFactKeys.LineItemDescription(0), "Target charge"));

        return (document, new BillStatementAiCandidate(
            ProviderName: null,
            AccountIdentifierSuffix: null,
            BillingPeriodStart: new DateOnly(2026, 8, 1),
            BillingPeriodEnd: new DateOnly(2026, 8, 31),
            StatementDate: null,
            DueDate: null,
            PreviousBalance: factKey == BillStatementAiFactKeys.PreviousBalance ? value : null,
            Payments: factKey == BillStatementAiFactKeys.Payments ? value : null,
            CurrentCharges: factKey == BillStatementAiFactKeys.CurrentCharges ? value : null,
            TotalDue: targetIsTotal ? value : 1.23m,
            CurrencyCode: "USD",
            PlanOrService: null,
            UsageSummary: null,
            LineItems: targetIsLine ?
                [new BillStatementAiLineItemCandidate("Target charge", value,
                    value < 0m ? BillStatementAiLineItemKind.Credit : BillStatementAiLineItemKind.Service)] : [],
            Evidence: evidence,
            ModelConfidence: BillStatementAiModelConfidence.Unknown));
    }
}
