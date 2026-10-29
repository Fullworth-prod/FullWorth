# Native Bills list financial evidence — 2026-10-29 UTC

## Demonstrated inconsistency

After #1013 corrected native Overview, BillsPageViewModel still computed a material decrease from missing/default/nonpositive current evidence, and still classified unsupported currency changes as Changed. The list could show an unsupported per-cycle/annual amount while Overview withheld the same claim. These are actual-source synthetic counterexamples, not evidence of five customer incidents. The API normally returns active debit-based metrics; this guard also prevents incomplete/contradictory financial fields in an otherwise identified snapshot from becoming facts.

## Correction

Require explicit current transaction evidence, a positive current debit and a usable raw currency before calculating a list change or including an amount in its latest recurring total. Preserve the existing previous-average>0, rounded absolute>=5 and relative>=10% materiality rules. Unknown current evidence cannot establish a zero balance, a reduction or an annual estimate. Unsupported changes remain unavailable and are not labeled Changed; valid rows and provider/category/history context remain.

BillListItem also guards its current, change and annual text so a hidden unsupported value cannot become a financial claim if reused. Reliable monthly evidence is still required for a yearly rate; known changed amounts with unknown cadence retain their amount/change without an invented annual projection. Currencies remain separate, with no conversion. Existing signed-money style is retained. The actual list heading is LATEST RECURRING TOTAL; its legacy MonthlyTotal property is not relabeled into an unsupported monthly-rate claim.

## Actual verification

14 focused xUnit cases pass through the real BillsPageViewModel/API client/auth/bill service. Five unsupported-amount/unit cases fail original source; nine compatibility cases pass. Cases compare public Overview and Bills results for the same inputs, preserve supported monthly reduction math, unverified-cadence behavior, five existing materiality/baseline boundaries, separate currency totals and prior-list retention after an invalid refresh. The dashboard29 + alert30 + list14 actual-source suites pass73 together.

Only network/device storage/Command/Shell and local namespace/implicit-using/XAML lookup execution adapters are substituted. The first local positive-control expectation was corrected to the existing dollar-before-minus formatter style; production formatting was not changed. Local execution is not a full SDK, MAUI rendering, physical device or real-provider acceptance. The test project links the real list source, with the test directory excluded from the production MAUI application.

This follow-up is based on the completed native dashboard/alert work and requires its own exact-head protected backend/Android/container/dependency and applicable packaging checks before integration. Record final source/tested/integrated trees and results in the handoff. No API persistence, bank/provider behavior, ledger/history retagging, financial export, theme redesign or MAUI retirement.

No production deployment/master promotion, spending/paid activation, new grants/secrets/account changes, outreach/delivery, destructive operation or private financial-data access. Provider/expected-bill/payroll/device/actual delivery/current nonempty real recovery/both-keyring/protected-backup/two-owner/security/legal acceptance remains separately open.
