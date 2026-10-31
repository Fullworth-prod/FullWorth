# Discovery currency-ambiguity retention — 2026-10-27

Mixed or partly unknown transaction units already prevented new discovery. Existing automatic bills could nevertheless be deactivated and their evidence unlinked when an unrelated import window satisfied the minimum source-account coverage. Contradictory units do not prove an obligation ended.

Initial804b0d8 failed compilation because its test assumed BillTransactionAssociationEntity had an Id. The entity uses a composite key; that fixture error is not a product defect. Corrected baseline840a0f5ad4344fba5fc8eac62c3cbfb345ac4ae6, CI40465193886/backend122058773464, actually ran2,895 .NET tests: three intended behavioral failures,2,892 passed, zero skipped and zero build warnings/errors. EUR, missing and malformed third-payment currency each unexpectedly deactivated one existing bill.

Reuse the existing ambiguity retention set for both source-account and currency evidence. Rename the private set to AmbiguousEvidenceProviders so its meaning stays accurate; add rejected-currency providers before continuing. Existing diagnostics and conservative new-candidate rejection remain unchanged. No change to legitimate negative category handling, cadence, currency conversion, transactions or manual bill ownership.

Regressions verify exact composite association keys, preserved active bill/source currency/no new alert, and normal rediscovery after the source currency is genuinely repaired. The implementation does not repair or guess source currency itself. Corrected final-head execution is pending.

The accompanying PWA budget document fills the roadmap's lightweight measurement-plan gap using source-observed counts/bounds, naming unmeasured runtime/device metrics explicitly. It adds no instrumentation, cache, analytics, credential or production behavior.

No real missing-bill/private statement/provider/device/production acceptance is established by these synthetic fixtures. Preserve the full cleanup objective and current branch requirements.
