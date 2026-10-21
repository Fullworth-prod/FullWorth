using System.Runtime.ExceptionServices;
using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.API.Services.Plaid;

public sealed class PlaidBankDataSyncGateway(
    PlaidConnectionSyncCoordinator coordinator,
    FullWorthDbContext dbContext,
    IServiceScopeFactory scopeFactory)
    : IBankDataSyncGateway
{
    public async Task SyncAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A valid user ID is required.", nameof(userId));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Read IDs only in the caller's context. Staged writes from a failed
        // connection must never be committed by another bank or health alerts.
        var connectionIds = await dbContext.BankConnections
            .AsNoTracking()
            .Where(connection =>
                connection.UserId == userId &&
                connection.Status == BankConnectionStatus.Active &&
                connection.ProtectedPlaidAccessToken != null &&
                connection.ProtectedPlaidAccessToken != string.Empty)
            .OrderBy(connection => connection.Id)
            .Select(connection => connection.Id)
            .ToListAsync(cancellationToken);

        ExceptionDispatchInfo? firstFailure = null;

        foreach (var connectionId in connectionIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var scope = scopeFactory.CreateAsyncScope();
            var connectionCoordinator = scope.ServiceProvider
                .GetRequiredService<PlaidConnectionSyncCoordinator>();

            try
            {
                await connectionCoordinator.SyncAccountsAsync(
                    userId, connectionId, cancellationToken);
                await connectionCoordinator.SyncTransactionsAsync(
                    userId, connectionId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Try other banks once, then preserve the original failure.
                // Discovery must not consume this partial pass as complete.
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        firstFailure?.Throw();
    }

    public async Task SyncAccountsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await coordinator.SyncAllAccountsAsync(
            userId,
            cancellationToken);
    }

    public async Task SyncTransactionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await coordinator.SyncAllTransactionsAsync(
            userId,
            cancellationToken);
    }
}
