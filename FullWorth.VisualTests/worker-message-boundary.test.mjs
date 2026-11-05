import assert from "node:assert/strict";
import { createServer } from "node:http";
import { readFile, mkdir, writeFile } from "node:fs/promises";
import { chromium, webkit } from "playwright";

// Actual production receiver/caller sources, disposable localhost clients only.
// Observer listeners record native metadata; they do not replace handler events.
const sw = await readFile(new URL("../FullWorth.Web/wwwroot/service-worker.js", import.meta.url), "utf8");
const dedicated = await readFile(new URL("../FullWorth.Web/wwwroot/js/transaction-filter-worker.js", import.meta.url), "utf8");
const pwa = await readFile(new URL("../FullWorth.Web/wwwroot/js/pwa.js", import.meta.url), "utf8");
let version = 1, documentLoads = 0;
const records = [];
let origin;
const outsider = createServer((req, res) => {
    req.resume();
    res.writeHead(200, { "Content-Type": "text/html" });
    res.end("<!doctype html><script>parent.postMessage({type:'initialize',rows:[{id:'outsider'}],fixtureWindow:true}," +
        JSON.stringify(origin) + ");</script>");
});
const server = createServer((req, res) => {
    req.resume();
    const path = new URL(req.url, "http://localhost").pathname;
    res.setHeader("Cache-Control", "no-store");
    if (path === "/service-worker.js") {
        res.writeHead(200, { "Content-Type": "text/javascript", "Service-Worker-Allowed": "/" });
        res.end(sw + "\n/* disposable version " + version + " */\n" +
            'self.addEventListener("message",event=>{if(event.source&&event.data?.fixtureProbe)event.source.postMessage({fixtureSw:true,probe:event.data.fixtureProbe,origin:event.origin,sourceType:event.source.type});});');
    } else if (path === "/transaction-filter-worker.js") {
        res.writeHead(200, { "Content-Type": "text/javascript" });
        res.end('self.addEventListener("message",event=>self.postMessage({fixtureWorker:true,requestId:event.data?.requestId,origin:event.origin,sourceIsNull:event.source===null}));\n' + dedicated);
    } else if (path === "/pwa.js") {
        res.writeHead(200, { "Content-Type": "text/javascript" }); res.end(pwa);
    } else if (path === "/offline.html") {
        res.writeHead(200, { "Content-Type": "text/html" }); res.end("<!doctype html><p>Disposable offline fixture</p>");
    } else if (path === "/") {
        documentLoads++;
        res.writeHead(200, { "Content-Type": "text/html" });
        res.end('<!doctype html><html lang="en"><head><script src="/pwa.js"></script></head><body><button id="apply" onclick="void FullWorthPwa.applyUpdate()">Explicit update</button></body></html>');
    } else { res.writeHead(404); res.end(); }
});
await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
origin = "http://127.0.0.1:" + server.address().port;
await new Promise(resolve => outsider.listen(0, "127.0.0.1", resolve));
const outsiderOrigin = "http://127.0.0.1:" + outsider.address().port;
const engines = (process.env.FULLWORTH_TEST_BROWSERS ?? "chromium,webkit").split(",");
assert.ok(engines.length > 0 && engines.every(name => ["chromium", "webkit"].includes(name)));
function passed(engine, name, evidence = {}) {
    records.push({ engine, name, outcome: "passed", ...evidence });
    console.log(engine + ": " + name + " passed");
}
try {
    for (const engine of engines) {
        version = 1; documentLoads = 0;
        const browser = await ({ chromium, webkit })[engine].launch({ headless: true });
        try {
            const context = await browser.newContext();
            try {
                await context.addInitScript(() => {
                    window.swObserved = [];
                    navigator.serviceWorker.addEventListener("message", event => {
                        if (event.data?.fixtureSw) window.swObserved.push(event.data);
                    });
                });
                const page = await context.newPage();
                await page.goto(origin);
                await page.waitForFunction(() => !!navigator.serviceWorker.controller && !!window.FullWorthPwa);
                const initial = await page.evaluate(async () => {
                    window.worker = new Worker("/transaction-filter-worker.js");
                    window.workerMetadata = [];
                    window.worker.addEventListener("message", event => {
                        if (event.data?.fixtureWorker) window.workerMetadata.push(event.data);
                    });
                    let id = 0;
                    window.askWorker = payload => new Promise((resolve, reject) => {
                        const requestId = ++id;
                        const receive = event => {
                            if (event.data?.fixtureWorker || event.data?.requestId !== requestId) return;
                            cleanup(); resolve(event.data);
                        };
                        const fail = () => { cleanup(); reject(new Error("Dedicated fixture worker failed.")); };
                        const cleanup = () => {
                            window.worker.removeEventListener("message", receive);
                            window.worker.removeEventListener("error", fail);
                        };
                        window.worker.addEventListener("message", receive);
                        window.worker.addEventListener("error", fail);
                        window.worker.postMessage({ ...payload, requestId });
                    });
                    const initialized = await window.askWorker({ type: "initialize", rows: [
                        { id: "synthetic-a", accountId: "A", searchText: "Spotify monthly" },
                        { id: "synthetic-b", accountId: "B", searchText: "Groceries" }
                    ] });
                    const filtered = await window.askWorker({ type: "filter", searchText: "SPOTIFY", selectedAccountId: " a " });
                    return { initialized, filtered, metadata: window.workerMetadata };
                });
                assert.equal(initial.initialized.ok, true);
                assert.deepEqual(initial.filtered.ids, ["synthetic-a"]);
                assert.equal(initial.metadata.length, 2);
                assert.ok(initial.metadata.every(m => m.origin === "" && m.sourceIsNull === true));
                passed(engine, "dedicated private-channel protocol and native empty-origin metadata", { metadata: initial.metadata });

                const isolated = await page.evaluate(async outsiderOrigin => {
                    const observed = [];
                    await new Promise(resolve => {
                        const receive = event => {
                            if (event.origin !== location.origin && event.origin !== outsiderOrigin) return;
                            if (!event.data?.fixtureWindow) return;
                            observed.push(event.origin);
                            if (observed.includes(location.origin) && observed.includes(outsiderOrigin)) {
                                window.removeEventListener("message", receive); resolve();
                            }
                        };
                        window.addEventListener("message", receive);
                        window.postMessage({ type: "initialize", rows: [{ id: "window" }], fixtureWindow: true }, location.origin);
                        const frame = document.createElement("iframe");
                        frame.src = outsiderOrigin; document.body.appendChild(frame);
                    });
                    return { observed, filtered: await window.askWorker({ type: "filter" }) };
                }, outsiderOrigin);
                assert.deepEqual(isolated.filtered.ids, ["synthetic-a", "synthetic-b"]);
                assert.ok(isolated.observed.includes(origin) && isolated.observed.includes(outsiderOrigin));
                passed(engine, "same-origin and cross-origin Window messages do not reach the dedicated worker");

                const bounded = await page.evaluate(async () => {
                    const rows = Array.from({ length: 500 }, (_, i) => ({ id: "synthetic-" + i, accountId: "A", searchText: "fixture" }));
                    const initialized = await window.askWorker({ type: "initialize", rows });
                    const before = await window.askWorker({ type: "filter" });
                    const rejected = await window.askWorker({ type: "initialize", rows: [...rows, { id: "extra" }] });
                    const after = await window.askWorker({ type: "filter" });
                    window.worker.terminate();
                    return { initialized, before, rejected, after };
                });
                assert.equal(bounded.initialized.ok, true);
                assert.equal(bounded.before.ids.length, 500);
                assert.equal(bounded.rejected.ok, false);
                assert.deepEqual(bounded.after.ids, bounded.before.ids);
                passed(engine, "500-row worker limit refuses 501 without replacing working data");

                version = 2;
                await page.evaluate(async () => { await (await navigator.serviceWorker.getRegistration()).update(); });
                await page.waitForFunction(async () => !!(await navigator.serviceWorker.getRegistration())?.waiting);
                await page.evaluate(async () => {
                    const registration = await navigator.serviceWorker.getRegistration();
                    window.oldController = navigator.serviceWorker.controller;
                    registration.waiting.postMessage({ type: "UNKNOWN", fixtureProbe: "unknown" });
                });
                await page.waitForFunction(() => window.swObserved.some(m => m.probe === "unknown"));
                const ignored = await page.evaluate(async () => ({
                    waiting: (await navigator.serviceWorker.getRegistration()).waiting?.state,
                    sameController: navigator.serviceWorker.controller === window.oldController,
                    metadata: window.swObserved.find(m => m.probe === "unknown")
                }));
                assert.equal(ignored.waiting, "installed");
                assert.equal(ignored.sameController, true);
                assert.equal(ignored.metadata.origin, origin);
                assert.equal(ignored.metadata.sourceType, "window");
                passed(engine, "native same-origin WindowClient metadata and unknown-command refusal", { metadata: ignored.metadata });

                const activated = await page.evaluate(async () => {
                    const waiting = (await navigator.serviceWorker.getRegistration()).waiting;
                    return await new Promise(resolve => {
                        const changed = () => {
                            if (navigator.serviceWorker.controller !== waiting) return;
                            navigator.serviceWorker.removeEventListener("controllerchange", changed);
                            resolve({ exactWaitingController: true });
                        };
                        navigator.serviceWorker.addEventListener("controllerchange", changed);
                        waiting.postMessage({ type: "SKIP_WAITING", fixtureProbe: "activate" });
                    });
                });
                await page.waitForFunction(() => window.swObserved.some(m => m.probe === "activate"));
                assert.equal(activated.exactWaitingController, true);
                const activationMetadata = await page.evaluate(() => window.swObserved.find(m => m.probe === "activate"));
                assert.equal(activationMetadata.origin, origin);
                assert.equal(activationMetadata.sourceType, "window");
                assert.equal(documentLoads, 1, "Direct activation must not invent a page reload.");
                passed(engine, "same-origin explicit command activates the exact waiting service worker", { metadata: activationMetadata });

                version = 3;
                await page.evaluate(async () => { await (await navigator.serviceWorker.getRegistration()).update(); });
                await page.waitForFunction(async () => !!(await navigator.serviceWorker.getRegistration())?.waiting);
                await Promise.all([
                    page.waitForEvent("framenavigated", { predicate: frame => frame === page.mainFrame() }),
                    page.locator("#apply").click()
                ]);
                await page.waitForLoadState("load");
                try {
                    await page.waitForFunction(() => navigator.serviceWorker.controller?.state === "activated");
                } catch (error) {
                    // This is an acceptance assertion, not a timeout workaround.
                    // Capture the browser's post-reload worker state before teardown,
                    // preserving the original failure when control is not established.
                    const workerState = await page.evaluate(async () => {
                        const registration = await navigator.serviceWorker.getRegistration();
                        return {
                            location: location.href,
                            readyState: document.readyState,
                            controller: navigator.serviceWorker.controller?.state ?? null,
                            active: registration?.active?.state ?? null,
                            waiting: registration?.waiting?.state ?? null,
                            installing: registration?.installing?.state ?? null,
                            scriptLoaded: Boolean(window.FullWorthPwa)
                        };
                    });
                    records.push({
                        engine,
                        name: "actual PWA caller preserves explicit single reload after guarded activation",
                        outcome: "failed",
                        phase: "post-reload-controller",
                        documentLoads,
                        workerState
                    });
                    throw new Error(
                        "Post-reload controller did not activate: " +
                        JSON.stringify({ documentLoads, workerState }), { cause: error });
                }
                assert.equal(documentLoads, 2, "The actual explicit PWA update reloads exactly once.");
                passed(engine, "actual PWA caller preserves explicit single reload after guarded activation");
            } finally { await context.close(); }
        } finally { await browser.close(); }
    }
} finally {
    const directory = new URL("./artifacts/confirmation/", import.meta.url);
    await mkdir(directory, { recursive: true });
    await writeFile(new URL("worker-message-boundary.json", directory), JSON.stringify({
        scope: "Synthetic localhost Chromium/WebKit contexts with actual worker/PWA sources. Not production, physical-device, cross-origin exploit or release acceptance.",
        records
    }, null, 2));
    await Promise.all([new Promise(resolve => server.close(resolve)), new Promise(resolve => outsider.close(resolve))]);
}
console.log(records.length + " worker browser scenarios passed.");
