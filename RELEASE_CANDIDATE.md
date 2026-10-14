# Release candidate readiness — 2026-10-14

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
| 2 | Protected-branch governance | Configuration verified: one approval, four GitHub Actions checks, strict current-branch policy, no bypass actors; independent review and verifier-detail fix remain | Repository administrator + independent reviewer |
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

Require at least one independent approving review and strict up-to-date checks. Configure these exact emitted check-run contexts from GitHub Actions (app ID `15368`):

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
