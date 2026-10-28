import assert from "node:assert/strict";
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { chromium, webkit } from "playwright";

// Actual shipped module, disposable loopback responses and synthetic proofs.
// No real account update, external provider or device acceptance.
const source = await readFile(new URL("../FullWorth.Web/wwwroot/js/account-security.js", import.meta.url), "utf8");
const security = { displayName: "Synthetic", email: "synthetic@example.test", emailConfirmed: true,
    twoFactorEnabled: true, hasAuthenticatorKey: true, recoveryCodesLeft: 3 };
const specs = [
    ["updateProfile", ["Synthetic"], security],
    ["changePassword", ["synthetic", "synthetic-new", null], null],
    ["revokeAllSessions", ["synthetic", null], null],
    ["requestEmailChange", ["synthetic", "synthetic@example.test", null], { message: "Synthetic sent" }],
    ["resendVerificationEmail", [], { message: "Synthetic sent" }],
    ["linkExternalIdentity", ["google", "synthetic", null, null], null],
    ["unlinkExternalIdentity", ["google", "synthetic", null, null], null],
    ["setupTwoFactor", ["synthetic", null], { sharedKey: "SYNTHETIC", otpAuthUri: "otpauth://totp/synthetic" }],
    ["enableTwoFactor", ["synthetic", "123456"], { recoveryCodes: ["synthetic-code"] }],
    ["regenerateRecoveryCodes", ["synthetic", "123456"], { recoveryCodes: ["synthetic-code"] }],
    ["disableTwoFactor", ["synthetic", "123456"], null],
    ["resetTwoFactor", ["synthetic", "123456"], { sharedKey: "SYNTHETIC", otpAuthUri: "otpauth://totp/synthetic" }]
];
const states = new Map();
const server = createServer((request, response) => {
    request.resume();
    const path = new URL(request.url, "http://localhost").pathname;
    const state = states.get(request.headers["x-synthetic-case"]);
    function json(body, status = 200) {
        response.writeHead(status, { "Content-Type": "Application/JSON; charset=utf-8" });
        response.end(JSON.stringify(body));
    }
    if (path === "/" || path === "/login") {
        response.writeHead(200, { "Content-Type": "text/html; charset=utf-8" });
        response.end("<!doctype html><html><body>Synthetic account boundary</body></html>");
    } else if (path === "/js/account-security.js") {
        response.writeHead(200, { "Content-Type": "text/javascript; charset=utf-8" });
        response.end(source);
    } else if (!state) {
        response.writeHead(404); response.end();
    } else if (path === "/redirect-target") {
        state.targets++; json({ accepted: true });
    } else if (path === "/bff/antiforgery") {
        state.tokens++;
        if (state.mode === "token-redirect") {
            response.writeHead(307, { Location: "/redirect-target" }); response.end();
        } else json({ requestToken: state.mode === "token" ? state.token : "synthetic-csrf" });
    } else if (request.method === "GET") {
        json(path === "/bff/account/external" ? { linkedProviders: [] } : security);
    } else {
        state.writes++; state.methods.push(request.method); state.csrf.push(request.headers["x-csrf-token"]);
        if (state.mode === "redirect") {
            response.writeHead(state.status, { Location: "/redirect-target" }); response.end();
        } else if (state.mode === "html") {
            response.writeHead(200, { "Content-Type": "text/html" }); response.end("<html>PRIVATE-CANARY</html>");
        } else if (state.mode === "malformed") {
            response.writeHead(200, { "Content-Type": "application/json" }); response.end("{PRIVATE-CANARY");
        } else if (state.mode === "accepted") json({ message: "Synthetic pending" }, 202);
        else if (state.mode === "proof-rejected") json({ title: "The current password is incorrect.", detail: "PRIVATE-CANARY" }, 401);
        else if (state.mode === "dropped") {
            // Drop after headers/body begin: exercise an unknown completion,
            // not the browser's independent pre-header connection retry policy.
            response.writeHead(200, { "Content-Type": "application/json", "Content-Length": "1000" });
            response.write('{"message":"');
            setImmediate(() => response.destroy());
        }
        else if (state.payload === null) { response.writeHead(204); response.end(); }
        else json(state.payload);
    }
});
await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
const origin = "http://127.0.0.1:" + server.address().port;
const engines = (process.env.FULLWORTH_TEST_BROWSERS ?? "chromium,webkit").split(",");
assert.ok(engines.length && engines.every(engine => ["chromium", "webkit"].includes(engine)));
let nextId = 0;
try {
    for (const engine of engines) {
        const browser = await ({ chromium, webkit })[engine].launch({ headless: true });
        let passed = 0;
        try {
            async function scenario(label, settings, verify) {
                const id = String(++nextId);
                const state = { writes: 0, tokens: 0, targets: 0, methods: [], csrf: [], ...settings };
                states.set(id, state);
                const context = await browser.newContext({ extraHTTPHeaders: { "X-Synthetic-Case": id } });
                try {
                    const page = await context.newPage();
                    await page.goto(origin);
                    const invoke = (method, args) => page.evaluate(async ({ method, args }) => {
                        try {
                            const api = await import("/js/account-security.js");
                            return { ok: true, result: await api[method](...args) };
                        } catch (error) { return { ok: false, message: error.message }; }
                    }, { method, args });
                    await verify({ page, state, invoke });
                    passed++;
                } catch (error) {
                    throw new Error(engine + " / " + label + ": " + error.message, { cause: error });
                } finally { await context.close(); states.delete(id); }
            }
            for (const [method, args, payload] of specs) {
                await scenario(method + " confirmed", { mode: "valid", payload }, async ({ invoke, page, state }) => {
                    const result = await invoke(method, args);
                    if (["changePassword", "revokeAllSessions"].includes(method)) await page.waitForURL(origin + "/login");
                    else assert.equal(result.ok, true);
                    assert.equal(state.writes, 1);
                    assert.deepEqual(state.methods, ["POST"]);
                    assert.deepEqual(state.csrf, ["synthetic-csrf"]);
                });
                await scenario(method + " HTML ambiguity", { mode: "html", payload }, async ({ invoke, page, state }) => {
                    const result = await invoke(method, args);
                    assert.equal(result.ok, false);
                    assert.match(result.message, /could not confirm this account update/);
                    assert.ok(!result.message.includes("PRIVATE"));
                    assert.equal(state.writes, 1);
                    assert.equal(page.url(), origin + "/");
                });
            }
            for (const status of [301, 302, 303, 307, 308]) {
                await scenario("POST redirect " + status, { mode: "redirect", status }, async ({ invoke, page, state }) => {
                    const result = await invoke("changePassword", ["synthetic", "new", null]);
                    assert.equal(result.ok, false);
                    assert.match(result.message, /Reload settings/);
                    assert.equal(state.writes, 1);
                    assert.equal(state.targets, 0, "Credentials must never be forwarded to a redirect target.");
                    assert.equal(page.url(), origin + "/");
                });
            }
            for (const token of [{}, 42, "", "   ", "line\r\nbreak", "a".repeat(4097)]) {
                await scenario("invalid token", { mode: "token", token }, async ({ invoke, state }) => {
                    assert.equal((await invoke("disableTwoFactor", ["synthetic", "123456"])).ok, false);
                    assert.equal(state.writes, 0);
                });
            }
            for (const mode of ["token-redirect", "malformed", "accepted", "dropped", "proof-rejected"]) {
                await scenario(mode, { mode }, async ({ invoke, page, state }) => {
                    const result = await invoke("requestEmailChange", ["synthetic", "synthetic@example.test", null]);
                    assert.equal(result.ok, false);
                    assert.ok(!result.message.includes("PRIVATE"));
                    if (mode === "proof-rejected") assert.match(result.message, /current password is incorrect/);
                    assert.equal(state.writes, mode === "token-redirect" ? 0 : 1);
                    assert.equal(state.targets, 0);
                    assert.equal(page.url(), origin + "/");
                });
            }
        } finally { await browser.close(); }
        console.log(engine + " account-security HTTP response boundaries passed: " + passed + " synthetic scenarios.");
    }
} finally {
    server.closeAllConnections();
    await new Promise(resolve => server.close(resolve));
}
