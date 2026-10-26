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
