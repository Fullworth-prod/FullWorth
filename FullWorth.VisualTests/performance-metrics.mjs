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

export function createReadTracker(origin, now = () => performance.now()) {
  let lease = 0, state;
  const entries = new WeakMap();
  function begin() {
    lease++;
    state = { pending: new Set(), counts: {}, failures: 0, unsafe: 0,
      records: [], total: 0, adminHeaderDenials: 0, expectedAdminBodyCancellations: 0,
      unexpectedAdminStatuses: 0, lastActivity: now() };
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
      wireComplete: false, deniedAdminHeaders: false };
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
    if (entry.deniedAdminHeaders &&
        ["ERR_ABORTED", "NS_BINDING_ABORTED", "CANCELLED"].includes(entry.failureCode)) {
      state.expectedAdminBodyCancellations++;
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
      idleMs: Math.max(0, now() - state.lastActivity),
      recordsTruncated: state.total > state.records.length,
      records: state.records.map(({ method, group, status, failureCode }) =>
        ({ method, group, status, failureCode }))
    };
  }
  begin();
  return { begin, started, responded, finished, failed, snapshot };
}
