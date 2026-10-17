namespace FullWorth.API.Services.Contracts;

public sealed record BankTransactionBillStreamMetrics(
    Guid BillStreamId,
    decimal CurrentAmount,
    decimal PreviousAverage)
{
    public string? CurrencyCode { get; init; }

    // Timing evidence only; this does not establish a provider's contract or cause.
    public bool HasMonthlyCadenceEvidence { get; init; }
}

public interface IBankTransactionBillMetricsGateway
{
    Task<IReadOnlyDictionary<Guid, BankTransactionBillStreamMetrics>>
        GetMetricsAsync(
            Guid userId,
            IReadOnlyCollection<Guid> billStreamIds,
            CancellationToken cancellationToken = default);
}
