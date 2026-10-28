using System.Net;
using FullWorth.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace FullWorth.Tests.Web;

public sealed class RecoveryLocalizationTests
{
    [Theory]
    [InlineData("en-US", "We couldn't find that page.")]
    [InlineData("es", "No pudimos encontrar esa página.")]
    public async Task MissingDocument_LocalizesRecoveryAndKeeps404(string language, string heading)
    {
        using var factory = new FullWorthWebFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        using var response = await client.GetAsync("/a-page-that-does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(heading, html);
        Assert.Contains("id=\"not-found-title\"", html);
        Assert.Contains("href=\"/app\"", html);
        Assert.Contains("href=\"/\"", html);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Theory]
    [InlineData("en-US", "FullWorth hit a snag.", "Reference")]
    [InlineData("es", "FullWorth tuvo un problema.", "Referencia")]
    public async Task ErrorDocument_LocalizesWithoutLosingSafeRecoveryAndReference(
        string language, string heading, string reference)
    {
        using var factory = new FullWorthWebFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        using var response = await client.GetAsync("/Error");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(heading, html);
        Assert.Contains(reference, html);
        Assert.Contains("system-request-id", html);
        Assert.Contains("href=\"/app\"", html);
        Assert.Contains("href=\"/\"", html);
    }

    [Theory]
    [InlineData("en-US", false, "Passkeys & security keys", "Connection status")]
    [InlineData("en-US", true, "Passkeys & security keys", "Connection status")]
    [InlineData("es", false, "Claves de acceso y llaves de seguridad", "Estado de la conexión")]
    [InlineData("es", true, "Claves de acceso y llaves de seguridad", "Estado de la conexión")]
    public async Task SecurityKeys_LocalizesBothFlagStates_WithoutChangingControls(
        string language, bool enabled, string heading, string connection)
    {
        using var original = new FullWorthWebFactory();
        using var factory = original.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Passkeys:Enabled"] = enabled.ToString()
                })));
        using var client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        using var response = await client.GetAsync("/app/account/security-keys");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(heading, html);
        Assert.Contains($"aria-label=\"{connection}\"", html);
        foreach (var id in new[]
        {
            "components-reconnect-modal", "components-seconds-to-next-attempt",
            "components-reconnect-button", "components-resume-button"
        }) Assert.Contains($"id=\"{id}\"", html);
        if (enabled)
        {
            Assert.Contains("data-passkeys=\"manage\"", html);
            Assert.Contains("name=\"currentPassword\"", html);
            Assert.Contains("name=\"twoFactorCode\"", html);
            Assert.Contains("data-passkey-action=\"register\"", html);
            Assert.Contains("data-passkey-action=\"retry\"", html);
        }
        else
        {
            Assert.DoesNotContain("data-passkeys=\"manage\"", html);
            Assert.DoesNotContain("data-passkey-action=\"register\"", html);
            Assert.Contains(language == "es"
                ? "Tu método de inicio de sesión actual sigue funcionando."
                : "Your existing sign-in still works.", html);
        }
    }
}
