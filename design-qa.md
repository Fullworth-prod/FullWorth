# FullWorth three-pair design QA

**final result: passed**

Accepted rendered source: `c66e534d60304303730740333c3bed3b090a4f48`, tree `e430e5c6855b94b8a587adcb95b84378c709e36e`. [CI 40377964637](https://github.com/Fullworth-prod/FullWorth/actions/runs/40377964637) passed backend, Android and Linux container jobs. Separate confirmation run40377964643 passed. This report records actual source/implementation comparison, not production deployment. Subsequent documentation-only commits require their own final-head checks and runtime-blob equality before merge.

## Targets, captures and normalization

The owner selected all three light/dark pairs across public Home and signed-in Overview. The six selected source images are1487×1058 pixels. Source root: `C:/Users/brist/.codex/generated_images/01a16c7b-6daf-7762-8b9a-6abcc5d801fb/`.

| Pair | Light source | Dark source |
|---|---|---|
| Editorial | exec-7e1d5274-9d49-4299-90d3-b0c17427334e.png | exec-312d0527-fbe4-4b76-9f32-27db4b68de39.png |
| Workspace | exec-9eaf0e68-21a3-45ea-bbb3-761b282ae72e.png | exec-eccef736-d2d7-48fb-a485-397f70f66918.png |
| Story | exec-86e06622-669e-4967-bda9-e65830c0cd5e.png | exec-2e5f39f5-8d96-4e34-8716-703ce05a989a.png |

Implementation artifact12678346695, `fullworth-visual-40377964637`, SHA256 `8bee057556e1a00532840526b860262c2e82715ff4ae80fa6e9c135d6fa35d75`, downloaded/checksum-verified at `C:/Users/brist/.codex/attachments/fullworth-theme-c66e534/`. Exact anonymous Home captures are `theme-pair-{editorial|workspace|story}-{light|dark}-home-chromium-reference.png`; associated regionsJSON verifies anonymous state,1487×1058 CSS viewport, deviceScaleFactor1. Source and implementation have equal pixels/density; no stretching, browser chrome or phone frames.

Strict comparator `C:/Users/brist/.codex/attachments/fullworth-compare-theme-references.py` rejects authentication/density/viewport mismatches. It produced24 combined source-left/implementation-right boards plus manifest in `C:/Users/brist/.codex/attachments/fullworth-qa-c66e534/`. Opened all six `{pair}-{mode}-full.png` boards. Focused boards opened: Editorial light/dark example; Workspace light/dark example; Story light hero/dark example; Editorial light, Workspace dark and Story light header. These expose actual typography, labels, colors, asset crop and navigation rather than relying on scaled full views.

Additional actual captures opened: all six `theme-pair-{pair}-{mode}-overview-chromium-reference.png`; all six English anonymous phone captures,390px light/320px dark,1000px high; Workspace/light Chromium and Story/dark WebKit mobile menus at320px/200% text. Overview uses the selected family language with real existing components and synthetic owned fixtures; there was no separate selected Overview mock to claim a pixel match against.

## Findings and five fidelity surfaces

No actionable P0/P1/P2 findings remain in the inspected views and measured matrix.

- **Fonts/typography:** Editorial retains the two-line serif desktop headline and ruled serif comparison. Workspace has its single-line sans desktop headline. Story has the intended three English display lines and clear copper second line; the earlier fallback-induced fourth line is gone. Licensed same-origin Inter WOFF2, family FullWorth Display, loads and passes the browser font assertion. Standard UI/financial text retains established fonts. Minor glyph/optical-weight differences from generated references are P3, not an exact-font identity claim.
- **Spacing/layout rhythm:** Editorial is centered/open, Workspace retains its276px cobalt desktop rail and list/detail composition, Story retains payment→comparison→evidence and visible evidence conclusion. Mobile content stacks without horizontal clipping,44px controls remain reachable,200% root text is actually>=31.9px. The repeated Story process section, overflowing numeric flex item, tablet header overlap and narrow summary tracks are resolved. Additional financial qualification text and appearance controls intentionally consume space.
- **Colors/tokens:** Six family/mode palettes are visibly distinct and consistent across Home/Overview. Cobalt rail ink and Workspace light mode glyph are now legible. Core copy, controls and actual visible mobile-menu labels pass4.5 contrast; explicit high-contrast base check passes>=7. Raw foreground/background chains and ratios are retained in auditJSON. The corrected diagnostic understands computed sRGB/RGB/RGBA and alpha. Flat palettes deliberately replace generated glows/gradients.
- **Image quality/assets:** Existing FullWorth mark and supplied sprite icons remain real source assets. Generated fictional EN/ES statement paper is sharp at rendered scale, transparent edges are clean, and the paper's own printed text stays within the image. Workspace intentionally reuses this paper evidence rather than reproducing the mock's generic gray document card. No customer data, invented live statement, custom CSS art or new icon drawings.
- **Copy/content:** Headlines preserve selected family identity. Product copy is deliberately narrower and evidence-qualified than mock claims: no claim that all spending/recurring bills were found; a single sample payment has no renewal/annualization inference; conditional USD300 illustration explicitly assumes12 months. Original currencies/coverage denominators/timing qualifications and uncertain/failed states survive in Overview. Evidence entry and anonymous/authenticated actions are functional.

Intentional product differences: independent style/mode controls; accessible native sample buttons/disclosures; honest no-history and no-live-check wording; cents/original currency and annualization qualifications; existing brand/icons; supplied statement asset reuse; native ordered-list markers instead of decorative numbered disks. These preserve hierarchy and functionality while honoring financial truth. P3 follow-up only: minor optical font/marker refinement, subject to the same accessibility/performance safeguards. No further P3 loop required.

## Actual execution

Exact c66e534 source:
- Backend2,870 .NET +127 Node, zero failures/skips; backend/offline builds zero warnings/errors; Android compilation and required dependency review passed.
- Chromium2,188 and WebKit2,212 theme assertions. **330 geometry cases per engine includes108 financial-state cases and12 mobile-menu cases**; these are overlapping counts, not450 distinct cases. Both auditJSON files have empty layoutIssues/errors. Each engine observed122 bank and122 bill fixture reads.
- Three styles × two modes × EN/ES × Home/Overview ×320/390/768/1440px × normal/200% text; disconnected/repair/first-sync/empty/sparse/mixed/saved/error/loading; failed-refresh data retention; original-currency invariance and no financial writes.
- Keyboard/native selector, local persistence/reload/navigation, blocked storage, sample/evidence interaction, account-aware entry, reduced motion/system preference/explicit override/efficiency, no admin link for denied account.
- All12 mobile menu cases per engine measure sheet/actual label contrast, above-fixed-nav placement, scroll/focus every destination, Escape and focus restoration.
- Only ancillary preference/admin GETs are reused within this synthetic matrix, after one actual200 JSON preference read and403 admin denial. Writes rejected; routes restored. Production rate limits/account code unchanged. Existing real disposable BFF/account/security suites independently passed afterward.
- Existing responsive672, native-select126, ARIA37 and subscription54 cases/assertions per engine passed; account/security/planning/navigation recovery suites passed.
- Dedicated confirmation40377964643: dialog38, BFF74, Hosted Link24 cases passed across Chromium/WebKit.
- WebKit24 screenshot CSP denials each matched observed Playwright1.55.1 `body {}` style insertion during exact-page capture. No other injected styles or browser errors accepted. Production CSP unchanged.
- Container security, parser isolation and disposable encrypted backup/restore fixtures passed. No real provider/device/private-financial/production clean-host/legal acceptance is inferred.

## Comparison and diagnostic history

Earlier blocked reports are superseded by the accepted c66e534 comparison above. This table preserves findings, repairs and post-fix evidence; full historical documents remain in Git history.

| Source / CI | Finding or diagnostic limitation | Repair and subsequent evidence |
|---|---|---|
| e8bada8 /40292167632 | Existing-suite captures1440px did not match selected1487px/auth state | Dedicated exact-size anonymous captures, metadata and strict comparator; matched d4981c9 onward |
| b479cc0 /40294050862 | P2 mode target39×39 |44px dimensions/focus; later measured suites pass |
| ddfc12e /40296968650 | P2 tablet768px/200% document803px, wrapped header overlap | Auto-height header, earlier stacked comparison/font-relative grids;875ac39/c66 geometry clear |
| f1c63aa /40301745942 | Fixture missed includeInactive query; honest empty account shown | Bounded query-aware reads; actual122+122 reads in final matrix |
| d4981c9 /40303579702 | P2 mobile numeric overflow, narrow Workspace tracks, topbar crowding, undersized display/table hierarchy;16 Chromium/48 WebKit findings | Breakable amount span, responsive tracks/topbar, cascade and type fixes;875ac39 zero geometry findings |
|875ac39 /40305803720 | Screenshot-only WebKit CSP injection; matched artifact retrieval failed | Exact inserted-style/message accounting; a3172bd actual full browser pass, artifact12670085499 verified |
| a3172bd /40360544623 | Matched boards: P2 Story fallback caused fourth line, duplicate process; Editorial heading/Workspace rail alignment | Licensed local font, visible shared anchor, remove duplicate Story panel, heading/track corrections;10f7daf matched recapture |
|10f7daf /40364203186 | P2 crowded Story tracking/excess vertical spacing; separate confirmation stalled before tests in slow Azure mirror | Tracking/spacing corrections visible in5495d71 and accepted c66; bounded signed Ubuntu URI prep in disposable runners |
|5495d71 /40368663432 | Transient fixture0×0 loading control; P2 faint Workspace/light mode glyph found in actual phone image | Atomic measurement waits for appearance only; soft-surface token fixed; new mobile menu coverage; c66 clear |
|13dc4f9 /40371911364 | Main installer still used mirror-list indirection; dedicated40371911639 passed | Same bounded signed-archive prep includes/etc/apt/apt-mirrors.txt in both jobs; no timeout/permission/test weakening; final both jobs passed |
|feefd5d /40374562638 |24 false menu contrast readings from normalized sRGB/container measurement; real auxiliary429 on repeated reloads | Actual label/raw-chain measurement and GET-only verified ancillary fixtures; c66 both engines330/108/12, zero issues/errors |

## Handoff checklist

- [x] All six matched full comparisons and focused typography/spacing/color/asset/copy review.
- [x] Actual desktop Overview, phone Home and enlarged mobile menu inspection.
- [x] Both-engine financial-state, interaction, contrast, motion/performance, console/no-write acceptance.
- [x] Separate confirmation/BFF/Hosted Link regression execution.
- [ ] Final documentation-head required checks and review/merge verification, then update repository/Drive handoff.
- [ ] Separately authorized production promotion/deployment and applicable real acceptance; outside this development completion.
