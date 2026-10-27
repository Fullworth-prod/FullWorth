# Milestone cleanup disposition — 2026-10-27

Active owner goal: finish actionable milestones0–8 work, continue independent bug evaluation, and flag precise external prerequisites rather than invent acceptance. No production/master promotion, account/secret/grant changes, spending, private financial export, destructive work or hourly automation.

## Current implementation candidate — not integrated yet

- #983: enforce shared monthly statement coverage and nullable unsupported/mixed-currency stream analysis. Baseline2162422:14 intended negative failures plus one corrected reversed-input fixture. Correctedc85f24c:2888 .NET +127 Node, zero failed/skipped/warnings; all main/native checks passed.
- #984: early iOS failure phase/result evidence, three bounded app-container read attempts without install/launch replay, five executable lifecycle fixture tests. Source1347ca1:main and actual macOS simulator install/launch/capture passed. Artifact12714586579 SHA256bf43696cb054e5800b46e01ae840d105c0beead293cb2dca546e5fc1ece45f13 downloaded/verified; actual logged-out hosted sign-in screenshot opened. This is simulator/public-hosted-shell proof, not physical iPhone or development Web deployment.
- #985: reject merchant cadence assembled from conflicting/partly unknown source accounts, retain existing bill links and expose aggregate EN/ES diagnostic. Baseline9e3b138:2 intended failures,2872 pass/0 skipped. Corrected035d7d4:2874 .NET/zero failures/skips/warnings; PostgreSQL suites passed; browser/container final verification still running at preparation.

These three independent file sets are combined on #983 with exact source blobs. Full combined-head protected, relational, browser and native checks must pass before development integration. No focused source PR is individually merged yet; preserve branches and accurately disposition represented sources afterward.

## Eight-area disposition and remaining evidence

| Area | Software/evidence already present | External prerequisite to record later |
|---|---|---|
| Bank visibility/sync/reconnect | #973 cancellation fix deployed at recorded9540b26; #958 one-time recovery software integrated; owner/session/replay guards and PostgreSQL fault tests | #972 owner initiates a fresh secure Wells Fargo Link and confirms visible bank/initial sync; #912 controlled provider/error-mode and actual device lifecycle. Never replay the old consumed token or remove accounts to simulate success. |
| Bill discovery | Conservative amount/cadence/category/currency/ownership rules and diagnostics; new cross-source ambiguity correction in this candidate | #762 owner-approved private expected-obligation vs posted/import/coverage/rejection evidence. Aggregate totals cannot identify a private missing bill's cause. Do not publish transaction/account data. |
| Subscription review | #971/#977 independent fields, dated/conflicting evidence, existing exact-owned linked-bill documents/manual confirmation and EN/ES implemented | #970 real subscription/billing-party acceptance; no supported authorized consumer-wide renewal connector established. New provider consent/credentials are outside current authority. No standalone receipt upload or live merchant check is claimed. |
| Statement intelligence | Deterministic parser/OCR/matching/change controls and shared-period guard; existing isolated parser tests | Controlled representative nonempty text/scanned/image statements and known facts on the intended release; verify explanation/matching/math and ownership. CI fixtures are not private semantic acceptance. |
| Alerts | Existing source-backed evidence alerts, reconciliation/deduplication and operations sender; ongoing source review continues | Observe actual receipt at intended destinations under separately authorized delivery; an HTTP success or script completion does not prove inbox receipt. |
| Auth/privacy | Existing session/BFF/CSRF/export/erasure/isolation tests; ongoing source review continues | Controlled same-release two-user resource access and disposable lifecycle/account deletion acceptance; no real-account deletion or private export here. |
| PWA/native | Six-theme Home/Overview browser acceptance and EN/ES/enlarged-state matrix; iOS simulator evidence repair/capture in candidate | #251 physical Android installed PWA and #258 physical iPhone/Safari/Add-to-Home-Screen, keyboard/safe-area/update/session checks. Real device/biometric interaction cannot be supplied by a simulator. MAUI retirement is a deliberate future compatibility decision. |
| Recovery/release | Existing guarded tooling, off-host read-only recovery history and synthetic encrypted restore checks; candidate branches protected | Current authorized recovery access, real nonempty statement/database/both-keyring clean-host/disaster bootstrap and provider-enforced retention proof; record the exact remaining portion, do not call every historical restore missing. New grants/private exports/production changes require separate authorization. Qualified Terms/Privacy/license review requires the qualified reviewer. |

Do not close #970/#972/#912/#762/#293/#251/#258/#291/#669 from these source changes. These are explicit acceptance dependencies, not reasons to stop independent engineering. Milestone6 expansion and creator activation remain deferred/default-off. Branch deletion/cleanup activation in #245 is outside the current non-destructive scope.

## Existing dependency work

#979 proposes .NET10.0.14 package updates, #980 runtime image pin and #981 SDK image pin. Microsoft release metadata https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json identifies10.0.14 (2026-10-25) as a security release with SDK10.0.403. Read-only official MCR manifest/config verification confirmed:
- aspnet index sha256:de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee, linux/amd64 manifest43cbcf11fd6f88c0375c9c0f7fe59af619abeb323befe5f57dc1502b5a05d8c3, DOTNET_VERSION/ASPNET_VERSION10.0.14.
- sdk index sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b, linux/amd64 manifest9c7b6b263d9d4c2e4718e16dae6d75cd8f4b5ccf206f1e7d48a98eb429981b03, runtime10.0.14/SDK10.0.403.

Those updates are not integrated by this candidate. Reuse the existing source PRs, verify fresh final-head combined compatibility, and preserve immutable image pins. #979's earlier iOS run built then failed during simulator readiness/container lookup; do not mislabel it a compilation failure.

## Historical #932 record preserved verbatim

The following dated prepend never reached current context. Preserve its historical source/evidence before accurate supersession of the stale handoff PR. It must not replace current deployment or newer acceptance status. #930 is actually merged as7416f9bb3404f5359a37ea6c34cb7c6277ef5676.

## Verified Web Plaid success boundary — 2026-10-24 UTC (2026-10-23 MDT)

- Current verified `development` source after [PR #930](https://github.com/Fullworth/FullWorth/pull/930): `7416f9bb3404f5359a37ea6c34cb7c6277ef5676`. This is **development only**, not a verified production deployment.
- Web Hosted Link polling now treats only recognized completion states as terminal and rejects malformed/unknown results. A Completed session only triggers the UI's bank-connected success and navigation when the backend supplies a currently `Active` bank with a valid GUID ID. Absent, invalid, disconnected or attention-required evidence cannot present as success. Existing cancellation, retry and timeout behavior remains covered. PR #930 integrated overlapping sources from #928 and #921, which were closed **superseded but not individually merged**, retaining branches.
- Exact [PR #930](https://github.com/Fullworth/FullWorth/pull/930) head `14c85021ff12a38bd16178f49188869f88a6ca61` passed required backend, Android, Linux production-container and dependency checks, plus Chromium/WebKit browser regression: **2,394 .NET and 37 Node tests**, zero failed/skipped, zero backend build warnings/errors. Linux tests include isolated visual/passkey/HTTP security, OCR and disposable encrypted backup/recovery. These tests do not prove physical-device, live provider, actual two-user or real backup/restore acceptance.
- Production/release boundary unchanged: last independently recorded deployed `master` SHA `dd31836703bd110a30f303ed3ce9fd13c0f61d82` from October 21 guarded deploy/auth smoke. [Release PR #878](https://github.com/Fullworth/FullWorth/pull/878) remains gated, and no release promotion/deploy or production mutation was made. [#912](https://github.com/Fullworth/FullWorth/issues/912) still needs real PostgreSQL unknown/ambiguous commit handling, safe bank-owner reconciliation, Plaid Sandbox and mobile/Web recovery acceptance; [#291](https://github.com/Fullworth/FullWorth/issues/291)/[#669](https://github.com/Fullworth/FullWorth/issues/669) still need provider-immutable off-host/clean-host real database and statements with both Data Protection keyrings, exact-deployed-release controlled two-owner/security/legal/operations and device gates. Do not close acceptance issues based on synthetic tests. Inspect fresh GitHub evidence before continuation.

---

