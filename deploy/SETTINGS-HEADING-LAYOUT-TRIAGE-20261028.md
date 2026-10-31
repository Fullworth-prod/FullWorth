# Narrow Settings heading and layout triage — 2026-10-28

## Demonstrated accessibility defect

The verified #992 WebKit 320px/200% Spanish Settings screenshot broke words inside the security heading despite passing document-overflow geometry. Actual current account-settings.css contains a later mobile .app-shell .settings-header h1 at 2.3rem, overriding the earlier responsive heading. An isolated Chromium151 rendering of actual CSS reproduced English preferences and Spanish Seguridad/preferencias wrapping inside words at 320px with root32px. The same DOM at root16px did not split those words.

## Source correction and regression scope

At widths up to480px, use a 1.6rem heading with1.15 line height and modest letter spacing. Keep overflow-wrap fallback for unusually long text. The font still doubles from25.6px to51.2px when root size doubles16→32; no maximum-size cap, global clipping, script-driven font shrinking or suppressed text zoom is introduced. Wider Settings typography is unchanged. The isolated actual-CSS EN/ES test passed all eight original/corrected/normal/enlarged combinations: old200% words split, corrected200% whole words, document width320 throughout. This is not pinned-CI app or physical-device acceptance.

Expand the existing real disposable Settings browser matrix to EN/ES × all three pairs × both modes × both engines. In addition to existing geometry/contrast, use per-word DOM Range rectangles to reject intraword wrapping at320px/200% and confirm the heading itself doubles relative to normal390px. Preserve six Spanish screenshot filenames and add English equivalents. No account/security/preference operation is performed.

## Overview layout shift remains unresolved

Prior two actual slow390px laboratory runs measured bounded shift0.216613, with fixed aliases app-content/overview-loading. Do not infer a cause or claim this heading change fixes Overview. Extend only the existing disposable-CI observer with previous/current rectangle coordinates, fixed region aliases and a small numeric header/content/root-font snapshot. Rectangles come from the layout-shift attribution; the current layout snapshot is collected at observer delivery and is not proof of event timing or causality. Keep the existing21 sample/read-integrity/financial/privacy gates unchanged.

The summarizer allows only finite bounded numeric x/y/width/height, five fixed layout numbers, at mostfive sources/event and the existing ten largest events. Unknowns stay null/unclassified; no DOM text, IDs, classes, URLs, credentials, account values or storage are exported. Two new negative/privacy tests plus21 existing methodology cases passed23/23 in isolated source execution. The actual candidate observer captured a controlled100px synthetic movement in isolated Chromium151; that validates observer wiring, not the FullWorth shift cause or a CLS result.

This candidate needs final-head compiled/container/browser evidence. Physical installed devices, production performance/field Web Vitals and all existing external release gates remain open. No production/master promotion, deployment, feature activation, credentials/grants/settings, financial caching or framework/network instrumentation changes.
