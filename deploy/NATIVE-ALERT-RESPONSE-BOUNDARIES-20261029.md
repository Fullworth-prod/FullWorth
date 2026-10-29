# Native alert read and mutation receipts — 2026-10-29 UTC

## Demonstrated behavior

The active native BillAlertService previously returned an empty list for HTTP200 JSON null and accepted malformed identities, duplicate records, pending202 and wrong-media JSON reads. MainPage then replaced previously verified warning/unread counts with a healthy empty account. Native Activity also calls this service and can present its nothing-needs-attention state after that false-empty read.

The same service accepted every2xx response for read/dismiss, although both owned API endpoints in BillAlertsController declare empty204 receipts after persistence (including already-completed idempotent operations). A200/202/205 or204 with unexpected content must not be treated as confirmed completion. This is a demonstrated source boundary, not a report of actual provider incidents.

## Correction

Require HTTP200 application/json with a nonnull alert array, nonnull rows, unique nonempty IDs, nonblank type/severity/title/message and supplied creation/update timestamps. These minimum record requirements mirror the existing Web Activity snapshot guard, without restricting future enum names or additive JSON fields. Valid empty arrays remain valid. Error messages are fixed, never raw response content.

Require the declared204 and an empty response stream after successful read/dismiss status handling. Probe at most one byte of the response stream to detect unexpected content. Existing failing-status exceptions, argument validation, authenticated requests and cancellation remain. There is no automatic retry, write replay, new UI success claim, API mutation, account access or real delivery claim.

The dashboard's existing error handling preserves its prior snapshot and hides current content after an invalid refresh; it cannot clear attention as if that invalid read proved a healthy account. Activity's existing error handling shows its load error instead of its empty feed. Its dismissal error now explicitly says the update could not be confirmed and asks for an Activity refresh before retrying. No MAUI layout/theme redesign is introduced.

## Actual regression evidence

30 focused xUnit cases pass in isolated actual-source execution using BillAlertService, FullWorthApiClient, AuthenticationService, BillStreamService and MainPageViewModel with disposable network/storage/Command/Shell substitutes. They cover13 invalid-read modes, valid empty and additive/future-type reads, six unexpected mutation statuses, two content-bearing204 responses, two correct204 receipts, two failed writes without replay, two invalid IDs before network and an invalid refresh following a verified attention snapshot.

Original alert source passes nine and fails21 controlled cases; malformed JSON/null-row failures already reject in the old source and are compatibility controls, not newly discovered incidents. The combined financial-dashboard29 plus alert30 suite passes59. Test sources use only synthetic access strings and fake endpoints, not real credentials or private financial data. Namespace/implicit-using/XAML lookup adapters are local execution aids, not a full SDK, OS navigation or physical-device acceptance.

This follow-up builds on #1013's actual native service links. Final-head protected backend/Android/container/dependency and applicable packaging/relational gates must pass before development integration. Record exact source/tested/integrated trees and actual final results in the handoff.

No production deployment/master promotion, grants/secrets/account changes, real outreach/delivery, spending, private export or destructive operation. External provider/device/recovery/privacy/legal acceptance issues remain open. MAUI retirement remains deferred until parity/support and a deliberate decision.
