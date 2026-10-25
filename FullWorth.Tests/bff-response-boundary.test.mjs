import test from "node:test";
import assert from "node:assert/strict";
import { createServer } from "node:http";

const clientUrls = {
    bff: new URL("../FullWorth.Web/wwwroot/js/bff.js", import.meta.url).href,
    subscription: new URL("../FullWorth.Web/wwwroot/js/subscription-bff.js", import.meta.url).href
};
let instance = 0;
const client = name => import(clientUrls[name] + "#boundary-" + ++instance);
const clients = ["bff", "subscription"];
const original = { fetch: globalThis.fetch, window: globalThis.window, document: globalThis.document };
test.afterEach(() => Object.assign(globalThis, original));

function fixture() {
    const calls = [], navigations = [];
    globalThis.window = { location: {
        pathname: "/app/account", search: "", hash: "",
        assign: value => navigations.push(value)
    }};
    return { calls, navigations };
}
const json = body => Response.json(body);
function responseWithRedirect(response) {
    Object.defineProperty(response, "redirected", { value: true });
    return response;
}
function mutation(name, module) {
    return name === "bff"
        ? module.dismissAlert("synthetic-alert")
        : module.redeemAccessKey("synthetic-key");
}
function read(name, module) {
    return name === "bff" ? module.getBankConnections() : module.getSubscription();
}

for (const name of clients) {
    const invalid = {
        "HTML success": () => new Response("<html>Login required</html>", { headers: { "Content-Type": "text/html" } }),
        "text success": () => new Response("unavailable", { headers: { "Content-Type": "text/plain" } }),
        "missing content type": () => new Response(new Uint8Array([123, 125])),
        "JSON prefix impostor": () => new Response("{}", { headers: { "Content-Type": "application/jsonp" } }),
        "malformed JSON": () => new Response("{ private-synthetic-payload", { headers: { "Content-Type": "application/json" } }),
        "empty read": () => new Response(null, { status: 204 }),
        "redirected JSON": () => responseWithRedirect(json({ synthetic: true }))
    };
    for (const [label, response] of Object.entries(invalid)) {
        test(name + " rejects " + label + " without exposing payload text", async () => {
            fixture();
            const module = await client(name);
            globalThis.fetch = async () => response();
            await assert.rejects(read(name, module), error =>
                !error.message.includes("private-synthetic-payload") &&
                !error.message.includes("<html>") &&
                !(error instanceof SyntaxError));
        });
    }

    test(name + " accepts case-insensitive JSON with a charset", async () => {
        fixture();
        const module = await client(name), expected = { synthetic: "café" };
        globalThis.fetch = async (_url, options) => {
            assert.equal(options.credentials, "same-origin");
            assert.equal(options.cache, "no-store");
            assert.equal(options.redirect, "error");
            assert.equal(options.headers.Authorization, undefined);
            return new Response(JSON.stringify(expected), { headers: { "Content-Type": "Application/JSON; charset=utf-8" } });
        };
        assert.deepEqual(await read(name, module), expected);
    });

    for (const value of [null, undefined, "", "   ", 12, {}, [], true]) {
        test(name + " rejects invalid token " + JSON.stringify(value) + " before any write and can recover", async () => {
            const f = fixture(), module = await client(name);
            let valid = false;
            globalThis.fetch = async (url, options) => {
                f.calls.push({ url, options });
                if (url === "/bff/antiforgery")
                    return json({ requestToken: valid ? "valid-synthetic-token" : value });
                assert.equal(options.headers["X-CSRF-TOKEN"], "valid-synthetic-token");
                return json({ accepted: true });
            };
            await assert.rejects(mutation(name, module));
            assert.equal(f.calls.length, 1, "invalid token must not reach the mutation");
            valid = true;
            assert.deepEqual(await mutation(name, module), { accepted: true });
            assert.deepEqual(f.calls.map(c => c.url === "/bff/antiforgery"), [true, true, false]);
        });
    }

    for (const status of [400, 401, 403]) {
        test(name + " refreshes a rejected token only on a later user attempt after " + status, async () => {
            const f = fixture(), module = await client(name);
            let tokens = 0, writes = 0;
            globalThis.fetch = async (url, options) => {
                f.calls.push({ url, options });
                if (url === "/bff/antiforgery") return json({ requestToken: "synthetic-" + ++tokens });
                writes++;
                assert.equal(options.headers["X-CSRF-TOKEN"], "synthetic-" + tokens);
                return writes === 1 ? new Response(null, { status }) : json({ accepted: true });
            };
            await assert.rejects(mutation(name, module));
            assert.equal(tokens, 1);
            assert.equal(writes, 1, "a rejected write must not be automatically replayed");
            if (status === 401) assert.equal(f.navigations.length, 1);
            assert.deepEqual(await mutation(name, module), { accepted: true });
            assert.equal(tokens, 2);
            assert.equal(writes, 2);
        });
    }

    test(name + " preserves a valid cached token and never retries an uncertain network write", async () => {
        const f = fixture(), module = await client(name);
        let writes = 0, tokens = 0;
        globalThis.fetch = async (url, options) => {
            f.calls.push({ url, options });
            if (url === "/bff/antiforgery") { tokens++; return json({ requestToken: "synthetic" }); }
            if (++writes === 1) throw new TypeError("Synthetic lost response");
            return json({ accepted: true });
        };
        await assert.rejects(mutation(name, module), /lost response/);
        assert.equal(writes, 1);
        await mutation(name, module);
        assert.equal(writes, 2);
        assert.equal(tokens, 1);
    });

    test(name + " rejects a redirected token response before a write", async () => {
        const f = fixture(), module = await client(name);
        globalThis.fetch = async (url, options) => {
            f.calls.push({ url, options });
            return responseWithRedirect(json({ requestToken: "redirected-synthetic" }));
        };
        await assert.rejects(mutation(name, module));
        assert.equal(f.calls.length, 1);
    });

    for (const status of [301, 302, 303, 307, 308]) {
        test(name + " native Fetch blocks HTTP " + status + " before the redirect target receives a write", async () => {
            fixture();
            const module = await client(name);
            let targets = 0, writes = 0;
            const server = createServer((request, response) => {
                if (request.url === "/bff/antiforgery") {
                    response.writeHead(200, { "Content-Type": "application/json" });
                    response.end(JSON.stringify({ requestToken: "synthetic" }));
                } else if (request.url === "/unexpected-target") {
                    targets++;
                    response.writeHead(200, { "Content-Type": "application/json" });
                    response.end(JSON.stringify({ accepted: true }));
                } else {
                    writes++;
                    response.writeHead(status, { Location: "/unexpected-target" });
                    response.end();
                }
            });
            await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
            const origin = "http://127.0.0.1:" + server.address().port;
            globalThis.fetch = (url, options) => original.fetch(origin + url, options);
            try {
                await assert.rejects(mutation(name, module));
                assert.equal(writes, 1);
                assert.equal(targets, 0, "redirect bodies/destinations must never be followed");
            } finally {
                server.closeAllConnections();
                await new Promise(resolve => server.close(resolve));
            }
        });
    }
}

const bffWrites = {
    "disconnect": module => module.disconnectBankConnection("synthetic-bank"),
    "archive": module => module.setBankConnectionArchived("synthetic-bank", true),
    "restore": module => module.setBankConnectionArchived("synthetic-bank", false),
    "confirm bill": module => module.confirmBillStream("synthetic-bill"),
    "mark read": module => module.markAlertRead("synthetic-alert"),
    "refresh monitoring": module => module.refreshBillMonitoring(),
    "JSON planning write": module => module.putPlanningPaySchedule({ frequency: "Synthetic" }),
    "legacy deletion wrapper": module => module.deleteFullWorthAccount("DELETE"),
    "statement upload": module => {
        globalThis.document = { getElementById: () => ({
            files: [Object.assign(new Blob(["synthetic"], { type: "application/pdf" }), { name: "synthetic.pdf" })]
        }) };
        return module.uploadBillStatement("synthetic-bill", "synthetic-input");
    }
};
for (const [label, write] of Object.entries(bffWrites)) {
    test("BFF " + label + " rejects HTML success instead of reporting a completed action", async () => {
        const f = fixture(), module = await client("bff");
        globalThis.fetch = async (url, options) => {
            f.calls.push({ url, options });
            assert.equal(options.redirect, "error");
            return url === "/bff/antiforgery" ? json({ requestToken: "synthetic" })
                : new Response("<html>Proxy failure</html>", { headers: { "Content-Type": "text/html" } });
        };
        await assert.rejects(write(module));
        assert.equal(f.calls.length, 2);
    });
}

test("BFF permits genuine 204 writes and preserves the explicit missing-pay-schedule state", async () => {
    fixture();
    const module = await client("bff");
    globalThis.fetch = async url => url === "/bff/antiforgery"
        ? json({ requestToken: "synthetic" }) : new Response(null, { status: 204 });
    assert.equal(await module.disconnectBankConnection("synthetic-bank"), null);
    assert.equal(await module.putPlanningPaySchedule({}), null);
    assert.equal(await module.deleteFullWorthAccount("DELETE"), true);
    globalThis.fetch = async (_url, options) => {
        assert.equal(options.redirect, "error");
        return new Response(null, { status: 404 });
    };
    assert.equal(await module.getPlanningPaySchedule(), null);
});

test("BFF preserves throttle metadata without retrying writes", async () => {
    const f = fixture(), module = await client("bff");
    globalThis.fetch = async (url, options) => {
        f.calls.push({ url, options });
        return url === "/bff/antiforgery" ? json({ requestToken: "synthetic" })
            : new Response(null, { status: 429, headers: { "Retry-After": "120" } });
    };
    await assert.rejects(module.completePlaidLinkSession("synthetic-session"),
        error => error.status === 429 && error.retryAfterMs === 120000);
    assert.equal(f.calls.length, 2);
});

test("BFF cancellation during security-token acquisition cannot submit a delayed Link write", async () => {
    const f = fixture(), module = await client("bff"), abort = new AbortController();
    let release;
    globalThis.fetch = async (url, options) => {
        f.calls.push({ url, options });
        return new Promise(resolve => { release = resolve; });
    };
    const request = module.createPlaidLinkSession(abort.signal);
    abort.abort();
    release(json({ requestToken: "synthetic" }));
    await assert.rejects(request, error => error.name === "AbortError");
    assert.equal(f.calls.length, 1);
});

test("subscription redirects only after a valid JSON checkout or portal response", async () => {
    const f = fixture(), module = await client("subscription");
    globalThis.fetch = async (url, options) => {
        f.calls.push({ url, options });
        assert.equal(options.redirect, "error");
        return url === "/bff/antiforgery" ? json({ requestToken: "synthetic" })
            : json({ url: "https://checkout.stripe.com/synthetic" });
    };
    await module.startCheckout("monthly");
    await module.openBillingPortal();
    assert.equal(f.navigations.length, 2);
    assert.equal(f.calls.filter(c => c.url === "/bff/antiforgery").length, 1);
});

test("only latest-upload discovery accepts a genuine empty read", async () => {
    fixture();
    const module = await client("bff");
    globalThis.fetch = async () => new Response(null, { status: 204 });
    assert.equal(await module.getLatestBillStatementUpload("synthetic-bill"), null);
    await assert.rejects(module.getBillStatementUploadStatus("synthetic-bill", "synthetic-upload"));
    await assert.rejects(module.getAlerts());
});

test("JSON mutation results and explicitly missing schedules retain their existing contracts", async () => {
    fixture();
    const module = await client("bff"), expected = { synthetic: true };
    globalThis.fetch = async url => url === "/bff/antiforgery"
        ? json({ requestToken: "synthetic" })
        : new Response(JSON.stringify(expected), { status: 201, headers: { "Content-Type": "application/json" } });
    assert.deepEqual(await module.confirmBillStream("synthetic-bill"), expected);
});
