# Service swap persistence correction — October 31, 2026

## Demonstrated failure and scope

Issue [#1024](https://github.com/Fullworth-prod/FullWorth/issues/1024) records the old production9540 parser returning503 because its dedicated cgroup swap limit became unlimited despite equal Docker memory/total-swap limits. Reapplying those existing limits and setting the existing systemd scope's runtime MemorySwapMax=0 restored readiness without restart. The exact production trigger/timing remains unproven.

Installed runc1.3.5 vendors cgroupsv0.0.4. Its [systemd resource generator](https://github.com/opencontainers/cgroups/blob/v0.0.4/systemd/v2.go) omits converted swap zero; [upstream #74](https://github.com/opencontainers/cgroups/issues/74) describes loss of that limit on service-manager reload. This source match explains why a kernel-only repair does not correct the manager's future behavior. It does not prove which production event caused the incident.

## Correction

The three services with explicit equal memory/memswap limits (API, parser-worker and Web) receive the documented OCI annotation `org.systemd.property.MemorySwapMax: "uint64 0"` through a shared production Compose annotation map. Read-only live metadata also found API/Web manager infinity with kernel zero; their existing configured no-swap boundary has the same vulnerability. No limits are added to services that had none. The manager owns the same no-swap boundary already intended by equal memory/memswap limits. See [tagged runc auxiliary properties](https://github.com/opencontainers/runc/blob/v1.3.5/docs/systemd.md#auxiliary-properties) and [Compose annotations](https://docs.docker.com/reference/compose-file/services/#annotations).

CPU, memory, PID limits, child containment, capabilities/identity drop, network isolation, TLS, readiness rejection and write behavior remain intact. There is no global scope drop-in, host package/runtime upgrade, new credential, production daemon reload or data change.

## Regression and acceptance

CI adds deploy/tests/service-swap-persistence-tests.sh after existing delegation verification. It requires a disposable GitHub-hosted Actions context, systemd and cgroupv2; refuses local/self-hosted/missing-run contexts before Docker/systemd; checks actual kernel zero and manager zero for all three services; reloads only that isolated runner's manager; checks both again, all three unchanged PIDs/start times and pinned internal parser readiness. It does not repair limits to make the test pass.

Local public-fixture shell syntax and three refusal contexts passed. The initial unchanged-Compose source c6f33f12ac6b6a3af02b56cd6ef2bddb19a6e026 failed at the missing manager zero boundary in run41232038277/job124628095390. Parser-only correction0c9d61903918313b42180df2cb3f008d157f3869 passed the actual isolated reload without restart/readiness loss, but its later passkey fixture timed out during password login (run41232318975/job124628921035). This is not a passing full container result or a diagnosed passkey product defect. Final scope also preserves API/Web's existing boundaries. Record corrected final-head protected execution in [#1026](https://github.com/Fullworth-prod/FullWorth/pull/1026). Do not merge a failing test, bypass checks or infer combined success from older heads. Normal per-document memory/OCR and disposable backup/restore checks remain required.

This is a development correction. Approved masterccdb0cc6 and the freshly verified live9540 remain separate identities; the earlier exact-candidate approval does not silently promote this new configuration. No new release is deployed here. Keep #1024 open until the durable configuration and same-release live manager/kernel/readiness evidence are established. Physical devices, providers, real recovery and legal acceptance stay open.
