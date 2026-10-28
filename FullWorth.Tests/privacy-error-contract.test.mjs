import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const source = name => readFile(new URL("../FullWorth.Web/wwwroot/js/" + name, import.meta.url), "utf8");
const data = text => "data:text/javascript;base64," + Buffer.from(text).toString("base64");
const sharedUrl = data(await source("account-privacy-requests.js"));
const { privacyError } = await import(sharedUrl);
const modules = {};
for (const action of ["delete", "export"])
    modules[action] = await import(data((await source("account-" + action + ".js"))
        .replace("./account-privacy-requests.js", sharedUrl)));

const unknown = "FullWorth could not confirm this privacy request.";
const expired = "FullWorth session expired.";
const rate = "Too many attempts. Please wait before trying again.";
const receipts = [
    [409, "FullWorth staff accounts cannot be self-deleted while privileged roles are assigned. Remove the staff roles through the authorized admin workflow first.", "Staff accounts cannot be self-deleted."],
    [503, "FullWorth could not safely finish deleting your account because a bank connection could not be revoked. Your FullWorth account was not deleted. Try again shortly.", "A bank connection could not be revoked."],
    [500, "FullWorth could not securely prepare stored statement files for deletion. Your FullWorth account was not deleted. Try again.", "Stored statement preparation failed; account was not deleted."]
];
const proofErrors = [
    ["Current password is incorrect.", "Current password is incorrect."],
    ["A current authenticator code is required.", "A valid current authenticator code is required."],
    ["The authenticator code is invalid.", "A valid current authenticator code is required."]
];

for (const action of ["delete", "export", undefined])
for (const status of [500, 502, 503, 504])
for (const [index, message] of [
    "Current password is incorrect.", "The authenticator code is invalid.",
    "Stored statement preparation failed; account was not deleted."
].entries())
    test(String(action) + " server status " + status + " cannot become proof/nondeletion claim " + index, async () => {
        assert.equal(await privacyError(Response.json({ message }, { status }), action), unknown);
    });

for (const action of ["delete", "export", undefined])
for (const [receiptStatus, message, expected] of receipts)
for (const status of [400, 401, 403, 404, 409, 429, 500, 502, 503, 504])
    test(String(action) + " deletion receipt " + receiptStatus + " bound to status " + status, async () => {
        const result = await privacyError(Response.json({ message }, { status }), action);
        assert.equal(result, status === 429 ? rate : status === 401 ? expired :
            action === "delete" && status === receiptStatus ? expected : unknown);
    });

for (const action of ["delete", "export"])
for (const [message, expected] of proofErrors)
for (const property of ["message", "title"])
    test(action + " documented401 " + property + " " + message, async () => {
        assert.equal(await privacyError(Response.json({ [property]: message }, { status: 401 }), action), expected);
    });

for (const action of ["delete", "export"])
for (const [index, payload] of [
    { message: "Not true: Current password is incorrect." },
    { message: "Current password is incorrect. synthetic-private-detail" },
    { title: "The authenticator code is invalid. More private context" },
    ["Current password is incorrect."], "Current password is incorrect.", null, 42
].entries())
    test(action + " unknown401 shape or substring " + index + " remains session/unknown", async () => {
        const result = await privacyError(Response.json(payload, { status: 401 }), action);
        assert.equal(result, expired);
        assert.ok(!result.includes("synthetic-private-detail"));
    });

for (const [status, message, expected] of receipts) {
    for (const [index, reply] of [
        () => Response.json({ title: message }, { status }),
        () => Response.json({ message, deleted: true }, { status }),
        () => Response.json({ message: message + " synthetic-private-detail" }, { status }),
        () => Response.json({ message: "Untrusted prefix " + message }, { status }),
        () => new Response(JSON.stringify({ message }), { status, headers: { "content-type": "text/html" } }),
        () => new Response(JSON.stringify({ message }), { status, headers: { "content-type": "application/problem+json" } }),
        () => new Response("{broken", { status, headers: { "content-type": "application/json" } })
    ].entries())
        test("predelete receipt " + status + " rejects wrong envelope " + index, async () => {
            assert.equal(await privacyError(reply(), "delete"), unknown);
        });
    test("predelete receipt " + status + " accepts normalized JSON MIME and exact message", async () => {
        const reply = new Response(JSON.stringify({ message }), { status,
            headers: { "content-type": "Application/JSON; charset=utf-8" } });
        assert.equal(await privacyError(reply, "delete"), expected);
    });
}

for (const action of ["delete", "export", undefined])
for (const status of [200, 202, 204, 302, 429, 502, 504])
    test(String(action) + " short-circuit " + status + " does not parse a body", async () => {
        let reads = 0;
        const response = { status, redirected: false, headers: new Headers(),
            json() { reads++; throw Error("synthetic-private-body"); } };
        assert.equal(await privacyError(response, action), status === 429 ? rate : unknown);
        assert.equal(reads, 0);
    });
test("token endpoint cannot report password proof failure", async () => {
    assert.equal(await privacyError(Response.json({ message: "Current password is incorrect." }, { status: 401 })), expired);
});
test("redirected response cannot declare a known failure", async () => {
    const response = Response.json({ message: receipts[0][1] }, { status: 409 });
    Object.defineProperty(response, "redirected", { value: true });
    assert.equal(await privacyError(response, "delete"), unknown);
});

async function attempt(action, failure, failToken = false) {
    const previous = globalThis.fetch;
    const calls = [];
    globalThis.fetch = async (url, options) => {
        calls.push({ url, options });
        if (url === "/bff/antiforgery" && !failToken)
            return Response.json({ requestToken: "synthetic-csrf" });
        return failure();
    };
    try {
        let error;
        try {
            if (action === "delete") await modules.delete.deleteFullWorthAccount("DELETE", "Synthetic!Proof123", null);
            else await modules.export.downloadAccountExport("Synthetic!Proof123", null);
        } catch (failure) { error = failure; }
        assert.ok(error);
        assert.equal(calls.length, failToken ? 1 : 2, "No automatic replay or extra token request.");
        if (!failToken) assert.equal(calls[1].options.redirect, "error");
        return error.message;
    } finally { globalThis.fetch = previous; modules.export.cancelAccountExport(); }
}
for (const action of ["delete", "export"])
for (const status of [500, 502, 503, 504])
    test(action + " actual module preserves uncertain server failure " + status, async () => {
        assert.equal(await attempt(action, () => Response.json({ message: "Current password is incorrect." }, { status })), unknown);
    });
for (const [status, message, expected] of receipts)
    test("actual deletion module preserves explicit predelete " + status, async () => {
        assert.equal(await attempt("delete", () => Response.json({ message }, { status })), expected);
    });
for (const action of ["delete", "export"])
    test(action + " token failure cannot blame password or issue a write", async () => {
        assert.equal(await attempt(action, () => Response.json({ message: "Current password is incorrect." }, { status: 401 }), true), expired);
    });
