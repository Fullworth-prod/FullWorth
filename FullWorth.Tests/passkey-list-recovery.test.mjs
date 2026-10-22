import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import vm from "node:vm";

test("failed passkey list has read-only recovery without biometric support or credential mutation", async () => {
    const source = await readFile(new URL("../FullWorth.Web/wwwroot/js/passkeys.js", import.meta.url), "utf8");
    const events = {}, status = { textContent: "" }, list = { textContent: "", replaceChildren() { this.textContent = ""; } };
    const root = { dataset: {}, isConnected: true,
        querySelector: selector => selector === "[data-passkey-status]" ? status : selector === "[data-passkey-list]" ? list : null };
    const button = { dataset: { passkeyAction: "retry" }, isConnected: true, disabled: false, closest: () => root };
    let mode = "fail", reads = 0;
    vm.runInNewContext(source, {
        document: { documentElement: {}, addEventListener: (name, fn) => { events[name] = fn; },
            querySelectorAll: () => root.dataset.loaded ? [] : [root] },
        window: { isSecureContext: false }, navigator: {},
        MutationObserver: class { observe() {} },
        fetch: async (url, options) => {
            reads++;
            assert.equal(url, "/bff/account/security/passkeys");
            assert.equal(options.credentials, "same-origin");
            assert.equal(options.cache, "no-store");
            assert.equal(options.method, undefined, "retry is read-only");
            return mode === "fail" ? new Response(null, { status: 503 }) :
                Response.json(mode === "invalid" ? {} : []);
        }
    });
    await new Promise(resolve => setImmediate(resolve));
    assert.match(status.textContent, /Could not load/);
    mode = "ok";
    await events.click({ target: { closest: () => button } });
    assert.equal(reads, 2);
    assert.equal(list.textContent, "No keys registered yet.");
    assert.equal(status.textContent, "Key list updated.");
    mode = "invalid";
    await events.click({ target: { closest: () => button } });
    assert.match(status.textContent, /Could not load/);
    assert.equal(list.textContent, "No keys registered yet.", "malformed refresh preserves the last known list");
    assert.equal(button.disabled, false);
});
