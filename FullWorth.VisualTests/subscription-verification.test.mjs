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
    try {
    await target.context().addCookies([{ name: ".AspNetCore.Culture",
      value: encodeURIComponent("c=en-US|uic=en-US"), url: origin, secure: true, sameSite: "Lax" }]);
    await target.goto(origin + "/app/subscription-checks");
    await target.locator('.subscription-reviews[aria-busy="false"]').waitFor();
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
    expect(createdResponse.ok(), "Real BFF create must succeed (HTTP " + createdResponse.status() + ").");
    const initial = await createdResponse.json();
    expect(initial.merchantVerification === "Unavailable" && initial.needsReview === true,
      "A new review cannot imply merchant verification or recurrence.");
    expect(initial.facts.every(f => f.value === null), "Initial details remain unknown.");
    await target.getByRole("heading", { name: "Confirm subscription details", exact: true }).waitFor();
    const observedInput = target.getByLabel("Evidence date", { exact: true });
    const untilInput = target.getByLabel("Review again by", { exact: true });
    const originalObserved = await observedInput.inputValue();
    const originalUntil = await untilInput.inputValue();
    await observedInput.fill("9999-12-01");
    await untilInput.fill("9999-12-31");
    await target.getByLabel("I reviewed these details for this exact account, plan and billing provider.", { exact: true }).check();
    await target.getByRole("button", { name: "Save my confirmation", exact: true }).click();
    await target.getByText("Review the account details, evidence source and dates before saving.", { exact: true }).waitFor();
    expect(await target.getByRole("button", { name: "Save my confirmation", exact: true }).isEnabled(),
      "Extreme future dates are rejected without breaking the review page.");
    await observedInput.fill(originalObserved);
    await untilInput.fill(originalUntil);
    await target.getByLabel("Service access", { exact: true }).selectOption("Active");
    await target.getByLabel("Will it renew?", { exact: true }).selectOption("No");
    await target.getByLabel("Billing arrangement", { exact: true }).selectOption("Prepaid");
    await target.getByLabel("I reviewed these details for this exact account, plan and billing provider.", { exact: true }).check();
    const saveResponse = target.waitForResponse(r => r.url().includes("/bff/subscription-reviews/") &&
      r.url().endsWith("/evidence") && r.request().method() === "POST");
    await target.getByRole("button", { name: "Save my confirmation", exact: true }).click();
    const savedResponse = await saveResponse;
    expect(savedResponse.ok(), "Real BFF save must succeed (HTTP " + savedResponse.status() + ").");
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
    // Explicit synthetic read: twelve newly uploaded older observations must
    // not conceal the newer dated evidence that determines the saved result.
    const crowded = structuredClone(saved);
    const renewal = crowded.facts.find(f => f.field === "Renewal");
    const latest = structuredClone(renewal.evidence[0]);
    const olderUploads = Array.from({ length: 12 }, (_, index) => {
      const older = structuredClone(latest);
      older.submissionId = "00000000-0000-0000-0000-" + String(index + 1).padStart(12, "0");
      older.recordedAtUtc = new Date(Date.parse(latest.recordedAtUtc) + (index + 1) * 60000).toISOString();
      older.fact.value = "Yes";
      older.fact.observedAtUtc = new Date(Date.parse(latest.fact.observedAtUtc) - (index + 1) * 86400000).toISOString();
      return older;
    }).reverse();
    renewal.evidence = [...olderUploads, latest];
    const crowdedRead = route => route.fulfill({ json: crowded });
    await target.route(detailPattern, crowdedRead);
    try {
      await target.getByRole("button", { name: "Refresh reviews", exact: true }).click();
      const renewalPanel = target.locator(".review-facts > div").filter({
        has: target.locator("dt").filter({ hasText: "Will it renew?" })
      });
      await renewalPanel.locator("details > summary").filter({ hasText: "(13)" }).waitFor();
      await renewalPanel.locator(".review-latest-evidence").getByText("Confirmed by you: No.", { exact: false }).waitFor();
      expect(await renewalPanel.locator(".review-latest-evidence p").count() === 1,
        "The latest source observation remains visible outside capped history.");
      await renewalPanel.locator("details > summary").click();
      expect(await renewalPanel.locator("details p").filter({ hasText: "Confirmed by you: Yes." }).count() === 10,
        "History still has a bounded ten-entry list.");
      expect(await renewalPanel.locator("details").getByText("Confirmed by you: No.", { exact: false }).count() === 0,
        "Winning evidence remains visible even when absent from the ten newest uploads.");
    } finally { await target.unroute(detailPattern, crowdedRead); }
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
    await target.locator('.subscription-reviews[aria-busy="false"]').waitFor();
      const selected = target.getByRole("button", { name: new RegExp("Synthetic subscription " + engine) });
      await selected.click();
      await target.locator("#review-source").waitFor();
      await target.locator(".review-provider-help > summary").click();
      expect(await target.getByRole("heading", { name: language === "es" ? "Confirmar datos de la suscripción" :
        "Confirm subscription details", exact: true }).isVisible(), "Localized confirmation is visible.");
      expect(await target.getByText(language === "es" ? "Observación más reciente de cada fuente" :
        "Latest observation from each source", { exact: true }).count() > 0,
        "Latest evidence provenance is localized and visible.");
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
    // The real server commits creation, but the browser sees an ambiguous response.
    // Explicit readback must recover that exact review and release the ID for a new one.
    await target.context().addCookies([{ name: ".AspNetCore.Culture",
      value: encodeURIComponent("c=en-US|uic=en-US"), url: origin, secure: true, sameSite: "Lax" }]);
    await target.goto(origin + "/app/subscription-checks");
    await target.locator('.subscription-reviews[aria-busy="false"]').waitFor();
    await target.getByLabel("Merchant", { exact: true }).fill("Ambiguous creation " + engine);
    await target.getByLabel("Account and plan label", { exact: true }).fill("Recovery fixture");
    await target.getByLabel("Billed by", { exact: true }).fill("Synthetic direct billing");
    let creationWrites = 0, committedId, creationStatus;
    const createPattern = "**/bff/subscription-reviews";
    const lostCreateAcknowledgement = async route => {
      if (route.request().method() !== "POST") return route.continue();
      creationWrites++;
      const upstream = await route.fetch();
      creationStatus = upstream.status();
      if (upstream.ok()) committedId = (await upstream.json()).id;
      await route.fulfill({ status: 503, json: {} });
    };
    await target.route(createPattern, lostCreateAcknowledgement);
    try {
      await target.getByRole("button", { name: "Start review", exact: true }).click();
      await target.getByText("The review could not be confirmed. Refresh the list before retrying. Repeating this request uses the same review ID.", { exact: true }).waitFor();
      expect(creationStatus >= 200 && creationStatus < 300 && !!committedId,
        "Real disposable create commits before acknowledgement loss (HTTP " + creationStatus + ").");
      expect(creationWrites === 1, "Ambiguous create is not automatically replayed.");
    } finally {
      await target.unroute(createPattern, lostCreateAcknowledgement);
    }
    const recovered = target.waitForResponse(r => new URL(r.url()).pathname ===
      "/bff/subscription-reviews/" + committedId && r.request().method() === "GET");
    await target.getByRole("button", { name: "Refresh reviews", exact: true }).click();
    expect((await recovered).ok(), "Explicit list/detail readback recovers the committed review.");
    await target.getByRole("heading", { name: "Ambiguous creation " + engine + " · Recovery fixture", exact: true }).waitFor();
    await target.waitForFunction(() => document.querySelector("#review-source")?.matches(":disabled") === false);
    await target.getByLabel("Merchant", { exact: true }).fill("Fresh after recovery " + engine);
    const nextCreated = target.waitForResponse(r => new URL(r.url()).pathname ===
      "/bff/subscription-reviews" && r.request().method() === "POST");
    await target.getByRole("button", { name: "Start review", exact: true }).click();
    const nextResponse = await nextCreated;
    expect(nextResponse.ok(), "A new review is possible after confirmed recovery.");
    expect((await nextResponse.json()).id !== committedId, "A new review uses a new ID after readback.");
    await target.getByText("Review created. Renewal remains unverified until supporting details are recorded.", { exact: true }).waitFor();
    const retainedReviewCount = await target.locator(".review-list-item").count();
    expect(retainedReviewCount > 0, "Recovery fixture has persisted reviews to preserve.");
    const malformedListPattern = "**/bff/subscription-reviews";
    for (const malformed of [{}, { reviews: null }, { reviews: [null], hasMore: false }]) {
      const malformedRead = route => route.request().method() === "GET"
        ? route.fulfill({ json: malformed }) : route.continue();
      await target.route(malformedListPattern, malformedRead);
      try {
        const response = target.waitForResponse(r => new URL(r.url()).pathname ===
          "/bff/subscription-reviews" && r.request().method() === "GET");
        await target.getByRole("button", { name: "Refresh reviews", exact: true }).click();
        await response;
        await target.getByText("Reviews could not be refreshed. Previously loaded details may be out of date.", { exact: true }).waitFor();
        await target.locator('.subscription-reviews[aria-busy="false"]').waitFor();
        expect(await target.locator(".review-list-item").count() === retainedReviewCount,
          "Malformed list responses preserve the last validated review list.");
      } finally { await target.unroute(malformedListPattern, malformedRead); }
    }
    const unavailableInitialList = route => route.request().method() === "GET"
      ? route.fulfill({ json: {} }) : route.continue();
    await target.route(malformedListPattern, unavailableInitialList);
    try {
      await target.goto(origin + "/app/subscription-checks");
      await target.locator('.subscription-reviews[aria-busy="false"]').waitFor();
      await target.getByText("Reviews could not be refreshed. Previously loaded details may be out of date.", { exact: true }).waitFor();
      expect(await target.getByText("No subscription reviews yet. You can start with just one posted payment.", { exact: true }).count() === 0,
        "An unavailable initial read must not claim the owner has no reviews.");
    } finally { await target.unroute(malformedListPattern, unavailableInitialList); }
    await target.getByRole("button", { name: "Refresh reviews", exact: true }).click();
    await target.locator(".review-list-item").first().waitFor();
    expect(await target.locator(".review-list-item").count() === retainedReviewCount,
      "A valid retry restores the real list without a write.");
    console.log("Subscription verification browser: " + checks + " assertions passed; " + engine +
      "; real disposable BFF/database plus intercepted synthetic failure/staleness.");
    } catch (error) {
      await target.screenshot({ path: path.join(outputDir, "subscription-verification-failure-" + engine + ".png"), fullPage: true });
      console.error("Subscription fixture failure state", await target.evaluate(() => ({
        language: document.documentElement.lang,
        busy: document.querySelector(".subscription-reviews")?.getAttribute("aria-busy"),
        helpOpen: document.querySelector(".review-provider-help")?.open,
        helpLabels: Array.from(document.querySelectorAll(".review-provider-help a")).map(a => a.textContent?.trim())
      })));
      throw error;
    }
  };
  const prepareIndependentOwner = async (target, engine) => {
    // Preserve production quotas: each engine exercises its own disposable owner.
    const runId = process.env.GITHUB_RUN_ID ?? String(Date.now());
    const email = "ci-subscription-" + runId + "-" + engine + "@fullworth.local";
    const password = "FullWorth!Evidence123";
    let phase = "registration", registrationStatus, loginStatus;
    const recordAuthStatus = response => {
      if (response.request().method() !== "POST") return;
      const pathname = new URL(response.url()).pathname;
      if (pathname === "/auth/register") registrationStatus = response.status();
      if (pathname === "/auth/login") loginStatus = response.status();
    };
    target.on("response", recordAuthStatus);
    try {
      await target.context().addCookies([{ name: ".AspNetCore.Culture",
        value: encodeURIComponent("c=en-US|uic=en-US"), url: origin, secure: true, sameSite: "Lax" }]);
      await target.goto(origin + "/register", { waitUntil: "domcontentloaded" });
      await target.locator(".auth-form").waitFor();
      // Match the parent auth fixture's settling interval before filling native
      // forms: InteractiveServer hydration can otherwise replace their inputs.
      await target.waitForTimeout(900);
      await target.locator('input[name="email"]').fill(email);
      await target.locator('input[name="password"]').fill(password);
      await target.locator('input[name="confirmPassword"]').fill(password);
      await target.locator('input[name="acceptedTermsAndPrivacy"]').check();
      await Promise.all([
        target.waitForURL(url => url.pathname === "/login" &&
          url.searchParams.get("message") === "Registration request received. Sign in to continue.",
          { waitUntil: "domcontentloaded" }),
        target.locator('button[type="submit"]').click()
      ]);
      assert.equal(registrationStatus, 302, "Disposable owner registration redirects successfully.");
      phase = "login";
      await target.locator(".auth-form").waitFor();
      await target.waitForTimeout(900);
      await target.locator('input[name="email"]').fill(email);
      await target.locator('input[name="password"]').fill(password);
      await Promise.all([
        target.waitForURL(url => url.pathname.startsWith("/app"), { waitUntil: "domcontentloaded" }),
        target.locator('button[type="submit"]').click()
      ]);
      assert.equal(loginStatus, 302, "Disposable owner login redirects successfully.");
      phase = "release notes";
      await target.goto(origin + "/app/setup", { waitUntil: "domcontentloaded" });
      const releaseNotes = target.locator(".whats-new-dialog");
      await releaseNotes.waitFor({ state: "visible", timeout: 15000 });
      await releaseNotes.getByRole("button", { name: "Explore FullWorth", exact: true }).click();
      await releaseNotes.waitFor({ state: "detached", timeout: 15000 });
    } catch (error) {
      // Never capture the sign-up form: it contains a disposable password.
      // Log only public error text, pathname, status and validity flags.
      console.error("Subscription owner setup failure", {
        engine, phase, registrationStatus, loginStatus,
        state: await target.evaluate(() => ({
          pathname: location.pathname, language: document.documentElement.lang,
          error: document.querySelector(".auth-error")?.textContent?.trim(),
          fields: Array.from(document.querySelectorAll(".auth-form input"))
            .filter(input => input.type !== "hidden")
            .map(input => ({ name: input.name, valid: input.validity.valid,
              missing: input.validity.valueMissing, typeMismatch: input.validity.typeMismatch }))
        }))
      });
      throw error;
    } finally { target.off("response", recordAuthStatus); }
  };
  await run(page, "chromium");
  const browser = await webkit.launch({ headless: true });
  try {
    const context = await browser.newContext({ baseURL: origin, ignoreHTTPSErrors: true,
      serviceWorkers: "block",
      viewport: { width: 390, height: 844 } });
    try {
      const webkitPage = await context.newPage();
      await prepareIndependentOwner(webkitPage, "webkit");
      await run(webkitPage, "webkit");
    }
    finally { await context.close(); }
  } finally { await browser.close(); }
  await page.context().addCookies([{ name: ".AspNetCore.Culture",
    value: encodeURIComponent("c=en-US|uic=en-US"), url: origin, secure: true, sameSite: "Lax" }]);
}
