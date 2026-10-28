import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { runInNewContext } from "node:vm";

// Execute the shipped module with synthetic HTTP/DOM. No actual account,
// password change, session revocation or authenticator is used.
const source = readFileSync(new URL("../FullWorth.Web/wwwroot/js/account-security.js", import.meta.url), "utf8");
const security = { displayName: "Synthetic", email: "synthetic@example.test", emailConfirmed: true,
    twoFactorEnabled: true, hasAuthenticatorKey: true, recoveryCodesLeft: 3 };
const json = (body, status = 200, type = "Application/JSON; charset=utf-8") =>
    new Response(JSON.stringify(body), { status, headers: { "Content-Type": type } });
const actions = [
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
async function harness(settings = {}) {
    const calls = [], navigation = [];
    const context = {
        Response, console,
        HTMLAnchorElement: class {},
        document: { querySelector: () => null, querySelectorAll: () => [] },
        window: { location: { assign: target => navigation.push(target), replace: target => navigation.push(target) } },
        fetch: async (url, options = {}) => {
            calls.push({ url, ...options });
            if (url === "/bff/antiforgery") return settings.tokenReply ? settings.tokenReply() : json({ requestToken: "synthetic-csrf" });
            if (!options.method) return settings.readReply ? settings.readReply(url) :
                json(url === "/bff/account/external" ? { linkedProviders: [] } : security);
            return settings.writeReply ? settings.writeReply(url) : new Response(null, { status: 204 });
        }
    };
    runInNewContext(source.replace(/\bexport /g, "") + "\nglobalThis.api = {" +
        [...actions.map(([name]) => name), "getAccountSecurity", "getExternalIdentityStatus"].join(",") + "};", context);
    await new Promise(resolve => setImmediate(resolve)); // linked-provider bootstrap is read only
    calls.length = 0; navigation.length = 0;
    return { api: context.api, calls, navigation };
}
const writes = result => result.calls.filter(call => call.method === "POST");
for (const [name, args, payload] of actions) {
    test(name + " accepts only its documented completion", async () => {
        const run = await harness({ writeReply: () => payload === null ? new Response(null, { status: 204 }) : json(payload) });
        const result = await run.api[name](...args);
        if (name === "resendVerificationEmail") assert.equal(result, true);
        else if (payload !== null) assert.deepEqual(JSON.parse(JSON.stringify(result)), payload);
        assert.equal(writes(run).length, 1);
        assert.ok(run.calls.every(call => call.redirect === "error" && call.credentials === "same-origin" && call.cache === "no-store"));
        assert.equal(writes(run)[0].headers["X-CSRF-TOKEN"], "synthetic-csrf");
        assert.deepEqual(run.navigation, ["changePassword", "revokeAllSessions"].includes(name) ? ["/login"] : []);
    });
    for (const [label, reply] of [
        ["HTML 200", () => new Response("<html>PRIVATE-CANARY</html>", { headers: { "Content-Type": "text/html" } })],
        ["uncommitted 202", () => json({ message: "PRIVATE-CANARY" }, 202)],
        ["JSON null", () => json(null)],
        ["malformed JSON", () => new Response("{PRIVATE-CANARY", { headers: { "Content-Type": "application/json" } })],
        ["misleading JSON prefix", () => json(payload, 200, "application/jsonp")],
        ["redirected completion", () => ({ status: payload === null ? 204 : 200, ok: true, redirected: true })]
    ]) test(name + " rejects " + label + " without success/navigation/replay", async () => {
        const run = await harness({ writeReply: reply });
        await assert.rejects(() => run.api[name](...args), error =>
            /could not confirm this account update/.test(error.message) && !error.message.includes("PRIVATE"));
        assert.equal(writes(run).length, 1);
        assert.deepEqual(run.navigation, []);
    });
    test(name + " does not accept another operation's success status or body", async () => {
        const run = await harness({ writeReply: () => payload === null ? json({ accepted: true }) :
            name === "resendVerificationEmail" ? json({}) : new Response(null, { status: 204 }) });
        await assert.rejects(() => run.api[name](...args), /could not confirm this account update/);
        assert.equal(writes(run).length, 1);
        assert.deepEqual(run.navigation, []);
    });
}
for (const token of [null, {}, 42, "", "   ", "header\r\ninjection", "embedded space", "é", "a".repeat(4097)]) {
    test("Reject unsupported token " + JSON.stringify(token).slice(0, 45) + " before POST", async () => {
        const run = await harness({ tokenReply: () => json({ requestToken: token }) });
        await assert.rejects(() => run.api.requestEmailChange("synthetic", "synthetic@example.test", null),
            /could not initialize a secure account update/);
        assert.equal(writes(run).length, 0);
        assert.deepEqual(run.navigation, []);
    });
}
for (const [label, reply] of [
    ["204 token", () => new Response(null, { status: 204 })],
    ["HTML token", () => new Response("<html>PRIVATE</html>", { headers: { "Content-Type": "text/html" } })],
    ["redirected token", () => ({ status: 200, redirected: true, headers: new Headers({ "Content-Type": "application/json" }) })],
    ["malformed token", () => new Response("{PRIVATE", { headers: { "Content-Type": "application/json" } })]
]) test(label + " never authorizes a POST", async () => {
    const run = await harness({ tokenReply: reply });
    await assert.rejects(() => run.api.disableTwoFactor("synthetic", "123456"), /could not initialize a secure account update/);
    assert.equal(writes(run).length, 0);
});
for (const name of ["getAccountSecurity", "getExternalIdentityStatus"]) {
    for (const [label, reply] of [
        ["wrong media", () => json({}, 200, "text/plain")],
        ["null", () => json(null)],
        ["array", () => json([])],
        ["missing fields", () => json({})],
        ["accepted", () => json({}, 202)],
        ["redirected", () => ({ status: 200, redirected: true })]
    ]) test(name + " preserves unknown state for " + label, async () => {
        const run = await harness({ readReply: reply });
        await assert.rejects(() => run.api[name](), /FullWorth could not load/);
        assert.equal(writes(run).length, 0);
        assert.deepEqual(run.navigation, []);
    });
    test(name + " redirects only explicit read 401", async () => {
        const run = await harness({ readReply: () => new Response(null, { status: 401 }) });
        await assert.rejects(() => run.api[name](), /Session expired/);
        assert.deepEqual(run.navigation, ["/login"]);
    });
}
test("Credential rejection preserves the session and mapped safe error", async () => {
    const run = await harness({ writeReply: () => json({ title: "The current password is incorrect.", detail: "PRIVATE" }, 401) });
    await assert.rejects(() => run.api.changePassword("synthetic", "new", null), /Your current password is incorrect/);
    assert.equal(writes(run).length, 1);
    assert.deepEqual(run.navigation, []);
});
test("A dropped write is unknown, with a fresh token only for a later explicit attempt", async () => {
    let tokens = 0;
    const run = await harness({ tokenReply: () => json({ requestToken: "synthetic-" + ++tokens }),
        writeReply: () => { throw new Error("PRIVATE-NETWORK"); } });
    for (let attempt = 1; attempt <= 2; attempt++) {
        await assert.rejects(() => run.api.regenerateRecoveryCodes("synthetic", "123456"),
            error => /Reload settings/.test(error.message) && !error.message.includes("PRIVATE"));
        assert.equal(writes(run).length, attempt);
        assert.equal(tokens, attempt);
    }
    assert.deepEqual(run.navigation, []);
});
test("Email verification alone allows its documented no-content response", async () => {
    const run = await harness();
    assert.equal(await run.api.resendVerificationEmail(), false);
    assert.equal(writes(run).length, 1);
});
test("Confirmed external write followed by failed status read remains safe", async () => {
    const run = await harness({ readReply: () => new Response(null, { status: 503 }) });
    await assert.rejects(() => run.api.unlinkExternalIdentity("google", "synthetic", null, null),
        /Reload settings/);
    assert.equal(writes(run).length, 1);
    assert.deepEqual(run.navigation, []);
});
