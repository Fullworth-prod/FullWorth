# Three selectable FullWorth theme pairs

Status: implementation in progress; no visual acceptance, protected CI, development integration or deployment yet. Human selected all three pairs on 2026-10-26. Reference gallery: https://docs.google.com/document/d/1saHL9hvcqRpG1NQvAQ0Xe5hywWPfM7BuMS5Gmo8iWUA/edit?tab=t.7oxhmcd6lnlu

SELECTED: ALL THREE PAIRS — 26 OCTOBER 2026

The owner chose all three light/dark pairs as selectable themes, across both the public homepage and signed-in Overview. This replaces the earlier request to pick a single pair. The six existing references below remain the targets.

Implementation chapter

One appearance system, six combinations
Editorial: centered serif headlines, open ruled comparisons and calm spacing.
Workspace: a cobalt public navigation rail on desktop, compact sans-serif hierarchy, and interactive list/detail comparison.
Story: warm ivory or charcoal, large humanist typography, copper accents and a payment → comparison → evidence narrative.
Each style has matching light and dark modes. Keep the established FullWorth mark and supplied sprite icons. The appearance choice is separate from the existing light/dark preference, stored on this browser with safe defaults if storage is blocked. It must not create financial writes or account grants.

Public homepage
Replace the current floating balances, decorative glows and broad net-position pitch with an evidence-first bill-change explanation. The same clearly labeled illustrative Internet comparison supports all three compositions: prior monthly USD79.99, latest USD104.99, difference USD25. A USD300 annualized illustration is conditional on that monthly difference continuing for12 months; it is not a customer forecast. Make comparison selection, evidence disclosure, navigation and account actions work. Use a dedicated generated sample statement asset where the reference shows paper; never depict a real customer record or fabricate a live merchant check.

Signed-in Overview
Carry each style into the attention area, recognized-bill summary, watchlist and useful next actions. Remove duplicated large change presentations while preserving source-backed amounts, original currencies, comparison coverage and timing qualifications. Keep loading, disconnected, repair, first-sync, empty, sparse, mixed-coverage, stale-refresh and populated states. Do not label recognized charges as total spending, a monthly budget, savings or net worth.

Verification and release
Use the existing repository and connected GitHub workflow, without a local checkout. Check all three styles × both modes on Home and synthetic Overview, EN/ES,320/390/1440px, enlarged text, keyboard focus, blocked storage, reload/navigation persistence, reduced motion and performance profiles. Preserve financial-truth tests and measure contrast. Compare actual rendered screenshots with the six selected targets and record design QA; a passing build alone is not visual acceptance. Merge into development only after required final-head checks and review. Production promotion/deployment remains separate.

Dependency completed
Subscription verification PR971 is integrated in development7c01abf61034b10d0288ee8efe1eb61361b9d913. All18 checks passed;2870 .NET +106 Node tests and54 subscription assertions per Chromium/WebKit. Merchant verification remains unavailable without supported authorized access; manual/document evidence is labeled. No merchant/UI production deployment occurred.

Existing reference gallery follows.


## Asset catalog and generation

The supplied FullWorth SVG mark and existing billwatch-sprites icon library are reused. No new icon vendor or font service. The statement is a deliberately fictional public example, never used as evidence on an authenticated customer record. English/Spanish transparent images were generated with the built-in image tool, inspected, then encoded as960x580 WebP (58102/59358bytes). Originals are retained in the project design-assets artifact folder; code consumes the optimized public assets.

Paths: `FullWorth.Web/wwwroot/assets/fullworth/sample-statement-en.webp`, `sample-statement-es.webp`.

English asset prompt:

Use case: product-mockup. Asset type: one isolated illustrative statement paper for FullWorth's selected Story light/dark website theme. Reference image role: the attached full-page design is a style reference ONLY; recreate only its small paper statement at lower right, not the webpage. Generate a front-facing warm ivory rectangular statement with a subtly torn bottom edge, very light paper grain and restrained soft shadow. Wide paper proportions about 1.65:1, flat, straight, no rotation or perspective. Paper fills most of canvas with transparent margins and a genuinely transparent background. Exact printed English text: small top-left 'Service Co.'; small top-right 'Page 1 of 2'; bold dark heading 'Your account statement'; a pale peach highlighted line 'Example: promotional rate ended'; below it three short light-gray printed text lines. Small footer 'Illustrative example'. Preserve the sparse, refined receipt look in the reference. No amounts, account numbers, barcodes, real names, logos, webpage controls, surrounding UI, overlays or watermark. Text belongs on the paper itself. The asset must remain readable against both ivory and charcoal backgrounds.

Spanish localization prompt:

Edit target: the attached standalone illustrative statement paper. Change ONLY the printed text to Spanish. Keep exactly the paper, torn edges, peach highlight, gray printed lines, composition, dimensions, colors, texture, shadow and transparent background. Top-left stays 'Service Co.'. Top-right becomes 'Página 1 de 2'. Main heading becomes 'Tu estado de cuenta'. Peach highlighted line becomes 'Ejemplo: terminó la tarifa promocional'. Footer becomes 'Ejemplo ilustrativo'. Do not add amounts, account data, logos or surrounding UI. This is a sample graphic for a Spanish-localized financial product homepage.

## Verified so far

21 isolated Node/VM appearance tests actually passed against prepared source, with source-read statements remapped in memory, without a checkout. This is not browser, .NET, protected final-head or design acceptance. Complete the page implementations, actual browser interactions, screenshot comparisons and required checks before integration.

## Current execution checkpoint

- Source e8bada80a45fa56a64a856b73f184494c91a8d58: CI40292167632 passed2870 .NET +127 Node tests, zero failures/skips; backend/offline/Android builds and existing container/browser checks passed. These results precede the dedicated all-pairs matrix.
- Source b479cc0add8fc456e5b58ef69f06465e6f3bc061: new browser matrix correctly failed the first phone touch-target gate because the existing mode toggle was39×39px. Do not claim all-pairs acceptance from the earlier baseline pass.
- Current corrections enlarge the toggle to44px, strengthen/localize its keyboard labels/focus, consolidate selected Overview and palette rules in the existing app-polish owner layer, remove inherited hero art/dark-only ink, preserve navigation resilience and add tablet coverage. The same touch/financial gates remain.
- The dedicated matrix now covers300 geometry and108 state cases per engine, core contrast, keyboard/native demo behavior, storage denial, navigation/reload persistence, stale-read retention, motion/performance and console checks. These are intended case counts, not claimed executed passes.
- Exact-reference captures use1487×1058 CSS pixels at density1. Source references are1487×1058. The new suite runs before the older visual suites to surface UI failures early; the older suites still run afterward and are not skipped.
- Project-root design-qa.md is blocked until post-fix matched source/implementation boards and both-engine results establish no P0/P1/P2 findings. No deployment.

- Source ddfc12eb, CI40296968650:2870 .NET +127 Node zero failures/skips and Android passed. The new early matrix exposed768px/200% Home overflow (803px document width) and header/content overlap. Artifact12639435422 digest5e36bd257879c23223df7ac0232f2298b5f7997a7d5efcf5cf76fa1c2c10e5ad was verified and its failure capture inspected.
- Next correction keeps the no-clipping/touch gates: automatic header height, earlier stacked comparison, font-relative process/security/footer tracks and wider diagnostics. No combined all-pairs or final design acceptance is established yet.

- The audit now continues independent layout/contrast measurements, saves bounded issue captures and attempts both browser engines before rejecting the run if any check failed. No failed check is waived. Anonymous Home reference captures are isolated from signed-in navigation, at exact1487×1058/density1 with region metadata; this matches the selected target's authentication state. The matrix includes312 geometry cases per engine plus108 state cases; those remain intended counts until actual logs establish completion.
- Mode controls remain disabled until their existing module initializes, avoiding lost first clicks. Public demo amounts stay on one line while labels reflow; long localized headings wrap without shrinking enlarged text.

- Source f1c63aae CI40301745942: both engines showed the honest empty-account state because the fixture omitted the client's includeInactive query on the bill-list read. The fixture boundary is now query-aware and records safe read counts. No product-data change or all-pairs success is inferred. Actual final-source execution is still required.


## Verified second browser pass and final fidelity corrections

Source a3172bd987b2d499b57b6e769b09b91fd3dea18d, CI40360544623: backend2,870 and Node127 zero failed/skipped, backend/offline builds zero warnings/errors. Production-container job and complete existing visual suite passed. Theme assertions1,760 Chromium/1,778 WebKit,312 geometry/108 state cases each; zero unexpected browser errors or layout/contrast issues. Existing responsive672/native-select126/ARIA37/subscription54 cases per engine also passed. Disposable recovery fixtures remain synthetic. Android wrapper success skipped compilation for this web-only revision; earlier UI source ran Android. Separate confirmation workflow must pass at final head.

Verified visual artifact12670085499 SHA256dc7251f3d282491eae97e4b67e0883931415a0ea94b82bddfa668c1f02c01de3. Strict1487×1058,density1,anonymous comparisons still found Story fallback-font wrapping/repeated process flow, Editorial heading scale and Workspace rail track alignment. design-qa remains blocked pending post-fix capture.

### Self-hosted display font

The selected Workspace/Story heading styles now use a real local Inter face rather than depending on an unprovided Inter installation. Source: https://github.com/google/fonts/tree/main/ofl/inter ; upstream Inter Project Authors/https://github.com/rsms/inter, SIL Open Font License1.1. Original Inter[opsz,wght].ttf Git blob047c92f6e2212473dc436020afed689527076d44 verified after download. Full license/copyright included at assets/fullworth/fonts/OFL-Inter.txt.

The bundled asset is a modified Latin/extended-Latin, combining marks, punctuation and currency subset, converted toWOFF2 with fontTools4.67.0 and brotli1.2.1. All100–900 weights and optical-size axis retained.189612bytes; SHA2565d63ac7c7b8b2e1438f8e511b95bed3681aad03b6df82ce2bf3c7e27e4f5825f. Font-family alias FullWorth Display; swap loading. Applied only to the selected public display headings, preserving the rest of the application's existing typography. Served from the same origin, style/font CSP unchanged; no Google Fonts request or new service/account. Roboto Condensed was evaluated locally but is not bundled.

Story removes the repeated process panel, retaining visible payment→comparison→evidence and evidence conclusion. SharedHow-it-works anchor moves to the comparison wrapper. Authenticated evidence entry opens /app/bills; anonymous entry retains /register. Browser assertions cover these entry states, actual local font loading, and the three English display lines at the selected viewport. Final verification is pending, not implied by this preparation.


### Follow-up matched review and isolated browser-runner repair

10f7daf6170c22e103a9d3e6708d632c77b5c7c2 main CI40364203186 passed all main jobs and the complete browser/security/disposable-recovery sequence: theme1,786 Chromium/1,804 WebKit assertions,312 geometry/108 states each; no layout/contrast or unexpected console findings. Backend2,870/Node127 zero failed/skipped/warnings. Verified artifact12671400099 SHA256839b79a07d57d907af236635e7a9ae31a7daf038a5fbec4da21a2678aaf91976. A matched visual review still found crowded display tracking and excess Story spacing. Those are corrected in the next source; QA remains blocked until recapture.

Dedicated confirmation40364205273 never reached tests: its15-minute limit expired while the Azure Ubuntu archive slowly downloaded system dependencies (same pattern in40360544086). The workflow now replaces only that official Ubuntu mirror URI with [Ubuntu's signed archive overHTTPS](https://archive.ubuntu.com/ubuntu/dists/noble/InRelease) in disposable-runner apt source files. Ubuntu [package-management guidance](https://ubuntu.com/server/docs/how-to/software/package-management/) remains authoritative. Existing signing settings, read-only token permissions, immutable action pins,15-minute timeout and dialog/BFF/Hosted-Link test commands are unchanged. Actual CI must demonstrate recovery; static YAML validation is not execution acceptance. No production/customer/host/account configuration is changed.

Add six anonymous English normal-text phone checks per engine (intended318 geometry total) and six Chromium viewport images, supplementing six exact desktop reference frames. Phone images are synthetic public examples only, suitable for the already authorized private Drive review. Do not call the new count passed before final execution.


### Palette and fixture-readiness follow-up

5495d71/CI40368663432 was not accepted: WebKit passed1,840 assertions/318 geometry/108 states, Chromium hit one transient0×0 loading-state control while all318/108 cases completed. New measurement is atomic and waits only for independent appearance readiness; the bank/bill request remains held for the loading scenario. No44px or overflow criterion is relaxed.

Verified artifact12671592646 SHA256e8ae8d6d30318a07352ba7058a5d81205df4ae464bead8716eef787c9522cca6 contains actual English phone previews and matched6-pair desktop frames. Post-fix typography/Story composition is visible. A faint Workspace/light mode glyph needs its rail soft-surface token corrected. Mobile Workspace sheets must reset inherited sidebar ink/surfaces to the page palette; new both-engine, both-language menu checks measure contrast, scrolling, position and Escape focus. Intended330 geometry/108 state/12 menu cases per engine must actually pass.

GitHub live logs exposed /etc/apt/apt-mirrors.txt as the active mirror-list indirection; the first URI repair changed0 files and confirmation40368663313 still exhausted15minutes before tests. Next source includes that exact file, keeping signature verification,15-minute limit and all test commands/permissions unchanged. No production or user-machine package configuration is modified.
