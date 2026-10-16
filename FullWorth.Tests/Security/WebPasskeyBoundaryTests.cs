using System.Net;
using System.Net.Http.Json;
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
