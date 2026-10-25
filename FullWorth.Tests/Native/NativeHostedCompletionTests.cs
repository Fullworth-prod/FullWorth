using System.Net;
using System.Net.Http.Json;
using FullWorth.Services;

namespace FullWorth.Tests.Native;

// Actual shipped API/auth/Plaid service sources, with device storage replaced
// by the existing test double. This is not provider or physical-device proof.
public sealed class NativeHostedCompletionTests
{
    [Theory]
    [InlineData("missing-bank")]
    [InlineData("empty-bank-id")]
    [InlineData("attention")]
    [InlineData("disconnected")]
    [InlineData("unknown-bank-state")]
    [InlineData("empty-bank-state")]
    [InlineData("wrong-session")]
    [InlineData("empty-session")]
    public async Task UnverifiedCompletion_ReturnsRecoveryWithoutReplayingTheWrite(string scenario)
    {
        var requested = Guid.NewGuid();
        var session = scenario == "wrong-session" ? Guid.NewGuid() :
            scenario == "empty-session" ? Guid.Empty : requested;
        var state = scenario switch
        {
            "attention" => "RequiresAttention",
            "disconnected" => "Disconnected",
            "unknown-bank-state" => "Unknown",
            "empty-bank-state" => "",
            _ => "Active"
        };
        PlaidConnectionResult? bank = scenario == "missing-bank" ? null :
            new(scenario == "empty-bank-id" ? Guid.Empty : Guid.NewGuid(), "Synthetic bank", state);
        using var handler = new Handler(new(session, "Completed", bank));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") };
        var service = await Service(http);
        var result = await service.CompleteLinkSessionAsync(requested);
        Assert.Equal(requested, result.SessionId);
        Assert.Equal("RecoveryRequired", result.Status);
        Assert.Null(result.Connection);
        Assert.Single(handler.Requests);
        Assert.Equal(("POST", $"/api/plaid/link-session/{requested}/complete"), handler.Requests[0]);
    }

    [Fact]
    public async Task ActiveMatchingCompletion_PreservesDurableBankEvidence()
    {
        var requested = Guid.NewGuid();
        var bank = new PlaidConnectionResult(Guid.NewGuid(), "Synthetic bank", "Active");
        var payload = new PlaidHostedLinkCompletionResult(requested, "Completed", bank);
        using var handler = new Handler(payload);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") };
        var result = await (await Service(http)).CompleteLinkSessionAsync(requested);
        Assert.Equal(payload, result);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("RecoveryRequired")]
    [InlineData("Exited")]
    [InlineData("Expired")]
    [InlineData("Failed")]
    public async Task HonestNonSuccessStates_DoNotBecomeSuccessOrTriggerAnotherRequest(string status)
    {
        var requested = Guid.NewGuid();
        var payload = new PlaidHostedLinkCompletionResult(requested, status, null);
        using var handler = new Handler(payload);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") };
        var result = await (await Service(http)).CompleteLinkSessionAsync(requested);
        Assert.Equal(payload, result);
        Assert.Single(handler.Requests);
    }

    private static async Task<PlaidConnectionService> Service(HttpClient http)
    {
        var session = new AuthSession();
        await session.SaveAsync("synthetic-owner-access", "synthetic-refresh", 900);
        var api = new FullWorthApiClient(http);
        return new(api, new AuthenticationService(api, session, http));
    }

    private sealed class Handler(PlaidHostedLinkCompletionResult payload) : HttpMessageHandler
    {
        public List<(string Method, string Path)> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method.Method, request.RequestUri!.AbsolutePath));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(payload) });
        }
    }
}
