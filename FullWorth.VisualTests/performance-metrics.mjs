// Pure measurement rules shared by the CI browser probe and negative regressions.
// Never return resource URLs, query strings, request bodies or account data.
export function requireDisposableOrigin(value) {
  const url = new URL(value);
  if (url.protocol !== "https:" || !url.hostname.endsWith(".localhost") ||
      url.username || url.password)
    throw new Error("PWA measurements require the disposable HTTPS .localhost fixture.");
  return url.origin;
}

export function summarizeStaticResources(entries, origin) {
  const groups = { css: [], js: [] };
  for (const entry of entries) {
    let url;
    try { url = new URL(entry.name, origin); } catch { continue; }
    if (url.origin !== origin || url.pathname.startsWith("/bff/") ||
        url.pathname.startsWith("/api/")) continue;
    const kind = /\.css$/i.test(url.pathname) ? "css"
      : /\.(?:m?js)$/i.test(url.pathname) ? "js" : null;
    if (kind) groups[kind].push(entry);
  }
  const summarize = values => {
    const valid = values.filter(entry =>
      ["transferSize", "encodedBodySize", "decodedBodySize"].every(key =>
        Number.isFinite(entry[key]) && entry[key] >= 0) &&
      (entry.transferSize > 0 || entry.encodedBodySize > 0 || entry.decodedBodySize > 0));
    const sum = key => values.length === 0 ? 0 : valid.length === 0 ? null
      : valid.reduce((total, entry) => total + entry[key], 0);
    return {
      entries: values.length,
      knownSizeEntries: valid.length,
      sizingComplete: valid.length === values.length,
      transferBytes: sum("transferSize"),
      encodedBodyBytes: sum("encodedBodySize"),
      decodedBodyBytes: sum("decodedBodySize"),
      browserCacheHints: valid.filter(entry => entry.transferSize === 0 &&
        entry.decodedBodySize > 0).length
    };
  };
  return { css: summarize(groups.css), js: summarize(groups.js) };
}

export function maximumLayoutShiftWindow(entries) {
  const eligible = entries.filter(entry => !entry.hadRecentInput &&
    Number.isFinite(entry.startTime) && entry.startTime >= 0 &&
    Number.isFinite(entry.value) && entry.value >= 0)
    .sort((a, b) => a.startTime - b.startTime);
  let best = 0, score = 0, start = -Infinity, last = -Infinity;
  for (const entry of eligible) {
    if (entry.startTime - last >= 1000 || entry.startTime - start >= 5000) {
      score = 0;
      start = entry.startTime;
    }
    score += entry.value;
    last = entry.startTime;
    best = Math.max(best, score);
  }
  return Number(best.toFixed(6));
}

export const unavailableBillFixtureId = "11111111-1111-4111-8111-111111111111";

export function createReadTracker(origin, now = () => performance.now()) {
  let lease = 0, state;
  const entries = new WeakMap();
  function begin(options = {}) {
    lease++;
    state = { pending: new Set(), counts: {}, failures: 0, unsafe: 0,
      records: [], total: 0, adminHeaderDenials: 0, expectedAdminBodyCancellations: 0,
      unexpectedAdminStatuses: 0, expectedUnavailableResponses: 0,
      expectedUnavailableBodyCancellations: 0,
      expectUnavailableBill: options.expectUnavailableBill === true, lastActivity: now() };
  }
  function current(request) {
    const entry = entries.get(request);
    return entry?.lease === lease ? entry : null;
  }
  function started(request) {
    let url;
    try { url = new URL(request.url()); } catch { return; }
    if (url.origin !== origin || !url.pathname.startsWith("/bff/")) return;
    const rawMethod = request.method();
    const method = ["GET", "HEAD", "OPTIONS", "POST", "PUT", "PATCH", "DELETE"].includes(rawMethod)
      ? rawMethod : "OTHER";
    const group = url.pathname === "/bff/admin/users" ? "admin-probe"
      : url.pathname === "/bff/account/preferences" ? "preferences"
      : url.pathname === "/bff/bank-connections" ? "bank-connections"
      : url.pathname === "/bff/bank-accounts" ? "bank-accounts"
      : url.pathname === "/bff/bill-streams" ? "bill-streams"
      : url.pathname.startsWith("/bff/bill-streams/") ? "bill-detail"
      : url.pathname === "/bff/alerts" ? "alerts" : "other-bff";
    const entry = { lease, method, group, status: null, failureCode: null,
      wireComplete: false, deniedAdminHeaders: false, unavailableHeaders: false,
      unavailableFixture: state.expectUnavailableBill && method === "GET" &&
        url.pathname === "/bff/bill-streams/" + unavailableBillFixtureId };
    entries.set(request, entry);
    state.pending.add(request);
    state.total++;
    if (state.records.length < 100) state.records.push(entry);
    if (!["GET", "HEAD", "OPTIONS"].includes(method)) state.unsafe++;
    state.lastActivity = now();
  }
  function responded(request, status) {
    const entry = current(request);
    if (!entry) return;
    entry.status = status;
    state.counts[status] = (state.counts[status] ?? 0) + 1;
    // AdminNavLink's GET probe resolves accessDenied at403 without reading a body.
    // That denial is complete; financial JSON and unknown failures are not.
    if (entry.group === "admin-probe" && entry.method === "GET") {
      if (status === 403) {
        entry.deniedAdminHeaders = true;
        state.adminHeaderDenials++;
        state.pending.delete(request);
      } else state.unexpectedAdminStatuses++;
    }
    // Only the explicitly selected, fixed nonexistent fixture can complete as404.
    // The browser probe still requires its visible error screen, never financial success.
    if (entry.unavailableFixture && status === 404) {
      entry.unavailableHeaders = true;
      state.expectedUnavailableResponses++;
      state.pending.delete(request);
    }
    state.lastActivity = now();
  }
  function finished(request) {
    const entry = current(request);
    if (!entry || entry.wireComplete) return;
    entry.wireComplete = true;
    if (state.pending.delete(request)) state.lastActivity = now();
  }
  function failed(request, reason = request.failure()?.errorText ?? "") {
    const entry = current(request);
    if (!entry || entry.wireComplete) return;
    entry.wireComplete = true;
    state.pending.delete(request);
    const raw = String(reason).trim();
    entry.failureCode = raw.match(/^(?:net::)?(ERR_[A-Z_]+|NS_[A-Z_]+)$/)?.[1]
      ?? (["cancelled", "canceled", "request cancelled", "request canceled"].includes(raw.toLowerCase())
        ? "CANCELLED" : "OTHER");
    const knownDisposal = ["ERR_ABORTED", "NS_BINDING_ABORTED", "CANCELLED"].includes(entry.failureCode);
    if (entry.deniedAdminHeaders && knownDisposal) {
      state.expectedAdminBodyCancellations++;
    } else if (entry.unavailableHeaders && knownDisposal) {
      state.expectedUnavailableBodyCancellations++;
    } else {
      state.failures++;
      state.lastActivity = now();
    }
  }
  function snapshot() {
    return {
      pending: state.pending.size, responseCounts: { ...state.counts },
      failures: state.failures, unsafe: state.unsafe, requests: state.total,
      adminHeaderDenials: state.adminHeaderDenials,
      expectedAdminBodyCancellations: state.expectedAdminBodyCancellations,
      unexpectedAdminStatuses: state.unexpectedAdminStatuses,
      expectedUnavailableResponses: state.expectedUnavailableResponses,
      expectedUnavailableBodyCancellations: state.expectedUnavailableBodyCancellations,
      idleMs: Math.max(0, now() - state.lastActivity),
      recordsTruncated: state.total > state.records.length,
      records: state.records.map(({ method, group, status, failureCode }) =>
        ({ method, group, status, failureCode }))
    };
  }
  begin();
  return { begin, started, responded, finished, failed, snapshot };
}

// Fixed public component regions only. Never serialize DOM classes/text/IDs.
export const layoutShiftRegions = Object.freeze([
  ["overview-heading", ".overview-heading"],
  ["overview-loading", ".dashboard-loading"],
  ["overview-empty", ".onboarding-card"],
  ["overview-attention", ".overview-attention"],
  ["overview-summary", ".fw-pulse"],
  ["overview-watchlist", ".fw-watchlist"],
  ["mobile-navigation", ".app-sidebar"],
  ["app-header", ".app-topbar"],
  ["app-content", ".app-content"],
  ["site-header", ".site-header"]
]);

// Triage events, not a second CLS calculation or an attribution of financial data.
export function layoutShiftEvidence(entries, start = 0) {
  const allowed = new Set(layoutShiftRegions.map(([alias]) => alias));
  const eligible = entries.filter(entry => !entry.hadRecentInput &&
    Number.isFinite(entry.startTime) && entry.startTime >= start &&
    Number.isFinite(entry.value) && entry.value >= 0)
    .sort((a, b) => b.value - a.value || a.startTime - b.startTime);
  return {
    eventsObserved: eligible.length,
    truncated: eligible.length > 10,
    largestEvents: eligible.slice(0, 10).map(entry => ({
      startMs: Number((entry.startTime - start).toFixed(2)),
      value: Number(entry.value.toFixed(6)),
      sourceRegions: [...new Set((Array.isArray(entry.sourceRegions) && entry.sourceRegions.length
        ? entry.sourceRegions.slice(0, 5) : ["unclassified"]).map(alias =>
        typeof alias === "string" && allowed.has(alias) ? alias : "unclassified"))],
      ...(Array.isArray(entry.sourceMovements) ? {
        sourceMovements: entry.sourceMovements.slice(0, 5).map(source => ({
          region: allowed.has(source?.region) ? source.region : "unclassified",
          previous: boundedRect(source?.previous), current: boundedRect(source?.current)
        }))
      } : {}),
      ...(entry.layout ? { layout: Object.fromEntries(
        ["headerTop", "headerHeight", "contentTop", "contentWidth", "rootFontPx"].map(key =>
          [key, boundedCoordinate(entry.layout[key])])) } : {})
    }))
  };
}

function boundedCoordinate(value) {
  return typeof value === "number" && Number.isFinite(value) && Math.abs(value) <= 1e7
    ? Number(value.toFixed(2)) : null;
}
function boundedRect(value) {
  if (!value || typeof value !== "object" || Array.isArray(value)) return null;
  return Object.fromEntries(["x", "y", "width", "height"].map(key =>
    [key, boundedCoordinate(value[key])]));
}
