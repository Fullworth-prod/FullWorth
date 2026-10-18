using System.Net;
using FullWorth.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FullWorth.Tests.Web;

public sealed class AlertCardReviewLinkTests
{
    [Fact]
    public async Task ReviewIsARealEvidenceLink_EvenWhileReadMutationIsBusy()
    {
        var id = Guid.NewGuid();
        var html = await Render(new() { Id = Guid.NewGuid(), BillStreamId = id, Title = "<script>private</script>", Severity = null! },
            $"/app/bills/{id:D}", true);
        Assert.Contains($"href=\"/app/bills/{id:D}\"", html);
        Assert.Contains("severity-info", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("javascript:", html);
        Assert.Contains("aria-busy=\"true\"", html);
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/app/account/settings")]
    public async Task UnmatchedOrExternalDestinationsAreNotRendered(string href)
    {
        var html = await Render(new() { AlertType = "ConnectionIssue" }, href, false);
        Assert.DoesNotContain("href=", html);
    }

    [Fact]
    public async Task ConnectionEvidenceLinkDoesNotNeedReadOrDismissToSucceed()
    {
        var html = WebUtility.HtmlDecode(await Render(new() { AlertType = "ConnectionIssue" }, "/app/account", false));
        Assert.Contains("href=\"/app/account\"", html);
        Assert.Contains("Mark read", html);
        Assert.Contains("Dismiss", html);
    }

    private static async Task<string> Render(AlertCard.AlertCardViewModel alert, string href, bool busy)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var rendered = await renderer.RenderComponentAsync<AlertCard>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { ["Alert"] = alert, ["CanReview"] = true, ["ReviewHref"] = href, ["IsBusy"] = busy }));
            return rendered.ToHtmlString();
        });
    }
}
