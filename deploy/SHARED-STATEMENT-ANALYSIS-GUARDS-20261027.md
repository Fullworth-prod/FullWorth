# Shared statement analysis coverage guards — 2026-10-27

Scope: shared financial analysis, no production or recurrence expansion.

The API statement reconciler already requires consecutive complete monthly coverage. Shared BillStatementComparisonService/BillAnalysisService did not: quarterly/annual/partial/overlapping/gapped/same-period/reversed direct calls could produce MonthlyChange and AnnualChange despite unsupported periods. BillStreamAnalysisService could throw on mixed currency instead of returning its existing nullable no-analysis outcome.

Baseline source2162422a08a1b88ab3f2e0df87a8600973c9e103, CI40459872063/backend122041663461: actual15 failures/2873 passed/0 skipped out of2888. Seven direct unsupported-period cases, six genuine stream unsupported-period cases and mixed-currency stream failure establish14 intended failures. The fifteenth was a mistaken negative fixture: BillStream intentionally sorts reversed input, so consecutive monthly coverage is still valid. That fixture is corrected to a positive chronological-sort regression; it is not counted as a product defect.

Fix: enforce the existing shared MonthlyStatementPeriodPolicy in direct comparison; return null from stream analysis for unsupported coverage/mixed currency. Preserve valid month-end and leap-year billing anchors and deterministic original-currency arithmetic. Existing currency explanation fixture now uses consecutive actual months instead of duplicate January coverage. No API change, schema migration, currency conversion, new cadence or automatic renewal inference.

Final corrected source tests/protected CI/native packaging remain pending. No real-provider/device/recovery/legal acceptance or deployment is established by these fixtures.
