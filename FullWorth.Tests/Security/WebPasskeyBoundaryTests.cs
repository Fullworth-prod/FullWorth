using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FullWorth.Tests.Infrastructure;

namespace FullWorth.Tests.Security;

public sealed class WebPasskeyBoundaryTests
{
    [Theory]
    [InlineData("/auth/passkeys/options")]
    [InlineData("/auth/passkeys/login")]
    public async Task AnonymousSignInPostsRequireAntiforgery(string path)
    {
        await using var factory = new FullWorthWebFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-FullWorth-Test-Anonymous", "true");
        var response = await client.PostAsJsonAsync(path,
            new { ceremonyId = new string('a', 43), credentialJson = "{}" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedPasskeyNavigationRejectsMissingAntiforgery()
    {
        await using var factory = new FullWorthWebFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.PostAsync("/auth/passkeys/continue",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["returnUrl"] = "/app/bills"
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedPasskeyNavigationRejectsAnonymousEvenWithValidAntiforgery()
    {
        await using var factory = new FullWorthWebFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-FullWorth-Test-Anonymous", "true");
        var token = await GetLoginAntiforgeryAsync(client);

        using var response = await client.PostAsync("/auth/passkeys/continue",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["returnUrl"] = "/app/bills"
            }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/app", "/app")]
    [InlineData("/app/bills", "/app/bills")]
    [InlineData("/app?tab=bills", "/app?tab=bills")]
    [InlineData("https://outside.example", "/app")]
    [InlineData("javascript:alert(1)", "/app")]
    [InlineData("//outside.example", "/app")]
    [InlineData("/\\\\outside.example", "/app")]
    [InlineData("/%2f%2foutside.example", "/app")]
    [InlineData("/%252f%252foutside.example", "/app")]
    [InlineData("/%5coutside.example", "/app")]
    [InlineData("/%0d%0aLocation:outside", "/app")]
    public async Task ProtectedPasskeyNavigationNormalizesUntrustedDestinationsOnServer(
        string value, string expected)
    {
        await using var factory = new FullWorthWebFactory();
        using var client = factory.CreateHttpsClient();
        // The factory's default synthetic principal uses a new GUID on each
        // request. Fix the owner identity across token issuance and POST so
        // antiforgery tests exercise the endpoint rather than user mismatch.
        client.DefaultRequestHeaders.Add(
            "X-FullWorth-Test-UserId", "11111111-1111-1111-1111-111111111111");
        var token = await GetLoginAntiforgeryAsync(client);

        using var response = await client.PostAsync("/auth/passkeys/continue",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["returnUrl"] = value
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expected, response.Headers.Location?.OriginalString);
        // Other security middleware may add no-cache/must-revalidate;
        // require no-store without rejecting stronger cache controls.
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    private static async Task<string> GetLoginAntiforgeryAsync(HttpClient client)
    {
        var html = await client.GetStringAsync("/login");
        var tag = Regex.Match(html,
            @"<input[^>]*name=""__RequestVerificationToken""[^>]*>");
        Assert.True(tag.Success, "Login page must issue an antiforgery token.");
        var value = Regex.Match(tag.Value, @"value=""([^""]+)""");
        Assert.True(value.Success);
        return WebUtility.HtmlDecode(value.Groups[1].Value);
    }

    [Theory]
    [InlineData("/bff/account/security/passkeys/options")]
    [InlineData("/bff/account/security/passkeys/register")]
    [InlineData("/bff/account/security/passkeys/remove")]
    public async Task AnonymousKeyMutationsAreRejected(string path)
    {
        await using var factory = new FullWorthWebFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-FullWorth-Test-Anonymous", "true");
        var response = await client.PostAsJsonAsync(path, new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
