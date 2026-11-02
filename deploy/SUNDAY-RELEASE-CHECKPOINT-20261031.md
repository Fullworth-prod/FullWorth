## Release verification correction — November 2, 2026

Master remains `b424cd9c08d612d9eadef57839f0dca8c81f2841`; last independently verified live remains `9540b2616841e9794d8f8c07616edcd1e339982c`. No new live inspection or deployment occurred in this continuation. Auth correction baseline `6e739bc6026c45140552c6609863a3fa3799b848` contains the completed #1031/#1032 auth corrections; #1027 is closed for that demonstrated startup defect. Earlier pending-auth cards below are historical.

#1033 is a reproduced release blocker: the old deployment consumer requests unversioned SPDX while the actual signed predicate is SPDX 2.3. Proposed shared consumer now enforces six cryptographic verifications, exact SHA/master/CI signer/hosted runner, immutable repository/owner IDs and selected push run/attempt; supplied UTF-8/unambiguous SPDX content must equal its signed predicate, and image config/tag must match the intended release. The same consumer runs after signing and before master artifact publication. It retains exact successful-master CI selection, existing target approval, signatures and runtime gates.

Executed locally: twelve synthetic policy/transport tests passed. Separately, official checksum-verified GitHub CLI 2.103.3 verified all six actual signatures from public artifact13073795897 using downloaded public bundles and the shared consumer policy. Every signed SBOM matched its supplied document and all three embedded revisions matched master. ZIP SHA256 `94a86b473b2d85ca2c396a8c6935b8763e77ba7f454c56027e4731c3a59fccba`; expiry November8 06:40:09UTC. Offline public-bundle transport is explicitly distinct from deployment's online repository lookup. Protected final-head CI/integration and a future newly signed master publisher/consumer execution remain required; #1033 stays open.

Repository-governance run41501154849 failed on HTTP403 reading unauthenticated public rulesets. #1037 is now integrated in development as30a9952f1b8a04aa5f380d33fc751560300e7004 after all five checks passed, with source/tested/integrated treef6102f103923069db2673c98522aba0c2acbea21 matching. Actual authenticated governance41508052055 passed using existing Contents-read/Metadata-read permissions, and existing failure predicates/denied-access regression passed; no new grant or fallback. Exact historical403 cause remains unproven because its headers were not captured. Main exact-master CI41281607324 remains successful, and four CodeQL analyses passed; do not call every current master check green. Connected GitHub read of active ruleset24230011 confirms strict four checks, PR requirement, thread resolution and no bypass. Diagnose the HTTP failure without new grants or weakening governance; no unchanged rerun was triggered.

#1024 remains open until separately authorized deployment proves intended image identity, durable OCI annotation, manager/kernel zero-swap and readiness. Real provider/private expected bills/merchant/payroll, physical Android/iPhone, actual alert delivery, two-owner/privacy, current nonempty database/statements/both-keyring clean-host recovery and qualified legal acceptance remain separate. Keep gated AI/creator off and terms proposed. No production mutation, master promotion, new access, spending, private export, destructive recovery or branch deletion.

---

## Verified development auth fix and current release boundary — November 1, 2026

Login preservation #1031 is integrated as `291ca6c17ebb2cd17d2926dfc982f546d39b057a`; final source2fc844f3/testedcf133aa4/integrated treebc76022b808a99eb671ec227d8906baadc77dc91 match. All five final-head checks passed: main41284071141, pinned browser41284071267, dependency41284071133; actual3,665 .NET/753 Node zero failed/skipped/backend+offline warnings/errors. Both engines preserve login fields through actual startup and completed appearance changes; native invalid refusal, one valid transport and the original full real disposable virtual-passkey/MFA/removal flow passed. Downloaded/hash-verified visual artifact13073879482 and inspected desktop-login/mobile-two-factor screenshots support bounded UI acceptance, not physical devices.

#1032 adds six actual registration/recovery/reset startup counterexamples: CI41285330965/job124785360770 found entered fields/consent lost on all three unchanged pages in Chromium/WebKit, with zero submissions and no account/mail/password changes. The proposed three-page static native-form correction still needs its own final-head checks. Do not certify a prepared fix or close the issue from earlier login tests alone.

Frozen master remains `b424cd9c08d612d9eadef57839f0dca8c81f2841`; independently verified production remains `9540b2616841e9794d8f8c07616edcd1e339982c`. These newer development fixes are not in that master artifact or live. Preserve the master bundle and existing guarded release path. Deployment/master promotion, private recovery/new access and all external acceptance need their existing prerequisites and separate authorization; this record grants none. #1024 stays open for intended-release durable live verification. Earlier cards below are historical.

---

## Current master and served-release disposition — November 1, 2026

Owner-merged [#1029](https://github.com/Fullworth-prod/FullWorth/pull/1029) is master `b424cd9c08d612d9eadef57839f0dca8c81f2841`; exact-master CI41281607324 and eight observed checks passed. Source/tested/integrated tree4b4e74699fbc6d34bf4ad040d5ca5d49f99a278d matches. Production remains independently verified `9540b2616841e9794d8f8c07616edcd1e339982c`. Promotion is complete; deployment and durable live #1024 acceptance are not. Do not request promotion of the already merged #1029 or use the old ccdb artifact for the new master.

Fresh artifact13073795897 expires November8 06:40:09UTC. ZIP SHA256 `94a86b473b2d85ca2c396a8c6935b8763e77ba7f454c56027e4731c3a59fccba`, three image revisions and published subject hashes were independently checked after download. Cryptographic provenance/SPDX verification must still pass through the existing guarded workflow before any use. Master container reload/HTTP/parser/disposable restore passed; master visual/passkey steps were conditional/skipped. This is not physical/provider/current-real-recovery acceptance.

Login-hydration input loss is reproduced in #1031; its proposed static native-login correction and two-engine/full-auth regression results remain pending. Preserve #1027's original failing record; a plausible mechanism does not certify its exact historical cause. This newer development correction is not deployed or included in the frozen master. Record candidate, client and served identities independently.

Browser control is available; no separate Cloud Work task is required or created. No deployment, new access, spending, activation, private financial export or destructive operation is authorized here. Current nonempty real-statement/database/both-keyring clean-host and all existing external acceptance prerequisites remain open. Earlier dated decisions/approval/tooling statements below retain their historical scope; they do not override this disposition.

---

# Latest development correction — October 31, 2026 UTC

[#1026](https://github.com/Fullworth-prod/FullWorth/pull/1026) merged into development as `ca848ad1e61f72a1408b7d87d4d656dbcd9dacec`. Final source9cfaa61e5b71822ef0a72b58eab8ce32979ded93/tested2e8c6661b00a01434f5affc534eafbdb627ca4e2/integrated tree146aa3a0242c0fa353306214ae0c5c69ed843eb0 match; all5 files read back exactly and source branch retained. Fresh strict ruleset/empty reviews+threads/no auto-delete checked. Master remainsccdb0cc6, live9540 remains independently verified; this later runtime configuration needs a distinct promotion decision.

All5 final-head checks passed: main41234428815 (backend124635121284, container124635121129, Android124635121408), pinned browser41234428884 and dependency41234428920. Actual3665.NET/753Node0failed/skipped/backend+offline+actual Android0warnings/errors. All-three actual systemd reload preserved manager/kernel zero and PIDs/start times; parser readiness and full hard containment/PDF/OCR/HTTP checks passed. Full rendered Chromium/WebKit themes/responsiveness/ARIA/subscription/privacy/Planning/recovery, virtualpasskey enrollment/firstfactor/MFAcode+passkey/removal with Spanish success and disposable encrypted restore554ea3096396f82c8e303e66440572066afd46930d84dd68b8325dd57f0675c0 passed. These are synthetic/disposable scopes, not physical/provider/real recovery acceptance; no financial PostgreSQL workflow was triggered by this correction.

Unchanged-Compose reproductionc6f failed manager zero (41232038277/job124628095390). Parser-only0c9 passed actual reload but later virtualpasskey login-response wait timed out (41232318975/job124628921035). Final9cf did not reproduce that timeout; [#1027](https://github.com/Fullworth-prod/FullWorth/issues/1027) remains open with boolean-only diagnostics and unchanged submission/assertion/timeout. No passkey root cause or fix is claimed.

Read-only production inspection also found API/Web manager infinity with kernel zero and existing equal Docker memory/memswap. Set only those existing scope runtime properties to the already effective zero. API PID2307463/start2026-10-31T03:22:37.808812353Z and Web PID2309741/start03:23:52.424094734Z were unchanged; manager/kernel0 verified. Full production verifier passed again afterward. No production manager reload, service restart, deployment, package/runtime upgrade, private export or new access occurred. One SSH/connector timeout recovered; public API/Web readiness200 and fresh host verification do not show an outage.

See [durable correction](PARSER-SWAP-PERSISTENCE-20261031.md) and [new decision proposal](SUNDAY-DEPLOYMENT-PROPOSAL-20261101.md). The earlier master artifact/approval evidence below stays tied toccdb; it is not automatically valid for the new configuration. Existing cloud task choice remains pending; no other task was created or messaged. #1024 stays open until durable live evidence. Preserve every external acceptance gate and privacy boundary.

---

## Earlier approved master and preflight evidence

# Sunday release checkpoint — October 31, 2026 UTC

## Identity and authorization

Human approval was received for protected promotion of preserved runtime candidatebb0cdd70abc80f82afae6bd193a5b7549f3bcf56, then the existing guarded production deployment after prerequisites. Excluded actions remain excluded. Approval is not deployment evidence.

[#1023](https://github.com/Fullworth-prod/FullWorth/pull/1023) merged through a merge commit, retaining ancestry and source branch. Sourceeea457bd6fb2a8adae430e97842ab4ddbb48c678; tested PR merge2964c80e6bf934979dd8693956e7a3f8b6371641; promoted masterccdb0cc6be4ae649c0292d2e1bf0a2a97bd71cb4. All three trees equal8799ac7c7b50a856f01fb5f622e929e0046ca495. The source differs from preserved runtimebb0 by seven documentation files only. The promotion accumulated234files/42commits from oldmaster9540; it was not a new runtime rewrite.

All23 final promotion-head checks passed. Ruleset24230011 was active with mandatory PR, four strict Actions checks, zero required approvals, resolved threads and no bypass; reviews/threads were empty, auto-delete false. Branches were preserved. No deployment has occurred in this pass.

## Exact-master execution and artifacts

[FullWorth CI41166392224](https://github.com/Fullworth-prod/FullWorth/actions/runs/41166392224) is a successful master push for exactlyccdb0cc6be4ae649c0292d2e1bf0a2a97bd71cb4. Actual backend log124434775755 records3,665 .NET/753 Node, zero failed/skipped and backend/offline builds zero warnings/errors. Android build and full Linux production container passed. The container log confirms HTTP/security/parser/cgroup/PDF/OCR boundaries and disposable encrypted backup/restore. These fixtures do not establish real recovery acceptance. Visual/browser steps were conditional/skipped on this push; earlier promotion browser and six PostgreSQL checks remain separately scoped evidence.

All14 observed master check-runs passed, including four CodeQL analyses, governance, Android packaging/emulator and iOS simulator. No unchanged jobs were rerun.

| Artifact | Identity | SHA256 / expiry |
| --- | --- | --- |
| Production images + SPDX |13029104536; exact-master CI; six files required by guarded deploy|archive ddc6ea60393563f240b22d2eaad6d4b874061a3c08fc6d296df7796bc7ce3d46; Nov7 03:59:59UTC|
| Android internal |13029368725; run41166392034|archive01700614d2845098b4222d3cedb2af89462df2169cde929f940dfbe294d35ec3; Nov14 04:01:48UTC|
| iOS simulator |13029630787; run41166392039|archive9657da39f980cfc2204af1bdc572915b71c04c9b1af0b11c3148d81ac5aa181b; Nov14 04:05:36UTC|
| iOS simulator launch |13029790421|archive3536aec800b28d88ee673afde83f663642cc23c6952f7f121f6c27b5da3d80d4; Nov14 04:05:30UTC|

Production image job124434775622 published provenance62031811 and SBOM attestations62031711/API,62031742/parser,62031776/Web. Image archive subject digests: API5ef6878fcf1c80535e1067ac1aa3bf87d745958bd1ba20a22caaac464f7b421f; parser6a41cd1c913adf81b48927739b126e308b7c7c180290d7e90ba6d62a52e5465d; Web35e8427569741d25b136e1c4d9633e308adfd9a44fcd4a5da34c85e8fca3d8a4. Metadata/log publication was inspected; the deployment workflow must still download and cryptographically verify both provenance and SPDX for each image before use. A green publication step is not independent verification of downloaded bytes.

Android archive was independently downloaded, exact two-file set inspected and archive digest verified. APK30394930bytes SHA25603b5cffc375f4fe6f4d111c82a384c19356448ad3eedc15bca256c0329df2dc0 matches its checksum file. It was not installed on a physical device. Internal ephemeral signing may prevent upgrade over an older internal build; preserve existing app data and record a mismatch instead of uninstalling blindly.

## Fresh production preflight and repair

Existing pinned SSH access verified the configured production host, deploy-owned0700 /opt/billwatch, clean master checkout, release marker and API/parser/Web9540b2616841e9794d8f8c07616edcd1e339982c. No financial rows or private documents were exported. No new access was granted.

Parser readiness was503 because live parent memory.swap.max=max, although Docker memory and total-memory-plus-swap were each536870912. CPU, memory and PID limits were finite. Live swap usage was0. Reapplying the same configured Docker memory/swap limits restored kernel swap.max=0 without restart. The systemd manager still reported infinity, so its existing container scope was set at runtime to MemorySwapMax=0. Manager/kernel now agree. The same parser PID1589517/startOctober26 remained. No protection was weakened. Root cause/timing and persistence after future replacement/reboot remain unproven; [#1024](https://github.com/Fullworth-prod/FullWorth/issues/1024) remains open.

After repair, pinned internal parser readiness returned ready; deploy/verify-production.sh and deploy/verify-beta-readiness.sh both passed. These verify configuration, permissions, release integrity/exposure/runtime identity, public API/Web readiness, role schema/exactly-one Owner, disabled subscription enforcement, backup/readiness timers, completed snapshot, retention/client policy and configured alert routing. No external alert was sent. These are old-release host prerequisites, not new-release acceptance.

Latest completed snapshot metadata:8ebe0578, October31 03:22:13UTC,189.915KiB; retention14daily/8weekly/12monthly/3yearly, append-only production mode. No new backup or recovery was initiated. Snapshot availability/policy metadata does not prove provider deletion denial, nonempty statements or both-keyring clean-host recovery. Earlier authorized separate-runner recovery remains partial evidence.

Read-only database preflight: BankTransactions relation499712bytes; latest migration20261025020500_AddPlaidLinkCompletionAttempts; zero other active/nonidle transactions at observation. The pending subscription migration adds a unique owner key and evidence/review tables, but can still take locks. Destructive Down drops evidence; old code deletion compatibility after evidence writes is unproven. No migration was applied.

## Next checkpoint

The existing production-deploy.yml requires masterccdb0cc6be4ae649c0292d2e1bf0a2a97bd71cb4, exact configured host and guarded confirmation. It verifies exact-master CI/artifact attestations, pinned transport, protected backup/migration path and deployed identity/readiness. The GitHub connector has no dispatch action. Browser inventory still reports local browsing disabled; no cloud browser is exposed in this task. A question to create a separate Cloud Work task is pending; none has been created. Do not bypass the disabled browser through another UI automation route or replace the guarded workflow with ad hoc SSH deployment.

Once supported dispatch access is available, recheck master/artifact expiry/target and current resource/backup prerequisites, then execute the already approved workflow once. Inspect actual failures before retrying. Verify the resulting live API/parser/Web and marker all match the intended release, then perform only separately permitted smoke checks. Do not claim that old marker means old services survived an interrupted deployment.

Until then, new-server Sunday cases are BLOCKED on availability. Use the checklist/private template; keep real provider, expected bills, merchant/payroll, physical devices, delivery, two-owner/document/privacy, current nonempty recovery and legal issues open. AI/creator remain off; no hourly automation, branch cleanup, access expansion, spending or destructive recovery.
