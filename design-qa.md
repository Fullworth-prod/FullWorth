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
