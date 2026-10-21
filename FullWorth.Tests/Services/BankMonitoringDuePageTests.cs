using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Plaid;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.Tests.Services;

public sealed class BankMonitoringDuePageTests
{
    [Fact]
    public async Task DuePages_AreDistinctOrderedBounded_AndIgnoreUnrefreshableConnections()
    {
        await using var db = new FullWorthDbContext(new DbContextOptionsBuilder<FullWorthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        var now = DateTimeOffset.UtcNow;
        var users = Enumerable.Range(1, 7).Select(index =>
            Guid.Parse($"00000000-0000-0000-0000-{index:000000000000}")).ToArray();
        BankConnectionEntity Connection(Guid user, string? credential = "synthetic-protected-token") =>
            new() { UserId = user, ProtectedPlaidAccessToken = credential,
                LastSuccessfulSyncAtUtc = now.AddDays(-2) };
        db.BankConnections.AddRange(
            Connection(users[0]), Connection(users[0]), Connection(users[1]), Connection(users[2]),
            Connection(users[3], null), Connection(users[4], ""));
        var archived = Connection(users[2]);
        archived.IsArchived = true; // Archiving is presentation only; monitoring remains eligible.
        db.BankConnections.Add(archived);
        var fresh = Connection(users[5]);
        fresh.LastSuccessfulSyncAtUtc = now;
        db.BankConnections.Add(fresh);
        var disconnected = Connection(users[6]);
        disconnected.Status = BankConnectionStatus.Disconnected;
        db.BankConnections.Add(disconnected);
        await db.SaveChangesAsync();

        var gateway = new PlaidBankConnectionReadGateway(db);
        Assert.Equal(users.Take(2), await gateway.GetDueUserIdsPageAsync(now, TimeSpan.FromHours(6), 2, 0));
        Assert.Equal(users.Skip(1).Take(2), await gateway.GetDueUserIdsPageAsync(now, TimeSpan.FromHours(6), 2, 1));
        Assert.Empty(await gateway.GetDueUserIdsPageAsync(now, TimeSpan.FromHours(6), 2, 3));
        Assert.Equal(users.Take(3), await gateway.GetDueUserIdsAsync(now, TimeSpan.FromHours(6), 20));
    }

    [Fact]
    public async Task NegativeOffset_IsRejected_AndCancellationIsPreserved()
    {
        await using var db = new FullWorthDbContext(new DbContextOptionsBuilder<FullWorthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        var gateway = new PlaidBankConnectionReadGateway(db);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            gateway.GetDueUserIdsPageAsync(DateTimeOffset.UtcNow, TimeSpan.FromHours(6), 2, -1));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            gateway.GetDueUserIdsPageAsync(DateTimeOffset.UtcNow, TimeSpan.FromHours(6), 2, 0, cancellation.Token));
    }
}
