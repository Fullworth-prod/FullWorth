using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FullWorth.Tests.Services;

public sealed class HostedLinkRecoveryTests
{
    [Theory]
    [InlineData("new")]
    [InlineData("existing")]
    public async Task ConcurrentCompletion_UsesOneDurableExchangePermit(string mode)
    {
        var f = await HostedRecoveryFixture.CreateAsync(mode);
        await using var first = f.Open();
        await using var second = f.Open();
        using var provider = new RecoveryProvider(f.ItemId);
        using var http = new HttpClient(provider);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.BeforeResponse = async (path, token) =>
        {
            if (path != "/item/public_token/exchange") return;
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        };
        var one = f.Service(first, http).CheckAndCompleteAsync(f.Owner, f.SessionId);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
        try
        {
            var two = await f.Service(second, http).CheckAndCompleteAsync(f.Owner, f.SessionId);
            Assert.Equal("Pending", two.Status);
            Assert.Null(two.Connection);
            Assert.Equal(1, provider.Polls);
            Assert.Equal(1, provider.Exchanges);
        }
        finally { release.TrySetResult(); }
        var completed = await one;
        Assert.Equal("Completed", completed.Status);
        Assert.NotNull(completed.Connection);
        var replay = await f.Service(second, http).CheckAndCompleteAsync(f.Owner, f.SessionId);
        Assert.Equal(completed.Connection.Id, replay.Connection?.Id);
        Assert.Equal(1, provider.Exchanges);
        Assert.Equal(1, provider.MaximumExchangesPerToken);
        await using var verify = f.OpenDirect();
        var attempt = await verify.PlaidLinkCompletionAttempts.AsNoTracking()
            .SingleAsync(a => a.SessionId == f.SessionId && a.UserId == f.Owner);
        Assert.Equal(PlaidLinkCompletionStage.Completed, attempt.Stage);
        Assert.Equal(string.Empty, (await verify.PlaidLinkSessions.AsNoTracking()
            .SingleAsync(s => s.Id == f.SessionId)).ProtectedLinkToken);
        await f.VerifyHistoryAsync();
    }

    public static IEnumerable<object[]> Failures()
    {
        foreach (var mode in new[] { "new", "existing" })
        foreach (var phase in new[] { "item", "save", "cancel" })
            yield return [mode, phase];
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task FailedConsumedToken_IsFencedAndRequiresExplicitFreshSession(string mode, string phase)
    {
        var fault = new CompletionFault { BeforeCommit = phase == "save" };
        var f = await HostedRecoveryFixture.CreateAsync(mode, fault);
        await using var db = f.Open();
        var tracked = await db.PlaidLinkSessions.SingleAsync(s => s.Id == f.SessionId);
        var originalCipher = tracked.ProtectedLinkToken;
        tracked.UpdatedAtUtc = f.Clock.Now.AddSeconds(1);
        var caller = new BillStreamEntity { UserId = f.Owner, ProviderName = "Unrelated caller work" };
        db.BillStreams.Add(caller);
        using var cancel = new CancellationTokenSource();
        using var provider = new RecoveryProvider(f.ItemId);
        using var http = new HttpClient(provider);
        provider.BeforeResponse = (path, token) =>
        {
            if (path == "/item/get" && phase == "item") throw new IOException("Synthetic provider response loss.");
            if (path == "/item/get" && phase == "cancel")
            {
                cancel.Cancel();
                token.ThrowIfCancellationRequested();
            }
            return Task.CompletedTask;
        };
        var error = await Record.ExceptionAsync(() =>
            f.Service(db, http).CheckAndCompleteAsync(f.Owner, f.SessionId, cancel.Token));
        Assert.NotNull(error);
        if (phase == "cancel") Assert.IsAssignableFrom<OperationCanceledException>(error);
        Assert.Equal(PlaidLinkSessionStatus.Pending, tracked.Status);
        Assert.Equal(originalCipher, tracked.ProtectedLinkToken);
        Assert.Equal(EntityState.Added, db.Entry(caller).State);
        await using (var verify = f.OpenDirect())
        {
            Assert.False(await verify.BillStreams.AnyAsync(b => b.Id == caller.Id));
            var attempt = await verify.PlaidLinkCompletionAttempts.AsNoTracking()
                .SingleAsync(a => a.UserId == f.Owner && a.SessionId == f.SessionId);
            Assert.Equal(PlaidLinkCompletionStage.Interrupted, attempt.Stage);
            Assert.Equal(string.Empty, (await verify.PlaidLinkSessions.AsNoTracking()
                .SingleAsync(s => s.Id == f.SessionId)).ProtectedLinkToken);
        }
        var calls = provider.Polls + provider.Exchanges + provider.Items;
        var replay = await f.Service(db, http).CheckAndCompleteAsync(f.Owner, f.SessionId);
        Assert.Equal("RecoveryRequired", replay.Status);
        Assert.Null(replay.Connection);
        Assert.Equal(calls, provider.Polls + provider.Exchanges + provider.Items);
        Assert.Equal(1, provider.Exchanges);
        await db.SaveChangesAsync();
        await using (var verify = f.OpenDirect())
        {
            Assert.True(await verify.BillStreams.AnyAsync(b => b.Id == caller.Id));
            var bank = await verify.BankConnections.AsNoTracking().SingleAsync(b => b.Id == f.BankId);
            Assert.Equal(BankConnectionStatus.RequiresAttention, bank.Status);
            Assert.Equal("owned-original", f.Protector.Unprotect(bank.ProtectedPlaidAccessToken!));
        }
        await f.VerifyHistoryAsync();

        // A fresh owner-initiated session is a different one-time token. The
        // failed session remains fenced; this is not a silent provider retry.
        provider.BeforeResponse = null;
        var freshId = Guid.NewGuid();
        await using (var fresh = f.OpenDirect())
        {
            fresh.PlaidLinkSessions.Add(f.NewSession(freshId));
            await fresh.SaveChangesAsync();
        }
        await using var next = f.Open();
        var recovered = await f.Service(next, http).CheckAndCompleteAsync(f.Owner, freshId);
        Assert.Equal("Completed", recovered.Status);
        Assert.NotNull(recovered.Connection);
        Assert.Equal(2, provider.Exchanges);
        Assert.Equal(1, provider.MaximumExchangesPerToken);
        await f.VerifyHistoryAsync();
    }

    [Theory]
    [InlineData("new")]
    [InlineData("existing")]
    public async Task AcknowledgementLostAfterCommit_ReplaysDurableOwnerResult(string mode)
    {
        var fault = new CompletionFault { AfterCommit = true };
        var f = await HostedRecoveryFixture.CreateAsync(mode, fault);
        await using var db = f.Open();
        using var provider = new RecoveryProvider(f.ItemId);
        using var http = new HttpClient(provider);
        var service = f.Service(db, http);
        await Assert.ThrowsAsync<IOException>(() => service.CheckAndCompleteAsync(f.Owner, f.SessionId));
        var calls = provider.Polls + provider.Exchanges + provider.Items;
        var replay = await service.CheckAndCompleteAsync(f.Owner, f.SessionId);
        Assert.Equal("Completed", replay.Status);
        Assert.NotNull(replay.Connection);
        Assert.Equal(calls, provider.Polls + provider.Exchanges + provider.Items);
        Assert.Equal(1, provider.Exchanges);
        await f.VerifyHistoryAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadOnlyPolling_CanRetryWithoutAnExchangeFence(bool providerFailure)
    {
        var f = await HostedRecoveryFixture.CreateAsync("new");
        await using var db = f.Open();
        using var provider = new RecoveryProvider(f.ItemId) { Pending = true };
        using var http = new HttpClient(provider);
        provider.BeforeResponse = (path, _) =>
        {
            if (providerFailure && path == "/link/token/get")
                throw new IOException("Synthetic status-read failure.");
            return Task.CompletedTask;
        };
        var service = f.Service(db, http);
        if (providerFailure) await Assert.ThrowsAsync<IOException>(() => service.CheckAndCompleteAsync(f.Owner, f.SessionId));
        else Assert.Equal("Pending", (await service.CheckAndCompleteAsync(f.Owner, f.SessionId)).Status);
        await using (var verify = f.OpenDirect())
            Assert.False(await verify.PlaidLinkCompletionAttempts.AnyAsync(a => a.SessionId == f.SessionId));
        provider.BeforeResponse = null;
        provider.Pending = false;
        Assert.Equal("Completed", (await service.CheckAndCompleteAsync(f.Owner, f.SessionId)).Status);
        Assert.Equal(1, provider.Exchanges);
        await f.VerifyHistoryAsync();
    }

    [Theory]
    [InlineData("/link/token/get")]
    [InlineData("/item/public_token/exchange")]
    public async Task InterruptedLease_StopsAnOldRequestFromCommitting(string heldPath)
    {
        var f = await HostedRecoveryFixture.CreateAsync("existing");
        await using var first = f.Open();
        await using var second = f.Open();
        using var provider = new RecoveryProvider(f.ItemId);
        using var http = new HttpClient(provider);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.BeforeResponse = async (path, token) =>
        {
            if (path != heldPath) return;
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        };
        var old = f.Service(first, http).CheckAndCompleteAsync(f.Owner, f.SessionId);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
        f.Clock.Now = f.Clock.Now.AddMinutes(7);
        try
        {
            var recovery = await f.Service(second, http).CheckAndCompleteAsync(f.Owner, f.SessionId);
            Assert.Equal("RecoveryRequired", recovery.Status);
            Assert.Null(recovery.Connection);
        }
        finally { release.TrySetResult(); }
        Assert.Equal("RecoveryRequired", (await old).Status);
        await using var verify = f.OpenDirect();
        Assert.Equal(PlaidLinkSessionStatus.Pending, (await verify.PlaidLinkSessions.AsNoTracking()
            .SingleAsync(s => s.Id == f.SessionId)).Status);
        Assert.Equal(BankConnectionStatus.RequiresAttention,
            (await verify.BankConnections.AsNoTracking().SingleAsync(b => b.Id == f.BankId)).Status);
        Assert.Equal(heldPath == "/link/token/get" ? 0 : 1, provider.Exchanges);
        await f.VerifyHistoryAsync();
    }

    [Fact]
    public async Task ForeignOwnerAndCancellation_CannotAcquireAProviderPermit()
    {
        var f = await HostedRecoveryFixture.CreateAsync("new");
        await using var db = f.Open();
        using var provider = new RecoveryProvider(f.ItemId);
        using var http = new HttpClient(provider);
        var service = f.Service(db, http);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CheckAndCompleteAsync(f.ForeignOwner, f.SessionId));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CheckAndCompleteAsync(f.Owner, f.SessionId, cancelled.Token));
        Assert.Equal(0, provider.Polls + provider.Exchanges + provider.Items);
        await using var verify = f.OpenDirect();
        Assert.False(await verify.PlaidLinkCompletionAttempts.AnyAsync(a => a.SessionId == f.SessionId));
        await f.VerifyHistoryAsync();
    }

    [Fact]
    public async Task ForeignOwner_CannotObserveOrInterruptExistingCompletionAttempt()
    {
        var f = await HostedRecoveryFixture.CreateAsync("existing");
        var nonce = Guid.NewGuid();
        DateTimeOffset persistedUpdated;
        await using (var seed = f.OpenDirect())
        {
            seed.PlaidLinkCompletionAttempts.Add(new PlaidLinkCompletionAttemptEntity
            {
                UserId = f.Owner, SessionId = f.SessionId, AttemptId = nonce,
                Stage = PlaidLinkCompletionStage.Exchanging,
                StartedAtUtc = f.Clock.Now.AddHours(-1), UpdatedAtUtc = f.Clock.Now.AddHours(-1)
            });
            await seed.SaveChangesAsync();
            persistedUpdated = await seed.PlaidLinkCompletionAttempts.AsNoTracking()
                .Where(a => a.SessionId == f.SessionId).Select(a => a.UpdatedAtUtc).SingleAsync();
        }
        await using var db = f.Open();
        using var provider = new RecoveryProvider(f.ItemId);
        using var http = new HttpClient(provider);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            f.Service(db, http).CheckAndCompleteAsync(f.ForeignOwner, f.SessionId));
        Assert.Equal(0, provider.Polls + provider.Exchanges + provider.Items);
        await using var verify = f.OpenDirect();
        var actual = await verify.PlaidLinkCompletionAttempts.AsNoTracking()
            .SingleAsync(a => a.SessionId == f.SessionId);
        Assert.Equal(f.Owner, actual.UserId);
        Assert.Equal(nonce, actual.AttemptId);
        Assert.Equal(PlaidLinkCompletionStage.Exchanging, actual.Stage);
        Assert.Equal(persistedUpdated, actual.UpdatedAtUtc);
        Assert.False(await verify.PlaidLinkCompletionAttempts
            .AnyAsync(a => a.SessionId == f.SessionId && a.UserId == f.ForeignOwner));
        await f.VerifyHistoryAsync();
    }

    private sealed class CompletionFault : SaveChangesInterceptor
    {
        internal bool BeforeCommit, AfterCommit;
        private static bool Completing(DbContext? db) =>
            db!.ChangeTracker.Entries<PlaidLinkSessionEntity>()
                .Any(e => e.Entity.Status == PlaidLinkSessionStatus.Completed);
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData data, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (BeforeCommit && Completing(data.Context))
            {
                BeforeCommit = false;
                throw new DbUpdateException("Synthetic final transaction failure.");
            }
            return ValueTask.FromResult(result);
        }
        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData data, int result, CancellationToken cancellationToken = default)
        {
            if (AfterCommit && Completing(data.Context))
            {
                AfterCommit = false;
                throw new IOException("Synthetic lost completion acknowledgement.");
            }
            return ValueTask.FromResult(result);
        }
    }
}
