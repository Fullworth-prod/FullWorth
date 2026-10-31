# Statement category evidence alerts — 2026-10-27

Baseline 670221ddb0213828bae207efe37398dc9b6463f8, CI40463244608/backend122052519507 executed 2,878 .NET tests: eight intended regressions failed, 2,870 passed, zero skipped, zero build warnings/errors. Same-description fee/tax/unknown rows inherited the first non-null category and attributed their combined amount to that category. Offsetting mixed rows could claim an existing fee was new or a still-present discount had disappeared. Padded homogeneous labels also lost valid typed alerts.

Require a normalized, unanimous nonblank category for each description group. Once a group becomes ambiguous, a later row cannot restore a category. Ambiguous prior evidence cannot prove a fee is newly present, and ambiguous current evidence cannot prove a discount disappeared. Preserve raw line items, original currency and the independent deterministic total-change calculation. Do not infer recurrence or merchant intent.

Regression coverage includes row-order/case/whitespace/missing-category permutations, offsetting fee/discount amounts, homogeneous totals, and actual statement reconciliation: newly conflicting evidence retracts only the unsupported typed alert while retaining the same general increase alert, accurate amount and source rows across repeated reconciliation.

Corrected final-head execution is pending. These tests do not prove external alert delivery, private statement acceptance or a production deployment.
