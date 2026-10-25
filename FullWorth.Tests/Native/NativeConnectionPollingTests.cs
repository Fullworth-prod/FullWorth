using System.Net;
using System.Net.Http.Json;
using FullWorth.Services;
using FullWorth.ViewModels;

namespace FullWorth.Tests.Native;

public sealed class NativeConnectionPollingTests
{
    [Fact]
    public async Task OrdinaryMultiMinuteAuthorization_CompletesInsideTheProviderBudget()
    {
        var clock = new NativeStepTimeProvider();
        using var handler = new ProviderFixture(clock, TimeSpan.FromMinutes(3), "Active");
        using var http = Client(handler);
        var vm = await ViewModel(http, clock);
        var operation = vm.ConnectBankAsync();
        await Drive(clock, operation);
        Assert.True(vm.HasConnectedInstitution);
        Assert.Contains("securely connected", vm.StatusMessage);
        Assert.Single(vm.Connections);
        Assert.InRange(handler.Polls.Count, 2, 18);
        Assert.True(handler.Polls.Zip(handler.Polls.Skip(1)).All(pair =>
            pair.Second - pair.First >= TimeSpan.FromSeconds(35)));
        Assert.Equal(1, handler.Created);
        Assert.Equal(1, handler.ReadBanks);
        Assert.True(handler.Polls.Count + handler.Created <= 20);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task TenMinutePendingFlow_IsBoundedWithoutConsumingTheWholeRateBudget()
    {
        var clock = new NativeStepTimeProvider();
        using var handler = new ProviderFixture(clock, null, "Active");
        using var http = Client(handler);
        var vm = await ViewModel(http, clock);
        var operation = vm.ConnectBankAsync();
        await Drive(clock, operation);
        Assert.Contains("stopped waiting", vm.StatusMessage);
        Assert.False(vm.HasConnectedInstitution);
        Assert.Equal(18, handler.Polls.Count);
        Assert.Equal(1, handler.Created);
        Assert.Equal(0, handler.ReadBanks);
        Assert.False(vm.IsBusy);
    }

    [Theory]
    [InlineData("RequiresAttention")]
    [InlineData("Disconnected")]
    public async Task CompletedButInactiveBank_ShowsRecoveryInsteadOfConnected(string bankStatus)
    {
        var clock = new NativeStepTimeProvider();
        using var handler = new ProviderFixture(clock, TimeSpan.Zero, bankStatus);
        using var http = Client(handler);
        var vm = await ViewModel(http, clock);
        var operation = vm.ConnectBankAsync();
        await Drive(clock, operation);
        Assert.False(vm.HasConnectedInstitution);
        Assert.Contains("Refresh connected banks", vm.StatusMessage);
        Assert.Single(handler.Polls);
        Assert.Equal(1, handler.Created);
        Assert.Equal(0, handler.ReadBanks);
    }

    [Fact]
    public async Task CancellingWhileWaiting_DoesNotPollOrCreateAnotherSession()
    {
        var clock = new NativeStepTimeProvider();
        using var handler = new ProviderFixture(clock, null, "Active");
        using var http = Client(handler);
        var vm = await ViewModel(http, clock);
        var operation = vm.ConnectBankAsync();
        await clock.WaitForScheduledAsync();
        vm.CancelPendingConnection();
        await operation.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("canceled", vm.StatusMessage);
        Assert.Empty(handler.Polls);
        Assert.Equal(1, handler.Created);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task ExplicitConnectAfterRecovery_CreatesAFreshSessionAndCanSucceed()
    {
        var clock = new NativeStepTimeProvider();
        using var handler = new ProviderFixture(clock, TimeSpan.Zero, "RequiresAttention");
        using var http = Client(handler);
        var vm = await ViewModel(http, clock);
        var first = vm.ConnectBankAsync();
        await Drive(clock, first);
        Assert.False(vm.HasConnectedInstitution);
        Assert.Equal(1, handler.Created);
        handler.BankStatus = "Active";
        var fresh = vm.ConnectBankAsync();
        await Drive(clock, fresh);
        Assert.True(vm.HasConnectedInstitution);
        Assert.Equal(2, handler.Created);
        Assert.Equal(2, handler.Polls.Count);
        Assert.Equal(2, handler.SessionIds.Distinct().Count());
        Assert.Equal(1, handler.ReadBanks);
    }

    private static async Task Drive(NativeStepTimeProvider clock, Task operation)
    {
        for (var index = 0; index < 25 && !operation.IsCompleted; index++)
            await clock.StepAsync(operation);
        await operation.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static HttpClient Client(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://api.example.test") };

    private static async Task<ConnectBankPageViewModel> ViewModel(HttpClient http, TimeProvider clock)
    {
        var session = new AuthSession();
        await session.SaveAsync("synthetic-owner-access", "synthetic-refresh", 900);
        var api = new FullWorthApiClient(http);
        var service = new PlaidConnectionService(api, new AuthenticationService(api, session, http));
        return new(service, clock);
    }

    private sealed class ProviderFixture(NativeStepTimeProvider clock, TimeSpan? completesAt, string bankStatus)
        : HttpMessageHandler
    {
        private readonly DateTimeOffset _start = clock.GetUtcNow();
        private Guid _session;
        private readonly Guid _bank = Guid.NewGuid();
        public string BankStatus { get; set; } = bankStatus;
        public List<Guid> SessionIds { get; } = [];
        public int Created, ReadBanks;
        public List<TimeSpan> Polls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = request.RequestUri!.AbsolutePath;
            object payload;
            if (path == "/api/plaid/link-token")
            {
                Created++;
                _session = Guid.NewGuid();
                SessionIds.Add(_session);
                payload = new PlaidHostedLinkResult(_session, "https://cdn.plaid.com/synthetic-test-only");
            }
            else if (path == $"/api/plaid/link-session/{_session}/complete")
            {
                var elapsed = clock.GetUtcNow() - _start;
                Polls.Add(elapsed);
                if (Created + Polls.Count > 20)
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
                var complete = completesAt is not null && elapsed >= completesAt.Value;
                payload = new PlaidHostedLinkCompletionResult(_session, complete ? "Completed" : "Pending",
                    complete ? new(_bank, "Synthetic bank", BankStatus) : null);
            }
            else if (path == "/api/bank-connections")
            {
                ReadBanks++;
                payload = new[] { new BankConnectionResult(_bank, "Synthetic bank", BankConnectionStatus.Active, null, _start) };
            }
            else throw new InvalidOperationException("Unexpected synthetic native request.");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(payload) });
        }
    }
}
