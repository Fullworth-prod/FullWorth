using System.Net;
using System.Text;
using System.Text.Json;
using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Bills;
using FullWorth.API.Services.Contracts;
using FullWorth.API.Services.Plaid;
using FullWorth.Tests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FullWorth.Tests.Services;

public sealed class BankMonitoringFailureIsolationTests
{
    [Theory]
    [InlineData("account-attention")]
    [InlineData("transaction-attention")]
    [InlineData("transaction-transient")]
    [InlineData("account-save")]
    [InlineData("transaction-save")]
    [InlineData("timeout")]
    [InlineData("account-invalid")]
    public async Task PartialFailure_RefreshesHealthyBank_AndPreservesBillEvidence(string mode)
    {
        using var handler = new BankHandler { Mode = mode };
        using var http = new HttpClient(handler);
        var failure = new SaveFailure { Mode = mode };
        var probes = new Probes();
        await using var services = Services(http, failure, probes);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
        var setup = await Seed(db, services.GetRequiredService<PlaidTokenProtector>());
        failure.Armed = true;

        var error = await Record.ExceptionAsync(() =>
            scope.ServiceProvider.GetRequiredService<BillMonitoringRefreshService>().RefreshAsync(setup.UserId));
        Assert.NotNull(error);
        if (mode.EndsWith("-save", StringComparison.Ordinal)) Assert.IsType<DbUpdateException>(error);
        else if (mode == "timeout") Assert.IsAssignableFrom<OperationCanceledException>(error);
        else if (mode == "account-invalid") Assert.IsType<InvalidOperationException>(error);
        else Assert.IsType<PlaidApiException>(error);

        await using var verification = services.CreateAsyncScope();
        var stored = verification.ServiceProvider.GetRequiredService<FullWorthDbContext>();
        var healthy = await stored.BankConnections.SingleAsync(c => c.Id == HealthyId);
        var failed = await stored.BankConnections.SingleAsync(c => c.Id == FailedId);
        Assert.Equal("cursor-b-final", healthy.TransactionsCursor);
        Assert.True(healthy.LastSuccessfulSyncAtUtc > setup.OldSync);
        Assert.Equal("cursor-a-original", failed.TransactionsCursor);
        Assert.Equal(setup.OldSync, failed.LastSuccessfulSyncAtUtc);
        var attention = mode.EndsWith("-attention", StringComparison.Ordinal);
        Assert.Equal(attention ? BankConnectionStatus.RequiresAttention : BankConnectionStatus.Active, failed.Status);
        var accountBatchFailed = mode is "account-save" or "account-attention" or "timeout" or "account-invalid";
        Assert.Equal(accountBatchFailed ? "Original account a" : "Provider account a",
            (await stored.BankAccounts.SingleAsync(a => a.PlaidAccountId == "account-a")).Name);
        Assert.False(await stored.BankTransactions.AnyAsync(t => t.PlaidTransactionId == "transaction-a"));
        Assert.True(await stored.BankTransactions.AnyAsync(t => t.PlaidTransactionId == "transaction-b"));
        if (mode == "account-save")
            Assert.False(await stored.BankAccounts.AnyAsync(a => a.PlaidAccountId == "account-a-new"));
        var stream = await stored.BillStreams.SingleAsync();
        Assert.True(stream.IsActive);
        Assert.Equal(setup.OldSync, stream.UpdatedAtUtc);
        Assert.Equal(BillStreamSource.AutomaticDiscovery, stream.Source);
        Assert.Equal(0, probes.DiscoveryCalls);
        Assert.Equal(0, probes.PlanningCalls);
        Assert.DoesNotContain(handler.Calls, call => call.Token is "foreign" or "disconnected" or "attention");
        Assert.Contains(("/accounts/get", "bank-b"), handler.Calls);
        Assert.Contains(("/transactions/sync", "bank-b"), handler.Calls);
        Assert.Empty(db.ChangeTracker.Entries<BankTransactionEntity>());
        var due = await new BillMonitoringRefreshScheduler(new PlaidBankConnectionReadGateway(stored))
            .GetDueUserIdsAsync(DateTimeOffset.UtcNow, TimeSpan.FromHours(6), 25);
        Assert.Equal(!attention, due.Contains(setup.UserId));
    }

    [Fact]
    public async Task CompletePass_OrdersEachBanksAccountsBeforeTransactions_ThenDiscovers()
    {
        using var handler = new BankHandler();
        using var http = new HttpClient(handler);
        var probes = new Probes();
        await using var services = Services(http, new SaveFailure(), probes);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
        var setup = await Seed(db, services.GetRequiredService<PlaidTokenProtector>());
        await scope.ServiceProvider.GetRequiredService<BillMonitoringRefreshService>().RefreshAsync(setup.UserId);

        Assert.Equal(new[] { ("/accounts/get", "bank-a"), ("/transactions/sync", "bank-a"),
            ("/accounts/get", "bank-b"), ("/transactions/sync", "bank-b") }, handler.Calls);
        Assert.Equal(1, probes.DiscoveryCalls);
        Assert.Equal(1, probes.PlanningCalls);
        Assert.Equal(2, await db.BankTransactions.CountAsync());
        Assert.DoesNotContain(setup.UserId,
            await new BillMonitoringRefreshScheduler(new PlaidBankConnectionReadGateway(db))
                .GetDueUserIdsAsync(DateTimeOffset.UtcNow, TimeSpan.FromHours(6), 25));
    }

    [Fact]
    public async Task CallerCancellation_StopsBeforeTheNextBank()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new BankHandler { Mode = "caller-cancel", Cancel = cancellation.Cancel };
        using var http = new HttpClient(handler);
        await using var services = Services(http, new SaveFailure(), new Probes());
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
        var setup = await Seed(db, services.GetRequiredService<PlaidTokenProtector>());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            scope.ServiceProvider.GetRequiredService<IBankDataSyncGateway>().SyncAsync(setup.UserId, cancellation.Token));
        Assert.Single(handler.Calls);
        Assert.False(await db.BankTransactions.AnyAsync());
    }

    [Fact]
    public async Task DefaultContract_PreservesLegacyProviderOrdering()
    {
        var legacy = new LegacyGateway();
        await ((IBankDataSyncGateway)legacy).SyncAsync(Guid.NewGuid());
        Assert.Equal(new[] { "accounts", "transactions" }, legacy.Calls);
    }

    [Fact]
    public async Task FullFailingBatch_DoesNotStarveLaterUsersOrRetryWithoutFinishingSweep()
    {
        using var handler = new BankHandler { Mode = "transaction-transient" };
        using var http = new HttpClient(handler);
        var probes = new Probes();
        await using var services = Services(http, new SaveFailure(), probes);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
        var failingUser = Guid.Parse("00000000-0000-0000-0000-000000000010");
        var laterUser = Guid.Parse("00000000-0000-0000-0000-000000000020");
        await Seed(db, services.GetRequiredService<PlaidTokenProtector>(), failingUser);
        foreach (var foreign in await db.BankConnections.Where(c => c.UserId != failingUser).ToListAsync())
        {
            foreign.LastSuccessfulSyncAtUtc = DateTimeOffset.UtcNow;
        }
        var laterConnection = new BankConnectionEntity
        {
            UserId = laterUser, InstitutionName = "Synthetic later bank",
            ProtectedPlaidAccessToken = services.GetRequiredService<PlaidTokenProtector>().Protect("bank-b"),
            TransactionsCursor = "cursor-later-original",
            LastSuccessfulSyncAtUtc = DateTimeOffset.UtcNow.AddDays(-2)
        };
        db.BankConnections.Add(laterConnection);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        using var background = new BillMonitoringBackgroundService(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new BillMonitoringBackgroundOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BillMonitoringBackgroundService>.Instance);
        var runPass = typeof(BillMonitoringBackgroundService).GetMethod("RunPassAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Task<int> Pass() => (Task<int>)runPass.Invoke(background,
            [TimeSpan.FromHours(6), 1, CancellationToken.None])!;

        Assert.Equal(1, await Pass()); // The first user fails after its healthy bank updates.
        Assert.Equal(1, await Pass()); // The next user must still get a turn.
        var stored = await db.BankConnections.AsNoTracking().SingleAsync(c => c.Id == laterConnection.Id);
        Assert.Equal("cursor-b-final", stored.TransactionsCursor);
        Assert.Equal(1, probes.DiscoveryCalls);
        Assert.Single(handler.Calls.Where(call => call == ("/transactions/sync", "bank-a")));
        Assert.Equal(0, await Pass()); // Finish the sweep, allowing the normal poll delay before retry.
        Assert.Single(handler.Calls.Where(call => call == ("/transactions/sync", "bank-a")));
    }

    private static readonly Guid FailedId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid HealthyId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private static ServiceProvider Services(HttpClient http, SaveFailure failure, Probes probes)
    {
        var options = new DbContextOptionsBuilder<FullWorthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).AddInterceptors(failure).Options;
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddScoped(_ => new FullWorthDbContext(options));
        collection.AddSingleton(http);
        collection.AddSingleton(new PlaidTokenProtector(new EphemeralDataProtectionProvider()));
        collection.AddSingleton(Options.Create(new PlaidOptions
        {
            ClientId = "test-client", Secret = "test-secret", Environment = PlaidOptions.SandboxEnvironment
        }));
        collection.AddSingleton(TestSecurityAlertAggregator.Create());
        collection.AddScoped<PlaidApiClient>();
        collection.AddScoped<PlaidAccountSyncService>();
        collection.AddScoped<PlaidTransactionSyncService>();
        collection.AddScoped<PlaidConnectionSyncCoordinator>();
        collection.AddScoped<IBankDataSyncGateway, PlaidBankDataSyncGateway>();
        collection.AddScoped<IBankConnectionReadGateway, PlaidBankConnectionReadGateway>();
        collection.AddScoped<BankConnectionHealthAlertService>();
        collection.AddScoped<BillMonitoringRefreshScheduler>();
        collection.AddScoped<RecurringBillDiscoveryPersistenceService>();
        collection.AddScoped<BillMonitoringRefreshService>();
        collection.AddSingleton<IBankTransactionDiscoveryGateway>(probes);
        collection.AddSingleton<IBillTransactionAssociationGateway>(probes);
        collection.AddSingleton<IPlanningPaydayAlertRefreshGateway>(probes);
        return collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static async Task<(Guid UserId, DateTimeOffset OldSync)> Seed(FullWorthDbContext db, PlaidTokenProtector protector, Guid? owner = null)
    {
        var userId = owner ?? Guid.NewGuid();
        var old = DateTimeOffset.UtcNow.AddDays(-2);
        BankConnectionEntity Connection(Guid owner, Guid id, string token, BankConnectionStatus status = BankConnectionStatus.Active) =>
            new() { Id = id, UserId = owner, InstitutionName = "Synthetic bank", PlaidItemId = "item-" + token,
                ProtectedPlaidAccessToken = protector.Protect(token), Status = status, LastSuccessfulSyncAtUtc = old,
                TransactionsCursor = token == "bank-a" ? "cursor-a-original" : "cursor-b-original" };
        db.BankConnections.AddRange(
            Connection(userId, FailedId, "bank-a"), Connection(userId, HealthyId, "bank-b"),
            Connection(Guid.NewGuid(), Guid.NewGuid(), "foreign"),
            Connection(userId, Guid.NewGuid(), "disconnected", BankConnectionStatus.Disconnected),
            Connection(userId, Guid.NewGuid(), "attention", BankConnectionStatus.RequiresAttention));
        db.BankAccounts.Add(new BankAccountEntity { UserId = userId, BankConnectionId = FailedId,
            PlaidAccountId = "account-a", Name = "Original account a", IsActive = true });
        db.BillStreams.Add(new BillStreamEntity { UserId = userId, ProviderName = "Prior bill evidence",
            Source = BillStreamSource.AutomaticDiscovery, IsActive = true, UpdatedAtUtc = old });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (userId, old);
    }

    private sealed class SaveFailure : SaveChangesInterceptor
    {
        public string Mode { get; init; } = "";
        public bool Armed { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var db = eventData.Context!;
            if (Armed && ((Mode == "account-save" && db.ChangeTracker.Entries<BankAccountEntity>()
                .Any(e => e.State == EntityState.Added && e.Entity.PlaidAccountId == "account-a-new")) ||
                (Mode == "transaction-save" && db.ChangeTracker.Entries<BankTransactionEntity>()
                .Any(e => e.State == EntityState.Added && e.Entity.PlaidTransactionId == "transaction-a"))))
            {
                Armed = false;
                throw new DbUpdateException("Synthetic save failure.");
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class BankHandler : HttpMessageHandler
    {
        public string Mode { get; init; } = "";
        public Action? Cancel { get; init; }
        public List<(string Path, string Token)> Calls { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var token = body.RootElement.GetProperty("access_token").GetString()!;
            var path = request.RequestUri!.AbsolutePath;
            Calls.Add((path, token));
            if (token is not ("bank-a" or "bank-b")) throw new InvalidOperationException("Unexpected bank.");
            if (token == "bank-a")
            {
                if (Mode == "caller-cancel") { Cancel!(); cancellationToken.ThrowIfCancellationRequested(); }
                if (Mode == "timeout") throw new TaskCanceledException("Synthetic provider timeout.");
                if ((Mode == "account-attention" && path == "/accounts/get") ||
                    (Mode == "transaction-attention" && path == "/transactions/sync"))
                    return Json(HttpStatusCode.BadRequest, new { error_type = "ITEM_ERROR", error_code = "ITEM_LOGIN_REQUIRED" });
                if (Mode == "transaction-transient" && path == "/transactions/sync")
                    return Json(HttpStatusCode.ServiceUnavailable, new { error_type = "API_ERROR", error_code = "INTERNAL_SERVER_ERROR" });
                if (Mode == "account-invalid") return Json(HttpStatusCode.OK, new { accounts = new[] { new { account_id = "" } } });
            }
            var suffix = token == "bank-a" ? "a" : "b";
            if (path == "/accounts/get")
                return Json(HttpStatusCode.OK, new { accounts = new[]
                {
                    new { account_id = "account-" + suffix, name = "Provider account " + suffix, type = "depository", subtype = "checking" },
                    new { account_id = "account-" + suffix + "-new", name = "New account " + suffix, type = "depository", subtype = "savings" }
                } });
            if (path == "/transactions/sync")
                return Json(HttpStatusCode.OK, new
                {
                    added = new[] { new { transaction_id = "transaction-" + suffix, account_id = "account-" + suffix,
                        name = "Synthetic service", amount = 10m, iso_currency_code = "USD", date = "2026-08-01", pending = false } },
                    modified = Array.Empty<object>(), removed = Array.Empty<object>(), next_cursor = "cursor-" + suffix + "-final", has_more = false
                });
            throw new InvalidOperationException("Unexpected endpoint.");
        }
        private static HttpResponseMessage Json(HttpStatusCode status, object body) =>
            new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
    }

    private sealed class Probes : IBankTransactionDiscoveryGateway, IBillTransactionAssociationGateway, IPlanningPaydayAlertRefreshGateway
    {
        public int DiscoveryCalls { get; private set; }
        public int PlanningCalls { get; private set; }
        public Task<IReadOnlyList<BankTransactionDiscoveryRecord>> GetDiscoveryTransactionsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            DiscoveryCalls++;
            return Task.FromResult<IReadOnlyList<BankTransactionDiscoveryRecord>>([]);
        }
        public Task<IReadOnlyList<BillTransactionAssociationRecord>> GetAsync(Guid userId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BillTransactionAssociationRecord>>([]);
        public Task StageAssignmentsAsync(Guid userId, IReadOnlyCollection<BillTransactionAssociationAssignment> assignments,
            DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PlanningPaydayAlertRefreshResult> RefreshAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            PlanningCalls++;
            return Task.FromResult(new PlanningPaydayAlertRefreshResult(0, 0, 0, false));
        }
    }

    private sealed class LegacyGateway : IBankDataSyncGateway
    {
        public List<string> Calls { get; } = [];
        public Task SyncAccountsAsync(Guid userId, CancellationToken cancellationToken = default) { Calls.Add("accounts"); return Task.CompletedTask; }
        public Task SyncTransactionsAsync(Guid userId, CancellationToken cancellationToken = default) { Calls.Add("transactions"); return Task.CompletedTask; }
    }
}
