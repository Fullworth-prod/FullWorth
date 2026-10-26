# Bank connection completion survives browser interruption — #972

## Observed production symptom

The owner reported Wells Fargo showed successful connection in Plaid but did not appear in FullWorth.
Read-only inspection at deployed release 3fd3aa5e019d35f11418c526aeaca3d772bd37ea found healthy
API/Web/runtime permissions and readiness. A recent initial Hosted Link attempt remained Pending
with an Interrupted exchange fence, its protected Link token cleared and no newly persisted bank.

Safe outbound logs show successful status polling and token exchange followed by an item lookup
without a completed response log; browser circuit disposal/disconnect events immediately follow.
The evidence supports interruption during finalization; it does not establish a bank outage,
bad user credentials, the contents of the response or universal provider acceptance.
No private account/transaction values, session IDs or tokens are recorded here.

## Reproduction and correction

Four regressions cancel the original browser request during the one-time token exchange or
the subsequent item lookup, for new and existing owned bank records. The unmodified deployed
implementation forwards that cancellation into the exchange chain, leaving the consumed-token
permit interrupted and no completed bank. Record actual red/green execution evidence in the PR.

Before the durable exchange fence, the incoming request cancellation remains authoritative.
After provider completion has been validated and the final cancellation precheck passes,
the fence, one-time exchange, item lookup and atomic database save use a separate four-minute
server deadline. The request handler awaits this work; it is not fire-and-forget.
A closed tab, disconnected Blazor circuit or lost HTTP client cannot cancel this already
authorized finite finalization halfway through.

The independent deadline is shorter than the six-minute durable permit lifetime. Per-provider
request deadlines and all owner/session/nonce/expiry/concurrency/terminal checks stay intact.
Before-fence cancellation makes zero exchange calls. Independent timeout, genuine provider
failure, database rollback, server termination and lost commit acknowledgements retain existing
fail-closed behavior. Interrupted/Exchanging permits are never removed to allow replay.
Only completed durable results can replay; no one-time provider token is exchanged twice.

Update mode (no one-time new token exchange) and ordinary status polling retain request cancellation.
Existing failed-final-save fixtures still prove rollback; a caller cancellation is not a substitute
for the independent finalization timeout. The timeout regression explicitly fires the finite
server timer and verifies Interrupted state with no false success or provider replay.

## Acceptance and release boundaries

Require focused old-source reproduction, new final-head backend and actual PostgreSQL recovery/
durable replay/network fault checks, protected backend/Android/container/dependency gates,
applicable security review and handoff before development integration.

The production interrupted attempt cannot be safely replayed or manufactured into a connected bank.
The original exchange credential was not durably saved. Do not reset the permit, silently re-exchange
its token, delete existing connections or trigger a real relink. After a separately authorized
guarded deployment, the owner must initiate a fresh secure Link session and verify the bank
appears and initial sync progresses. Synthetic provider tests do not prove a real Wells Fargo
reconnection. Production deployment and live-provider actions are outside this source-fix authorization.

Preserve the separate #970/#971 subscription verification goal and branch. No merchant feature,
paid activation, secret/account-setting change or deployment is part of this correction.

Actual old-source reproduction: `9685da0c10f70ed7c3c7d5b8a25c2d3e5cb57643`, PostgreSQL workflow40246135156/job121327980893: all four new disconnect regressions failed with TaskCanceledException; all16 prior recovery scenarios passed. Corrected-source execution is pending.
