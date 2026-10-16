using FullWorth.Core.Configuration;

namespace FullWorth.Tests.Configuration;

public sealed class FullWorthWebNavigationTests
{
    [Fact]
    public void EntryPointIsAuthenticatedWebsiteNotApiOrDuplicateNativeLogin()
    {
        Assert.Equal("https://fullworth.org/app", FullWorthWebNavigation.EntryPoint.AbsoluteUri);
        Assert.Equal(FullWorthWebDestination.InApp,
            FullWorthWebNavigation.Classify(FullWorthWebNavigation.EntryPoint.AbsoluteUri));
    }

    [Theory]
    [InlineData("https://fullworth.org/")]
    [InlineData("https://FULLWORTH.ORG/app/bills")]
    [InlineData("https://fullworth.org:443/login?ReturnUrl=%2Fapp")]
    [InlineData("https://fullworth.org/auth/login")]
    [InlineData("https://fullworth.org/login?twoFactor=true")]
    [InlineData("https://fullworth.org/app/account#security")]
    [InlineData("https://fullworth.org/app?return=https%3A%2F%2Fexample.com")]
    public void WebsitePagesAndAuthStayInSameWebSession(string url) =>
        Assert.Equal(FullWorthWebDestination.InApp, FullWorthWebNavigation.Classify(url));

    [Theory]
    [InlineData("https://example.com/")]
    [InlineData("https://fullworth.org.evil.example/login")]
    [InlineData("https://evilfullworth.org/")]
    [InlineData("https://api.fullworth.org/")]
    [InlineData("https://www.fullworth.org/")]
    [InlineData("https://fullworth.org./")]
    [InlineData("https://example.com/?return=https://fullworth.org")]
    public void OtherOriginsAreNeverEmbedded(string url) =>
        Assert.Equal(FullWorthWebDestination.ExternalBrowser, FullWorthWebNavigation.Classify(url));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("/app")]
    [InlineData("//fullworth.org/app")]
    [InlineData("http://fullworth.org/app")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,test")]
    [InlineData("file:///etc/passwd")]
    [InlineData("about:blank")]
    [InlineData("intent://fullworth.org/")]
    [InlineData("tel:12345")]
    [InlineData("https://fullworth.org:444/app")]
    [InlineData("https://username:password@fullworth.org/app")]
    [InlineData("https://fullworth.org@evil.example/")]
    [InlineData("https://127.0.0.1/")]
    [InlineData("https://[::1]/")]
    [InlineData("https://fullworth.org/\napp")]
    public void UnsupportedOrCredentialBearingUrlsAreBlocked(string? url) =>
        Assert.Equal(FullWorthWebDestination.Blocked, FullWorthWebNavigation.Classify(url));
}
