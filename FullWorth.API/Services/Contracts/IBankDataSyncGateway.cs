namespace FullWorth.API.Services.Contracts;

public interface IBankDataSyncGateway
{
    // Providers may isolate each connection's complete account/transaction
    // refresh. An incomplete pass must fail before bill discovery runs.
    async Task SyncAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await SyncAccountsAsync(userId, cancellationToken);
        await SyncTransactionsAsync(userId, cancellationToken);
    }

    Task SyncAccountsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task SyncTransactionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
