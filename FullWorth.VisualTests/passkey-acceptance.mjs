import { chromium } from "playwright";
import { createHmac } from "node:crypto";
import assert from "node:assert/strict";
import { mkdir } from "node:fs/promises";
import path from "node:path";

// Virtual browser coverage is NOT physical iPhone/Face ID acceptance.
const baseURL = process.env.FULLWORTH_VISUAL_BASE_URL;
assert.equal(process.env.GITHUB_ACTIONS, "true", "Run only in isolated GitHub CI.");
assert.equal(baseURL, "https://web.localhost", "Never enroll test keys on a public host.");
const email = "ci-passkey-" + process.env.GITHUB_RUN_ID + "@fullworth.local";
const password = "FullWorth!PasskeyCi123";
const outputDir = path.resolve("FullWorth.VisualTests/artifacts");
await mkdir(outputDir, { recursive: true });
function code(secret) {
  const alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
  const bytes = [];
  let buffer = 0, bits = 0;
  for (const character of secret.replace(/ /g, "")) {
    buffer = (buffer << 5) | alphabet.indexOf(character);
    bits += 5;
    if (bits >= 8) { bits -= 8; bytes.push((buffer >> bits) & 255); }
  }
  const counter = Buffer.alloc(8);
  counter.writeBigInt64BE(BigInt(Math.floor(Date.now() / 30000)));
  const hash = createHmac("sha1", Buffer.from(bytes)).update(counter).digest();
  const offset = hash[hash.length - 1] & 15;
  return String((hash.readUInt32BE(offset) & 0x7fffffff) % 1000000).padStart(6, "0");
}
const browser = await chromium.launch({ args: ["--ignore-certificate-errors"] });
try {
  const context = await browser.newContext({
    baseURL, ignoreHTTPSErrors: true, viewport: { width: 390, height: 844 }
  });
  const apiPost = async (route, body, token) => {
    const response = await context.request.post("https://localhost" + route, {
      data: body, headers: token ? { Authorization: "Bearer " + token } : {}
    });
    assert.ok(response.ok(), "Isolated fixture failed at " + route + ": " + response.status());
    return response;
  };
  await apiPost("/api/auth/register", {
    email, password, acceptedTermsAndPrivacy: true, legalTermsVersion: "2026-09-04-beta"
  });
  const page = await context.newPage();
  page.setDefaultTimeout(15000);
  const cdp = await context.newCDPSession(page);
  await cdp.send("WebAuthn.enable");
  const { authenticatorId } = await cdp.send("WebAuthn.addVirtualAuthenticator", {
    options: { protocol: "ctap2", transport: "internal", hasResidentKey: true,
      hasUserVerification: true, isUserVerified: true, automaticPresenceSimulation: true }
  });
  async function passwordStep() {
    await page.goto("/login");
    await page.locator('input[name="email"]').fill(email);
    await page.locator('input[name="password"]').fill(password);
    await page.locator('input[name="rememberMe"]').check();
    await page.locator('.auth-form button[type="submit"]').click();
  }
  await passwordStep();
  await page.waitForURL(url => url.pathname === "/app");
  await page.goto("/app/account/security-keys");
  const management = page.locator('[data-passkeys="manage"]');
  await management.locator('input[name="currentPassword"]').fill(password);
  await management.locator('input[name="keyName"]').fill("CI virtual platform passkey");
  await management.locator('[data-passkey-action="register"]').click();
  await management.locator("[data-passkey-list]").getByText("CI virtual platform passkey", { exact: false }).waitFor();
  assert.equal((await cdp.send("WebAuthn.getCredentials", { authenticatorId })).credentials.length, 1);
  const bootstrap = await (await apiPost("/api/auth/login", { email, password })).json();
  const setup = await (await apiPost("/api/account/security/two-factor/setup",
    { currentPassword: password }, bootstrap.accessToken)).json();
  await apiPost("/api/account/security/two-factor/enable",
    { currentPassword: password, authenticatorCode: code(setup.sharedKey) }, bootstrap.accessToken);
  await context.clearCookies();
  await passwordStep();
  await page.waitForURL(url => url.searchParams.get("twoFactor") === "true");
  await page.locator("[data-password-passkey-button]").waitFor();
  assert.equal(await page.locator('input[name="password"], input[name="email"]').count(), 0);
  await page.screenshot({ path: path.join(outputDir, "password-once-passkey-choice-mobile.png"), fullPage: true });

  // Simulated user cancellation, without replacing signature validation.
  await page.evaluate(() => {
    const original = navigator.credentials.get.bind(navigator.credentials);
    navigator.credentials.get = async (...args) => {
      navigator.credentials.get = original;
      throw new DOMException("CI simulated cancellation", "NotAllowedError");
    };
  });
  await page.locator("[data-password-passkey-button]").click();
  await page.getByText("Cancelled or timed out. You can try again or use an authenticator code.", { exact: true }).waitFor();
  assert.equal(await page.locator('input[name="password"], input[name="email"]').count(), 0);
  await page.locator('input[name="twoFactorCode"]').fill(code(setup.sharedKey));
  await page.locator('form[action="/auth/login"]').first().getByRole("button", { name: "Verify and sign in" }).click();
  await page.waitForURL(url => url.pathname === "/app");
  await context.clearCookies();
  await passwordStep();
  await page.waitForURL(url => url.searchParams.get("twoFactor") === "true");
  const completionRequest = page.waitForRequest(request =>
    request.url().endsWith("/auth/login/passkey-complete") && request.method() === "POST");
  await page.locator("[data-password-passkey-button]").click();
  const completion = (await completionRequest).postDataJSON();
  assert.deepEqual(Object.keys(completion).sort(), ["ceremonyId", "credentialJson"]);
  await page.waitForURL(url => url.pathname === "/app");
  const credentials = (await cdp.send("WebAuthn.getCredentials", { authenticatorId })).credentials;
  assert.ok(credentials[0].signCount > 0, "Browser must produce a signed assertion.");
  assert.equal((await context.cookies()).filter(cookie =>
    cookie.name === "__Host-FullWorth.PendingPassword").length, 0);
  const account = await context.request.get(baseURL + "/bff/account/security/passkeys");
  assert.equal(account.status(), 200, "Browser must have an authenticated session.");
  await cdp.send("WebAuthn.removeVirtualAuthenticator", { authenticatorId });
  await context.close();
  console.log("PASS: virtual passkey enrollment, password-once sign-in, cancellation/code fallback.");
} finally { await browser.close(); }
