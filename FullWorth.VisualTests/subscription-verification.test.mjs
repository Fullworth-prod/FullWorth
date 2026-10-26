import assert from "node:assert/strict";
import path from "node:path";
import { webkit } from "playwright";

// Real BFF/API/database exercise only on the disposable authenticated CI stack.
// Subsequent stale/error cases intercept synthetic reads/writes explicitly.
export async function verifySubscriptionVerification(page, outputDir) {
  const origin = new URL(page.url()).origin;
  assert.ok(new URL(origin).hostname.endsWith(".localhost"), "Disposable CI origin required.");
  const run = async (target, engine) => {
    let checks = 0, heldSave, attemptedWrites = 0;
    const expect = (value, message) => { assert.ok(value, engine + ": " + message); checks++; };
    await target.context().addCookies([{ name: ".AspNetCore.Culture",
      value: encodeURIComponent("c=en-US|uic=en-US"), url: origin, secure: true, sameSite: "Lax" }]);
    await target.goto(origin + "/app/subscription-checks");
    await target.getByRole("heading", { name: "Start a subscription review", exact: true }).waitFor();
    await target.locator(".review-provider-help > summary").click();
    const officialLinks = [
      ["Spotify account", "https://www.spotify.com/account"],
      ["Apple subscription help", "https://support.apple.com/billing"],
      ["Google Play subscriptions", "https://play.google.com/store/account/subscriptions"],
      ["PayPal automatic payment help", "https://www.paypal.com/us/cshelp/article/what-is-an-automatic-payment-and-how-do-i-update-or-cancel-one-help240"]
    ];
    for (const [label, href] of officialLinks) {
      const link = target.getByRole("link", { name: label, exact: true });
      expect(await link.getAttribute("href") === href, "Account help uses a fixed official destination.");
      expect(await link.getAttribute("referrerpolicy") === "no-referrer" &&
        (await link.getAttribute("rel")).includes("noreferrer"), "Account help does not send the review URL.");
    }
    expect(await target.getByText("Use the account and billing provider for this exact plan. Opening a link does not connect FullWorth or verify any subscription details. Record only what you can confirm.", { exact: true }).isVisible(),
      "Opening account help must not imply a merchant connection.");
    await target.getByLabel("Merchant", { exact: true }).fill("Synthetic subscription " + engine);
    await target.getByLabel("Account and plan label", { exact: true }).fill("Personal monthly plan");
    await target.getByLabel("Billed by", { exact: true }).fill("Synthetic direct billing");
    const createResponse = target.waitForResponse(r => new URL(r.url()).pathname === "/bff/subscription-reviews" &&
      r.request().method() === "POST");
    await target.getByRole("button", { name: "Start review", exact: true }).click();
    const createdResponse = await createResponse;
    expect(createdResponse.ok(), "Real BFF create must succeed.");
    const initial = await createdResponse.json();
    expect(initial.merchantVerification === "Unavailable" && initial.needsReview === true,
      "A new review cannot imply merchant verification or recurrence.");
    expect(initial.facts.every(f => f.value === null), "Initial details remain unknown.");
    await target.getByRole("heading", { name: "Confirm subscription details", exact: true }).waitFor();
    await target.getByLabel("Service access", { exact: true }).selectOption("Active");
    await target.getByLabel("Will it renew?", { exact: true }).selectOption("No");
    await target.getByLabel("Billing arrangement", { exact: true }).selectOption("Prepaid");
    await target.getByLabel("I reviewed these details for this exact account, plan and billing provider.", { exact: true }).check();
    const saveResponse = target.waitForResponse(r => r.url().includes("/bff/subscription-reviews/") &&
      r.url().endsWith("/evidence") && r.request().method() === "POST");
    await target.getByRole("button", { name: "Save my confirmation", exact: true }).click();
    const savedResponse = await saveResponse;
    expect(savedResponse.ok(), "Real BFF save must succeed.");
    const saved = await savedResponse.json();
    expect(saved.facts.find(f => f.field === "ServiceStatus").value === "Active" &&
      saved.facts.find(f => f.field === "Renewal").value === "No", "Active access and no renewal remain independent.");
    expect(saved.facts.every(f => f.evidence.every(e => e.source === "UserConfirmed")), "Manual evidence cannot become merchant proof.");
    await target.getByText("Your confirmation was saved. It is not a live merchant verification.", { exact: true }).waitFor();
    const readback = target.waitForResponse(r => new URL(r.url()).pathname ===
      "/bff/subscription-reviews/" + initial.id && r.request().method() === "GET");
    await target.getByRole("button", { name: "Refresh reviews", exact: true }).click();
    expect((await readback).ok(), "Real persisted readback succeeds.");
    await target.waitForFunction(() => document.querySelector("#review-source")?.matches(":disabled") === false);
    expect(await target.getByLabel("Will it renew?", { exact: true }).inputValue() === "No", "Confirmation survives a real readback.");

    const evidencePattern = "**/bff/subscription-reviews/*/evidence";
    const failSave = async route => {
      attemptedWrites++;
      await new Promise(resolve => { heldSave = resolve; });
      await route.fulfill({ status: 503, json: {} });
    };
    await target.route(evidencePattern, failSave);
    try {
      await target.getByLabel("I reviewed these details for this exact account, plan and billing provider.", { exact: true }).check();
      await target.getByRole("button", { name: "Save my confirmation", exact: true }).click();
      await target.waitForFunction(() => document.querySelector("#review-source")?.matches(":disabled") === true);
      expect(await target.getByRole("button", { name: "Save my confirmation", exact: true }).isDisabled(),
        "Held save disables repeated submission.");
      const deadline = Date.now() + 10000;
      while (!heldSave && Date.now() < deadline) await new Promise(r => setTimeout(r, 20));
      expect(!!heldSave, "Held synthetic request reached handler.");
      heldSave();
      await target.getByRole("button", { name: "Check saved result", exact: true }).waitFor();
      expect(await target.getByRole("button", { name: "Save my confirmation", exact: true }).isDisabled(),
        "Uncertain outcome blocks another write.");
      await new Promise(r => setTimeout(r, 250));
      expect(attemptedWrites === 1, "No automatic write replay.");
      await target.getByRole("button", { name: "Check saved result", exact: true }).click();
      await target.waitForFunction(() => document.querySelector("#review-source")?.matches(":disabled") === false);
      expect(await target.getByLabel("Will it renew?", { exact: true }).inputValue() === "No", "Readback preserves persisted confirmation.");
    } finally { heldSave?.(); await target.unroute(evidencePattern, failSave); }

    const detailPattern = "**/bff/subscription-reviews/" + initial.id;
    const stale = structuredClone(saved);
    stale.needsReview = true;
    const staleRenewal = stale.facts.find(f => f.field === "Renewal");
    staleRenewal.state = "Stale"; staleRenewal.value = null;
    const staleHandler = route => route.fulfill({ json: stale });
    await target.route(detailPattern, staleHandler);
    try {
      await target.getByRole("button", { name: "Refresh reviews", exact: true }).click();
      await target.getByText("Unknown · Stale evidence", { exact: true }).waitFor();
      expect(await target.getByText("Needs review: renewal is unknown, evidence is stale or the details disagree.", { exact: true }).isVisible(),
        "Stale evidence requires review rather than false current status.");
    } finally { await target.unroute(detailPattern, staleHandler); }

    for (const language of ["en-US", "es"]) {
      await target.context().addCookies([{ name: ".AspNetCore.Culture",
        value: encodeURIComponent("c=en-US|uic=" + language), url: origin, secure: true, sameSite: "Lax" }]);
      await target.goto(origin + "/app/subscription-checks");
      const selected = target.getByRole("button", { name: new RegExp("Synthetic subscription " + engine) });
      await selected.click();
      await target.locator("#review-source").waitFor();
      await target.locator(".review-provider-help > summary").click();
      expect(await target.getByRole("heading", { name: language === "es" ? "Confirmar datos de la suscripción" :
        "Confirm subscription details", exact: true }).isVisible(), "Localized confirmation is visible.");
      for (const width of [320, 390]) {
        await target.setViewportSize({ width, height: 844 });
        await target.evaluate(() => { document.documentElement.style.fontSize = "200%"; });
        const geometry = await target.evaluate(() => ({
          viewport: document.documentElement.clientWidth, documentWidth: document.documentElement.scrollWidth,
          bodyWidth: document.body.scrollWidth
        }));
        expect(geometry.documentWidth <= geometry.viewport + 1 && geometry.bodyWidth <= geometry.viewport + 1,
          "No horizontal overflow at " + width + "px, 200% text, " + language);
        await target.evaluate(() => { document.documentElement.style.fontSize = ""; });
        await target.screenshot({ path: path.join(outputDir,
          "subscription-verification-" + engine + "-" + language + "-" + width + ".png"), fullPage: true });
      }
    }
    console.log("Subscription verification browser: " + checks + " assertions passed; " + engine +
      "; real disposable BFF/database plus intercepted synthetic failure/staleness.");
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
  await page.context().addCookies([{ name: ".AspNetCore.Culture",
    value: encodeURIComponent("c=en-US|uic=en-US"), url: origin, secure: true, sameSite: "Lax" }]);
}
