import assert from "node:assert/strict";

// Read-only controlled reproduction on the disposable CI host. No login POST,
// credentials, tokens, response bodies or raw URLs are recorded.
export async function verifyLoginHydration(browser, baseURL) {
  assert.equal(process.env.GITHUB_ACTIONS, "true");
  assert.equal(baseURL, "https://web.localhost");
  const context = await browser.newContext({
    baseURL, ignoreHTTPSErrors: true, viewport: { width: 390, height: 844 }
  });
  const page = await context.newPage();
  page.setDefaultTimeout(15000);
  let release, requested;
  const gate = new Promise(resolve => { release = resolve; });
  const held = new Promise(resolve => { requested = resolve; });
  const sample = { email: "hydration-probe@fullworth.local", password: "Synthetic!Hydration123" };
  let submissions = 0;
  page.on("request", request => {
    if (new URL(request.url()).pathname === "/auth/login" && request.method() === "POST")
      submissions++;
  });
  await page.route(/\/_framework\/blazor\.web[^/]*\.js(?:\?.*)?$/, async route => {
    requested();
    await gate;
    await route.continue();
  });
  const observe = () => page.evaluate(sample => {
    const form = document.querySelector('form[action="/auth/login"]');
    return {
      formPresent: !!form,
      emailMatches: form?.querySelector('[name="email"]')?.value === sample.email,
      passwordMatches: form?.querySelector('[name="password"]')?.value === sample.password,
      rememberMe: form?.querySelector('[name="rememberMe"]')?.checked === true,
      formValid: form?.checkValidity() === true
    };
  }, sample);
  try {
    // Stop the actual framework script rather than relying on an arbitrary
    // sleep to land between prerender and the interactive server render.
    await page.goto("/login", { waitUntil: "commit" });
    await page.locator('form[action="/auth/login"] input[name="email"]').waitFor();
    await Promise.race([
      held,
      page.waitForTimeout(15000).then(() => { throw new Error("Framework hold was not reached."); })
    ]);
    assert.equal(await page.locator(".design-picker select").isDisabled(), true,
      "The controlled sample must precede interactive hydration.");
    await page.locator('input[name="email"]').fill(sample.email);
    await page.locator('input[name="password"]').fill(sample.password);
    await page.locator('input[name="rememberMe"]').check();
    const before = await observe();
    assert.ok(Object.values(before).every(Boolean), "Prerendered native controls must accept valid input.");
    release();
    await page.locator(".design-picker select:not([disabled])").waitFor();
    const after = await observe();
    console.log("Controlled login hydration: " + JSON.stringify({ before, after, submissions }));
    assert.equal(submissions, 0, "Hydration observation must not submit credentials.");
    assert.ok(Object.values(after).every(Boolean),
      "Interactive hydration must preserve the email, password and remember-me choice entered before startup.");
  } finally {
    release();
    await context.close();
  }
}
