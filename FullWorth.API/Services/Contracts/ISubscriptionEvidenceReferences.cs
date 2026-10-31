namespace FullWorth.API.Services.Contracts;

public interface ISubscriptionTransactionEvidenceGateway
{
    Task<bool> IsOwnedPostedDebitAsync(Guid userId, Guid transactionId, CancellationToken cancellationToken);
}

public interface ISubscriptionDocumentEvidenceGateway
{
    Task<bool> IsOwnedBillDocumentAsync(Guid userId, Guid billStreamId, Guid uploadId, CancellationToken cancellationToken);
}
