using FullWorth.API.Data;
using FullWorth.API.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.API.Services.Statements;

public sealed class BillStatementHistoryReadGateway(
    FullWorthDbContext dbContext)
    : IBillStatementHistoryReadGateway
{
    public async Task<BillStatementHistorySnapshot> GetAsync(
        Guid userId,
        Guid billStreamId,
        CancellationToken cancellationToken = default)
    {
        if (userId ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "User ID is required.",
                nameof(userId));
        }

        if (billStreamId ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Bill stream ID is required.",
                nameof(billStreamId));
        }

        var statements =
            await dbContext.BillStatements
                .AsNoTracking()
                .Where(
                    statement =>
                        statement.UserId ==
                            userId &&
                        statement.BillStreamId ==
                            billStreamId)
                .OrderByDescending(
                    statement =>
                        statement.PeriodEnd)
                .ThenByDescending(
                    statement =>
                        statement.StatementDate)
                .ThenByDescending(statement => statement.Id)
                .Select(
                    statement =>
                        new BillStatementHistoryReadRecord(
                            statement.Id,
                            statement.PeriodStart,
                            statement.PeriodEnd,
                            statement.StatementDate,
                            statement.DueDate,
                            statement.TotalAmount,
                            statement.CurrencyCode))
                .ToListAsync(
                    cancellationToken);

        // A single validated statement may have several duplicate uploads.
        // Resolve its most recent processed source through both owner and bill
        // boundaries; never return a raw storage key to Web clients.
        var statementIds = statements.Select(statement => statement.Id).ToArray();
        var sources = await dbContext.BillStatementUploads
            .AsNoTracking()
            .Where(upload => upload.UserId == userId &&
                upload.BillStreamId == billStreamId &&
                upload.Status == FullWorth.API.Data.Entities.BillStatementUploadStatus.Processed &&
                upload.BillStatementId.HasValue &&
                statementIds.Contains(upload.BillStatementId.Value))
            .OrderByDescending(upload => upload.CreatedAtUtc)
            .ThenByDescending(upload => upload.Id)
            .Select(upload => new
            {
                StatementId = upload.BillStatementId!.Value,
                UploadId = upload.Id
            })
            .ToListAsync(cancellationToken);

        var uploadByStatement = sources
            .GroupBy(source => source.StatementId)
            .ToDictionary(group => group.Key, group => group.First().UploadId);

        var linkedStatements = statements
            .Select(statement => statement with
            {
                SourceUploadId = uploadByStatement.TryGetValue(statement.Id, out var uploadId)
                    ? uploadId : null
            })
            .ToList();

        var changes =
            await dbContext.BillChanges
                .AsNoTracking()
                .Where(
                    change =>
                        change.UserId ==
                            userId &&
                        change.BillStreamId ==
                            billStreamId)
                .OrderByDescending(
                    change =>
                        change.DetectedAtUtc)
                .ThenByDescending(change => change.Id)
                .Select(
                    change =>
                        new BillChangeHistoryReadRecord(
                            change.Id,
                            change.PreviousStatementId,
                            change.CurrentStatementId,
                            change.ChangeType.ToString(),
                            change.Confidence.ToString(),
                            change.Description,
                            change.PreviousAmount,
                            change.CurrentAmount,
                            change.AmountDifference,
                            change.AnnualizedImpact,
                            change.IsAcknowledged,
                            change.DetectedAtUtc))
                .ToListAsync(
                    cancellationToken);

        var ownedPeriods = linkedStatements.ToDictionary(statement => statement.Id);
        var qualifiedChanges = changes.Select(change => change with
        {
            HasAnnualizedImpactEvidence = CanAnnualize(change, ownedPeriods)
        }).ToList();
        return new BillStatementHistorySnapshot(linkedStatements, qualifiedChanges);
    }

    private static bool CanAnnualize(
        BillChangeHistoryReadRecord change,
        IReadOnlyDictionary<Guid, BillStatementHistoryReadRecord> statements)
    {
        if (change.PreviousStatementId is not { } previousId ||
            previousId == Guid.Empty || change.CurrentStatementId == Guid.Empty ||
            previousId == change.CurrentStatementId ||
            !statements.TryGetValue(previousId, out var previous) ||
            !statements.TryGetValue(change.CurrentStatementId, out var current) ||
            !FullWorth.Core.Formatting.FinancialAmountFormatter.TryNormalizeCurrency(
                previous.CurrencyCode, out var previousCurrency) ||
            !FullWorth.Core.Formatting.FinancialAmountFormatter.TryNormalizeCurrency(
                current.CurrencyCode, out var currentCurrency) ||
            previousCurrency != currentCurrency ||
            !FullWorth.Core.Services.MonthlyStatementPeriodPolicy.CanCompare(
                previous.PeriodStart, previous.PeriodEnd, current.PeriodStart, current.PeriodEnd))
            return false;

        try
        {
            return change.AmountDifference == decimal.Round(
                change.CurrentAmount - change.PreviousAmount, 2, MidpointRounding.AwayFromZero) &&
                change.AnnualizedImpact == decimal.Round(
                    change.AmountDifference * 12m, 2, MidpointRounding.AwayFromZero);
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
