## Actual independent-run configuration-probe failure — 2026-10-25 UTC

The owner approved the 24-hour, single-bucket Object Read only recovery key. Actual provider reads passed and synthetic write/delete returned HTTP403/AccessDenied. The key was privately staged; no production write credential was transferred to the runner.

The first fresh GitHub-hosted production recovery run [40222367338](https://github.com/Fullworth-prod/FullWorth/actions/runs/40222367338) failed before acceptance. A separate real Restic diagnostic against the same protected snapshot established the initial `cat config` also acquires a repository lock: ordinary configuration read failed with a lock/access-denied error, while explicit `--no-lock cat config` and the frozen snapshot list succeeded. The initial verifier called the ordinary repository check before its guarded snapshot/restore operations.

The fix routes only the verification configuration probe through the existing guarded recovery wrapper. Ordinary verification, capture, listing and retention keep their existing locking behavior. The regression executes the real verification entry point against a backend stub that rejects lock writes and confirms it reaches database readiness; the old source fails that assertion. No recovery gate is waived and no write permission is granted.

Independent production recovery remains unproven until the corrected exact-master workflow actually passes. The source diagnostic is not separate-host acceptance. Existing protected metadata remains outside the checkout. A local temporary credential file remains restricted to the Windows owner and SYSTEM because automatic approval review rejected its deletion; the approved key expires after 24 hours.

---

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

Fourteen Python boundary/provider-denial tests and shell recovery-lock boundaries passed in isolated source execution. A separate real Restic synthetic read-only filesystem regression demonstrates the underlying lock behavior; it is not production/provider recovery acceptance. Final-head protected CI is required after these source changes.

The isolated database password is generated only in the runner process and passed directly to the drill. It is absent from persisted recovery configuration and Docker container environment. The drill materializes the database password/passfile in its private temporary secret directory; the hosted workflow requires tmpfs and uses /dev/shm for that directory. PostgreSQL receives a mounted password file. Historical manual recovery environments remain supported.

The backup password is also passed directly in memory and absent from saved recovery configuration. The hosted runner holds the received bundle, AWS credential file, and private recovery log in its per-run private /dev/shm directory. Both ephemeral password environment variables are unset before invoking Docker; the containers receive mounted secret files. Shell regressions cover legacy configuration, in-memory handoff, malformed inputs, no password leakage to Docker environment/output, and secret cleanup.
