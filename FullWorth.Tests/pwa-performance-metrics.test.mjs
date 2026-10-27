import test from "node:test";
import assert from "node:assert/strict";
import { requireDisposableOrigin, summarizeStaticResources, maximumLayoutShiftWindow, createReadTracker, unavailableBillFixtureId }
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

const request = (url, method = "GET") => ({ url: () => url, method: () => method, failure: () => null });

test("the exact denied admin probe completes at headers and records body cancellation separately", () => {
  const reads = createReadTracker("https://web.localhost");
  const req = request("https://web.localhost/bff/admin/users?skip=0&take=1");
  reads.started(req); reads.responded(req, 403); reads.failed(req, "net::ERR_ABORTED");
  const state = reads.snapshot();
  assert.equal(state.failures, 0);
  assert.equal(state.pending, 0);
  assert.equal(state.adminHeaderDenials, 1);
  assert.equal(state.expectedAdminBodyCancellations, 1);
  assert.equal(state.unexpectedAdminStatuses, 0);
  assert.deepEqual(state.records, [{ method: "GET", group: "admin-probe", status: 403, failureCode: "ERR_ABORTED" }]);
  assert.deepEqual(state.responseCounts, { 403: 1 });
});

test("read diagnostics never serialize private paths, queries or raw error strings", () => {
  const reads = createReadTracker("https://web.localhost");
  const req = request("https://web.localhost/bff/bill-streams/private-owner-id?token=secret");
  reads.started(req); reads.failed(req, "private token=secret");
  assert.equal(reads.snapshot().failures, 1);
  assert.equal(reads.snapshot().records[0].failureCode, "OTHER");
  assert.doesNotMatch(JSON.stringify(reads.snapshot()), /private|secret|token|https/);
});

test("late responses and failures from an older sample cannot contaminate a new sample", () => {
  let clock = 0;
  const reads = createReadTracker("https://web.localhost", () => clock);
  const old = request("https://web.localhost/bff/bill-streams");
  reads.started(old); reads.begin();
  const current = request("https://web.localhost/bff/alerts");
  reads.started(current);
  reads.responded(old, 503); reads.failed(old, "net::ERR_ABORTED");
  assert.equal(reads.snapshot().failures, 0);
  assert.deepEqual(reads.snapshot().responseCounts, {});
  assert.equal(reads.snapshot().pending, 1);
  reads.responded(current, 200); reads.finished(current);
  clock = 900;
  assert.equal(reads.snapshot().idleMs, 900);
  assert.equal(reads.snapshot().pending, 0);
  assert.deepEqual(reads.snapshot().responseCounts, { 200: 1 });
});

test("mutation counters and unknown transport failures are never treated as successful reads", () => {
  const reads = createReadTracker("https://web.localhost");
  const req = request("https://web.localhost/bff/account/preferences", "PUT");
  reads.started(req); reads.failed(req, "connection reset");
  const state = reads.snapshot();
  assert.equal(state.unsafe, 1);
  assert.equal(state.failures, 1);
  assert.equal(state.records[0].status, null);
  assert.equal(state.records[0].failureCode, "OTHER");
});

test("bounded diagnostic records cannot hide failures beyond the retained window", () => {
  const reads = createReadTracker("https://web.localhost");
  for (let i = 0; i < 101; i++) {
    const req = request("https://web.localhost/bff/bill-streams");
    reads.started(req);
    if (i === 100) reads.failed(req, "net::ERR_ABORTED");
    else { reads.responded(req, 200); reads.finished(req); }
  }
  const state = reads.snapshot();
  assert.equal(state.records.length, 100);
  assert.equal(state.recordsTruncated, true);
  assert.equal(state.requests, 101);
  assert.equal(state.failures, 1);
});

test("a denied financial read does not inherit the admin-body exception", () => {
  const reads = createReadTracker("https://web.localhost");
  const req = request("https://web.localhost/bff/bill-streams");
  reads.started(req); reads.responded(req, 403); reads.failed(req, "net::ERR_ABORTED");
  assert.equal(reads.snapshot().failures, 1);
  assert.equal(reads.snapshot().expectedAdminBodyCancellations, 0);
});

test("success or missing headers cannot turn a transport failure into an expected admin cancellation", () => {
  for (const status of [200, null]) {
    const reads = createReadTracker("https://web.localhost");
    const req = request("https://web.localhost/bff/admin/users");
    reads.started(req);
    if (status !== null) reads.responded(req, status);
    reads.failed(req, "net::ERR_ABORTED");
    assert.equal(reads.snapshot().failures, 1);
    assert.equal(reads.snapshot().expectedAdminBodyCancellations, 0);
    assert.equal(reads.snapshot().unexpectedAdminStatuses, status === 200 ? 1 : 0);
  }
});

test("unknown errors after denied admin headers remain real failures", () => {
  for (const reason of ["net::ERR_CONNECTION_RESET", "unknown cancelled privateURL"]) {
    const reads = createReadTracker("https://web.localhost");
    const req = request("https://web.localhost/bff/admin/users");
    reads.started(req); reads.responded(req, 403); reads.failed(req, reason);
    assert.equal(reads.snapshot().failures, 1);
    assert.equal(reads.snapshot().expectedAdminBodyCancellations, 0);
  }
});

test("later admin body disposal does not extend the already completed logical read window", () => {
  let clock = 0;
  const reads = createReadTracker("https://web.localhost", () => clock);
  const req = request("https://web.localhost/bff/admin/users");
  reads.started(req); reads.responded(req, 403);
  clock = 900;
  assert.equal(reads.snapshot().pending, 0);
  reads.failed(req, "net::ERR_ABORTED");
  assert.equal(reads.snapshot().idleMs, 900);
  assert.equal(reads.snapshot().expectedAdminBodyCancellations, 1);
});

test("a shift at the exact five-second boundary starts a new session", () => {
  const entries = [0, 900, 1800, 2700, 3600, 4500, 5000]
    .map(startTime => ({ startTime, value: .1 }));
  assert.equal(maximumLayoutShiftWindow(entries), .6);
});

test("only the selected fixed unavailable bill completes at404 headers and records disposal", () => {
  const reads = createReadTracker("https://web.localhost");
  reads.begin({ expectUnavailableBill: true });
  const req = request("https://web.localhost/bff/bill-streams/" + unavailableBillFixtureId);
  reads.started(req); reads.responded(req, 404);
  assert.equal(reads.snapshot().pending, 0);
  assert.equal(reads.snapshot().expectedUnavailableResponses, 1);
  reads.failed(req, "net::ERR_ABORTED");
  assert.equal(reads.snapshot().failures, 0);
  assert.equal(reads.snapshot().expectedUnavailableBodyCancellations, 1);
  assert.deepEqual(reads.snapshot().responseCounts, { 404: 1 });
  assert.doesNotMatch(JSON.stringify(reads.snapshot()), /11111111|https|bill-streams/);
});

test("other bills, modes, methods and statuses cannot inherit the unavailable-fixture rule", () => {
  for (const entry of [
    { selected: false, id: unavailableBillFixtureId, method: "GET", status: 404 },
    { selected: true, id: "22222222-2222-4222-8222-222222222222", method: "GET", status: 404 },
    { selected: true, id: unavailableBillFixtureId, method: "GET", status: 200 },
    { selected: true, id: unavailableBillFixtureId, method: "GET", status: 403 },
    { selected: true, id: unavailableBillFixtureId, method: "PUT", status: 404 }
  ]) {
    const reads = createReadTracker("https://web.localhost");
    reads.begin({ expectUnavailableBill: entry.selected });
    const req = request("https://web.localhost/bff/bill-streams/" + entry.id, entry.method);
    reads.started(req); reads.responded(req, entry.status);
    assert.equal(reads.snapshot().pending, 1);
    reads.failed(req, "net::ERR_ABORTED");
    assert.equal(reads.snapshot().failures, 1);
    assert.equal(reads.snapshot().expectedUnavailableResponses, 0);
    assert.equal(reads.snapshot().expectedUnavailableBodyCancellations, 0);
  }
});

test("an unknown transport failure after the expected404 remains a failure", () => {
  const reads = createReadTracker("https://web.localhost");
  reads.begin({ expectUnavailableBill: true });
  const req = request("https://web.localhost/bff/bill-streams/" + unavailableBillFixtureId);
  reads.started(req); reads.responded(req, 404); reads.failed(req, "net::ERR_CONNECTION_RESET");
  assert.equal(reads.snapshot().failures, 1);
  assert.equal(reads.snapshot().expectedUnavailableBodyCancellations, 0);
});

test("unavailable-fixture opt-in cannot leak into a later normal sample", () => {
  const reads = createReadTracker("https://web.localhost");
  reads.begin({ expectUnavailableBill: true }); reads.begin();
  const req = request("https://web.localhost/bff/bill-streams/" + unavailableBillFixtureId);
  reads.started(req); reads.responded(req, 404);
  assert.equal(reads.snapshot().pending, 1);
  assert.equal(reads.snapshot().expectedUnavailableResponses, 0);
});
