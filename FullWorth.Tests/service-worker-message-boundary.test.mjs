import assert from "node:assert/strict";
import test from "node:test";
import vm from "node:vm";
import { readFile } from "node:fs/promises";

const source = await readFile(new URL("../FullWorth.Web/wwwroot/service-worker.js", import.meta.url), "utf8");
const origin = "https://fullworth.example";
function receiver() {
    const listeners = new Map();
    let activations = 0;
    const self = {
        location: { origin },
        addEventListener(type, listener) { listeners.set(type, listener); },
        skipWaiting() { activations++; return Promise.resolve(); }
    };
    vm.runInNewContext(source, { self, URL });
    return {
        send(event) { listeners.get("message")(event); },
        activations: () => activations
    };
}
test("same-origin explicit SKIP_WAITING activates once per delivered command", () => {
    const h = receiver();
    h.send({ origin, data: { type: "SKIP_WAITING" } });
    assert.equal(h.activations(), 1);
});
for (const [label, value] of [
    ["foreign", "https://outsider.example"],
    ["suffix lookalike", origin + ".outsider.example"],
    ["prefix lookalike", "https://fullworth.example-outsider.example"],
    ["scheme mismatch", "http://fullworth.example"],
    ["port mismatch", origin + ":8443"],
    ["opaque", "null"], ["empty", ""], ["missing", undefined]
]) {
    test(label + " origin cannot activate the waiting worker", () => {
        const h = receiver();
        h.send({ origin: value, data: { type: "SKIP_WAITING" } });
        assert.equal(h.activations(), 0);
    });
}
for (const [label, data] of [
    ["missing", undefined], ["null", null], ["empty", {}],
    ["unknown command", { type: "INITIALIZE" }],
    ["wrong case", { type: "skip_waiting" }],
    ["primitive", "SKIP_WAITING"]
]) {
    test("same-origin " + label + " message cannot activate the waiting worker", () => {
        const h = receiver();
        h.send({ origin, data });
        assert.equal(h.activations(), 0);
    });
}
test("rejected messages do not prevent a later explicit authorized update", () => {
    const h = receiver();
    h.send({ origin: "https://outsider.example", data: { type: "SKIP_WAITING" } });
    h.send({ origin, data: { type: "unknown" } });
    assert.equal(h.activations(), 0);
    h.send({ origin, data: { type: "SKIP_WAITING" } });
    assert.equal(h.activations(), 1);
});
