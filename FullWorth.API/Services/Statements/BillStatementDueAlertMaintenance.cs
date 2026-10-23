using FullWorth.API.Data;
using FullWorth.API.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.API.Services.Statements;

/// <summary>
/// Re-evaluates an existing PaymentDue slot using current UTC date and the
/// latest owner-scoped statement. It never invents payment state or due facts.
/// </summary>
public sealed class BillStatementDueAlertMaintenance(
    FullWorthDbContext db,
    IBillDueReminderMaintenanceReadGateway billReads,
    BillStatementPaymentDueAlertService dueAlerts)
{
    public async Task ReconcileOneAsync(
        Guid userId, Guid billStreamId, DateOnly today, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || billStreamId == Guid.Empty)
            throw new ArgumentException("A valid owner and bill identity are required.");

        var isActive = await billReads.IsOwnedActiveAsync(
            userId, billStreamId, cancellationToken);

        // A missing/foreign stream must not cause an alert write or leak data.
        if (isActive is null)
            return;

        var latest = await db.BillStatements.AsNoTracking()
            .Where(item => item.UserId == userId && item.BillStreamId == billStreamId)
            .OrderByDescending(item => item.PeriodEnd)
            .ThenByDescending(item => item.RetrievedAtUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.Id)
            .Select(item => new
            {
                item.DueDate, item.TotalAmount, item.CurrencyCode
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Inactive streams and missing/invalid historical evidence must not
        // continue to claim an upcoming payable obligation.
        DateOnly? dueDate = isActive.Value ? latest?.DueDate : null;
        decimal total = latest?.TotalAmount ?? 0m;
        string currency = latest?.CurrencyCode ?? "USD";

        await dueAlerts.ReconcileAsync(
            userId, billStreamId, dueDate, total, currency,
            today, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Sweep at UTC-date boundaries without requiring another provider upload.
/// Each owned bill is processed in a fresh scope to isolate EF tracking and
/// avoid one failure blocking every other customer's reminders.
/// </summary>
public sealed class BillStatementDueAlertMaintenanceWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<BillStatementDueAlertMaintenanceWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Payment reminder maintenance could not complete: {ExceptionType}.",
                    ex.GetType().Name);
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<BillDueReminderMaintenanceCandidate> candidates;
        using (var scope = scopeFactory.CreateScope())
        {
            var billReads = scope.ServiceProvider
                .GetRequiredService<IBillDueReminderMaintenanceReadGateway>();
            candidates = await billReads.GetCandidatesAsync(cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // A separate scoped DbContext for each alert prevents one failed
            // unit of work or its tracked data from contaminating another.
            using var scope = scopeFactory.CreateScope();
            try
            {
                var maintenance = scope.ServiceProvider
                    .GetRequiredService<BillStatementDueAlertMaintenance>();
                await maintenance.ReconcileOneAsync(
                    candidate.UserId, candidate.BillStreamId,
                    today, now, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // No provider, user, amount, document text, or account identifiers.
                logger.LogWarning(
                    "A payment reminder maintenance item failed: {ExceptionType}.",
                    ex.GetType().Name);
            }
        }
    }
}
