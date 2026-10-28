import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { runInNewContext } from "node:vm";

// Full shipped handlers with synthetic DOM/HTTP only. No real account, OS
// credential, provider or physical-device acceptance is established here.
const sourceFor = filename => readFileSync(new URL("../FullWorth.Web/wwwroot/js/" + filename, import.meta.url), "utf8");
const ceremony = { ceremonyId: "a".repeat(43), options: { challenge: "dGVzdA", user: { id: "dXNlcg" } } };
const key = { id: "synthetic-id", name: "<script>user key name</script>", createdAt: "2026-10-01T00:00:00Z" };
const json = (body, status = 200) => new Response(JSON.stringify(body), {
    status, headers: { "Content-Type": "application/json; charset=utf-8" }
});
function element(tag = "") {
    return {
        tag, children: [], dataset: {}, textContent: "", disabled: false, hidden: false, isConnected: true,
        append(...items) { this.children.push(...items); },
        replaceChildren(fragment) { this.children = fragment.children.slice(); this.textContent = ""; }
    };
}
async function run(filename, settings = {}) {
    let handler, nativeCalls = 0, submitted = 0;
    const status = element(), list = element(), retry = element();
    list.children = ["previously verified key"];
    const password = { value: "synthetic-password" };
    const root = { isConnected: true, dataset: {}, querySelector(selector) {
        if (selector.includes("status")) return status;
        if (selector.includes("list")) return list;
        if (selector.includes("retry")) return retry;
        if (selector.includes('name="currentPassword"')) return password;
        if (selector.includes('name="keyName"')) return { value: "Synthetic key" };
        return null;
    }};
    const button = { ...element(), dataset: { passkeyAction: settings.action || "login", keyId: key.id },
        closest: () => root };
    const calls = [];
    const document = {
        documentElement: { lang: settings.lang || "en-US" },
        addEventListener(type, callback) { if (type === "click") handler = callback; },
        querySelector(selector) {
            if (selector.includes("__RequestVerificationToken"))
                return settings.missingToken ? null : { value: settings.token ?? "synthetic-csrf" };
            if (selector.includes("returnUrl")) return { value: "/app" };
            return null;
        },
        querySelectorAll: () => [],
        body: { append() {} },
        createDocumentFragment: () => element("fragment"),
        createElement(tag) { const node = element(tag); node.submit = () => { submitted++; }; return node; }
    };
    const PublicKeyCredential = {
        parseRequestOptionsFromJSON: value => value,
        parseCreationOptionsFromJSON: value => value
    };
    const credential = async () => {
        nativeCalls++;
        if (settings.nativeError) throw settings.nativeError;
        return settings.noCredential ? null : { toJSON: () => ({ type: "public-key" }) };
    };
    const context = {
        document,
        window: { isSecureContext: settings.secure !== false,
            PublicKeyCredential: settings.webAuthn === false ? undefined : PublicKeyCredential },
        PublicKeyCredential,
        navigator: { credentials: settings.webAuthn === false ? undefined : { create: credential, get: credential } },
        MutationObserver: class { observe() {} }, confirm: () => settings.confirm !== false,
        fetch: async (path, options = {}) => {
            calls.push({ path, ...options });
            if (settings.fetchError) throw new Error("synthetic-secret-must-not-be-shown");
            if (path === "/bff/antiforgery") return settings.csrf ? settings.csrf() : json({ requestToken: "new-synthetic-csrf" });
            if (!options.method) return settings.listResponse ? settings.listResponse() : json([key]);
            if (/options$/.test(path)) return settings.optionsResponse ? settings.optionsResponse() : json(ceremony);
            if (settings.onWrite) settings.onWrite(root);
            return settings.writeResponse ? settings.writeResponse() : new Response(null, { status: 204 });
        },
        Uint8Array, atob, btoa, console
    };
    runInNewContext(sourceFor(filename), context, { filename });
    await handler({ target: { closest: () => button } });
    return { status: status.textContent, list, retry, password, button, calls, nativeCalls, submitted, root };
}
const files = ["passkeys.js", "password-step.js"];
for (const filename of files) {
    test(filename + " navigates only after the contracted 204 completion", async () => {
        const result = await run(filename);
        assert.equal(result.submitted, 1);
        assert.equal(result.nativeCalls, 1);
        assert.equal(result.calls.filter(c => /login$|complete$/.test(c.path)).length, 1);
        assert.ok(result.calls.every(c => c.redirect === "error" && c.credentials === "same-origin" && c.cache === "no-store"));
    });
    for (const [name, response] of [
        ["JSON 200", () => json({})],
        ["HTML 200", () => new Response("<html>Sign in</html>", { headers: { "Content-Type": "text/html" } })],
        ["accepted 202", () => json({}, 202)],
        ["server failure", () => json({}, 503)],
        ["redirected 204", () => ({ status: 204, redirected: true })]
    ]) test(filename + " does not navigate on ambiguous completion: " + name, async () => {
        const result = await run(filename, { writeResponse: response });
        assert.equal(result.submitted, 0);
        assert.match(result.status, /could not confirm this sign-in/);
        assert.equal(result.calls.filter(c => /login$|complete$/.test(c.path)).length, 1, "No write replay.");
        assert.equal(result.button.disabled, false);
    });
    for (const [name, response] of [
        ["missing ceremony", () => json({ options: ceremony.options })],
        ["short ceremony", () => json({ ...ceremony, ceremonyId: "short" })],
        ["missing challenge", () => json({ ...ceremony, options: {} })],
        ["array", () => json([])],
        ["HTML", () => new Response("{}", { headers: { "Content-Type": "text/html" } })],
        ["invalid JSON", () => new Response("{", { headers: { "Content-Type": "application/json" } })],
        ["redirect", () => ({ status: 200, redirected: true })],
        ["no content", () => new Response(null, { status: 204 })]
    ]) test(filename + " rejects invalid options before the OS prompt: " + name, async () => {
        const result = await run(filename, { optionsResponse: response });
        assert.equal(result.nativeCalls, 0);
        assert.equal(result.submitted, 0);
        assert.equal(result.calls.length, 1);
        assert.match(result.status, /Could not start passkey verification/);
    });
    for (const token of [" ", "\r\n", 42]) test(filename + " rejects an unusable CSRF token: " + JSON.stringify(token), async () => {
        const result = await run(filename, { token });
        assert.equal(result.calls.length, 0);
        assert.equal(result.nativeCalls, 0);
        assert.match(result.status, /expired|Sign in again/);
    });
    test(filename + " keeps raw transport details out of recovery UI", async () => {
        const result = await run(filename, { fetchError: true });
        assert.equal(result.submitted, 0);
        assert.equal(result.calls.length, 1);
        assert.doesNotMatch(result.status, /synthetic-secret/);
        assert.equal(result.button.disabled, false);
    });
    test(filename + " preserves cancellation and authenticator/password fallback", async () => {
        const error = new Error("synthetic-secret"); error.name = "NotAllowedError";
        const result = await run(filename, { nativeError: error });
        assert.match(result.status, /Cancelled or timed out/);
        assert.equal(result.submitted, 0);
        assert.equal(result.calls.length, 1);
    });
    test(filename + " translates dynamic cancellation without exposing error text", async () => {
        const error = new Error("synthetic-secret"); error.name = "NotAllowedError";
        const result = await run(filename, { lang: "es-MX", nativeError: error });
        assert.match(result.status, /Se canceló/);
        assert.doesNotMatch(result.status, /synthetic-secret|Cancelled/);
    });
    test(filename + " does not perform credential operations in an insecure context", async () => {
        const result = await run(filename, { secure: false });
        assert.equal(result.calls.length, 0);
        assert.equal(result.nativeCalls, 0);
        assert.equal(result.submitted, 0);
    });
    test(filename + " preserves rate-limit guidance without replay", async () => {
        const result = await run(filename, { writeResponse: () => json({}, 429) });
        assert.match(result.status, /Too many attempts/);
        assert.equal(result.calls.filter(c => /login$|complete$/.test(c.path)).length, 1);
        assert.equal(result.submitted, 0);
    });
}
for (const action of ["remove", "register"]) {
    test(action + " is not reported saved after unexpected JSON 200", async () => {
        const result = await run("passkeys.js", { action, writeResponse: () => json({}) });
        assert.match(result.status, /could not confirm this change/);
        assert.doesNotMatch(result.status, /^Saved\./);
        assert.equal(result.list.children[0], "previously verified key");
        assert.equal(result.calls.filter(c => /remove$|register$/.test(c.path)).length, 1);
        assert.equal(result.password.value, "");
    });
    test(action + " requires a verified list before displaying Saved", async () => {
        const result = await run("passkeys.js", { action, listResponse: () => json({}, 503) });
        assert.match(result.status, /Could not load/);
        assert.doesNotMatch(result.status, /^Saved\./);
        assert.equal(result.list.children[0], "previously verified key");
        assert.equal(result.retry.hidden, false);
    });
}
test("secure browsers can remove a key with password verification without WebAuthn", async () => {
    const result = await run("passkeys.js", { action: "remove", webAuthn: false });
    assert.match(result.status, /^Saved\./);
    assert.equal(result.nativeCalls, 0);
    const write = result.calls.find(c => c.method === "POST");
    assert.equal(write.path, "/bff/account/security/passkeys/remove");
    assert.deepEqual(JSON.parse(write.body), {
        currentPassword: "synthetic-password", twoFactorCode: null, credentialId: key.id
    });
    assert.equal(result.password.value, "");
});
test("insecure browsers cannot remove keys", async () => {
    const result = await run("passkeys.js", { action: "remove", secure: false, webAuthn: false });
    assert.equal(result.calls.length, 0);
});
test("cancelling removal does not write or discard the saved list", async () => {
    const result = await run("passkeys.js", { action: "remove", confirm: false });
    assert.equal(result.calls.length, 0);
    assert.equal(result.list.children[0], "previously verified key");
    assert.equal(result.password.value, "");
});
for (const response of [
    () => json({}), () => json([{ ...key, createdAt: "invalid" }]), () => json([key, key]),
    () => ({ status: 200, redirected: true }),
    () => new Response("[]", { headers: { "Content-Type": "text/html" } }),
    () => { throw new Error("synthetic-secret-must-not-be-shown"); }
]) test("failed read preserves previously verified keys and enables read-only retry", async () => {
    const result = await run("passkeys.js", { action: "retry", webAuthn: false, listResponse: response });
    assert.equal(result.list.children[0], "previously verified key");
    assert.equal(result.retry.hidden, false);
    assert.equal(result.retry.disabled, false);
    assert.doesNotMatch(result.status, /synthetic-secret/);
    assert.equal(result.calls.length, 1);
    assert.equal(result.calls[0].method, undefined);
});
test("Spanish list labels use text nodes and preserve user-provided key names", async () => {
    const result = await run("passkeys.js", { action: "retry", lang: "es-ES" });
    assert.equal(result.status, "Se cargaron las claves guardadas.");
    const item = result.list.children[0];
    assert.ok(item.children[0].textContent.startsWith(key.name + " — añadida el "));
    assert.equal(item.children[2].textContent, "Eliminar");
});
for (const response of [
    () => json({ requestToken: " " }), () => json({ requestToken: {} }),
    () => json([]), () => ({ status: 200, redirected: true }),
    () => new Response("{}", { headers: { "Content-Type": "text/html" } })
]) test("unusable fetched antiforgery responses cannot send a credential write", async () => {
    const result = await run("passkeys.js", { action: "remove", missingToken: true, csrf: response });
    assert.equal(result.calls.length, 1);
    assert.equal(result.calls[0].path, "/bff/antiforgery");
    assert.match(result.status, /Sign in again/);
});
test("valid fetched antiforgery response permits exactly one explicit removal", async () => {
    const result = await run("passkeys.js", { action: "remove", missingToken: true });
    assert.equal(result.calls.filter(c => c.method === "POST").length, 1);
    assert.equal(result.calls.find(c => c.method === "POST").headers["X-CSRF-TOKEN"], "new-synthetic-csrf");
    assert.match(result.status, /^Saved\./);
});
