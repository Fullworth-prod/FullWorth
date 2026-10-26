import assert from "node:assert/strict";
import { webkit } from "playwright";

// Actual authenticated components; financial reads/writes below are intercepted
// synthetic CI fixtures. Never run this suite against a live financial account.
export async function verifyAriaStates(page) {
  const origin = new URL(page.url()).origin;
  assert.ok(new URL(origin).hostname.endsWith(".localhost"), "Isolated CI only.");
  const run = async (target, engine) => {
    let checks = 0, releaseDiscovery, releaseExport, releaseSync;
    const writes = [];
    const billId = "ce111111-1111-4111-8111-111111111111";
    const bill = (id, name, amount, previous) => ({ id, providerName: name,
      category: "Other", isActive: true, source: "Manual", currentAmount: amount,
      previousAverage: previous, currencyCode: "USD", hasTransactionEvidence: true,
      hasMonthlyCadenceEvidence: true });
    const pattern = "**/bff/**";
    const handler = async route => {
      const request = route.request(), endpoint = new URL(request.url()).pathname;
      if (!["GET", "HEAD", "OPTIONS"].includes(request.method())) {
        writes.push(request.method() + " " + endpoint);
        assert.ok(request.headers()["x-csrf-token"], "Synthetic writes still use the real request module.");
        if (endpoint === "/bff/account/export" && request.method() === "POST") {
          await new Promise(resolve => { releaseExport = resolve; });
          return route.fulfill({ status: 401, contentType: "application/problem+json",
            body: JSON.stringify({ title: "Current password is incorrect." }) });
        }
        if (endpoint === "/bff/bill-monitoring/refresh" && request.method() === "POST") {
          await new Promise(resolve => { releaseSync = resolve; });
          return route.fulfill({ status: 503, json: {} });
        }
        await route.abort("blockedbyclient");
        throw new Error("Unexpected synthetic write: " + request.method() + " " + endpoint);
      }
      if (endpoint === "/bff/bill-streams") return route.fulfill({ json: [
        bill(billId, "Synthetic increase", 20, 10),
        bill("ce222222-2222-4222-8222-222222222222", "Synthetic steady", 10, 10) ] });
      if (endpoint === "/bff/alerts") return route.fulfill({ json: [false, true].map((read, index) => ({
        id: index ? "cf222222-2222-4222-8222-222222222222" : "cf111111-1111-4111-8111-111111111111",
        billStreamId: billId, alertType: "BillIncrease", severity: "Warning",
        title: "Synthetic alert", message: "Synthetic comparison", isRead: read,
        isDismissed: false, createdAtUtc: "2026-10-01T12:00:00Z", updatedAtUtc: "2026-10-01T12:00:00Z"
      })) });
      if (endpoint === "/bff/bill-discovery/diagnostics") {
        await new Promise(resolve => { releaseDiscovery = resolve; });
        return route.fulfill({ status: 503, json: {} });
      }
      if (endpoint === "/bff/bank-transactions") return route.fulfill({ json: [] });
      return route.fallback(); // Other reads use only the disposable test owner.
    };
    const attribute = async (locator, name, value) => {
      assert.equal(await locator.getAttribute(name), value, engine + " " + name);
      checks++;
    };
    const held = async read => {
      const deadline = Date.now() + 10000;
      while (!read() && Date.now() < deadline) await new Promise(resolve => setTimeout(resolve, 20));
      assert.ok(read(), engine + " must reach the held response.");
    };
    const valid = async () => {
      const invalid = await target.locator(".app-shell").evaluate(shell =>
        [...shell.querySelectorAll("[aria-busy],[aria-expanded],[aria-pressed]")].flatMap(element =>
          ["aria-busy", "aria-expanded", "aria-pressed"].filter(name => element.hasAttribute(name) &&
            !["true", "false"].includes(element.getAttribute(name)))
            .map(name => element.tagName.toLowerCase() + ":" + name)));
      assert.deepEqual(invalid, [], "ARIA states must be explicit true/false strings.");
      checks++;
    };
    await target.context().addCookies([{ name: ".AspNetCore.Culture",
      value: encodeURIComponent("c=en-US|uic=en-US"), url: origin, secure: true, sameSite: "Lax" }]);
    await target.route(pattern, handler);
    try {
      for (const [route, group, rows] of [
        ["/app/bills", ".fw-bills-filters", ".fw-bills-row"],
        ["/app/activity", ".activity-filters", ".alert-card"]
      ]) {
        await target.goto(route, { waitUntil: "domcontentloaded", timeout: 20000 });
        await target.locator(rows).first().waitFor({ timeout: 15000 });
        const filters = target.locator(group + " button");
        assert.ok(await filters.count() >= 4);
        for (let index = 0; index < await filters.count(); index++)
          await attribute(filters.nth(index), "aria-pressed", index === 0 ? "true" : "false");
        await valid();
        await filters.nth(1).click();
        await target.waitForFunction(selector => document.querySelectorAll(selector).length === 1,
          rows, { timeout: 15000 });
        await attribute(filters.first(), "aria-pressed", "false");
        await attribute(filters.nth(1), "aria-pressed", "true");
        assert.equal(await target.locator(group).getByRole("button", { pressed: true }).count(), 1);
        await filters.first().focus();
        await target.keyboard.press("Space");
        await target.waitForFunction(selector => document.querySelectorAll(selector).length === 2,
          rows, { timeout: 15000 });
        await attribute(filters.first(), "aria-pressed", "true");
        await attribute(filters.nth(1), "aria-pressed", "false");
        await valid();
        if (route === "/app/bills") {
          const toggle = target.locator('[aria-controls="discovery-details"]');
          await attribute(toggle, "aria-expanded", "false");
          await toggle.click();
          await held(() => releaseDiscovery);
          await attribute(toggle, "aria-expanded", "true");
          await attribute(target.locator("#discovery-details"), "aria-busy", "true");
          releaseDiscovery();
          await target.locator("#discovery-details [role=alert]").waitFor({ timeout: 15000 });
          await attribute(target.locator("#discovery-details"), "aria-busy", "false");
          await toggle.click();
          await target.locator("#discovery-details").waitFor({ state: "detached", timeout: 15000 });
          await attribute(toggle, "aria-expanded", "false");
        }
      }
      await target.goto("/app/account/privacy", { waitUntil: "domcontentloaded", timeout: 20000 });
      await target.getByRole("button", { name: "Download data", exact: true }).waitFor();
      await target.locator('.privacy-page[aria-busy="false"]').waitFor();
      const privacy = target.locator(".privacy-page");
      await attribute(privacy, "aria-busy", "false");
      await target.getByRole("button", { name: "Download data", exact: true }).click();
      await target.locator(".export-confirmation input[type=password]").fill("Synthetic!Proof123");
      await target.getByRole("button", { name: "Download securely", exact: true }).click();
      await held(() => releaseExport);
      await attribute(privacy, "aria-busy", "true");
      await valid();
      releaseExport();
      await target.locator(".export-confirmation [role=alert]").waitFor({ timeout: 15000 });
      await attribute(privacy, "aria-busy", "false");
      await valid();

      await target.goto("/app/account/transactions", { waitUntil: "domcontentloaded", timeout: 20000 });
      const sync = target.getByRole("button", { name: "Sync now", exact: true });
      await sync.waitFor();
      await target.waitForFunction(() => !document.querySelector(".page-heading button")?.disabled,
        null, { timeout: 15000 });
      await attribute(sync, "aria-busy", "false");
      await sync.click();
      await held(() => releaseSync);
      const busySync = target.getByRole("button", { name: "Syncing…", exact: true });
      await attribute(busySync, "aria-busy", "true");
      assert.equal(await busySync.isDisabled(), true);
      await valid();
      releaseSync();
      await target.getByText("Sync failed. Your previously loaded transactions are still shown. Try syncing again.",
        { exact: true }).waitFor({ timeout: 15000 });
      await attribute(sync, "aria-busy", "false");
      assert.equal(await sync.isDisabled(), false);
      await valid();
      assert.deepEqual(writes, ["POST /bff/account/export", "POST /bff/bill-monitoring/refresh"],
        "Only the two intercepted synthetic requests are allowed, without replay.");
      console.log("ARIA interaction states passed: " + checks + " assertions; engine: " + engine);
    } finally {
      releaseDiscovery?.(); releaseExport?.(); releaseSync?.();
      await target.unroute(pattern, handler);
    }
  };
  await run(page, "chromium");
  const browser = await webkit.launch({ headless: true });
  try {
    const context = await browser.newContext({ baseURL: origin, ignoreHTTPSErrors: true,
      serviceWorkers: "block", storageState: await page.context().storageState(),
      viewport: { width: 390, height: 844 } });
    try { await run(await context.newPage(), "webkit"); }
    finally { await context.close(); }
  } finally { await browser.close(); }
}
