import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

export async function verifyNavigationViewportModule() {
  const source = await readFile(new URL("../FullWorth.Web/wwwroot/js/navigation-viewport.js", import.meta.url), "utf8");
  const module = await import("data:text/javascript;base64," + Buffer.from(source).toString("base64"));
  const names = ["window", "document", "getComputedStyle", "ResizeObserver", "MutationObserver",
    "requestAnimationFrame", "cancelAnimationFrame", "fetch"];
  const original = Object.fromEntries(names.map(name => [name, globalThis[name]]));
  let height = 72, bottom = "8px", display = "grid", requests = 0, next = 0;
  const frames = new Map(), listeners = new Map(), resizes = [], mutations = [];
  const properties = new Map();
  const bar = { getBoundingClientRect: () => ({ height }) };
  const nav = {};
  const shell = { querySelector: selector => selector === ".app-sidebar" ? bar : nav,
    style: { getPropertyValue: name => properties.get(name), setProperty: (name, value) => properties.set(name, value),
      removeProperty: name => properties.delete(name) } };
  const anchor = { closest: selector => selector === ".app-shell" ? shell : null };
  const flush = () => { const pending = [...frames.values()]; frames.clear(); pending.forEach(fn => fn()); };
  class Observer {
    constructor(callback) { this.callback = callback; this.disconnected = false; this.targets = []; }
    observe(...args) { this.targets.push(args); }
    disconnect() { this.disconnected = true; }
  }
  try {
    globalThis.window = { addEventListener: (name, handler) => {
      if (!listeners.has(name)) listeners.set(name, new Set()); listeners.get(name).add(handler);
    }, removeEventListener: (name, handler) => listeners.get(name)?.delete(handler) };
    globalThis.document = { documentElement: {}, fonts: { ready: Promise.resolve() } };
    globalThis.getComputedStyle = element => element === nav ? { display } : { bottom };
    globalThis.requestAnimationFrame = handler => { frames.set(++next, handler); return next; };
    globalThis.cancelAnimationFrame = id => frames.delete(id);
    globalThis.ResizeObserver = class extends Observer { constructor(fn) { super(fn); resizes.push(this); } };
    globalThis.MutationObserver = class extends Observer { constructor(fn) { super(fn); mutations.push(this); } };
    globalThis.fetch = () => { requests++; throw Error("Layout measurement must not use the network."); };

    assert.equal(module.observeNavigation(null), 0);
    assert.equal(module.observeNavigation({ closest: () => null }), 0);
    const lease = module.observeNavigation(anchor);
    assert.ok(lease > 0);
    assert.equal(properties.get("--fw-navigation-inset"), "100px");
    await Promise.resolve(); flush();
    assert.equal(resizes[0].targets.length, 2);
    assert.deepEqual(mutations[0].targets[0][1].attributeFilter, ["style", "data-fullworth-text-size", "class"]);
    height = 180; bottom = "12px";
    for (let i = 0; i < 50; i++) resizes[0].callback();
    assert.equal(frames.size, 1, "Resize work must be coalesced.");
    flush();
    assert.equal(properties.get("--fw-navigation-inset"), "212px");
    display = "none"; resizes[0].callback(); flush();
    assert.equal(properties.get("--fw-navigation-inset"), "0px", "Desktop must not reserve mobile clearance.");
    display = "grid"; height = 84; bottom = "auto";
    mutations[0].callback(); flush();
    assert.equal(properties.get("--fw-navigation-inset"), "104px");

    const replacement = module.observeNavigation(anchor);
    assert.ok(resizes[0].disconnected && mutations[0].disconnected);
    assert.equal(listeners.get("resize").size, 1);
    module.disconnectNavigation(lease);
    assert.equal(properties.get("--fw-navigation-inset"), "104px", "Old cleanup must not clear a newer observer.");
    resizes.at(-1).callback();
    assert.equal(frames.size, 1);
    module.disconnectNavigation(replacement);
    assert.equal(frames.size, 0);
    assert.equal(properties.has("--fw-navigation-inset"), false);
    assert.equal(listeners.get("resize").size, 0);
    assert.equal(listeners.get("orientationchange").size, 0);
    await Promise.resolve(); flush();
    assert.equal(properties.has("--fw-navigation-inset"), false, "Late font readiness must not revive a disposed observer.");
    module.disconnectNavigation(replacement);

    globalThis.ResizeObserver = undefined; globalThis.MutationObserver = undefined;
    const fallback = module.observeNavigation(anchor);
    height = 90;
    for (const handler of listeners.get("resize")) handler();
    flush();
    assert.equal(properties.get("--fw-navigation-inset"), "110px");
    module.disconnectNavigation(fallback);
    globalThis.ResizeObserver = class { observe() { throw Error("unavailable"); } disconnect() {} };
    assert.throws(() => module.observeNavigation(anchor));
    assert.equal(properties.has("--fw-navigation-inset"), false);
    assert.equal(listeners.get("resize").size, 0);
    assert.equal(requests, 0);
    console.log("Navigation geometry module: measured inset, resize coalescing, desktop reset, replacement/disposal, late callbacks, fallback and zero network passed.");
  } finally {
    for (const name of names) globalThis[name] = original[name];
  }
}
