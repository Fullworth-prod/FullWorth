import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const names = ["account-privacy-requests.js", "account-delete.js", "account-export.js"];
const sources = Object.fromEntries(await Promise.all(names.map(async name =>
    [name, await readFile(new URL("../FullWorth.Web/wwwroot/js/" + name, import.meta.url), "utf8")])));
const data = source => "data:text/javascript;base64," + Buffer.from(source).toString("base64");
let ordinal = 0;

async function exercise(action, tokenReply, writeReply) {
    const nonce = "\n// isolated case " + ordinal++;
    const shared = data(sources[names[0]] + nonce);
    const module = await import(data(sources[action === "delete" ? names[1] : names[2]]
        .replace("./account-privacy-requests.js", shared) + nonce));
    const original = { fetch: globalThis.fetch, window: globalThis.window, document: globalThis.document,
        create: URL.createObjectURL, revoke: URL.revokeObjectURL };
    const calls = [];
    let downloads = 0;
    try {
        globalThis.window = { setTimeout: () => 1, clearTimeout() {} };
        globalThis.document = { createElement: () => ({ click() { downloads++; }, remove() {} }),
            body: { appendChild() {} } };
        URL.createObjectURL = () => "blob:synthetic";
        URL.revokeObjectURL = () => {};
        globalThis.fetch = async (url, options) => {
            calls.push({ url, options });
            return url === "/bff/antiforgery" ? tokenReply() : writeReply();
        };
        let result, error;
        try {
            result = action === "delete" ? await module.deleteFullWorthAccount("DELETE", "Synthetic!Proof123", null) :
                await module.downloadAccountExport("Synthetic!Proof123", null);
        } catch (failure) { error = failure; }
        return { result, error, calls, downloads };
    } finally {
        module.cancelAccountExport?.();
        globalThis.fetch = original.fetch; globalThis.window = original.window; globalThis.document = original.document;
        URL.createObjectURL = original.create; URL.revokeObjectURL = original.revoke;
    }
}
const validToken = () => Response.json({ requestToken: "synthetic-csrf" });
const validWrite = action => () => action === "delete" ? new Response(null, { status: 204 }) :
    Response.json({ synthetic: "not private data" });

for (const action of ["delete", "export"]) {
    test(action + " accepts documented response with one explicit write", async () => {
        const outcome = await exercise(action, validToken, validWrite(action));
        assert.equal(outcome.result, true);
        assert.equal(outcome.error, undefined);
        assert.equal(outcome.calls.length, 2);
        for (const call of outcome.calls) {
            assert.equal(call.options.redirect, "error");
            assert.equal(call.options.credentials, "same-origin");
            assert.equal(call.options.cache, "no-store");
        }
        assert.equal(outcome.downloads, action === "export" ? 1 : 0);
    });
    const invalidTokens = [null, [], {}, 42, "", " ", "a b", "\t", "\r", "\n", "\0", "\x7f", "é", "a".repeat(4097)];
    for (const [index, requestToken] of invalidTokens.entries())
        test(action + " rejects unsafe token case " + index + " before write", async () => {
            const outcome = await exercise(action, () => Response.json({ requestToken }), validWrite(action));
            assert.ok(outcome.error);
            assert.equal(outcome.calls.length, 1);
            assert.equal(outcome.downloads, 0);
        });

    const badEnvelopes = [
        () => Response.json({ requestToken: "synthetic" }, { status: 202 }),
        () => Response.json({ requestToken: "synthetic" }, { status: 201 }),
        () => new Response(null, { status: 204 }),
        () => new Response('{"requestToken":"synthetic"}', { headers: { "content-type": "text/html" } }),
        () => new Response('{"requestToken":"synthetic"}', { headers: { "content-type": "application/problem+json" } }),
        () => new Response('{"requestToken":"synthetic"}', { headers: { "content-type": "application/json-extra" } }),
        () => new Response("{broken", { headers: { "content-type": "application/json" } }),
        () => Response.json([]),
        () => Response.json("synthetic"),
        () => Response.json(null),
        () => { const response = validToken(); Object.defineProperty(response, "redirected", { value: true }); return response; }
    ];
    for (const [index, reply] of badEnvelopes.entries())
        test(action + " rejects invalid token response case " + index + " before write", async () => {
            const outcome = await exercise(action, reply, validWrite(action));
            assert.ok(outcome.error);
            assert.equal(outcome.calls.length, 1);
            assert.equal(outcome.downloads, 0);
        });
    test(action + " accepts safe token at length limit with normalized JSON MIME", async () => {
        const outcome = await exercise(action, () => new Response(JSON.stringify({ requestToken: "a".repeat(4096) }),
            { headers: { "content-type": "Application/JSON; charset=utf-8" } }), validWrite(action));
        assert.equal(outcome.result, true);
        assert.equal(outcome.calls[1].options.headers["X-CSRF-TOKEN"].length, 4096);
    });
    test(action + " does not replay an ambiguous write", async () => {
        const outcome = await exercise(action, validToken, () => { throw new TypeError("synthetic lost response"); });
        assert.ok(outcome.error);
        assert.equal(outcome.calls.length, 2);
        assert.equal(outcome.downloads, 0);
    });
}
for (const status of [202, 204])
    test("deletion accepts direct " + status + " only", async () => {
        const direct = await exercise("delete", validToken, () => new Response(null, { status }));
        assert.equal(direct.result, true);
        const redirected = await exercise("delete", validToken, () => {
            const response = new Response(null, { status });
            Object.defineProperty(response, "redirected", { value: true });
            return response;
        });
        assert.ok(redirected.error);
        assert.equal(redirected.calls.length, 2);
    });
for (const status of [200, 201, 205, 206, 302, 401, 429, 503])
    test("deletion rejects unexpected response " + status + " without replay", async () => {
        const outcome = await exercise("delete", validToken, () => new Response(null, { status }));
        assert.ok(outcome.error);
        assert.equal(outcome.calls.length, 2);
    });
