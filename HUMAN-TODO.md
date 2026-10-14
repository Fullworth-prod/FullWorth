## Current release state — 2026-10-14

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

Before promotion, configure the protected-branch governance described below and review the prepared release PR. Generate a fresh artifact only through green exact-master CI; do not dispatch an expired artifact.

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

**Status:** Human repository-administrator action required.

The active repository ruleset **FullWorth protected branches** targets both `master` and `development`. Current verified configuration:

- active enforcement;
- pull requests required;
- deletion blocked;
- non-fast-forward updates blocked;
- review-thread resolution required;
- extra approval required for unattributed changes;
- no bypass actors;
- current connection cannot bypass;
- required approving review count: **0**;
- required status-check rule: **not configured**.

**Why a human is needed:** Current connected GitHub tooling can inspect the ruleset but cannot mutate the repository-administration settings.

**Action:**

1. Open **GitHub → FullWorth repository → Settings → Rules → Rulesets → FullWorth protected branches**.
2. Preserve the existing protections for both `master` and `development`.
3. Require strict current-branch checks for the actual GitHub Actions contexts: `Backend build and tests`, `MAUI Android build`, `Linux production container`, and `Dependency review`. Use GitHub Actions app ID `15368`; workflow display names alone are not check-run contexts.
4. Require at least one independent approving review, as enforced by the repository governance verifier. An author cannot supply their own independent approval.
5. Keep bypass actors empty unless a deliberate, documented emergency policy is later approved.

**Verify:** GitHub ruleset readback shows the intended approval policy and required status checks for both protected branches.

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
