# Three-pair design QA — in progress

**final result: blocked**

The owner selected all three light/dark pairs for both public Home and signed-in Overview. The six 1487×1058 targets were opened. The owner-private reference gallery is the FullWorth UI Designs paired-directions document already shared with the owner. Exact source image identifiers: editorial light7e1d5274/dark312d0527; workspace light9eaf0e68/darkeccef736; story light86e06622/dark2e5f39f5.

Implementation inspected: CI40292167632, source e8bada80a45fa56a64a856b73f184494c91a8d58. Artifact12637026710 SHA256ee88f0a239d3d4d4e2f3fb88157d58d4cc74fb63d852a37912cad1006ffd6102 verified. Opened public-home-desktop.png (1440×1822 full-page), public-home-mobile-320.png and synthetic polish-overview-1440-dark-en-US.png. These are diagnostic views; the desktop CSS viewport was1440×1100, not the selected 1487×1058 target. They cannot establish a matched-viewport fidelity pass. The new suite captures exact1487×1058 frames for all six targets and must finish before a full comparison.

## Findings and pending verification

- P2: The actual phone mode-toggle hit target measured 39×39px in CI40294050862 on b479cc0add8fc456e5b58ef69f06465e6f3bc061. The unchanged 44px browser requirement correctly failed. Fix prepared: 44px width/height/minimums and a strong theme-aware keyboard outline. Post-fix execution is pending.
- P2 diagnostic: old shared presentation rules still override selected compositions and some dark-only ink. Correction prepared: move selected Overview/palette ownership into the existing app-polish layer, preserve shared navigation resilience, remove old decorative hero art and set mode-aware foregrounds. Re-capture and verify rather than assuming the source change fixes appearance.
- Earlier diagnostic Home views had a narrow header and muted hero ink. b479cc0 corrects header width and explicit headline color. Latest matched-size evidence is still required; no pass is claimed.

## Required fidelity surfaces

Fonts/typography: source references opened; exact-size output comparison pending.
Spacing/layout rhythm: source references opened; legacy cascade corrections need post-fix matched capture.
Colors/tokens: six explicit palettes and existing high-contrast/mode behavior implemented; normal and high-contrast browser measurements now cover core ink. Final results pending.
Image quality: supplied mark/sprite icons reused; fictional EN/ES transparent statement images inspected and optimized to 960×580 WebP (58102/59358bytes), never represented as customer evidence. Inspect the images in final rendered layouts for scale/sharpness/halos.
Copy/content: fictional examples retain USD, twelve-month annualization qualification and no-live-check notice; real Overview calculations/coverage qualifiers remain. Browser content-invariance and EN/ES checks pending on final source.

## Verification gate

Both Chromium/WebKit: all three styles × both modes × EN/ES × Home/synthetic Overview;320/390/1440px and200% text, reachable44px controls, keyboard style selection, sample interactions, storage denial/reload/navigation, loading/disconnected/repair/first-sync/empty/sparse/mixed/saved/error states, failed-refresh retention, motion/performance profiles, console errors and no financial writes.

No full-view or focused-region matched comparison has passed yet. Produce combined source/implementation boards at the same viewport after final capture; inspect all five surfaces, fix P0/P1/P2, capture again and record comparison history. Compilation and pre-existing regression success do not replace design QA. Do not merge or present this draft as complete. No deployment.

## Subsequent execution — tablet enlarged-text failure

Exact source ddfc12eb1618a8db55d4625ca762aada292ac4fa, CI40296968650:2870 .NET +127 Node passed with zero failures/skips; Android passed. The unchanged browser geometry gate then failed Home/editorial/light/en-US at768px/200%: document/body width803px. Verified artifact12639435422 SHA2565e36bd257879c23223df7ac0232f2298b5f7997a7d5efcf5cf76fa1c2c10e5ad; opened theme-pair-failure-chromium.png. The image showed a wrapped header overlapping the title and crowded table headings. The mode control had advanced beyond its previous39px failure.

P2 fixes prepared: automatic header height, earlier stacked comparison layout, font-relative process/security/footer grids, clearer tablet composition and full-page diagnostics. Orange/green public comparison labels now follow the selected workspace target, with explicit status text retained. Post-fix matched screenshots and both-engine results are still pending. No viewport clipping was added. **final result: blocked** remains.

## Authentication-state and audit coverage correction

Selected Home targets depict anonymous visitors. The new capture pass therefore uses a separate signed-out browser context for exact1487×1058 frames, asserts sign-in/account-entry routes and writes region/density metadata. Signed-in Home/Overview navigation remains independently covered. This is necessary before claiming a like-for-like visual comparison.

Independent geometry/contrast issues are now collected across cases and both engines; any issue still rejects acceptance. Reference and bounded issue images are preserved for a comprehensive repair pass. No broader acceptance result is claimed until the actual run completes. Current comparator utility rejects viewport/density/authentication-state mismatches.

## Fixture-boundary correction — both engines

Source f1c63aae, CI40301745942: both engines reached a genuine empty-account state instead of the synthetic populated fixture. The real client reads /bff/bill-streams?includeInactive=false; an exact-endpoint glob omitted the query and did not intercept it. This was a test fixture error, not proof of a product data failure. Artifact12641720772 SHA2567640165867c6f495345e61e4845a05277373d761ceac030f636b80f453f7142f verified; opened the Chromium failure capture showing an accurately qualified empty state. Corrected bounded query-aware patterns and safe read-count/partial-progress diagnostics. Full paired captures and visual acceptance remain pending.


## First matched six-pair comparison — d4981c9

CI40303579702 completed all312 geometry and108 state cases per engine, but failed acceptance: Chromium recorded16 and WebKit48 layout/contrast findings plus console resource errors. These are not passing results. The bill/bank fixtures each received110 reads per engine, confirming the earlier query mismatch is fixed. Both engines reached all functional sample, loading, evidence, failed-refresh, storage-denial and motion checks before final audit rejection.

Artifact12642605466 SHA2560ac3337fcb8860b331a77d1622fb36f233e4a31a9f49feeeeed28d5352c252c3 verified and expanded to owner-local fullworth-theme-d4981c9. Strict comparison utility produced24 boards at1487×1058, density1, anonymousHome. Opened all six combined full views plus editorial/light example, workspace/light example, story/light hero and story/dark example focused boards. These are now matched visual evidence, not a fidelity pass.

P2 findings:
- Story phone200% comparison amount spills horizontally. The numeric text was an anonymous flex item. Wrap the full amount in a min-width-zero, breakable text span; preserve the number and text enlargement.
- Workspace phone200% still carries a desktop two-column summary into narrow space. Release summary tracks at1000px and rail tracks at600px; preserve all financial qualifiers. Recheck Spanish and WebKit.
- Workspace heading wraps across two lines where the selected target has one; Story display hierarchy is materially smaller. Adjust family-specific desktop type sizes/grid proportions and inspect recapture.
- Workspace detail labels and editorial table headers are smaller than the selected target. Increase those UI text sizes; retain honest no-history/annualized-illustration wording.
- Appearance controls crowd the enlarged topbar. Give the mobile brand its own row and allow the picker full remaining width; check no overlap/overflow.

Console diagnosis: existing AdminNavLink deliberately performs a real non-admin GET /bff/admin/users, which receives403 and setsAccessDenied. This existing access probe is not an uncaught application failure. Narrow console classification to exactendpoint+403 only, retain the live response and all other errors. Record actual URL on remaining console errors. Write full geometry/error auditJSON before final assertions so independent failures remain reviewable; no assertion is removed.

Build warning: MSBuild treats resource keys case-insensitively. Annualized illustration/annualized illustration collide despite exact-case XML uniqueness. Use the existing capitalized key everywhere and remove its duplicate. Backend2870 and Node127 passed, zero failed/skipped, but one resource warning existed; do not claim zero warnings on this source. Android wrapper succeeded with its build steps skipped for this test-only revision; earlier UI source did execute Android.

Intentional deviations: source mockup copy implying recurring discovery or explanation is replaced by qualified illustrative/supported-history language; USD cents and annualization qualification remain explicit. The newly required style/mode controls are additional product controls. Supplied brand/sprite assets replace invented art. Workspace document evidence uses the generated paper asset already inspected; no fake customer statement or merchant verification. Flat accessible palettes preserve six family/mode identities. A new capture is required after fixes before classifying remaining visual differences.


## Post-fix browser result —875ac39e

CI40305803720: Chromium completed1,544 assertions,312 geometry and108 state cases with zero layout/contrast findings. WebKit completed the same312/108 with zero layout/contrast findings, then rejected22 CSP console messages; no combined pass is claimed. This resolves the earlier16/48 geometry findings in actual execution. Required final-head/full older-suite acceptance remains pending.

Investigation read the pinned Playwright1.55.1 source at microsoft/playwright/packages/playwright-core/src/server/screenshotter.ts. Its WebKit animation sync inserts a style element containing exactly body {} before screenshot capture. FullWorth deliberately permits style-src self only; that external test tool injection is correctly blocked. The production policy is not changed. Next suite observes each capture's inserted styles, counts only the exact known CSP message on the exact page during capture, asserts no other styles were inserted and that every classified denial is explained by observed body {} injections. All other CSP/browser errors still fail. Separate computed-font-size assertion now proves200% root text was actually applied in each enlarged geometry case.

Artifact12643086981 metadata records SHA2560a967616114fa5958de3c668cebd7ab5c240a30e100901393999dec0fe0cd68d, but download connector returned internal errors twice, so this artifact has not been downloaded or visually accepted. Preserve honest blockedQA until a later capture can be retrieved and inspected.

Further P2 fixes from the earlier matched captures: restore Workspace public rail icons/alignment using supplied sprites, brighten its account/menu ink on cobalt, and use Story's chosen accent for the selected navigation state. No new art approximation or financial change. Re-capture required.


## Full browser acceptance and second matched comparison —a3172bd

CI40360544623 Linux production container passed, including the complete existing visual suite, security boundaries and disposable encrypted recovery fixtures. Actual theme results: Chromium1,760 assertions and WebKit1,778;312 geometry+108 state cases each; zero layout/contrast findings or unexpected browser errors.18 WebKit screenshot denials were each matched1:1 to the observed Playwright body {} insertion; production CSP unchanged. Root font>=31.9px proves200% cases applied. Existing responsive672 cases per engine, native selects126 per engine, ARIA37 per engine and subscription54 per engine passed. Backend2,870/Node127 zero failed/skipped; backend/offline builds zero warnings/errors. Separate confirmation regression run remains independent and pending.

Artifact12670085499 SHA256dc7251f3d282491eae97e4b67e0883931415a0ea94b82bddfa668c1f02c01de3 successfully downloaded/checksum-verified at fullworth-theme-a3172bd. Earlier artifact download failure was not permanent. Strict comparator produced all24 boards at exact1487×1058,density1,anonymousHome. Opened Editorial light, Workspace light and both Story combined full boards; source/implementation are visibly aligned/auth-normalized.

Remaining P2:
- Story's third display phrase wraps to a fourth line, shifting the comparison and evidence conclusion below the intended fold. The intended Inter face had no actual supplied font file and fell back to a wider system face. Supply a licensed, same-origin Inter display asset rather than distorting/scaling text, and re-capture. Source type analysis compared actual official Inter/Roboto glyph lengths; Inter optical32 matches the chosen geometric display direction. Self-host/subset only; no font service or external requests.
- Story repeats the general process panel after its own payment/comparison/evidence sequence. Hide that duplicate only forStory and bring the existing evidence conclusion directly after the sequence. Put the shared How-it-works anchor on the visible comparison wrapper so navigation remains functional.
- Editorial comparison heading remains smaller than reference; increase desktop2rem→2.3rem.
- Workspace public rail's intrinsic grid track still prevents full-row alignment; use an explicit flexible track/justify stretch.
- Authenticated evidence-entry link incorrectly targets registration. Render the existing bills-workspace link for authenticated visitors; retain registration for anonymous visitors. Add actual browser entry-state assertions.

Next captures must verify all corrections. The earlier phone/tablet/summary/amount/contrast blockers are resolved by actual both-engine execution. Current result stays blocked until these remaining P2 fixes receive a matched post-fix visual review.


## Third matched comparison —10f7daf

CI40364203186 full main CI passed. Actual dedicated matrix: Chromium1,786/WebKit1,804 assertions,312 geometry+108 state cases each, zero layout/contrast findings or unexpected browser errors. Backend2,870/Node127 passed, zero failed/skipped/warnings; existing responsive672/native-select126/ARIA37/subscription54 per engine passed. Artifact12671400099 SHA256839b79a07d57d907af236635e7a9ae31a7daf038a5fbec4da21a2678aaf91976 verified, local fullworth-theme-10f7daf/fullworth-qa-10f7daf. Opened combined Story light/dark, Workspace light, Editorial light full views and Story light/Workspace light/Editorial dark hero plus Workspace dark evidence regions.

Resolved: Story is back to three lines, the repeated process is removed, evidence conclusion follows it, local face loading/anonymous/authenticated entry assertions pass; rail track alignment is full-width and Editorial heading scale improved. FontTools/PIL comparison confirmed original and WOFF2-subset advances identical, including the20px space at104px/850/optical32.

Remaining P2: the old negative tracking is too tight with the now-real Inter face; word separation looks crowded compared with the selected target. Open Story tracking -.065em→-.035em and Workspace -.055em→-.025em, using slightly smaller Workspace display size to retain its single line. Story's extra transparent image-box/paragraph spacing pushes the conclusion to the lower edge; remove figure margins, reduce list-bottom28→16 and qualification top margins14→8 while preserving text/image dimensions. Re-capture required. Current QA remains blocked.

Independent CI failure: dedicated confirmation run40364205273 was cancelled at its15-minute job limit, before tests, while the Azure Ubuntu mirror slowly downloaded OS packages. The earlier40360544086 showed the same installation behavior. Main CI was independently green; the standalone dialog/BFF/recovery suites are not claimed on this head. Next workflow uses Ubuntu's existing signed archive via HTTPS on the disposable runner only; signing configuration, action pins, read-only permissions,15-minute limit and all three test commands remain unchanged. Official archive Noble InRelease returnedHTTP200; source https://archive.ubuntu.com/ubuntu/dists/noble/InRelease and https://ubuntu.com/server/docs/how-to/software/package-management/ .

Prepared phone previews: add six anonymous normal-text English phone cases per engine and six viewport captures for private owner review. This adds actual coverage (intended318 geometry per engine); it is not yet an executed passing count. Runtime pages remain the same except the spacing corrections above.


## Phone pass and extended palette review —5495d71

Main CI40368663432 failed the final browser aggregate; do not claim combined success. WebKit completed1,840 assertions,318 geometry+108 state cases with zero issues. Chromium completed the same318/108 but recorded one zero-sized control only during the held loading state (Story/dark/en-US/320px/200%). The code measured visibility and bounds in separate asynchronous reads while hydration could replace controls. Next suite waits for layout appearance modules independently of the held financial request and measures all visible control rectangles in one DOM snapshot.44px/in-viewport/document-overflow gates remain. This is a harness-readiness correction, not evidence of a persisted financial-state failure.

Artifact12671592646 SHA256e8ae8d6d30318a07352ba7058a5d81205df4ae464bead8716eef787c9522cca6 verified, local fullworth-theme-5495d71 and fullworth-qa-5495d71. Opened Story light full, Story dark hero, Workspace light full/header and actual390px English Workspace phone. Three-line display, improved tracking, visible evidence conclusion, full-width rail and compact Story spacing are now visible post-fix. Remaining differences in native phase markers, glyph shape and qualified illustrative copy are minor/intentional; do not keep changing those as P2.

P2 confirmed visually: Workspace/light mode glyph is nearly white on a white button. The rail sets white ink but inherits a light bw-surface-soft. Set rail soft/hover surface tokens to its existing dark-blue surface. Add control contrast measurement across all public/app pairs.

Potential palette leak found in direct source review: Workspace's white sidebar ink/hard-coded nav-link rule also reaches the nested light mobile sheet. Reset that sheet to page surface/ink and give its links the page palette. Verify actual open menus across all3styles×2modes×EN/ES at320px/200%, every destination's contrast/scroll reachability, sheet bounds above measured navigation and Escape/focus restoration. Capture six menu examples per engine. New intended total330 geometry/108 financial states/12 menu cases per engine; not a passed claim.

Runner repair correction: live GitHub UI logs confirmed the prior step changed0 files. GitHub's Ubuntu runner uses mirror+file indirection through /etc/apt/apt-mirrors.txt, which still selected the slow Azure URI. Include that exact observed file in the narrowly scoped replacement. Current40368663313 again expired before tests. The signing settings, timeout, all test commands and permissions remain unchanged; validate the real next job.


## Shared main-runner installation path

13dc4f9 dedicated confirmation40371911639 passed after the mirror-list correction. Log evidence: exactly1 runner file updated; apt used https://archive.ubuntu.com/ubuntu forNoble and fetched119MB in7seconds. Actual dialog38/BFF74/Hosted-Link24 browser cases passed. Backend40371911364 passed2,870/127 zero failed/skipped/warnings; Android passed with compilation executed.

The main container job121744569749 was still installing browsers about15minutes into the run, so the new menu/contrast matrix had not executed and is not claimed. Its separate install step had not received the confirmed URI repair. Apply the identical bounded mirror-list preparation immediately before main visual dependency installation, only when visual checks are required. Existing30-minute main timeout, test commands, action pins and permissions stay unchanged. No unchanged rerun or passing UI claim; a new source/checkpoint is required.


## Expanded audit execution —feefd5d

CI40374562638 failed the new audit aggregate; backend/Android/dependency and dedicated confirmation passed separately. Both engines completed330 geometry (including108 financial states and12 menus).24 reported navigation contrast failures were diagnostic defects: the helper interpreted normalized color(srgb...) channels as0–255 and measured link-container ink rather than its visible label. Opened actual Editorial/Workspace/Story light menu captures from verified artifact12673589449 SHA25672fc36accc1d55607826b2fb87c50ae5183b46a9a14bfbc8f51035d8bb916d5c. Labels visibly use dark, legible text. Do not call those24 readings verified product contrast defects. New parser supports computed sRGB/RGB/RGBA/percent alpha, measures actual text spans and records raw foreground/background chains in auditJSON. Four isolated source normalization cases actually passed; browser acceptance is still required.

WebKit also hit real429 rate limits on repeated account-preference/admin-probe reads during the high-volume navigation matrix. Production rate limiting is not changed. The new fixture first verifies one actual200 JSON preference response and an actual403 admin denial for this same synthetic account, then reuses those GET-only ancillary responses within the matrix. It asserts denied users never see admin navigation, rejects any fixture write, and restores routes afterward. Account/security/preference behavior is independently exercised against the real disposable BFF by the existing suites; no application caching or rate-limit bypass is added.

The main installer now completed after its mirrored preparation and reached browser execution. Both30/15-minute limits remain. New UI controls/menu positioning/scroll/focus cases completed; final trustworthy contrast/console acceptance still awaits this corrected diagnostic pass. No runtime style change is proposed from the false contrast numbers. Keep final QA blocked until the new actual run and matched images are reviewed.
