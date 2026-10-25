using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace FullWorth.API.Services.Plaid;

// Each metadata write owns a separate context/connection. Claiming a one-time
// provider operation must never commit unrelated work in the caller's tracker.
internal sealed class PlaidLinkCompletionAttemptStore
{
    // Polling, exchange and item lookup each have a bounded provider deadline.
    // Allow those calls plus database writes before expiring an abandoned permit.
    internal static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(6);
    private readonly DbContextOptions<FullWorthDbContext> _options;
    private readonly TimeProvider _time;

    internal PlaidLinkCompletionAttemptStore(FullWorthDbContext context, TimeProvider time)
    {
        _options = (DbContextOptions<FullWorthDbContext>)context.GetService<IDbContextOptions>();
        if (_options.Extensions.OfType<RelationalOptionsExtension>()
            .Any(extension => extension.Connection is not null))
            throw new InvalidOperationException(
                "Hosted Link completion requires independently owned database connections.");
        _time = time;
    }

    private FullWorthDbContext Open() => new(_options);

    internal async Task<PlaidLinkCompletionAttemptEntity?> ReadAsync(
        Guid owner, Guid sessionId, CancellationToken cancellationToken)
    {
        await using var db = Open();
        return await db.PlaidLinkCompletionAttempts.AsNoTracking().SingleOrDefaultAsync(
            attempt => attempt.UserId == owner && attempt.SessionId == sessionId,
            cancellationToken);
    }

    internal static bool IsFresh(PlaidLinkCompletionAttemptEntity attempt,
        PlaidLinkSessionEntity session, DateTimeOffset now) =>
        now >= attempt.StartedAtUtc &&
        now < attempt.StartedAtUtc.Add(Lifetime) &&
        now < session.ExpiresAtUtc;

    internal async Task<CompletionClaim> TryAcquireAsync(
        PlaidLinkSessionEntity expected, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var db = Open();
        var existing = await db.PlaidLinkCompletionAttempts.AsNoTracking()
            .SingleOrDefaultAsync(attempt => attempt.UserId == expected.UserId &&
                attempt.SessionId == expected.Id, cancellationToken);
        if (existing is not null) return new(existing, false);

        var session = await db.PlaidLinkSessions.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == expected.Id &&
                candidate.UserId == expected.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Plaid Link session was not found.");
        if (!MatchesPending(session, expected)) return new(null, false);

        var now = _time.GetUtcNow();
        var attempt = new PlaidLinkCompletionAttemptEntity
        {
            SessionId = expected.Id, UserId = expected.UserId,
            AttemptId = Guid.NewGuid(), Stage = PlaidLinkCompletionStage.Polling,
            StartedAtUtc = now, UpdatedAtUtc = now
        };
        db.PlaidLinkCompletionAttempts.Add(attempt);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return new(attempt, true);
        }
        catch
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await TryAbortAsync(attempt);
                cancellationToken.ThrowIfCancellationRequested();
            }

            // A lost acknowledgement is safe only when a NEW connection proves
            // this exact nonce committed. A competing request's row grants no permit.
            PlaidLinkCompletionAttemptEntity? durable = null;
            try { durable = await ReadAsync(expected.UserId, expected.Id, cancellationToken); }
            catch { /* Preserve the original failure; never infer an acquired permit. */ }
            if (durable is not null)
                return new(durable, durable.AttemptId == attempt.AttemptId &&
                    durable.Stage == PlaidLinkCompletionStage.Polling);
            throw;
        }
    }

    internal async Task<PlaidLinkCompletionAttemptEntity> BeginExchangeAsync(
        PlaidLinkCompletionAttemptEntity claim, PlaidLinkSessionEntity expected,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var db = Open();
        var attempt = await db.PlaidLinkCompletionAttempts.SingleOrDefaultAsync(
            candidate => candidate.UserId == claim.UserId &&
                candidate.SessionId == claim.SessionId &&
                candidate.AttemptId == claim.AttemptId, cancellationToken)
            ?? throw new PlaidLinkRecoveryRequiredException();
        var session = await db.PlaidLinkSessions.SingleOrDefaultAsync(
            candidate => candidate.UserId == claim.UserId && candidate.Id == claim.SessionId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Plaid Link session was not found.");
        if (attempt.Stage != PlaidLinkCompletionStage.Polling ||
            !MatchesPending(session, expected) || !IsFresh(attempt, session, _time.GetUtcNow()))
            throw new PlaidLinkRecoveryRequiredException();

        attempt.Stage = PlaidLinkCompletionStage.Exchanging;
        attempt.UpdatedAtUtc = _time.GetUtcNow();
        // Commit the exchange fence and credential clearing together BEFORE
        // sending the public token. No future request can poll/re-exchange it.
        session.ProtectedLinkToken = string.Empty;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return attempt;
        }
        catch
        {
            if (cancellationToken.IsCancellationRequested)
                cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var durable = await ReadAsync(claim.UserId, claim.SessionId, cancellationToken);
                await using var proof = Open();
                var durableSession = await proof.PlaidLinkSessions.AsNoTracking()
                    .SingleOrDefaultAsync(candidate => candidate.Id == claim.SessionId &&
                        candidate.UserId == claim.UserId, cancellationToken);
                if (durable is not null && durable.AttemptId == claim.AttemptId &&
                    durable.Stage == PlaidLinkCompletionStage.Exchanging &&
                    durableSession is not null &&
                    durableSession.Status == PlaidLinkSessionStatus.Pending &&
                    durableSession.ProtectedLinkToken == string.Empty &&
                    IsFresh(durable, durableSession, _time.GetUtcNow()))
                    return durable;
            }
            catch { /* An unproved commit never permits provider access. */ }
            throw;
        }
    }

    internal static void PrepareCompletion(FullWorthDbContext db,
        PlaidLinkCompletionAttemptEntity attempt, PlaidLinkSessionEntity session,
        DateTimeOffset now, bool requireUnexpiredSession = true)
    {
        if (attempt.SessionId != session.Id || attempt.UserId != session.UserId ||
            attempt.AttemptId == Guid.Empty ||
            attempt.Stage is not (PlaidLinkCompletionStage.Polling or PlaidLinkCompletionStage.Exchanging) ||
            now < attempt.StartedAtUtc || now >= attempt.StartedAtUtc.Add(Lifetime) ||
            (requireUnexpiredSession && now >= session.ExpiresAtUtc))
            throw new PlaidLinkRecoveryRequiredException();
        if (db.Entry(attempt).State == EntityState.Detached)
        {
            // A separately materialized permit can retain a Session navigation
            // from another context. Never recursively attach that graph over
            // the caller's already tracked, owner-validated session.
            db.Entry(attempt).State = EntityState.Unchanged;
        }
        attempt.Stage = PlaidLinkCompletionStage.Completed;
        attempt.UpdatedAtUtc = now;
    }

    internal async Task InterruptAsync(PlaidLinkCompletionAttemptEntity expected,
        CancellationToken cancellationToken)
    {
        await using var db = Open();
        var attempt = await db.PlaidLinkCompletionAttempts.SingleOrDefaultAsync(
            candidate => candidate.UserId == expected.UserId &&
                candidate.SessionId == expected.SessionId &&
                candidate.AttemptId == expected.AttemptId, cancellationToken);
        if (attempt is null ||
            attempt.Stage is PlaidLinkCompletionStage.Completed or PlaidLinkCompletionStage.Interrupted)
            return;
        attempt.Stage = PlaidLinkCompletionStage.Interrupted;
        attempt.UpdatedAtUtc = _time.GetUtcNow();
        var session = await db.PlaidLinkSessions.SingleOrDefaultAsync(
            candidate => candidate.Id == expected.SessionId &&
                candidate.UserId == expected.UserId, cancellationToken);
        if (session?.Status == PlaidLinkSessionStatus.Pending)
            session.ProtectedLinkToken = string.Empty;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            // A completion/another interruption won. Its durable state remains authoritative.
        }
    }

    internal async Task ReleasePollingAsync(PlaidLinkCompletionAttemptEntity expected,
        CancellationToken cancellationToken)
    {
        await using var db = Open();
        var attempt = await db.PlaidLinkCompletionAttempts.SingleOrDefaultAsync(
            candidate => candidate.UserId == expected.UserId &&
                candidate.SessionId == expected.SessionId &&
                candidate.AttemptId == expected.AttemptId, cancellationToken);
        if (attempt is null || attempt.Stage != PlaidLinkCompletionStage.Polling) return;
        db.PlaidLinkCompletionAttempts.Remove(attempt);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { /* Never remove an exchange/completion fence. */ }
    }

    internal async Task TryAbortAsync(PlaidLinkCompletionAttemptEntity expected)
    {
        // Only bounded metadata cleanup survives cancellation. No provider call,
        // bank mutation, caller SaveChanges or automatic exchange retry occurs.
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            var durable = await ReadAsync(expected.UserId, expected.SessionId, cleanup.Token);
            if (durable is null || durable.AttemptId != expected.AttemptId) return;
            if (durable.Stage == PlaidLinkCompletionStage.Polling)
                await ReleasePollingAsync(durable, cleanup.Token);
            else if (durable.Stage == PlaidLinkCompletionStage.Exchanging)
                await InterruptAsync(durable, cleanup.Token);
        }
        catch
        {
            // A persisted claim remains fail-closed and expires into recovery.
            // Cleanup failure is never permission to replay the public token.
        }
    }

    private static bool MatchesPending(PlaidLinkSessionEntity actual,
        PlaidLinkSessionEntity expected) =>
        actual.Status == PlaidLinkSessionStatus.Pending &&
        actual.Id == expected.Id && actual.UserId == expected.UserId &&
        actual.BankConnectionId == expected.BankConnectionId &&
        actual.ExpiresAtUtc.UtcTicks / 10 == expected.ExpiresAtUtc.UtcTicks / 10 &&
        string.Equals(actual.ProtectedLinkToken, expected.ProtectedLinkToken, StringComparison.Ordinal);

    internal sealed record CompletionClaim(PlaidLinkCompletionAttemptEntity? Attempt, bool Acquired);
}

internal sealed class PlaidLinkRecoveryRequiredException : InvalidOperationException
{
    public PlaidLinkRecoveryRequiredException()
        : base("Bank connection completion could not be verified. Start a new secure Link session.") { }
}
