# Account-security response boundaries — 2026-10-28 UTC

## Confirmed defects

An isolated execution of the actual development account-security.js accepted HTML200 as a successful email update and password change, and the latter navigated to login. A non-string requestToken object also reached the POST header options. No real account or credentials were used.

The API's email-verification endpoint deliberately returns204 when the account is already verified and200 JSON after requesting email delivery. The old Settings handler ignored that distinction and always announced a sent email.

## Implementation

All account-security/token/status Fetch calls explicitly reject redirects. Tokens must be bounded, nonblank ASCII header values. Reads require200 application/json and their documented security/provider state shape before replacing current state.

Each of the12 update operations declares its completion contract: password/session/2FA-disable/external-link/unlink require204; profile/email/setup/reset/recovery-code operations require200 JSON with the corresponding fields. Email-verification alone permits200 or204. JSON media types are normalized exactly; HTML, misleading prefixes, malformed/scalar/null/wrong-shape JSON,202 and redirected responses cannot signal success.

A lost or unexpected write response raises a fixed uncertainty message. Settings maps only that known message to EN/ES instructions to reload and check current state; arbitrary response/parser/transport text is never displayed. No application write retry was added. Existing credential-error/session distinctions remain. A failed status refresh following a confirmed external-identity write also instructs a state check.

The email-verification helper returns whether a send was confirmed. For204, Settings refreshes security state and reports that the email is already verified instead of claiming delivery. This is request/response evidence, not proof of arrival in an inbox.

## Executed source validation

127 Node regressions pass against the corrected complete module in isolated execution; source-file loading is remapped in memory, with synthetic DOM/HTTP only. The initial suite against original source passed12/failed115; those are contract assertions, not115 independent product defects. The original HTML/token behavior was separately reproduced directly.

40 loopback HTTP scenarios passed in locally installed Chromium151, covering all12 actual exported update operations, valid completion, HTML ambiguity, redirect status301/302/303/307/308 without forwarding to targets, invalid tokens, malformed/accepted responses, dropped response bodies and credential401. The existing read-recovery helper also passed against corrected source.

The protected browser workflow now runs the same HTTP suite in its pinned Chromium/WebKit environment. The real disposable Settings browser journey adds EN/ES ambiguous-email dialogs and already-verified responses, checks one explicit POST plus read-only state refresh, no fabricated success/navigation/private canaries, and captures the rendered messages. These final-head CI results remain required before integration; they are not predeclared by the local probe.

## Scope and limits

The no-replay assertion applies to application Fetch calls. A separate pre-header socket-reset probe on local Chromium151 produced three HTTP arrivals from one application call, demonstrating browser transport retries. The regression drops a response after headers/body start so it can test an unknown completion without confusing that transport policy with application retry logic. This change does not claim server-side exactly-once processing; review existing sensitive-operation replay/idempotency behavior separately.

No API/Identity policy, credential, session, MFA, email delivery configuration, grant, production account, paid capability or database schema was changed by the validation. CI uses disposable synthetic accounts. Real provider/delivery/device/two-owner/current-release recovery/legal acceptance gates remain open. Development integration is not deployment.
