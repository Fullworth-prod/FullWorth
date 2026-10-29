# Sunday candidate deployment and recovery proposal — NOT APPROVED

This is a preparation document. It grants no production/master promotion, deployment, spending, new credential/grant, secret/account change, private financial export or destructive recovery authorization.

## Current identity and decision

Engineering work: #1017, currently sourcefc6ea59ec97d8a410fdec5a32e4510c9edf60466; all final-head checks/review/integration are still required. This source is not an approved deployable master SHA.

Last independently fetched master:9540b2616841e9794d8f8c07616edcd1e339982c. Last separately recorded served production release:9540, not a fresh host observation. Verify API, Web and client identity at test time. RELEASE_CANDIDATE.md begins with October15 markers; treat them as historical in light of newer verified context, not current deployment evidence. Reconcile that document in the final handoff without erasing history.

Decision: HOLD. Prepare the candidate and testing package; do not dispatch production workflows. Physical testing on an older served release does not certify new statement-history behavior.

## Concrete approval packet to complete

- One frozen integrated development commit and runtime tree; exact source/tested/integrated correspondence and final-head required/applicable check results.
- All remaining reproducible Sunday-journey defects and their dispositions; never call a merely green unit suite full readiness.
- Proposed development-to-master PR, its diff scope and resulting exact master commit. Promotion itself needs separate explicit approval.
- Successful exact-master push CI, fresh unexpired production image artifacts, their provenance/attestation/SBOM verification and matching release revision. PR artifacts or an older successful master run cannot stand in.
- Existing configured production host/environment identity confirmed without exposing credentials. Do not create a key or broaden access to complete this field.
- Current database migration delta and compatibility assessment, protected backup availability, both keyring coverage and actual recovery-evidence limitations.
- Proposed maintenance/test window, operator access using already authorized means, stop conditions and post-deployment smoke scope.
- Previous verified served release, artifact availability/expiry and the conditions under which code-only rollback could be safe. Do not promise that it is safe without checking the schema/data transition.

The source of these requirements is the current .github/workflows/production-deploy.yml and deploy/deploy-production.sh, not a new deployment route. The workflow dispatch requires release_sha, explicit guarded-deploy confirmation and exact configured-host confirmation, runs from master and rejects a stale/different master commit. It downloads verified image artifacts for successful CI on that exact master release. No dispatch is performed by writing this proposal.

## Proposed authorized execution sequence, only after approval

1. Recheck current branch rules, source freshness, review requirements and candidate acceptance. Resolve divergence through a normal protected PR; no bypass/force push.
2. If separately approved, promote the reviewed candidate to master through the protected path and verify the integrated runtime tree.
3. Wait for that exact master's required release CI and fresh attested artifacts; inspect actual results and artifact availability. Stop on any missing or failed gate.
4. Before a separately approved deployment, verify the configured target, current served revisions, migration/backup/recovery prerequisites and existing operator access. No account/secret changes are implied.
5. Dispatch only the existing guarded production workflow for the explicitly approved exact master SHA and confirmed host. Preserve its validation, migration, backup, containment and failure behavior.
6. Verify running API/Web image revisions, release marker, public readiness and authorized authentication/read-only smoke on the same release. A green workflow alone does not prove every customer journey.
7. Record the time, approved master, actual served component versions, smoke outcomes and artifact references. Only then fill the Sunday release card and run the relevant physical/provider/document scenarios with owner consent.

This is an execution proposal, not permission to perform steps1–7 that exceed existing authorization. Engineering checks/read-only source review and preparation can continue now.

## Failure and rollback decision

The guarded deploy script explicitly warns that forward database migrations may already have run. After candidate runtime verification fails, it stops unverified public application services and preserves the previous verified marker. That marker is not proof that the previous application is running.

Do not blindly retry, reverse migrations, restore a database, restart old containers or assume switching image tags is safe. Preserve sanitized diagnostics and determine runtime/schema/data state. A tested forward fix or compatible code-only rollback needs a concrete review and appropriate separate approval; real database/file/keyring restore is destructive and requires its own explicit authorization and validated recovery plan.

The prior separate-runner recovery restored a protected older snapshot with deletion-denial checks; preserve it as partial evidence. It does not prove current nonempty representative statements/database/both-keyring clean-host recovery. Synthetic CI restores do not close this prerequisite. Expired temporary recovery access is not renewed by this goal.

## No-deployment alternatives and Sunday limitations

Existing Android CI packages and iOS simulator/unsigned device artifacts can support engineering review. Record their exact source/digest and do not claim a simulator/unsigned IPA is an installed physical iPhone result. Native test packages point at the configured API; an older API's absent evidence flag should withhold annual estimates, not certify the new server implementation.

If no approved environment serves the candidate, mark new-server Sunday cases BLOCKED by release availability. Continue useful owner observations against the clearly identified currently served release, and use those results only for that release. Do not provision paid hosting, expose private CI databases, open a new tunnel/grant or deploy to manufacture a test entry point.

## Remaining separate acceptance

Actual Wells Fargo/first sync, expected-bill/private original facts, supported merchant access/renewal/prepaid evidence, representative real documents/payroll, physical Android/iPhone, actual alert receipt, controlled two-owner/privacy lifecycle, current nonempty real recovery/protected backup and qualified legal acceptance remain separate. No launch, billing enforcement, AI/creator activation or merchant outreach is approved here.
