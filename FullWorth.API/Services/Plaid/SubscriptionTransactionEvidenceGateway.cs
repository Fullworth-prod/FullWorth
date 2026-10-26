using FullWorth.API.Data;
using FullWorth.API.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.API.Services.Plaid;

public sealed class SubscriptionTransactionEvidenceGateway(FullWorthDbContext dbContext)
    : ISubscriptionTransactionEvidenceGateway
{
    public Task<bool> IsOwnedPostedDebitAsync(Guid userId, Guid transactionId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || transactionId == Guid.Empty) return Task.FromResult(false);
        return dbContext.BankTransactions.AsNoTracking().AnyAsync(t =>
            t.UserId == userId && t.Id == transactionId && !t.IsRemoved &&
            !t.IsPending && t.Amount > 0m, cancellationToken);
    }
}
