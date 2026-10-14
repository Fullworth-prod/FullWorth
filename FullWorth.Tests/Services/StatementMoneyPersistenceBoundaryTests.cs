using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Bills;
using FullWorth.API.Services.Statements;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.Tests.Services;

public sealed class StatementMoneyPersistenceBoundaryTests
{
    [Theory]
    [InlineData("10000000000000000.00")]
    [InlineData("79228162514264337593543950335")]
    [InlineData("100.001")]
    [InlineData("-1.00")]
    public async Task UnsupportedTotal_IsRejectedWithoutStagingFinancialRows(string value)
    {
        await using var db = Db();
        var user = Guid.NewGuid();
        var stream = new BillStreamEntity { UserId = user, ProviderName = "Example" };
        db.BillStreams.Add(stream);
        await db.SaveChangesAsync();
        var streams = new BillStreamReadGateway(db);
        var alerts = new BillAlertReconciliationGateway(db);
        var service = new BillStatementPersistenceService(db,
            new BillStatementChangeDetectionService(db, streams,
                new BillStatementEvidenceAlertService(), alerts),
            new BillStatementPaymentDueAlertService(streams, alerts));
        var parsed = new BillStatementStructuredData(
            decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
            null, null, "USD", BillStatementStructuredDataConfidence.StrongEvidence, []);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PersistAsync(
            new BillStatementUploadEntity { UserId = user, BillStreamId = stream.Id },
            parsed, []));
        Assert.Empty(db.BillStatements.Local);
        Assert.Empty(db.BillChanges.Local);
        Assert.Empty(db.BillAlerts.Local);
    }

    [Fact]
    public async Task AnnualizationBeyondStorageRange_PreservesStatementsWithoutChangeRows()
    {
        await using var db = Db();
        var user = Guid.NewGuid();
        var stream = new BillStreamEntity { UserId = user, ProviderName = "Example" };
        db.BillStreams.Add(stream);
        var previous = new BillStatementEntity
        {
            UserId = user, BillStreamId = stream.Id,
            PeriodStart = new DateOnly(2026, 1, 1), PeriodEnd = new DateOnly(2026, 1, 31),
            TotalAmount = 0m, CurrencyCode = "USD"
        };
        var current = new BillStatementEntity
        {
            UserId = user, BillStreamId = stream.Id,
            PeriodStart = new DateOnly(2026, 2, 1), PeriodEnd = new DateOnly(2026, 2, 28),
            TotalAmount = 9_999_999_999_999_999.99m, CurrencyCode = "USD"
        };
        db.BillStatements.AddRange(previous, current);
        await db.SaveChangesAsync();
        var service = new BillStatementChangeDetectionService(db,
            new BillStreamReadGateway(db), new BillStatementEvidenceAlertService(),
            new BillAlertReconciliationGateway(db));

        await service.ReconcileAsync(user, stream.Id);
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.BillStatements.CountAsync());
        Assert.Empty(await db.BillChanges.ToListAsync());
        Assert.Empty(await db.BillAlerts.ToListAsync());
    }

    private static FullWorthDbContext Db() =>
        new(new DbContextOptionsBuilder<FullWorthDbContext>()
            .UseInMemoryDatabase($"money-boundary-{Guid.NewGuid():N}").Options);
}
