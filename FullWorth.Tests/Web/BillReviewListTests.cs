using System.Globalization;
using System.Net;
using FullWorth.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FullWorth.Tests.Web;

public sealed class BillReviewListTests
{
    [Theory]
    [InlineData("en", "Bills needing review", "Review bill")]
    [InlineData("es", "Facturas que necesitan revisión", "Revisar factura")]
    public async Task ReviewList_ExplainsExcludedBills_AndLinksToOwnedEvidence(string culture, string heading, string action)
    {
        var id = Guid.NewGuid();
        var html = await RenderAsync(culture, [new(id, "<script>private provider</script>")]);
        var decoded = WebUtility.HtmlDecode(html);
        Assert.Contains(heading, decoded);
        Assert.Contains(action, decoded);
        Assert.Contains($"/app/bills/{id:D}", html);
        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<form", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$0", html);
        if (culture == "en")
            Assert.Contains("excluded from monitored totals", decoded);
    }

    private static async Task<string> RenderAsync(string culture, IReadOnlyList<BillReviewList.Item> data)
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
                var output = await renderer.RenderComponentAsync<BillReviewList>(
                    ParameterView.FromDictionary(new Dictionary<string, object?> { ["Bills"] = data }));
                return output.ToHtmlString();
            });
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }
}
