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
