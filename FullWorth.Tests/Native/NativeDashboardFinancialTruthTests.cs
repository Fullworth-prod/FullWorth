using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using FullWorth.Services;
using FullWorth.ViewModels;

namespace FullWorth.Tests.Native;

public sealed class NativeDashboardFinancialTruthTests
{
    [Fact]
    public async Task UnknownCadence_KeepsKnownAmountWithoutMonthlyOrAnnualClaim()
    {
        var vm = await Load(Bill(100, 100, monthly: false));
        Assert.Equal("$100.00", vm.MonthlyBillsText);
        Assert.Equal("Yearly estimate unavailable", vm.AnnualBillsText);
        Assert.Equal("Yearly estimate unavailable", vm.AnnualImpactHeadline);
        Assert.Equal(0, vm.ChangesDetected);
    }

    [Fact]
    public async Task SupportedMonthlyEvidence_PreservesExactAnnualMath()
    {
        var vm = await Load(Bill(100, 90));
        Assert.Equal("$100.00", vm.MonthlyBillsText);
        Assert.StartsWith("$1200.00", vm.AnnualBillsText);
        Assert.Equal("$120.00/year", vm.AnnualImpactHeadline);
        Assert.DoesNotContain("unavailable", vm.AnnualImpactCaption);
        Assert.Equal(1, vm.ChangesDetected);
    }

    [Fact]
    public async Task MixedCadenceChanges_CannotProduceAPartialAnnualHeadline()
    {
        var vm = await Load(Bill(100, 90), Bill(100, 80, monthly: false));
        Assert.Equal("$200.00", vm.MonthlyBillsText);
        Assert.Equal(2, vm.ChangesDetected);
        Assert.Equal("Yearly estimate unavailable", vm.AnnualImpactHeadline);
        Assert.Equal("Change detected; yearly estimate unavailable", vm.AnnualImpactCaption);
        Assert.Equal("Yearly estimate unavailable", vm.AddedAnnualCost);
        Assert.Equal("Yearly estimate unavailable", vm.AnnualBillsText);
    }

    [Fact]
    public async Task UnsupportedIncrease_CannotTurnTheHeadlineIntoAPartialReduction()
    {
        var vm = await Load(Bill(100, 120), Bill(100, 80, monthly: false));
        Assert.Equal(2, vm.ChangesDetected);
        Assert.Equal("Yearly estimate unavailable", vm.AnnualImpactHeadline);
        Assert.Equal("Yearly estimate unavailable", vm.ReducedAnnualCost);
    }

    [Fact]
    public async Task SupportedMonthlyReduction_RetainsItsAmountAndEstimateLabel()
    {
        var vm = await Load(Bill(80, 100));
        Assert.Equal("$240.00/year less", vm.AnnualImpactHeadline);
        Assert.DoesNotContain("unavailable", vm.AnnualImpactCaption);
        Assert.StartsWith("$960.00", vm.AnnualBillsText);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("uſd")]
    public async Task UnknownCurrency_DoesNotCreateChangeOrAnUnavailablePerYearClaim(string? currency)
    {
        var vm = await Load(Bill(110, 100, currency: currency));
        Assert.Equal("Amount unavailable", vm.MonthlyBillsText);
        Assert.Equal(0, vm.ChangesDetected);
        Assert.Equal("Yearly estimate unavailable", vm.AnnualImpactHeadline);
        Assert.Equal("Yearly estimate unavailable", vm.AnnualBillsText);
        Assert.DoesNotContain("/year", vm.AnnualImpactHeadline);
    }

    [Fact]
    public async Task MissingCurrentEvidence_IsNotAZeroBalanceOrADecrease()
    {
        var vm = await Load(Bill(0, 100, known: false));
        Assert.Equal("Amount unavailable", vm.MonthlyBillsText);
        Assert.Equal(0, vm.ChangesDetected);
        Assert.Equal("Yearly estimate unavailable", vm.AnnualBillsText);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task UnsupportedNonDebitAmount_DoesNotCreateAnObligation(int amount)
    {
        var vm = await Load(Bill(amount, 100));
        Assert.Equal("Amount unavailable", vm.MonthlyBillsText);
        Assert.Equal(0, vm.ChangesDetected);
        Assert.Equal("Yearly estimate unavailable", vm.AnnualBillsText);
    }

    [Fact]
    public async Task MissingDenomination_BlocksTheWholeAnnualBillProjection()
    {
        var vm = await Load(Bill(100, 100), Bill(200, 200, currency: null));
        Assert.Equal("$100.00", vm.MonthlyBillsText);
        Assert.Equal("Yearly estimate unavailable", vm.AnnualBillsText);
        Assert.Equal(2, vm.BillsMonitored);
    }

    [Fact]
    public async Task DifferentCurrencies_StaySeparateWithoutConversion()
    {
        var vm = await Load(Bill(100, 100), Bill(200, 200, currency: "CAD"));
        Assert.Equal("CAD 200.00 · $100.00", vm.MonthlyBillsText);
        Assert.StartsWith("CAD 2400.00 · $1200.00", vm.AnnualBillsText);
    }

    [Fact]
    public async Task InactiveInputs_DoNotBecomeMonitoredObligations()
    {
        // The API normally excludes inactive rows. Keep the read projection
        // defensive without claiming this fixture is a real provider incident.
        var vm = await Load(Bill(100, 100), Bill(1000, 900, active: false));
        Assert.Equal(1, vm.BillsMonitored);
        Assert.Equal("$100.00", vm.MonthlyBillsText);
        Assert.Equal("$1200.00 per year at current monthly amounts", vm.AnnualBillsText);
        Assert.Equal(0, vm.ChangesDetected);
    }

    [Theory]
    [InlineData(101, 100, 0)]
    [InlineData(109, 100, 0)]
    [InlineData(110, 100, 1)]
    [InlineData(45, 50, 1)]
    [InlineData(100, 0, 0)]
    public async Task ExistingMaterialityAndUnknownBaselineRulesRemain(
        int current, int previous, int expected)
    {
        var vm = await Load(Bill(current, previous));
        Assert.Equal(expected, vm.ChangesDetected);
    }

    [Fact]
    public async Task ANewUnsupportedSnapshot_ClearsPriorAnnualConfidence()
    {
        using var fixture = await Fixture.Create([Bill(100, 90)]);
        await fixture.ViewModel.LoadAsync();
        Assert.False(fixture.ViewModel.HasError);
        Assert.Equal("$120.00/year", fixture.ViewModel.AnnualImpactHeadline);
        fixture.Handler.Bills = [Bill(130, 100, monthly: false)];
        await fixture.ViewModel.LoadAsync();
        Assert.False(fixture.ViewModel.HasError);
        Assert.Equal("$130.00", fixture.ViewModel.MonthlyBillsText);
        Assert.Equal("Yearly estimate unavailable", fixture.ViewModel.AnnualImpactHeadline);
        Assert.Equal("Yearly estimate unavailable", fixture.ViewModel.AnnualBillsText);
        Assert.Equal("Change detected; yearly estimate unavailable", fixture.ViewModel.AnnualImpactCaption);
        Assert.Equal(4, fixture.Handler.Requests.Count);
        Assert.All(fixture.Handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
    }

    [Fact]
    public async Task EmptyConfirmedSnapshot_IsNotAnInventedZeroAnnualRate()
    {
        var vm = await Load();
        Assert.True(vm.HasNoBills);
        Assert.Equal(0, vm.BillsMonitored);
        Assert.Equal("Amount unavailable", vm.MonthlyBillsText);
        Assert.Equal("Yearly estimate unavailable", vm.AnnualBillsText);
        Assert.Equal("Yearly estimate unavailable", vm.AnnualImpactHeadline);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("duplicate")]
    [InlineData("missing-id")]
    [InlineData("missing-provider")]
    [InlineData("accepted-status")]
    [InlineData("wrong-media")]
    [InlineData("malformed-json")]
    public async Task InvalidRead_CannotReplaceVerifiedBillsWithEmptyOrDuplicateTotals(string mode)
    {
        var bill = Bill(100, 100);
        using var fixture = await Fixture.Create([bill]);
        await fixture.ViewModel.LoadAsync();
        Assert.False(fixture.ViewModel.HasError);
        fixture.Handler.BillsJsonOverride = mode switch
        {
            "null" => "null",
            "duplicate" => System.Text.Json.JsonSerializer.Serialize(new[] { bill, bill },
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
            "missing-id" => System.Text.Json.JsonSerializer.Serialize(new[] { bill with { Id = Guid.Empty } },
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
            "missing-provider" => System.Text.Json.JsonSerializer.Serialize(new[] { bill with { ProviderName = " " } },
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
            "malformed-json" => "{PRIVATE-TEST-CANARY",
            _ => null
        };
        if (mode == "accepted-status") fixture.Handler.BillsStatus = HttpStatusCode.Accepted;
        if (mode == "wrong-media") fixture.Handler.BillsMediaType = "text/plain";
        await fixture.ViewModel.LoadAsync();
        Assert.True(fixture.ViewModel.HasError);
        Assert.False(fixture.ViewModel.HasContent);
        Assert.Equal(1, fixture.ViewModel.BillsMonitored);
        Assert.Equal("$100.00", fixture.ViewModel.MonthlyBillsText);
        Assert.DoesNotContain("PRIVATE", fixture.ViewModel.ErrorMessage);
        Assert.Equal(4, fixture.Handler.Requests.Count);
        Assert.All(fixture.Handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
    }

    [Fact]
    public void ShippedDashboardLabels_DoNotCallUnknownCadenceAmountsMonthly()
    {
        var xaml = File.ReadAllText(FindRepositoryFile("MainPage.xaml"));
        Assert.Contains("Text=\"KNOWN AMOUNTS\"", xaml);
        Assert.Contains("Text=\"Known bill amounts\"", xaml);
        Assert.DoesNotContain("Text=\"MONTHLY BILLS\"", xaml);
        Assert.DoesNotContain("Text=\"Monthly bills\"", xaml);
    }

    private static BillStreamResult Bill(decimal current, decimal previous = 0m,
        bool monthly = true, bool known = true, string? currency = "USD", bool active = true) =>
        new(Guid.NewGuid(), "Synthetic provider", "Other", active, current, previous)
        {
            CurrencyCode = currency, HasTransactionEvidence = known,
            HasMonthlyCadenceEvidence = monthly
        };

    private static async Task<MainPageViewModel> Load(params BillStreamResult[] bills)
    {
        using var fixture = await Fixture.Create(bills);
        await fixture.ViewModel.LoadAsync();
        Assert.False(fixture.ViewModel.HasError, fixture.ViewModel.ErrorMessage);
        Assert.Equal(2, fixture.Handler.Requests.Count);
        Assert.All(fixture.Handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
        return fixture.ViewModel;
    }

    private static string FindRepositoryFile(string name)
    {
        for (DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, name);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("Repository source fixture not found.", name);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly HttpClient _http;
        public DashboardHandler Handler { get; }
        public MainPageViewModel ViewModel { get; }

        private Fixture(DashboardHandler handler, HttpClient http, MainPageViewModel viewModel)
        { Handler = handler; _http = http; ViewModel = viewModel; }

        public static async Task<Fixture> Create(BillStreamResult[] bills)
        {
            var handler = new DashboardHandler { Bills = bills };
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://native-dashboard.test") };
            var session = new AuthSession();
            await session.SaveAsync("synthetic-native-access", "synthetic-native-refresh", 3600);
            var api = new FullWorthApiClient(http);
            var auth = new AuthenticationService(api, session, http);
            return new Fixture(handler, http, new MainPageViewModel(
                new BillStreamService(api, auth, http), new BillAlertService(http, auth)));
        }

        public void Dispose() => _http.Dispose();
    }

    private sealed class DashboardHandler : HttpMessageHandler
    {
        public BillStreamResult[] Bills { get; set; } = [];
        public string? BillsJsonOverride { get; set; }
        public HttpStatusCode BillsStatus { get; set; } = HttpStatusCode.OK;
        public string BillsMediaType { get; set; } = "application/json";
        public ConcurrentQueue<(HttpMethod Method, string Path)> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Enqueue((request.Method, request.RequestUri!.AbsolutePath));
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("synthetic-native-access", request.Headers.Authorization?.Parameter);
            return Task.FromResult(request.RequestUri.AbsolutePath switch
            {
                "/api/bill-streams" => new HttpResponseMessage(BillsStatus)
                    { Content = BillsJsonOverride is null && BillsMediaType == "application/json"
                        ? JsonContent.Create(Bills)
                        : new StringContent(BillsJsonOverride ?? System.Text.Json.JsonSerializer.Serialize(Bills,
                            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
                            System.Text.Encoding.UTF8, BillsMediaType) },
                "/api/alerts" => new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = JsonContent.Create(Array.Empty<BillAlertResult>()) },
                _ => throw new InvalidOperationException("Unexpected isolated dashboard request.")
            });
        }
    }
}
