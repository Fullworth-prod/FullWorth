## Current release state

**Status:** Deployment remains blocked until the pinned SSH host key is verified and the clean-recovery-host requirement is resolved. No candidate deployment is confirmed.

Current release candidate on `master`:

`c8858a38b38d7eb0888d273b9a1a409308e565b4`

- [x] Exact-head FullWorth CI completed successfully for `c8858a38b38d7eb0888d273b9a1a409308e565b4` (run [37511551336](https://github.com/Fullworth/FullWorth/actions/runs/37511551336)).
- [x] Exact attested production image artifact: `fullworth-production-image-artifacts-c8858a38b38d7eb0888d273b9a1a409308e565b4` (artifact ID `11435103773`, digest `sha256:39d566b68cdbef5dfd52ef616ce23642b8aa33e77fe26026f4f1c2ea135db823`; expires 2026-10-13 18:30 UTC).
- [x] Operator supplied OVH target IP `40.160.137.55`.
- [x] Isolated restore drill passed on VPS `vps-32597e02` using Restic snapshot `726a1e85` (manifest ReleaseId `f401a591a8abdade557827c09412dc3166fb9de2`). Restored files passed checksum validation; database restore and migration verification passed. The drill used the exact snapshot release, not the current `master` release.
- [ ] Verify the SSH host key for `40.160.137.55` against a trusted OVH/provider source and the pinned GitHub Environment value `FULLWORTH_PRODUCTION_SSH_KNOWN_HOSTS`. The operator has been asked to retrieve the server's public host key; verification is pending.
- [ ] Resolve whether `vps-32597e02` can satisfy the documented separate clean-recovery-host requirement; it is also the nominated deployment target. The operator reports there is no live production server.
- [ ] Verify provider-enforced backup immutability and that temporary recovery credentials have been removed or returned to least privilege.
- [ ] After all recovery and target-identity gates pass, dispatch the guarded workflow for the exact current `master` SHA and verify auth smoke, public readiness, running release, and release marker.

**Production state:** Operator reports no production server is currently live. The older claimed live marker `7e8571a26447538db249c862ad009487cce119bc` is unverified and must not be treated as current. No successful deployment of the current candidate has been observed. Keep credentials out of chat, source control, command history, and logs.

## Guarded production deployment and same-release acceptance — issue #669

**Next concrete sequence:**

1. Verify the trusted OVH host key against the pinned GitHub Environment host-key value before connecting the deployment workflow.
2. Confirm the recovery drill meets the separate clean-host requirement; if not, run it on a separate clean VPS using the exact backup release `f401a591a8abdade557827c09412dc3166fb9de2`.
3. Verify the current exact-master artifact and all guarded deployment preflights before dispatch. Artifact `11435103773` expires 2026-10-13 18:30 UTC.
4. In GitHub Environment `production`, set `FULLWORTH_PRODUCTION_SSH_HOST` to `40.160.137.55` only after the host-key match is established. At workflow dispatch, enter the same host into `confirm_production_host` and approve the guarded deployment.
5. Confirm automatic auth smoke, public API/Web readiness, running image release, and verified release marker all identify `c8858a38b38d7eb0888d273b9a1a409308e565b4`.

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
3. Add the actual CI/security check contexts that must block merge when failing.
4. Decide whether at least one independent approving review is required and set the approval count accordingly.
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
