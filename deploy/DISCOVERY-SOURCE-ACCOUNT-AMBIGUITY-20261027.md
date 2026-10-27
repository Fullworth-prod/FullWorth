# Discovery source-account identity — 2026-10-27

Baseline9e3b138b5f5d012e6c929dab214f781cb2c6c3b1, CI40461125656/backend122045737949: two intended negative cases failed;2872 passed/0 skipped. A merchant's January payment from one account and February payment from a separate/unknown account were accepted as one automatic monthly bill. This is a synthetic deterministic identity defect, not proof of the owner's private missing-bill cause.

Reject those mixed-source monthly candidates before persistence and expose only an aggregate count with EN/ES guidance. Merchant names cannot establish account/obligation identity. Existing automatic bills/links are retained when such an ambiguous candidate exists, even if the old minimum history-coverage threshold is met. Single-source cadence, foreign-owner isolation and repeated scan behavior remain covered. Currency/category/monthly/ownership gates are retained; no cadence expansion or auto-confirmation.

Four old single-account fixtures generated a new opaque account ID per payment. They now use one stable per-owner fixture account; numeric, cadence, ownership, rollback and retention assertions are unchanged. Account-specific tests still supply distinct explicit IDs. All-unknown legacy source metadata is not claimed to prove unique account identity; this fix addresses conflicting/partially unknown evidence.

Corrected-source .NET/browser/protected verification is pending. #762 real private expected-payment/rejection acceptance remains open. No bank sync, production change, account mutation or private financial export.
