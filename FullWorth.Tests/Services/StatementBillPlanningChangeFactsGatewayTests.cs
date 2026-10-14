using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Statements;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.Tests.Services;

public sealed class StatementBillPlanningChangeFactsGatewayTests
{
    [Fact]
    public async Task GetLatestConfirmedAsync_ReturnsLatestOwnedConfirmedTotalChange()
    {
        var options =
            new DbContextOptionsBuilder<FullWorthDbContext>()
                .UseInMemoryDatabase(
                    $"planning-change-facts-{Guid.NewGuid():N}")
                .Options;

        var userId =
            Guid.NewGuid();

        var otherUserId =
            Guid.NewGuid();

        var billStreamId =
            Guid.NewGuid();

        var oldStatement =
            Statement(
                userId,
                billStreamId,
                new DateOnly(
                    2026,
                    8,
                    31),
                80m);

        var latestStatement =
            Statement(
                userId,
                billStreamId,
                new DateOnly(
                    2026,
                    9,
                    30),
                95m);

        var foreignStatement =
            Statement(
                otherUserId,
                billStreamId,
                new DateOnly(
                    2026,
                    10,
                    31),
                999m);

        var expectedChange =
            Change(
                userId,
                billStreamId,
                oldStatement.Id,
                latestStatement.Id,
                BillChangeType.TotalIncrease,
                BillChangeConfidence.Confirmed,
                80m,
                95m,
                15m,
                180m,
                "Provider statements confirm a $15.00 monthly increase.");

        await using (var seed =
                     new FullWorthDbContext(
                         options))
        {
            seed.BillStatements.AddRange(
                oldStatement,
                latestStatement,
                foreignStatement);

            seed.BillChanges.AddRange(
                Change(
                    userId,
                    billStreamId,
                    oldStatement.Id,
                    oldStatement.Id,
                    BillChangeType.TotalDecrease,
                    BillChangeConfidence.Confirmed,
                    90m,
                    80m,
                    -10m,
                    -120m,
                    "Older change"),
                expectedChange,
                Change(
                    userId,
                    billStreamId,
                    oldStatement.Id,
                    latestStatement.Id,
                    BillChangeType.TotalIncrease,
                    BillChangeConfidence.Possible,
                    80m,
                    999m,
                    919m,
                    11028m,
                    "Unconfirmed change"),
                Change(
                    otherUserId,
                    billStreamId,
                    foreignStatement.Id,
                    foreignStatement.Id,
                    BillChangeType.TotalIncrease,
                    BillChangeConfidence.Confirmed,
                    1m,
                    999m,
                    998m,
                    11976m,
                    "Foreign change"));

            await seed.SaveChangesAsync();
        }

        await using var dbContext =
            new FullWorthDbContext(
                options);

        var gateway =
            new StatementBillPlanningChangeFactsGateway(
                dbContext);

        var result =
            await gateway.GetLatestConfirmedAsync(
                userId,
                [billStreamId]);

        var fact =
            Assert.Single(
                result);

        Assert.Equal(
            billStreamId,
            fact.Key);

        Assert.Equal(
            expectedChange.Id,
            fact.Value.ChangeId);

        Assert.Equal(
            latestStatement.Id,
            fact.Value.CurrentStatementId);

        Assert.Equal(
            new DateOnly(
                2026,
                9,
                30),
            fact.Value.CurrentPeriodEnd);

        Assert.Equal(
            15m,
            fact.Value.AmountDifference);

        Assert.Equal(
            180m,
            fact.Value.AnnualizedImpact);

        Assert.Equal(
            expectedChange.Description,
            fact.Value.Description);
    }

    [Fact]
    public async Task GetLatestConfirmedAsync_IgnoresLineItemAndUnconfirmedChanges()
    {
        var options =
            new DbContextOptionsBuilder<FullWorthDbContext>()
                .UseInMemoryDatabase(
                    $"planning-change-facts-filter-{Guid.NewGuid():N}")
                .Options;

        var userId =
            Guid.NewGuid();

        var billStreamId =
            Guid.NewGuid();

        var statement =
            Statement(
                userId,
                billStreamId,
                new DateOnly(
                    2026,
                    9,
                    30),
                95m);

        await using (var seed =
                     new FullWorthDbContext(
                         options))
        {
            seed.BillStatements.Add(
                statement);

            seed.BillChanges.AddRange(
                Change(
                    userId,
                    billStreamId,
                    statement.Id,
                    statement.Id,
                    BillChangeType.LineItemAdded,
                    BillChangeConfidence.Confirmed,
                    0m,
                    5m,
                    5m,
                    60m,
                    "New fee"),
                Change(
                    userId,
                    billStreamId,
                    statement.Id,
                    statement.Id,
                    BillChangeType.TotalIncrease,
                    BillChangeConfidence.StrongInference,
                    80m,
                    95m,
                    15m,
                    180m,
                    "Not confirmed"));

            await seed.SaveChangesAsync();
        }

        await using var dbContext =
            new FullWorthDbContext(
                options);

        var gateway =
            new StatementBillPlanningChangeFactsGateway(
                dbContext);

        var result =
            await gateway.GetLatestConfirmedAsync(
                userId,
                [billStreamId]);

        Assert.Empty(
            result);
    }

    [Fact]
    public async Task GetLatestConfirmedAsync_OtherUserGetsNoFacts()
    {
        var options =
            new DbContextOptionsBuilder<FullWorthDbContext>()
                .UseInMemoryDatabase(
                    $"planning-change-facts-owner-{Guid.NewGuid():N}")
                .Options;

        var ownerUserId =
            Guid.NewGuid();

        var billStreamId =
            Guid.NewGuid();

        var statement =
            Statement(
                ownerUserId,
                billStreamId,
                new DateOnly(
                    2026,
                    9,
                    30),
                95m);

        await using (var seed =
                     new FullWorthDbContext(
                         options))
        {
            seed.BillStatements.Add(
                statement);

            seed.BillChanges.Add(
                Change(
                    ownerUserId,
                    billStreamId,
                    statement.Id,
                    statement.Id,
                    BillChangeType.TotalIncrease,
                    BillChangeConfidence.Confirmed,
                    80m,
                    95m,
                    15m,
                    180m,
                    "Owned change"));

            await seed.SaveChangesAsync();
        }

        await using var dbContext =
            new FullWorthDbContext(
                options);

        var gateway =
            new StatementBillPlanningChangeFactsGateway(
                dbContext);

        var result =
            await gateway.GetLatestConfirmedAsync(
                Guid.NewGuid(),
                [billStreamId]);

        Assert.Empty(
            result);
    }

    [Theory]
    [InlineData("foreign_previous")]
    [InlineData("different_stream")]
    [InlineData("missing_previous")]
    [InlineData("self_reference")]
    [InlineData("stale_previous_total")]
    [InlineData("stale_current_total")]
    [InlineData("wrong_delta")]
    [InlineData("wrong_annual")]
    [InlineData("wrong_direction")]
    [InlineData("different_currency")]
    [InlineData("invalid_currency")]
    [InlineData("quarterly_period")]
    [InlineData("four_weekly")]
    [InlineData("missing_month")]
    public async Task GetLatestConfirmedAsync_RejectsUntrustworthySavedChange(string defect)
    {
        var options = new DbContextOptionsBuilder<FullWorthDbContext>()
            .UseInMemoryDatabase($"change-evidence-{Guid.NewGuid():N}").Options;
        var userId = Guid.NewGuid();
        var streamId = Guid.NewGuid();
        var previous = Statement(userId, streamId, new DateOnly(2026, 8, 31), 80m);
        var current = Statement(userId, streamId, new DateOnly(2026, 9, 30), 95m);
        var change = Change(userId, streamId, previous.Id, current.Id,
            BillChangeType.TotalIncrease, BillChangeConfidence.Confirmed,
            80m, 95m, 15m, 180m, "Statement evidence");

        switch (defect)
        {
            case "foreign_previous": previous.UserId = Guid.NewGuid(); break;
            case "different_stream": previous.BillStreamId = Guid.NewGuid(); break;
            case "missing_previous": change.PreviousStatementId = Guid.NewGuid(); break;
            case "self_reference": change.PreviousStatementId = current.Id; break;
            case "stale_previous_total": previous.TotalAmount = 70m; break;
            case "stale_current_total": current.TotalAmount = 100m; break;
            case "wrong_delta": change.AmountDifference = 150m; break;
            case "wrong_annual": change.AnnualizedImpact = 1800m; break;
            case "wrong_direction": change.ChangeType = BillChangeType.TotalDecrease; break;
            case "different_currency": previous.CurrencyCode = "EUR"; break;
            case "invalid_currency": previous.CurrencyCode = current.CurrencyCode = "12X"; break;
            case "quarterly_period": previous.PeriodStart = new DateOnly(2026, 6, 1); break;
            case "four_weekly":
                previous.PeriodStart = new DateOnly(2026, 1, 1);
                previous.PeriodEnd = new DateOnly(2026, 1, 28);
                current.PeriodStart = new DateOnly(2026, 1, 29);
                current.PeriodEnd = new DateOnly(2026, 2, 25);
                break;
            case "missing_month":
                previous.PeriodStart = new DateOnly(2026, 7, 1);
                previous.PeriodEnd = new DateOnly(2026, 7, 31);
                break;
        }

        await using var db = new FullWorthDbContext(options);
        db.BillStatements.AddRange(previous, current);
        db.BillChanges.Add(change);
        await db.SaveChangesAsync();

        var result = await new StatementBillPlanningChangeFactsGateway(db)
            .GetLatestConfirmedAsync(userId, [streamId]);

        Assert.Empty(result);
        // Read filtering must preserve the original evidence and stored history.
        Assert.Equal(2, await db.BillStatements.CountAsync());
        Assert.Single(await db.BillChanges.ToListAsync());
    }

    private static BillStatementEntity Statement(
        Guid userId,
        Guid billStreamId,
        DateOnly periodEnd,
        decimal totalAmount)
    {
        var now =
            DateTimeOffset.UtcNow;

        return new BillStatementEntity
        {
            Id =
                Guid.NewGuid(),
            UserId =
                userId,
            BillStreamId =
                billStreamId,
            PeriodStart =
                new DateOnly(periodEnd.Year, periodEnd.Month, 1),
            PeriodEnd =
                periodEnd,
            StatementDate =
                periodEnd,
            DueDate =
                periodEnd.AddDays(
                    21),
            TotalAmount =
                totalAmount,
            CurrencyCode =
                "USD",
            RetrievedAtUtc =
                now,
            CreatedAtUtc =
                now,
            UpdatedAtUtc =
                now
        };
    }

    private static BillChangeEntity Change(
        Guid userId,
        Guid billStreamId,
        Guid previousStatementId,
        Guid currentStatementId,
        BillChangeType changeType,
        BillChangeConfidence confidence,
        decimal previousAmount,
        decimal currentAmount,
        decimal difference,
        decimal annualizedImpact,
        string description)
    {
        var now =
            DateTimeOffset.UtcNow;

        return new BillChangeEntity
        {
            Id =
                Guid.NewGuid(),
            UserId =
                userId,
            BillStreamId =
                billStreamId,
            PreviousStatementId =
                previousStatementId,
            CurrentStatementId =
                currentStatementId,
            ChangeType =
                changeType,
            Confidence =
                confidence,
            Description =
                description,
            PreviousAmount =
                previousAmount,
            CurrentAmount =
                currentAmount,
            AmountDifference =
                difference,
            AnnualizedImpact =
                annualizedImpact,
            IsAcknowledged =
                false,
            DetectedAtUtc =
                now,
            CreatedAtUtc =
                now,
            UpdatedAtUtc =
                now
        };
    }
}

