using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Contracts;
using FullWorth.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.API.Services.Statements;

public sealed class StatementBillPlanningChangeFactsGateway(
    FullWorthDbContext dbContext)
    : IBillPlanningChangeFactsGateway
{
    private const int MaximumBillStreamsPerRequest =
        500;

    public async Task<IReadOnlyDictionary<Guid, BillPlanningChangeFact>>
        GetLatestConfirmedAsync(
            Guid userId,
            IReadOnlyCollection<Guid> billStreamIds,
            CancellationToken cancellationToken = default)
    {
        if (userId ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "User ID is required.",
                nameof(userId));
        }

        ArgumentNullException.ThrowIfNull(
            billStreamIds);

        if (billStreamIds.Count ==
            0)
        {
            return new Dictionary<Guid, BillPlanningChangeFact>();
        }

        var ids =
            billStreamIds
                .Distinct()
                .ToArray();

        if (ids.Any(
                id =>
                    id == Guid.Empty))
        {
            throw new ArgumentException(
                "Bill stream IDs must not be empty.",
                nameof(billStreamIds));
        }

        if (ids.Length >
            MaximumBillStreamsPerRequest)
        {
            throw new ArgumentOutOfRangeException(
                nameof(billStreamIds),
                $"At most {MaximumBillStreamsPerRequest} bill streams may be requested at once.");
        }

        var candidates =
            await (
                from change in
                    dbContext.BillChanges.AsNoTracking()
                join statement in
                    dbContext.BillStatements.AsNoTracking()
                    on new
                    {
                        change.CurrentStatementId,
                        change.UserId
                    }
                    equals new
                    {
                        CurrentStatementId =
                            statement.Id,
                        statement.UserId
                    }
                join previous in dbContext.BillStatements.AsNoTracking()
                    on new { StatementId = change.PreviousStatementId, change.UserId }
                    equals new { StatementId = (Guid?)previous.Id, previous.UserId }
                where
                    previous.BillStreamId == change.BillStreamId &&
                    previous.Id != statement.Id &&
                    previous.TotalAmount == change.PreviousAmount &&
                    statement.TotalAmount == change.CurrentAmount &&
                    previous.TotalAmount >= 0m &&
                    statement.TotalAmount >= 0m &&
                    change.AmountDifference == statement.TotalAmount - previous.TotalAmount &&
                    change.AnnualizedImpact == change.AmountDifference * 12m &&
                    (
                        (change.ChangeType == BillChangeType.TotalIncrease && change.AmountDifference > 0m) ||
                        (change.ChangeType == BillChangeType.TotalDecrease && change.AmountDifference < 0m)
                    ) &&
                    change.UserId ==
                        userId &&
                    ids.Contains(
                        change.BillStreamId) &&
                    statement.BillStreamId ==
                        change.BillStreamId &&
                    change.Confidence ==
                        BillChangeConfidence.Confirmed &&
                    (
                        change.ChangeType ==
                            BillChangeType.TotalIncrease ||
                        change.ChangeType ==
                            BillChangeType.TotalDecrease
                    )
                orderby
                    change.BillStreamId,
                    statement.PeriodEnd descending,
                    change.DetectedAtUtc descending,
                    change.Id descending
                select new
                {
                    PreviousPeriodStart = previous.PeriodStart,
                    PreviousPeriodEnd = previous.PeriodEnd,
                    CurrentPeriodStart = statement.PeriodStart,
                    CurrentPeriodEnd = statement.PeriodEnd,
                    PreviousCurrency = previous.CurrencyCode,
                    CurrentCurrency = statement.CurrencyCode,
                    Fact = new BillPlanningChangeFact(
                    change.BillStreamId,
                    change.Id,
                    change.PreviousStatementId,
                    change.CurrentStatementId,
                    change.PreviousAmount,
                    change.CurrentAmount,
                    change.AmountDifference,
                    change.AnnualizedImpact,
                    change.Description,
                    statement.PeriodEnd)
                }
            )
            .ToListAsync(
                cancellationToken);

        return candidates
            .Where(candidate =>
                IsSupportedCurrency(candidate.CurrentCurrency) &&
                string.Equals(candidate.PreviousCurrency, candidate.CurrentCurrency,
                    StringComparison.Ordinal) &&
                MonthlyStatementPeriodPolicy.CanCompare(
                    candidate.PreviousPeriodStart, candidate.PreviousPeriodEnd,
                    candidate.CurrentPeriodStart, candidate.CurrentPeriodEnd))
            .Select(candidate => candidate.Fact)
            .GroupBy(
                candidate =>
                    candidate.BillStreamId)
            .ToDictionary(
                group =>
                    group.Key,
                group =>
                    group.First());
    }
    private static bool IsSupportedCurrency(string? currency) =>
        currency is { Length: 3 } &&
        currency.All(character => character is >= 'A' and <= 'Z');
}
