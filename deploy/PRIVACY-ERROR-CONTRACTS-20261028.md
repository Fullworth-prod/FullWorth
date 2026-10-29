## Verified integration status — 2026-10-29 UTC

Verified in development through #1007 as0c89f56b76f345607d193cb99fb2e791bc0e5704. Final sourcef4ddfb70ddd56f3f3d5ad43889be03182f319108/tested2ce79a61f5263928bb01de27d5a86f22d949f11c/tree73414e6bafc1c84d7ef5c96dd788ac0d92f50f5b equal; all5 checks passed.3,507 .NET/663 Node,94 privacy HTTP cases per pinned engine, full rendered EN/ES uncertainty/proof-clearing/no-replay passed. This is synthetic controlled acceptance, not a real account deletion/export.

The detailed current ledger is in MILESTONE-CLEANUP-AUDIT-20261027.md. Original preparation notes below are preserved as history; this documentation change requires its own checks. No production deployment or new real acceptance is implied.

---

# Privacy error response contracts — 2026-10-28

## Reproduced failure and source contracts

Actual shipped privacyError execution relabeled500/502/503/504 bodies containing proof-like phrases as incorrect password/authenticator or definite account nondeletion. The caller then showed a specific cause instead of preserving the uncertain result. Token-fetch failures could also blame password proof even though that request carries none. No real account/export/deletion or private credentials were used.

Source review confirmed valid current API contracts that must remain useful:401 proof errors,409 staff-role conflict,503 bank-revocation failure before local deletion,500 statement-quarantine preparation failure before the transaction. These pre-deletion response bodies are application/json objects with only message. The controller locations were inspected; synthetic client tests are not real provider or account-deletion acceptance.

## Correction

Bind known error interpretation to the operation, status and current declared envelope. Only export/deletion401 responses can identify exact known proof errors. Only deletion responses with the exact current409/503/500 message in a one-property JSON message object can report their documented pre-deletion condition. Problem-detail titles, extra contradictory fields, changed/substr-only messages, wrong status/MIME/operation, redirected responses and unrelated failures remain generic/uncertain.429 takes precedence over body text. Token failures cannot claim incorrect proof.

This intentionally matches the current API's English response contract. A future contract/copy change fails closed and must update both sides/test evidence together; the browser never reflects arbitrary response bodies. Keep existing localized fixed UI strings, proof clearing, session behavior, response validation and no application write replay. Direct202 cleanup-pending/204 and export download contracts are unchanged.

## Verification

212 new Node cases cover status/operation/envelope binding, exact proof controls, documented pre-deletion receipts, mismatches, irrelevant-body short circuits and actual-module no-replay/token behavior. All212 passed locally, as did the prior66 boundary cases and existing lifecycle/cancellation/object-URL suite.

Extend the actual-module isolated HTTP browser suite from59 to94 cases per engine: server proof-like failures, documented401/deletion controls,429 priority and token401 no-write. All94 installed-Chromium cases passed locally with source imports remapped in memory; pinned Chromium/WebKit require own final-head CI. Update the rendered staff fixture to the actual API receipt and add real rendered EN/ES server-failure uncertainty/proof-clearing/no-replay checks with screenshots. Those application render checks have not yet run.

## Gates and boundaries

Require final-head protected/backend/native/container/pinned-browser/rendered privacy checks, inspect any failures and verify rules/reviews/readbacks/integrated tree before development integration. No real proof/export/delete, provider/device/current production recovery/legal acceptance, deployment/master promotion, new credentials/grants/account changes, spending, outreach, destructive operations or hourly automation. Preserve branches and the full active goal.

## First-head verification and corrected test contract

Initial f761 backend CI40758198000 ran3,362 .NET cases:3,361 passed and one source-text ordering assertion failed (WebPwaBoundaryTests line376); the Node step did not run. The helper now scopes status handling before proof classification, so the old lowercase-string/index test does not establish actual behavior. Retain every reauthentication/CSRF/no-cache/no-navigation assertion, explicitly verify the export context is passed, and move precedence coverage to six actual-module401 proof cases in the required Node gate (218 new Node cases total). No implementation comment or weakened check is used to game string positions. Prior pinned browser job123062687379 passed its scenarios; the revised head still needs full own CI and rendered EN/ES proof.
