import assert from "node:assert/strict";
import http from "node:http";
import { readFile } from "node:fs/promises";
import { chromium, webkit } from "playwright";

// A local HTTP fixture serves actual shipped modules. No real accounts, proof,
// private exports or deletion APIs are reached.
const sources = Object.fromEntries(await Promise.all(
    ["account-delete.js", "account-export.js", "account-privacy-requests.js"].map(async name =>
        ["/modules/" + name, await readFile(new URL("../FullWorth.Web/wwwroot/js/" + name, import.meta.url), "utf8")])));
let scenario, writes, sinks, proofAtSink, downloads, foreignOrigin;
const foreignServer = http.createServer(async (request, response) => {
    let body = ""; for await (const chunk of request) body += chunk;
    sinks++;
    if (body.includes("Synthetic!Proof123")) proofAtSink++;
    response.writeHead(200, { "content-type": "application/json",
        "access-control-allow-origin": "*", "access-control-allow-methods": "GET, POST, DELETE, OPTIONS",
        "access-control-allow-headers": "content-type,x-csrf-token" });
    response.end('{"synthetic":true}');
});
const server = http.createServer(async (request, response) => {
    let body = "";
    for await (const chunk of request) body += chunk;
    const send = (status, type, content) => {
        response.writeHead(status, { "content-type": type, "cache-control": "no-store" });
        response.end(content);
    };
    if (sources[request.url]) return send(200, "text/javascript", sources[request.url]);
    if (request.url === "/") return send(200, "text/html", "<!doctype html><title>Synthetic privacy fixture</title>");
    if (request.url === "/bff/antiforgery") {
        if (scenario.stage === "token-redirect") {
            response.writeHead(scenario.redirect, { location: (scenario.crossOrigin ? foreignOrigin : "") + "/redirect-sink" }); response.end(); return;
        }
        return send(scenario.tokenStatus ?? 200, scenario.tokenType ?? "application/json",
            scenario.tokenRaw ?? JSON.stringify({ requestToken: scenario.token ?? "synthetic-csrf" }));
    }
    if (request.url === "/redirect-sink") {
        sinks++;
        if (body.includes("Synthetic!Proof123")) proofAtSink++;
        return send(scenario.action === "delete" ? 204 : 200, "application/json",
            scenario.action === "delete" ? "" : '{"synthetic":true}');
    }
    if (request.url === "/bff/account" || request.url === "/bff/account/export") {
        writes++;
        assert.equal(request.method, scenario.action === "delete" ? "DELETE" : "POST");
        assert.equal(JSON.parse(body).currentPassword, "Synthetic!Proof123");
        assert.ok(request.headers["x-csrf-token"]);
        if (scenario.stage === "write-redirect") {
            response.writeHead(scenario.redirect, { location: (scenario.crossOrigin ? foreignOrigin : "") + "/redirect-sink" }); response.end(); return;
        }
        return send(scenario.action === "delete" ? scenario.writeStatus ?? 204 : 200,
            "application/json", scenario.action === "delete" ? "" : '{"synthetic":"not private data"}');
    }
    send(404, "text/plain", "");
});
await new Promise(done => foreignServer.listen(0, "127.0.0.1", done));
foreignOrigin = "http://127.0.0.1:" + foreignServer.address().port;
await new Promise(done => server.listen(0, "127.0.0.1", done));
const origin = "http://127.0.0.1:" + server.address().port;
try {
    for (const [name, engine] of [["Chromium", chromium], ["WebKit", webkit]]) {
        const browser = await engine.launch({ headless: true });
        let cases = 0;
        try {
            const run = async value => {
                scenario = value; writes = sinks = proofAtSink = downloads = 0;
                const context = await browser.newContext({ acceptDownloads: true });
                const page = await context.newPage();
                page.on("download", () => downloads++);
                try {
                    await page.goto(origin);
                    const result = await page.evaluate(async action => {
                        try {
                            const module = await import("/modules/account-" + (action === "delete" ? "delete" : "export") + ".js");
                            const value = action === "delete" ?
                                await module.deleteFullWorthAccount("DELETE", "Synthetic!Proof123", null) :
                                await module.downloadAccountExport("Synthetic!Proof123", null);
                            module.cancelAccountExport?.();
                            return { accepted: value === true };
                        } catch { return { accepted: false }; }
                    }, value.action);
                    assert.equal(result.accepted, value.accepted, name + " acceptance: " + JSON.stringify(value));
                    assert.equal(writes, value.expectedWrites, "Unexpected write arrival");
                    assert.equal(sinks, 0, "A privacy redirect must not reach its destination");
                    assert.equal(proofAtSink, 0, "Synthetic proof must not be forwarded");
                    if (!value.accepted) assert.equal(downloads, 0);
                    cases++;
                } finally { await context.close(); }
            };
            for (const action of ["delete", "export"]) {
                await run({ action, accepted: true, expectedWrites: 1 });
                if (action === "delete") await run({ action, writeStatus: 202, accepted: true, expectedWrites: 1 });
                for (const redirect of [301, 302, 303, 307, 308]) {
                    for (const crossOrigin of [false, true]) {
                        await run({ action, stage: "write-redirect", redirect, crossOrigin, accepted: false, expectedWrites: 1 });
                        await run({ action, stage: "token-redirect", redirect, crossOrigin, accepted: false, expectedWrites: 0 });
                    }
                }
                for (const token of ["a b", "\n", "é", "a".repeat(4097)])
                    await run({ action, token, accepted: false, expectedWrites: 0 });
                for (const tokenStatus of [201, 202])
                    await run({ action, tokenStatus, accepted: false, expectedWrites: 0 });
                await run({ action, tokenType: "text/html", accepted: false, expectedWrites: 0 });
                await run({ action, tokenRaw: "{broken", accepted: false, expectedWrites: 0 });
            }
            console.log(name + ": " + cases + " privacy HTTP response/redirect cases passed.");
        } finally { await browser.close(); }
    }
} finally {
    await new Promise(done => server.close(done));
    await new Promise(done => foreignServer.close(done));
}
