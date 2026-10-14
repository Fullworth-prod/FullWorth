using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Bills;
using FullWorth.API.Services.Statements;
using FullWorth.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.Tests.Services;

public sealed class BillStatementAlertGenerationTests
{
    [Fact]
    public async Task Increase_CreatesSingleOwnershipScopedAlert()
    {
        await using var dbContext =
            CreateDbContext();

        var userId =
            Guid.NewGuid();

        var stream =
            CreateBillStream(
                userId);

        var previous =
            CreateStatement(
                userId,
                stream.Id,
                new DateOnly(
                    2026,
                    6,
                    1),
                new DateOnly(
                    2026,
                    6,
                    30),
                79.99m);

        var current =
            CreateStatement(
                userId,
                stream.Id,
                new DateOnly(
                    2026,
                    7,
                    1),
                new DateOnly(
                    2026,
                    7,
                    31),
                104.99m);

        dbContext.BillStreams.Add(
            stream);

        dbContext.BillStatements.AddRange(
            previous,
            current);

        await dbContext.SaveChangesAsync();

        var service =
            new BillStatementChangeDetectionService(
                dbContext,
                new BillStreamReadGateway(
                    dbContext),
                new BillStatementEvidenceAlertService(),
                new BillAlertReconciliationGateway(
                    dbContext));

        await service.ReconcileAsync(
            userId,
            stream.Id);

        await dbContext.SaveChangesAsync();

        var change =
            Assert.Single(
                await dbContext.BillChanges
                    .Where(
                        item =>
                            item.UserId ==
                                userId)
                    .ToListAsync());

        var alert =
            Assert.Single(
                await dbContext.BillAlerts
                    .Where(
                        item =>
                            item.UserId ==
                                userId)
                    .ToListAsync());

        Assert.Equal(
            stream.Id,
            alert.BillStreamId);

        Assert.Equal(
            change.Id,
            alert.BillChangeId);

        Assert.Equal(
            BillAlertType.BillIncrease,
            alert.AlertType);

        Assert.Equal(
            BillAlertSeverity.Warning,
            alert.Severity);

        Assert.False(
            alert.IsRead);

        Assert.False(
            alert.IsDismissed);

        Assert.Contains(
            "$25.00/month",
            alert.Title);

        Assert.Contains(
            "$300.00/year",
            alert.Message);

        /*
         * Run reconciliation again.
         *
         * The same BillChange must not produce another alert.
         */
        await service.ReconcileAsync(
            userId,
            stream.Id);

        await dbContext.SaveChangesAsync();

        Assert.Single(
            await dbContext.BillAlerts
                .Where(
                    item =>
                        item.UserId ==
                            userId)
                .ToListAsync());
    }

    [Fact]
    public async Task Decrease_CreatesInformationalAlert()
    {
        await using var dbContext =
            CreateDbContext();

        var userId =
            Guid.NewGuid();

        var stream =
            CreateBillStream(
                userId);

        dbContext.BillStreams.Add(
            stream);

        dbContext.BillStatements.AddRange(
            CreateStatement(
                userId,
                stream.Id,
                new DateOnly(
                    2026,
                    6,
                    1),
                new DateOnly(
                    2026,
                    6,
                    30),
                104.99m),

            CreateStatement(
                userId,
                stream.Id,
                new DateOnly(
                    2026,
                    7,
                    1),
                new DateOnly(
                    2026,
                    7,
                    31),
                79.99m));

        await dbContext.SaveChangesAsync();

        var service =
            new BillStatementChangeDetectionService(
                dbContext,
                new BillStreamReadGateway(
                    dbContext),
                new BillStatementEvidenceAlertService(),
                new BillAlertReconciliationGateway(
                    dbContext));

        await service.ReconcileAsync(
            userId,
            stream.Id);

        await dbContext.SaveChangesAsync();

        var alert =
            Assert.Single(
                await dbContext.BillAlerts
                    .Where(
                        item =>
                            item.UserId ==
                                userId)
                    .ToListAsync());

        Assert.Equal(
            BillAlertType.BillDecrease,
            alert.AlertType);

        Assert.Equal(
            BillAlertSeverity.Info,
            alert.Severity);

        Assert.Contains(
            "$25.00/month",
            alert.Title);

        Assert.Contains(
            "$300.00/year",
            alert.Message);
    }

    [Fact]
    public async Task ObsoleteChange_RemovesItsAlert()
    {
        await using var dbContext =
            CreateDbContext();

        var userId =
            Guid.NewGuid();

        var stream =
            CreateBillStream(
                userId);

        var previous =
            CreateStatement(
                userId,
                stream.Id,
                new DateOnly(
                    2026,
                    6,
                    1),
                new DateOnly(
                    2026,
                    6,
                    30),
                79.99m);

        var current =
            CreateStatement(
                userId,
                stream.Id,
                new DateOnly(
                    2026,
                    7,
                    1),
                new DateOnly(
                    2026,
                    7,
                    31),
                104.99m);

        dbContext.BillStreams.Add(
            stream);

        dbContext.BillStatements.AddRange(
            previous,
            current);

        await dbContext.SaveChangesAsync();

        var service =
            new BillStatementChangeDetectionService(
                dbContext,
                new BillStreamReadGateway(
                    dbContext),
                new BillStatementEvidenceAlertService(),
                new BillAlertReconciliationGateway(
                    dbContext));

        await service.ReconcileAsync(
            userId,
            stream.Id);

        await dbContext.SaveChangesAsync();

        Assert.Single(
            await dbContext.BillChanges
                .ToListAsync());

        Assert.Single(
            await dbContext.BillAlerts
                .ToListAsync());

        /*
         * A corrected statement now says there was no price change.
         *
         * The old change and its alert are no longer valid.
         */
        current.TotalAmount =
            79.99m;

        current.UpdatedAtUtc =
            DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync();

        await service.ReconcileAsync(
            userId,
            stream.Id);

        await dbContext.SaveChangesAsync();

        Assert.Empty(
            await dbContext.BillChanges
                .ToListAsync());

        Assert.Empty(
            await dbContext.BillAlerts
                .ToListAsync());
    }

    [Theory]
    [InlineData(7, 7)]
    [InlineData(14, 14)]
    [InlineData(22, 31)]
    [InlineData(31, 22)]
    [InlineData(23, 31)]
    [InlineData(31, 23)]
    [InlineData(27, 31)]
    [InlineData(31, 27)]
    [InlineData(32, 31)]
    [InlineData(31, 32)]
    [InlineData(38, 31)]
    [InlineData(31, 38)]
    [InlineData(39, 31)]
    [InlineData(31, 39)]
    [InlineData(90, 91)]
    [InlineData(365, 365)]
    [InlineData(366, 365)]
    public async Task NonMonthlyPeriods_DoNotCreateMonthlyOrAnnualizedAlerts(
        int previousDays,
        int currentDays)
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var stream = CreateBillStream(userId);
        var previousStart = new DateOnly(2024, 1, 1);
        var currentStart = previousStart.AddDays(previousDays);
        var previous = CreateStatement(userId, stream.Id,
            previousStart, currentStart.AddDays(-1), 100m);
        var current = CreateStatement(userId, stream.Id,
            currentStart, currentStart.AddDays(currentDays - 1), 200m);
        dbContext.BillStreams.Add(stream);
        dbContext.BillStatements.AddRange(previous, current);
        await dbContext.SaveChangesAsync();

        var service = CreateChangeService(dbContext);
        var result = await service.ReconcileAsync(userId, stream.Id);
        await dbContext.SaveChangesAsync();

        Assert.Equal(0, result.CreatedCount);
        Assert.Empty(await dbContext.BillChanges.ToListAsync());
        Assert.Empty(await dbContext.BillAlerts.ToListAsync());
        Assert.Equal(2, await dbContext.BillStatements.CountAsync());
        Assert.Equal(200m, current.TotalAmount);
    }

    [Theory]
    [InlineData(2024, 1, 1)]
    [InlineData(2024, 1, 31)]
    [InlineData(2025, 1, 31)]
    [InlineData(2026, 6, 1)]
    [InlineData(2026, 12, 1)]
    public async Task CalendarMonthlyPeriods_PreserveDeterministicAnnualImpact(
        int year, int month, int day)
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var stream = CreateBillStream(userId);
        var previousStart = new DateOnly(year, month, day);
        var currentStart = previousStart.AddMonths(1);
        var nextStart = previousStart.AddMonths(2);
        dbContext.BillStreams.Add(stream);
        dbContext.BillStatements.AddRange(
            CreateStatement(userId, stream.Id, previousStart,
                currentStart.AddDays(-1), 100m),
            CreateStatement(userId, stream.Id, currentStart,
                nextStart.AddDays(-1), 125m));
        await dbContext.SaveChangesAsync();

        await CreateChangeService(dbContext).ReconcileAsync(userId, stream.Id);
        await dbContext.SaveChangesAsync();

        var change = Assert.Single(await dbContext.BillChanges.ToListAsync());
        Assert.Equal(25m, change.AmountDifference);
        Assert.Equal(300m, change.AnnualizedImpact);
        Assert.Contains("$300.00/year",
            Assert.Single(await dbContext.BillAlerts.ToListAsync()).Message);
    }

    [Theory]
    [InlineData(2026, 1, 1, 2026, 1, 28, 2026, 1, 29, 2026, 2, 25)]
    [InlineData(2026, 1, 1, 2026, 1, 31, 2027, 1, 1, 2027, 1, 31)]
    [InlineData(2026, 1, 1, 2026, 1, 31, 2026, 3, 1, 2026, 3, 31)]
    public async Task NonconsecutiveOrFourWeeklyCoverage_DoesNotAnnualize(
        int py, int pm, int pd, int pey, int pem, int ped,
        int cy, int cm, int cd, int cey, int cem, int ced)
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var stream = CreateBillStream(userId);
        dbContext.BillStreams.Add(stream);
        dbContext.BillStatements.AddRange(
            CreateStatement(userId, stream.Id,
                new DateOnly(py, pm, pd), new DateOnly(pey, pem, ped), 100m),
            CreateStatement(userId, stream.Id,
                new DateOnly(cy, cm, cd), new DateOnly(cey, cem, ced), 125m));
        await dbContext.SaveChangesAsync();

        await CreateChangeService(dbContext).ReconcileAsync(userId, stream.Id);
        await dbContext.SaveChangesAsync();

        Assert.Empty(await dbContext.BillChanges.ToListAsync());
        Assert.Empty(await dbContext.BillAlerts.ToListAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    public async Task OverlappingMonthlyPeriods_DoNotCreateRateChangeAlerts(int overlapDays)
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var stream = CreateBillStream(userId);
        var previousStart = new DateOnly(2026, 1, 1);
        var previousEnd = new DateOnly(2026, 1, 31);
        var currentStart = previousEnd.AddDays(-overlapDays);
        dbContext.BillStreams.Add(stream);
        dbContext.BillStatements.AddRange(
            CreateStatement(userId, stream.Id, previousStart, previousEnd, 100m),
            CreateStatement(userId, stream.Id, currentStart,
                currentStart.AddDays(30), 125m));
        await dbContext.SaveChangesAsync();

        await CreateChangeService(dbContext).ReconcileAsync(userId, stream.Id);
        await dbContext.SaveChangesAsync();

        Assert.Empty(await dbContext.BillChanges.ToListAsync());
        Assert.Empty(await dbContext.BillAlerts.ToListAsync());
    }

    [Fact]
    public async Task CorrectedNonMonthlyPeriod_RemovesPreviouslyGeneratedMonthlyAlert()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var stream = CreateBillStream(userId);
        var previous = CreateStatement(userId, stream.Id,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 100m);
        var current = CreateStatement(userId, stream.Id,
            new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28), 200m);
        dbContext.BillStreams.Add(stream);
        dbContext.BillStatements.AddRange(previous, current);
        await dbContext.SaveChangesAsync();
        var service = CreateChangeService(dbContext);

        await service.ReconcileAsync(userId, stream.Id);
        await dbContext.SaveChangesAsync();
        Assert.Single(await dbContext.BillChanges.ToListAsync());
        Assert.Single(await dbContext.BillAlerts.ToListAsync());

        current.PeriodEnd = new DateOnly(2026, 4, 30);
        await dbContext.SaveChangesAsync();
        var result = await service.ReconcileAsync(userId, stream.Id);
        await dbContext.SaveChangesAsync();

        Assert.Equal(1, result.RemovedCount);
        Assert.Empty(await dbContext.BillChanges.ToListAsync());
        Assert.Empty(await dbContext.BillAlerts.ToListAsync());
        Assert.Equal(2, await dbContext.BillStatements.CountAsync());
    }

    [Theory]
    [InlineData("EUR", "de-DE")]
    [InlineData("GBP", "fr-FR")]
    [InlineData("CAD", "en-CA")]
    public async Task NonUsdChangeAndEvidenceAlerts_RetainStatementCurrency(
        string currencyCode, string cultureName)
    {
        var originalCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture =
                System.Globalization.CultureInfo.GetCultureInfo(cultureName);
            await using var db = CreateDbContext();
            var userId = Guid.NewGuid();
            var stream = CreateBillStream(userId);
            var previous = CreateStatement(userId, stream.Id,
                new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), 80m);
            var current = CreateStatement(userId, stream.Id,
                new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), 105m);
            previous.CurrencyCode = current.CurrencyCode = currencyCode;
            db.BillStreams.Add(stream);
            db.BillStatements.AddRange(previous, current);
            db.BillLineItems.Add(new BillLineItemEntity
            {
                UserId = userId,
                BillStatementId = current.Id,
                Description = "Network fee",
                Amount = 25m,
                Category = "Fee"
            });
            await db.SaveChangesAsync();

            await CreateChangeService(db).ReconcileAsync(userId, stream.Id);
            await db.SaveChangesAsync();

            var alerts = await db.BillAlerts.ToListAsync();
            Assert.Equal(2, alerts.Count);
            foreach (var alert in alerts)
            {
                Assert.DoesNotContain("$", alert.Message);
                Assert.Contains(currencyCode + " 25.00", alert.Message);
            }
            var totalAlert = Assert.Single(alerts, item => item.AlertType == BillAlertType.BillIncrease);
            Assert.Contains(currencyCode + " 300.00/year", totalAlert.Message);
            Assert.DoesNotContain("$",
                Assert.Single(await db.BillChanges.ToListAsync()).Description);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static BillStatementChangeDetectionService CreateChangeService(
        FullWorthDbContext dbContext) =>
        new(dbContext,
            new BillStreamReadGateway(dbContext),
            new BillStatementEvidenceAlertService(),
            new BillAlertReconciliationGateway(dbContext));

    private static FullWorthDbContext
        CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<
                    FullWorthDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid()
                        .ToString(
                            "N"))
                .Options;

        return new FullWorthDbContext(
            options);
    }

    private static BillStreamEntity
        CreateBillStream(
            Guid userId)
    {
        return new BillStreamEntity
        {
            Id =
                Guid.NewGuid(),

            UserId =
                userId,

            ProviderName =
                "Midco",

            Category =
                BillCategory.Unknown,

            Source =
                BillStreamSource.Manual,

            IsActive =
                true,

            CreatedAtUtc =
                DateTimeOffset.UtcNow,

            UpdatedAtUtc =
                DateTimeOffset.UtcNow
        };
    }

    private static BillStatementEntity
        CreateStatement(
            Guid userId,
            Guid billStreamId,
            DateOnly periodStart,
            DateOnly periodEnd,
            decimal totalAmount)
    {
        return new BillStatementEntity
        {
            Id =
                Guid.NewGuid(),

            UserId =
                userId,

            BillStreamId =
                billStreamId,

            PeriodStart =
                periodStart,

            PeriodEnd =
                periodEnd,

            StatementDate =
                periodEnd.AddDays(
                    1),

            DueDate =
                periodEnd.AddDays(
                    21),

            TotalAmount =
                totalAmount,

            CurrencyCode =
                "USD",

            RetrievedAtUtc =
                DateTimeOffset.UtcNow,

            CreatedAtUtc =
                DateTimeOffset.UtcNow,

            UpdatedAtUtc =
                DateTimeOffset.UtcNow
        };
    }
}
