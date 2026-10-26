using System.Net;
using System.Security.Claims;
using FullWorth.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FullWorth.Tests.Security;

public sealed class AdminBffWriteProxySessionSecurityTests
{
    [Fact]
    public async Task PasswordChange_Success_SignsOutWebSession()
    {
        var authentication =
            new RecordingAuthenticationService();

        var httpContext =
            CreateHttpContext(authentication);

        var proxy =
            CreateProxy(
                HttpStatusCode.NoContent);

        _ =
            await proxy.ForwardJsonAsync(
                httpContext,
                HttpMethod.Post,
                "/api/account/security/password",
                new
                {
                    currentPassword =
                        "current-password",

                    newPassword =
                        "replacement-password",

                    twoFactorCode =
                        (string?)null
                });

        Assert.Equal(
            1,
            authentication.SignOutCount);

        Assert.Equal(
            CookieAuthenticationDefaults
                .AuthenticationScheme,
            authentication.LastSignOutScheme);
    }

    [Fact]
    public async Task PasswordChange_Failure_PreservesWebSession()
    {
        var authentication =
            new RecordingAuthenticationService();

        var httpContext =
            CreateHttpContext(authentication);

        var proxy =
            CreateProxy(
                HttpStatusCode.BadRequest);

        _ =
            await proxy.ForwardJsonAsync(
                httpContext,
                HttpMethod.Post,
                "/api/account/security/password",
                new
                {
                    currentPassword =
                        "wrong-password",

                    newPassword =
                        "replacement-password",

                    twoFactorCode =
                        (string?)null
                });

        Assert.Equal(
            0,
            authentication.SignOutCount);
    }

    [Fact]
    public async Task OrdinaryAccountSecurityWrite_DoesNotSignOutWebSession()
    {
        var authentication =
            new RecordingAuthenticationService();

        var httpContext =
            CreateHttpContext(authentication);

        var proxy =
            CreateProxy(
                HttpStatusCode.NoContent);

        _ =
            await proxy.ForwardJsonAsync(
                httpContext,
                HttpMethod.Post,
                "/api/account/security/profile",
                new
                {
                    displayName =
                        "Security Test"
                });

        Assert.Equal(
            0,
            authentication.SignOutCount);
    }

    [Theory]
    [InlineData("/api/subscription-reviews", 200)]
    [InlineData("/api/subscription-reviews/11111111-1111-1111-1111-111111111111/evidence", 200)]
    [InlineData("/api/subscription-reviews", 409)]
    [InlineData("/api/subscription-reviews/11111111-1111-1111-1111-111111111111/evidence", 503)]
    public async Task SubscriptionWrites_ForwardOnlyTheExpectedPost_WithoutReplayingFailures(
        string path, int status)
    {
        var authentication = new RecordingAuthenticationService();
        var handler = new StaticResponseHandler((HttpStatusCode)status);
        var proxy = CreateProxy((HttpStatusCode)status, handler);
        var result = await proxy.ForwardJsonAsync(
            CreateHttpContext(authentication), HttpMethod.Post, path, new { synthetic = true });
        Assert.Equal(status, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal(path, handler.LastPath);
        Assert.Equal("Bearer", handler.LastAuthorizationScheme);
        Assert.Equal(0, authentication.SignOutCount);
    }

    [Theory]
    [InlineData("PUT", "/api/subscription-reviews")]
    [InlineData("PUT", "/api/subscription-reviews/11111111-1111-1111-1111-111111111111/evidence")]
    [InlineData("DELETE", "/api/subscription-reviews")]
    [InlineData("POST", "/api/subscription-reviews/00000000-0000-0000-0000-000000000000/evidence")]
    [InlineData("POST", "/api/subscription-reviews/not-a-guid/evidence")]
    [InlineData("POST", "/api/subscription-reviews/evidence")]
    [InlineData("POST", "/api/subscription-reviews//evidence")]
    [InlineData("POST", "/api/subscription-reviews/11111111-1111-1111-1111-111111111111")]
    [InlineData("POST", "/api/subscription-reviews/11111111-1111-1111-1111-111111111111/evidence/extra")]
    [InlineData("POST", "/api/subscription-reviews/../account")]
    [InlineData("POST", "/api/subscription-reviews?next=/api/admin/users")]
    [InlineData("POST", "https://example.invalid/api/subscription-reviews")]
    [InlineData("POST", "//example.invalid/api/subscription-reviews")]
    public async Task SubscriptionWriteLookalikes_AreRejectedBeforeForwarding(
        string method, string path)
    {
        var handler = new StaticResponseHandler(HttpStatusCode.OK);
        var proxy = CreateProxy(HttpStatusCode.OK, handler);
        var result = await proxy.ForwardJsonAsync(
            CreateHttpContext(new RecordingAuthenticationService()), new HttpMethod(method), path, new { });
        Assert.Equal(StatusCodes.Status400BadRequest,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(0, handler.RequestCount);
    }

    private static DefaultHttpContext
        CreateHttpContext(
            RecordingAuthenticationService
                authentication)
    {
        var services =
            new ServiceCollection()
                .AddSingleton<
                    IAuthenticationService>(
                    authentication)
                .BuildServiceProvider();

        return new DefaultHttpContext
        {
            RequestServices =
                services
        };
    }

    private static AdminBffWriteProxyService
        CreateProxy(
            HttpStatusCode responseStatusCode,
            StaticResponseHandler? handler = null)
    {
        var client =
            new HttpClient(
                handler ?? new StaticResponseHandler(
                    responseStatusCode))
            {
                BaseAddress =
                    new Uri(
                        "https://api.fullworth.test")
            };

        return new AdminBffWriteProxyService(
            new SingleHttpClientFactory(
                client));
    }

    private sealed class RecordingAuthenticationService
        : IAuthenticationService
    {
        public int SignOutCount { get; private set; }

        public string? LastSignOutScheme { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(
            HttpContext context,
            string? scheme)
        {
            var identity =
                new ClaimsIdentity(
                    [
                        new Claim(
                            ClaimTypes.NameIdentifier,
                            Guid.NewGuid()
                                .ToString("D"))
                    ],
                    CookieAuthenticationDefaults
                        .AuthenticationScheme);

            var properties =
                new AuthenticationProperties
                {
                    ExpiresUtc =
                        DateTimeOffset.UtcNow
                            .AddMinutes(10)
                };

            properties.StoreTokens(
                [
                    new AuthenticationToken
                    {
                        Name =
                            "access_token",

                        Value =
                            "test-access-token"
                    },

                    new AuthenticationToken
                    {
                        Name =
                            "refresh_token",

                        Value =
                            "test-refresh-token"
                    },

                    new AuthenticationToken
                    {
                        Name =
                            "expires_at",

                        Value =
                            DateTimeOffset.UtcNow
                                .AddMinutes(10)
                                .ToString("O")
                    },

                    new AuthenticationToken
                    {
                        Name =
                            "token_type",

                        Value =
                            "Bearer"
                    }
                ]);

            return Task.FromResult(
                AuthenticateResult.Success(
                    new AuthenticationTicket(
                        new ClaimsPrincipal(
                            identity),
                        properties,
                        CookieAuthenticationDefaults
                            .AuthenticationScheme)));
        }

        public Task ChallengeAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task ForbidAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task SignOutAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            SignOutCount++;
            LastSignOutScheme =
                scheme;

            return Task.CompletedTask;
        }
    }

    private sealed class SingleHttpClientFactory(
        HttpClient client)
        : IHttpClientFactory
    {
        public HttpClient CreateClient(
            string name)
        {
            return client;
        }
    }

    private sealed class StaticResponseHandler(
        HttpStatusCode responseStatusCode)
        : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public string? LastPath { get; private set; }
        public string? LastAuthorizationScheme { get; private set; }

        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            RequestCount++;
            LastMethod = request.Method;
            LastPath = request.RequestUri?.AbsolutePath;
            LastAuthorizationScheme = request.Headers.Authorization?.Scheme;
            return Task.FromResult(
                new HttpResponseMessage(
                    responseStatusCode));
        }
    }
}
