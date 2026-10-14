using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Bills;
using FullWorth.API.Services.Statements;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.Tests.Services;

public sealed class StatementDueReminderPersistenceTests
{
    [Fact]
    public async Task OldUploadAndOldDuplicate_CannotReplaceLatestReminder()
    {
        await using var db = CreateDb();
        var user = Guid.NewGuid();
        var stream = new BillStreamEntity { UserId = user, ProviderName = "Example" };
        db.BillStreams.Add(stream);
        await db.SaveChangesAsync();
        var service = Service(db);
        var currentStart = new DateOnly(DateTime.UtcNow.Year + 1, 3, 1);
        var currentDue = currentStart.AddMonths(1).AddDays(10);
        await service.PersistAsync(Upload(user, stream.Id),
            Parsed(currentStart, currentDue, 100m), []);

        var older = Parsed(currentStart.AddMonths(-1), currentDue.AddDays(-15), 80m);
        await service.PersistAsync(Upload(user, stream.Id), older, []);
        await service.PersistAsync(Upload(user, stream.Id), older, []);

        var reminder = Assert.Single(await db.BillAlerts
            .Where(item => item.AlertType == BillAlertType.PaymentDue).ToListAsync());
        Assert.Contains(currentDue.ToString("MMM d, yyyy",
            System.Globalization.CultureInfo.InvariantCulture), reminder.Title);
        Assert.Contains("$100.00", reminder.Message);
        Assert.Equal(2, await db.BillStatements.CountAsync());
    }

    [Fact]
    public async Task LatestRetrievedCorrection_DrivesBothPlanningAndDueReminder()
    {
        await using var db = CreateDb();
        var user = Guid.NewGuid();
        var stream = new BillStreamEntity { UserId = user, ProviderName = "Example" };
        db.BillStreams.Add(stream);
        await db.SaveChangesAsync();
        var service = Service(db);
        var start = new DateOnly(DateTime.UtcNow.Year + 1, 3, 1);
        var due = start.AddMonths(1).AddDays(10);
        var first = await service.PersistAsync(Upload(user, stream.Id),
            Parsed(start, due, 100m), []);
        var old = await db.BillStatements.SingleAsync(item => item.Id == first.StatementId);
        old.RetrievedAtUtc = DateTimeOffset.UtcNow.AddDays(-1);
        old.StatementDate = start.AddMonths(1).AddDays(5);
        await db.SaveChangesAsync();

        var correction = await service.PersistAsync(Upload(user, stream.Id),
            Parsed(start, due.AddDays(5), 125m), []);
        var facts = await new StatementBillPlanningFactsGateway(db)
            .GetLatestAsync(user, [stream.Id]);

        Assert.Equal(correction.StatementId, facts[stream.Id].StatementId);
        Assert.Equal(125m, facts[stream.Id].Amount);
        Assert.Contains("$125.00", Assert.Single(await db.BillAlerts
            .Where(item => item.AlertType == BillAlertType.PaymentDue).ToListAsync()).Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewCorrectionWithoutPayableAmount_ClearsOldReminder(bool zeroAmount)
    {
        await using var db = CreateDb();
        var user = Guid.NewGuid();
        var stream = new BillStreamEntity { UserId = user, ProviderName = "Example" };
        db.BillStreams.Add(stream);
        await db.SaveChangesAsync();
        var service = Service(db);
        var start = new DateOnly(DateTime.UtcNow.Year + 1, 3, 1);
        var due = start.AddMonths(1).AddDays(10);
        await service.PersistAsync(Upload(user, stream.Id), Parsed(start, due, 100m), []);
        var first = await db.BillStatements.SingleAsync();
        first.RetrievedAtUtc = DateTimeOffset.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        await service.PersistAsync(Upload(user, stream.Id),
            Parsed(start, zeroAmount ? due : null, zeroAmount ? 0m : 100m), []);

        Assert.Empty(await db.BillAlerts.Where(item =>
            item.AlertType == BillAlertType.PaymentDue).ToListAsync());
        Assert.Equal(2, await db.BillStatements.CountAsync());
    }

    private static BillStatementStructuredData Parsed(DateOnly start, DateOnly? due, decimal amount) =>
        new(amount, start, start.AddMonths(1).AddDays(-1), start.AddMonths(1), due,
            "USD", BillStatementStructuredDataConfidence.StrongEvidence, []);

    private static BillStatementUploadEntity Upload(Guid user, Guid stream) =>
        new() { UserId = user, BillStreamId = stream };

    private static FullWorthDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<FullWorthDbContext>()
            .UseInMemoryDatabase($"statement-due-{Guid.NewGuid():N}").Options);

    private static BillStatementPersistenceService Service(FullWorthDbContext db)
    {
        var streams = new BillStreamReadGateway(db);
        var alerts = new BillAlertReconciliationGateway(db);
        return new BillStatementPersistenceService(db,
            new BillStatementChangeDetectionService(db, streams,
                new BillStatementEvidenceAlertService(), alerts),
            new BillStatementPaymentDueAlertService(streams, alerts));
    }
}
