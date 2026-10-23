using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.API.Services.Bills;

/// <summary>
/// Bills owns the alert and bill tables; Statements only receives explicit,
/// already ownership-validated identifiers for its daily due-date reconciliation.
/// </summary>
public sealed class BillPaymentDueRefreshCandidatesGateway(
    FullWorthDbContext dbContext) : IBillPaymentDueRefreshCandidatesGateway
{
    public async Task<IReadOnlyList<BillPaymentDueRefreshCandidate>> ListManagedAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.BillAlerts
            .AsNoTracking()
            .Where(alert =>
                alert.AlertType == BillAlertType.PaymentDue &&
                alert.BillStreamId.HasValue &&
                dbContext.BillStreams.Any(stream =>
                    stream.Id == alert.BillStreamId.Value &&
                    stream.UserId == alert.UserId))
            .Select(alert => new BillPaymentDueRefreshCandidate(
                alert.UserId, alert.BillStreamId!.Value))
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
