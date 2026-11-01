import assert from "node:assert/strict";
import { chromium, webkit } from "playwright";

const baseURL = process.env.FULLWORTH_VISUAL_BASE_URL;
assert.equal(process.env.GITHUB_ACTIONS, "true", "Only disposable GitHub CI is allowed.");
assert.equal(baseURL, "https://web.localhost", "Never probe a public authentication host.");
const sampleEmail = "native-startup-probe@fullworth.local";
const samplePassword = "Synthetic!Startup123";
const scenarios = [
  { name: "registration", path: "/register", action: "/auth/register",
    fields: { email: sampleEmail, password: samplePassword, confirmPassword: samplePassword,
      acceptedTermsAndPrivacy: true } },
  { name: "recovery-request", path: "/forgot-password", action: "/auth/forgot-password",
    fields: { email: sampleEmail } },
  { name: "password-reset", path: "/reset-password?email=" + encodeURIComponent(sampleEmail) +
      "&code=synthetic-invalid-reset-proof", action: "/auth/reset-password",
    fields: { newPassword: samplePassword, confirmPassword: samplePassword } }
];
const results = [];
for (const engine of ["chromium", "webkit"]) {
  const browser = await ({ chromium, webkit })[engine].launch(
    engine === "chromium" ? { args: ["--ignore-certificate-errors"] } : {});
  try {
    for (const scenario of scenarios) {
      const context = await browser.newContext({ baseURL, ignoreHTTPSErrors: true,
        viewport: { width: 390, height: 844 } });
      const page = await context.newPage();
      page.setDefaultTimeout(15000);
      let release, reached;
      const held = new Promise(resolve => { reached = resolve; });
      const gate = new Promise(resolve => { release = resolve; });
      let submissions = 0;
      page.on("request", request => {
        if (request.method() === "POST" && new URL(request.url()).pathname === scenario.action)
          submissions++;
      });
      await page.route(/\/_framework\/blazor\.web[^/]*\.js(?:\?.*)?$/, async route => {
        reached(); await gate; await route.continue();
      });
      const selector = 'form.auth-form[action="' + scenario.action + '"]';
      async function state() {
        return page.locator(selector).evaluate((form, fields) => ({
          valuesMatch: Object.entries(fields).every(([name, expected]) => {
            const input = form.elements.namedItem(name);
            return input instanceof HTMLInputElement && (typeof expected === "boolean"
              ? input.type === "checkbox" && input.checked === expected : input.value === expected);
          }),
          nativeValid: form.checkValidity(),
          antiforgeryPresent: !!form.elements.namedItem("__RequestVerificationToken")?.value
        }), scenario.fields);
      }
      try {
        await page.goto(scenario.path, { waitUntil: "commit" });
        await page.locator(selector).waitFor();
        await Promise.race([held, page.waitForTimeout(15000).then(() => {
          throw new Error("Framework hold was not reached.");
        })]);
        assert.equal(await page.locator(".design-picker select").isDisabled(), true);
        for (const [name, value] of Object.entries(scenario.fields)) {
          const field = page.locator(selector).locator('[name="' + name + '"]');
          if (typeof value === "boolean") await field.setChecked(value);
          else await field.fill(value);
        }
        const before = await state();
        assert.ok(Object.values(before).every(Boolean), "Pre-start native fields must be valid.");
        release();
        await page.locator(".design-picker select:not([disabled])").waitFor();
        const after = await state();
        assert.equal(submissions, 0, "This reproduction must never register, send mail or reset a password.");
        // Fixed categories/booleans only; never log entered values, reset proof,
        // antiforgery tokens, arbitrary bodies, query strings or raw URLs.
        results.push({ engine, scenario: scenario.name, before, after, submissions });
        console.log("Native auth startup: " + JSON.stringify(results.at(-1)));
      } finally {
        release(); await context.close();
      }
    }
  } finally { await browser.close(); }
}
assert.equal(results.length, 6);
assert.ok(results.every(result => Object.values(result.after).every(Boolean)),
  "Every native authentication form must preserve entered fields and consent through startup.");
console.log("PASS: six controlled native-auth startup observations; no submissions or delivery.");
