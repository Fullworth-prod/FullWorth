## Verified development integration — 2026-10-29 UTC

Merged as 8166996fcb712fa0424cc2a1c9492b9398f6a802. All five final-head checks passed on source 025b044b1baccadf98ead305e9eb7258d3cef6dd; tested candidate 46380475af1214e9af87ef6f3e87370d3c4a628c and integrated tree 7779e7633c530c52b1d8b197efbc13b99e9dec7c match. Actual main CI40818833945 passed 3,507 .NET/748 Node, zero failed/skipped and zero backend/offline warnings/errors. Pinned Chromium/WebKit account boundary coverage passed47 cases per engine (browser40818833950); privacy/passkey fixtures and full rendered/virtual-passkey/HTTP/parser/disposable encrypted restore passed. Container123266689292 recorded retained-root saved-es and restore verification e508dd4d3c11ba4201e7e854e3aacafd82c96caf0865f2bab32e12d72bccaa4e. Android build steps were conditional. Source branch remains present. Development integration is not deployment or physical/provider/private recovery acceptance.

The pending language in the preparation record below is historical.

---

# Account read errors and rate-limit priority — 2026-10-29 UTC

## Demonstrated current-source behavior

The actual account-security module interprets arbitrary error titles from authenticated settings/sign-in-method GETs as incorrect password, invalid authenticator or conflicting email. Those reads submit no such proof or mutation. The same title mapping precedes429 handling, so a throttled request can misleadingly tell a user to correct credentials or an email.78 controlled cases reproduce these wrong fixed messages against verified current source; they are not78 real incidents. Server-failed credential writes already remain uncertain under #999 and are not the defect addressed here.

## Change

Keep failed security/sign-in-method reads neutral with their existing load-failure messages. Preserve explicit read401 session expiry/navigation and429 rate-limit guidance. Give429 priority before parsing the response body for writes; do not replay the write or navigate as if a change succeeded. Existing400 authenticator enrollment and401 password-rejection API contracts remain supported for writes, and existing redirect/token/strict-success/ambiguous-write guards remain.

No new customer copy is introduced, so existing EN/ES message mapping remains intact. No API, authentication, CSRF, ownership, account state or financial-data behavior is changed.

## Verification

The219-case actual-module Node suite passes locally:139 existing plus80 new (78 negative counterexamples and2 actual API credential-rejection controls). The controller source was inspected to retain its401 password and400 authenticator validation responses. Actual-module loopback HTTP coverage expands41 to47 cases per Chromium/WebKit: neutral503/rate-limited reads for both APIs, write429 priority and valid enrollment rejection. All47 installed local Chromium cases passed; pinned engines/full protected SDK/container checks are still required on the final source.

Tests use synthetic responses, fake HTTP/DOM or disposable loopback contexts. They do not prove real provider/device/private-account acceptance or authorize a private export, production deployment, new grant, spending or automatic write retry. The complete milestone goal/external release gates remain open.
