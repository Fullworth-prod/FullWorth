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

## Server boundary corrections and combined follow-up

Actual compiled original proxy execution showed that200 password and202 session-revocation responses each signed out the Web session. It also relabeled HTML200, malformed JSON200 and pending JSON202 as JSON export files. All probes used an in-memory HTTP handler, synthetic authentication and synthetic bytes only.

The proxy now signs out only for the documented204, with the explicit deletion202 exception for already-removed accounts awaiting file cleanup. Other successful statuses return a safe502 without reflecting the untrusted body or signing out. Non-success responses retain their status.

Account-export forwarding now requires200 and the actual upstream application/json media type, then checks a complete JSON-object envelope using Utf8JsonReader on the existing byte buffer. It rejects HTML/malformed/scalar/array/empty/trailing/deep JSON and unsupported media/status responses without creating a file. Valid original bytes, Unicode and the selected filename/content type are preserved. This validates transport/JSON syntax, not the completeness or ownership correctness of every export field; existing API owner/proof/export tests retain those responsibilities. No extra private-data object graph or JSON logging is created.

All57 cases in the actual compiled proxy test class passed, including37 new regressions. This local execution used ASP.NET10.0.14 with PowerShell's .NET10.0.11 and existing xUnit2.9.3 assertions invoked directly, with implicit usings/namespace and unused ticket-store constant adapters. Assembly compatibility warning1701 was suppressed only for that isolated Add-Type host. Normal protected builds/tests keep their existing warning policy; full solution CI is still required.

The browser module also treats5xx write responses as uncertain outcomes and no longer labels every503 read failure as email delivery trouble. The expanded139 Node cases and41 local Chromium151 HTTP scenarios passed in isolated execution. Pinned both-engine and full rendered acceptance must run on this new combined head.

Initial de693120d21690ab29433969cbb1211dd8495195 CI actually passed2,966 .NET/356 Node, zero failed/skipped/backend/offline warnings/errors, Android with zero warnings/errors, and40 HTTP scenarios per pinned engine. Its full visual run failed a new test's read baseline (10 versus9) after EN/ES ambiguous-email messages rendered: the counter was captured before initial security loading finished. The test now waits for the real Send button before recording the baseline; it retains exact one POST/one action-refresh assertions. No unchanged failed job was rerun and no gate was relaxed. Subsequent virtual-key/HTTP/disposable restore steps were skipped after that failure and are not claimed passed for that head.

## Disposable owner-setup readiness follow-up

Source487451aecb8fc13974f3d2606f1aa13c124ba581 passed3,003 .NET/368 Node, zero failed/skipped/backend/offline warnings/errors, Android and41 account HTTP cases per pinned engine. Its full visual run stopped earlier than the Settings cases: WebKit disposable-owner registration returned302, but no login POST/response was observed and login navigation timed out. The existing setup used900ms settling sleeps and separate fill/click commands. This is not evidence of a customer login or account-response defect.

A loopback form-replacement counterexample reproduced lost fields/no POST with the old split-call setup. Seven local Chromium151 scenarios pass with a new test-only helper that verifies loopback origin, native POST action, current antiforgery token, required fields/consent and native constraint validity, then fills and requestSubmits once in the same browser task. Registration/login still must each return302 and create the real disposable Web session; no backend auth/provider/limit interception or write retry is added. The browser workflow runs this counterexample/helper suite in pinned Chromium and WebKit. Missing proof, invalid fields, unsupported action and live-host misuse are refused.

The subscription fixture uses this helper for its independent synthetic WebKit owner, preserving all54 real BFF/database assertions and logging only JSON-stringified path/status/validity metadata on setup failure. Native auth-form user-interaction acceptance is separate from this setup helper; no production auth markup/rendering behavior is changed. The existing Settings count assertion still waits for the actual Send button before recording its baseline.

Failed487 artifact12803821617 SHA256dd17a0e8f892789b48ab15d6d1c6f4fcd327c0ebcc2450f876f4de2397ed9c8c was downloaded and verified. Earlier de693 artifact12803172417 SHA25684bafeee11b92856c448eecb7e8d09cf4537ed0582191621390b6b136c95b0f7 was also verified; EN/ES ambiguous-email messages and English already-verified state were inspected. Neither failed run establishes full combined browser/virtual-key/HTTP/restore acceptance. Fresh final-head CI remains mandatory.

Source5a00daeb74b6bc1405675b43c66fee382ef13d93 passed3,003 .NET/368 Node, Android,7 setup and41 account HTTP scenarios per pinned engine. Its full visual run again reached real subscription checks (54 Chromium assertions passed) but WebKit setup failed before filling/submitting: the helper's form-readiness guard rejected the evaluated form; current-page email/password fields remained empty and no login POST was observed. No successful full UI/HTTP/restore outcome is claimed for it.

The test-only helper now treats a disconnected pre-write form as a bounded read-only reattachment case (40×25ms maximum); it never retries after invoking requestSubmit or after an exception. Method/origin/path, antiforgery, native validity and consent checks remain strict. An actual detached ElementHandle replacement regression passes locally, bringing the setup suite to8 cases per engine, pending new-head pinned verification. An action mismatch still fails and now logs only method/origin/path metadata to distinguish it from detachment without exposing values/tokens. This is an observed setup-readiness failure and a tested defensive correction, not proof of a production authentication defect.
