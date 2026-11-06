## Verified final binding correction — November 4, 2026

[#1049](https://github.com/Fullworth-prod/FullWorth/pull/1049) integrated diagnostic capture as7e362e3510e441c7fa1d4f96a583e191e0475bc0. [#1050](https://github.com/Fullworth-prod/FullWorth/pull/1050) then integrated the demonstrated correction as `75df865b787394c7a6a83f24497376a1206b9602`. Source `aff578a6ddd479badc0f311e398998947da5a2c7`, tested merge `8b481791036075781a79be1af5377109c1331ca4` and integration share tree `c3ea4a237a7d7fc5e11bf1900851a463f697576f`. All three integrated files read back exactly and both source branches are retained.

The seven original compiled unresolved-name rows are gone after explicit delegate typing. Handler bodies, paths, argument order and authorization/rate metadata remain unchanged. Compiled rows/diagnostic notifications: **7/1 before; 0/0 after**. Source-only rows/notifications: **4,069/116 before; 4,062/115 after**, preserving the remaining native/source coverage limitation. The new compiled zero-error guard passed. Existing `cs/user-controlled-bypass` consent-gate finding at ExternalIdentityController.cs:157 and `js/missing-origin-check` dedicated-worker finding at transaction-filter-worker.js:55 remain visible; no suppression, dismissal or complete-security claim.

All nine final-head checks passed under current strict rules/no bypass and clear reviews/threads: [CI41932749130](https://github.com/Fullworth-prod/FullWorth/actions/runs/41932749130), dependency41932748907, [reports41932749037](https://github.com/Fullworth-prod/FullWorth/actions/runs/41932749037). Actual3,719.NET/769Node zero failed/skipped/backend+offline+Android warnings/errors. Full Chromium/WebKit EN/ES/all-three-theme/responsive/ARIA/privacy/subscription/native-auth startup, password-once/virtual-passkey/MFA/removal, session/parser/HTTP/no-swap and disposable encrypted restore passed. Restore receipt `e66edaa7f527e9b4d9cfcbfce8c1da74b3621cdb91f3c7d1bd9b251d297aab59` is CI evidence, not real protected recovery. No new APK/IPA packaging or physical-device/provider acceptance is claimed.

All five downloaded archive/raw SARIF hashes, source/tree/run manifests and both CSV hashes match:
- compiled C#13371259386 ZIP `81f4a5ae36d7079a1a9c8b8f8ddcdd9d991cae5919fec2daa45b26372c3ff0c6`; zero-row CSV `c21007310741e348ef112120e87801d2fce12b60c5f11986417feec5e1eccead`;
- source C#13372318195 ZIP `529da3c970bf2a01b7911d3d0a0cd075f5705bb5cc6740a47c881cb6944213a9`; CSV `79893cfb66e3ebc4ed8a4c454ddfd1d09196c0e1e01b9a6f2bee6b70bcc5ebff`;
- JavaScript13372930828 ZIP `55d8d6dccb2f35cfebdcb28f4c1e6f7fc38055d5d1396a6f31bcb20e68d3e7cb`;
- Actions13372342687 ZIP `64fc84b741867b9f9914c793d5f879f405dd91f72559942e5cc4f9fd899dc2f3`;
- Python13371459475 ZIP `722f768a867a4162728486156abd8c5d28ea4d270e351a6c8125c7779cf4a272`.

This closes the bounded repository analysis correction. Default alert queues, native/source-only completeness and real same-release security/provider/device/recovery/legal acceptance are separate; nineteen real cases remain NOT_RUN. Masterb424 and last independently November3 served9540 remain unchanged. No promotion/deployment, new grant/credential/scan setting, private export or branch deletion occurred. Earlier pending/experiment statements below are dated development history.

---

## Explicit Web route delegate experiment — November 4, 2026

Merged diagnostic capture #1049 is development7e362e3510e441c7fa1d4f96a583e191e0475bc0, with source2e7ea88f90cf3e162ad3179b4c9262382848c24d/tested95fca636c2a27f6c19834ca2b67b62d01782d995/integrated tree22b9b43c67f75aa5f847574f9041dbbd012e92af matching, four files read back exactly and branch retained. All nine checks passed: actual3719.NET/769Node0failed/skipped/backend+offline+Android0warnings/errors, container session/parser/HTTP/disposable restore. Raw five report ZIP/SARIF hashes and both CSV hashes matched their source manifests.

The compiled CSV contains **seven** extractor rows, all Failed to resolve name on the seven MapGet/MapPost registrations in ExternalAuthenticationEndpointMappings.cs (lines143/148/153/160/165/172/177 at this source). Its one SARIF error notification was a summarized diagnostic, not one underlying error. Compiled ZIP13371134666 SHA2561c95d8c9119f227f582eeb27475e84262114c1d2c4bfcaf173e15bfb5112f4c9; CSV SHA256e75f741ed659c096c631067a4ec6235bfd5cf9169bad4605e4194c552a82f5fd.

Source-only CSV retains4069 extractor rows, while its SARIF reports116 summarized diagnostic notifications. Source ZIP13372651290 SHA256f81be48879a6fdeb9cd62619445820c266b29eab48fbcf27aefbb590325d95cf; CSV SHA25608852cc5780c0355751c58dd65eefacda12fcad4e6e3150bd76906cdd746d8d0. Native/source-only coverage remains partial. These different counts are retained explicitly; no errors were suppressed.

The proposed source change explicitly types the existing seven delegates. Handler bodies, route strings, argument order and authorization/rate metadata are unchanged. Actual compiled extraction must demonstrate whether this resolves the binding errors; it is currently a hypothesis, not a verified fix. The compiled target will now fail after preserving its manifest/artifact if either raw extractor rows or diagnostic error notifications remain. The independent source-only target retains its disclosed errors and full scope. Query findings remain unfiltered; zero extraction errors would not establish complete native/device/provider/security acceptance.

No master promotion/deployment, new credentials/grants/settings, provider access or private data is used.

---

## Locate remaining extractor errors — November 4, 2026

The actual compiled report on c3736869 retains one extractor error: Failed to resolve name in ExternalAuthenticationEndpointMappings.cs. The standard CodeQL diagnostic query selects only a message and severity, discarding its available location. No source vulnerability or runtime failure is inferred from that tool diagnostic.

Both C# targets now run a separate table query over the same analyzed database to preserve every ExtractorError's file, line, column, origin and message as CSV. The existing raw SARIF, full-source controls, query suite, compiler trace, scope, diagnostic count and partial-extraction status remain unchanged. The manifest hashes the additional CSV. Database/BQRS uploads remain disabled; no permission, scan setting or authentication code changes. An actual report with the retained error's location is required before deciding on any source correction.

Uses the pinned bundle's library search path and the documented [query run](https://docs.github.com/en/code-security/reference/code-scanning/codeql/codeql-cli-manual/query-run) and [BQRS decode](https://docs.github.com/en/code-security/reference/code-scanning/codeql/codeql-cli-manual/bqrs-decode) commands. This is diagnostic evidence collection; execution and review remain pending.

---

## Compiled backend companion and visible diagnostics — November 3, 2026

The source-only C# report on sourceee634ae6be151f80d09fdadfa324b596b166c7b5 completed but retained116 extraction-error diagnostics. A successful report producer did not establish complete C# coverage. The original all-source C# target is retained, including platform/native files and all unfiltered diagnostics.

A separate csharp-backend target now requests manual CodeQL tracing and compiles FullWorth.Tests plus its API/Core/Web/ParserWorker references, test-linked native logic, and the offline AiEvaluation runner. Compiler sharing and incremental/build-server reuse are disabled so the trace observes compilation. This companion does not silently replace source coverage and does not claim complete MAUI platform/native coverage; the source target's limits remain visible. No-build native coverage and physical-device/security acceptance remain independent.

Reports now identify target, language, actual requested build mode and scope, diagnostic error/warning counts and a coverage-review state. Errors are prominent in the job summary and a workflow warning; raw notifications and findings are preserved. Even zero error diagnostics means scope-review-required, never complete-security acceptance. Producer job names explicitly say report, and the five target artifacts cannot overwrite one another. Alert/database uploads and repository settings/permissions remain unchanged.

Source-only #1043 evidence: all four ZIP/source/tree/raw hashes and absence of incremental markers were verified. Actions/Python had zero findings and warning-error diagnostics; JavaScript had two worker-origin findings. C# had one client-consent-condition finding and116 extraction errors, distinct from actual clean compiled application builds. See introducing PR#1043 and issue#291 for precise triage; nothing is suppressed or dismissed.

Current compiled-companion execution is pending. A prepared workflow is not coverage evidence. [GitHub's C# build guidance](https://docs.github.com/en/code-security/reference/code-scanning/codeql/build-options-for-compiled-languages) explains the tracing requirement for UseSharedCompilation=false. [dotnet build options](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build) define the rebuild and no-dependencies behavior.

---

# Candidate CodeQL reports

The existing GitHub default setup remains the source of GitHub's Code scanning alert queue. The authenticated development view reported that the branch had not been scanned, and the existing default CodeQL workflow offered no manual Run control. This workflow provides an independent exact-candidate static report without switching setup type or adding security-events permissions.

The workflow uses only contents-read permissions, exact-source checkout with credential persistence disabled, a commit-pinned official CodeQL action, and a linked tool bundle. It runs actions, C#, JavaScript/TypeScript and Python with the security-extended query suite. Source-only build mode is explicit; it does not replace the separately required compiled/backend/Android tests or prove complete symbol/extraction coverage.

Both SARIF-to-Code-scanning and CodeQL database uploads are disabled. Raw SARIF, unfiltered findings, diagnostics and an integrity/source manifest are retained as ordinary seven-day CI artifacts. No repository security setting, alert disposition, new credential/grant, production environment or private provider/financial data is used. There is no schedule or deployment trigger.

A successful report-production job does not mean zero vulnerabilities or release acceptance. The metadata step refuses absent reports/query metadata or unsuccessful/missing invocation records. Review every language's raw findings and extraction/configuration diagnostics; retain exact source/tree, action/tool version, run/attempt, artifact hash and limitations. Do not suppress findings, exclude tests/source or mark development's default alert queue scanned from these reports. Investigate actionable source findings with meaningful regressions and protected PRs before integration.

Authoritative action inputs: [pinned analyze definition](https://github.com/github/codeql-action/blob/1fc58d04a4d4374f921bdac573c3811c240f9c26/analyze/action.yml), [pinned init definition](https://github.com/github/codeql-action/blob/1fc58d04a4d4374f921bdac573c3811c240f9c26/init/action.yml). GitHub documents that [default setup rejects separate CodeQL SARIF uploads](https://docs.github.com/en/code-security/reference/code-scanning/sarif-files/troubleshoot-sarif-uploads/default-setup-enabled); this report does not attempt those uploads.

Initial source bae6c5e41e95bae09d290528b06b6ccf3fe97219 produced actual Actions and Python SARIF artifacts with successful invocation metadata and no error/warning diagnostics, but both declared incrementalMode=diff-informed. Their zero findings do not establish a full candidate scan. ZIP13278247984 SHA2562e5d8182a25b3bf6156a803876b7c6c5e1a19228f5ae8de6a527cae422821f80 and ZIP13277327375 SHA256d26c8806ffcc6d2780ba7f34f37366dc767cd19caa31d939be7524ca4faabe1c were downloaded and verified along with exact source/tree/run and raw SARIF hashes.

The corrected workflow explicitly opts out of diff-informed and overlay queries using the pinned action's feature controls and requires both controls disabled and refuses any SARIF run carrying an incrementalMode property. This broadens the analysis; it adds no suppression or alert/database upload. Source-only C# build mode remains a disclosed extraction limitation. Source3c1c8e9416adc3d2c797893075797df2e551a56a actually disabled diff-informed/overlay analysis (logs: no precomputed diff ranges), but its first validator incorrectly expected a literal none value. The pinned action's [query implementation](https://github.com/github/codeql-action/blob/1fc58d04a4d4374f921bdac573c3811c240f9c26/src/analyze.ts) only emits incrementalMode when an incremental mode is used. The corrected validator requires both full-source controls and rejects any incrementalMode property; absence is recorded as absent, never invented as a raw scanner field.

Corrected final-head execution and complete finding/extraction review remain pending. Earlier report-production failures are not source vulnerability findings or final scan success.


The exact corrected workflow collector passed seven isolated local cases: accepted the actual hash-verified full-mode Actions SARIF; rejected the actual earlier diff-informed SARIF, enabled incremental control, failed invocation, missing query metadata, wrong source identity and absent report. Git identity/environment were remapped in memory; no local repository or git execution. These are collector tests, not candidate scan acceptance.
