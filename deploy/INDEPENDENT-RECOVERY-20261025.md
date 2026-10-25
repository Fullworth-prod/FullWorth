# Independent recovery and protected-copy checkpoint — 2026-10-25

This adds a manual, production-environment, exact-master recovery workflow on a fresh standard GitHub-hosted Ubuntu runner. It uses the existing pinned production SSH transport to read a separately staged recovery-only bundle. It never retrieves the production environment or its write credential, publishes restored data, or uploads backup/log artifacts.

The source operator must first stage an owned mode-600 JSON bundle at `/home/deploy/fullworth-acceptance/recovery-runner/bundle.json`, outside the checkout, with exactly these fields:

- `version`: integer 1; `createdAtUtc`: timezone-aware ISO timestamp, at most 24 hours old.
- `repository`: the frozen R2 `immutable-recovery/<copy-id>/` Restic repository URL.
- `resticPassword`, `accessKeyId`, `secretAccessKey`: private Restic password and **separate read-only** recovery credentials; never commit or publish values.
- `snapshotId`: exact 64-character completed snapshot; `sourceRelease`: actual backed-up release SHA.
- `protectedCopyVerified`: true only after encrypted copy verification and provider-enforced production overwrite/delete denials.
- `canaryKey`: `recovery-readonly-probe/<24 lowercase hex characters>`, containing exactly `FullWorth synthetic read-only recovery permission probe.`. This synthetic object is deliberately outside the locked prefix.

The runner proves readable synthetic canary contents and requires explicit HTTP 403 / AccessDenied on both write and delete. Invalid authentication and HTTP 409 bucket-lock denials cannot substitute for read-only credential proof. It then runs the existing cryptographic, PostgreSQL, statement-manifest and both-keyring verifier against the frozen copy. Snapshot identity must match the staged exact snapshot. Only completion metadata is returned to the existing private evidence host.

Frozen recovery copies cannot write Restic lock files. The new `BILLWATCH_RECOVERY_IMMUTABLE_COPY=true` option enables `--no-lock` only for restore verification against the narrowly validated frozen R2 prefix. Live backup capture, retention maintenance and ordinary verification retain their existing locking behavior. Never set this option for a mutable repository. Provider protection must already be independently established; a matching URL alone does not establish bucket protection.

## Actual operator evidence, distinct from CI fixtures

On 2026-10-25 the existing live release `dd31836703bd110a30f303ed3ce9fd13c0f61d82` produced a fresh encrypted backup; all production readiness checks passed after services resumed. The completed snapshot is `46772e80141cfd2528cf3c17b885bb651fa1b20749b0d0e05db1d6218d2c90da`.

A server-side encrypted copy under the enabled provider bucket-lock prefix was verified object-by-object: 399 objects, 7,879,162 encrypted bytes; inventory SHA-256 `a7bf9cc1d2f852c98f53395c5210b2e51d8fa6729d8620e40785eddecc860f96`. Both synthetic overwrite and delete using the valid production credential returned HTTP 409 / ObjectLockedByBucketPolicy. Protected metadata is recorded outside the checkout at `/home/debian/fullworth-recovery-evidence/protected-backup-46772e80141c.json`.

The protected prefix is separate from the live Restic root, so normal capture, candidate-to-complete tagging, and lock cleanup continue working. No bucket-wide lock or retention deletion was enabled.

**Independent production restore remains pending.** The existing saved recovery credential failed with SignatureDoesNotMatch. A new bucket-only, read-only, 24-hour credential has been prepared in the Cloudflare UI but has not been created; browser security requires owner confirmation. Do not report a complete clean-host drill from the provider protection probe or synthetic tests.

A real zero-statement restore verifies the state that actually exists; it does not establish nonempty real-statement recovery, physical-device, provider, legal, or broader launch acceptance. This workflow leaves nonempty-statement acceptance explicitly unestablished. No deployment or feature activation is performed by the recovery workflow.

## Validation

Thirteen Python boundary/provider-denial tests and shell recovery-lock boundaries passed in isolated source execution. A separate real Restic synthetic read-only filesystem regression demonstrates the underlying lock behavior; it is not production/provider recovery acceptance. Final-head protected CI is required after these source changes.
