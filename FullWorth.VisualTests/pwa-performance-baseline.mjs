import assert from "node:assert/strict";
import { writeFile } from "node:fs/promises";
import path from "node:path";
import { webkit } from "playwright";
import { requireDisposableOrigin, summarizeStaticResources, maximumLayoutShiftWindow }
  from "./performance-metrics.mjs";

// Installs only in newly-created disposable browser contexts. No application,
// framework internals, request payloads or production instrumentation are changed.
function installProbe() {
  try { localStorage.setItem("fullworth.performance-profile", "balanced"); } catch {}
  const supported = new Set(globalThis.PerformanceObserver?.supportedEntryTypes ?? []);
  const state = { shifts: [], lcp: null, longTasks: [], profileCalls: {}, shiftsDropped: 0, longTasksDropped: 0 };
  const token = Math.random();
  let profile;
  Object.defineProperty(window, "FullWorthPerformance", {
    configurable: true,
    get: () => profile,
    set(value) {
      for (const name of ["getPreference", "getTransactionLoadLimit", "getExecutionMode", "savePreference"]) {
        const original = value?.[name];
        if (typeof original === "function") value[name] = function(...args) {
          state.profileCalls[name] = (state.profileCalls[name] ?? 0) + 1;
          return original.apply(this, args);
        };
      }
      profile = value;
    }
  });
  if (supported.has("layout-shift")) new PerformanceObserver(list => {
    const entries = list.getEntries();
    state.shiftsDropped += Math.max(0, entries.length - 500);
    state.shifts.push(...entries.slice(-500).map(entry =>
      ({ value: entry.value, startTime: entry.startTime, hadRecentInput: entry.hadRecentInput })));
    state.shiftsDropped += Math.max(0, state.shifts.length - 500);
    state.shifts = state.shifts.slice(-500);
  }).observe({ type: "layout-shift", buffered: true });
  if (supported.has("largest-contentful-paint")) new PerformanceObserver(list => {
    for (const entry of list.getEntries()) state.lcp = entry.startTime;
  }).observe({ type: "largest-contentful-paint", buffered: true });
  if (supported.has("longtask")) new PerformanceObserver(list => {
    const entries = list.getEntries();
    state.longTasksDropped += Math.max(0, entries.length - 500);
    state.longTasks.push(...entries.slice(-500).map(entry =>
      ({ duration: entry.duration, startTime: entry.startTime })));
    state.longTasksDropped += Math.max(0, state.longTasks.length - 500);
    state.longTasks = state.longTasks.slice(-500);
  }).observe({ type: "longtask", buffered: true });
  window.__fullworthCiPerformance = {
    token,
    reset() {
      state.shifts = []; state.lcp = null; state.longTasks = []; state.profileCalls = {};
      state.shiftsDropped = 0; state.longTasksDropped = 0;
      performance.clearResourceTimings();
      return { token, start: performance.now() };
    },
    read() {
      const links = [...document.querySelectorAll('link[rel="stylesheet"]')];
      const hrefs = links.map(link => link.href);
      return {
        token, now: performance.now(),
        navigation: performance.getEntriesByType("navigation")[0]?.toJSON() ?? null,
        fcp: performance.getEntriesByName("first-contentful-paint")[0]?.startTime ?? null,
        lcp: state.lcp, shifts: state.shifts, longTasks: state.longTasks,
        shiftsDropped: state.shiftsDropped, longTasksDropped: state.longTasksDropped,
        supportsLayoutShift: supported.has("layout-shift"),
        supportsLcp: supported.has("largest-contentful-paint"),
        supportsLongTasks: supported.has("longtask"),
        stylesheetLinks: links.length,
        duplicateStylesheetHrefs: hrefs.length - new Set(hrefs).size,
        externalScriptElements: document.querySelectorAll("script[src]").length,
        resources: performance.getEntriesByType("resource").filter(entry => {
          const url = new URL(entry.name);
          return url.origin === location.origin && !/^\/(?:bff|api)\//.test(url.pathname) &&
            /\.(?:css|m?js)$/i.test(url.pathname);
        }).map(entry => {
          const url = new URL(entry.name);
          return { name: url.origin + url.pathname, transferSize: entry.transferSize,
            encodedBodySize: entry.encodedBodySize, decodedBodySize: entry.decodedBodySize };
        }),
        profileCalls: state.profileCalls,
        effectiveProfile: document.documentElement.dataset.fullworthPerformance ?? null,
        appError: !!document.querySelector("main .app-error-state"),
        workerSupported: "serviceWorker" in navigator,
        workerControlled: navigator.serviceWorker?.controller !== null &&
          navigator.serviceWorker?.controller !== undefined
      };
    }
  };
}

export async function verifyPwaPerformance(authenticatedPage, outputDir) {
  const origin = requireDisposableOrigin(authenticatedPage.url());
  const source = await authenticatedPage.context().storageState(); // memory only; never an artifact
  const storageState = { ...source, cookies: source.cookies.filter(cookie => cookie.name !== ".AspNetCore.Culture") };
  const originalBrowser = authenticatedPage.context().browser();
  assert.ok(originalBrowser, "The existing disposable Chromium browser is required.");
  const report = {
    schemaVersion: 1,
    testedMergeRevision: /^[0-9a-f]{40}$/.test(process.env.GITHUB_SHA ?? "") ? process.env.GITHUB_SHA : null,
    runId: /^\d+$/.test(process.env.GITHUB_RUN_ID ?? "") ? process.env.GITHUB_RUN_ID : null,
    fixture: "existing-disposable-synthetic-account",
    completed: false,
    limitations: [
      "CI laboratory samples, not physical installed-device or production acceptance.",
      "Cold/warm refer to the browser cache; the shared server is already warm.",
      "Worker-blocked HTTP-cache controls and worker-allowed startup samples are separate; no request routing in either.",
      "At most500 observer records are retained per window; overflow makes affected metrics unknown.",
      "Heading readability includes automation observation overhead, not financial-data readiness.",
      "Paint/LCP/layout observations are bounded windows, not final field Core Web Vitals or percentile SLAs.",
      "Profile API counters cover four public methods; resource entries are not import or total JS-interop counts.",
      "Bill detail is the truthful unavailable state; populated/private histories and CPU/device throttling are not claimed."
    ],
    samples: []
  };
  const cases = [
    { name: "overview-cold", route: "/app", selector: ".overview-page h1", mode: "cold" },
    { name: "overview-warm", route: "/app", selector: ".overview-page h1", mode: "reload" },
    { name: "bills", route: "/app/bills", heading: "Bills" },
    { name: "activity", route: "/app/activity", heading: "Activity" },
    { name: "account", route: "/app/account", heading: "Account hub" },
    { name: "transactions", route: "/app/account/transactions", heading: "Transactions" },
    { name: "settings", route: "/app/account/settings", selector: ".settings-page h1" },
    { name: "bill-detail-unavailable", route: "/app/bills/11111111-1111-4111-8111-111111111111",
      selector: "main .app-error-state h2", unavailable: true }
  ];
  const run = async (browser, engine, selectedCases, network, workerPolicy = "block") => {
    const context = await browser.newContext({
      baseURL: origin, ignoreHTTPSErrors: true, serviceWorkers: workerPolicy, locale: "en-US",
      storageState, viewport: network === "emulated-slow" ? { width: 390, height: 844 } : { width: 1440, height: 1000 }
    });
    let session;
    try {
      await context.addInitScript(installProbe);
      const page = await context.newPage();
      if (network === "emulated-slow") {
        session = await context.newCDPSession(page);
        await session.send("Network.enable");
        await session.send("Network.emulateNetworkConditions", {
          offline: false, latency: 150, downloadThroughput: 187500, uploadThroughput: 75000,
          connectionType: "cellular3g"
        });
      }
      let pending = new Set(), counts = {}, failures = 0, unsafe = 0, pageErrors = 0, lastActivity = 0;
      const bff = request => new URL(request.url()).origin === origin &&
        new URL(request.url()).pathname.startsWith("/bff/");
      page.on("request", request => {
        if (!bff(request)) return;
        if (!["GET", "HEAD", "OPTIONS"].includes(request.method())) unsafe++;
        pending.add(request); lastActivity = Date.now();
      });
      page.on("requestfinished", request => {
        if (pending.delete(request)) lastActivity = Date.now();
      });
      page.on("requestfailed", request => {
        if (pending.delete(request)) { failures++; lastActivity = Date.now(); }
      });
      page.on("response", response => {
        if (!bff(response.request())) return;
        const status = response.status();
        counts[status] = (counts[status] ?? 0) + 1;
      });
      page.on("pageerror", () => pageErrors++);
      for (const item of selectedCases) {
        if (workerPolicy === "allow" && engine === "chromium" && item.mode === "reload")
          await page.waitForFunction(() => navigator.serviceWorker?.controller != null,
            null, { timeout: 10000 });
        pending = new Set(); counts = {}; failures = 0; unsafe = 0; pageErrors = 0; lastActivity = Date.now();
        const before = await page.evaluate(() => window.__fullworthCiPerformance?.reset() ?? null);
        let requestedMode = "document";
        if (item.mode === "reload") await page.reload({ waitUntil: "domcontentloaded" });
        else {
          const link = page.locator('a[href="' + item.route + '"]:visible').first();
          if (!item.mode && await link.count()) {
            requestedMode = "link";
            await Promise.all([
              page.waitForURL(url => url.pathname === item.route),
              link.click()
            ]);
          } else await page.goto(item.route, { waitUntil: "domcontentloaded" });
        }
        assert.equal(new URL(page.url()).pathname, item.route, engine + ": benchmark must stay authenticated.");
        const heading = item.selector ? page.locator(item.selector).first()
          : page.getByRole("heading", { name: item.heading, exact: true }).first();
        await heading.waitFor({ state: "visible", timeout: 20000 });
        const headingObservation = await page.evaluate(() => ({
          token: window.__fullworthCiPerformance.token, now: performance.now()
        }));
        const deadline = Date.now() + 20000;
        // Require actual observed BFF reads and a quiet bounded observation window.
        while ((Object.keys(counts).length === 0 || pending.size || Date.now() - lastActivity < 900) &&
               Date.now() < deadline) await page.waitForTimeout(50);
        assert.ok(Object.keys(counts).length > 0 && pending.size === 0, engine + ": incomplete read observation.");
        assert.equal(unsafe, 0, "Performance measurement must not issue BFF mutations.");
        assert.equal(failures, 0, engine + ": failed reads invalidate the measurement.");
        assert.ok(!Object.keys(counts).some(status => Number(status) === 401 || Number(status) === 429 ||
          Number(status) >= 500), engine + ": auth/rate/provider failures invalidate the measurement.");
        assert.equal(pageErrors, 0, engine + ": unhandled page errors invalidate the measurement.");
        const observation = await page.evaluate(() => window.__fullworthCiPerformance.read());
        const enhanced = requestedMode === "link" && before?.token === observation.token;
        const start = enhanced ? before.start : 0;
        const resources = summarizeStaticResources(observation.resources, origin);
        const longTasks = observation.longTasks.filter(entry => entry.startTime >= start);
        if (engine === "chromium" && item.mode === "cold") {
          assert.ok(resources.css.knownSizeEntries > 0 && resources.js.knownSizeEntries > 0,
            "Chromium cold baseline must expose actual static transfer/body sizes.");
        }
        assert.equal(new URL(page.url()).pathname, item.route, "No late authenticated redirect during measurement.");
        assert.equal(observation.effectiveProfile, "balanced");
        assert.equal(observation.appError, !!item.unavailable);
        assert.ok(observation.stylesheetLinks >= 20, "Actual styles must be present, not an unstyled success.");
        const round = value => Number.isFinite(value) ? Number(value.toFixed(2)) : null;
        report.samples.push({
          engine, browserVersion: browser.version(), case: item.name, network, serviceWorkerPolicy: workerPolicy,
          viewport: page.viewportSize(), navigationKind: enhanced ? "enhanced-link" : item.mode === "reload" ? "reload" : "document",
          headingObservedMs: round(headingObservation.now - start),
          observationEndedMs: round(observation.now - start),
          ttfbMs: enhanced ? null : round(observation.navigation?.responseStart),
          domContentLoadedMs: enhanced ? null : round(observation.navigation?.domContentLoadedEventEnd),
          fcpMs: enhanced ? null : round(observation.fcp),
          lcpAtObservationMs: enhanced ? null : round(observation.lcp),
          layoutShiftWindowScore: observation.supportsLayoutShift && observation.shiftsDropped === 0 ? maximumLayoutShiftWindow(observation.shifts.filter(entry => entry.startTime >= start)) : null,
          longTasksObserved: observation.supportsLongTasks && observation.longTasksDropped === 0 ? longTasks.length : null,
          maxLongTaskMs: observation.supportsLongTasks && observation.longTasksDropped === 0 ? round(Math.max(0, ...longTasks.map(entry => entry.duration))) : null,
          stylesheetLinks: observation.stylesheetLinks, duplicateStylesheetHrefs: observation.duplicateStylesheetHrefs,
          externalScriptElements: observation.externalScriptElements, resources,
          profileApiCalls: observation.profileCalls, bffReadResponseCounts: counts,
          expectedUnavailable: !!item.unavailable, workerSupported: observation.workerSupported,
          workerControlled: observation.workerControlled,
          observerRecordsDropped: { layoutShift: observation.shiftsDropped, longTask: observation.longTasksDropped }
        });
      }
    } finally {
      if (session) await session.detach().catch(() => {});
      await context.close();
    }
  };
  try {
    await run(originalBrowser, "chromium", cases, "unthrottled");
    await run(originalBrowser, "chromium", cases.slice(0, 2), "unthrottled", "allow");
    const webkitBrowser = await webkit.launch({ headless: true });
    try {
      await run(webkitBrowser, "webkit", cases, "unthrottled");
      await run(webkitBrowser, "webkit", cases.slice(0, 2), "unthrottled", "allow");
    }
    finally { await webkitBrowser.close(); }
    await run(originalBrowser, "chromium", [cases[0]], "emulated-slow");
    report.completed = true;
  } finally {
    await writeFile(path.join(outputDir, "pwa-performance-baseline.json"), JSON.stringify(report, null, 2) + "\n");
  }
  console.log("PWA performance baseline: " + report.samples.length +
    " aggregate-only CI observations; no production/device performance acceptance.");
}
