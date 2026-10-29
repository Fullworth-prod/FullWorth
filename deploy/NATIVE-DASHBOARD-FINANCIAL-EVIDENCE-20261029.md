## Verified development integration — 2026-10-29 UTC

[#1013](https://github.com/Fullworth-prod/FullWorth/pull/1013) merged as f710711361592c352e0d636f3ff6acef730a1133. All13 checks passed on exact source78968f79b8cbca6840d410567f2dc5e9d601b0ce; tested36ef8d8fc0a2c231827054baa7634c5011c44692 and integrated tree24ccf9424a93b90e4cd38ff96bb15a3b5fb59cb9 match. Actual final execution: 3,567 .NET/748 Node, zero failed/skipped and zero backend/offline/actual Android warnings/errors; applicable container/relational/native packaging checks passed. Branch retained. Details, run/job IDs and explicit scope limits are in [the cleanup audit](MILESTONE-CLEANUP-AUDIT-20261027.md). These are development and synthetic/emulator/simulator results, not deployment, physical-device or real provider/private recovery acceptance.

The pending language in the preparation record below is historical. The complete milestones0–8 goal and external gates remain open.

---

# Native dashboard financial evidence — 2026-10-29 UTC

## Reproduced problem and resulting behavior

The active native MainPageViewModel reads bill-stream and alert services directly. It previously displayed latest debit amounts under MONTHLY BILLS without requiring monthly cadence, generated a partial yearly change headline after dropping changes with unknown cadence, counted missing current evidence as a zero-balance decrease and attached yearly suffixes to unavailable amounts. The legacy Core DashboardSummaryService is not used by this screen and is unchanged.

The dashboard now calls these values known bill amounts. A full yearly bill estimate requires every active bill to have a positive known debit, a valid raw ASCII currency and reliable monthly cadence. Meaningful changes retain the existing rounded absolute >=5 and relative >=10% rules, but require known positive current and previous amounts. Yearly change headlines require monthly evidence for every meaningful change, so an unsupported increase cannot turn the result into a partial reduction. Currency groups remain separate; no conversion, ranking or ledger mutation is introduced. Valid monthly estimates are explicitly estimates at current amounts, not confirmed renewal commitments. Unknown-cadence known amounts remain visible without an invented monthly rate.

The native bill-list client also requires HTTP 200 application/json with a nonnull list, nonnull records, nonempty unique IDs and nonblank provider names. JSON null, duplicate identities, pending responses and wrong media cannot overwrite a verified dashboard with an apparently empty or doubled account. Valid empty arrays remain successful. Existing dashboard error handling hides content, keeps the prior snapshot for a later explicit refresh and does not expose response bodies. There is no automatic write retry.

The API normally excludes inactive streams; the inactive-input case is defensive and is not a claim of a provider incident. HasTransactionEvidence and HasMonthlyCadenceEvidence are distinct server-provided evidence flags. No inference about merchant renewal, cancellation, prepaid terms or real-provider acceptance is added.

## Implementation and regression scope

Production: ViewModels/MainPageViewModel.cs, MainPage.xaml and Services/FullWorthApiClient.cs. Test project links the actual native dashboard and bill services alongside existing native auth links. Command/Shell test substitutes only satisfy isolated compilation; FullWorth.csproj excludes the entire test directory from the MAUI application. Device storage and network are disposable fakes, not real account access.

29 xUnit cases exercise public LoadAsync through the actual API client, authentication and bill/alert services: supported increase/reduction, sparse/unknown cadence, conflicting directions, missing/nonpositive current evidence, raw invalid currency, missing denomination, separate currencies, inactive defense, unchanged materiality, refresh state reset, confirmed empty account, seven invalid-read boundaries and shipped XAML financial copy. Invalid-read cases first load a verified snapshot, then verify error state, retained prior values, no raw-body disclosure and one read per endpoint per attempt.

## Executed evidence and outstanding gates

Before a repository branch was created, isolated actual-source execution passed all 29 cases. Running the same cases against the original production sources yielded nine passes and 20 controlled failures; these are regression counterexamples, not 20 customer incidents. Reflection awaits async test tasks and uses xUnit 2.9.3. The local adapter combines namespaces/implicit usings and substitutes the exact XAML text for repository-file lookup; it does not prove a full SDK build, MAUI rendering or device behavior.

Repository baseline: development 7eb783bec319142ff7c5874249e83adbcb9d9e7a, master 9540b2616841e9794d8f8c07616edcd1e339982c. Final-head protected backend/Android/container/dependency checks and relevant native packaging must pass before development integration. Record exact source, merge-candidate, integrated tree and executed results in the final handoff. No production deployment or master promotion is authorized.

Physical device, real-provider/payroll/expected-bill, actual alert receipt, same-release private two-owner/document/privacy/security, current nonempty real recovery/both keyrings/protected backup and qualified legal gates remain open. Synthetic CI and simulator/emulator checks cannot close them. MAUI retirement remains deferred pending parity/support and a deliberate decision; this fix preserves the currently supported native screen.
