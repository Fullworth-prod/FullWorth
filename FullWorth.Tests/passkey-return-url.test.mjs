import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { runInNewContext } from "node:vm";

// Exercise the actual browser click handler after a successful synthetic
// WebAuthn ceremony; no credentials, provider calls or real sessions are used.
async function completedPasskeyRedirect(filename, returnUrl) {
    let handler;
    let redirected;
    let submitted = 0;
    const followUpForm = { requestSubmit: () => { submitted++; } };
    const status = { textContent: "" };
    const root = {
        isConnected: true,
        querySelector: selector => selector.includes("status") ? status : null
    };
    const button = {
        disabled: false,
        isConnected: true,
        dataset: { passkeyAction: "login" },
        closest: () => root
    };
    const document = {
        documentElement: {},
        addEventListener: (type, callback) => {
            if (type === "click") handler = callback;
        },
        querySelector: selector => {
            if (selector.includes("data-passkey-continue")) return followUpForm;
            if (selector.includes("__RequestVerificationToken")) return { value: "synthetic-csrf" };
            if (selector.includes("returnUrl")) return { value: returnUrl };
            return null;
        },
        querySelectorAll: () => []
    };
    const PublicKeyCredential = { parseRequestOptionsFromJSON: json => json };
    const response = {
        ok: true,
        status: 200,
        json: async () => ({ ceremonyId: "synthetic-ceremony", options: { challenge: "test" } })
    };
    const context = {
        document,
        window: { isSecureContext: true, PublicKeyCredential },
        PublicKeyCredential,
        navigator: { credentials: { get: async () => ({ toJSON: () => ({ type: "public-key" }) }) } },
        fetch: async () => response,
        location: { assign: value => { redirected = value; } },
        MutationObserver: class { observe() {} },
        confirm: () => true,
        Uint8Array,
        console
    };
    const source = readFileSync(new URL("../FullWorth.Web/wwwroot/js/" + filename, import.meta.url), "utf8");
    runInNewContext(source, context, { filename });
    assert.equal(typeof handler, "function", filename + " must register its passkey handler");
    await handler({ target: { closest: () => button } });
    assert.equal(submitted, 1, filename + " must submit the protected server form after WebAuthn");
    assert.equal(redirected, undefined, filename + " must not navigate to a DOM-derived URL");
    return { submitted, redirected };
}

const cases = [
    ["/app", "/app"],
    ["/app?tab=bills", "/app?tab=bills"],
    ["/app/inbox%2Ftoday", "/app/inbox%2Ftoday"],
    ["javascript:alert(1)", "/app"],
    ["https://outside.example/steal", "/app"],
    ["//outside.example", "/app"],
    ["/\\outside.example", "/app"],
    ["/%2f%2foutside.example", "/app"],
    ["/%252f%252foutside.example", "/app"],
    ["/%5coutside.example", "/app"],
    ["/%255coutside.example", "/app"],
    ["/%0d%0aLocation:outside", "/app"],
    ["/app\u0000bad", "/app"],
    ["/incomplete%", "/app"],
    ["/%252525252f", "/app"],
    ["", "/app"]
];

for (const filename of ["passkeys.js", "password-step.js"]) {
    test(filename + " never navigates directly to a DOM-derived destination after WebAuthn", async () => {
        for (const [input] of cases) {
            const result = await completedPasskeyRedirect(filename, input);
            assert.equal(result.submitted, 1);
            assert.equal(result.redirected, undefined,
                filename + " must not navigate to browser-controlled URL: " + JSON.stringify(input));
        }
    });
}
