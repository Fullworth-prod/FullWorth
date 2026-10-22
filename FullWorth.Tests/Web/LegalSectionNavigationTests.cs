using System.Net;
using System.Text.RegularExpressions;
using FullWorth.Tests.Infrastructure;

namespace FullWorth.Tests.Web;

public sealed class LegalSectionNavigationTests
{
    [Theory]
    [InlineData("/terms", 15)]
    [InlineData("/privacy", 11)]
    public async Task EveryContentsLink_ResolvesWithinItsDocument_WithAnExistingTarget(
        string route, int expectedSections)
    {
        using var factory = new FullWorthWebFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-FullWorth-Test-Anonymous", "true");
        using var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        // Resolve the actual rendered hrefs against the rendered base element,
        // as browsers do. Fragment-only hrefs resolve to / under Blazor's base,
        // even when every referenced section exists in this document.
        var baseElement = Match(html, @"<base\b[^>]*href=""([^""]+)""");
        Assert.True(baseElement.Success, "The rendered document must expose its base URL.");
        var documentUri = new Uri(client.BaseAddress!, route);
        var baseUri = new Uri(documentUri, WebUtility.HtmlDecode(baseElement.Groups[1].Value));
        var contents = Match(html, @"<aside\b[^>]*class=""legal-toc""[^>]*>(.*?)</aside>");
        Assert.True(contents.Success, "The legal document must retain its contents navigation.");
        var links = Matches(contents.Groups[1].Value, @"<a\b[^>]*href=""([^""]+)""");
        Assert.Equal(expectedSections, links.Count);

        foreach (Match link in links)
        {
            var href = WebUtility.HtmlDecode(link.Groups[1].Value);
            var destination = new Uri(baseUri, href);
            Assert.Equal(documentUri.Scheme, destination.Scheme);
            Assert.Equal(documentUri.Authority, destination.Authority);
            Assert.Equal(documentUri.AbsolutePath, destination.AbsolutePath);
            Assert.Empty(destination.Query);
            Assert.False(string.IsNullOrEmpty(destination.Fragment));
            var id = Uri.UnescapeDataString(destination.Fragment[1..]);
            Assert.Equal(1, Matches(html, @"\bid=""" + Regex.Escape(id) + @"""").Count);
        }
    }

    private static Match Match(string html, string pattern) =>
        Regex.Match(html, pattern, RegexOptions.Singleline | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

    private static MatchCollection Matches(string html, string pattern) =>
        Regex.Matches(html, pattern, RegexOptions.Singleline | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
}
