## Bank-link hotfix deployed and independently verified — 2026-10-25 America/Denver

The owner explicitly approved this guarded deployment. Verified live release and master are
`9540b2616841e9794d8f8c07616edcd1e339982c`; integrated release tree
`4a60de3eac68f891887ee017af4b2100ea94649d`. Production checkout is clean and its
Git HEAD, release marker, environment release ID and running API/Web identities agree.

[#975](https://github.com/Fullworth-prod/FullWorth/pull/975) promoted only #973's bank-link
cancellation fix, tests and handoff after14 final-source checks passed on6764e155.
Exact-master CI40257284360, all four CodeQL languages40257284548 and governance40257284186 passed.
Image artifact12623132717 is nonexpired,365,661,542 bytes, SHA256
`32595d2e0dd8d942a64ebbe2e81e36efb9a3b33d1564df7dfbc3c8c76fae16b7`.
Guarded deployment40257893875 actually verified image/SBOM attestations, took fresh encrypted
backup snapshot63c7835ac56597724bbccdee77a096ed4c4ab98ebc47fccaf1b103dbfbd94037
before runtime replacement, and passed host/beta/public readiness. Postdeployment auth40258169056
passed both non-destructive bearer-token and browser-cookie/logout boundaries.

Independent SSH verification passed permissions, release integrity, exposure, runtime and API/Web
readiness. Private before/after verification found all36 existing tracked environment fields unchanged
(excluding intended release ID), all five core financial-table counts unchanged, and the same
latest migration20261025020500_AddPlaidLinkCompletionAttempts. No merchant-verification schema was
deployed. Private evidence: /home/debian/fullworth-recovery-evidence/predeployment-integrity-bank972-20261025.json
and postdeployment-integrity-bank972-20261025.json. Counts do not prove private expected-bill truth.

Fresh-host recovery40257392831 succeeded with runner/tools9540b261, restoring the protected
pre-hotfix source release3fd3aa5 snapshot8067b8e455c187515a36ce7bec89002383021068874bf22144f5a5ced3bc24f6.
Read-only provider denials were verified; no private recovery artifacts were uploaded.
This is a current-tooling restore of the prior live release, not a claim of a restored9540 backup,
nonempty-real-statement acceptance or disaster-bootstrap acceptance.

**Real bank acceptance remains open:** #972's old consumed token stays fenced. The owner has been
told to start a fresh secure Wells Fargo connection and confirm bank visibility and initial sync.
Do not replay the old token, reset permits, remove accounts or manufacture a connected result.
The code fix's passing fixtures and deployment do not prove the owner's actual bank is connected.

**Active goal continues:** complete merchant subscription verification #970/#971 and the newly
requested full UI overhaul of BOTH the public homepage and signed-in Overview. The owner explicitly
selected both surfaces and requested a clean, professional system, planned then implemented after
this deployment. Treat that as the latest design brief; earlier colorful concepts were not selected.
Audit current screens, document the concrete design, implement and verify responsive/accessibility/
reduced-motion/performance states while preserving financial behavior. Do not deploy these new
features or redesign without separate authorization.

Preserve #971 at its actual GitHub head; initial source65ade1d is NOT integrated.
Its17 PostgreSQL checks passed; its filter quoting and three explicit security inventories/coverage
registrations still need correction. Additional UI/evidence refinements are conversation scratch,
not committed or tested. Read current source/failures before resuming, and merge this release
ancestry into that branch rather than duplicating work.

Hourly automation stays off. Paid/creator activation, new credentials/account grants, secret changes,
spending/outreach/payouts and private financial exports remain unauthorized. Existing temporary
read-only recovery access retains its original expiry and scope. Broader provider/device/legal/
nonempty-statement/two-owner/disaster-bootstrap gates stay open.

Current release candidate on master:

`9540b2616841e9794d8f8c07616edcd1e339982c`

Current verified live production release marker:

`9540b2616841e9794d8f8c07616edcd1e339982c`

---

## Authorized bank-link hotfix release — 2026-10-25 UTC

The owner explicitly approved deploying the tested bank-link fix in this chat. This supersedes
the earlier pending-authorization note below. Promotion and deployment must still pass current
protected checks, exact-master signed-image verification, fresh backup and guarded production
health/authentication checks. Approval does not authorize real-bank token replay, automatic relink,
paid activation, new credentials, secret changes or unrelated feature deployment.

Scope: #973's bounded finalization after browser disconnect, its tests/runbook and verified handoff.
The only application runtime change from the currently live release is the Hosted Link completion
cancellation boundary. #971 merchant-subscription features and the requested homepage overhaul are
excluded; continue those after this deployment. The owner requested a clean, professional homepage
revamp, planned then implemented, with its precise public/Overview scope being clarified.

The old interrupted Link attempt remains fenced. After verified deployment the owner must initiate
a fresh secure Wells Fargo flow and verify bank visibility and initial sync. Keep #972 open until
that actual provider acceptance is observed; fixture success alone is insufficient.

Current release candidate on master:

`3fd3aa5e019d35f11418c526aeaca3d772bd37ea`

Current verified live production release marker:

`3fd3aa5e019d35f11418c526aeaca3d772bd37ea`

---

## Bank-link browser-disconnect fix verified in development — 2026-10-25 UTC

Development runtime: `6f0b27a624da4c4e56d92493b8c1646907585950`.
Master and verified live production remain `3fd3aa5e019d35f11418c526aeaca3d772bd37ea`; no new deployment occurred.

The owner reported Wells Fargo showed success in Plaid but was absent from FullWorth.
Read-only host evidence found an interrupted initial Hosted Link finalization with no saved bank;
token exchange succeeded, item lookup began and browser Account.DisposeAsync disconnect events
immediately followed. No private financial values, tokens or session IDs were published.

[#973](https://github.com/Fullworth-prod/FullWorth/pull/973) fixes the reproduced cancellation path.
Before the durable one-time exchange fence, browser cancellation is honored. The fence, single
exchange, item lookup and atomic persistence then run under a separate four-minute server deadline,
awaited by the handler. Ownership, consent/session expiry, nonce/concurrency and no-reexchange guards
remain. Real provider/database failures, independent timeout and process termination remain fail-closed.

All four new disconnect cases failed on old source `9685da0c10f70ed7c3c7d5b8a25c2d3e5cb57643`
in PostgreSQL run40246135156 (16 prior scenarios passed). Corrected source
`796e1c0dbeedf707cb044fb4b30b48aef50906ae` passed all nine checks before merge.
Tested candidate and integrated tree: `68a848b9f1a9f05f8cab800089f0a8b16b0d819f`.
Main40246345177 passed2,792 .NET/106 Node, zero failed/skipped; Android/container/dependency passed.
Hosted40246345197 passed22 actual PostgreSQL recovery and53 durable-replay tests plus12 actual
network/relational fault scenarios; bank-lifecycle/monitoring/confirmation suites passed.
These use synthetic providers and do not establish real Wells Fargo acceptance.

**Next required release decision:** the current merchant goal explicitly reserves master promotion/
production deployment for separate authorization. Obtain that authorization before guarded release.
Do not reset the interrupted permit, replay its consumed token, delete an existing account or initiate
a real relink automatically. After authorized deployment, the owner must start a fresh secure Link
session and verify bank visibility and initial sync. Keep [#972](https://github.com/Fullworth-prod/FullWorth/issues/972)
open for that live acceptance. Detailed source/release handoff: deploy/BANK-LINK-BROWSER-DISCONNECT-972.md.

The original merchant-subscription goal/#970 remains active. Preserve separate open #971 at initial
head65ade1d: its17 new PostgreSQL checks passed, but the workflow's test filter needs shell quoting
and three explicit security endpoint/resource-coverage inventories need registration. Initial main CI
passed2,826 tests and failed those three inventory/coverage guards; no #971 integration is established.
Additional source refinements were prepared in conversation scratch only, not committed. Read actual
GitHub source and failures before resuming; do not duplicate the PR or treat scratch as tested source.

Hourly automation remains off. No production, real-bank, credential/account-setting, paid/creator,
outreach/payout or destructive action occurred. Existing UI concept selection and broader external
provider/device/legal/recovery gates remain unchanged.

---

## Operator checkpoint after verified guarded deployment — 2026-10-25 UTC

Current release candidate on `master`:

`3fd3aa5e019d35f11418c526aeaca3d772bd37ea`

Current verified live production release marker:

`3fd3aa5e019d35f11418c526aeaca3d772bd37ea`

Normal guarded deployment is verified complete; this is not a claim that every external-beta acceptance gate is complete. No paid feature/creator activation, payout, outreach, branch deletion or hourly-automation restart occurred. Earlier deployment/billing/recovery failures below are dated history, superseded by this verified checkpoint.

Deployment40226147172, exact-master CI/signing40225561325 and non-destructive auth smoke40226442461 all passed. Independent SSH verification confirmed the live revision, clean checkout, public readiness, owner/roles and disabled subscription enforcement. All36 tracked existing configuration values and five core financial-table counts were unchanged; the expected completion-attempt migration is applied.

The deployed release's own encrypted snapshot `8067b8e455c187515a36ce7bec89002383021068874bf22144f5a5ced3bc24f6` is provider-protected and was restored successfully on a fresh GitHub-hosted runner in40227647810. Source=runner=live release; PostgreSQL/checksums/statement manifest/both key rings passed, with separate provider read-only denials verified. Private metadata is at `/home/deploy/fullworth-acceptance/recovery-results/run-40227647810.json`; full hashes/artifact/limits are in context. Nonempty real-statement acceptance is explicitly not established.

Owner recovery kit: `.ssh/fullworth-recovery-transfer-20261025/fullworth-recovery-kit-live-3fd3.dpapi` plus README on the owner's PC. Windows DPAPI CurrentUser encryption/decryption round trip passed; the current Windows profile is needed. The kit excludes the expiring S3 key. Actual disaster bootstrap from that kit has not been exercised.

The approved24-hour recovery key expires onOct26. A temporary local read-only key copy remains restricted to the owner and SYSTEM because automatic approval review rejected deletion; keep that distinction explicit and do not publish values. No further owner approval is pending for this completed deployment.

Remaining owner-dependent work is separate: controlled real-provider/network/private expected truth, physical devices/VoiceOver, nonempty real statements, two-owner/document/security/legal and full disaster-bootstrap acceptance. Three mobile Overview designs remain unselected. No payout/outreach/paid activation or hourly automation restart is authorized by this checkpoint. Preserve branches and older #932.

---

## Operator checkpoint after real independent recovery — 2026-10-25 UTC

Current release candidate on `master`:

`1041bde352c2d30b01719c6d492c6ae284cd1bf1`

Current verified live production release marker:

`dd31836703bd110a30f303ed3ce9fd13c0f61d82`

Independent production recovery [40223645645](https://github.com/Fullworth-prod/FullWorth/actions/runs/40223645645) actually passed on a fresh GitHub-hosted runner using the approved read-only credential and provider-protected snapshot `46772e80141cfd2528cf3c17b885bb651fa1b20749b0d0e05db1d6218d2c90da`. Its private metadata pins runner release `1041bde352c2d30b01719c6d492c6ae284cd1bf1`, backed-up live release `dd31836703bd110a30f303ed3ce9fd13c0f61d82`, explicit provider read-only denials and completion at 2026-10-25T18:57:26Z. No recovery artifacts were uploaded. Nonempty real-statement acceptance remains explicitly unestablished.

#967's guarded configuration-probe fix passed all nine source checks and merged as `1041bde352c2d30b01719c6d492c6ae284cd1bf1`. Its subsequent master CI 40223585134 hit an independent Windows toolchain failure: the floating Microsoft aka.ms download selected SDK10.0.403/runtime10.0.14 and redirected to a blob endpoint returning HTTP409. Official .NET10 release metadata still identifies SDK10.0.402/runtime10.0.13 (release date 2026-10-13); these are also the versions used by the passing source checks. The correction pins only the two Windows main-CI SDK inputs to 10.0.402. No build/test/branch gate is weakened, no alternate untrusted feed is added, and no application runtime is changed. Require new final-head/master checks and exact-release signing before deployment.

An owner-held recovery kit was encrypted with Windows DPAPI CurrentUser and round-trip verified off the production host. It contains repository/password/snapshot metadata, excludes the expiring S3 key and requires the current owner Windows profile. Disaster bootstrap from that kit is a separate, unexercised operator procedure. The temporary read-only key copy remains owner/SYSTEM-only because automatic review rejected its deletion; its provider access expires after 24 hours.

Production remains on the healthy existing release. No deployment or paid activation has occurred. Preserve the real-provider/device/legal/nonempty-statement distinctions, branches and disabled hourly automation. Private completion record: `/home/deploy/fullworth-acceptance/recovery-results/run-40223645645.json`.

---

## Operator checkpoint after approved read-only recovery access — 2026-10-25 UTC

Current release candidate on `master`:

`e9e5687d7affb4525e1159efceb514cad69ce0dc`

Current verified live production release marker:

`dd31836703bd110a30f303ed3ce9fd13c0f61d82`

The owner approved the temporary 24-hour, single-bucket Object Read only key. Actual S3 reads passed; synthetic write/delete returned HTTP403/AccessDenied. The recovery-only bundle is privately staged, with no production write credential transferred.

Fresh GitHub-hosted recovery run [40222367338](https://github.com/Fullworth-prod/FullWorth/actions/runs/40222367338) failed before acceptance. A real Restic probe against the protected snapshot reproduced a lock-write rejection on ordinary `cat config`; `--no-lock cat config` and the exact frozen snapshot list succeeded. The correction routes only the verifier's initial configuration probe through its existing guarded recovery wrapper. Normal verification/capture/maintenance keep locks. The regression executes the real verification entry point: it fails on the old source and passes on the corrected source.

The independent recovery gate remains open until the corrected exact-master workflow passes. Require protected final-head checks, current-master signed image/SBOM material, real recovery proof, guarded deployment and same-release health/authentication verification. No production update has occurred; paid activations stay off. Preserve prior evidence and source branches. A temporary local read-only credential copy remains restricted to its owner and SYSTEM because automatic approval review rejected deletion; the key expires after 24 hours.

---

## Operator checkpoint after protected release promotion — 2026-10-25 UTC

Current release candidate on `master`:

`e9e5687d7affb4525e1159efceb514cad69ce0dc`

Current verified live production release marker:

`dd31836703bd110a30f303ed3ce9fd13c0f61d82`

[#965](https://github.com/Fullworth-prod/FullWorth/pull/965) promoted the reviewed development release plus independent recovery safeguards through the protected PR process. Exact source `d636072a02cf21bfebff8a987d09a74876f74244`, tested and integrated tree `a9682dde73c4e63dcc536213b3cbd5f8d6bdc816`. All 17 final-source checks passed: 2,786 .NET/106 Node; 14 recovery-runner Python tests and full shell/secret boundaries; PostgreSQL bank 26+12 related xUnit, Hosted 53 durable/16 recovery+12 relational faults, monitoring 124, confirmation 118; Chromium/WebKit 672 geometry+126 native+37 ARIA per engine plus 38 dialog/74 BFF/24 recovery cases; Android and unsigned iPhone packaging. CodeQL passed all four languages, automatically closed both password-storage findings, and showed zero open PR findings and unresolved threads.

This is a release promotion, **not a deployment**. Exact-master CI/signing [40195520636](https://github.com/Fullworth-prod/FullWorth/actions/runs/40195520636) is running; governance 40195520603 passed. Require successful current-master image/SBOM attestations and real independent recovery before guarded deployment. The owner authorized preparation and deployment; hourly automation stays deleted/off.

Actual SSH inspection verified the existing production checkout and release marker at `dd31836703bd110a30f303ed3ce9fd13c0f61d82`. A fresh normal encrypted backup completed at 2026-10-25 12:15 UTC; all host/public readiness checks passed after services resumed. The completed snapshot `46772e80141cfd2528cf3c17b885bb651fa1b20749b0d0e05db1d6218d2c90da` was copied server-side into the separately enabled R2 `immutable-recovery/` bucket-lock prefix: 399 encrypted objects / 7,879,162 bytes, verified object-by-object. The valid production credential received HTTP409/ObjectLockedByBucketPolicy for synthetic overwrite and delete within that prefix. Live Restic capture/tagging/lock cleanup remain outside this prefix. Private metadata: `/home/debian/fullworth-recovery-evidence/protected-backup-46772e80141c.json`.

**Required approval remains:** the saved separate recovery credential fails with SignatureDoesNotMatch. A Cloudflare Object Read only key scoped only to `billwatch-production-backups-01`, expiring after 24 hours, is prepared but uncreated. Browser security requires action-time approval for new access. The pending request also specifies use on a fresh GitHub-hosted runner to restore the encrypted backup. Do not create the key or run dependent recovery before approval, copy production write credentials into the runner, or claim the clean-host gate passed.

Use the new manual exact-master production-environment recovery workflow after a valid separate read-only bundle is privately staged. It proves explicit 403/AccessDenied write/delete failures against an unlocked synthetic canary, verifies the exact frozen snapshot through the existing database/statement/both-keyring verifier, and retains metadata only. Passwords pass in memory into mounted tmpfs secrets; the received bundle, read-only credential and private logs stay in a private per-run tmpfs directory. Frozen-copy no-lock mode is restore-only and refuses mutable/live repository paths. Detailed handoff: `deploy/INDEPENDENT-RECOVERY-20261025.md`.

The old zero-runtime inventory and R2 AccessDenied paragraphs below are historical evidence, superseded for current host state by the fresh inspection and protected-copy proof above. Independent production restore, nonempty real-statement recovery and same-release provider/physical-device/security/legal acceptance remain distinct and open. Paid features, creators and other activations remain default-off; no new UI concept was selected. Preserve branches and the fail-closed cleanup guard.

---

## Operator checkpoint after accessible interaction software — 2026-10-25 UTC

Latest verified development runtime: `1b1de0cd56aeb55ca66e0c8f04e44433d25366fd` (#963), exact source `32769d3313f83ddcbe5058fa827ecf159d471d64`. Current release candidate on `master`: `33223f74d171e0af2fc39b5a1c94ee2ceaaae7c3`. Last independently recorded live production release: `dd31836703bd110a30f303ed3ce9fd13c0f61d82`. No deployment or host acceptance in this pass.

All five final-head checks passed, including37 state checks per browser engine and2786 .NET/106 Node; context has exact tree/runs/artifact evidence. #960/#961/#963 are integrated software checkpoints; prior acceptance/operator instructions remain below. No unpublished work or new owner action is required for these merges.

Keep real provider/device/VoiceOver/safe-area, private expected-truth, actual off-host/clean-host/database/statements/both-keyring/two-owner/security/legal and release approval gates open. Creator terms/features remain proposed/default-off. Three Overview designs remain unselected. Hourly automation stays deleted/off; no financial/account/secret/rule changes, spending/activation, outreach/payouts, private export or branch deletion occurred.

---

## Operator checkpoint after responsive/native recovery software — 2026-10-25 UTC

Current release candidate on `master`:

`33223f74d171e0af2fc39b5a1c94ee2ceaaae7c3`

Last independently recorded live production release:

`dd31836703bd110a30f303ed3ce9fd13c0f61d82`

Latest verified development runtime: `938a2a09ca812396dfd00b1ae96130ed3fb4553d` (#961); preceding responsive merge #960 `b21e2eb77492a1a67fb4dcc6fbd7738db59c5738`. Neither is deployed. All required final-head gates passed with review requirements respected; no rules changed.

Software evidence: #960 actual2765 .NET/106 Node,672 app geometry+126 native cases per engine; #961 actual2786 .NET including21 native cases,106 Node,18 ownership fixtures,PostgreSQL53 durable/16 consumed-token/12 faults,Android/unsigned iPhone/container recovery. Full exact source/tree/run/artifact details are at the top of FULLWORTH_CONTEXT.md. Synthetic providers, virtual passkeys, device doubles and disposable encrypted fixtures are not real acceptance.

No new owner action is required for these software merges. Existing owner/operator gates remain: controlled Plaid Sandbox and installed Android/iPhone recovery (#912,#251/#258), approved private expected-bill/import/payroll/provider truth (#762/#293), actual immutable off-host/clean-host database+statements+both keyrings, two-owner/security/document/legal/same-release acceptance and explicit deployment authorization (#291/#669). Keep them open; do not export private data or activate/spend to manufacture evidence. Creator terms remain proposed/default-off.

Three Overview concepts remain unselected; no choice or redesign was made. Hourly automation remains deleted/off. Source branches retained; older #932's historical handoff is still separate. Preserve all existing operator instructions below.

---

## Operator checkpoint after Hosted Link software recovery — 2026-10-25 UTC

Current release candidate on `master`:

`33223f74d171e0af2fc39b5a1c94ee2ceaaae7c3`

Current verified live production release marker:

`dd31836703bd110a30f303ed3ce9fd13c0f61d82`

Verified development runtime integration:

`1d6c114d9d62f9619b0903b95449b98829c12963`

[PR #958](https://github.com/Fullworth-prod/FullWorth/pull/958) implements the #912 **software recovery milestone**: owner/session-bound durable permits; protected Link-token clearing before one-time exchange; conditional final bank/session/permit persistence; committed-result reconciliation without re-exchange; interrupted/stale fail-closed recovery; and explicit bank review before a fresh Link session. It adds an owner-bound table and additive migration, with no production migration performed.

Verified final source: `a2c6a2f094197e6d583fbe0f097cbfa41d4c5aec`; final source and tested merge-candidate tree: `bdb284cb68f22d66620f480c00a8b80549d19da0`. Development runtime integration: `1d6c114d9d62f9619b0903b95449b98829c12963`.

[Main CI](https://github.com/Fullworth-prod/FullWorth/actions/runs/40164677190) passed **2,765 .NET and 106 Node tests**, zero failed/skipped, and matched the committed EF model/migration. Backend and offline builds have zero warnings/errors. All 12 exact-source check runs passed, including Android, unsigned iPhone package, dependency and production-container checks. Android and unsigned iPhone builds have zero warnings/errors. [PostgreSQL Hosted recovery](https://github.com/Fullworth-prod/FullWorth/actions/runs/40164677162) passed **16 consumed-token cases, 53 durable-replay cases and 12 relational fault scenarios**: six TCP acknowledgements lost after real server ReadyForQuery; two server-trigger rollbacks; two after-prepare stale-writer atomic rollbacks; composite-owner FK/cascade and owned deletion preserving foreign state. Provider responses are synthetic.

Other exact-source PostgreSQL suites passed **26 bank-lifecycle race checks plus 12 related xUnit cases**, **124 monitoring** and **118 bill-confirmation** cases. [Chromium/WebKit](https://github.com/Fullworth-prod/FullWorth/actions/runs/40164677191) passed **24 Hosted Link recovery, 38 dialog and 74 BFF boundary cases**. Tests verify no false connected state, no silent exchange/new-session retry, localized bank-review links and explicit fresh session creation. Native guidance directs bank refresh first. No installed-device acceptance is inferred.

Runbook: [Hosted Link recovery boundaries](deploy/HOSTED-LINK-RECOVERY-20261025.md). A later separately authorized release must verify migration/backup/lock safeguards and prevent mixed old/new completion writers from bypassing the durable permit. Never remove/reset an Exchanging or Interrupted permit just to retry the consumed token.

**#912 stays open** for controlled Plaid Sandbox/error-mode and physical-device acceptance. The software milestone does not satisfy real provider/device/production recovery, qualified legal, #934/vNext as a whole, or other release gates. #762/#293 private expected-bill/import/payroll/provider proof, #251/#258 physical devices, and #291/#669 real clean-host/off-host/both-keyring/security/legal proof remain open without fabricated evidence. Source-level fixture results must not be promoted into those claims.

The `master` release candidate remains `33223f74d171e0af2fc39b5a1c94ee2ceaaae7c3`; latest independently recorded live release remains `dd31836703bd110a30f303ed3ce9fd13c0f61d82`. This pass merged development only. No master promotion, deployment, production migration, paid activation, financial/secret/account change, payout, outreach or private-data export occurred. Branches are preserved; do not enable the old-name cleanup guard.

The three new Overview design concepts still await human selection; this is functional recovery UX, not selection or implementation of a visual redesign. Preserve reduced motion/performance profiles and financial behavior. Creator terms stay proposed and features default-off. Current canonical repository is `Fullworth-prod/FullWorth` (ID `1344061157`).

This verified checkpoint supersedes candidate/pending notes below; prior history is preserved.

Operator evidence still needed: controlled Plaid Sandbox terminal/error responses and physical-phone explicit recovery; separately authorized migration/release readiness including mixed-writer prevention; existing real recovery/security/legal acceptance. Keep all proof metadata-only and use the established private channels for private evidence.

---

## Operator checkpoint after verified reliability integration — 2026-10-25 UTC

Current release candidate on `master`:

`33223f74d171e0af2fc39b5a1c94ee2ceaaae7c3`

Current verified live production release marker:

`dd31836703bd110a30f303ed3ce9fd13c0f61d82`

Verified development runtime integration:

`0a3ec424086a02bc81c85044660da8011e1b7879`

[Integration #953](https://github.com/Fullworth-prod/FullWorth/pull/953) merged into development as `0a3ec424086a02bc81c85044660da8011e1b7879` after **all nine** checks passed on exact final source `c3f58dacf2852aaf4e6e698c0060c316f56c019f`. The tested merge-candidate tree and integrated tree both equal `3020e22b57bd1ebc7039775307df54967a4ced9a`.

[Final main CI](https://github.com/Fullworth-prod/FullWorth/actions/runs/40150705166) executed **2,749 .NET and 104 Node tests**, zero failed/skipped and zero backend/offline/Android build warnings/errors. Android, container, HTTP security, passkey/browser and disposable encrypted-backup/recovery fixtures passed. Dedicated PostgreSQL suites passed **26 bank-lifecycle**, **53 Hosted Link**, **124 monitoring** and **118 bill-confirmation** cases. [Chromium/WebKit](https://github.com/Fullworth-prod/FullWorth/actions/runs/40150705140) passed **38 dialog plus 74 BFF scenarios**. These repeated/isolated suites are not a claim of real-provider, installed-device or production recovery acceptance.

Final-head visual artifact `12581863951` was downloaded and SHA256 verified as `92bd74ec419c235d623eeeef91eb3e4a3c147ecfdb749f4ed605777b602de763`; four screenshots were inspected for 320px first-sync/Activity, 390px Account and Spanish Activity states. This is the existing vNext integration, not implementation of the three unselected new mockups.

Current release state: master advanced independently through #878; that is repository state, not evidence of deployment. The latest independently recorded live proof remains the October 21 guarded deployment/authentication smoke. This pass merged development only and dispatched no production deployment or master promotion.

Code issues #940/#951/#954 are closed as completed in development. Represented PRs #941/#942/#943/#944/#945/#948/#950/#952/#955/#956 were closed as **superseded, not individually merged**; all ten source branches were verified present. No branch was deleted.

Continue #912 failed/unknown-commit consumed-token and controlled provider recovery; #762/#293 private expected-bill/import/payroll/provider evidence; #251/#258 physical Android/iPhone acceptance; and #291/#669 immutable off-host/real clean-host statements plus both-keyring recovery, controlled two-owner/document/security and qualified legal acceptance. Those gates remain open. No paid/creator/passkey/AI activation, financial account/secret change, outreach, payout or private financial-data export occurred.

The three new Overview visual concepts still require human selection; preserve financial behavior, reduced-motion and performance settings. Creator terms remain proposals and features default-off. Use the current canonical repository `Fullworth-prod/FullWorth` (unchanged repository ID `1344061157`); do not enable its old-name branch-cleanup guard without separate authorization.

Operator next actions remain controlled real-provider/financial-evidence, recovery/security, physical-device and qualified legal acceptance against an explicitly verified release. Selecting one of the three already shared Overview concepts is the next product-design decision. Neither this handoff nor passing CI authorizes deployment, broad launch, paid activation, customer outreach or money movement. Earlier pending/billing-lock notes below are historical.

---

## Release branch identity reconciled — 2026-10-25 UTC

Current release candidate on `master`:

`33223f74d171e0af2fc39b5a1c94ee2ceaaae7c3`

Current verified live production release marker:

`dd31836703bd110a30f303ed3ce9fd13c0f61d82`

The `master` branch advanced independently through [release PR #878](https://github.com/Fullworth-prod/FullWorth/pull/878) on 2026-10-25. This records repository state, not a deployment or new launch approval. The latest independently recorded live evidence remains the October 21 [guarded deployment](https://github.com/Fullworth-prod/FullWorth/actions/runs/39665707749) and [same-release authentication smoke](https://github.com/Fullworth-prod/FullWorth/actions/runs/39666163983). No fresh host inspection or later deployment is established by this reconciliation.

The reliability work remains in development integration [#953](https://github.com/Fullworth-prod/FullWorth/pull/953), pending its final-head checks. Keep real recovery, two-owner/provider/statement, physical-device and qualified legal gates open. Do not promote/deploy or activate billing/creators/passkeys/AI merely to reconcile these documents. Earlier release identities below are historical.

---

## Verified guarded production release — 2026-10-21 UTC

Current release candidate on `master`:

`dd31836703bd110a30f303ed3ce9fd13c0f61d82`

Current verified live production release marker:

`dd31836703bd110a30f303ed3ce9fd13c0f61d82`

[Guarded deploy #39665707749](https://github.com/Fullworth/FullWorth/actions/runs/39665707749) completed on the exact `master` release and [same-release authenticated smoke #39666163983](https://github.com/Fullworth/FullWorth/actions/runs/39666163983) passed. The deploy verified image provenance/SPDX, pinned SSH, a pre-replacement encrypted snapshot and API/Web readiness. This is **not** a separate clean-host recovery of real statements and both key rings, provider immutability, physical-device acceptance or permission to enable passkeys/payments/AI. See #669/#291. The Face ID first-step login work in PR #801 is included via release PR #804 and remains disabled and gated.

Earlier handoffs below are historical, not the current release identity.

---

## Release evidence reconciled — 2026-10-18 UTC

Current release candidate on `master`:

`422718fad7d05a3729b08d461929d0d60c6761a6`

Current verified live production release marker:

`422718fad7d05a3729b08d461929d0d60c6761a6`

[Guarded deployment #25](https://github.com/Fullworth/FullWorth/actions/runs/39053793950) and [same-release authentication smoke](https://github.com/Fullworth/FullWorth/actions/runs/39054021470) both completed successfully for this exact SHA. This is the last verified deployment record, not a new host inspection. Issue #291 still has three open security evidence gates: provider-enforced immutable/off-host recovery protection, separate clean/compromised-host real-data restore including statements and both key rings, and complete exact-release controlled security acceptance. A one-user smoke or CI fixture restore does not close them.

Current development base is `db4248d78b6ec0ccec290e7ebd6b0b49bfac466d`. PR #773 (password-once verification) and PR #774 (transaction evidence/resilience) are unmerged development work, not live changes. Passkeys remain disabled pending Apple association and physical-device acceptance; no payment/AI/security controls are enabled by this checkpoint.

## Earlier checkpoints (historical; current release identities below are superseded)

## Verified release checkpoint — 2026-10-16 UTC

Current release candidate on `master`:

`8c64cc8f7cdf519a6214be47c2016f601331150e`

Current verified live production release marker:

`8c64cc8f7cdf519a6214be47c2016f601331150e`

[Guarded deploy #24](https://github.com/Fullworth/FullWorth/actions/runs/38932402673) succeeded for the exact release after [master CI](https://github.com/Fullworth/FullWorth/actions/runs/38930703375), CodeQL and governance passed. Deployment verified image provenance/SBOM attestations, runtime identity, server production/beta checks, and public API/Web readiness. Read-only host verification matched both checkout and verified release marker to this SHA; public modal CSS, JavaScript and BFF assets matched approved Git blob hashes. No bank was disconnected during verification.

This release includes the hybrid website styling and themed bank-disconnect modal only; development-only passkeys remain disabled and unpromoted. The Bills empty-state follow-up is development work, not claimed live. Installed-iPhone acceptance and the remaining provider/recovery/legal gates are separate.

## Earlier checkpoints (historical; superseded for current release identity)

## Release checkpoint — 2026-10-15 UTC

Current release candidate on `master`:

`b275ad413a0532cdd2c42ee6ab3f9a1a4ed3fdd2`

Current verified live production release marker:

`457e7e4b77a88714e8870e369a1aaf17e76e2de2`

- On October 15, the verified Web/PWA hybrid redesign was promoted to `master` at `b275ad413a0532cdd2c42ee6ab3f9a1a4ed3fdd2`; [exact-master CI](https://github.com/Fullworth/FullWorth/actions/runs/38696477773) passed. No guarded production deployment of this newer SHA is established. A later operator check reported SSH port 22 unreachable on the VPS; resolve host console/SSH access and reverify runtime before any further guarded deployment. Preserve last verified deployed release identity separately from repository state.

- [Guarded production deploy #23](https://github.com/Fullworth/FullWorth/actions/runs/38690114274) succeeded on 2026-10-15 at 06:05 UTC. Pinned SSH, exact-release provenance/SBOM attestations, image identity, database-role separation/migrations, native OCR and kernel containment, file-backed secrets/non-disclosure, permissions, runtime/exposure, and public API/Web readiness passed. The host's verified release marker matches `457e7e4b77a88714e8870e369a1aaf17e76e2de2`.
- [Production Auth Smoke](https://github.com/Fullworth/FullWorth/actions/runs/38690396582) passed on the same release: API login/refresh, protected read endpoints, non-admin denial, export secret/storage boundaries, Web cookie login, authenticated app/BFF, antiforgery issuance, and logout invalidation. Mutation probes were disabled; this does not prove two-user ownership or statement/provider acceptance.
- [Exact-master CI #1797](https://github.com/Fullworth/FullWorth/actions/runs/38681858810), [Governance #36](https://github.com/Fullworth/FullWorth/actions/runs/38681858771), and all three exact-master CodeQL analyses passed. Attested production image artifact `11948796248` expires 2026-10-22 04:32 UTC. PR #748 fixed the OCR cgroup observer race without relaxing limits; PR #749 promoted it.
- Direct post-deployment verification confirmed release identity, public HTTPS readiness, private exposure boundaries, the Owner role, disabled subscription enforcement, active backup/readiness timers, configured alerting, and append-only backup-client policy. Snapshot availability is verified; a new post-deployment snapshot, separate clean-host recovery, provider immutability, and external alert delivery are not established by these checks.
- Keep payments/subscription enforcement and AI-derived persistence disabled. Broader launch remains gated by controlled statement and two-user ownership acceptance, Plaid lifecycle/payroll acceptance, physical-device checks, actual reboot recovery, clean-host recovery with real statements and both key rings, provider-enforced immutability, external alert delivery, and qualified legal/license review.
- Next: collect those remaining acceptance results against this deployed release. Preserve production data and the existing manual guarded deployment path; do not redeploy merely to publish handoff documentation.

## Previous handoff snapshots (historical; superseded by checkpoint above)

## Current release state — 2026-10-14

## Owner-approved release policy — 2026-10-14

On 2026-10-14 the owner explicitly approved removing the independent-review requirement to release as a solo maintainer. The approved policy permits zero required approvals while retaining mandatory pull requests, strict current-branch checks for `Backend build and tests`, `MAUI Android build`, `Linux production container`, and `Dependency review` (GitHub Actions app 15368), thread resolution, deletion/force-push restrictions, and an empty bypass list. Read back the saved ruleset before merging; authorization is not evidence that GitHub saved it. Independent review is no longer a release prerequisite under this decision. Host identity, real recovery, attested exact-master artifacts, and deployed acceptance remain required.

This decision supersedes earlier independent-approval requirements below. It does not mark production as deployed or waive data-recovery gates. Physical-device acceptance follows verified deployment.

**Status:** Deployment remains blocked. No current live release is established. Do not start public services against empty data.

The October 12–13 operator-assisted handoff reports an isolated restore of Restic snapshot `726a1e85` (manifest ReleaseId `f401a591a8abdade557827c09412dc3166fb9de2`) on `vps-32597e02`, with checksum/database/migration/key checks passing. This backup had **zero statement uploads**, so actual statement-file recovery remains unproven. Separate clean-host recovery, trusted host identity, provider immutability, and current least-privilege credentials remain open. This pass did not rerun that drill.

The recorded artifact expiry, **2026-10-13 18:30 UTC**, has elapsed. Require a fresh exact-release artifact and green gates before dispatch. PR #724's proposed saved-host behavior remains separate and is not authorized by this handoff.

The checked items immediately below retain historical repository evidence; they do not establish a currently downloadable artifact or current development head.

### Repository release preparation

Integration #739 and cleanup #740 are merged and verified: FullWorth CI #1775/#1778, Dependency Security #865/#868, Android internal #78, and iOS internal #72. Cleanup head `16bdda005d4d9d8f00ac28a9604a2fe0faed6d42` passed all 1,348 backend tests with zero build warnings/errors. PR #741 also merged after exact-head FullWorth CI #1779 and Dependency Security #869 passed. It corrects governance verification to require actual job contexts; it does not configure administrator settings.

Use [RELEASE_CANDIDATE.md](RELEASE_CANDIDATE.md) for the ordered go/no-go matrix, exact required check contexts, and controlled financial acceptance scenarios. Repository checks do not close the real host/recovery/provider/device/legal gates below.

Current release candidate on `master`:

`c8858a38b38d7eb0888d273b9a1a409308e565b4`

- [x] PR #711 promoted the candidate to `master`.
- [x] Exact PR checks passed on development head `0168093384961e4d2e9838c495fbaf5bfb2c279a`: FullWorth CI #1707, Dependency Security #798, and PR workflow #59. After promotion, master FullWorth CI #1708, Repository Governance #20, and CodeQL via Push on master #60 passed.
- [x] Exact attested artifact: `fullworth-production-image-artifacts-c8858a38b38d7eb0888d273b9a1a409308e565b4` (artifact ID `11435103773`, digest `sha256:39d566b68cdbef5dfd52ef616ce23642b8aa33e77fe26026f4f1c2ea135db823`; expires 2026-10-13).
- [x] Historical development head `0168093384961e4d2e9838c495fbaf5bfb2c279a` includes PR #710's release-handoff refresh, PR #707's recovery passfile fix, and PR #709's host-confirmation gate. Exact current-head FullWorth CI #1707 and Dependency Security #798 passed. These changes do not grant remote R2 access or prove recovery.
- [ ] Run #15 failed closed before candidate startup. Its partial API/Web/edge report conflicts with subsequent host inspection: OVH 40.160.137.55 read-only inventory found zero Docker containers, zero Docker volumes, and a missing release marker; this conflicts with run #15's partial-runtime report.
- [ ] Remote R2 recovery returned AccessDenied with the host's configured backup credential; a separate read-only recovery credential is required. The development fix does not supply this credential; do not broaden the production backup key.
- [ ] Reconcile the reported isolated snapshot restore with the required separate clean-host and actual statement-file recovery evidence; do not treat a zero-statement drill as complete recovery proof.
- [ ] Reconcile the guarded workflow SSH destination with the inspected OVH host and verify the actual host/project read-only before any service repair.
- [ ] Restore and verify the encrypted backup in isolation before any data-bearing production startup. The inspected OVH host has no Docker volumes; a normal first startup would be empty.
- [ ] Only after all recovery and target-identity gates pass, run the guarded workflow for the current exact master SHA and verify auth smoke, public readiness, and the release marker all identify that SHA.

Current verified live production release marker:

`none`. The operator reports no production server is running; prior deployment markers are historical only. No successful deployment of the candidate has been observed. Keep credentials out of chat, source control, command history, and logs.

## Guarded production deployment and same-release acceptance — issue #669

**Next concrete sequence:**

Before promotion, verify the owner-approved solo-maintainer ruleset has saved and review the prepared release PR. Generate a fresh artifact only through green exact-master CI; do not dispatch an expired artifact.

1. Verify the separate least-privilege R2 Object Read credential is installed on the trusted recovery host through the approved secret-handling path; the historical AccessDenied report does not establish current credential state. Do not paste its value into chat or write it into source control or shell history.
2. Complete the separate clean-host exercise against the real off-host repository, including actual statement files, database, and both Data Protection rings. Preserve the snapshot/release relationship and sanitized evidence; the reported zero-statement drill does not close this gate.
3. In GitHub Environment `production`, set the non-secret variable `FULLWORTH_PRODUCTION_SSH_HOST` to `40.160.137.55` only after verifying the pinned `FULLWORTH_PRODUCTION_SSH_KNOWN_HOSTS` entry is this OVH server. At workflow dispatch, type the same host into `confirm_production_host`; preserve the current live release and do not guess at service restarts.
4. Only after recovery and target identity are verified, check the exact master artifact and all guarded deployment preflights, then dispatch the guarded workflow for that exact SHA.
5. Confirm automatic auth smoke, public API/Web readiness, running release, and verified marker all agree on the deployed SHA.

A workflow checkout, CI result, artifact, or copied object tree is not proof of production deployment or a successful restore.

## Deployed-host security and ownership proof — issue #669 / #291

**Status:** Human production evidence required after the guarded deploy succeeds.

Repository and CI coverage already exercise these controls. The remaining work is to prove the same controls on the exact deployed release.

**Action:**

1. Run the deployed parser/OCR containment verification and retain only sanitized metadata.
2. Run deployed secret non-disclosure verification without printing protected values.
3. Run the production host/operator hardening verifier and record only pass/fail metadata.
4. Prove the steady-state API uses the dedicated runtime database role and does not retain migration/bootstrap credentials.
5. With two controlled test identities, perform objective Web/BFF/API ownership checks.
6. Confirm manipulated cross-user identifiers return the intended non-disclosing 404 behavior.
7. Process controlled representative text-PDF, scanned-PDF, and JPG/PNG statements.
8. Verify extraction, Bill Stream matching, historical comparison, deterministic monthly/annualized math, evidence-grounded explanation, and alert behavior against operator-known facts.
9. Confirm an external alert reaches the intended destination.
10. Complete disposable-account deletion without affecting another controlled user.
11. Perform the controlled production reboot and prove the exact release returns healthy.

**Verify:** All evidence is tied to the exact deployed release. CI or a different release does not satisfy this gate.

## Off-host immutable backup and compromised-host recovery — issue #669 / #291

**Status:** Human/provider action required before relying on backups for destructive incidents.

**Action:**

1. Confirm the actual encrypted Restic repository is off-host and reachable through the documented recovery path.
2. Configure provider-enforced Object Lock, WORM, immutable snapshots, or an equivalent control.
3. Verify from the provider control plane that production-host credentials cannot remove or weaken the protected recovery points.
4. Run a compromised-host / clean-host recovery exercise against the real off-host repository.
5. Restore the database, statement files, API Data Protection ring, and Web Data Protection ring into an isolated clean host.
6. Confirm restored ownership/modes, migration consistency, application readiness, and release identity.
7. Record only sanitized metadata in issue #669.

**Verify:** Both provider-enforced immutability and a real clean-host restore are demonstrated. Local CI restore tests are not substitutes.

## Installed-device and external-provider acceptance — issues #251, #258, #293

**Status:** Human/device/provider acceptance required after the current candidate is successfully deployed.

**Action:**

1. After the new release is verified live, re-pin installed-device acceptance issue #251 from the old release to the exact new production SHA before recording evidence.
2. Complete all required Android installed-PWA checks on a physical Android device.
3. Record Android acceptance only after every required check passes.
4. Re-test iOS Safari Add to Home Screen on a real iPhone for issue #258 if iOS remains in launch scope.
5. Complete controlled Plaid connect/update/reconnect lifecycle and Hosted Link return observation.
6. Complete issue #293's remaining real-world sandbox acceptance with Plaid payroll transactions.
7. Confirm provider failure/reconnect behavior without exposing provider tokens or raw provider payloads.

**Verify:** Physical-device and provider evidence is tied to the exact live release. Browser emulation, simulator-only evidence, repository tests, and a previous production SHA do not satisfy these gates.

## GitHub protected-branch governance

**Status:** Owner approved zero required approvals; verify saved settings and exact-head CI before merge.

Ruleset 24230011 was saved through the user-approved browser and verified by GitHub API readback on 2026-10-14: active on `master` and `development`, one required approving review, strict up-to-date checks for `Backend build and tests`, `MAUI Android build`, `Linux production container`, and `Dependency review`, all bound to GitHub Actions app 15368. Existing deletion/non-fast-forward/thread restrictions and the empty bypass list are preserved.

The verifier must read branch targeting from the full ruleset detail response; GitHub's list response contains metadata only. This change repairs that false-negative bug with realistic fixtures. Local regression and replay of freshly read GitHub metadata pass; exact-head CI and a live workflow run must still be checked. No independent PR approval or deployment is implied.

**Next:** Verify the approved zero-approval setting, retain mandatory PRs and strict checks, and merge only green exact heads. Do not fabricate reviews or add a bypass. Verify the governance workflow against the corrected exact source before release promotion.

## Commercial legal/license review

**Status:** Human action required before commercial launch.

**Why a human is needed:** Legal entity selection, commercial rights, customer-facing terms, third-party obligations, model/runtime licenses, and proprietary licensing require qualified legal judgment.

**Action:**

1. Review closed draft PR #368 only as historical draft material; do not treat it as current approved legal text.
2. Confirm the correct FullWorth copyright holder/legal entity.
3. Have qualified counsel review the exact customer-facing Terms of Service and Privacy Policy intended for launch.
4. Have qualified counsel review the proprietary source-license approach and third-party/model/runtime notice obligations.
5. After approval, recreate the approved text from the then-current `development` head and send it through normal PR/CI review.

**Verify:** The legally approved versions are merged from current source before commercial launch, and repository/product documentation does not claim legal approval before that review is complete.
