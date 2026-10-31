## Verified upload recovery candidate — October 30, 2026

Latest runtime candidate in development: `bb0cdd70abc80f82afae6bd193a5b7549f3bcf56` ([#1020](https://github.com/Fullworth-prod/FullWorth/pull/1020)). This supersedes the older candidate card below; the Sunday goal remains active and prior history is preserved.

Two actual SDK regressions on unchanged production source proved original-file deletion after metadata persistence plus acknowledgement failure/cancellation (run41013048107:2 failed/3,659 passed). The correction preserves originals after uncertain save outcomes, detaches only the candidate and rethrows without save replay or false success. Owner account deletion includes finalized unreferenced originals in its existing quarantine lifecycle; invalid/temp/nested/link entries refuse enumeration. Failed uncommitted originals can remain until controlled account erasure. No timed orphan deletion or real account erasure occurred.

Final source `00eac25bd3be7c222edfc5a42f8b8b46ec0963aa`, tested `6b860dbbd7127b4666576c3b8964a330be1b7b98` and integrated tree `be480f9e58bd93eb52589abb293b2b2ecb720c04` verified. All10 checks passed; [main run41013942600](https://github.com/Fullworth-prod/FullWorth/actions/runs/41013942600) executed3,665 .NET/753 Node with zero failures/skips and zero backend/offline build warnings/errors. All six scoped PostgreSQL checks passed. Container HTTP/parser/security/disposable backup/restore checks passed; this is not real recovery acceptance. Android job was conditional for server scope; no new native artifact is claimed. Strict rules, empty reviews/threads and disabled auto-delete were checked; no bypass. Native client artifacts from #1018 remain separately identified and do not certify this server revision.

[Sunday checklist](deploy/SUNDAY-TESTING-20261101.md) now includes interrupted-upload readback and retention limits. All source reviews remain scoped evidence: bank one-time completion/ownership; discovery sparse/conflicting currency preservation; independent subscription facts; upload ownership/status; refresh-family replay protection. Real provider/private expected bills/payroll/merchant/device/delivery/two-owner/document/privacy/current nonempty both-keyring recovery/legal gates remain open. Gated AI/creator stay off; cleanup unauthorized. Master9540b2616841e9794d8f8c07616edcd1e339982c remains unchanged; its separately recorded deployment has not been freshly host-confirmed. No deployment/promotion/access/account/spending/private export occurred. Candidate availability still requires the separate guarded proposal and approval; integration is not deployment.

---

## Sunday candidate checkpoint — October 29, 2026

The active objective is **FULLWORTH — READY FOR SUNDAY TESTING**, for November 1. This supersedes older active-goal wording below; earlier checkpoints remain historical.

Verified runtime candidate: `c07c5b739487252fdd17282690c6ab8c70257760` in development. [#1017](https://github.com/Fullworth-prod/FullWorth/pull/1017) qualifies yearly statement estimates from owned complete consecutive monthly coverage, usable same currency and matching deterministic math, and orders native detail by billing period. [#1018](https://github.com/Fullworth-prod/FullWorth/pull/1018) rejects malformed native detail snapshots and qualifies unsupported historical explanation estimates while preserving original saved words and raw history. Missing evidence stays unavailable; monitoring does not establish merchant renewal.

#1018 source `2e04866bbcce4d490a00d0d889a2d054a28fbc69`, tested merge candidate `891afab79f5480d52e12b3165741a25e4ffc71df` and integrated runtime tree `a1bdbfbf7dfd9a101c3f203d93a322ef5b7a569c` were verified. All 12 checks passed, including required backend/Android/container/dependency checks, Chromium/WebKit regression, APK/emulator, iOS simulator and unsigned device packaging. [Main run](https://github.com/Fullworth-prod/FullWorth/actions/runs/40843332700) executed 3,659 .NET and 753 Node tests, zero failures/skips; backend/offline builds reported zero warnings/errors. These are overlapping synthetic scopes, not real provider, physical-device or production recovery acceptance. Branch protection remained active, review threads empty and branch auto-deletion disabled.

Fresh repository master: `9540b2616841e9794d8f8c07616edcd1e339982c`. The same separately recorded deployed release has **not** been freshly confirmed from the host in this pass. No deployment or master promotion occurred. A candidate in development is not a served release.

Use [the Sunday checklist](deploy/SUNDAY-TESTING-20261101.md), [private evidence template](deploy/SUNDAY-EVIDENCE-TEMPLATE-20261101.csv) and [separate deployment proposal](deploy/SUNDAY-DEPLOYMENT-PROPOSAL-20261101.md). Candidate availability and remaining journey review must be resolved before calling the entire package ready. Keep real provider/expected-bill/merchant/payroll/device/delivery/two-owner/document/privacy/recovery/legal gates open (#972, #912, #762, #970, #293, #251, #258, #291, #669). Record results privately; no credentials or financial records in public GitHub. Creator/gated AI remain off; cleanup #245 remains unauthorized. Earlier source fixes are completed engineering, not substitutes for external acceptance.

---

# Release readiness checkpoint — 2026-10-15 UTC

**Guarded deployment and authenticated smoke passed. Broader launch remains on hold pending acceptance.**

Current master candidate: `b275ad413a0532cdd2c42ee6ab3f9a1a4ed3fdd2` (not verified deployed). Last verified live release: `457e7e4b77a88714e8870e369a1aaf17e76e2de2`.

- On October 15, the verified Web/PWA hybrid redesign was promoted to `master` at `b275ad413a0532cdd2c42ee6ab3f9a1a4ed3fdd2`; [exact-master CI](https://github.com/Fullworth/FullWorth/actions/runs/38696477773) passed. No guarded production deployment of this newer SHA is established. A later operator check reported SSH port 22 unreachable on the VPS; resolve host console/SSH access and reverify runtime before any further guarded deployment. Preserve last verified deployed release identity separately from repository state.

- [Guarded production deploy #23](https://github.com/Fullworth/FullWorth/actions/runs/38690114274) succeeded on 2026-10-15 at 06:05 UTC. Pinned SSH, exact-release provenance/SBOM attestations, image identity, database-role separation/migrations, native OCR and kernel containment, file-backed secrets/non-disclosure, permissions, runtime/exposure, and public API/Web readiness passed. The host's verified release marker matches `457e7e4b77a88714e8870e369a1aaf17e76e2de2`.
- [Production Auth Smoke](https://github.com/Fullworth/FullWorth/actions/runs/38690396582) passed on the same release: API login/refresh, protected read endpoints, non-admin denial, export secret/storage boundaries, Web cookie login, authenticated app/BFF, antiforgery issuance, and logout invalidation. Mutation probes were disabled; this does not prove two-user ownership or statement/provider acceptance.
- [Exact-master CI #1797](https://github.com/Fullworth/FullWorth/actions/runs/38681858810), [Governance #36](https://github.com/Fullworth/FullWorth/actions/runs/38681858771), and all three exact-master CodeQL analyses passed. Attested production image artifact `11948796248` expires 2026-10-22 04:32 UTC. PR #748 fixed the OCR cgroup observer race without relaxing limits; PR #749 promoted it.
- Direct post-deployment verification confirmed release identity, public HTTPS readiness, private exposure boundaries, the Owner role, disabled subscription enforcement, active backup/readiness timers, configured alerting, and append-only backup-client policy. Snapshot availability is verified; a new post-deployment snapshot, separate clean-host recovery, provider immutability, and external alert delivery are not established by these checks.
- Keep payments/subscription enforcement and AI-derived persistence disabled. Broader launch remains gated by controlled statement and two-user ownership acceptance, Plaid lifecycle/payroll acceptance, physical-device checks, actual reboot recovery, clean-host recovery with real statements and both key rings, provider-enforced immutability, external alert delivery, and qualified legal/license review.
- Next: collect those remaining acceptance results against this deployed release. Preserve production data and the existing manual guarded deployment path; do not redeploy merely to publish handoff documentation.

## Previous release checklist (historical; checkpoint above supersedes stale candidate/live/governance claims)

The matrix and controlled acceptance scenarios below remain useful. Old commit IDs, expired artifacts, no-live-release statements, and one-review requirements describe prior checkpoints; current policy allows zero approvals while retaining mandatory PRs, strict required checks, and no bypass actors.

# Release candidate readiness — 2026-10-14

## Owner-approved release policy — 2026-10-14

On 2026-10-14 the owner explicitly approved removing the independent-review requirement to release as a solo maintainer. The approved policy permits zero required approvals while retaining mandatory pull requests, strict current-branch checks for `Backend build and tests`, `MAUI Android build`, `Linux production container`, and `Dependency review` (GitHub Actions app 15368), thread resolution, deletion/force-push restrictions, and an empty bypass list. Read back the saved ruleset before merging; authorization is not evidence that GitHub saved it. Independent review is no longer a release prerequisite under this decision. Host identity, real recovery, attested exact-master artifacts, and deployed acceptance remain required.

This decision supersedes earlier independent-approval requirements below. It does not mark production as deployed or waive data-recovery gates. Physical-device acceptance follows verified deployment.

**Decision: HOLD for production.** Repository verification and operational acceptance are separate gates. No current live release is verified. This checklist is the acceptance contract for the next candidate; it does not authorize a deployment.

## Candidate identity and verified repository evidence

- Integration PR [#739](https://github.com/Fullworth/FullWorth/pull/739) merged as `b425fc39bd28a333a73d9aca46c7d0367752b951`.
- Exact integration head `ccac12913064599b6f909cf0656510150f6bdca2` passed [FullWorth CI #1775](https://github.com/Fullworth/FullWorth/actions/runs/38507907171), [Dependency Security #865](https://github.com/Fullworth/FullWorth/actions/runs/38507907139), [Android internal #78](https://github.com/Fullworth/FullWorth/actions/runs/38507907153), and [iOS internal #72](https://github.com/Fullworth/FullWorth/actions/runs/38507907191). Backend: 1,346 tests, zero failed/skipped. Container validation included native OCR, parser containment, encrypted backup, and isolated restore.
- PR [#740](https://github.com/Fullworth/FullWorth/pull/740) merged as `6c9cdd0d577e8945acee9949e577f8c6d4439e99`. Exact head `16bdda005d4d9d8f00ac28a9604a2fe0faed6d42` passed [FullWorth CI #1778](https://github.com/Fullworth/FullWorth/actions/runs/38509229578) and [Dependency Security #868](https://github.com/Fullworth/FullWorth/actions/runs/38509229591). All 1,348 backend tests passed, none failed/skipped; both backend builds report zero warnings/errors. Linux production-container validation passed.
- PR #741 merged as `a48183d44582e79985c332f6f59125127b9001e3`. Exact head `a252cf71b0398d81b036f9aaab31f11902e44aef` passed [FullWorth CI #1779](https://github.com/Fullworth/FullWorth/actions/runs/38509349648) and [Dependency Security #869](https://github.com/Fullworth/FullWorth/actions/runs/38509349672). The verifier requires actual emitted job contexts; behavioral tests reject workflow-name-only settings and each missing required job. The administrator configuration gap is now closed; see the verified policy below.
- Current master remains `c8858a38b38d7eb0888d273b9a1a409308e565b4`. Its artifact `11435103773` expired on 2026-10-13 at 18:30 UTC. It is historical evidence, not a deployable fallback.
- Read fresh branch refs and PR check-runs before promotion. The final master merge SHA, not a PR head or a CI synthetic merge SHA, is the release identity used by image attestations and deployed evidence.

## Changes requiring acceptance

The candidate adds calendar-month comparison coverage, planning-evidence revalidation, currency-preserving generated text, current-statement reminder reconciliation, conflicting-extraction review gates, safe negative-credit parsing, database money-range/cent-precision checks, updated pinned .NET images, native OCR compatibility, and corrected governance verification.

No schema migration is introduced by the financial-evidence milestone. This does not establish that the complete development-to-master promotion has no migration changes; inspect the final promotion diff and use the existing guarded migration path.

Calendar coverage is not proof a charge recurs. Annualized impact remains a projection from comparable monthly amounts. Currency formatting performs no exchange conversion. Reminder reconciliation manages the latest statement's obligation, not a debt ledger or periodic expiry job. AI-derived persistence and subscription enforcement remain disabled.

## Ordered release gates

| Order | Gate | Current evidence / closure condition | Responsible role |
| --- | --- | --- | --- |
| 1 | Final repository candidate | Exact-head backend, container, dependency/security and applicable mobile checks pass; promotion diff reviewed | Engineering |
| 2 | Protected-branch governance | Owner approved zero approvals; verify saved settings, mandatory PRs, four strict GitHub Actions checks, no bypass actors and updated verifier | Repository administrator / maintainer |
| 3 | Master artifact | Merge the verified promotion through a PR; exact master CI, CodeQL, governance and signed image/SBOM publication must pass; retain artifact ID, digest and expiry | Engineering |
| 4 | Trusted target | Reconcile OVH inventory with failed run #15; independently verify configured host and pinned SSH key | Infrastructure operator |
| 5 | Recovery credential and real recovery | Separate least-privilege read-only credential, actual off-host encrypted repository, isolated clean-host restore of database, real statement files, and both key rings | Recovery operator |
| 6 | Immutable recovery points | Provider control-plane proof that production credentials cannot delete or weaken protected snapshots | Backup administrator |
| 7 | Guarded deployment | Only after pre-deployment gates close, dispatch the existing master-only workflow for the exact fresh release; verify marker, auth and public readiness all match | Release operator |
| 8 | Deployed security and statements | Same-release host, containment, secret, runtime-role, two-user ownership and controlled statement checks | Security / acceptance operator |
| 9 | Provider and installed device | Plaid connect/update/reconnect and payroll checks; physical Android installed-PWA checks; real iPhone if in scope | Acceptance tester |
| 10 | Operational lifecycle | Observe external alert delivery, controlled account deletion, reboot/recovery and same-release health | Operations |
| 11 | Commercial and launch approval | Qualified Terms/Privacy/license review and stage-specific external approvals | Legal / launch owner |

Do not replace a missing gate with an unchecked claim, a simulator result, a local CI restore, or evidence from another release.

## Verified repository governance

Ruleset 24230011 was saved through the user-approved browser and verified by GitHub API readback on 2026-10-14: active on `master` and `development`, one required approving review, strict up-to-date checks for `Backend build and tests`, `MAUI Android build`, `Linux production container`, and `Dependency review`, all bound to GitHub Actions app 15368. Existing deletion/non-fast-forward/thread restrictions and the empty bypass list are preserved.

The verifier must read branch targeting from the full ruleset detail response; GitHub's list response contains metadata only. This change repairs that false-negative bug with realistic fixtures. Local regression and replay of freshly read GitHub metadata pass; exact-head CI and a live workflow run must still be checked. No independent PR approval or deployment is implied.

Open [FullWorth protected branches](https://github.com/Fullworth/FullWorth/rules/24230011). Preserve its coverage of `master` and `development`, active enforcement, deletion/non-fast-forward restrictions, PR requirement, thread resolution, and empty bypass list.

The owner approved zero required approving reviews; retain mandatory PRs and strict up-to-date checks. Configure these exact emitted check-run contexts from GitHub Actions (app ID `15368`):

| Workflow | Required check-run context |
| --- | --- |
| FullWorth CI | `Backend build and tests` |
| FullWorth CI | `MAUI Android build` |
| FullWorth CI | `Linux production container` |
| FullWorth Dependency Security | `Dependency review` |

The workflow display names `FullWorth CI` and `FullWorth Dependency Security` are not the emitted job contexts. Requiring only those names can wait forever for nonexistent checks and omit the actual jobs.

After saving, read back the ruleset and run `bash deploy/verify-github-branch-governance.sh` through the protected GitHub workflow or the documented authenticated operator environment. Do not expose its token. The connector remains read-only for administration; the configuration was saved through the authorized browser session. A successful settings save is not an independent review of the release PR.

## Same-release financial acceptance

Use controlled documents with operator-known expected facts on the exact deployed release. Keep raw documents, extracted text, account identifiers and provider payloads out of issues/logs. Record only release SHA, UTC time, scenario, expected result, observed pass/fail and protected evidence reference.

| Controlled scenario | Required result |
| --- | --- |
| Consecutive calendar months including February/leap year and month-end anchors | Valid comparisons use exact observed amounts; projection is distinguished from provider recurrence |
| Four-week, partial, quarterly/annual, gap or overlapping periods | No unsupported monthly comparison or annualization |
| Saved change with inconsistent owner/stream, amount, arithmetic or currency evidence | No planning fact derived from the invalid saved change; another user's data stays inaccessible |
| EUR/GBP/CAD under a non-US display culture | Generated explanation/alert retains source currency and exact signed amount; no false dollar label or FX conversion |
| Correct current statement due date; later upload an older statement | One current managed reminder; historical upload cannot replace it |
| Latest statement has no payable future obligation | Managed reminder clears without affecting another owner or unrelated reminders |
| Equal-priority conflicting totals or conflicting explicit dates | Statement stays in review; no persisted statement/change/alert fact from unresolved extraction |
| Equivalent repeated labels and valid higher-priority amount label | Deterministic extraction follows the documented precedence; repetition alone is not a conflict |
| Parenthesized credits, including extreme decimal endpoints | No positive debt fabricated and no overflow-driven persistence |
| Fractional-cent or out-of-storage-range total/line item | Reject before staging financial rows |
| Comparable amount with annualized impact outside database range | No overflowing stored change; valid statement remains preserved |
| Representative text PDF, scanned PDF, JPG/PNG | Correct extraction, matching, deterministic math, explanation and alert behavior; OCR containment remains healthy |

## Recovery and deployment sequence

The reported snapshot `726a1e85`, manifest release `f401a591a8abdade557827c09412dc3166fb9de2`, was restored in an operator-assisted isolated drill on `vps-32597e02`. It contained zero statement uploads. This is not proof of actual statement-file recovery, separate clean-host/compromised-host recovery, current read-only access, or provider immutability.

Follow the existing runbooks rather than starting an empty public stack:

1. [Backup trust separation](deploy/README-BACKUP-TRUST.md): verify separate backup, recovery and maintenance authorities.
2. [Technical evidence](deploy/README-PRIVATE-BETA-TECHNICAL-EVIDENCE.md): run the real clean-host recovery proof with protected configuration outside the checkout.
3. [Production operations](deploy/README-OPERATIONS.md): use the guarded exact-master artifact/deploy path only after trusted target and recovery gates close.
4. [Acceptance evidence](deploy/README-PRIVATE-BETA-ACCEPTANCE-EVIDENCE.md): collect same-release Plaid and installed-device evidence.
5. [External alert observation](deploy/README-ALERT-OBSERVATION-PROOF.md) and controlled reboot proof: record actual observations, not just script success.
6. Use the existing technical, acceptance and external-approval verifiers for the intended launch stage. Do not fabricate completion files.

A failed preflight must preserve data and the verified marker. Do not bypass it with a raw Compose startup, remove production volumes, weaken the host confirmation, broaden backup credentials, or select an expired/different-release image. The existing guarded deployment/operations runbook remains authoritative for failure handling and rollback.

[Issue #669](https://github.com/Fullworth/FullWorth/issues/669) tracks same-release acceptance; [#291](https://github.com/Fullworth/FullWorth/issues/291) retains the four open security evidence/governance gates.
