using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FullWorth.Tests.Infrastructure;
using FullWorth.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FullWorth.Tests.Security;

public sealed class PasskeySessionEnrollmentFailureTests
{
    [Theory]
    [InlineData(false, HttpStatusCode.TooManyRequests)]
    [InlineData(true, HttpStatusCode.TooManyRequests)]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.Unauthorized)]
    [InlineData(false, HttpStatusCode.InternalServerError)]
    [InlineData(true, HttpStatusCode.InternalServerError)]
    public async Task FailedRefreshEnrollmentNeverIssuesSessionOrRetriesConsumedProof(
        bool firstFactor, HttpStatusCode failure)
    {
        var responses = new List<HttpResponseMessage>
        {
            new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    tokenType = "Bearer", accessToken = "fixture-access-token",
                    expiresIn = 900, refreshToken = "fixture-refresh-token"
                })
            }
        };
        if (firstFactor)
            responses.Add(new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { email = "fixture@fullworth.local" })
            });
        responses.Add(new(failure) { Content = new StringContent("private upstream error") });
        using var handler = new ScriptedHttpMessageHandler(responses.ToArray());
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.fullworth.test") };
        var service = new WebAuthenticationService(new ClientFactory(client));
        var context = new DefaultHttpContext();

        AuthOperationResult result;
        if (firstFactor)
        {
            result = await service.LoginPasskeyAsync(context, new { ceremonyId = new string('c', 43),
                credentialJson = "{}" }, true, CancellationToken.None);
        }
        else
        {
            var pending = new PendingWebPasswordLogin(new string('p', 43),
                DateTimeOffset.UtcNow.AddMinutes(3), "fixture@fullworth.local", true, true);
            var step = await service.CompletePasswordSignInAsync(context, pending, null, null,
                new string('c', 43), "{}", CancellationToken.None);
            Assert.Null(step.Pending);
            result = step.Result;
        }

        Assert.False(result.Succeeded);
        Assert.Equal(failure == HttpStatusCode.TooManyRequests, result.IsRateLimited);
        Assert.False(context.User.Identity?.IsAuthenticated ?? false);
        Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
        Assert.DoesNotContain("private upstream error", result.ErrorMessage ?? "");
        Assert.DoesNotContain("fixture-access-token", result.ErrorMessage ?? "");
        Assert.DoesNotContain("fixture-refresh-token", result.ErrorMessage ?? "");
        Assert.Equal(firstFactor ? 3 : 2, handler.Requests.Count);
        Assert.Equal(firstFactor ? "/api/auth/passkeys/login" : "/api/auth/password/complete",
            handler.Requests[0].Uri.AbsolutePath);
        Assert.Equal("/api/auth/refresh", handler.Requests[^1].Uri.AbsolutePath);
        Assert.Single(handler.Requests.Where(request => request.Uri.AbsolutePath == "/api/auth/refresh"));
        if (result.IsRateLimited)
            Assert.Equal("Too many sign-in attempts. Wait a minute and try again.", result.ErrorMessage);
    }


    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task PrimaryAccountConfirmationFailureNeverStartsRefreshOrIssuesSession(HttpStatusCode failure)
    {
        using var handler = new ScriptedHttpMessageHandler(
            new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { tokenType = "Bearer", accessToken = "fixture-access-token",
                    expiresIn = 900, refreshToken = "fixture-refresh-token" })
            },
            new(failure));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.fullworth.test") };
        var service = new WebAuthenticationService(new ClientFactory(client));
        var context = new DefaultHttpContext();
        var result = await service.LoginPasskeyAsync(context,
            new { ceremonyId = new string('c', 43), credentialJson = "{}" }, false, CancellationToken.None);
        Assert.False(result.Succeeded);
        Assert.Equal(failure == HttpStatusCode.TooManyRequests, result.IsRateLimited);
        Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
        Assert.Equal(2, handler.Requests.Count);
        Assert.DoesNotContain(handler.Requests, request => request.Uri.AbsolutePath == "/api/auth/refresh");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WebPasskeyRoutesReportRefreshThrottlingWithoutCreatingASession(bool firstFactor)
    {
        var responses = new List<HttpResponseMessage>();
        if (!firstFactor)
            responses.Add(new(HttpStatusCode.Accepted)
            {
                Content = JsonContent.Create(new { loginId = new string('p', 43),
                    expiresAt = DateTimeOffset.UtcNow.AddMinutes(3), canUsePasskey = true })
            });
        responses.Add(new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { tokenType = "Bearer",
                accessToken = "fixture-access-token", expiresIn = 900, refreshToken = "fixture-refresh-token" })
        });
        if (firstFactor)
            responses.Add(new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { email = "fixture@fullworth.local" })
            });
        responses.Add(new(HttpStatusCode.TooManyRequests));
        using var handler = new ScriptedHttpMessageHandler(responses.ToArray());
        await using var root = new FullWorthWebFactory();
        await using var web = root.WithWebHostBuilder(builder => builder
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Passkeys:Enabled"] = "true" }))
            .ConfigureServices(services => services.AddHttpClient("FullWorthApi")
                .ConfigurePrimaryHttpMessageHandler(() => handler)));
        using var client = web.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
        });
        client.DefaultRequestHeaders.Add("X-FullWorth-Test-Anonymous", "true");
        var html = await client.GetStringAsync("/login");
        if (!firstFactor)
        {
            using var started = await client.PostAsync("/auth/login",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["email"] = "fixture@fullworth.local", ["password"] = "FullWorth!Tests123",
                    ["__RequestVerificationToken"] = AntiforgeryToken(html)
                }));
            Assert.Equal(HttpStatusCode.Redirect, started.StatusCode);
            html = await client.GetStringAsync(started.Headers.Location);
        }
        using var request = new HttpRequestMessage(HttpMethod.Post,
            firstFactor ? "/auth/passkeys/login" : "/auth/login/passkey-complete")
        {
            Content = JsonContent.Create(new { ceremonyId = new string('c', 43),
                credentialJson = "{}", twoFactorCode = (string?)null, rememberMe = true })
        };
        request.Headers.Add("X-CSRF-TOKEN", AntiforgeryToken(html));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var cookieName = web.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme).Cookie.Name!;
        IEnumerable<string> cookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];
        Assert.DoesNotContain(cookies, value => value.StartsWith(cookieName + "=", StringComparison.Ordinal));
        Assert.Equal(3, handler.Requests.Count);
        Assert.Single(handler.Requests.Where(item => item.Uri.AbsolutePath == "/api/auth/refresh"));
    }

    private static string AntiforgeryToken(string html)
    {
        var input = Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>");
        Assert.True(input.Success);
        var value = Regex.Match(input.Value, "value=\"([^\"]+)\"");
        Assert.True(value.Success);
        return WebUtility.HtmlDecode(value.Groups[1].Value);
    }

    private sealed class ClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            Assert.Equal("FullWorthApi", name);
            return client;
        }
    }
}
