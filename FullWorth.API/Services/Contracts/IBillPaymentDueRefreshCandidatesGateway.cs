namespace FullWorth.API.Services.Contracts;

/// <summary>An alert owner and bill identity that Bills has validated.</summary>
public sealed record BillPaymentDueRefreshCandidate(Guid UserId, Guid BillStreamId);

public interface IBillPaymentDueRefreshCandidatesGateway
{
    Task<IReadOnlyList<BillPaymentDueRefreshCandidate>> ListManagedAsync(
        CancellationToken cancellationToken = default);
}
