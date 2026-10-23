using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.API.Services.Statements;

/// <summary>
/// Reconciles only previously-created statement PaymentDue alert slots.
/// No bank transaction or posted payment is inferred from a due date.
/// The caller supplies a UTC date for repeatable daily evaluation.
/// </summary>
public sealed class BillStatementDueReminderRefreshService(
    FullWorthDbContext dbContext,
    BillStatementPaymentDueAlertService paymentDueAlerts,
    IBillAlertReconciliationGateway alerts)
{
    public async Task<int> RefreshAsync(
        DateOnly today,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        // Each alert's owner is retained in the key, with a secondary owned
        // BillStream check. No statement storage paths or raw text are read.
        var keys = await dbContext.BillAlerts
            .AsNoTracking()
            .Where(alert => alert.AlertType == BillAlertType.PaymentDue &&
                dbContext.BillStreams.Any(stream =>
                    stream.Id == alert.BillStreamId &&
                    stream.UserId == alert.UserId))
            .Select(alert => new { alert.UserId, alert.BillStreamId })
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var latest = await dbContext.BillStatements
                .AsNoTracking()
                .Where(statement => statement.UserId == key.UserId &&
                    statement.BillStreamId == key.BillStreamId)
                .OrderByDescending(statement => statement.PeriodEnd)
                .ThenByDescending(statement => statement.RetrievedAtUtc)
                .ThenByDescending(statement => statement.CreatedAtUtc)
                .ThenByDescending(statement => statement.Id)
                .Select(statement => new
                {
                    statement.DueDate,
                    statement.TotalAmount,
                    statement.CurrencyCode
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (latest is null)
            {
                await alerts.StageReconciliationAsync(
                    key.UserId,
                    key.BillStreamId,
                    [new BillAlertReconciliationScope(
                        null, [BillAlertContractType.PaymentDue], [],
                        BillAlertReconciliationMode.SingleManagedSlot)],
                    [], now, cancellationToken);
            }
            else
            {
                await paymentDueAlerts.ReconcileAsync(
                    key.UserId, key.BillStreamId, latest.DueDate,
                    latest.TotalAmount, latest.CurrencyCode, today, now,
                    cancellationToken);
            }

            // Persist each completed owner's reconciliation independently;
            // cancellation or a later error must not roll back earlier users.
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return keys.Count;
    }
}
