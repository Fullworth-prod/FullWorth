import test from "node:test";
import assert from "node:assert/strict";
import { requireDisposableOrigin, summarizeStaticResources, maximumLayoutShiftWindow }
  from "../FullWorth.VisualTests/performance-metrics.mjs";

test("performance probe rejects production, insecure, misleading and credentialed origins", () => {
  assert.equal(requireDisposableOrigin("https://web.localhost:8443/app"), "https://web.localhost:8443");
  for (const origin of ["https://fullworth.org", "http://web.localhost",
    "https://web.localhost.other.invalid", "https://user:secret@web.localhost"])
    assert.throws(() => requireDisposableOrigin(origin), /disposable/);
});

test("resource sizes exclude cross-origin and financial endpoints without leaking names", () => {
  const result = summarizeStaticResources([
    { name: "https://web.localhost/app.css?secret=private", transferSize: 150, encodedBodySize: 100, decodedBodySize: 500 },
    { name: "https://web.localhost/app.js", transferSize: 0, encodedBodySize: 60, decodedBodySize: 200 },
    { name: "https://other.invalid/app.css", transferSize: 0, encodedBodySize: 0, decodedBodySize: 0 },
    { name: "https://web.localhost/bff/private-owner-id.css?token=private", transferSize: 400, encodedBodySize: 300, decodedBodySize: 800 }
  ], "https://web.localhost");
  assert.deepEqual(result.css, { entries: 1, knownSizeEntries: 1, sizingComplete: true,
    transferBytes: 150, encodedBodyBytes: 100, decodedBodyBytes: 500, browserCacheHints: 0 });
  assert.equal(result.js.browserCacheHints, 1);
  assert.equal(result.js.transferBytes, 0);
  assert.doesNotMatch(JSON.stringify(result), /private|secret|token|https/);
});

test("unavailable all-zero sizing is unknown, never a fabricated zero-byte success", () => {
  const unavailable = summarizeStaticResources([
    { name: "https://web.localhost/app.css", transferSize: 0, encodedBodySize: 0, decodedBodySize: 0 },
    { name: "https://web.localhost/app.js", transferSize: -1, encodedBodySize: 20, decodedBodySize: 20 }
  ], "https://web.localhost");
  assert.equal(unavailable.css.transferBytes, null);
  assert.equal(unavailable.css.sizingComplete, false);
  assert.equal(unavailable.js.transferBytes, null);
  const empty = summarizeStaticResources([], "https://web.localhost");
  assert.equal(empty.css.entries, 0);
  assert.equal(empty.css.transferBytes, 0);
});

test("layout shift uses the largest bounded session window and excludes recent input", () => {
  const entries = [
    { startTime: 100, value: .1 }, { startTime: 700, value: .15 },
    { startTime: 2000, value: .2 }, { startTime: 2100, value: 1, hadRecentInput: true },
    { startTime: NaN, value: 5 }, { startTime: 2300, value: -1 }
  ];
  assert.equal(maximumLayoutShiftWindow(entries.reverse()), .25);
  assert.equal(maximumLayoutShiftWindow([]), 0);
});

test("a shift session cannot grow beyond five seconds even with short gaps", () => {
  const entries = Array.from({ length: 8 }, (_, i) => ({ startTime: i * 900, value: .1 }));
  assert.equal(maximumLayoutShiftWindow(entries), .6);
  assert.equal(maximumLayoutShiftWindow([
    { startTime: 0, value: .2 }, { startTime: 1000, value: .3 }
  ]), .3);
});
