import assert from "node:assert/strict";
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { chromium, webkit } from "playwright";

// Disposable, loopback-only BFF responses. No provider or customer credentials.
const moduleNames = ["bff", "subscription-bff", "login-redirect", "plaid-completion", "confirmation-dialog"];
const sources = new Map(await Promise.all(moduleNames.map(async name => [
    "/js/" + name + ".js",
    await readFile(new URL("../FullWorth.Web/wwwroot/js/" + name + ".js", import.meta.url), "utf8")
])));
const scenarios = new Map();
let nextScenario = 0, passed = 0;
const server = createServer((request, response) => {
    request.resume();
    const path = new URL(request.url, "http://localhost").pathname;
    if (path === "/" || path === "/login") {
        response.writeHead(200, { "Content-Type": "text/html" });
        response.end('<!doctype html><html><body><input type="file" id="synthetic-statement"></body></html>');
        return;
    }
    if (sources.has(path)) {
        response.writeHead(200, { "Content-Type": "text/javascript" });
        response.end(sources.get(path));
        return;
    }
    const state = scenarios.get(request.headers["x-synthetic-case"]);
    if (!state) { response.writeHead(404); response.end(); return; }
    function json(body, status = 200, type = "application/json") {
        response.writeHead(status, { "Content-Type": type });
        response.end(JSON.stringify(body));
    }
    if (path === "/redirect-target") {
        state.targets++;
        json({ accepted: true });
        return;
    }
    if (path === "/bff/antiforgery") {
        state.tokens++;
        json({ requestToken: state.mode === "invalid-token" ? state.token : "synthetic-" + state.tokens });
        return;
    }
    state.requests++;
    state.methods.push(request.method);
    state.csrf.push(request.headers["x-csrf-token"]);
    state.contentTypes.push(request.headers["content-type"] ?? "");
    if (state.mode === "redirect") {
        response.writeHead(state.status, { Location: "/redirect-target" });
        response.end();
    } else if (state.mode === "reject-once" && state.requests === 1) {
        response.writeHead(state.status);
        response.end();
    } else if (state.mode === "html") {
        response.writeHead(200, { "Content-Type": "text/html" });
        response.end("<html>Not a completed operation</html>");
    } else if (state.mode === "invalid-json") {
        response.writeHead(200, { "Content-Type": "application/json" });
        response.end("{ private-synthetic-payload");
    } else if (state.mode === "json-prefix") {
        json({ accepted: true }, 200, "application/jsonp");
    } else if (state.mode === "no-content") {
        response.writeHead(204);
        response.end();
    } else if (state.mode === "missing") {
        response.writeHead(404);
        response.end();
    } else {
        json({ accepted: true }, 200, "Application/JSON; charset=utf-8");
    }
});
await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
const origin = "http://127.0.0.1:" + server.address().port;
const browserNames = (process.env.FULLWORTH_TEST_BROWSERS ?? "chromium,webkit").split(",");
assert.ok(browserNames.length > 0 && browserNames.every(name => ["chromium", "webkit"].includes(name)));
try {
    for (const name of browserNames) {
        const browser = await ({ chromium, webkit })[name].launch({ headless: true });
        try {
            async function scenario(label, settings, check) {
                const id = String(++nextScenario);
                const state = { mode: "json", requests: 0, tokens: 0, targets: 0, csrf: [], methods: [], contentTypes: [], ...settings };
                scenarios.set(id, state);
                const context = await browser.newContext({ extraHTTPHeaders: { "X-Synthetic-Case": id } });
                try {
                    const page = await context.newPage();
                    await page.goto(origin);
                    const invoke = (module, method, args = []) => page.evaluate(async ({ module, method, args }) => {
                        try {
                            const client = await import("/js/" + module + ".js");
                            return { ok: true, result: await client[method](...args) };
                        } catch (error) {
                            return { ok: false, message: error.message };
                        }
                    }, { module, method, args });
                    await check({ page, invoke, state });
                    passed++;
                } catch (error) {
                    throw new Error(name + " / " + label + ": " + error.message, { cause: error });
                } finally {
                    await context.close();
                    scenarios.delete(id);
                }
            }

            for (const module of ["bff", "subscription-bff"]) {
                const write = module === "bff" ? "dismissAlert" : "redeemAccessKey";
                const read = module === "bff" ? "getBankConnections" : "getSubscription";
                for (const mode of ["html", "invalid-json", "json-prefix"]) {
                    await scenario(module + " rejects " + mode, { mode }, async ({ invoke, state }) => {
                        const result = await invoke(module, write, ["synthetic"]);
                        assert.equal(result.ok, false);
                        assert.ok(!result.message.includes("private-synthetic-payload"));
                        assert.equal(state.requests, 1);
                        assert.equal(state.tokens, 1);
                    });
                }
                for (const token of ["   ", 42]) {
                    await scenario(module + " validates token " + JSON.stringify(token),
                        { mode: "invalid-token", token }, async ({ invoke, state }) => {
                            assert.equal((await invoke(module, write, ["synthetic"])).ok, false);
                            assert.equal(state.requests, 0);
                        });
                }
                for (const status of [301, 302, 303, 307, 308]) {
                    await scenario(module + " refuses redirect " + status,
                        { mode: "redirect", status }, async ({ invoke, state }) => {
                            assert.equal((await invoke(module, write, ["synthetic"])).ok, false);
                            assert.equal(state.requests, 1);
                            assert.equal(state.targets, 0);
                        });
                }
                for (const status of [400, 403]) {
                    await scenario(module + " refreshes rejected token " + status,
                        { mode: "reject-once", status }, async ({ invoke, state }) => {
                            assert.equal((await invoke(module, write, ["synthetic"])).ok, false);
                            assert.equal(state.requests, 1, "no automatic write retry");
                            assert.equal(state.tokens, 1);
                            assert.equal((await invoke(module, write, ["synthetic"])).ok, true);
                            assert.equal(state.requests, 2);
                            assert.equal(state.tokens, 2);
                            assert.deepEqual(state.csrf, ["synthetic-1", "synthetic-2"]);
                        });
                }
                await scenario(module + " rejects an HTML read", { mode: "html" }, async ({ invoke }) => {
                    assert.equal((await invoke(module, read)).ok, false);
                });
                await scenario(module + " accepts JSON without a read token", {}, async ({ invoke, state }) => {
                    assert.deepEqual(await invoke(module, read), { ok: true, result: { accepted: true } });
                    assert.equal(state.tokens, 0);
                });
                await scenario(module + " preserves successful writes and token reuse", {}, async ({ invoke, state }) => {
                    assert.equal((await invoke(module, write, ["synthetic"])).ok, true);
                    assert.equal((await invoke(module, write, ["synthetic"])).ok, true);
                    assert.equal(state.requests, 2);
                    assert.equal(state.tokens, 1);
                });
            }

            for (const [method, args] of [
                ["disconnectBankConnection", ["synthetic-bank"]],
                ["putPlanningPaySchedule", [{ frequency: "Synthetic" }]],
                ["deleteFullWorthAccount", ["DELETE"]]
            ]) {
                await scenario(method + " rejects HTML", { mode: "html" }, async ({ invoke, state }) => {
                    assert.equal((await invoke("bff", method, args)).ok, false);
                    assert.equal(state.requests, 1);
                });
            }
            await scenario("legitimate empty mutation remains successful", { mode: "no-content" }, async ({ invoke }) => {
                assert.deepEqual(await invoke("bff", "disconnectBankConnection", ["synthetic-bank"]),
                    { ok: true, result: null });
            });
            await scenario("only latest-upload discovery permits an empty read", { mode: "no-content" }, async ({ invoke }) => {
                assert.deepEqual(await invoke("bff", "getLatestBillStatementUpload", ["synthetic-bill"]),
                    { ok: true, result: null });
                assert.equal((await invoke("bff", "getBankConnections")).ok, false);
            });
            await scenario("missing pay schedule remains an intentional empty state", { mode: "missing" }, async ({ invoke }) => {
                assert.deepEqual(await invoke("bff", "getPlanningPaySchedule"), { ok: true, result: null });
            });
            await scenario("statement upload rejects an HTML response", { mode: "html" }, async ({ page, invoke, state }) => {
                await page.setInputFiles("#synthetic-statement", {
                    name: "synthetic.pdf", mimeType: "application/pdf", buffer: Buffer.from("synthetic fixture only")
                });
                assert.equal((await invoke("bff", "uploadBillStatement",
                    ["synthetic-bill", "synthetic-statement"])).ok, false);
                assert.equal(state.requests, 1);
                assert.deepEqual(state.methods, ["POST"]);
                assert.ok(state.contentTypes[0].startsWith("multipart/form-data; boundary="));
                assert.deepEqual(state.csrf, ["synthetic-1"]);
            });
        } finally {
            await browser.close();
        }
    }
    console.log("Browser BFF response boundaries passed: " + passed + " cases; engines: " + browserNames.join(", ") + ".");
} finally {
    server.closeAllConnections();
    await new Promise(resolve => server.close(resolve));
}
