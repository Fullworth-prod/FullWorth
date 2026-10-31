import assert from "node:assert/strict";
import test from "node:test";
import vm from "node:vm";
import { readFile } from "node:fs/promises";

const source = await readFile(new URL("../FullWorth.Web/wwwroot/js/pwa.js", import.meta.url), "utf8");
class Events {
    listeners = new Map();
    addEventListener(type, callback, options) {
        const entries = this.listeners.get(type) || [];
        entries.push({ callback, once: !!options?.once });
        this.listeners.set(type, entries);
    }
    removeEventListener(type, callback) {
        this.listeners.set(type, (this.listeners.get(type) || []).filter(entry => entry.callback !== callback));
    }
    count(type) { return (this.listeners.get(type) || []).length; }
    async dispatch(type) {
        const pending = [];
        for (const entry of [...(this.listeners.get(type) || [])]) {
            if (entry.once) this.removeEventListener(type, entry.callback);
            pending.push(entry.callback({ type }));
        }
        await Promise.all(pending);
    }
}
async function harness({ waiting = true, workerThrows = false, unsupported = false,
    synchronousChange = false, registerThrows = false } = {}) {
    const window = new Events();
    const serviceWorker = new Events();
    const registration = new Events();
    const timers = new Map();
    const callbacks = [];
    const messages = [];
    let nextTimer = 0, reloads = 0, updates = 0;
    const worker = { postMessage(message) {
        messages.push(message.type);
        if (workerThrows) throw new Error("synthetic redundant worker");
        if (synchronousChange) {
            serviceWorker.controller = worker;
            void serviceWorker.dispatch("controllerchange");
        }
    }};
    registration.waiting = waiting ? worker : null;
    registration.update = async () => { updates++; };
    serviceWorker.controller = {};
    serviceWorker.register = async (url, options) => {
        assert.equal(url, "/service-worker.js");
        assert.equal(options.scope, "/");
        assert.equal(options.updateViaCache, "none");
        if (registerThrows) throw new Error("synthetic registration failure");
        return registration;
    };
    const navigator = unsupported ? {} : { serviceWorker };
    window.navigator = navigator;
    window.location = { reload() { reloads++; } };
    window.setTimeout = (callback, delay) => {
        assert.equal(delay, 10000);
        callbacks.push(callback);
        timers.set(++nextTimer, callback);
        return nextTimer;
    };
    window.clearTimeout = id => timers.delete(id);
    vm.runInNewContext(source, { window, navigator, Boolean, Promise });
    await window.dispatch("load");
    return {
        api: window.FullWorthPwa, registration, serviceWorker, worker, timers, callbacks, messages,
        reloads: () => reloads, updates: () => updates,
        activate(target = worker) { serviceWorker.controller = target; return serviceWorker.dispatch("controllerchange"); },
        expire() {
            for (const [id, callback] of [...timers]) {
                timers.delete(id); callback();
            }
        }
    };
}
function clean(h) {
    assert.equal(h.timers.size, 0, "A settled attempt must not retain its timeout.");
    assert.equal(h.serviceWorker.count("controllerchange"), 0,
        "A settled attempt must not retain a reload listener.");
}
test("explicit update reloads once and releases the listener/timer", async () => {
    const h = await harness();
    const pending = h.api.applyUpdate();
    assert.deepEqual(h.messages, ["SKIP_WAITING"]);
    await h.activate();
    assert.equal(await pending, "reloaded");
    assert.equal(h.reloads(), 1);
    clean(h);
    await h.activate();
    h.callbacks[0]();
    assert.equal(h.reloads(), 1);
});
test("timeout cannot turn a later controller change into an unexpected reload", async () => {
    const h = await harness();
    const pending = h.api.applyUpdate();
    h.expire();
    assert.equal(await pending, "timeout");
    await h.activate();
    assert.equal(h.reloads(), 0);
    clean(h);
    assert.deepEqual(h.messages, ["SKIP_WAITING"]);
});
test("an explicit retry after timeout owns exactly one reload", async () => {
    const h = await harness();
    const first = h.api.applyUpdate();
    h.expire();
    assert.equal(await first, "timeout");
    const second = h.api.applyUpdate();
    await h.activate();
    assert.equal(await second, "reloaded");
    assert.equal(h.reloads(), 1);
    assert.deepEqual(h.messages, ["SKIP_WAITING", "SKIP_WAITING"]);
    clean(h);
});
test("a redundant worker message failure leaves no future reload or automatic replay", async () => {
    const h = await harness({ workerThrows: true });
    assert.equal(await h.api.applyUpdate(), "unavailable");
    clean(h);
    await h.activate();
    assert.equal(h.reloads(), 0);
    assert.deepEqual(h.messages, ["SKIP_WAITING"]);
});
test("synchronous controller observation still settles once and clears resources", async () => {
    const h = await harness({ synchronousChange: true });
    assert.equal(await h.api.applyUpdate(), "reloaded");
    assert.equal(h.reloads(), 1);
    clean(h);
});
test("no waiting worker checks for an update without a reload listener", async () => {
    const h = await harness({ waiting: false });
    assert.equal(await h.api.applyUpdate(), "unavailable");
    assert.equal(h.updates(), 1);
    assert.deepEqual(h.messages, []);
    assert.equal(h.reloads(), 0);
    clean(h);
});
test("a worker discovered by the explicit update can complete normally", async () => {
    const h = await harness({ waiting: false });
    h.registration.update = async () => { h.registration.waiting = h.worker; };
    const pending = h.api.applyUpdate();
    await new Promise(resolve => setImmediate(resolve));
    await h.activate();
    assert.equal(await pending, "reloaded");
    assert.equal(h.reloads(), 1);
    clean(h);
});
test("a failed browser update check does not install a future reload listener", async () => {
    const h = await harness({ waiting: false });
    h.registration.update = async () => { throw new Error("synthetic update rejection"); };
    await assert.rejects(h.api.applyUpdate(), /synthetic update rejection/);
    clean(h);
    await h.activate();
    assert.equal(h.reloads(), 0);
});
test("repeated explicit timeouts do not accumulate reloads or live resources", async () => {
    const h = await harness();
    for (let attempt = 0; attempt < 8; attempt++) {
        const pending = h.api.applyUpdate();
        h.expire();
        assert.equal(await pending, "timeout");
        clean(h);
    }
    await h.activate();
    assert.equal(h.reloads(), 0);
    assert.equal(h.messages.length, 8);
});
for (const options of [{ unsupported: true }, { registerThrows: true }]) {
    test("unavailable registration stays nonblocking: " + JSON.stringify(options), async () => {
        const h = await harness(options);
        assert.equal(await h.api.applyUpdate(), "unavailable");
        assert.equal(h.reloads(), 0);
        assert.deepEqual(h.messages, []);
        clean(h);
    });
}

test("explicit retry reloads the exact requested worker if it activated after timeout", async () => {
    const h = await harness();
    const first = h.api.applyUpdate();
    h.expire();
    assert.equal(await first, "timeout");
    h.registration.waiting = null;
    h.serviceWorker.controller = h.worker;
    await h.serviceWorker.dispatch("controllerchange");
    assert.equal(h.reloads(), 0, "Late activation must not reload without a fresh user action.");
    assert.equal(await h.api.applyUpdate(), "reloaded");
    assert.equal(h.reloads(), 1);
    assert.deepEqual(h.messages, ["SKIP_WAITING"], "An active worker must not receive an automatic replay.");
    clean(h);
});
test("same script URL is not proof that the requested worker became controller", async () => {
    const h = await harness();
    h.worker.scriptURL = "/service-worker.js";
    const first = h.api.applyUpdate();
    h.expire();
    assert.equal(await first, "timeout");
    h.registration.waiting = null;
    h.serviceWorker.controller = { scriptURL: "/service-worker.js" };
    await h.serviceWorker.dispatch("controllerchange");
    assert.equal(await h.api.applyUpdate(), "unavailable");
    assert.equal(h.reloads(), 0);
    assert.deepEqual(h.messages, ["SKIP_WAITING"]);
    clean(h);
});
test("a failed worker message does not authorize a later active-worker retry shortcut", async () => {
    const h = await harness({ workerThrows: true });
    assert.equal(await h.api.applyUpdate(), "unavailable");
    h.registration.waiting = null;
    h.serviceWorker.controller = h.worker;
    assert.equal(await h.api.applyUpdate(), "unavailable");
    assert.equal(h.reloads(), 0);
    clean(h);
});
test("a newer waiting worker takes priority over reloading a previously requested controller", async () => {
    const h = await harness();
    const first = h.api.applyUpdate();
    h.expire();
    assert.equal(await first, "timeout");
    h.serviceWorker.controller = h.worker;
    const next = { postMessage(message) { h.messages.push("new:" + message.type); } };
    h.registration.waiting = next;
    const pending = h.api.applyUpdate();
    assert.equal(h.reloads(), 0);
    assert.deepEqual(h.messages, ["SKIP_WAITING", "new:SKIP_WAITING"]);
    h.serviceWorker.controller = next;
    await h.serviceWorker.dispatch("controllerchange");
    assert.equal(await pending, "reloaded");
    assert.equal(h.reloads(), 1);
    clean(h);
});

test("an unrelated controller change cannot confirm the requested waiting worker", async () => {
    const h = await harness();
    const pending = h.api.applyUpdate();
    await h.activate({ scriptURL: "/service-worker.js" });
    assert.equal(h.reloads(), 0, "A different worker must not be reported as the requested update.");
    assert.equal(h.serviceWorker.count("controllerchange"), 1);
    await h.activate(h.worker);
    assert.equal(await pending, "reloaded");
    assert.equal(h.reloads(), 1);
    clean(h);
});
