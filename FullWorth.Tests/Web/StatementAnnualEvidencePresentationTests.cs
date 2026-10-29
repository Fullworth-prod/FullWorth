using System.Reflection;
using System.Text.Json;
using FullWorth.Web.Components.Pages.App;
using Microsoft.Extensions.Localization;

namespace FullWorth.Tests.Web;

public sealed class StatementAnnualEvidencePresentationTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OldResponseWithoutEvidence_CannotBecomeAnAnnualClaim(bool includeUnit)
    {
        var change = JsonSerializer.Deserialize<BillDetail.BillChangeViewModel>(
            "{\"currencyCode\":\"USD\",\"annualizedImpact\":240}",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.False(change.HasAnnualizedImpactEvidence);
        Assert.Equal("Yearly estimate unavailable", Format(change, includeUnit));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("uſd")]
    public void InvalidUnit_CannotAcquireAnAnnualLabelEvenWithEvidenceFlag(string? currency)
    {
        var change = new BillDetail.BillChangeViewModel
            { CurrencyCode = currency, AnnualizedImpact = 240m, HasAnnualizedImpactEvidence = true };
        Assert.Equal("Yearly estimate unavailable", Format(change, false));
        Assert.Equal("Yearly estimate unavailable", Format(change, true));
    }

    [Fact]
    public void QualifiedMonthlyEstimate_PreservesItsAmountAndHistoryUnit()
    {
        var change = new BillDetail.BillChangeViewModel
            { CurrencyCode = "USD", AnnualizedImpact = 240m, HasAnnualizedImpactEvidence = true };
        Assert.Equal("+$240.00", Format(change, false));
        Assert.Equal("+$240.00 / year", Format(change, true));
    }

    private static string Format(BillDetail.BillChangeViewModel change, bool includeUnit)
    {
        var page = new BillDetail();
        typeof(BillDetail).GetProperty("L", Flags)!.SetValue(page, new Localizer());
        return (string)typeof(BillDetail).GetMethod("FormatAnnualImpact", Flags)!
            .Invoke(page, [change, includeUnit])!;
    }

    private sealed class Localizer : IStringLocalizer<BillDetail>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
