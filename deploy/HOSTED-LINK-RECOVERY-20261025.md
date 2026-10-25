# Hosted Link consumed-token recovery — issue #912

Status: implementation and verification candidate in [PR #958](https://github.com/Fullworth-prod/FullWorth/pull/958), based on development `ce6cea275968b7c2e3b07427fca9dd38acafcf28`. This is not deployed. Final-head protected CI, relational fault tests and browser results must be read before integration. Real Plaid Sandbox and installed-device acceptance remain open.

## Failure contract

A provider public token can be consumed before the final database outcome is known. FullWorth cannot roll back the provider operation or safely retry that token. The durable permit prevents the SAME Hosted Link session from initiating a second exchange. It does not promise exactly-once behavior by Plaid, nor apply to an unrelated direct-exchange API.

Each permit is identified by session, owner and a unique attempt nonce. A composite database foreign key binds the session and owner; stage and nonce are concurrency predicates.

1. Read owner-scoped durable session/bank evidence before any provider call. A committed completion is replayed from that evidence, with the bank's present status checked.
2. Claim Polling using an independent context/connection. A competing caller returns Pending. Read-only pending/error outcomes may release Polling; no public token has been consumed.
3. Before public-token exchange, atomically persist Exchanging and clear the protected Link token. A missing acknowledgement grants access only if an independent read proves this exact nonce, stage and cleared pending session.
4. Persist the owned bank, terminal session and Completed permit together. An interrupted or stale permit prevents an old writer from committing.
5. A failure after the exchange fence retains Interrupted metadata and cleared Link token. RecoveryRequired is terminal for client polling. No automatic exchange or new Link session is attempted.
6. After a lost final commit acknowledgement, the next explicit status check reads the committed session/bank and returns that result without another provider operation.

The six-minute permit lifetime bounds abandoned work and allows the three provider calls' individual 100-second deadlines plus commit time. It is a fail-closed limit, not a guarantee that arbitrarily slow database operations will finish. A stale permit becomes recovery-required and prevents a late commit. Cleanup uses a bounded independent metadata operation; it never replays a provider request or commits the caller's pending work.

The original session/attempt tracking is restored or detached on a failed completion. Unrelated owned/foreign transaction history, monetary amounts, currencies, cursors and last successful sync are not rewritten for recovery. No plaintext provider credential is stored in the permit table.

## Explicit user recovery

Web recognizes RecoveryRequired as terminal and displays localized English/Spanish guidance with a Review connected banks link. Review current bank state first. If the connection is absent or still needs attention, the owner can explicitly choose Connect or Reconnect to create a new secure session. The previously fenced session is never silently reused.

A failed/uncertain Web completion response also offers bank review before a fresh session. Missing/invalid completed-bank evidence cannot produce a success message. Native guidance directs the user to refresh connected banks before an explicit Connect/Reconnect action. Actual installed-phone acceptance remains required; a compiled application or browser fixture is not that evidence.

## Verification surfaces

- `FullWorth.Tests/Services/HostedLinkRecoveryTests.cs`: concurrent requests, post-consumption item/save/cancellation failures, committed-result replay, safe read-only retries, stale permit interruption, cross-owner metadata isolation and preserved history. Runs against InMemory normally and guarded disposable PostgreSQL in its dedicated job.
- `FullWorth.Tests/RelationalHostedRecovery/`: a separate executable requiring the explicit fixed loopback database fixture. Six network faults cover new/existing connections at permit insert, exchange fence and final completion. The proxy withholds CommandComplete until real server ReadyForQuery, then drops both sockets so the client loses the acknowledgement after server completion.
- That executable also injects two real PostgreSQL trigger failures before final completion, verifies bank/session rollback with an interrupted token fence, checks composite-owner FK rejection and unloaded-session cascade, and verifies owned account deletion preserves foreign bank/history/session/permit state. Two further scenarios interrupt the permit AFTER the old writer prepares its final save; the database concurrency predicate must reject that entire bank/session transaction. Twelve relational scenarios in total; provider responses are synthetic.
- `FullWorth.VisualTests/hosted-link-recovery.test.mjs`: actual BFF/polling/DOM behavior in Chromium and WebKit. English/Spanish, Connect/Reconnect, recovery-required/uncertain-response/invalid-completed cases. No automatic retry, no false connected state, usable bank-review link, explicit fresh-session creation.
- Existing durable replay, bank lifecycle, monitoring, bill confirmation, ownership, backend/model, Node, Android, container and browser gates remain required. New fault tests do not replace those suites.

Actual executed counts and run links belong in the PR and final handoff only after reading their results. Initial head failures exposed cross-context graph attachment and a missing ownership-evidence registration; both were corrected without weakening the guards. A subsequent ownership assertion exposed PostgreSQL microsecond precision; it now compares the actual stored timestamp before/after the unauthorized call.

## Migration and release boundary

`20261025020500_AddPlaidLinkCompletionAttempts` adds the session-owner alternate key and owner-bound attempt table with cascade dependency. It stores no raw provider token. Account deletion includes owned metadata. The EF model/snapshot must match and the migration must execute against disposable PostgreSQL before release.

No production migration is authorized or performed here. A later separately authorized release must verify its backup/rollback procedure and database lock budget and prevent mixed old/new Hosted Link writers from bypassing the new exchange fence. An old application version does not enforce this new protocol. Never delete or reset an Exchanging/Interrupted permit to make the same public token retry.

## Acceptance still open

- Controlled Plaid Sandbox terminal/error modes with a disposable owner and metadata-only evidence; no real customer data or credentials in issues/logs.
- Physical Android/iPhone flow, browser popup/navigation, interrupted network and explicit recovery UX.
- Actual production migration/deployment and same-release operational/recovery acceptance under their existing authorization and safeguards.

Issue #912 remains open until its provider/device/security acceptance is supported. No master promotion, deployment, paid activation, secret change, private-data export or production recovery claim accompanies this work.
