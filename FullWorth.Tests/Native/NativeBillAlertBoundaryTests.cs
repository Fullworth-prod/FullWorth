using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using FullWorth.Services;
using FullWorth.ViewModels;

namespace FullWorth.Tests.Native;

public sealed class NativeBillAlertBoundaryTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("null-row")]
    [InlineData("duplicate")]
    [InlineData("empty-id")]
    [InlineData("missing-title")]
    [InlineData("missing-message")]
    [InlineData("missing-type")]
    [InlineData("missing-severity")]
    [InlineData("missing-created")]
    [InlineData("missing-updated")]
    [InlineData("accepted")]
    [InlineData("wrong-media")]
    [InlineData("malformed")]
    public async Task UnconfirmedOrInvalidAlertReads_AreNotAnEmptyOrDoubledAccount(string mode)
    {
        using var f = await Fixture.Create();
        f.Handler.Mode = mode;
        await Assert.ThrowsAnyAsync<Exception>(() => f.Alerts.GetAlertsAsync());
        Assert.Single(f.Handler.Requests);
        Assert.Equal(HttpMethod.Get, f.Handler.Requests.Single().Method);
    }

    [Fact]
    public async Task ConfirmedEmptyRead_RemainsAValidEmptyAccount()
    {
        using var f = await Fixture.Create();
        f.Handler.Mode = "empty";
        Assert.Empty(await f.Alerts.GetAlertsAsync());
        Assert.Single(f.Handler.Requests);
    }

    [Fact]
    public async Task CompleteAlert_WithAdditiveFieldsAndNewType_RemainsReadable()
    {
        using var f = await Fixture.Create();
        f.Handler.Mode = "additive";
        var result = Assert.Single(await f.Alerts.GetAlertsAsync());
        Assert.Equal("FutureSupportedType", result.AlertType);
        Assert.Equal("Synthetic alert", result.Title);
        Assert.Equal(f.Handler.Alert.Id, result.Id);
    }

    [Theory]
    [InlineData(false, 200)]
    [InlineData(false, 202)]
    [InlineData(false, 205)]
    [InlineData(true, 200)]
    [InlineData(true, 202)]
    [InlineData(true, 205)]
    public async Task A2xxWithoutTheDeclaredReceipt_CannotConfirmAMutation(bool dismiss, int status)
    {
        using var f = await Fixture.Create();
        f.Handler.MutationStatus = (HttpStatusCode)status;
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            dismiss ? f.Alerts.DismissAsync(f.Handler.Alert.Id) : f.Alerts.MarkReadAsync(f.Handler.Alert.Id));
        Assert.Single(f.Handler.Requests);
        Assert.Equal(HttpMethod.Post, f.Handler.Requests.Single().Method);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NoContentWithAnUnexpectedBody_IsUnconfirmedWithoutReflectingTheBody(bool dismiss)
    {
        using var f = await Fixture.Create();
        f.Handler.MutationBody = "PRIVATE-TEST-CANARY";
        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            dismiss ? f.Alerts.DismissAsync(f.Handler.Alert.Id) : f.Alerts.MarkReadAsync(f.Handler.Alert.Id));
        Assert.DoesNotContain("PRIVATE", error.Message);
        Assert.Single(f.Handler.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Declared204Receipt_CompletesExactlyOneMutation(bool dismiss)
    {
        using var f = await Fixture.Create();
        if (dismiss) await f.Alerts.DismissAsync(f.Handler.Alert.Id);
        else await f.Alerts.MarkReadAsync(f.Handler.Alert.Id);
        var request = Assert.Single(f.Handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith(dismiss ? "/dismiss" : "/read", request.Path);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedMutation_IsNotReplayed(bool dismiss)
    {
        using var f = await Fixture.Create();
        f.Handler.MutationStatus = HttpStatusCode.ServiceUnavailable;
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            dismiss ? f.Alerts.DismissAsync(f.Handler.Alert.Id) : f.Alerts.MarkReadAsync(f.Handler.Alert.Id));
        Assert.Single(f.Handler.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyIdentity_FailsBeforeAnyRequest(bool dismiss)
    {
        using var f = await Fixture.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            dismiss ? f.Alerts.DismissAsync(Guid.Empty) : f.Alerts.MarkReadAsync(Guid.Empty));
        Assert.Empty(f.Handler.Requests);
    }

    [Fact]
    public async Task InvalidAlertRefresh_CannotClearPriorAttentionAsIfTheAccountWereHealthy()
    {
        using var f = await Fixture.Create();
        await f.Dashboard.LoadAsync();
        Assert.False(f.Dashboard.HasError);
        Assert.Equal(1, f.Dashboard.AttentionCount);
        Assert.Equal(1, f.Dashboard.UnreadAlerts);
        f.Handler.Mode = "null";
        await f.Dashboard.LoadAsync();
        Assert.True(f.Dashboard.HasError);
        Assert.False(f.Dashboard.HasContent);
        Assert.Equal(1, f.Dashboard.AttentionCount);
        Assert.Equal("Synthetic alert", f.Dashboard.PrimaryAlertTitle);
        Assert.Equal(4, f.Handler.Requests.Count);
        Assert.All(f.Handler.Requests, r => Assert.Equal(HttpMethod.Get, r.Method));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly HttpClient _http;
        public Handler Handler { get; }
        public BillAlertService Alerts { get; }
        public MainPageViewModel Dashboard { get; }
        private Fixture(Handler handler, HttpClient http, BillAlertService alerts, MainPageViewModel dashboard)
        { Handler = handler; _http = http; Alerts = alerts; Dashboard = dashboard; }
        public static async Task<Fixture> Create()
        {
            var handler = new Handler();
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://native-alert.test") };
            var session = new AuthSession();
            await session.SaveAsync("synthetic-native-access", "synthetic-native-refresh", 3600);
            var api = new FullWorthApiClient(http);
            var auth = new AuthenticationService(api, session, http);
            var alerts = new BillAlertService(http, auth);
            return new(handler, http, alerts, new MainPageViewModel(new BillStreamService(api, auth, http), alerts));
        }
        public void Dispose() => _http.Dispose();
    }

    private sealed class Handler : HttpMessageHandler
    {
        public string Mode { get; set; } = "valid";
        public HttpStatusCode MutationStatus { get; set; } = HttpStatusCode.NoContent;
        public string? MutationBody { get; set; }
        public ConcurrentQueue<(HttpMethod Method, string Path)> Requests { get; } = new();
        public BillAlertResult Alert { get; } = new(Guid.NewGuid(), null, null, "ConnectionIssue", "Warning",
            "Synthetic alert", "Synthetic monitoring issue", false, false,
            DateTimeOffset.Parse("2026-10-29T00:00:00Z"), DateTimeOffset.Parse("2026-10-29T00:00:00Z"));
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Enqueue((request.Method, path));
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("synthetic-native-access", request.Headers.Authorization?.Parameter);
            if (path == "/api/bill-streams")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = JsonContent.Create(Array.Empty<BillStreamResult>()) });
            if (request.Method == HttpMethod.Post)
            {
                Assert.True(path.EndsWith("/read", StringComparison.Ordinal) ||
                    path.EndsWith("/dismiss", StringComparison.Ordinal));
                return Task.FromResult(new HttpResponseMessage(MutationStatus)
                    { Content = MutationBody is null ? new ByteArrayContent([]) : new StringContent(MutationBody) });
            }
            Assert.Equal("/api/alerts", path);
            var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
            var json = Mode switch
            {
                "null" => "null",
                "null-row" => "[null]",
                "duplicate" => System.Text.Json.JsonSerializer.Serialize(new[] { Alert, Alert }, options),
                "empty-id" => System.Text.Json.JsonSerializer.Serialize(new[] { Alert with { Id = Guid.Empty } }, options),
                "missing-title" => System.Text.Json.JsonSerializer.Serialize(new[] { Alert with { Title = " " } }, options),
                "missing-message" => System.Text.Json.JsonSerializer.Serialize(new[] { Alert with { Message = "" } }, options),
                "missing-type" => System.Text.Json.JsonSerializer.Serialize(new[] { Alert with { AlertType = " " } }, options),
                "missing-severity" => System.Text.Json.JsonSerializer.Serialize(new[] { Alert with { Severity = "" } }, options),
                "missing-created" => System.Text.Json.JsonSerializer.Serialize(new[] { Alert with { CreatedAtUtc = default } }, options),
                "missing-updated" => System.Text.Json.JsonSerializer.Serialize(new[] { Alert with { UpdatedAtUtc = default } }, options),
                "empty" => "[]",
                "malformed" => "{PRIVATE-TEST-CANARY",
                "additive" => System.Text.Json.JsonSerializer.Serialize(new[] { Alert with { AlertType = "FutureSupportedType" } }, options)
                    .Replace("\"title\":", "\"futureMetadata\":true,\"title\":", StringComparison.Ordinal),
                _ => System.Text.Json.JsonSerializer.Serialize(new[] { Alert }, options)
            };
            return Task.FromResult(new HttpResponseMessage(Mode == "accepted" ? HttpStatusCode.Accepted : HttpStatusCode.OK)
                { Content = new StringContent(json, System.Text.Encoding.UTF8,
                    Mode == "wrong-media" ? "text/plain" : "application/json") });
        }
    }
}
