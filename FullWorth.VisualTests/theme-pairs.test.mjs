import assert from "node:assert/strict";
import path from "node:path";
import { webkit } from "playwright";
import { writeFile } from "node:fs/promises";

// Only disposable authenticated CI. Financial data below is explicitly synthetic.
// This suite never creates accounts, changes financial records or contacts providers.
export async function verifyThemePairs(page, outputDir) {
  const origin = new URL(page.url()).origin;
  assert.ok(new URL(origin).hostname.endsWith(".localhost"), "Disposable CI origin required.");
  const originalViewport = page.viewportSize();
  const original = await page.evaluate(() => ({
    design: document.documentElement.dataset.design ?? "editorial",
    mode: document.documentElement.dataset.theme ?? "dark"
  }));
  const storageState = await page.context().storageState();
  const styles = ["editorial", "workspace", "story"];
  const modes = ["light", "dark"];
  const languages = ["en-US", "es"];
  const bills = [
    { id: "de111111-1111-4111-8111-111111111111", providerName: "Synthetic Mountain & Valley Community Utilities — Household Services",
      category: "Utility", isActive: true, source: "Manual", currentAmount: 89.25, previousAverage: 79.25,
      currencyCode: "USD", hasTransactionEvidence: true, hasMonthlyCadenceEvidence: true },
    { id: "de222222-2222-4222-8222-222222222222", providerName: "Synthetic Music Plan", category: "Other",
      isActive: true, source: "Manual", currentAmount: 13.8, previousAverage: 13.8,
      currencyCode: "USD", hasTransactionEvidence: true, hasMonthlyCadenceEvidence: true },
    { id: "de333333-3333-4333-8333-333333333333", providerName: "Synthetic Regional Internet", category: "Internet",
      isActive: true, source: "Manual", currentAmount: 45, previousAverage: 50,
      currencyCode: "EUR", hasTransactionEvidence: true, hasMonthlyCadenceEvidence: false }
  ];
  const bank = { id: "df111111-1111-4111-8111-111111111111", institutionName: "Synthetic Theme Bank",
    status: 0, lastSuccessfulSyncAtUtc: "2026-10-01T12:00:00Z", createdAtUtc: "2026-09-01T12:00:00Z" };

  const run = async (target, engine) => {
    let state = "populated", failRefresh = false, hold, release;
    let geometryCases = 0, stateCases = 0, menuCases = 0, assertions = 0;
    const unsafe = [], errors = [], financial = new Map(), intentionalFailures = new Set();
    const layoutIssues = [], issueCaptures = new Set();
    const fixtureReads = { banks: 0, bills: 0 };
    const screenshotCsp = []; let capturing, captureCsp;
    const expect = (value, message) => { assert.ok(value, engine + ": " + message); assertions++; };
    const audit = (value, message) => {
      assertions++;
      if (!value) layoutIssues.push(engine + ": " + message);
    };
    const record = request => {
      if (new URL(request.url()).pathname.startsWith("/bff/") &&
          !["GET", "HEAD", "OPTIONS"].includes(request.method()))
        unsafe.push(request.method() + " " + new URL(request.url()).pathname);
    };
    const pageError = error => errors.push(error.name + ": " + error.message.slice(0, 160));
    const consoleError = message => {
      if (message.type() !== "error") return;
      const url = message.location().url;
      // Playwright1.55.1 screenshotter.ts injects exactly "body {}" for WebKit
      // animation synchronization. Production style-src self correctly rejects it.
      // Count only this message during a verified capture, never application CSP.
      if (engine === "webkit" && capturing && url === capturing.url &&
          message.text() === "Refused to apply a stylesheet because its hash, its nonce, or 'unsafe-inline' does not appear in the style-src directive of the Content Security Policy.") {
        captureCsp.push(message.text()); return;
      }
      if (intentionalFailures.has(url) || url?.startsWith("https://www.google.com/s2/favicons")) return;
      // AdminNavLink deliberately probes GET /bff/admin/users for a non-admin.
      // Keep the real denial and all other errors; this exact403 is expected.
      if (url && new URL(url, origin).pathname === "/bff/admin/users" &&
          message.text().includes("403")) return;
      errors.push("console: " + message.text().slice(0, 160) + " at " + url);
    };
    const read = async route => {
      assert.equal(route.request().method(), "GET", "Appearance fixture is read-only.");
      const snapshot = state, fail = failRefresh, pending = hold;
      if (pending) await pending;
      if (snapshot === "error" || fail) {
        intentionalFailures.add(route.request().url());
        return route.fulfill({ status: 503, json: {} });
      }
      const isBank = new URL(route.request().url()).pathname === "/bff/bank-connections";
      if (isBank) fixtureReads.banks++; else fixtureReads.bills++;
      let connections = [bank], charges = bills;
      if (snapshot === "disconnected") { connections = []; charges = []; }
      if (snapshot === "repair") { connections = [{ ...bank, status: 1 }]; charges = []; }
      if (snapshot === "first-sync") { connections = [{ ...bank, lastSuccessfulSyncAtUtc: null }]; charges = []; }
      if (snapshot === "empty") charges = [];
      if (snapshot === "sparse") charges = bills.map(b => ({ ...b, previousAverage: 0 }));
      if (snapshot === "mixed") connections = [bank, { ...bank,
        id: "df222222-2222-4222-8222-222222222222", lastSuccessfulSyncAtUtc: null }];
      if (snapshot === "saved") connections = [];
      return route.fulfill({ json: isBank ? connections : charges });
    };
    const patterns = [new RegExp("/bff/bank-connections(?:[?].*)?$"), new RegExp("/bff/bill-streams(?:[?].*)?$")];
    const logo = new RegExp("^https://www[.]google[.]com/s2/favicons");
    const blockLogo = route => route.abort("failed");
    const culture = async (language, surface = target) => surface.context().addCookies([{ name: ".AspNetCore.Culture",
      value: encodeURIComponent("c=en-US|uic=" + language), url: origin, secure: true, sameSite: "Lax" }]);
    const ready = async label => {
      await target.locator(".design-picker select:not([disabled])").waitFor();
      if (label === "overview") await target.locator(".overview-dashboard .fw-pulse").waitFor();
      else await target.locator(".home-bill-example").waitFor();
      await target.evaluate(async () => { await document.fonts.ready; });
    };
    const appearance = async (style, mode, surface = target) => {
      const select = surface.locator(".design-picker select");
      await select.waitFor();
      await surface.waitForFunction(() => document.querySelector(".design-picker select")?.disabled === false);
      if (await select.inputValue() !== style) await select.selectOption(style);
      await surface.waitForFunction(value => document.documentElement.dataset.design === value, style);
      await surface.locator(".theme-toggle:not([disabled])").waitFor();
      if (await surface.locator("html").getAttribute("data-theme") !== mode)
        await surface.locator(".theme-toggle").click();
      await surface.waitForFunction(value => document.documentElement.dataset.theme === value, mode);
      // Let the existing220ms mode transition finish before measuring colors.
      await surface.waitForTimeout(250);
    };
    const capture = async (surface, options) => {
      if (engine !== "webkit") return surface.screenshot(options);
      await surface.evaluate(() => {
        window.__themeCaptureStyles = [];
        window.__themeCaptureObserver = new MutationObserver(records => {
          for (const record of records) for (const node of record.addedNodes)
            if (node.nodeType === Node.ELEMENT_NODE && node.tagName === "STYLE")
              window.__themeCaptureStyles.push(node.textContent);
        });
        window.__themeCaptureObserver.observe(document.head, { childList: true });
      });
      capturing = { url: surface.url() }; captureCsp = [];
      try { await surface.screenshot(options); }
      finally {
        const inserted = await surface.evaluate(() => {
          window.__themeCaptureObserver.disconnect();
          const value = window.__themeCaptureStyles;
          delete window.__themeCaptureObserver; delete window.__themeCaptureStyles;
          return value;
        });
        const denied = captureCsp.length;
        capturing = undefined; captureCsp = undefined;
        const known = inserted.filter(text => text === "body {}").length;
        expect(denied <= known && inserted.every(text => text === "body {}"),
          "Only verified Playwright empty-rule capture styles may explain screenshot CSP messages: " +
          JSON.stringify({ denied, known, inserted }));
        screenshotCsp.push({ file: path.basename(options.path), denied, known });
      }
    };

    const geometry = async (label, width, large, surface = target) => {
      const priorIssues = layoutIssues.length;
      await surface.setViewportSize({ width, height: 1000 });
      await surface.evaluate(enlarged => {
        document.documentElement.style.fontSize = enlarged ? "200%" : "";
        document.documentElement.style.setProperty("overflow-x", "visible", "important");
        document.body.style.setProperty("overflow-x", "visible", "important");
      }, large);
      await surface.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
      const result = await surface.evaluate(() => ({
        viewport: document.documentElement.clientWidth, rootFontSize: parseFloat(getComputedStyle(document.documentElement).fontSize),
        documentWidth: document.documentElement.scrollWidth, bodyWidth: document.body.scrollWidth,
        offenders: [...document.querySelectorAll("body *")].filter(element => {
          const r = element.getBoundingClientRect(), s = getComputedStyle(element);
          return s.display !== "none" && s.visibility !== "hidden" && s.position !== "fixed" &&
            r.width > 0 && (r.right > document.documentElement.clientWidth + 1 || element.scrollWidth > element.clientWidth + 1);
        }).slice(0, 8).map(element => ({ tag: element.tagName, classes: element.className?.toString().slice(0, 100) }))
      }));
      if (large) audit(result.rootFontSize >= 31.9, "200% text zoom actually applies: " + result.rootFontSize);
      audit(result.documentWidth <= result.viewport + 1 && result.bodyWidth <= result.viewport + 1,
        label + " fits " + width + "px, large=" + large + " without clipping: " + JSON.stringify(result));
      // Measure visibility and size atomically: hydration must not replace a
      // locator between an asynchronous isVisible() and bounding-box read.
      const controls = await surface.evaluate(() =>
        [...document.querySelectorAll(".design-picker select, .theme-toggle, .home-primary-action, .sample-option, .sample-evidence-disclosure > summary")]
          .filter(element => element.getClientRects().length > 0 &&
            getComputedStyle(element).visibility !== "hidden")
          .map(element => {
            const r = element.getBoundingClientRect();
            return { control: element.tagName + "." + element.className,
              left: r.left, right: r.right, height: r.height, viewport: innerWidth };
          }));
      for (const r of controls)
        audit(r.left >= -1 && r.right <= r.viewport + 1 && r.height >= 44,
          label + ": appearance/demo control remains usable: " + JSON.stringify(r));
      geometryCases++;
      if (layoutIssues.length > priorIssues && !issueCaptures.has(label) && issueCaptures.size < 12) {
        issueCaptures.add(label);
        await capture(surface, { path: path.join(outputDir, "theme-pair-issue-" + engine + "-" +
          label.replaceAll("/", "-") + "-" + width + "-" + large + ".png"), fullPage: true });
      }
    };
    const readable = async (selector, surface = target) => {
      const locator = typeof selector === "string" ? surface.locator(selector).first() : selector;
      const label = typeof selector === "string" ? selector : "mobile navigation item";
      const ratio = await locator.evaluate(element => {
        const rgba = value => {
          const v = value.match(/[0-9.]+/g).map(Number);
          return [v[0], v[1], v[2], v.length > 3 ? v[3] : 1];
        };
        const fg = rgba(getComputedStyle(element).color);
        let bg = [0, 0, 0, 0];
        for (let node = element; node && bg[3] < .999; node = node.parentElement) {
          const next = rgba(getComputedStyle(node).backgroundColor);
          const alpha = bg[3] + next[3] * (1 - bg[3]);
          if (alpha > 0) bg = [0, 1, 2].map(i =>
            (bg[i] * bg[3] + next[i] * next[3] * (1 - bg[3])) / alpha).concat(alpha);
        }
        if (bg[3] < 1) bg = bg.slice(0, 3).map(c => c * bg[3] + 255 * (1 - bg[3])).concat(1);
        const text = fg.slice(0, 3).map((c, i) => c * fg[3] + bg[i] * (1 - fg[3]));
        const luminance = v => v.slice(0, 3).map(c => {
          c /= 255; return c <= .04045 ? c / 12.92 : Math.pow((c + .055) / 1.055, 2.4);
        }).reduce((sum, c, i) => sum + c * [.2126, .7152, .0722][i], 0);
        const a = luminance(text), b = luminance(bg);
        return (Math.max(a, b) + .05) / (Math.min(a, b) + .05);
      });
      audit(ratio >= 4.5, "Core text retains readable contrast: " + label + ", ratio=" + ratio.toFixed(2));
    };
    const snapshot = () => target.locator(".fw-pulse-amount, .fw-pulse-stat strong, .fw-bill-value strong")
      .allTextContents().then(values => values.map(value => value.trim().replace(/\s+/g, " ")));

    target.on("request", record);
    target.on("pageerror", pageError);
    target.on("console", consoleError);
    for (const pattern of patterns) await target.route(pattern, read);
    await target.route(logo, blockLogo);
    try {
      await culture("en-US");
      await target.goto(origin + "/", { waitUntil: "domcontentloaded" });
      await ready("home");
      for (const style of styles) for (const mode of modes) {
        await appearance(style, mode);
        for (const language of languages) {
          await culture(language);
          for (const [label, route] of [["home", "/"], ["overview", "/app"]]) {
            state = "populated";
            await target.goto(origin + route, { waitUntil: "domcontentloaded" });
            await ready(label);
            expect(await target.locator("html").getAttribute("data-design") === style &&
              await target.locator("html").getAttribute("data-theme") === mode,
              "Navigation restores the selected pair before rendering.");
            expect(await target.locator(".design-picker select").inputValue() === style,
              "The visible selector agrees with the restored style.");
            if (label === "home") {
              expect(await target.locator(".hero-title:visible").count() === 1, "One chosen headline is visible.");
              expect(await target.locator('.home-evidence a[href="/app/bills"]').isVisible(),
                "Authenticated evidence entry opens the existing bills workspace.");
              expect(await target.locator(".sample-" + style).isVisible(), "The matching composition is visible.");
              expect(await target.locator(".sample-qualification").isVisible() &&
                (await target.locator(".sample-qualification").innerText()).includes("12"),
                "The fictional annualized illustration retains its twelve-month qualification.");
            } else {
              const amounts = await snapshot();
              if (!financial.has(language)) financial.set(language, amounts);
              assert.deepEqual(amounts, financial.get(language), "Themes cannot change financial content.");
              const summary = await target.locator(".fw-pulse").innerText();
              expect(summary.includes("$103.05") && summary.includes("EUR 45.00"),
                "Original-currency groups remain separate.");
              expect(await target.locator(".fw-pulse-spotlight").count() === 0 &&
                await target.locator(".overview-attention").count() === 1,
                "One priority presentation replaces the duplicate per-bill spotlight.");
            }
            for (const selector of (label === "home"
              ? [".hero-title:visible", ".hero-description", ".sample-qualification", ".home-primary-action", ".theme-toggle"]
              : [".fw-pulse-amount", ".fw-pulse-label", ".fw-coverage-note", ".fw-primary-action", ".theme-toggle"]))
              await readable(selector);
            for (const width of [320, 390, 768, 1440]) for (const large of [false, true]) {
              await geometry(label + "/" + style + "/" + mode + "/" + language, width, large);
              if (engine === "webkit" && language === "es" && !large &&
                  width === (mode === "dark" ? 320 : 390))
                await capture(target, { path: path.join(outputDir,
                  "theme-pair-" + style + "-" + mode + "-" + label + "-" + engine + "-es-" + width + ".png"), fullPage: true });
            }
            await target.evaluate(() => { document.documentElement.style.fontSize = ""; });
            await target.setViewportSize({ width: 1487, height: 1058 });
            if (label === "home")
              await target.locator(".hero-copy").evaluate(element => Promise.all(element.getAnimations().map(animation => animation.finished)));
            if (engine === "chromium" && language === "en-US" && label === "overview") {
              await target.evaluate(() => scrollTo({ top: 0, behavior: "instant" }));
              await capture(target, { path: path.join(outputDir,
                "theme-pair-" + style + "-" + mode + "-" + label + "-" + engine + "-reference.png"), fullPage: false });
              await writeFile(path.join(outputDir, "theme-pair-" + style + "-" + mode + "-" + label + "-" + engine + "-regions.json"),
                JSON.stringify(await target.evaluate(() => ({
                  viewport: { width: innerWidth, height: innerHeight, density: devicePixelRatio },
                  authenticated: true, route: location.pathname,
                  regions: Object.fromEntries([".overview-heading", ".overview-attention", ".fw-pulse", ".fw-watchlist"].map(selector =>
                    [selector, document.querySelector(selector)?.getBoundingClientRect().toJSON()]))
                })), null, 2));
            }
            if (label === "overview") {
              const arrangement = await target.evaluate(() => ({
                priority: document.querySelector(".overview-attention").getBoundingClientRect().toJSON(),
                summary: document.querySelector(".fw-pulse").getBoundingClientRect().toJSON(),
                watchlist: document.querySelector(".fw-watchlist").getBoundingClientRect().toJSON()
              }));
              if (style === "editorial") audit(arrangement.priority.bottom <= arrangement.summary.top + 2,
                "Editorial puts priority and ruled summary in an open vertical flow.");
              if (style === "workspace") audit(arrangement.priority.left > arrangement.watchlist.left &&
                arrangement.summary.top < arrangement.priority.top,
                "Workspace places comparison detail beside the watchlist.");
              if (style === "story") audit(arrangement.priority.left < arrangement.summary.left &&
                arrangement.watchlist.top >= arrangement.priority.top,
                "Story pairs priority and recognized charges above the watchlist.");
            }
          }
        }
      }

      // Selected Home references show an anonymous visitor. Keep that state
      // separate from authenticated navigation/persistence cases above.
      const publicContext = await target.context().browser().newContext({
        ignoreHTTPSErrors: true, serviceWorkers: "block",
        viewport: { width: 1487, height: 1058 }
      });
      try {
        const publicPage = await publicContext.newPage();
        publicPage.on("request", record);
        publicPage.on("pageerror", pageError);
        publicPage.on("console", consoleError);
        await culture("en-US", publicPage);
        await publicPage.goto(origin + "/", { waitUntil: "domcontentloaded" });
        await publicPage.locator(".design-picker select:not([disabled])").waitFor();
        for (const style of styles) for (const mode of modes) {
          await appearance(style, mode, publicPage);
          await culture("en-US", publicPage);
          await publicPage.reload({ waitUntil: "domcontentloaded" });
          await publicPage.locator(".design-picker select:not([disabled])").waitFor();
          await publicPage.evaluate(async () => { await document.fonts.ready; });
          await publicPage.setViewportSize({ width: 1487, height: 1058 });
          await publicPage.locator(".hero-copy").evaluate(element => Promise.all(element.getAnimations().map(a => a.finished)));
          await readable(".theme-toggle", publicPage);
          if (style === "story" || style === "workspace") {
            expect(await publicPage.evaluate(() => [...document.fonts].some(font =>
              font.family.replaceAll('"', "") === "FullWorth Display" && font.status === "loaded")),
              "The self-hosted display face is loaded before reference capture.");
          }
          if (style === "story") {
            const lines = await publicPage.locator(".hero-title-story > span").evaluateAll(elements =>
              elements.map(element => ({
                height: element.getBoundingClientRect().height,
                lineHeight: parseFloat(getComputedStyle(element).lineHeight)
              })));
            expect(lines.length === 3 && lines.every(line => line.height <= line.lineHeight + 1),
              "The English Story reference retains three display lines: " + JSON.stringify(lines));
            expect(!await publicPage.locator(".home-process").isVisible(),
              "Story follows payment, comparison and evidence without a repeated process panel.");
          }
          expect(await publicPage.locator('.home-evidence a[href="/register"]').isVisible(),
            "Anonymous evidence entry retains account creation.");
          expect(await publicPage.locator('.header-actions a[href="/login"]').isVisible(),
            "Anonymous visitors retain a sign-in route.");
          expect(await publicPage.locator(".home-primary-action").getAttribute("href") === "/register",
            "The anonymous primary action opens account creation.");
          await publicPage.evaluate(() => scrollTo({ top: 0, behavior: "instant" }));
          await capture(publicPage, { path: path.join(outputDir,
            "theme-pair-" + style + "-" + mode + "-home-" + engine + "-reference.png"), fullPage: false });
          await writeFile(path.join(outputDir, "theme-pair-" + style + "-" + mode + "-home-" + engine + "-regions.json"),
            JSON.stringify(await publicPage.evaluate(() => ({
              viewport: { width: innerWidth, height: innerHeight, density: devicePixelRatio },
              authenticated: false, route: location.pathname,
              regions: Object.fromEntries([".site-header", ".hero", ".home-bill-example", ".hero-actions"].map(selector =>
                [selector, document.querySelector(selector)?.getBoundingClientRect().toJSON()]))
            })), null, 2));
          const phoneWidth = mode === "dark" ? 320 : 390;
          await geometry("anonymous/" + style + "/" + mode + "/en-US", phoneWidth, false, publicPage);
          if (engine === "chromium") {
            await publicPage.evaluate(() => scrollTo({ top: 0, behavior: "instant" }));
            await capture(publicPage, { path: path.join(outputDir,
              "theme-pair-" + style + "-" + mode + "-home-" + engine + "-en-" + phoneWidth + "-phone.png"),
              fullPage: false });
          }
          await geometry("anonymous/" + style + "/" + mode + "/en-US", 768, true, publicPage);
          await culture("es", publicPage);
          await publicPage.reload({ waitUntil: "domcontentloaded" });
          await publicPage.locator(".design-picker select:not([disabled])").waitFor();
          await geometry("anonymous/" + style + "/" + mode + "/es", 390, true, publicPage);
          await publicPage.evaluate(() => { document.documentElement.style.fontSize = ""; });
        }
        await culture("en-US", publicPage);
        await publicPage.goto(origin + "/", { waitUntil: "domcontentloaded" });
        await publicPage.locator(".design-picker select:not([disabled])").waitFor();
        await Promise.all([
          publicPage.waitForURL(url => url.pathname === "/register", { waitUntil: "domcontentloaded" }),
          publicPage.locator(".home-primary-action").click()
        ]);
        await publicPage.locator('.auth-form input[name="email"]').waitFor();
        expect(await publicPage.locator('.auth-form input[name="email"]').isVisible(),
          "The public account action reaches the actual registration screen without submitting it.");
      } finally { await publicContext.close(); }

      // Real native keyboard and sample interactions, without financial writes.
      await culture("en-US");
      await target.goto(origin + "/", { waitUntil: "domcontentloaded" });
      await ready("home");
      await appearance("editorial", "light");
      await target.locator("a.brand").first().focus();
      let reached = false;
      for (let attempt = 0; attempt < 16; attempt++) {
        await target.keyboard.press("Tab");
        reached = await target.locator(".design-picker select").evaluate(element => element === document.activeElement);
        if (reached) break;
      }
      expect(reached, "The style selector is reachable through keyboard navigation.");
      await target.keyboard.press("ArrowDown");
      await target.keyboard.press("Enter");
      await target.waitForFunction(() => document.documentElement.dataset.design === "workspace");
      expect(await target.locator(".design-picker select").inputValue() === "workspace",
        "Native keyboard selection applies a different design.");
      await target.locator(".sample-option").nth(1).click();
      await target.locator(".sample-detail").getByText("No comparable history", { exact: true }).waitFor();
      expect(!(await target.locator(".sample-detail").innerText()).includes("$300"),
        "A single illustrative payment cannot acquire an annualized projection.");
      await target.locator(".sample-option").nth(2).click();
      await target.locator(".sample-detail").getByText("No increase to project", { exact: true }).waitFor();
      await target.locator(".sample-option").nth(0).click();
      const image = target.locator(".sample-workspace .sample-paper img");
      await image.scrollIntoViewIfNeeded();
      await target.waitForFunction(() => {
        const image = document.querySelector(".sample-workspace .sample-paper img");
        return image?.complete && image.naturalWidth > 0;
      });
      expect((await image.getAttribute("src")).endsWith("-en.webp"), "The English sample asset loads.");
      await target.locator(".sample-workspace .sample-evidence-disclosure > summary").click();
      expect(await target.locator(".sample-workspace .sample-evidence-disclosure[open]").isVisible(),
        "Sample evidence disclosure works.");
      await culture("es");
      await target.reload({ waitUntil: "domcontentloaded" });
      await ready("home");
      expect((await target.locator(".sample-workspace .sample-paper img").getAttribute("src")).endsWith("-es.webp"),
        "The Spanish sample uses its localized asset.");

      const states = [
        ["disconnected", ".onboarding-card"], ["repair", ".attention-state"],
        ["first-sync", ".sync-state"], ["empty", ".history-state"], ["sparse", ".fw-pulse"],
        ["mixed", ".connection-warning"], ["saved", ".connection-warning"],
        ["error", ".app-error-state"], ["loading", ".dashboard-loading"]
      ];
      for (const style of styles) for (const mode of modes) {
        await appearance(style, mode);
        for (const language of languages) {
          await culture(language);
          for (const [name, selector] of states) {
            state = name;
            if (name === "loading") hold = new Promise(resolve => { release = resolve; });
            try {
              await target.goto(origin + "/app", { waitUntil: "domcontentloaded" });
              await target.locator(selector).first().waitFor();
              await target.locator(".design-picker select:not([disabled])").waitFor();
              await target.locator(".theme-toggle:not([disabled])").waitFor();
              await target.evaluate(async () => { await document.fonts.ready; });
              await geometry("state/" + name + "/" + style + "/" + mode + "/" + language, 320, true);
              if (name === "sparse") expect(!(await target.locator(".overview-attention").innerText()).includes("$0.00"),
                "Sparse history is not false zero-change certainty.");
              if (name === "saved" || name === "mixed")
                expect(await target.locator(".connection-warning").first().isVisible(),
                  "Coverage and saved-record limitations remain visible.");
              stateCases++;
            } finally {
              release?.(); release = undefined; hold = undefined;
            }
            if (name === "loading") await target.locator(".fw-pulse").waitFor();
          }
        }
      }

      // The mobile sheet must stay readable/reachable even inside Workspace's
      // cobalt sidebar. Exercise the actual menu and Escape focus in every pair.
      state = "populated";
      for (const style of styles) for (const mode of modes) for (const language of languages) {
        await culture(language);
        await target.goto(origin + "/app", { waitUntil: "domcontentloaded" });
        await ready("overview");
        await appearance(style, mode);
        await target.setViewportSize({ width: 320, height: 1000 });
        await target.evaluate(() => { document.documentElement.style.fontSize = "200%"; });
        await target.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
        const trigger = target.locator(".mobile-menu-trigger");
        await trigger.click();
        const sheet = target.locator(".mobile-menu-sheet");
        await sheet.waitFor();
        expect(await trigger.getAttribute("aria-expanded") === "true", "The real menu reports its open state.");
        await geometry("menu/" + style + "/" + mode + "/" + language, 320, true);
        await readable(".mobile-menu-sheet");
        const bounds = await sheet.evaluate(element => {
          const r = element.getBoundingClientRect(), nav = document.querySelector(".app-sidebar").getBoundingClientRect();
          return { left: r.left, right: r.right, top: r.top, bottom: r.bottom, viewport: innerWidth, navTop: nav.top };
        });
        audit(bounds.left >= -1 && bounds.right <= bounds.viewport + 1 && bounds.top >= -1 &&
          bounds.bottom <= bounds.navTop + 2, "Menu remains above persistent navigation: " + JSON.stringify(bounds));
        const links = await sheet.locator(".app-nav-link").all();
        expect(links.length >= 4, "The account/settings navigation remains available.");
        for (const link of links) await readable(link);
        if (language === "en-US")
          await capture(target, { path: path.join(outputDir, "theme-pair-" + style + "-" + mode +
            "-menu-" + engine + "-en-320-large.png"), fullPage: false });
        for (const link of links) {
          await link.focus();
          await link.scrollIntoViewIfNeeded();
          const visible = await link.evaluate(element => {
            const item = element.getBoundingClientRect(), menu = element.closest(".mobile-menu-sheet").getBoundingClientRect();
            return item.top >= menu.top - 1 && item.bottom <= menu.bottom + 1;
          });
          audit(visible, "Each menu destination is reachable within its scrolling sheet.");
        }
        await target.keyboard.press("Escape");
        await sheet.waitFor({ state: "hidden" });
        await target.waitForFunction(() => document.activeElement?.classList.contains("mobile-menu-trigger"));
        expect(await trigger.getAttribute("aria-expanded") === "false", "Escape closes the menu and restores focus.");
        menuCases++;
      }

      state = "populated";
      await culture("en-US");
      await target.goto(origin + "/app", { waitUntil: "domcontentloaded" });
      await ready("overview");
      const beforeFailure = await snapshot();
      failRefresh = true;
      await target.locator(".status-check-button").click();
      await target.locator(".connection-warning[role='status']").waitFor();
      assert.deepEqual(await snapshot(), beforeFailure, "A failed refresh retains the validated financial snapshot.");
      failRefresh = false;
      await target.locator(".status-check-button:not([disabled])").waitFor();
      await target.locator(".status-check-button").click();
      await target.waitForFunction(() => !document.querySelector(".connection-warning[role='status']"));

      await target.emulateMedia({ reducedMotion: "reduce" });
      await target.goto(origin + "/", { waitUntil: "domcontentloaded" });
      await ready("home");
      expect(await target.locator(".hero-copy").evaluate(element => getComputedStyle(element).animationName) === "none",
        "System reduced motion disables entrance animation.");
      await target.emulateMedia({ reducedMotion: "no-preference" });
      await target.evaluate(() => {
        document.documentElement.dataset.fullworthReduceMotion = "true";
      });
      expect(await target.locator(".hero-copy").evaluate(element => getComputedStyle(element).animationName) === "none",
        "The explicit reduced-motion preference remains respected.");
      await target.evaluate(() => {
        document.documentElement.dataset.fullworthReduceMotion = "false";
        document.documentElement.dataset.fullworthPerformance = "efficiency";
      });
      expect(await target.locator(".hero-copy").evaluate(element => getComputedStyle(element).animationName) === "none",
        "Efficiency mode avoids decorative motion.");

      await target.evaluate(() => { document.documentElement.dataset.fullworthHighContrast = "true"; });
      for (const style of styles) for (const mode of modes) {
        await appearance(style, mode);
        const contrast = await target.evaluate(() => {
          const rgb = value => value.match(/[0-9.]+/g).slice(0, 3).map(Number);
          const luminance = values => values.map(value => {
            const c = value / 255;
            return c <= .04045 ? c / 12.92 : Math.pow((c + .055) / 1.055, 2.4);
          }).reduce((sum, value, index) => sum + value * [.2126, .7152, .0722][index], 0);
          const fg = luminance(rgb(getComputedStyle(document.querySelector(".paired-home")).color));
          const bg = luminance(rgb(getComputedStyle(document.querySelector(".home-site-shell")).backgroundColor));
          return (Math.max(fg, bg) + .05) / (Math.min(fg, bg) + .05);
        });
        expect(contrast >= 7, "High-contrast appearance remains readable for " + style + "/" + mode);
      }
      await target.evaluate(() => { document.documentElement.dataset.fullworthHighContrast = "false"; });

      const blocked = await target.context().browser().newContext({
        storageState: await target.context().storageState(), ignoreHTTPSErrors: true,
        serviceWorkers: "block", viewport: { width: 390, height: 1000 }
      });
      try {
        await blocked.addInitScript(() => {
          Object.defineProperty(window, "localStorage", { get() { throw new DOMException("Blocked", "SecurityError"); } });
        });
        const isolated = await blocked.newPage();
        isolated.on("request", record);
        isolated.on("pageerror", pageError);
        isolated.on("console", consoleError);
        await isolated.goto(origin + "/", { waitUntil: "domcontentloaded" });
        await isolated.locator(".design-picker select:not([disabled])").waitFor();
        await isolated.locator(".design-picker select").selectOption("story");
        await isolated.waitForFunction(() => document.documentElement.dataset.design === "story");
        expect(await isolated.locator(".sample-story").isVisible(), "Blocked storage still permits session appearance.");
        await isolated.reload({ waitUntil: "domcontentloaded" });
        await isolated.locator(".design-picker select:not([disabled])").waitFor();
        expect(await isolated.locator("html").getAttribute("data-design") === "editorial",
          "Blocked storage reload uses an honest safe default.");
      } finally { await blocked.close(); }

      await writeFile(path.join(outputDir, "theme-pair-audit-" + engine + ".json"),
        JSON.stringify({ engine, geometryCases, stateCases, menuCases, layoutIssues, errors, fixtureReads, screenshotCsp }, null, 2));
      console.log("Three-pair complete geometry diagnostics", JSON.stringify(layoutIssues));
      expect(unsafe.length === 0, "Appearance and public demo make no financial writes: " + JSON.stringify(unsafe));
      expect(errors.length === 0, "No uncaught browser errors: " + JSON.stringify(errors));
      console.log("Three-pair layout audit: engine=" + engine + "; geometry=" + geometryCases +
        "; states=" + stateCases + "; menus=" + menuCases + "; issues=" + layoutIssues.length + "; verified capture CSP denials=" + screenshotCsp.reduce((sum, item) => sum + item.denied, 0));
      if (layoutIssues.length) throw new AggregateError(layoutIssues.slice(0, 24).map(message => new Error(message)),
        engine + ": " + layoutIssues.length + " layout/contrast checks failed; all cases were audited.");
      console.log("Three-pair UI: " + assertions + " assertions; " + geometryCases + " geometry cases; " +
        stateCases + " state cases; " + menuCases + " menu cases; engine=" + engine + "; synthetic financial reads only.");
    } catch (error) {
      release?.();
      await capture(target, { path: path.join(outputDir, "theme-pair-failure-" + engine + ".png"), fullPage: true });
      console.error("Three-pair audit progress", { engine, geometryCases, stateCases, menuCases,
        layoutIssues: layoutIssues.length, fixtureReads, browserErrors: errors.length });
      console.error("Three-pair failure state", await target.evaluate(() => ({
        pathname: location.pathname, design: document.documentElement.dataset.design,
        mode: document.documentElement.dataset.theme, language: document.documentElement.lang,
        viewport: innerWidth
      })));
      throw error;
    } finally {
      target.off("request", record);
      target.off("pageerror", pageError);
      target.off("console", consoleError);
      for (const pattern of patterns) await target.unroute(pattern, read);
      await target.unroute(logo, blockLogo);
      await target.emulateMedia({ reducedMotion: "no-preference" });
    }
  };

  const engineFailures = [];
  try { await run(page, "chromium"); } catch (error) { engineFailures.push(error); }
  const browser = await webkit.launch({ headless: true });
  try {
    const context = await browser.newContext({ storageState, ignoreHTTPSErrors: true,
      serviceWorkers: "block", viewport: { width: 1487, height: 1058 } });
    try {
      try { await run(await context.newPage(), "webkit"); } catch (error) { engineFailures.push(error); }
    } finally { await context.close(); }
  } finally { await browser.close(); }
  await page.context().addCookies([{ name: ".AspNetCore.Culture",
    value: encodeURIComponent("c=en-US|uic=en-US"), url: origin, secure: true, sameSite: "Lax" }]);
  await page.evaluate(value => {
    localStorage.setItem("fullworth-design", value.design);
    localStorage.setItem("billwatch-theme", value.mode);
  }, original);
  if (originalViewport) await page.setViewportSize(originalViewport);
  if (engineFailures.length) throw new AggregateError(engineFailures, "Three-pair UI acceptance failed.");
}
