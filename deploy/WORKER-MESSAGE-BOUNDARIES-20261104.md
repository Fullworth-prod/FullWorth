# Worker message boundary review — 2026-11-04

The service-worker update receiver accepted a synthetic `SKIP_WAITING` message regardless of event origin. Sixteen Node VM cases against the actual source reproduced nine failed assertions (seven passed) before the guard. These are handler-admission counterexamples, not proof that an external website can obtain or message a cross-origin service-worker reference.

The receiver now requires exact equality between `event.origin` and `self.location.origin` before considering its existing explicit command. Foreign, lookalike, scheme/port-mismatched, opaque, empty and absent origins cannot request early activation. Same-origin explicit updates and later valid attempts remain available. No update timeout, replay, navigation, caching, ownership, financial behavior or auto-update policy changes.

The transaction-filter worker is a dedicated Worker, constructed inside the transaction-stream module's private closure. Its messages travel on the implicit private MessagePort. The HTML MessagePort delivery algorithm does not populate `origin`; legitimate messages have empty origin and null source. A Window-style origin comparison here would reject the real owner. No such comparison or static-analysis suppression is added.

## Executable evidence

- `FullWorth.Tests/service-worker-message-boundary.test.mjs`: actual receiver under Node VM; sixteen positive/negative boundary and subsequent-valid-attempt cases. Isolated pre-fix execution: 7 passed / 9 failed. Isolated fixed execution: 16 passed / 0 failed. Only public source paths were remapped; no local checkout.
- `FullWorth.VisualTests/worker-message-boundary.test.mjs`: actual dedicated-worker, service-worker and PWA caller sources with observer-only fixture listeners on two disposable localhost origins. Six scenarios per engine target native empty-origin/null-source delivery; same/cross-origin Window message separation; 500/501 capacity and preservation; native WindowClient metadata/unknown-command refusal; exact waiting-worker activation; actual explicit caller single reload. Final hosted execution passed all twelve scenarios on both named engines; exact identity and artifact details follow. Results JSON is retained in the existing confirmation-browser artifact.
- Chromium/WebKit fixtures are not physical Android/iPhone, production PWA upgrade, arbitrary cross-origin exploit, provider, private financial-data or release acceptance. Node VM messages can construct otherwise browser-constrained inputs.

## Static review status

The full-candidate diagnostic reports run without alert/database upload, settings changes, grants or suppression. The production service-worker warning is absent in the verified final-source report; only the dedicated-worker origin warning remains in JavaScript. The dedicated-worker finding remains raw and requires contextual disposition; no blanket clean verdict is claimed. The separate C# consent-gate finding and remaining compiled/source extraction diagnostics remain open under the existing security/release work.

References: [HTML workers](https://html.spec.whatwg.org/multipage/workers.html), [MessagePort delivery](https://html.spec.whatwg.org/multipage/web-messaging.html), [ServiceWorker postMessage](https://w3c.github.io/ServiceWorker/), [CodeQL missing origin check](https://codeql.github.com/codeql-query-help/javascript/js-missing-origin-check/).

## Verified final source, checks and integration

PR [#1045](https://github.com/Fullworth-prod/FullWorth/pull/1045) normally squash-merged into development as `40aa3c2c0394f36f12cf8b55fab87620b902b06c`. Source `0a8a9a5911e7277677f1e03f7471e267860db40e`, tested merge `9f6111f6951a0ecc248bf80da55faa709d21bd6a` and integrated tree `f96f0e3a3b937f5d7ddfe5c1a7bb3af16cb6d971` match exactly. All ten final-head checks passed, with current strict four required Actions checks, zero required approvals, resolved threads/no reviews, no bypass and retained source branches.

[Main CI 41787013091](https://github.com/Fullworth-prod/FullWorth/actions/runs/41787013091) actually executed 3,700 .NET / 769 Node tests with zero failed/skipped and zero backend/offline build warnings/errors. Android build also had zero warnings/errors. Actual API/Web/parser images, native-auth startup, rendered visual checks, isolated virtual-passkey continuation, HTTP/secret/Redis/Web-BFF boundaries, parser memory/no-swap and disposable encrypted restore passed. Disposable restore snapshot `85d8666653b0698bd178925933084e5752ce4075e8ac774ab6d5edb9e66b7648` is synthetic CI evidence, not real protected clean-host recovery. Financial PostgreSQL suites were not newly triggered by this worker-only change; preserve #1041's separately recorded execution.

[Browser run 41787013188 / job 126431462592](https://github.com/Fullworth-prod/FullWorth/actions/runs/41787013188/job/126431462592) passed 464 scenarios: confirmation 38, BFF 74, auth setup 16, passkey removal 18, account security 94, privacy 188, Hosted Link 24 and new worker contexts 12. Browser artifact **13301433007**, ZIP SHA256 `173ce9353886e46c83171636b8441e2426898511d436207382ce8b9d46bbc473`, was downloaded and hash-verified; its twelve-record JSON confirms six successes per engine, real empty-origin/null-source dedicated messages and same-origin WindowClient service-worker messages. Visual artifact **13301829606** has API digest `3118f945ab465228d40e1f07dc57163650329e5c742733b89586c2912b8deee7`; that separate large archive was not downloaded or newly inspected pixel by pixel. No new visual-design claim is made.

[Candidate report run 41787013106](https://github.com/Fullworth-prod/FullWorth/actions/runs/41787013106) produced five full-candidate security-extended reports with pinned CodeQL 2.27.4/action revision, controls false, no incremental marker, no alert/database upload, exact source/tree/workflow/run/attempt and successful invocations. All ZIP API digests and raw SARIF manifest hashes were independently verified:

| Target | Artifact | ZIP SHA256 | Raw findings / diagnostic errors |
| --- | --- | --- | --- |
| python | 13302205111 | `72dcf80daa22c9b79c1ef707aa19ed1f63794f2b398a8760db378cd0513d447d` | 0 / 0 |
| javascript-typescript | 13301632496 | `9ced90aa848b80bd2dd3bddea1b857890ce10f70612ff13409946fb04dc466e1` | 1 / 0 |
| csharp-backend | 13301536966 | `155fb9b831872c779663cba14d2c06db1b15b9bc51270cf7d52c7bedb3867358` | 1 / 1 |
| actions | 13301274693 | `ce94993d747b0d162a6f1f5f8e34327bfaadb745b7cb82ad6068c093bedb94d3` | 0 / 0 |
| csharp-source | 13300749876 | `6aa1d0bba43e5f6581bf7530b230bbfdd567fb2d17f855feb14f47bc209d2986` | 1 / 116 |

Actions ran 23 rules, Python 50, JavaScript 103 and each C# target 63. All targets had zero diagnostic warnings. Remaining JavaScript finding is `transaction-filter-worker.js:55`, consistent with the private-port context actually observed in both engines. No Window-origin check was added there. Both C# reports retain `ExternalIdentityController.cs:157` registration-consent `cs/user-controlled-bypass`; server-side provider identity/password/ownership checks remain and no authentication bypass was established. The compiled extractor still reports “Failed to resolve name” in `ExternalAuthenticationEndpointMappings.cs`; both C# reports are explicitly partial extraction. No result was suppressed, dismissed or represented as complete security acceptance.

The initial source `472aaece9d1021004cd82a32d5d641a1bba95125` scan found an additional missing-origin filter in the test Window observation listener. Final source restricts it to the two explicit fixture origins. Initial run 41786745132 was cancelled after this real source correction; it is not reused as final combined success.

## Compiled C# companion provenance

[#1044](https://github.com/Fullworth-prod/FullWorth/pull/1044) normally merged as `77a21c5075a9f3666beda21ee5014772d152c095`, source `b3ab7232fbbc0eae5380e31e29ce713457a8b2a1`, tested merge `28fe9a3304d39e526f844883a1d58ace94d0b075`, shared tree `3206103edaae63078af32fc2092f784b98c477e7`. All nine final-head checks passed; CI 41736560815 executed 3,700 .NET / 753 Node, Android/container passed; reports 41736560786 and dependency 41736560763 passed. Five report ZIP/manifests/raw hashes and nine isolated actual-collector acceptance/refusal/diagnostic-retention cases were verified without a local checkout. The original full-source/native target remains visible alongside compiled backend/shared coverage; 1 versus 116 diagnostic errors is a scope distinction, not proof of full C# coverage.

## Release and real evidence boundary

Master stays `b424cd9c08d612d9eadef57839f0dca8c81f2841`; last independent read-only production verification (November 3) served `9540b2616841e9794d8f8c07616edcd1e339982c`. No master promotion, deployment or new host inspection occurred. The prior master artifact expires November 8 06:40:09 UTC; an intended release needs separately authorized promotion/deployment and fresh exact-master signed publisher/consumer evidence.

The 16-column real evidence template retains 19 NOT_RUN cases, planned candidate only and all actual fields blank. Physical-device rows explicitly include intended waiting-worker upgrade, one explicit reload and no surprise pre-consent reload. Existing provider/private facts, physical Android/iPhone, actual destination receipt, controlled two-owner/document/security, current nonempty protected clean-host database/original-statements/both-keyring recovery and qualified legal gates remain open. No expired grant renewal, private financial export, spending/activation, outreach, destructive recovery or branch deletion. Gated AI/creator remain off and terms proposed.
