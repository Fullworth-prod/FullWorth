using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Bills;
using FullWorth.API.Services.Statements;
using FullWorth.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.Tests.Services;

public sealed class BillStatementPaymentDueAlertServiceTests
{
    [Fact]
    public async Task ExplicitFutureDueDate_CreatesAlert()
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

        await dbContext.SaveChangesAsync();

        var service =
            new BillStatementPaymentDueAlertService(
                new BillStreamReadGateway(
                    dbContext),
                new BillAlertReconciliationGateway(
                    dbContext));

        await service.ReconcileAsync(
            userId,
            stream.Id,
            new DateOnly(
                2026,
                9,
                15),
            94.99m,
            "USD",
            new DateOnly(
                2026,
                8,
                27),
            DateTimeOffset.UtcNow);

        await dbContext.SaveChangesAsync();

        var alert =
            Assert.Single(
                await dbContext.BillAlerts
                    .ToListAsync());

        Assert.Equal(
            BillAlertType.PaymentDue,
            alert.AlertType);

        Assert.Equal(
            BillAlertSeverity.Info,
            alert.Severity);

        Assert.Contains(
            "$94.99",
            alert.Message);

        Assert.Contains(
            "Sep 15, 2026",
            alert.Title);

        Assert.Contains(
            "directly on the provider statement",
            alert.Message);
    }

    [Fact]
    public async Task SameDueEvent_IsUpdatedInsteadOfDuplicated()
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

        await dbContext.SaveChangesAsync();

        var service =
            new BillStatementPaymentDueAlertService(
                new BillStreamReadGateway(
                    dbContext),
                new BillAlertReconciliationGateway(
                    dbContext));

        var dueDate =
            new DateOnly(
                2026,
                9,
                1);

        var today =
            new DateOnly(
                2026,
                8,
                27);

        await service.ReconcileAsync(
            userId,
            stream.Id,
            dueDate,
            79.99m,
            "USD",
            today,
            DateTimeOffset.UtcNow);

        await dbContext.SaveChangesAsync();

        var original =
            Assert.Single(
                await dbContext.BillAlerts
                    .ToListAsync());

        original.IsRead =
            true;

        original.IsDismissed =
            true;

        await dbContext.SaveChangesAsync();

        await service.ReconcileAsync(
            userId,
            stream.Id,
            dueDate,
            104.99m,
            "USD",
            today,
            DateTimeOffset.UtcNow);

        await dbContext.SaveChangesAsync();

        var updated =
            Assert.Single(
                await dbContext.BillAlerts
                    .ToListAsync());

        Assert.Equal(
            original.Id,
            updated.Id);

        Assert.Contains(
            "$104.99",
            updated.Message);

        Assert.False(
            updated.IsRead);

        Assert.False(
            updated.IsDismissed);
    }

    [Fact]
    public async Task MissingOrPastDueDate_DoesNotCreateAlert()
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

        await dbContext.SaveChangesAsync();

        var service =
            new BillStatementPaymentDueAlertService(
                new BillStreamReadGateway(
                    dbContext),
                new BillAlertReconciliationGateway(
                    dbContext));

        var today =
            new DateOnly(
                2026,
                8,
                27);

        await service.ReconcileAsync(
            userId,
            stream.Id,
            null,
            94.99m,
            "USD",
            today,
            DateTimeOffset.UtcNow);

        await service.ReconcileAsync(
            userId,
            stream.Id,
            new DateOnly(
                2026,
                8,
                20),
            94.99m,
            "USD",
            today,
            DateTimeOffset.UtcNow);

        await dbContext.SaveChangesAsync();

        Assert.Empty(
            await dbContext.BillAlerts
                .ToListAsync());
    }

    [Fact]
    public async Task OtherUsersBillStream_IsRejected()
    {
        await using var dbContext =
            CreateDbContext();

        var ownerUserId =
            Guid.NewGuid();

        var otherUserId =
            Guid.NewGuid();

        var stream =
            CreateBillStream(
                ownerUserId);

        dbContext.BillStreams.Add(
            stream);

        await dbContext.SaveChangesAsync();

        var service =
            new BillStatementPaymentDueAlertService(
                new BillStreamReadGateway(
                    dbContext),
                new BillAlertReconciliationGateway(
                    dbContext));

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () =>
                service.ReconcileAsync(
                    otherUserId,
                    stream.Id,
                    new DateOnly(
                        2026,
                        9,
                        15),
                    94.99m,
                    "USD",
                    new DateOnly(
                        2026,
                        8,
                        27),
                    DateTimeOffset.UtcNow));

        Assert.Empty(
            await dbContext.BillAlerts
                .ToListAsync());
    }

    [Fact]
    public async Task LongProviderName_SameDueEvent_DoesNotDuplicate()
    {
        await using var dbContext =
            CreateDbContext();

        var userId =
            Guid.NewGuid();

        var stream =
            CreateBillStream(
                userId);

        stream.ProviderName =
            new string(
                'P',
                400);

        dbContext.BillStreams.Add(
            stream);

        await dbContext.SaveChangesAsync();

        var service =
            new BillStatementPaymentDueAlertService(
                new BillStreamReadGateway(
                    dbContext),
                new BillAlertReconciliationGateway(
                    dbContext));

        var dueDate =
            new DateOnly(
                2026,
                9,
                15);

        var today =
            new DateOnly(
                2026,
                8,
                27);

        await service.ReconcileAsync(
            userId,
            stream.Id,
            dueDate,
            94.99m,
            "USD",
            today,
            DateTimeOffset.UtcNow);

        await dbContext.SaveChangesAsync();

        await service.ReconcileAsync(
            userId,
            stream.Id,
            dueDate,
            94.99m,
            "USD",
            today,
            DateTimeOffset.UtcNow);

        await dbContext.SaveChangesAsync();

        Assert.Single(
            await dbContext.BillAlerts
                .Where(
                    alert =>
                        alert.UserId ==
                            userId &&
                        alert.BillStreamId ==
                            stream.Id &&
                        alert.AlertType ==
                            BillAlertType.PaymentDue)
                .ToListAsync());
    }

    [Fact]
    public async Task CorrectedDueDate_ReplacesTheManagedReminder()
    {
        await using var db = CreateDbContext();
        var user = Guid.NewGuid();
        var stream = CreateBillStream(user);
        db.BillStreams.Add(stream);
        await db.SaveChangesAsync();
        var service = new BillStatementPaymentDueAlertService(
            new BillStreamReadGateway(db), new BillAlertReconciliationGateway(db));
        var today = new DateOnly(2026, 8, 1);
        await service.ReconcileAsync(user, stream.Id, today.AddDays(10),
            100m, "USD", today, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        var original = Assert.Single(await db.BillAlerts.ToListAsync());
        original.IsRead = original.IsDismissed = true;
        await db.SaveChangesAsync();

        await service.ReconcileAsync(user, stream.Id, today.AddDays(20),
            125m, "USD", today, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        var updated = Assert.Single(await db.BillAlerts.ToListAsync());
        Assert.Equal(original.Id, updated.Id);
        Assert.Contains("Aug 21, 2026", updated.Title);
        Assert.Contains("$125.00", updated.Message);
        Assert.False(updated.IsRead);
        Assert.False(updated.IsDismissed);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("past")]
    [InlineData("zero")]
    public async Task NoCurrentDueObligation_ClearsOnlyOwnedPaymentReminder(string reason)
    {
        await using var db = CreateDbContext();
        var user = Guid.NewGuid();
        var stream = CreateBillStream(user);
        db.BillStreams.Add(stream);
        await db.SaveChangesAsync();
        var service = new BillStatementPaymentDueAlertService(
            new BillStreamReadGateway(db), new BillAlertReconciliationGateway(db));
        var today = new DateOnly(2026, 8, 1);
        await service.ReconcileAsync(user, stream.Id, today.AddDays(10),
            100m, "USD", today, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        var untouched = new[]
        {
            new BillAlertEntity { UserId = Guid.NewGuid(), BillStreamId = stream.Id,
                AlertType = BillAlertType.PaymentDue, Title = "Foreign reminder", Message = "Foreign" },
            new BillAlertEntity { UserId = user, BillStreamId = stream.Id,
                AlertType = BillAlertType.NewFee, Title = "Owned fee", Message = "Keep" }
        };
        db.BillAlerts.AddRange(untouched);
        await db.SaveChangesAsync();

        DateOnly? due = reason == "missing" ? null :
            reason == "past" ? today.AddDays(-1) : today.AddDays(10);
        await service.ReconcileAsync(user, stream.Id, due,
            reason == "zero" ? 0m : 100m, "USD", today, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        var remaining = await db.BillAlerts.ToListAsync();
        Assert.Equal(2, remaining.Count);
        Assert.All(untouched, item => Assert.Contains(remaining, actual => actual.Id == item.Id));
    }

    [Fact]
    public async Task ForeignStream_CannotClearRemindersWithMissingDueDate()
    {
        await using var db = CreateDbContext();
        var owner = Guid.NewGuid();
        var stream = CreateBillStream(owner);
        db.BillStreams.Add(stream);
        await db.SaveChangesAsync();
        var service = new BillStatementPaymentDueAlertService(
            new BillStreamReadGateway(db), new BillAlertReconciliationGateway(db));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReconcileAsync(Guid.NewGuid(), stream.Id, null, 100m, "USD",
                new DateOnly(2026, 8, 1), DateTimeOffset.UtcNow));
    }

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
}
