using System.Globalization;
using System.Net;
using FullWorth.Web.Components.Shared;
using FullWorth.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FullWorth.Tests.Web;

public sealed class OverviewBillSummaryTests
{
    [Theory]
    [InlineData("en", "Latest recognized bill charges", "Comparison history needed")]
    [InlineData("es", "Últimos cargos de facturas reconocidas", "Se necesita historial para comparar")]
    public async Task NoHistory_ShowsCoverageAndUnknownRatherThanFalseCalm(
        string culture, string heading, string unknown)
    {
        var summary = OverviewBillCoverage.Create([
            OverviewBillCoverageTests.Bill(29m, 0m), OverviewBillCoverageTests.Bill(13.8m, 0m)]);
        var html = await RenderAsync(culture, summary);
        Assert.Contains(heading, html);
        Assert.Contains("$42.80", html);
        Assert.Contains(unknown, html);
        Assert.DoesNotContain("$0.00", html);
        Assert.DoesNotContain("Everything looks steady", html);
        Assert.DoesNotContain("is-calm", html);
        Assert.Contains("href=\"/app/account/transactions\"", html);
    }

    [Fact]
    public async Task ComparedZero_IsNotAmountUnavailableOrAnnualSavings()
    {
        var summary = OverviewBillCoverage.Create([OverviewBillCoverageTests.Bill(29m, 29m)]);
        var html = await RenderAsync("en", summary);
        Assert.Contains("$0.00", html);
        Assert.Contains("No increase to project", html);
        Assert.Contains("This is not total spending or a monthly budget", html);
        Assert.Contains("A sync timestamp does not confirm every connection", html);
        Assert.DoesNotContain("Amount unavailable", html);
        Assert.DoesNotContain("Yearly estimate unavailable", html);
    }

    [Theory]
    [InlineData("en", "Only some bills can be compared")]
    [InlineData("es", "Solo se pueden comparar algunas facturas")]
    public async Task PartialHistory_NeverClaimsEveryBillUnchanged(string culture, string heading)
    {
        var summary = OverviewBillCoverage.Create([
            OverviewBillCoverageTests.Bill(29m, 29m), OverviewBillCoverageTests.Bill(13.8m, 0m)]);
        var html = await RenderAsync(culture, summary, true);
        Assert.Contains(heading, html);
        Assert.DoesNotContain("No increases in compared bills", html);
        Assert.DoesNotContain("Everything looks steady", html);
        Assert.Contains("href=\"/app/bills\"", html);
    }

    [Fact]
    public async Task MissingAmounts_AreCountedAndExcluded()
    {
        var summary = OverviewBillCoverage.Create([
            OverviewBillCoverageTests.Bill(29m), OverviewBillCoverageTests.Bill(999m, currency: null)]);
        var html = await RenderAsync("en", summary);
        Assert.Contains("$29.00", html);
        Assert.Contains("1 of 2 active bills have a usable amount and currency.", html);
        Assert.Contains("1 bills are excluded", html);
        Assert.Contains("Uncompared bills are not confirmed unchanged.", html);
        Assert.DoesNotContain("999", html);
    }

    [Fact]
    public async Task Increase_EncodesProviderAndLinksToEvidenceWithoutMutations()
    {
        var bill = OverviewBillCoverageTests.Bill() with { ProviderName = "<script>example</script>" };
        var summary = OverviewBillCoverage.Create([bill]);
        var html = await RenderAsync("en", summary, true, decode: false);
        Assert.Contains($"/app/bills/{bill.Id:D}", html);
        Assert.Contains("$9.00", html);
        Assert.Contains("Bills need review", html);
        Assert.Contains("does not establish a provider rate increase", html);
        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<form", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<button", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MixedCurrencies_KeepIncreasesUnranked()
    {
        var summary = OverviewBillCoverage.Create([
            OverviewBillCoverageTests.Bill(), OverviewBillCoverageTests.Bill(50m, 40m, "EUR")]);
        var html = await RenderAsync("en", summary, true);
        Assert.Contains("Different currencies are not ranked together.", html);
        Assert.DoesNotContain("/app/bills/", html);
        Assert.Contains("Bills need review", html);
    }

    [Fact]
    public async Task UnsupportedTiming_IsNotSilentlyAnnualized()
    {
        var summary = OverviewBillCoverage.Create([OverviewBillCoverageTests.Bill(monthly: false)]);
        var html = await RenderAsync("en", summary);
        Assert.Contains("Monthly timing not established", html);
        Assert.DoesNotContain("$108.00", html);
    }

    [Theory]
    [InlineData("en", "Bill amounts need review", "Amount or currency unavailable")]
    [InlineData("es", "Hay importes de facturas que necesitan revisión", "Importe o moneda no disponible")]
    public async Task NoUsableAmounts_IsNotMisdiagnosedAsMissingHistory(
        string culture, string status, string unavailable)
    {
        var summary = OverviewBillCoverage.Create([
            OverviewBillCoverageTests.Bill(29m, 20m, currency: null)]);
        Assert.Contains(status, await RenderAsync(culture, summary, true));
        var html = await RenderAsync(culture, summary);
        Assert.Contains(unavailable, html);
        Assert.DoesNotContain("$0.00", html);
        Assert.DoesNotContain("Comparison history needed", html);
    }

    private static async Task<string> RenderAsync(string culture, OverviewBillCoverage summary,
        bool statusOnly = false, bool decode = true)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddLocalization(options => options.ResourcesPath = "Resources");
            await using var provider = services.BuildServiceProvider();
            await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
            var html = await renderer.Dispatcher.InvokeAsync(async () =>
            {
                var output = await renderer.RenderComponentAsync<OverviewBillSummary>(
                    ParameterView.FromDictionary(new Dictionary<string, object?>
                    {
                        ["Summary"] = summary, ["StatusOnly"] = statusOnly,
                        ["LatestSuccessfulSync"] = DateTimeOffset.Parse("2026-10-18T12:00:00Z", CultureInfo.InvariantCulture)
                    }));
                return output.ToHtmlString();
            });
            return decode ? WebUtility.HtmlDecode(html) : html;
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }
}
