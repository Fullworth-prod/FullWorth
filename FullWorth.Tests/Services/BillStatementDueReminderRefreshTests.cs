using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Bills;
using FullWorth.API.Services.Statements;
using FullWorth.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.Tests.Services;

public sealed class BillStatementDueReminderRefreshTests
{
    [Fact]
    public async Task Refresh_UpdatesUrgencyAndRemovesExpiredReminderWithoutNewUpload()
    {
        await using var db = CreateDb();
        var owner = Guid.NewGuid();
        var stream = SeedStream(db, owner);
        var due = new DateOnly(2026, 8, 20);
        SeedStatement(db, owner, stream.Id, due, 94.99m);
        await db.SaveChangesAsync();

        var gateway = new BillStreamReadGateway(db);
        var alerts = new BillAlertReconciliationGateway(db);
        var service = new BillStatementPaymentDueAlertService(gateway, alerts);
        var firstDay = new DateOnly(2026, 8, 1);
        await service.ReconcileAsync(owner, stream.Id, due, 94.99m, "USD",
            firstDay, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        var original = Assert.Single(await db.BillAlerts.ToListAsync());
        Assert.Equal(BillAlertSeverity.Info, original.Severity);

        var refresh = new BillStatementDueReminderRefreshService(
            db, service, alerts, new BillPaymentDueRefreshCandidatesGateway(db));
        Assert.Equal(1, await refresh.RefreshAsync(
            new DateOnly(2026, 8, 14), DateTimeOffset.UtcNow));

        var warning = Assert.Single(await db.BillAlerts.ToListAsync());
        Assert.Equal(original.Id, warning.Id);
        Assert.Equal(BillAlertSeverity.Warning, warning.Severity);
        Assert.Contains("$94.99", warning.Message);

        Assert.Equal(1, await refresh.RefreshAsync(
            new DateOnly(2026, 8, 21), DateTimeOffset.UtcNow));
        Assert.Empty(await db.BillAlerts.Where(alert =>
            alert.AlertType == BillAlertType.PaymentDue).ToListAsync());
    }

    [Fact]
    public async Task Refresh_UsesEachAlertsOwnedStatementAndDoesNotMixAmounts()
    {
        await using var db = CreateDb();
        var firstOwner = Guid.NewGuid();
        var secondOwner = Guid.NewGuid();
        var firstStream = SeedStream(db, firstOwner);
        var secondStream = SeedStream(db, secondOwner);
        var due = new DateOnly(2026, 9, 20);
        SeedStatement(db, firstOwner, firstStream.Id, due, 25m);
        SeedStatement(db, secondOwner, secondStream.Id, due, 90m);
        await db.SaveChangesAsync();

        var gateway = new BillStreamReadGateway(db);
        var alerts = new BillAlertReconciliationGateway(db);
        var dueService = new BillStatementPaymentDueAlertService(gateway, alerts);
        var firstDay = new DateOnly(2026, 9, 1);
        await dueService.ReconcileAsync(firstOwner, firstStream.Id, due, 25m,
            "USD", firstDay, DateTimeOffset.UtcNow);
        await dueService.ReconcileAsync(secondOwner, secondStream.Id, due, 90m,
            "USD", firstDay, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        var refresh = new BillStatementDueReminderRefreshService(
            db, dueService, alerts, new BillPaymentDueRefreshCandidatesGateway(db));
        Assert.Equal(2, await refresh.RefreshAsync(
            new DateOnly(2026, 9, 14), DateTimeOffset.UtcNow));

        var results = await db.BillAlerts.ToListAsync();
        Assert.Equal(2, results.Count);
        Assert.Contains(results, item => item.UserId == firstOwner &&
            item.BillStreamId == firstStream.Id &&
            item.Message.Contains("$25.00") && !item.Message.Contains("$90.00") &&
            item.Severity == BillAlertSeverity.Warning);
        Assert.Contains(results, item => item.UserId == secondOwner &&
            item.BillStreamId == secondStream.Id &&
            item.Message.Contains("$90.00") && !item.Message.Contains("$25.00") &&
            item.Severity == BillAlertSeverity.Warning);
    }

    private static FullWorthDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<FullWorthDbContext>()
            .UseInMemoryDatabase($"due-refresh-{Guid.NewGuid():N}").Options);

    private static BillStreamEntity SeedStream(FullWorthDbContext db, Guid owner)
    {
        var stream = new BillStreamEntity
        {
            UserId = owner,
            ProviderName = "Example Utility",
            Category = BillCategory.Utility,
            Source = BillStreamSource.Manual,
            IsActive = true
        };
        db.BillStreams.Add(stream);
        return stream;
    }

    private static void SeedStatement(
        FullWorthDbContext db, Guid owner, Guid streamId,
        DateOnly due, decimal amount)
    {
        db.BillStatements.Add(new BillStatementEntity
        {
            UserId = owner,
            BillStreamId = streamId,
            PeriodStart = due.AddMonths(-1).AddDays(-9),
            PeriodEnd = due.AddDays(-9),
            StatementDate = due.AddDays(-8),
            DueDate = due,
            CurrencyCode = "USD",
            TotalAmount = amount,
            RetrievedAtUtc = DateTimeOffset.UtcNow
        });
    }
}
