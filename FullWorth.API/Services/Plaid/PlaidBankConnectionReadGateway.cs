using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.API.Services.Plaid;

public sealed class PlaidBankConnectionReadGateway(
    FullWorthDbContext dbContext)
    : IBankConnectionReadGateway
{
    public async Task<IReadOnlyList<string>>
        GetAttentionInstitutionNamesAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User ID is required.",
                nameof(userId));
        }

        return await dbContext.BankConnections
            .AsNoTracking()
            .Where(
                connection =>
                    connection.UserId == userId &&
                    connection.Status ==
                        BankConnectionStatus.RequiresAttention)
            .OrderBy(
                connection =>
                    connection.InstitutionName)
            .ThenBy(
                connection =>
                    connection.Id)
            .Select(
                connection =>
                    connection.InstitutionName)
            .ToListAsync(
                cancellationToken);
    }

    public Task<IReadOnlyList<Guid>> GetDueUserIdsAsync(
        DateTimeOffset now,
        TimeSpan refreshCadence,
        int maxUsers,
        CancellationToken cancellationToken = default) =>
        GetDueUserIdsPageAsync(now, refreshCadence, maxUsers, 0, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetDueUserIdsPageAsync(
        DateTimeOffset now,
        TimeSpan refreshCadence,
        int maxUsers,
        int offset,
        CancellationToken cancellationToken = default)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (refreshCadence <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(refreshCadence),
                "Refresh cadence must be greater than zero.");
        }

        if (maxUsers <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxUsers),
                "Maximum users must be greater than zero.");
        }

        var cutoff =
            now -
            refreshCadence;

        return await dbContext.BankConnections
            .AsNoTracking()
            .Where(
                connection =>
                    connection.Status ==
                        BankConnectionStatus.Active &&
                    connection.ProtectedPlaidAccessToken != null &&
                    connection.ProtectedPlaidAccessToken != string.Empty &&
                    (
                        !connection.LastSuccessfulSyncAtUtc.HasValue ||
                        connection.LastSuccessfulSyncAtUtc.Value <= cutoff
                    ))
            .Select(
                connection =>
                    connection.UserId)
            .Distinct()
            .OrderBy(
                userId =>
                    userId)
            .Skip(offset)
            .Take(
                maxUsers)
            .ToListAsync(
                cancellationToken);
    }
}
