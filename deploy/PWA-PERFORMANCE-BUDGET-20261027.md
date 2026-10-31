# PWA performance budget and measurement boundary — 2026-10-27

Milestone1 requires a lightweight budget without financial caching or invasive analytics. This records source-observed bounds at development11dc0b3d3babded2ac717018b76be30f5c20fcbd and a repeatable measurement procedure. Counts below describe authored source, not measured browser transfers, installed-device latency or a passing production performance audit.

| Surface | Verified source baseline | Budget / next measurement |
|---|---|---|
| Initial styles | Components/App.razor declares20 stylesheet links:18 named static sheets, app.css and generated FullWorth.Web.styles.css. The18 named sheets total226,143 uncompressed source bytes from Git tree metadata. | Do not increase the20-link baseline without a recorded need and cold/warm transfer measurement. Measure generated bundle and compression separately; do not call226,143 the total CSS download size. |
| Initial scripts | Six external script elements: theme bootstrap, performance profile, PWA, passkeys, password step and Blazor. ImportMap/HeadOutlet/ResourcePreloader and route imports can add other browser work. | Record actual requests/transferred bytes under cold and warm cache. Keep early appearance/profile application before style paint. No invented global six-request limit. |
| Route stylesheet loading | Profile code lists two balanced and four high idle-prefetch paths; efficiency schedules no route prefetch. Existing link checks avoid duplicates. | Record styles added when navigating Overview/Bills/Detail/Activity/Account/Transactions/Settings. Avoid duplicate hrefs and unbounded route accumulation; do not remove necessary styles merely to hit a number. |
| Initial transaction rows |100 efficiency,250 balanced,500 high; failed/unavailable JS resolution falls back to100; page clamps requests to50–500 and supports bounded expansion. | Maximum500 rows in the supported view; deeper-history/cursor work is a separate future contract. Never hide the current view limit or move financial rows into browser persistence. |
| Initial Activity rows | Requests100, rejects snapshots above100 and duplicate/malformed IDs, preserves last valid feed on failed refresh. | Maximum100 initial alerts. Record filter/re-render cost using synthetic rows; refresh/mutation failure must not fake an empty feed. |
| Repeated interop | NavigationViewportObserverTests proves one import/observation lease per component lifecycle and exact disposal, including late completion. Transactions resolves its initial profile once; Activity retains its BFF module. | Count imports/interop calls across repeated renders and route revisits for each primary page. Existing lifecycle tests are scoped evidence, not a claim of one global call across all routes. Node navigation geometry tests cover resize coalescing/no network. |
| Service-worker cache | Existing WebPwaBoundaryTests requires network-first navigation, one generic offline-page cache entry and no financial cache writes. | No authenticated document/API/BFF data cache and no financial local/session storage to improve timings. Preserve explicit update consent/truthful offline behavior. |
| Motion/presentation | Performance profiles reduce effects/list workload; the user-selected theme family and reduced-motion preference must preserve financial controls and content. | Test all three light/dark pairs, efficiency/auto, reduced motion and enlarged text before approving presentation changes. Browser geometry is not a startup latency benchmark. |

## Measured follow-up — #990/#992

The [laboratory report](PWA-PERFORMANCE-LAB-20261027.md) now records21 completed samples per source, preserving the source table above as an authored inventory. Observed DOM counts were21 stylesheet links/7 script elements (22 links on Transactions/Settings), distinct from20/6 authored declarations; every sample had0 duplicate stylesheet hrefs. Reported CSS/JS transfer/body sizes, cache hints, worker control and four profile API counters are recorded with their scope/unknowns.

The slow390px sample repeated bounded shift0.216613. New aliases place moving content in app-content/overview-loading at~2,005ms; its cause is unresolved. Geometry and probe completion do not pass a latency SLA, field Core Web Vitals or physical installed-device acceptance. Investigate source behavior without weakening financial cache boundaries or hiding latency. Full interop, populated/private history and actual intended-release/device timing remain separate.

## Repeatable acceptance procedure

On the intended release, use a disposable controlled account with synthetic data. Record browser/OS/device class, installed versus ordinary browser, release SHA, profile, theme, network conditions, cold versus warm cache and whether the server circuit was new or reconnected. Keep traces private if any real account is involved; do not export private financial data under this goal.

Measure navigation start to readable primary content, shell readiness, layout shift, external CSS/JS request count and transferred bytes, row count, module imports/interop calls, first interaction, route revisits, temporary disconnect/reconnect and service-worker update. Use local browser performance/network tooling; no third-party analytics or new credentials are needed. Repeat the same synthetic scenario before and after an optimization, including a slow-network run. Do not disguise latency with longer loading screens.

Physical mid-range Android installed launch (#251), iPhone standalone/keyboard/safe-area/update behavior (#258), actual deployment and live authenticated timing remain separate prerequisites. Current Chromium/WebKit geometry, simulator launch and lifecycle suites do not satisfy those gates. Do not set numeric latency targets from unmeasured guesses or claim a subjective device pass from CI.

## Inspectable sources

- FullWorth.Web/Components/App.razor
- FullWorth.Web/wwwroot/js/performance-profile.js
- FullWorth.Web/Components/Pages/App/AccountTransactions.razor
- FullWorth.Web/Components/Pages/App/Activity.razor
- FullWorth.Tests/Security/WebPwaBoundaryTests.cs
- FullWorth.Tests/Security/WebTransactionPerformanceBoundaryTests.cs
- FullWorth.Tests/Web/AccountTransactionPerformanceFallbackTests.cs
- FullWorth.Tests/Web/NavigationViewportObserverTests.cs
- FullWorth.VisualTests/navigation-viewport.test.mjs

## Verified startup correction and asset placement — 2026-10-28 UTC

#994 repeated0.216613 slow390px shift, with4px width/8px vertical movement. #995 persistent-head shell stylesheet recorded0 in its complete21-case report, artifact12786438837 SHA2563d9d59f2b16538026b14080051ada2951f19ec44f910008879841045606919ac. No dropped observer records/duplicate stylesheet links in that sample. This is laboratory stability, not a speed/SLA/physical-device pass; varied TTFB prevents a latency claim.

One stylesheet moved from interactive body to head without duplication/content change: App source now21 head styles (old20 plus the layout style); authenticated DOM stays21 before page extras. Public/auth routes now request this scoped static asset, a disclosed tradeoff. Source scripts stay6; runtime7 includes framework work. Slow timing report:23 CSS entries/422486 decoded bytes,18 JS/314426 decoded bytes. Sums are not wire captures or a public-page budget pass. Preserve financial caching boundaries, no write replay, reduced-motion and performance profiles. See the laboratory report for exact run/source/tree/limitations.
