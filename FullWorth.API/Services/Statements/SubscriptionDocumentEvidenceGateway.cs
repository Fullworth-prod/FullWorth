using FullWorth.API.Data;
using FullWorth.API.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.API.Services.Statements;

public sealed class SubscriptionDocumentEvidenceGateway(FullWorthDbContext dbContext)
    : ISubscriptionDocumentEvidenceGateway
{
    public Task<bool> IsOwnedBillDocumentAsync(Guid userId, Guid billStreamId, Guid uploadId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || billStreamId == Guid.Empty || uploadId == Guid.Empty)
            return Task.FromResult(false);
        // A stored document is supporting user-reviewed evidence, not a live
        // merchant assertion or proof that machine extraction verified renewal.
        return dbContext.BillStatementUploads.AsNoTracking().AnyAsync(u =>
            u.UserId == userId && u.Id == uploadId && u.BillStreamId == billStreamId,
            cancellationToken);
    }
}
