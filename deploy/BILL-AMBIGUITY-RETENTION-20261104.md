## Verified integration — November 4, 2026

PR #1047 merged normally into development as `4bcee5b7a4e23fc09720c32d4e8a905c76c5b122`. Source `c3736869a80043d805675fcf5ae20848a1eea29a`, tested merge `112da0c993702c25f7bc19a01d4cf7571ab33847` and integration share tree `d010d4251baa86f62bdf04317a69ed34447d71f5`; all six integrated files read back exactly. Source branch `codex/bill-ambiguity-retention-20261104` is retained. Current strict four app15368 checks/PR/thread rules and no bypass were verified; reviews/threads were empty and repository auto-delete remains false.

[Final CI41892072296](https://github.com/Fullworth-prod/FullWorth/actions/runs/41892072296) passed 3,719 .NET and 769 Node tests, zero failed/skipped and zero backend/offline build warnings/errors. All 19 new service cases passed alongside the unchanged source-account coverage-retirement test. All six financial check-runs passed. [Bank lifecycle41892072167](https://github.com/Fullworth-prod/FullWorth/actions/runs/41892072167) executed 38 durable lifecycle/retention cases (12 new) and 12 preflight tests. Actual configured Redis/version/auth boundary and synthetic-owner Web/BFF smoke, parser/HTTP/no-swap and disposable encrypted restore passed. Restore receipt: `6012b461d11499748dd8fbf74a9acfc0abfcbd41611298d5d3e0375ff99e762b`. Native/browser builds were conditional for this backend diff, so earlier scoped device/browser evidence is not relabeled as new execution.

All five [candidate report41892072294](https://github.com/Fullworth-prod/FullWorth/actions/runs/41892072294) jobs completed. Downloaded archive hashes match metadata:
- compiled C#13354782214: `0957e3a2761868115720ee4e31834943d3677d6faa90144fabce4f9d846c0ce7`;
- full-source C#13353997220: `749915864cca7beefacd441632fa2aa970b2d04eeaa23a8dd3a37d267f385551`;
- JavaScript13353667535: `35b3afa6e12f123bca5ceb8f74bf5e9ba97fe93275a5fb76eab8b4fac62ded02`;
- Actions13353802354: `1ea38af0e9d3c40e36a2e314ed4235426612f111adc9b3e4ccf02f21aea6b68f`;
- Python13354116771: `8a66c71c39659f255d293a1b9f59558063044947824ca0d5390c5cb4ab657592`.

C# retains partial extraction: one compiled /116 full-source error notifications. Both retain `cs/user-controlled-bypass` at ExternalIdentityController.cs:157 (existing consent-gate context); JS retains `js/missing-origin-check` at transaction-filter-worker.js:55 (dedicated private-channel context). Actions/Python have no query findings. These results preserve prior limitations and are not a clean vulnerability verdict or uploaded/default development scan. No suppression, scan setting, alert disposition or permission changed.

No current private/production incident or release acceptance is inferred. Master b424 and last November3 independently served9540 remain unchanged. The following development history is preserved; pending statements below refer to earlier attempts.

---

# Existing bill ambiguity retention — 2026-11-04

## Demonstrated source defect

Corrected tests-only source `c490c0e4db2b4047312128462afb34c86f94057c` leaves application code unchanged (exact API source readback confirmed). [CI 41791169790 / backend job 126444649499](https://github.com/Fullworth-prod/FullWorth/actions/runs/41791169790/job/126444649499) executed 3,717 tests: **10 failed, 3,707 passed, zero skipped**. The ten new failed cases are active/inactive variants of a duplicate posted date with same currency/account, conflicting currency, unknown currency, conflicting account or unknown gateway-account evidence.

Four already linked monthly owned charges span January through April. An extra same-day charge makes the existing conservative cadence detector return no candidate. Currency/account conflict guards ran only over returned candidates. The deactivation pass therefore missed that unresolved source ambiguity, deactivated an active automatic obligation (actual1/expected0), or removed its four original associations from an inactive obligation (actual4/expected0). No real/private account cause or production incident is inferred from these synthetic source counterexamples.

Initial tests-only source `c8646f595c09b79804afd5a65568dc5f4323b3e7` did not execute: CI41790740434/job126443270634 had two fixture compile errors and one warning from incorrectly treating the persisted account FK as nullable. The corrected baseline keeps persisted FKs concrete and models unknown account evidence through the actual nullable discovery-gateway contract. The compile-failed run is not application-failure evidence.

Intermediate corrected-source attempt `8ae0f3bcdadfb769541f51aed7a4e006d5e762a4` actually passed the twelve new PostgreSQL cases and all26 original lifecycle cases in CI41791828826/job126446733117. Follow-up builds then found a missing explicit source link: the main test project excludes the PostgreSQL executable directory and selectively includes reusable scenario files. The new helper was not yet linked there. That produced CS0103 in the focused/main test builds; the run is not final combined success. The correction adds only the reusable helper to the existing explicit includes, preserving generated-source exclusions and fixture safety tests.

## Correction

Duplicate-date retention protection now derives from every eligible normalized merchant group, independently of whether cadence returns a candidate. Duplicate posting dates protect an existing obligation and its original evidence from automatic deactivation/unlinking. Existing monthly-candidate currency/account conflict protection remains, with normalization semantics shared by its guards. Sparse histories with distinct posting dates retain the existing source-account coverage and retirement policy.

New ambiguous candidates remain unpromoted. Existing active/inactive state, timestamps and links remain intact; no extra association or alert is created for the rejected group. Normal rediscovery, supported categories/cadence, foreign-owner exclusion and the existing unambiguous covered-stale retirement policy remain. A later authoritative source correction can restore ordinary rediscovery. No history row is edited by discovery, no financial value/unit is guessed, no FX conversion or cancellation claim is added.

## Existing-policy regression and bounded correction

Source `44a38be8b9bf73c08d37fef8b507588c5be41427` built with zero warnings/errors and passed all six relational checks, but [CI41792208158/backend126448275653](https://github.com/Fullworth-prod/FullWorth/actions/runs/41792208158/job/126448275653) executed **1 failed / 3,718 passed / 0 skipped**. The existing `EveryLinkedAccount_MustMeetMinimumCoverage(60, true)` case expected retirement after complete coverage of both linked accounts; the new broad pre-cadence account guard incorrectly retained that sparse, distinct-date history. Its assertions are preserved. The correction narrows the new pre-cadence veto to the demonstrated duplicate-date condition and retains existing per-candidate currency/account protection. This avoids expanding the retirement policy without supporting recurrence evidence. Final combined execution is required.

## Targeted verification

- Seventeen baseline actual-service cases include ten demonstrated failures and seven controls. Final test source adds two further cases: normal rediscovery after an authoritative source removal, and independent valid discovery/alerting while another provider remains ambiguous. Final suite execution is pending.
- The existing guarded loopback PostgreSQL bank executable now includes twelve durable retention cases after its 26 lifecycle races/preflights. Eight collision cases plus normal/foreign controls use the actual Plaid discovery gateway and real ownership/FK constraints; two unknown-account cases deliberately adapt the nullable gateway contract over valid persisted rows. All use actual EF PostgreSQL persistence and separate verification contexts.
- The existing fixture guard remains: explicit ci-fixture-only marker, loopback127.0.0.1, exact fullworth_bank_lifecycle_fixture database and fixture role. No new endpoint, credential, grant, deletion/cleanup command or production database access. The fixture database dies with the existing hosted service.
- Final-head required checks, applicable financial PostgreSQL suites and full-candidate reports must execute and pass before protected development integration. Prepared tests are not a PASS claim. C# extraction limitations and existing contextual warnings remain subject to the actual final reports.

## Release and private evidence boundary

Master `b424cd9c08d612d9eadef57839f0dca8c81f2841` and last independently verified November3 served `9540b2616841e9794d8f8c07616edcd1e339982c` remain separate. No promotion/deployment, host mutation, access renewal, account/secret setting, private financial export, spending/activation, outreach, destructive recovery or branch deletion.

This correction does not identify why a particular real expected bill was missed, prove complete import coverage, cancellation, merchant renewal, real provider recovery, actual delivery or physical-device/recovery/legal acceptance. #762/#934/#669/#291 and the other real gates stay open. The nineteen real evidence-template cases must remain NOT_RUN until separately observed. Gated AI/creator stay off and creator terms proposed.
