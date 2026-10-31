using System.Net;
using System.Net.Http.Json;
using FullWorth.Services;
using FullWorth.ViewModels;

namespace FullWorth.Tests.Native;

public sealed class NativeBillListFinancialTruthTests
{
    [Theory]
    [InlineData("missing-evidence")]
    [InlineData("zero")]
    [InlineData("negative")]
    [InlineData("unknown-currency")]
    [InlineData("alias-currency")]
    public async Task UnsupportedCurrentAmount_CannotBecomeAChangedBillOrAnnualReduction(string mode)
    {
        var bill = Bill(0, 100);
        bill = mode switch
        {
            "missing-evidence" => bill with { HasTransactionEvidence = false },
            "zero" => bill,
            "negative" => bill with { CurrentAmount = -10m },
            "unknown-currency" => bill with { CurrentAmount = 110m, CurrencyCode = null },
            _ => bill with { CurrentAmount = 110m, CurrencyCode = "uſd" }
        };
        using var f = await Fixture.Create([bill]);
        await f.List.LoadAsync();
        await f.Dashboard.LoadAsync();
        Assert.False(f.List.HasError);
        Assert.False(f.Dashboard.HasError);
        var row = Assert.Single(f.List.Bills);
        Assert.False(row.HasMeaningfulChange);
        Assert.Equal("Watching", row.Status);
        Assert.Equal("Amount unavailable", row.CurrentAmountText);
        Assert.Equal("Amount unavailable", f.List.MonthlyTotal);
        Assert.Equal(f.Dashboard.MonthlyBillsText, f.List.MonthlyTotal);
        Assert.Equal("Change unavailable", row.ChangeText);
        Assert.Equal("Yearly estimate unavailable", row.AnnualImpactText);
        Assert.Equal(0, f.Dashboard.ChangesDetected);
        Assert.Equal(3, f.Handler.Reads);
    }

    [Fact]
    public async Task KnownMonthlyChange_PreservesAmountDirectionAndExactMath()
    {
        using var f = await Fixture.Create([Bill(80, 100)]);
        await f.List.LoadAsync();
        var row = Assert.Single(f.List.Bills);
        Assert.True(row.HasMeaningfulChange);
        Assert.Equal("Changed", row.Status);
        Assert.Equal("$80.00", row.CurrentAmountText);
        Assert.Equal("$-20.00 per cycle", row.ChangeText);
        Assert.Equal("$-240.00/year", row.AnnualImpactText);
        Assert.Equal("$80.00", f.List.MonthlyTotal);
    }

    [Fact]
    public async Task KnownAmountWithUnknownCadence_RetainsChangeWithoutAnAnnualRate()
    {
        using var f = await Fixture.Create([Bill(110, 100) with { HasMonthlyCadenceEvidence = false }]);
        await f.List.LoadAsync();
        var row = Assert.Single(f.List.Bills);
        Assert.True(row.HasMeaningfulChange);
        Assert.Equal("$10.00 per cycle", row.ChangeText);
        Assert.Equal("Yearly estimate unavailable", row.AnnualImpactText);
        Assert.Equal("$110.00", row.CurrentAmountText);
    }

    [Theory]
    [InlineData(101, 100, false)]
    [InlineData(109, 100, false)]
    [InlineData(110, 100, true)]
    [InlineData(45, 50, true)]
    [InlineData(100, 0, false)]
    public async Task ExistingMaterialityAndUnknownBaselineRemain(int current, int previous, bool changed)
    {
        using var f = await Fixture.Create([Bill(current, previous)]);
        await f.List.LoadAsync();
        Assert.Equal(changed, Assert.Single(f.List.Bills).HasMeaningfulChange);
    }

    [Fact]
    public async Task MixedCurrencyTotals_DoNotConvertOrIncludeUnsupportedAmounts()
    {
        using var f = await Fixture.Create([Bill(100, 100), Bill(200, 200) with { CurrencyCode = "CAD" },
            Bill(0, 100), Bill(300, 200) with { CurrencyCode = null }]);
        await f.List.LoadAsync();
        Assert.Equal("CAD 200.00 · $100.00", f.List.MonthlyTotal);
        Assert.Equal(4, f.List.Bills.Count);
    }

    [Fact]
    public async Task InvalidRefresh_PreservesThePriorListAndSignalsFailure()
    {
        using var f = await Fixture.Create([Bill(100, 100)]);
        await f.List.LoadAsync();
        var prior = Assert.Single(f.List.Bills);
        f.Handler.Invalid = true;
        await f.List.LoadAsync();
        Assert.True(f.List.HasError);
        Assert.Same(prior, Assert.Single(f.List.Bills));
        Assert.Equal("$100.00", f.List.MonthlyTotal);
        Assert.Equal(2, f.Handler.Reads);
    }

    private static BillStreamResult Bill(decimal current, decimal previous) =>
        new(Guid.NewGuid(), "Synthetic provider", "Other", true, current, previous)
        { CurrencyCode = "USD", HasTransactionEvidence = true, HasMonthlyCadenceEvidence = true };

    private sealed class Fixture : IDisposable
    {
        private readonly HttpClient _http;
        public Handler Handler { get; }
        public BillsPageViewModel List { get; }
        public MainPageViewModel Dashboard { get; }
        private Fixture(HttpClient http, Handler handler, BillStreamService bills, BillAlertService alerts)
        { _http = http; Handler = handler; List = new(bills); Dashboard = new(bills, alerts); }
        public static async Task<Fixture> Create(BillStreamResult[] bills)
        {
            var handler = new Handler(bills);
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://native-bills.test") };
            var session = new AuthSession();
            await session.SaveAsync("synthetic-native-access", "synthetic-native-refresh", 3600);
            var api = new FullWorthApiClient(http);
            var auth = new AuthenticationService(api, session, http);
            return new(http, handler, new BillStreamService(api, auth, http), new BillAlertService(http, auth));
        }
        public void Dispose() => _http.Dispose();
    }

    private sealed class Handler(BillStreamResult[] bills) : HttpMessageHandler
    {
        public int Reads;
        public bool Invalid { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Reads);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("synthetic-native-access", request.Headers.Authorization?.Parameter);
            return Task.FromResult(request.RequestUri!.AbsolutePath switch
            {
                "/api/bill-streams" => new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = Invalid ? new StringContent("null", System.Text.Encoding.UTF8, "application/json")
                        : JsonContent.Create(bills) },
                "/api/alerts" => new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = JsonContent.Create(Array.Empty<BillAlertResult>()) },
                _ => throw new InvalidOperationException("Unexpected native bill-list request.")
            });
        }
    }
}
