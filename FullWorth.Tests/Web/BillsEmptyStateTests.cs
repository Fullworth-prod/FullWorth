using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using FullWorth.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FullWorth.Tests.Web;

public sealed class BillsEmptyStateTests
{
    [Theory]
    [InlineData("en", "No recurring bills to show yet.", "Next steps for your bills",
        "Check bank connections", "Review imported transactions", "Check monitoring progress")]
    [InlineData("es", "Aún no hay facturas recurrentes para mostrar.", "Próximos pasos para tus facturas",
        "Revisar conexiones bancarias", "Revisar transacciones importadas", "Revisar el progreso del monitoreo")]
    public async Task RenderedEmptyState_ProvidesLocalizedReadOnlyNextSteps(
        string culture, string heading, string navigationLabel,
        string connections, string transactions, string progress)
    {
        var html = WebUtility.HtmlDecode(await RenderAsync(culture));

        Assert.Contains($">{heading}</h2>", html);
        Assert.Contains("aria-labelledby=\"bills-empty-title\"", html);
        Assert.Contains($"aria-label=\"{navigationLabel}\"", html);
        Assert.Contains(connections, html);
        Assert.Contains(transactions, html);
        Assert.Contains(progress, html);
        Assert.Equal(
            new[] { "/app/account", "/app/account/transactions", "/app" },
            Regex.Matches(html, "<a[^>]+href=\"([^\"]+)\"")
                .Select(match => match.Groups[1].Value).ToArray());
        Assert.DoesNotContain("<form", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<button", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenderedEmptyState_DoesNotClaimSuccessfulSyncOrGuaranteedDiscovery()
    {
        var html = WebUtility.HtmlDecode(await RenderAsync("en"));

        Assert.Contains("An empty list does not confirm that your bank is connected or syncing.", html);
        Assert.Contains("Reconnect any bank that needs attention.", html);
        Assert.Contains("If your bank is syncing successfully", html);
        Assert.Contains("more history may be needed", html);
        Assert.DoesNotContain("Automatic monitoring is active", html);
        Assert.DoesNotContain("$0", html);
    }

    private static async Task<string> RenderAsync(string culture)
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
                var output = await renderer.RenderComponentAsync<BillsEmptyState>(ParameterView.Empty);
                return output.ToHtmlString();
            });
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }
}
