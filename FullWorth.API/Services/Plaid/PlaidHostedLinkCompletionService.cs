using System.Text.Json;
using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace FullWorth.API.Services.Plaid;

public sealed class PlaidHostedLinkCompletionService
{
    private const int MaxLinkSessions =
        50;

    private const int MaxItemAddResults =
        50;

    private const int MaxPublicTokenLength =
        8 * 1024;

    private readonly FullWorthDbContext
        _dbContext;

    private readonly PlaidApiClient
        _plaidApiClient;

    private readonly PlaidTokenProtector
        _tokenProtector;

    private readonly PlaidConnectionExchangeService
        _exchangeService;

    private readonly TimeProvider _timeProvider;
    private readonly PlaidLinkCompletionAttemptStore _attemptStore;

    public PlaidHostedLinkCompletionService(
        FullWorthDbContext dbContext,
        PlaidApiClient plaidApiClient,
        PlaidTokenProtector tokenProtector,
        PlaidConnectionExchangeService exchangeService,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(
            dbContext);

        ArgumentNullException.ThrowIfNull(
            plaidApiClient);

        ArgumentNullException.ThrowIfNull(
            tokenProtector);

        ArgumentNullException.ThrowIfNull(
            exchangeService);

        _dbContext =
            dbContext;

        _plaidApiClient =
            plaidApiClient;

        _tokenProtector =
            tokenProtector;

        _exchangeService =
            exchangeService;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _attemptStore = new PlaidLinkCompletionAttemptStore(dbContext, _timeProvider);
    }

    public async Task<PlaidHostedLinkCompletionResult> CheckAndCompleteAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A valid user ID is required.", nameof(userId));
        if (sessionId == Guid.Empty)
            throw new ArgumentException("A valid Plaid Link session ID is required.", nameof(sessionId));
        cancellationToken.ThrowIfCancellationRequested();
        // The provider operation cannot be enlisted in a caller's later commit.
        if (System.Transactions.Transaction.Current is not null ||
            (_dbContext.Database.IsRelational() && _dbContext.Database.CurrentTransaction is not null))
            throw new InvalidOperationException("Check bank connection status in a fresh request.");

        var durable = await GetDurableSessionAsync(userId, sessionId, cancellationToken);
        if (durable.Status != PlaidLinkSessionStatus.Pending)
            return await CreateTerminalResultAsync(durable, cancellationToken);
        var observed = await ReadAttemptResultAsync(durable, cancellationToken);
        if (observed is not null) return observed;

        var tracked = _dbContext.ChangeTracker.Entries<PlaidLinkSessionEntity>()
            .SingleOrDefault(entry => entry.Entity.Id == sessionId);
        var session = durable;
        if (tracked is not null)
        {
            if (tracked.State is EntityState.Added or EntityState.Deleted)
                throw new InvalidOperationException("Plaid Link session changed. Reload before checking its status.");
            EnsureSamePendingSession(tracked.Entity, durable);
            session = tracked.Entity;
        }
        else _dbContext.PlaidLinkSessions.Attach(session);

        if (_dbContext.ChangeTracker.Entries<PlaidLinkCompletionAttemptEntity>()
            .Any(entry => entry.Entity.SessionId == sessionId))
            throw new InvalidOperationException("Bank connection recovery state is already tracked. Reload first.");

        var snapshot = new CompletionEntrySnapshot(_dbContext.Entry(session));
        PlaidLinkCompletionAttemptEntity? claim = null;
        try
        {
            var acquisition = await _attemptStore.TryAcquireAsync(session, cancellationToken);
            if (!acquisition.Acquired)
            {
                durable = await GetDurableSessionAsync(userId, sessionId, cancellationToken);
                if (durable.Status != PlaidLinkSessionStatus.Pending)
                    return await CreateTerminalResultAsync(durable, cancellationToken);
                return await ReadAttemptResultAsync(durable, cancellationToken) ?? CreateResult(durable);
            }
            claim = acquisition.Attempt!;
            durable = await GetDurableSessionAsync(userId, sessionId, cancellationToken);
            if (durable.Status != PlaidLinkSessionStatus.Pending)
                return await CreateTerminalResultAsync(durable, cancellationToken);
            EnsureSamePendingSession(session, durable);
            return await CompletePendingSessionAsync(session, claim, cancellationToken);
        }
        catch (PlaidLinkRecoveryRequiredException)
        {
            snapshot.Restore();
            DetachAttempt(sessionId);
            return new(sessionId, "RecoveryRequired", null);
        }
        catch
        {
            snapshot.Restore();
            DetachAttempt(sessionId);
            throw;
        }
        finally
        {
            if (claim is not null) await _attemptStore.TryAbortAsync(claim);
        }
    }

    private void DetachAttempt(Guid sessionId)
    {
        // Only this method's owned permit can have been attached after preflight.
        var entry = _dbContext.ChangeTracker.Entries<PlaidLinkCompletionAttemptEntity>()
            .SingleOrDefault(candidate => candidate.Entity.SessionId == sessionId);
        if (entry is not null) entry.State = EntityState.Detached;
    }

    private async Task<PlaidHostedLinkCompletionResult?> ReadAttemptResultAsync(
        PlaidLinkSessionEntity session, CancellationToken cancellationToken)
    {
        var attempt = await _attemptStore.ReadAsync(session.UserId, session.Id, cancellationToken);
        if (attempt is null) return null;
        if ((attempt.Stage is PlaidLinkCompletionStage.Polling or PlaidLinkCompletionStage.Exchanging) &&
            PlaidLinkCompletionAttemptStore.IsFresh(attempt, session, _timeProvider.GetUtcNow()))
            return new(session.Id, "Pending", null);

        if (attempt.Stage is PlaidLinkCompletionStage.Polling or PlaidLinkCompletionStage.Exchanging)
            await _attemptStore.InterruptAsync(attempt, cancellationToken);

        // A completion can win while an interrupted/stale claim is being read.
        var latest = await GetDurableSessionAsync(session.UserId, session.Id, cancellationToken);
        if (latest.Status != PlaidLinkSessionStatus.Pending)
            return await CreateTerminalResultAsync(latest, cancellationToken);
        return new(session.Id, "RecoveryRequired", null);
    }

    private async Task<PlaidLinkSessionEntity> GetDurableSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        // Owner and identifier remain in the database predicate, even when
        // a row with the same identifier is already tracked.
        return await _dbContext.PlaidLinkSessions.AsNoTracking()
            .SingleOrDefaultAsync(session =>
                session.Id == sessionId && session.UserId == userId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Plaid Link session was not found.");
    }

    private static void EnsureSamePendingSession(
        PlaidLinkSessionEntity tracked,
        PlaidLinkSessionEntity durable)
    {
        // Never overwrite caller changes to reconcile a stale identity map.
        // Harmless timestamp edits remain part of the caller's unit of work.
        if (tracked.Id != durable.Id ||
            tracked.UserId != durable.UserId ||
            tracked.Status != PlaidLinkSessionStatus.Pending ||
            durable.Status != PlaidLinkSessionStatus.Pending ||
            tracked.BankConnectionId != durable.BankConnectionId ||
            // PostgreSQL timestamps retain microseconds, not .NET's 100ns ticks.
            tracked.ExpiresAtUtc.UtcTicks / 10 != durable.ExpiresAtUtc.UtcTicks / 10 ||
            !string.Equals(tracked.ProtectedLinkToken, durable.ProtectedLinkToken,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Plaid Link session changed. Reload before checking its status.");
        }
    }

    private async Task<PlaidHostedLinkCompletionResult> CreateTerminalResultAsync(
        PlaidLinkSessionEntity session,
        CancellationToken cancellationToken)
    {
        if (session.Status != PlaidLinkSessionStatus.Completed ||
            session.BankConnectionId is not Guid bankId)
        {
            return CreateResult(session);
        }

        // Recheck the bank's owner independently of the session. A stale,
        // removed or subsequently disconnected bank must not be presented
        // as connected, and a forged cross-owner bank ID must reveal nothing.
        var bank = await _dbContext.BankConnections.AsNoTracking()
            .SingleOrDefaultAsync(candidate =>
                candidate.Id == bankId &&
                candidate.UserId == session.UserId &&
                candidate.Status != BankConnectionStatus.Disconnected,
                cancellationToken);

        if (bank is null)
        {
            return CreateResult(session);
        }

        return new PlaidHostedLinkCompletionResult(
            session.Id,
            session.Status.ToString(),
            new PlaidConnectionResult(
                bank.Id,
                bank.InstitutionName,
                bank.Status.ToString()));
    }

    private async Task<PlaidHostedLinkCompletionResult> CompletePendingSessionAsync(
        PlaidLinkSessionEntity session,
        PlaidLinkCompletionAttemptEntity claim,
        CancellationToken cancellationToken)
    {
        var userId = session.UserId;
        var now =
            _timeProvider.GetUtcNow();

        if (session.ExpiresAtUtc <=
            now)
        {
            return await SetTerminalStatusAsync(
                session,
                PlaidLinkSessionStatus.Expired,
                claim,
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(
                session.ProtectedLinkToken))
        {
            throw new InvalidOperationException(
                "Pending Plaid Link session does not contain a valid protected token.");
        }

        /*
         * Plaintext Link tokens exist only in server memory and are never
         * returned from this service.
         */
        var linkToken =
            _tokenProtector.UnprotectLinkToken(
                session.ProtectedLinkToken);

        using var response =
            await _plaidApiClient.PostAsync(
                "link/token/get",
                new
                {
                    link_token =
                        linkToken
                },
                cancellationToken);

        // Polling Plaid awaited external work. Another request may have
        // completed this session meanwhile; use its durable result instead of
        // exchanging an already-consumed public token or overwriting a terminal
        // state with this response.
        var durableSession = await GetDurableSessionAsync(
            userId, session.Id, cancellationToken);
        if (durableSession.Status != PlaidLinkSessionStatus.Pending)
        {
            return await CreateTerminalResultAsync(durableSession, cancellationToken);
        }

        var durableClaim = await _attemptStore.ReadAsync(
            userId, session.Id, cancellationToken);
        if (durableClaim is null || durableClaim.AttemptId != claim.AttemptId ||
            durableClaim.Stage != PlaidLinkCompletionStage.Polling ||
            !PlaidLinkCompletionAttemptStore.IsFresh(durableClaim, durableSession, _timeProvider.GetUtcNow()))
            throw new PlaidLinkRecoveryRequiredException();
        EnsureSamePendingSession(session, durableSession);

        var plaidState =
            ReadPlaidSessionState(
                response.RootElement);

        if (session.BankConnectionId is Guid)
        {
            if (!plaidState.HasLinkSessions ||
                plaidState.HasUnfinishedSession)
            {
                return CreateResult(
                    session);
            }

            if (plaidState.HasExitedSession)
            {
                return await SetTerminalStatusAsync(
                    session,
                    PlaidLinkSessionStatus.Exited,
                    claim,
                    cancellationToken);
            }

            if (plaidState.HasFinishedSession)
            {
                return await CompleteUpdateModeAsync(
                    session,
                    claim,
                    cancellationToken);
            }

            return CreateResult(
                session);
        }

        if (plaidState.PublicToken is not null &&
            !plaidState.HasUnfinishedSession &&
            !plaidState.HasExitedSession)
        {
            /*
             * Do not consume the one-time public token while any provider
             * session is unfinished or has exited. The response may contain
             * multiple sessions, and their order must not change the decision.
             *
             * The public token is immediately exchanged server-side. It is
             * never persisted and never returned to the MAUI client.
             */
            // A successful initial link is one database persistence unit:
            // the exchange service saves both the owner-scoped bank connection
            // and terminal Hosted Link session together. A separate save here
            // could leave the bank committed while the session stays Pending.
            // Before the durable fence the browser may cancel freely. Once
            // we commit permission to consume the one-time token, a disappearing
            // tab/circuit must not strand the exchanged credential halfway through
            // item lookup and atomic persistence. Continue only this authorized
            // operation under a separate finite server deadline; never replay it.
            cancellationToken.ThrowIfCancellationRequested();
            using var finalization = new CancellationTokenSource(
                TimeSpan.FromMinutes(4), _timeProvider);
            var exchangeClaim = await _attemptStore.BeginExchangeAsync(
                claim, session, finalization.Token);
            var connection =
                await _exchangeService.ExchangeAndCompleteHostedLinkAsync(
                    userId,
                    plaidState.PublicToken,
                    session,
                    exchangeClaim,
                    finalization.Token);

            return new PlaidHostedLinkCompletionResult(
                session.Id,
                session.Status.ToString(),
                connection);
        }

        /*
         * No provider session yet, or at least one provider session is
         * still unfinished. The correct state remains Pending.
         */
        if (!plaidState.HasLinkSessions ||
            plaidState.HasUnfinishedSession)
        {
            return CreateResult(
                session);
        }

        if (plaidState.HasExitedSession)
        {
            return await SetTerminalStatusAsync(
                session,
                PlaidLinkSessionStatus.Exited,
                claim,
                cancellationToken);
        }

        if (plaidState.HasFinishedSession)
        {
            /*
             * A finished Hosted Link flow without a public token cannot be
             * treated as a successful connection.
             */
            return await SetTerminalStatusAsync(
                session,
                PlaidLinkSessionStatus.Failed,
                claim,
                cancellationToken);
        }

        return CreateResult(
            session);
    }

    private async Task<PlaidHostedLinkCompletionResult>
        CompleteUpdateModeAsync(
            PlaidLinkSessionEntity session,
            PlaidLinkCompletionAttemptEntity claim,
            CancellationToken cancellationToken)
    {
        if (session.BankConnectionId is not Guid connectionId)
        {
            throw new InvalidOperationException(
                "Plaid update session is missing its bank connection.");
        }

        var connection =
            await _dbContext.BankConnections
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.UserId == session.UserId &&
                        candidate.Id == connectionId,
                    cancellationToken)
            ?? throw new KeyNotFoundException(
                "Bank connection was not found.");

        if (connection.Status ==
            BankConnectionStatus.Disconnected)
        {
            throw new InvalidOperationException(
                "Disconnected bank connection cannot complete Plaid update mode.");
        }

        var snapshot = new CompletionEntrySnapshot(_dbContext.Entry(connection));
        try
        {
            var completedAt =
                _timeProvider.GetUtcNow();

            connection.Status =
                BankConnectionStatus.Active;

            connection.UpdatedAtUtc =
                completedAt;

            session.Status =
                PlaidLinkSessionStatus.Completed;

            session.CompletedAtUtc =
                completedAt;

            session.UpdatedAtUtc =
                completedAt;

            session.ProtectedLinkToken =
                string.Empty;

            PlaidLinkCompletionAttemptStore.PrepareCompletion(
                _dbContext, claim, session, completedAt);
            await _dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch
        {
            snapshot.Restore();
            throw;
        }

        return new PlaidHostedLinkCompletionResult(
            session.Id,
            session.Status.ToString(),
            new PlaidConnectionResult(
                connection.Id,
                connection.InstitutionName,
                connection.Status.ToString()));
    }

    private static PlaidSessionState
        ReadPlaidSessionState(
            JsonElement root)
    {
        if (root.ValueKind !=
            JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                "Plaid returned an invalid Hosted Link status response.");
        }

        if (!root.TryGetProperty(
                "link_sessions",
                out var linkSessionsElement) ||
            linkSessionsElement.ValueKind ==
                JsonValueKind.Null)
        {
            return new PlaidSessionState(
                HasLinkSessions:
                    false,
                HasFinishedSession:
                    false,
                HasUnfinishedSession:
                    false,
                HasExitedSession:
                    false,
                PublicToken:
                    null);
        }

        if (linkSessionsElement.ValueKind !=
            JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                "Plaid returned an invalid Hosted Link status response.");
        }

        if (linkSessionsElement.GetArrayLength() >
            MaxLinkSessions)
        {
            throw new InvalidOperationException(
                "Plaid returned too many Hosted Link session records.");
        }

        var hasLinkSessions =
            false;

        var hasFinishedSession =
            false;

        var hasUnfinishedSession =
            false;

        var hasExitedSession =
            false;

        string?
            publicToken =
                null;

        foreach (var linkSession in
                 linkSessionsElement.EnumerateArray())
        {
            if (linkSession.ValueKind !=
                JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    "Plaid returned an invalid Hosted Link session record.");
            }

            hasLinkSessions =
                true;

            var isFinished =
                HasNonEmptyOptionalString(
                    linkSession,
                    "finished_at");

            if (isFinished)
            {
                hasFinishedSession =
                    true;
            }
            else
            {
                hasUnfinishedSession =
                    true;
            }

            if (HasExitResult(
                    linkSession))
            {
                hasExitedSession =
                    true;
            }

            // Always parse both provider fields, even after finding a token
            // in an earlier session. Short-circuiting can conceal malformed
            // responses or conflicting item credentials in later sessions.
            var resultToken =
                GetPublicTokenFromResults(
                    linkSession);

            var legacyToken =
                GetPublicTokenFromLegacySuccess(
                    linkSession);

            foreach (var candidate in new[] { resultToken, legacyToken })
            {
                if (candidate is null)
                {
                    continue;
                }

                if (publicToken is not null &&
                    !string.Equals(publicToken, candidate,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Plaid returned conflicting Hosted Link public tokens.");
                }

                publicToken = candidate;
            }

            // A later unfinished session must prevent premature exchange.
        }

        return new PlaidSessionState(
            hasLinkSessions,
            hasFinishedSession,
            hasUnfinishedSession,
            hasExitedSession,
            publicToken);
    }

    private static string?
        GetPublicTokenFromResults(
            JsonElement linkSession)
    {
        if (!linkSession.TryGetProperty(
                "results",
                out var resultsElement) ||
            resultsElement.ValueKind ==
                JsonValueKind.Null)
        {
            return null;
        }

        if (resultsElement.ValueKind !=
            JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                "Plaid returned an invalid Hosted Link result.");
        }

        if (!resultsElement.TryGetProperty(
                "item_add_results",
                out var itemAddResultsElement) ||
            itemAddResultsElement.ValueKind ==
                JsonValueKind.Null)
        {
            return null;
        }

        if (itemAddResultsElement.ValueKind !=
            JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                "Plaid returned an invalid Hosted Link result.");
        }

        if (itemAddResultsElement.GetArrayLength() >
            MaxItemAddResults)
        {
            throw new InvalidOperationException(
                "Plaid returned too many Hosted Link item results.");
        }

        string? selectedToken = null;

        foreach (var itemResult in
                 itemAddResultsElement.EnumerateArray())
        {
            if (itemResult.ValueKind !=
                JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    "Plaid returned an invalid Hosted Link item result.");
            }

            var candidate =
                GetOptionalPublicToken(
                    itemResult,
                    "public_token");

            if (candidate is null)
            {
                continue;
            }

            // Multiple items or conflicting credentials cannot safely
            // become one FullWorth bank connection by arbitrary ordering.
            if (selectedToken is not null &&
                !string.Equals(selectedToken, candidate,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Plaid returned conflicting Hosted Link public tokens.");
            }

            selectedToken = candidate;
        }

        return selectedToken;
    }

    private static string?
        GetPublicTokenFromLegacySuccess(
            JsonElement linkSession)
    {
        if (!linkSession.TryGetProperty(
                "on_success",
                out var onSuccessElement) ||
            onSuccessElement.ValueKind ==
                JsonValueKind.Null)
        {
            return null;
        }

        if (onSuccessElement.ValueKind !=
            JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                "Plaid returned an invalid Hosted Link success result.");
        }

        return GetOptionalPublicToken(
            onSuccessElement,
            "public_token");
    }

    private static string?
        GetOptionalPublicToken(
            JsonElement element,
            string propertyName)
    {
        if (!element.TryGetProperty(
                propertyName,
                out var publicTokenElement) ||
            publicTokenElement.ValueKind ==
                JsonValueKind.Null)
        {
            return null;
        }

        if (publicTokenElement.ValueKind !=
            JsonValueKind.String)
        {
            throw new InvalidOperationException(
                "Plaid returned an invalid Hosted Link public token.");
        }

        var publicToken =
            publicTokenElement.GetString();

        if (string.IsNullOrWhiteSpace(
                publicToken))
        {
            return null;
        }

        if (publicToken.Length >
                MaxPublicTokenLength ||
            publicToken.Any(
                char.IsControl))
        {
            throw new InvalidOperationException(
                "Plaid returned an invalid Hosted Link public token.");
        }

        /*
         * Public tokens are opaque credentials. Do not trim or otherwise
         * normalize them.
         */
        return publicToken;
    }

    private static bool HasExitResult(
        JsonElement linkSession)
    {
        if (TryReadOptionalObjectPresence(
                linkSession,
                "exit",
                out var hasExit) &&
            hasExit)
        {
            return true;
        }

        return TryReadOptionalObjectPresence(
                   linkSession,
                   "on_exit",
                   out var hasLegacyExit) &&
               hasLegacyExit;
    }

    private static bool
        TryReadOptionalObjectPresence(
            JsonElement element,
            string propertyName,
            out bool isPresent)
    {
        isPresent =
            false;

        if (!element.TryGetProperty(
                propertyName,
                out var propertyElement) ||
            propertyElement.ValueKind ==
                JsonValueKind.Null)
        {
            return false;
        }

        if (propertyElement.ValueKind !=
            JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                "Plaid returned an invalid Hosted Link session record.");
        }

        isPresent =
            true;

        return true;
    }

    private static bool
        HasNonEmptyOptionalString(
            JsonElement element,
            string propertyName)
    {
        if (!element.TryGetProperty(
                propertyName,
                out var propertyElement) ||
            propertyElement.ValueKind ==
                JsonValueKind.Null)
        {
            return false;
        }

        if (propertyElement.ValueKind !=
            JsonValueKind.String)
        {
            throw new InvalidOperationException(
                "Plaid returned an invalid Hosted Link session record.");
        }

        return !string.IsNullOrWhiteSpace(
            propertyElement.GetString());
    }

    private async Task<PlaidHostedLinkCompletionResult>
        SetTerminalStatusAsync(
            PlaidLinkSessionEntity session,
            PlaidLinkSessionStatus status,
            PlaidLinkCompletionAttemptEntity claim,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            session);

        if (status ==
            PlaidLinkSessionStatus.Pending)
        {
            throw new ArgumentException(
                "Terminal status cannot be Pending.",
                nameof(status));
        }

        var now =
            _timeProvider.GetUtcNow();

        session.Status =
            status;

        session.CompletedAtUtc =
            now;

        session.UpdatedAtUtc =
            now;

        /*
         * Terminal sessions no longer need a usable Link credential.
         */
        session.ProtectedLinkToken =
            string.Empty;

        PlaidLinkCompletionAttemptStore.PrepareCompletion(
            _dbContext, claim, session, now, requireUnexpiredSession: false);
        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return CreateResult(
            session);
    }

    private sealed class CompletionEntrySnapshot
    {
        private readonly EntityEntry _entry;
        private readonly PropertyValues _current;
        private readonly PropertyValues _original;
        private readonly EntityState _state;
        private readonly HashSet<string> _modified;

        public CompletionEntrySnapshot(EntityEntry entry)
        {
            _entry = entry;
            _current = entry.CurrentValues.Clone();
            _original = entry.OriginalValues.Clone();
            _state = entry.State;
            _modified = entry.Properties
                .Where(property => property.IsModified)
                .Select(property => property.Metadata.Name)
                .ToHashSet(StringComparer.Ordinal);
        }

        public void Restore()
        {
            _entry.CurrentValues.SetValues(_current);
            _entry.OriginalValues.SetValues(_original);
            _entry.State = _state;
            if (_state == EntityState.Modified)
            {
                foreach (var property in _entry.Properties)
                {
                    property.IsModified = _modified.Contains(property.Metadata.Name);
                }
            }
        }
    }

    private static PlaidHostedLinkCompletionResult
        CreateResult(
            PlaidLinkSessionEntity session)
    {
        ArgumentNullException.ThrowIfNull(
            session);

        return new PlaidHostedLinkCompletionResult(
            session.Id,
            session.Status.ToString(),
            null);
    }

    private sealed record PlaidSessionState(
        bool HasLinkSessions,
        bool HasFinishedSession,
        bool HasUnfinishedSession,
        bool HasExitedSession,
        string? PublicToken);
}

public sealed record PlaidHostedLinkCompletionResult(
    Guid SessionId,
    string Status,
    PlaidConnectionResult? Connection);
