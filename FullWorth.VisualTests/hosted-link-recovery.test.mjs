import assert from "node:assert/strict";
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { chromium, webkit } from "playwright";

// Actual browser modules; only BFF/provider responses are disposable fixtures.
const sources = new Map(await Promise.all(
    ["bff", "plaid-completion", "login-redirect", "confirmation-dialog"].map(async name =>
        ["/js/" + name + ".js", await readFile(new URL(
            "../FullWorth.Web/wwwroot/js/" + name + ".js", import.meta.url), "utf8")])
));
const states = new Map();
let nextId = 0, passed = 0;
const server = createServer((req, res) => {
    req.resume();
    const path = new URL(req.url, "http://localhost").pathname;
    if (sources.has(path)) {
        res.writeHead(200, { "Content-Type": "text/javascript" });
        res.end(sources.get(path));
        return;
    }
    const state = states.get(req.headers["x-synthetic-case"]);
    if (!state) { res.writeHead(404); res.end(); return; }
    function json(body, code = 200) {
        res.writeHead(code, { "Content-Type": "application/json" });
        res.end(JSON.stringify(body));
    }
    if (path === "/") {
        res.writeHead(200, { "Content-Type": "text/html" });
        res.end('<!doctype html><html lang="' + state.lang +
            '"><body><button id="connect">Connect</button><p id="status" role="status" aria-live="polite"></p></body></html>');
    } else if (path === "/bff/antiforgery") json({ requestToken: "synthetic-csrf" });
    else if (path.endsWith("/complete")) {
        state.completed.push(path);
        if (state.mode === "lost-reply") json({ message: "Synthetic uncertain reply" }, 503);
        else if (state.mode === "invalid-completed") json({ status: "Completed", connection: null });
        else json({ status: "RecoveryRequired", connection: null });
    } else if (path.endsWith("/link-session") || path.endsWith("/update-link-session")) {
        const id = "synthetic-session-" + (++state.created);
        state.sessionIds.push(id);
        json({ sessionId: id, hostedLinkUrl: "https://cdn.plaid.com/link/" + id });
    } else { state.unexpected.push(path); res.writeHead(404); res.end(); }
});
await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
const origin = "http://127.0.0.1:" + server.address().port;
const engines = (process.env.FULLWORTH_TEST_BROWSERS ?? "chromium,webkit").split(",");
assert.ok(engines.length > 0 && engines.every(name => ["chromium", "webkit"].includes(name)));
try {
    for (const engine of engines) {
        const browser = await ({ chromium, webkit })[engine].launch({ headless: true });
        try {
            for (const lang of ["en", "es"])
            for (const reconnect of [false, true])
            for (const mode of ["recovery", "lost-reply", "invalid-completed"]) {
                const id = String(++nextId);
                const state = { lang, mode, created: 0, completed: [], sessionIds: [], unexpected: [] };
                states.set(id, state);
                const context = await browser.newContext({
                    viewport: { width: 390, height: 844 },
                    extraHTTPHeaders: { "X-Synthetic-Case": id }
                });
                try {
                    const page = await context.newPage();
                    await page.goto(origin);
                    await page.clock.install();
                    await page.evaluate(async reconnect => {
                        // No external Plaid window or real bank data. Preserve the
                        // production click handler, timer, DOM and fetch behavior.
                        window.open = () => ({
                            closed: false, opener: null,
                            document: document.implementation.createHTMLDocument('Synthetic Plaid'),
                            location: { replace() {} }, close() { this.closed = true; }
                        });
                        const client = await import("/js/bff.js");
                        if (reconnect) client.wirePlaidReconnectButton(
                            "connect", "status", "synthetic-bank", "Synthetic bank");
                        else client.wirePlaidConnectButton("connect", "status");
                    }, reconnect);
                    async function clickAndComplete(expected) {
                        await page.locator("#connect").click();
                        await page.waitForFunction(() => document.querySelector("#status").dataset.state === "working");
                        // Wait for session creation and the real polling timer.
                        await page.waitForFunction(() => document.querySelector("#status").textContent.includes("FullWorth"));
                        await page.clock.runFor(2100);
                        await page.waitForFunction(() => document.querySelector("#connect").disabled === false);
                        assert.equal(state.created, expected);
                        assert.equal(state.completed.length, expected);
                    }
                    await clickAndComplete(1);
                    assert.equal(await page.locator("#status").getAttribute("data-state"), "error");
                    const message = await page.locator("#status").textContent();
                    assert.ok(!/is connected|is reconnected|está activa|se restableció/.test(message));
                    assert.equal(page.url(), origin + "/");
                    if (mode !== "invalid-completed") {
                        const review = page.locator("#status a");
                        assert.equal(await review.getAttribute("href"), "/app/account");
                        assert.equal(await review.textContent(), lang === "es" ?
                            "Revisar bancos conectados" : "Review connected banks");
                        assert.match(message, lang === "es" ? /bancos conectados/ : /connected banks/);
                    }
                    // Advancing time cannot silently create or replay a Link.
                    await page.clock.runFor(120000);
                    assert.equal(state.created, 1);
                    assert.equal(state.completed.length, 1);
                    // An explicit new click starts a different session, never
                    // retries the previously fenced session.
                    await clickAndComplete(2);
                    assert.notEqual(state.sessionIds[0], state.sessionIds[1]);
                    assert.ok(state.completed[0].includes(state.sessionIds[0]));
                    assert.ok(state.completed[1].includes(state.sessionIds[1]));
                    assert.deepEqual(state.unexpected, []);
                    passed++;
                } catch (error) {
                    throw new Error(engine + "/" + lang + "/" + reconnect + "/" + mode +
                        ": " + error.message, { cause: error });
                } finally {
                    states.delete(id);
                    await context.close();
                }
            }
        } finally { await browser.close(); }
    }
    console.log("Hosted Link browser recovery passed: " + passed + " cases; engines: " + engines.join(", "));
} finally {
    server.closeAllConnections();
    await new Promise(resolve => server.close(resolve));
}
