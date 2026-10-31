# Shared legal-navigation usability — 2026-10-28

Verified #994 artifact12785653600 SHA256b826b01d66166acd094e29f58dac532017b008cf5e658cac58703157883f372c showed Spanish missing-page footer links running together. MainLayout uses adjacent Terms/Privacy anchors, but the only existing flex/gap/44px target rule is scoped to .home-site-shell, excluding authentication/recovery pages.

Share these existing legal-navigation rules with all .site-shell pages; keep20px wrapping gap and44px minimum height, add44px minimum width, bounded width,4px inline padding and inherited-theme color fallback. Standalone legal page bodies, content, versions, links and auth/financial behavior are unchanged. This is navigation usability, not legal acceptance or certification.

Actual stylesheet/representative markup in isolated Chromium151 reproduced17px normal targets/zero gap on both EN/ES routes at320/390px; enlarged targets still ran together. All eight original width/language/text-scale combinations failed the intended usability bounds. The candidate passed all eight with44px targets/20px gaps and no off-page links. This is isolated CSS evidence, not compiled/pinned-browser/physical-device acceptance.

Extend the existing12 real disposable recovery-page cases to check legal destinations, minimum44px targets, >=19px measured inter-target gap, page bounds and keyboard focus order at100/200% text on EN/ES missing/error pages (16 legal-navigation states). Keep the existing locale/404/reconnect/default-off safeguards and all broader theme/financial/privacy/browser gates. No published legal text or private financial data is changed. No deployment, master promotion, activation, credentials/grants/settings or spending.
