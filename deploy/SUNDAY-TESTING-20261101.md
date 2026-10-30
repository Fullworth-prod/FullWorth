# FullWorth Sunday testing package — November 1, 2026

## Release card: fill before testing

Status: VERIFIED DEVELOPMENT CANDIDATE; remaining journey review and served availability are pending. This package does not authorize deployment.

- Candidate commit: c07c5b739487252fdd17282690c6ab8c70257760.
- Final source: 2e04866bbcce4d490a00d0d889a2d054a28fbc69 (#1018); all 12 checks passed. Main run: https://github.com/Fullworth-prod/FullWorth/actions/runs/40843332700 (3,659 .NET / 753 Node, zero failures/skips).
- Tested and integrated tree: a1bdbfbf7dfd9a101c3f203d93a322ef5b7a569c.
- Website entry: https://fullworth.org/ (existing production; not a candidate preview).
- API served commit and observation time: not freshly verified.
- Last separately recorded production release:9540b2616841e9794d8f8c07616edcd1e339982c.
- Android: [APK build run](https://github.com/Fullworth-prod/FullWorth/actions/runs/40843332683), artifact 12884610408, built from tested merge 891afab79f5480d52e12b3165741a25e4ffc71df (same candidate tree). Archive SHA256: 0b389d6f2b46f6c190a4e6783547406607a4f3d1f8b892c1b59f6da29dea54f1; expires November 12, 08:43 UTC. Metadata verified; archive not independently downloaded in this pass. Requires existing GitHub access; installing on a physical device remains owner acceptance.
- iPhone: Safari Add to Home Screen on the confirmed served website. An unsigned IPA is not proof of installability on a physical iPhone.
- Browser/native client version and API version must both be recorded. A new native build pointing to the older API does not test new server features; absent annual-evidence flags should show unavailable.
- Rollback target: identify the independently verified previously served release and protected backup evidence in the separate deployment proposal. Do not deploy or restore from this checklist.

Until the intended release is actually available at an approved test entry point, new-feature cases are BLOCKED, not passed. Observations on current production can diagnose that release but cannot certify the candidate.

## Before Sunday

Engineering: #1017 and #1018 are merged after final-head checks and tree verification. Owned coverage/unit/math, old responses, native chronology and historical explanation qualification have regression coverage. Continue the bank/bill/statement/alert/auth/device journey review. Freeze one candidate after actionable defects are resolved; preserve source branches.

Operator: prepare a concrete promotion/deployment/rollback proposal for separate approval. No approval is inferred from this goal. Identify database/migration compatibility, current recovery evidence, proposed smoke checks and stop conditions; never assume restoring a database is a safe ordinary rollback.

Owner: have the Android/iPhone available, bank login under your own control, a private list of expected bills and representative statements you consent to process. Keep a small expected-fact sheet privately: merchant/bill, posted debit date, currency, billing period and expected result. No passwords, tokens or account numbers in chat/issues.

Use an existing access-controlled evidence location you control. Sharing state must be checked before putting financial screenshots there. Public GitHub receives only sanitized defect summaries; private screenshots and financial facts remain private. A protected-reference field below is a placeholder, not permission for an agent to export data.

## Priority 1 — Bank visibility and first sync

Consent: linking grants provider access; the owner completes and approves the fresh flow. Never reuse an old consumed Link token or repeatedly reconnect while the first attempt is unresolved.

1. Record served release, device, browser/app mode and time. Open the existing connections screen. Privately record whether Wells Fargo is already connected and which accounts are expected.
2. Complete one fresh Wells Fargo Link with credentials entered only in the provider's UI. Record provider success/error, then return to FullWorth.
3. Check that the correct institution appears, with an honest current status. Record whether account visibility and initial sync complete, remain pending or fail.
4. Refresh once after the attempt settles. Verify the connection/account count is stable and no duplicate obligation/account was created. Compare a small set of known posted transactions privately.
5. If interrupted, observe FullWorth's recovery/status before starting another explicit attempt. Do not treat provider success alone as FullWorth completion.

Pass: expected accounts and posted samples are visible, status accurately reflects completion and refresh does not duplicate data. Pending is not a completed pass. Fail: provider success with no visible connection, false success, wrong account or duplicate data. Record time/error category; never capture credentials.

## Priority 2 — Expected bills and financial truth

1. Compare each privately selected expected bill against its known posted debit, source account and imported history.
2. For missing bills, record the visible rejection/coverage/confirmation reason. Do not mark a bill missing before verifying the underlying posted debit was imported.
3. Check duplicates and account/currency separation. Unknown units must not become USD; mixed units must not become one converted total.
4. Sparse history may correctly show known amounts while withholding cadence, prior baseline or annual projection. Monitoring status does not confirm merchant access or renewal.
5. For subscription claims, independently compare access, renewal, cancellation, prepaid/end date and one-off facts against an authorized merchant/billing source. Without one, merchant verification should remain unavailable.

Pass: known facts match; uncertainties stay explicit; duplicates and unsupported annual/merchant claims are absent. Record each expected obligation separately so a good aggregate cannot hide a missing bill.

## Priority 3 — Statement accuracy and history

Consent: uploading a document processes its private contents. Use only documents you own or are authorized to test; do not upload unrelated financial material.

1. Process representative supported text PDF, scan/image and multipage samples where available. Compare period, amount, currency, due date and bill association against the original privately.
2. Open/download only your own original through the existing permitted user flow if separately appropriate; the agent is not authorized to export it.
3. Check deterministic difference and explanation against actual evidence. Missing extraction or explanation must remain unknown rather than fabricated.
4. Where the supported workflow allows it, add an older statement after a newer one. The most recent billing period should remain primary; upload/detection time must not replace period chronology.
5. Comparable monthly same-unit evidence may support a clearly qualified yearly estimate. Quarterly, incomplete, conflicting or unknown-unit evidence must not show a fabricated yearly rate. No renewal guarantee follows from comparable statement periods.
6. Preserve genuine zero impact and discount/reduction direction. Do not alter real records merely to manufacture a desired test.

Pass: original facts and history are preserved, primary chronology is correct, and projections/explanations match their evidence. Mark unavailable sample types NOT RUN rather than passing them from synthetic CI.

## Priority 4 — Physical-device usability

Run on each actual device; emulator results are supporting engineering evidence only.

- Installed launch and return from background.
- Sign-in, refresh and session-expiry/recovery without exposing another account.
- Keyboard: fields and submit controls remain usable; focus is visible.
- Small-screen/safe-area layout, scrolling, navigation/back and file selection/upload.
- EN/ES text and all three light/dark pairs; reduced-motion behavior under device preference.
- Network loss/reconnect: honest pending/error state, preserved evidence and no automatic write replay.
- Existing explicit update/reload flow: no surprise reload after a timed-out update attempt.
- Passkeys only if the account/device is already enabled and eligible; do not enable a new credential as an assumed test prerequisite.

Pass/fail per scenario and device. Do not label the entire platform passed from one successful launch.

## Priority 5 — Alerts and trust

Start with existing authorized in-app events. Verify that the alert refers to the right bill/change, has correct amount/unit, persists appropriately and does not duplicate after refresh. For an unconfirmed dismissal/update, refresh/reconcile before retrying.

Actual email/push destination receipt requires a separately authorized controlled delivery event and recipient observation. A mock, HTTP response or test runner success is not inbox delivery.

Two-owner isolation, export/deletion lifecycle, account recovery changes, real restore/reboot, immutable-backup deletion-denial testing and legal review require their existing controlled prerequisites and separate authorization where applicable. Do not perform destructive actions or inspect another person's account as routine Sunday tests. Keep these acceptance gates open if not exercised.

## Evidence record

Use the companion CSV, one row per scenario/device/release. Allowed results: PASS, FAIL, BLOCKED, NOT_RUN. Include exact expected/actual outcome and an access-controlled evidence reference; no secret or financial content in a public copy.

A failure report needs: test ID, UI/API/client versions, timestamp, device/mode, sanitized steps, expected/actual result, reproducibility and protected evidence reference. Do not post tokens, request bodies, document contents, account numbers or private amount tables publicly.

## Stop and escalation

Stop the affected journey for wrong-owner data, unexpected financial mutation, credential exposure, repeated duplicate imports, unsupported financial claims or an unresolved write outcome. Preserve non-secret observations and investigate before retrying. Do not reset connections, delete data, restore a backup or change account settings to make the checklist pass.

## Sunday completion

Summarize pass/fail/blocked/not-run by journey and device, with one confirmed candidate/served-release identity. Separate bugs from missing access/consent/evidence. Passing Sunday tests is not legal acceptance, full recovery acceptance, merchant-wide verification or launch authorization.
