using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FullWorth.Services;
using FullWorth.ViewModels;

namespace FullWorth.Tests.Native;

public sealed class NativeLoginTests
{
    private static HttpResponseMessage Challenge() => Json(HttpStatusCode.Unauthorized, """{"detail":"RequiresTwoFactor"}""");
    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/problem+json") };
    private static HttpResponseMessage Tokens(string prefix) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(new LoginResult("Bearer", prefix + "-access", 900, prefix + "-refresh")) };

    [Fact]
    public async Task ChallengeRequiresCodeWithoutSavingOrRefreshing()
    {
        using var handler = new Handler(Challenge());
        using var http = Client(handler);
        var session = new AuthSession();
        var vm = ViewModel(http, session);
        Assert.Equal(LoginPageDestination.None, await vm.AuthenticateAsync());
        Assert.True(vm.RequiresTwoFactor);
        Assert.Contains("two-step verification", vm.ErrorMessage);
        Assert.Null(session.AccessToken);
        Assert.Single(handler.Requests);
        Assert.Equal(LoginPageDestination.None, await vm.AuthenticateAsync());
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SecondFactorUsesCorrectFieldThenEnrolledTokensOnly(bool recovery)
    {
        using var handler = new Handler(Challenge(), Tokens("initial"), Tokens("enrolled"));
        using var http = Client(handler);
        var session = new AuthSession();
        var vm = ViewModel(http, session);
        await vm.AuthenticateAsync();
        vm.UseRecoveryCode = recovery;
        vm.VerificationCode = recovery ? "  recovery-code  " : "  123456  ";
        Assert.Equal(LoginPageDestination.Home, await vm.AuthenticateAsync());
        using var submitted = JsonDocument.Parse(handler.Requests[1].Body);
        Assert.Equal(recovery ? "recovery-code" : "123456",
            submitted.RootElement.GetProperty(recovery ? "twoFactorRecoveryCode" : "twoFactorCode").GetString());
        Assert.Equal(JsonValueKind.Null,
            submitted.RootElement.GetProperty(recovery ? "twoFactorCode" : "twoFactorRecoveryCode").ValueKind);
        Assert.Equal("/api/auth/refresh", handler.Requests[2].Path);
        Assert.Equal("enrolled-access", session.AccessToken);
        Assert.Equal("enrolled-refresh", session.RefreshToken);
        Assert.Empty(vm.Password);
        Assert.Empty(vm.VerificationCode);
        Assert.False(vm.RequiresTwoFactor);
    }

    [Theory]
    [InlineData("""{"detail":"Failed"}""")]
    [InlineData("""{"detail":"NotAllowed"}""")]
    [InlineData("""{"detail":"LockedOut"}""")]
    [InlineData("""{"detail":123}""")]
    [InlineData("""["RequiresTwoFactor"]""")]
    [InlineData("not json RequiresTwoFactor")]
    public async Task OtherFailuresNeverBecomeChallenges(string body)
    {
        using var handler = new Handler(Json(HttpStatusCode.Unauthorized, body));
        using var http = Client(handler);
        var vm = ViewModel(http, new AuthSession());
        Assert.Equal(LoginPageDestination.None, await vm.AuthenticateAsync());
        Assert.False(vm.RequiresTwoFactor);
        Assert.Equal("The email address or password is incorrect.", vm.ErrorMessage);
    }

    [Fact]
    public async Task OversizedBodyDoesNotBecomeChallenge()
    {
        var body = JsonSerializer.Serialize(new { detail = "RequiresTwoFactor", padding = new string('x', 5000) });
        using var handler = new Handler(Json(HttpStatusCode.Unauthorized, body));
        using var http = Client(handler);
        var vm = ViewModel(http, new AuthSession());
        await vm.AuthenticateAsync();
        Assert.False(vm.RequiresTwoFactor);
    }

    [Fact]
    public async Task InvalidCodeCanBeRetriedAndIsCleared()
    {
        using var handler = new Handler(Challenge(), Json(HttpStatusCode.Unauthorized, """{"detail":"Failed"}"""),
            Tokens("initial"), Tokens("enrolled"));
        using var http = Client(handler);
        var vm = ViewModel(http, new AuthSession());
        await vm.AuthenticateAsync();
        vm.VerificationCode = "000000";
        Assert.Equal(LoginPageDestination.None, await vm.AuthenticateAsync());
        Assert.True(vm.RequiresTwoFactor);
        Assert.Empty(vm.VerificationCode);
        Assert.Contains("authenticator or recovery code", vm.ErrorMessage);
        vm.VerificationCode = "123456";
        Assert.Equal(LoginPageDestination.Home, await vm.AuthenticateAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CredentialChangesDiscardChallenge(bool email)
    {
        using var handler = new Handler(Challenge());
        using var http = Client(handler);
        var vm = ViewModel(http, new AuthSession());
        await vm.AuthenticateAsync();
        vm.VerificationCode = "123456";
        if (email) vm.Email = "another@example.com"; else vm.Password = "Another-password1!";
        Assert.False(vm.RequiresTwoFactor);
        Assert.Empty(vm.VerificationCode);
    }

    [Fact]
    public async Task LeavingOrChangingModeClearsSensitiveInputs()
    {
        using var handler = new Handler(Challenge(), Challenge());
        using var http = Client(handler);
        var vm = ViewModel(http, new AuthSession());
        await vm.AuthenticateAsync();
        vm.VerificationCode = "123456";
        vm.ToggleMode();
        Assert.False(vm.RequiresTwoFactor);
        Assert.Empty(vm.Password);
        Assert.Empty(vm.VerificationCode);
        vm.ToggleMode();
        vm.Password = "Example-password1!";
        await vm.AuthenticateAsync();
        vm.VerificationCode = "123456";
        vm.ClearSensitiveInputs();
        Assert.False(vm.RequiresTwoFactor);
        Assert.Empty(vm.Password);
        Assert.Empty(vm.VerificationCode);
    }

    [Fact]
    public async Task RefreshFailureIsNotWrongPasswordAndStoresNothing()
    {
        using var handler = new Handler(Tokens("initial"), new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var http = Client(handler);
        var session = new AuthSession();
        var vm = ViewModel(http, session);
        Assert.Equal(LoginPageDestination.None, await vm.AuthenticateAsync());
        Assert.Contains("secure session", vm.ErrorMessage);
        Assert.Null(session.AccessToken);
        Assert.Null(session.RefreshToken);
    }

    [Fact]
    public async Task NonMfaLoginAndRateLimitingStillWork()
    {
        using var handler = new Handler(new HttpResponseMessage(HttpStatusCode.TooManyRequests),
            Tokens("initial"), Tokens("enrolled"));
        using var http = Client(handler);
        var vm = ViewModel(http, new AuthSession());
        await vm.AuthenticateAsync();
        Assert.Contains("Too many attempts", vm.ErrorMessage);
        Assert.False(vm.RequiresTwoFactor);
        Assert.Equal(LoginPageDestination.Home, await vm.AuthenticateAsync());
    }

    private static HttpClient Client(Handler handler) => new(handler) { BaseAddress = new Uri("https://api.example.test") };
    private static LoginPageViewModel ViewModel(HttpClient http, AuthSession session) =>
        new(new AuthenticationService(new FullWorthApiClient(http), session, http))
        { Email = "owner@example.com", Password = "Example-password1!" };

    private sealed class Handler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public List<(string Path, string Body)> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!.AbsolutePath, await request.Content!.ReadAsStringAsync(cancellationToken)));
            return _responses.Dequeue();
        }
    }
}
