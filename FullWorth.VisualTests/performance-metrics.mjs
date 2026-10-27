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
    if (entry.startTime - last >= 1000 || entry.startTime - start > 5000) {
      score = 0;
      start = entry.startTime;
    }
    score += entry.value;
    last = entry.startTime;
    best = Math.max(best, score);
  }
  return Number(best.toFixed(6));
}
