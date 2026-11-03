using System.Net;
using System.Net.Http.Json;
using FullWorth.API.Controllers;
using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Bills;
using FullWorth.API.Services.Planning;
using FullWorth.API.Services.Plaid;
using FullWorth.Core.Models.Planning;
using FullWorth.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FullWorth.Tests.Security;

public sealed class PaydayPlanCapacityBoundaryTests
{
    private static readonly DateOnly Posted = new(2026, 9, 25);

    [Theory]
    [InlineData(1, true)]
    [InlineData(500, true)]
    [InlineData(501, false)]
    [InlineData(750, false)]
    public async Task Generation_ReportsOwnedCapacityWithoutTruncationOrPartialWrites(
        int activeCount, bool supported)
    {
        await using var factory = new FullWorthApiFactory();
        using var client = factory.CreateHttpsClient();
        using var other = factory.CreateHttpsClient();
        var login = await TestUserAuthentication.RegisterAndLoginAsync(client);
        var otherLogin = await TestUserAuthentication.RegisterAndLoginAsync(other);
        var owner = await TestUserAuthentication.GetUserIdAsync(factory, login.Email);
        var foreign = await TestUserAuthentication.GetUserIdAsync(factory, otherLogin.Email);
        var payroll = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
            await SeedAsync(scope.ServiceProvider, owner, payroll, activeCount);
            // The owner at the exact supported boundary must not inherit another owner's bills.
            db.BillStreams.AddRange(Enumerable.Range(0, 501).Select(i => new BillStreamEntity
            {
                UserId = foreign, ProviderName = $"Foreign fixture {i:D4}", IsActive = true
            }));
            await db.SaveChangesAsync();
        }

        TestUserAuthentication.Authorize(client, login);
        using var response = await client.PutAsJsonAsync(
            $"/api/planning/payday-plans/{payroll}", new { postedDate = Posted });
        Assert.Equal(supported ? HttpStatusCode.OK : HttpStatusCode.UnprocessableEntity,
            response.StatusCode);
        if (supported)
        {
            var plan = await response.Content.ReadFromJsonAsync<PlanningPaydayPlanResponse>();
            Assert.NotNull(plan);
            Assert.False(plan.IsReplay);
            Assert.Equal(100m, plan.RecommendedSetAside);
            Assert.Equal(900m, plan.PaycheckRemainingAfterPlan);
            Assert.Single(plan.Items);
            var coverage = Assert.IsType<PlanningPaycheckPlanCoverage>(plan.Coverage);
            Assert.Equal(activeCount, coverage.EvaluatedBillCount);
            Assert.Equal(activeCount - 1, coverage.MissingStatementFactCount);
        }
        else
        {
            var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            Assert.NotNull(error);
            Assert.Equal("This payday plan exceeds the supported bill limit. No plan was saved.",
                error["message"]);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
            Assert.Equal(supported ? 1 : 0,
                await db.PlanningPaycheckPlanRuns.CountAsync(x => x.UserId == owner));
            Assert.Equal(supported ? 1 : 0,
                await db.PlanningPaycheckAllocations.CountAsync(x => x.UserId == owner));
            Assert.False(await db.BillAlerts.AnyAsync(x => x.UserId == owner));
            Assert.Equal(activeCount, await db.BillStreams.CountAsync(x => x.UserId == owner));
        }

        TestUserAuthentication.Authorize(other, otherLogin);
        using var foreignRequest = await other.PutAsJsonAsync(
            $"/api/planning/payday-plans/{payroll}", new { postedDate = Posted });
        Assert.Equal(HttpStatusCode.NotFound, foreignRequest.StatusCode);
        Assert.Empty((await other.GetFromJsonAsync<List<PlanningPaydayPlanSummaryResponse>>(
            "/api/planning/payday-plans/recent"))!);
    }

    [Fact]
    public async Task SavedReplay_RemainsAvailableAfterActiveBillCountExceedsCapacity()
    {
        await using var factory = new FullWorthApiFactory();
        using var client = factory.CreateHttpsClient();
        var login = await TestUserAuthentication.RegisterAndLoginAsync(client);
        var owner = await TestUserAuthentication.GetUserIdAsync(factory, login.Email);
        var payroll = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
            await new PlanningPaycheckAllocationStore(db, TimeProvider.System)
                .SaveCompletePaycheckPlanAsync(owner, payroll,
                    new PlanningPaycheckPlanRunDraft(Posted, 1000m, "USD", 0m, 1000m, 0m)
                    { Coverage = new(0, 0, 0, 0, 0, 0) }, []);
            await SeedAsync(scope.ServiceProvider, owner, Guid.NewGuid(), 501);
        }

        TestUserAuthentication.Authorize(client, login);
        using var response = await client.PutAsJsonAsync(
            $"/api/planning/payday-plans/{payroll}", new { postedDate = Posted });
        response.EnsureSuccessStatusCode();
        var replay = await response.Content.ReadFromJsonAsync<PlanningPaydayPlanResponse>();
        Assert.NotNull(replay);
        Assert.True(replay.IsReplay);
        Assert.Equal(0m, replay.RecommendedSetAside);
        Assert.Equal(1000m, replay.PaycheckRemainingAfterPlan);
        Assert.Equal(0, Assert.IsType<PlanningPaycheckPlanCoverage>(replay.Coverage).EvaluatedBillCount);
        Assert.Empty(replay.Items);
        await using var verify = factory.Services.CreateAsyncScope();
        var stored = verify.ServiceProvider.GetRequiredService<FullWorthDbContext>();
        Assert.Single(await stored.PlanningPaycheckPlanRuns.ToListAsync());
        Assert.Empty(await stored.PlanningPaycheckAllocations.ToListAsync());
    }

    [Fact]
    public async Task Monitoring_CapacityFailureStaysObservableWithoutBlockingSavedPaycheck()
    {
        await using var factory = new FullWorthApiFactory();
        using var client = factory.CreateHttpsClient();
        var login = await TestUserAuthentication.RegisterAndLoginAsync(client);
        var owner = await TestUserAuthentication.GetUserIdAsync(factory, login.Email);
        var failedPayroll = Guid.NewGuid();
        var savedPayroll = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
        await SeedAsync(scope.ServiceProvider, owner, failedPayroll, 501);
        db.BankTransactions.Add(Payroll(owner, savedPayroll, Posted.AddDays(1)));
        await db.SaveChangesAsync();
        await new PlanningPaycheckAllocationStore(db, TimeProvider.System)
            .SaveCompletePaycheckPlanAsync(owner, savedPayroll,
                new PlanningPaycheckPlanRunDraft(Posted.AddDays(1), 1000m, "USD", 0m, 1000m, 0m)
                { Coverage = new(0, 0, 0, 0, 0, 0) }, []);
        var clock = new FixedClock(new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero));
        var refresh = new PlanningPaydayAlertRefreshService(
            scope.ServiceProvider.GetRequiredService<PlanningPaydayPlanService>(),
            new PlaidPlanningPostedPayrollFactsGateway(db),
            new PlanningPaydayAlertGateway(db), clock);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => refresh.RefreshAsync(owner));
        Assert.Equal("Payday plan exceeds the supported bill limit.", error.Message);
        Assert.Equal(savedPayroll, Assert.Single(await db.BillAlerts.AsNoTracking().ToListAsync()).SourceEventId);
        Assert.Equal(savedPayroll,
            Assert.Single(await db.PlanningPaycheckPlanRuns.AsNoTracking().ToListAsync()).PayrollTransactionId);
        Assert.Empty(await db.PlanningPaycheckAllocations.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CapacityRefusal_DoesNotFlushOrDiscardPendingCallerWork()
    {
        await using var factory = new FullWorthApiFactory();
        using var client = factory.CreateHttpsClient();
        var login = await TestUserAuthentication.RegisterAndLoginAsync(client);
        var owner = await TestUserAuthentication.GetUserIdAsync(factory, login.Email);
        var payroll = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
        await SeedAsync(scope.ServiceProvider, owner, payroll, 501);
        var retained = await db.BankConnections.SingleAsync();
        var originalName = retained.InstitutionName;
        retained.InstitutionName = "Pending caller edit";
        var added = new BankConnectionEntity { UserId = owner, InstitutionName = "Pending caller addition" };
        db.Add(added);
        db.ChangeTracker.DetectChanges();
        var countBefore = db.ChangeTracker.Entries().Count();

        var result = await scope.ServiceProvider.GetRequiredService<PlanningPaydayPlanService>()
            .GenerateAsync(owner, payroll, Posted);

        Assert.Equal("BillLimitExceeded", result.Status.ToString());
        Assert.Empty(result.Items);
        Assert.Null(result.Coverage);
        Assert.Equal(0m, result.RecommendedSetAside);
        Assert.Equal(countBefore, db.ChangeTracker.Entries().Count());
        Assert.Equal(EntityState.Modified, db.Entry(retained).State);
        Assert.Equal(originalName,
            db.Entry(retained).OriginalValues.GetValue<string>(nameof(retained.InstitutionName)));
        Assert.Equal(EntityState.Added, db.Entry(added).State);
        await using (var verify = factory.Services.CreateAsyncScope())
        {
            var durable = verify.ServiceProvider.GetRequiredService<FullWorthDbContext>();
            Assert.Equal(originalName,
                (await durable.BankConnections.AsNoTracking().SingleAsync()).InstitutionName);
            Assert.False(await durable.BankConnections.AnyAsync(x => x.Id == added.Id));
            Assert.Empty(await durable.PlanningPaycheckPlanRuns.ToListAsync());
        }
        await db.SaveChangesAsync();
        Assert.Empty(await db.PlanningPaycheckPlanRuns.AsNoTracking().ToListAsync());
        Assert.Empty(await db.PlanningPaycheckAllocations.AsNoTracking().ToListAsync());
        Assert.Equal(2, await db.BankConnections.CountAsync());
    }

    private static async Task SeedAsync(IServiceProvider services, Guid owner, Guid payroll, int activeCount)
    {
        var db = services.GetRequiredService<FullWorthDbContext>();
        var bills = Enumerable.Range(0, activeCount).Select(i => new BillStreamEntity
        {
            UserId = owner, ProviderName = $"Owned fixture {i:D4}", IsActive = true
        }).ToArray();
        db.BillStreams.AddRange(bills);
        db.BankTransactions.Add(Payroll(owner, payroll, Posted));
        // Put the only supported fact at the end: taking the first 500 is not an acceptable fix.
        db.BillStatements.Add(new BillStatementEntity
        {
            UserId = owner, BillStreamId = bills[^1].Id,
            PeriodStart = new(2026, 9, 1), PeriodEnd = Posted.AddDays(-1),
            DueDate = Posted, TotalAmount = 100m, CurrencyCode = "USD"
        });
        await db.SaveChangesAsync();
        await services.GetRequiredService<PlanningSettingsService>().SavePayScheduleAsync(
            owner, PayScheduleFrequency.Weekly, Posted, null, 1);
    }

    private static BankTransactionEntity Payroll(Guid owner, Guid id, DateOnly posted)
    {
        var connection = new BankConnectionEntity
        {
            UserId = owner, InstitutionName = "Synthetic institution"
        };
        var account = new BankAccountEntity
        {
            UserId = owner, BankConnectionId = connection.Id, BankConnection = connection,
            PlaidAccountId = Guid.NewGuid().ToString("N"), Name = "Synthetic account"
        };
        return new BankTransactionEntity
        {
            Id = id, UserId = owner, BankAccountId = account.Id, BankAccount = account,
            PlaidTransactionId = id.ToString("N"), Name = "Synthetic payroll",
            PostedDate = posted, Amount = -1000m, IsoCurrencyCode = "USD",
            CategoryPrimary = "INCOME", CategoryDetailed = "INCOME_WAGES"
        };
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
