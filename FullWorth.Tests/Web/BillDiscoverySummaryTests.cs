using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using FullWorth.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FullWorth.Tests.Web;

public sealed class BillDiscoverySummaryTests
{
    [Theory]
    [InlineData("en", "Imported transactions", "Candidates accepted by current rules")]
    [InlineData("es", "Transacciones importadas", "Candidatos aceptados por las reglas actuales")]
    public async Task Summary_RendersCountsAndLocalizedGuidance(string culture, string imported, string accepted)
    {
        var data = new BillDiscoverySummary.Snapshot
        {
            RejectedCurrencyAmbiguity = 2,
            RejectedSourceAccountAmbiguity = 3,
            ImportedTransactions = 125,
            EligiblePostedTransactions = 111,
            FirstEligiblePostedDate = new DateOnly(2026, 1, 5),
            LastEligiblePostedDate = new DateOnly(2026, 3, 5),
            SingleObservationGroups = 7,
            GroupsWithoutSupportedMonthlyCadence = 6,
            DetectedMonthlyCandidates = 4,
            RejectedExplicitFallbackCategories = 1,
            RejectedInsufficientTwoObservationEvidence = 1,
            RejectedOtherWeakOrVariableEvidence = 1,
            AcceptedCandidates = 1
        };
        var html = WebUtility.HtmlDecode(await RenderAsync(culture, data));
        Assert.Contains(imported, html);
        Assert.Contains(accepted, html);
        Assert.Contains(culture == "es" ? "cuentas de origen distintas" :
            "different or partially unknown source accounts", html);
        Assert.Matches("<dd[^>]*>3</dd>", html);
        Assert.Matches("<dd[^>]*>125</dd>", html);
        Assert.Matches("<dd[^>]*>2</dd>", html);
        Assert.Matches("<dd[^>]*>111</dd>", html);
        Assert.Matches("<dd[^>]*>7</dd>", html);
        Assert.Matches("<dd[^>]*>6</dd>", html);
        Assert.Contains("href=\"/app/account\"", html);
        Assert.Contains("href=\"/app/account/transactions\"", html);
        Assert.DoesNotContain("<form", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmptyHistory_DoesNotClaimNoBillsOrSuccessfulSynchronization()
    {
        var html = WebUtility.HtmlDecode(await RenderAsync("en", new()));
        Assert.Contains("No eligible posted payments are available.", html);
        Assert.Contains("This does not prove that you have no bills.", html);
        Assert.Contains("without syncing your bank or changing monitored bills", html);
        Assert.Contains("not why a specific payment is missing", html);
        Assert.DoesNotContain("Eligible posted payments:", html);
    }

    [Fact]
    public async Task Coverage_DoesNotClaimCompleteHistoryOrSpecificMissingBillCause()
    {
        var html = WebUtility.HtmlDecode(await RenderAsync("en", new()
        {
            EligiblePostedTransactions = 2,
            FirstEligiblePostedDate = new DateOnly(2026, 1, 5),
            LastEligiblePostedDate = new DateOnly(2026, 2, 5)
        }));
        Assert.Contains("These dates do not prove uninterrupted bank coverage.", html);
        Assert.Contains("Weekly, biweekly, annual, and irregular payments may be missing.", html);
        Assert.Contains("not the same as your active bill count or total spending.", html);
        Assert.Contains("Do not post bank details", html);
    }

    private static async Task<string> RenderAsync(string culture, BillDiscoverySummary.Snapshot data)
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddLocalization(options => options.ResourcesPath = "Resources");
            await using var provider = services.BuildServiceProvider();
            await using var renderer = new HtmlRenderer(
                provider, provider.GetRequiredService<ILoggerFactory>());

            return await renderer.Dispatcher.InvokeAsync(async () =>
            {
                var output = await renderer.RenderComponentAsync<BillDiscoverySummary>(
                    ParameterView.FromDictionary(new Dictionary<string, object?> { ["Data"] = data }));
                return output.ToHtmlString();
            });
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }
}
