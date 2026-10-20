import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

// Exercises the shipped module without any real user, network or browser storage.
export async function verifyExperiencePreferenceModule() {
  const source = await readFile(new URL("../FullWorth.Web/wwwroot/js/local-time.js", import.meta.url), "utf8");
  const module = await import("data:text/javascript;base64," + Buffer.from(source).toString("base64"));
  const original = { window: globalThis.window, document: globalThis.document, fetch: globalThis.fetch,
    Element: globalThis.Element, MutationObserver: globalThis.MutationObserver };
  const valid = () => ({ timestampDisplayMode: "Utc", experienceSetupComplete: true,
    preferredUiLanguage: "en-US", themePreference: "Dark", textSizePreference: "Large",
    highContrastEnabled: true, reduceMotionEnabled: true, experienceFocus: ["BillChanges", "Statements"] });
  const request = () => { const { experienceSetupComplete, timestampDisplayMode, ...draft } = valid(); return draft; };
  let blocked = false, cookieBlocked = false, cookieText = "", calls = [], storage = new Map(), cookies = 0;
  let handler;
  const reset = () => {
    calls = []; storage.clear(); blocked = false; cookieBlocked = false; cookieText = ""; cookies = 0;
    document.documentElement.dataset = {};
    handler = async url => url === "/bff/antiforgery" ? Response.json({ requestToken: "synthetic" }) : Response.json(valid());
  };
  try {
    globalThis.Element = class {};
    globalThis.MutationObserver = class { observe() {} disconnect() {} };
    globalThis.window = {
      localStorage: { getItem: key => { if (blocked) throw Error("storage blocked"); return storage.get(key) ?? null; },
        setItem: (key, value) => { if (blocked) throw Error("storage blocked"); storage.set(key, value); } },
      matchMedia: () => ({ matches: false })
    };
    globalThis.document = { documentElement: { dataset: {} }, body: {}, querySelectorAll: () => [],
      querySelector: () => ({ setAttribute() {} }) };
    Object.defineProperty(document, "cookie", { configurable: true, get() { return cookieText; },
      set(value) { cookies++; if (!cookieBlocked) cookieText = value.split(";")[0]; } });
    globalThis.fetch = async (url, options) => { calls.push({ url, options }); return handler(url, options); };
    reset();

    const result = await module.persistExperiencePreferences(request());
    assert.deepEqual(result, valid());
    assert.deepEqual(calls.map(value => value.url), ["/bff/antiforgery", "/bff/account/preferences/experience"]);
    const put = calls[1].options;
    assert.equal(put.method, "PUT");
    assert.equal(put.credentials, "same-origin");
    assert.equal(put.cache, "no-store");
    assert.equal(put.headers["X-CSRF-TOKEN"], "synthetic");
    assert.deepEqual(JSON.parse(put.body), request());
    assert.deepEqual(document.documentElement.dataset, {}, "Persisting server preferences must not apply browser state.");
    assert.equal(cookies, 0);

    for (const malformed of [null, {}, { ...request(), experienceFocus: [] },
      { ...request(), highContrastEnabled: "false" }, { ...request(), themePreference: "unknown" }]) {
      reset();
      await assert.rejects(module.persistExperiencePreferences(malformed));
      assert.equal(calls.length, 0);
    }
    for (const requestToken of [null, "", " ", 42]) {
      reset(); handler = async () => Response.json({ requestToken });
      await assert.rejects(module.persistExperiencePreferences(request()));
      assert.equal(calls.length, 1, "Missing CSRF proof must prevent PUT.");
    }
    for (const mutate of [
      value => ({ ...value, experienceSetupComplete: false }),
      value => ({ ...value, reduceMotionEnabled: undefined }),
      value => ({ ...value, themePreference: "Light" }),
      value => ({ ...value, experienceFocus: ["BillChanges"] }),
      () => null
    ]) {
      reset();
      handler = async url => url === "/bff/antiforgery" ? Response.json({ requestToken: "synthetic" }) :
        Response.json(mutate(valid()));
      await assert.rejects(module.persistExperiencePreferences(request()));
      assert.equal(calls.length, 2, "Uncertain responses must never cause automatic PUT replay.");
      assert.deepEqual(document.documentElement.dataset, {});
    }

    reset();
    assert.equal(module.applyExperiencePreferences(valid(), true), true);
    reset(); cookieBlocked = true;
    assert.equal(module.applyExperiencePreferences(valid(), true), false,
      "Silently rejected culture cookies must report a saved-but-not-remembered browser result.");
    assert.equal(document.documentElement.dataset.theme, "dark");
    reset(); blocked = true;
    assert.equal(module.applyExperiencePreferences(valid(), true), false);
    assert.equal(document.documentElement.dataset.theme, "dark");
    assert.equal(document.documentElement.dataset.fullworthTextSize, "large");
    assert.equal(document.documentElement.dataset.fullworthHighContrast, "true");
    assert.equal(document.documentElement.dataset.fullworthReduceMotion, "true");
    assert.ok(module.formatLocalDateTime("2026-01-01T12:00:00Z").includes("2026"));
    assert.deepEqual(await module.saveExperiencePreferences(request()), valid(),
      "Storage denial cannot turn confirmed persistence into an apparent failed write.");
    assert.equal(calls.filter(value => value.options?.method === "PUT").length, 1);

    reset(); blocked = true;
    handler = async () => { throw Error("network unavailable"); };
    await module.initializeTimestampPreferences();
    assert.ok(module.formatLocalDate("2026-01-01T12:00:00Z"));

    reset();
    let finish;
    handler = async () => new Promise(resolve => { finish = resolve; });
    const pending = module.initializeTimestampPreferences();
    const deadline = Date.now() + 2000;
    while (!finish && Date.now() < deadline) await new Promise(done => setImmediate(done));
    assert.ok(finish);
    const newer = { ...valid(), themePreference: "Light", textSizePreference: "ExtraLarge", highContrastEnabled: false };
    module.applyExperiencePreferences(newer, true);
    finish(Response.json(valid()));
    await pending;
    assert.equal(document.documentElement.dataset.theme, "light");
    assert.equal(document.documentElement.dataset.fullworthTextSize, "extra-large");
    assert.equal(document.documentElement.dataset.fullworthHighContrast, "false",
      "An older initialization read must not undo an explicit applied setup.");

    reset();
    const draft = request();
    let tokenDone;
    handler = async url => url === "/bff/antiforgery" ?
      new Promise(resolve => { tokenDone = resolve; }) : Response.json(valid());
    const saving = module.persistExperiencePreferences(draft);
    draft.experienceFocus.push("Spending");
    tokenDone(Response.json({ requestToken: "synthetic" }));
    await saving;
    assert.deepEqual(JSON.parse(calls[1].options.body).experienceFocus, ["BillChanges", "Statements"]);
    console.log("Experience preference module: CSRF, immutable request, no replay, schema/match checks, blocked storage and stale initialization passed.");
  } finally {
    globalThis.window = original.window; globalThis.document = original.document; globalThis.fetch = original.fetch;
    globalThis.Element = original.Element; globalThis.MutationObserver = original.MutationObserver;
  }
}
