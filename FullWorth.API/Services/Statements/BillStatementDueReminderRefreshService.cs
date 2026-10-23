using FullWorth.API.Data;
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
    IBillAlertReconciliationGateway alerts,
    IBillPaymentDueRefreshCandidatesGateway candidates)
{
    public async Task<int> RefreshAsync(
        DateOnly today,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        // Only Bills can enumerate managed alerts and validate that their
        // bill ownership still exists. Statements receives IDs, not Bills data.
        var keys = await candidates.ListManagedAsync(cancellationToken);

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
