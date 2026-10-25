import assert from "node:assert/strict";
import path from "node:path";
import { writeFile } from "node:fs/promises";

// Never include text, input values, URLs with IDs, or financial evidence in
// geometry diagnostics. Element types/classes are enough to locate a constraint.
export async function measureDocumentGeometry(page) {
  return await page.evaluate(() => {
    const viewport = document.documentElement.clientWidth;
    const identify = element => ({ tag: element.tagName.toLowerCase(),
      classes: [...element.classList].slice(0, 5) });
    const offenders = [...document.querySelectorAll(".app-shell, .app-shell *")]
      .filter(element => {
        const rect = element.getBoundingClientRect(), style = getComputedStyle(element);
        return style.display !== "none" && style.visibility !== "hidden" &&
          rect.width > 0 && rect.height > 0 &&
          (rect.right > viewport + 1 || rect.left < -1);
      }).slice(0, 16).map(element => {
        const r = element.getBoundingClientRect(), style = getComputedStyle(element);
        return { ...identify(element), left: Math.round(r.left), right: Math.round(r.right),
          width: Math.round(r.width), minWidth: style.minWidth, display: style.display,
          position: style.position, overflowX: style.overflowX };
      });
    return { viewport, documentWidth: document.documentElement.scrollWidth,
      bodyWidth: document.body.scrollWidth, offenders };
  });
}

export async function assertNoDocumentOverflow(page, label) {
  const geometry = await measureDocumentGeometry(page);
  assert.ok(geometry.documentWidth <= geometry.viewport + 1 &&
    geometry.bodyWidth <= geometry.viewport + 1,
    "Document overflow: " + label + " " + JSON.stringify(geometry));
  return geometry;
}

export async function verifyResponsiveMatrix(page, outputDir, setCulture) {
  const widths = [320, 375, 390, 430, 768, 1024, 1440];
  const routes = [
    ["overview", "/app", ".fw-pulse"],
    ["bills", "/app/bills", ".fw-bills-row"],
    ["activity", "/app/activity", ".alert-card"],
    ["account", "/app/account", ".account-panel"],
    ["transactions", "/app/account/transactions", ".app-page"],
    ["planning", "/app/planning", ".planning-workspace"],
    ["settings", "/app/account/settings", ".app-page"],
    ["privacy", "/app/account/privacy", ".app-page"],
    ["profile", "/app/profile", ".app-page"],
    ["subscription", "/app/subscription", ".app-page"],
    ["setup-loading", "/app/setup", ".onboarding-card"],
    ["setup", "/app/setup", ".setup-fields"]
  ];
  const report = [];
  const failures = [];
  const unsafe = [];
  const record = request => {
    const url = new URL(request.url());
    if (url.pathname.startsWith("/bff/") && !["GET", "HEAD", "OPTIONS"].includes(request.method()))
      unsafe.push(request.method() + " " + url.pathname.replace(/[0-9a-f]{8}-[0-9a-f-]{27,}/gi, ":id"));
  };
  page.on("request", record);
  try {
    for (const language of ["en-US", "es"]) {
      await setCulture(language);
      for (const [label, url, ready] of routes) {
        let original = null, releaseLoading = null, loadingHandler = null;
        const heldLoading = label === "setup-loading";
        try {
          if (heldLoading) {
            const gate = new Promise(resolve => { releaseLoading = resolve; });
            loadingHandler = async route => { await gate; await route.continue(); };
            await page.route("**/bff/account/preferences", loadingHandler);
          }
          await page.setViewportSize({ width: 390, height: 1000 });
          await page.goto(url, { waitUntil: "domcontentloaded" });
          await page.locator(ready).first().waitFor({ state: "visible" });
          await page.waitForFunction(() => Boolean(document.querySelector('link[href="/app-polish.css"]')?.sheet));
          if (!heldLoading) {
            // Measure the intended ready view, rather than racing initial API
            // loads. Loading is a separate held-response case below.
            await page.waitForFunction(() => {
              const root = document.querySelector(".app-page");
              const busy = root?.getAttribute("aria-busy")?.toLowerCase() === "true";
              const loading = [...document.querySelectorAll(
                ".settings-loading-card,.fw-profile-loading,.planning-loading,.planning-section-loading")]
                .some(element => element.getClientRects().length > 0);
              return root && !busy && !loading;
            });
          }
          await page.evaluate(() => document.fonts?.ready);
          original = await page.evaluate(() => ({
            html: document.documentElement.style.cssText,
            body: document.body.style.cssText,
            theme: document.documentElement.dataset.theme
          }));
          // Test layout rather than a global clipping rule. This intentionally
          // makes accidental overflow visible; it never masks a defect.
          await page.evaluate(() => {
            document.documentElement.style.setProperty("overflow-x", "visible", "important");
            document.body.style.setProperty("overflow-x", "visible", "important");
          });
          const initialFont = await page.evaluate(() => parseFloat(getComputedStyle(document.documentElement).fontSize));
          for (const theme of ["light", "dark"])
          for (const scale of [1, 2]) {
            await page.emulateMedia({ reducedMotion: scale === 2 ? "reduce" : "no-preference" });
            await page.evaluate(({theme, scale}) => {
              document.documentElement.dataset.theme = theme;
              document.documentElement.style.fontSize = scale === 2 ? "200%" : "";
            }, { theme, scale });
            for (const width of widths) {
              await page.setViewportSize({ width, height: 1000 });
              // Let CSS, ResizeObserver and the measured navigation inset settle.
              await page.evaluate(() => new Promise(resolve =>
                requestAnimationFrame(() => requestAnimationFrame(resolve))));
              await page.waitForFunction(() => {
                const shell = document.querySelector(".app-shell");
                const bar = shell?.querySelector(".app-sidebar");
                const nav = shell?.querySelector(".mobile-bottom-nav");
                if (!shell || !bar || !nav) return false;
                const mobile = getComputedStyle(nav).display !== "none";
                const actual = parseFloat(shell.style.getPropertyValue("--fw-navigation-inset"));
                const expected = mobile ? Math.ceil(bar.getBoundingClientRect().height +
                  Math.max(0, parseFloat(getComputedStyle(bar).bottom) || 0) + 20) : 0;
                return Number.isFinite(actual) && Math.abs(actual - expected) <= 1;
              });
              if (heldLoading) assert.equal(await page.locator(".experience-setup-page")
                .getAttribute("aria-busy").then(value => value?.toLowerCase()), "true", "The loading fixture must remain held throughout inspection");
              const name = label + "/" + width + "/" + language + "/" + theme + "/" + scale;
              const geometry = await measureDocumentGeometry(page);
              const caseFailures = [];
              if (geometry.documentWidth > geometry.viewport + 1 ||
                  geometry.bodyWidth > geometry.viewport + 1)
                caseFailures.push("document overflow");
              const rootFont = await page.evaluate(() => parseFloat(getComputedStyle(document.documentElement).fontSize));
              if (scale === 2 && rootFont < initialFont * 1.9)
                caseFailures.push("enlarged text was reduced");
              if (width <= 900) {
                const nav = await page.locator(".app-sidebar").evaluate(element => {
                  const r = element.getBoundingClientRect();
                  return { left: r.left, right: r.right, top: r.top, bottom: r.bottom,
                    width: innerWidth, height: innerHeight };
                });
                if (!(nav.left >= -1 && nav.right <= nav.width + 1 &&
                  nav.top >= 0 && nav.bottom <= nav.height + 1))
                  caseFailures.push("navigation outside viewport");
              }
              report.push({ route: label, width, language, theme, textScale: scale,
                reducedMotion: scale === 2, failures: caseFailures, ...geometry });
              if (caseFailures.length) {
                failures.push({ name, failures: caseFailures, ...geometry });
                if (failures.filter(f => f.name.startsWith(label + "/")).length === 1)
                  await page.screenshot({ path: path.join(outputDir,
                    "responsive-failure-" + label + ".png"), fullPage: true });
              }
              if (language === "en-US" && theme === "dark" &&
                  ((width === 375 && scale === 1) || (width === 320 && scale === 2))) {
                await page.evaluate(() => window.scrollTo({ top: 0, behavior: "instant" }));
                await page.screenshot({ path: path.join(outputDir,
                  "responsive-" + label + "-" + width + "-" + scale + "x.png"), fullPage: true });
              }
            }
          }
        } catch (error) {
          await page.screenshot({ path: path.join(outputDir, "responsive-matrix-failure.png"), fullPage: true });
          throw error;
        } finally {
          if (original) await page.evaluate(original => {
            document.documentElement.style.cssText = original.html;
            document.body.style.cssText = original.body;
            document.documentElement.dataset.theme = original.theme;
          }, original);
          if (releaseLoading) {
            releaseLoading();
            await page.unroute("**/bff/account/preferences", loadingHandler);
            await page.waitForFunction(() => document.querySelector(".experience-setup-page")
              ?.getAttribute("aria-busy")?.toLowerCase() !== "true");
          }
        }
      }
    }
    assert.deepEqual(unsafe, [], "Responsive inspection must not mutate financial resources.");
    assert.equal(failures.length, 0, "Responsive failures: " + JSON.stringify(failures.slice(0, 24)));
    console.log("Responsive geometry passed: " + report.length +
      " route/width/theme/text/language cases; document clipping disabled during inspection.");
  } finally {
    await writeFile(path.join(outputDir, "responsive-matrix.json"), JSON.stringify({
      completedCases: report.length, failedCases: failures.length, widths,
      failures, measurements: report,
      physicalSafeAreaAcceptance: false, syntheticEvidence: true
    }, null, 2));
    page.off("request", record);
    await setCulture("en-US");
    await page.emulateMedia({ reducedMotion: "no-preference" });
    await page.setViewportSize({ width: 1440, height: 1100 });
  }
}
