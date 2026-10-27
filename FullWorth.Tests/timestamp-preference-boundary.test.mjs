import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const source = await readFile(new URL("../FullWorth.Web/wwwroot/js/local-time.js", import.meta.url), "utf8");
let sequence = 0;
const timestampKey = "billwatch.timestamp-display-mode";
const validExperience = () => ({ timestampDisplayMode: "Utc", experienceSetupComplete: true,
  preferredUiLanguage: "en-US", themePreference: "Dark", textSizePreference: "Large",
  highContrastEnabled: false, reduceMotionEnabled: false, experienceFocus: ["BillChanges"] });
const redirect = body => {
  const response = Response.json(body);
  Object.defineProperty(response, "redirected", { value: true });
  return response;
};

async function fixture(action, handler = url => Promise.resolve(Response.json(
  url === "/bff/antiforgery" ? { requestToken: "synthetic" } : { timestampDisplayMode: "Utc" }))) {
  const keys = ["fetch", "window", "document", "Element", "MutationObserver"];
  const previous = new Map(keys.map(key => [key, Object.getOwnPropertyDescriptor(globalThis, key)]));
  const storage = new Map([[timestampKey, "Local12Hour"]]);
  const calls = [];
  let blocked = false;
  try {
    globalThis.Element = class {};
    globalThis.MutationObserver = class { observe() {} disconnect() {} };
    globalThis.window = { localStorage: {
      getItem(key) { if (blocked) throw Error("storage blocked"); return storage.get(key) ?? null; },
      setItem(key, value) { if (blocked) throw Error("storage blocked"); storage.set(key, value); }
    }};
    globalThis.document = { documentElement: { dataset: {} }, body: {}, querySelectorAll: () => [] };
    globalThis.fetch = async (url, options) => {
      calls.push({ url, options });
      return await handler(url, options);
    };
    const module = await import("data:text/javascript;base64," +
      Buffer.from(source + "\n// isolated fixture " + (++sequence)).toString("base64"));
    await action({ module, calls, storage, blockStorage: () => { blocked = true; } });
  } finally {
    for (const [key, descriptor] of previous)
      if (descriptor) Object.defineProperty(globalThis, key, descriptor);
      else delete globalThis[key];
  }
}

const assertSafeRequests = calls => {
  for (const { options } of calls) {
    assert.equal(options.redirect, "error", "No read, token fetch or write may automatically follow a redirect.");
    assert.equal(options.credentials, "same-origin");
    assert.equal(options.cache, "no-store");
  }
};

test("timestamp reads accept only supported confirmed modes without browser changes", async () => {
  for (const mode of ["Utc", "Local12Hour"])
    await fixture(async ({ module, calls, storage }) => {
      assert.equal((await module.getTimestampPreference()).timestampDisplayMode, mode);
      assert.equal(storage.get(timestampKey), "Local12Hour");
      assertSafeRequests(calls);
    }, () => Response.json({ timestampDisplayMode: mode }));
});

test("malformed preference reads cannot silently become the default mode", async () => {
  for (const payload of [null, [], {}, { timestampDisplayMode: "" },
    { timestampDisplayMode: "Future" }, { timestampDisplayMode: 42 }])
    await fixture(async ({ module, storage }) => {
      await assert.rejects(module.getTimestampPreference(), /Could not load/);
      assert.equal(storage.get(timestampKey), "Local12Hour");
    }, () => Response.json(payload));
});

test("redirected, non-JSON, partial and empty preference reads are rejected", async () => {
  for (const response of [redirect({ timestampDisplayMode: "Utc" }),
    Response.json({ timestampDisplayMode: "Utc" }, { status: 201 }),
    new Response(null, { status: 204 }),
    new Response('{"timestampDisplayMode":"Utc"}', { headers: { "content-type": "text/html" } }),
    new Response("{", { headers: { "content-type": "application/json" } })])
    await fixture(async ({ module }) => {
      await assert.rejects(module.getTimestampPreference(), /Could not load/);
    }, () => response);
});

test("timestamp save applies only the exact server-confirmed requested mode", async () => {
  for (const mode of ["Utc", "Local12Hour"])
    await fixture(async ({ module, calls, storage }) => {
      const saved = await module.saveTimestampPreference(mode);
      assert.equal(saved.timestampDisplayMode, mode);
      assert.equal(storage.get(timestampKey), mode);
      assert.equal(calls.length, 2);
      assert.equal(calls[1].options.method, "PUT");
      assert.equal(calls[1].options.headers["X-CSRF-TOKEN"], "synthetic");
      assert.deepEqual(JSON.parse(calls[1].options.body), { timestampDisplayMode: mode });
      assertSafeRequests(calls);
    }, url => Response.json(url === "/bff/antiforgery" ?
      { requestToken: "synthetic" } : { timestampDisplayMode: mode }));
});

test("a mismatched timestamp confirmation cannot report success or change the display", async () => {
  await fixture(async ({ module, calls, storage }) => {
    storage.set(timestampKey, "Utc");
    await assert.rejects(module.saveTimestampPreference("Local12Hour"), /Could not confirm/);
    assert.equal(storage.get(timestampKey), "Utc");
    assert.equal(calls.filter(call => call.options.method === "PUT").length, 1);
  });
});

test("malformed and redirected write confirmations preserve the last applied preference", async () => {
  for (const response of [Response.json({}), Response.json(null), Response.json([]),
    Response.json({ timestampDisplayMode: "Future" }), redirect({ timestampDisplayMode: "Utc" }),
    new Response(null, { status: 204 }),
    new Response("{", { headers: { "content-type": "application/json" } }),
    new Response('{"timestampDisplayMode":"Utc"}', { headers: { "content-type": "text/plain" } })])
    await fixture(async ({ module, calls, storage }) => {
      await assert.rejects(module.saveTimestampPreference("Utc"));
      assert.equal(storage.get(timestampKey), "Local12Hour");
      assert.equal(calls.filter(call => call.options.method === "PUT").length, 1,
        "An uncertain confirmation never replays its write.");
    }, url => url === "/bff/antiforgery" ? Response.json({ requestToken: "synthetic" }) : response);
});

test("invalid security tokens prevent a timestamp write", async () => {
  for (const requestToken of [undefined, null, "", " ", 42])
    await fixture(async ({ module, calls, storage }) => {
      await assert.rejects(module.saveTimestampPreference("Utc"), /secure preference/);
      assert.equal(calls.length, 1);
      assert.equal(storage.get(timestampKey), "Local12Hour");
    }, () => Response.json({ requestToken }));
});

test("redirected or malformed token responses cannot authorize a preference write", async () => {
  for (const response of [redirect({ requestToken: "synthetic" }),
    new Response('{"requestToken":"synthetic"}', { headers: { "content-type": "text/html" } }),
    new Response(null, { status: 204 }), Response.json(null)])
    await fixture(async ({ module, calls }) => {
      await assert.rejects(module.saveTimestampPreference("Utc"), /secure preference/);
      assert.equal(calls.length, 1);
    }, () => response);
});

test("an ambiguous write transport failure is not automatically replayed", async () => {
  await fixture(async ({ module, calls, storage }) => {
    await assert.rejects(module.saveTimestampPreference("Utc"), /connection lost/);
    assert.equal(calls.filter(call => call.options.method === "PUT").length, 1);
    assert.equal(storage.get(timestampKey), "Local12Hour");
  }, url => {
    if (url === "/bff/antiforgery") return Response.json({ requestToken: "synthetic" });
    throw Error("connection lost");
  });
});

test("blocked local storage does not turn confirmed persistence into a failed write", async () => {
  await fixture(async ({ module, calls, blockStorage }) => {
    blockStorage();
    assert.equal((await module.saveTimestampPreference("Utc")).timestampDisplayMode, "Utc");
    assert.equal(calls.filter(call => call.options.method === "PUT").length, 1);
  });
});

test("invalid caller modes are rejected before network or browser changes", async () => {
  for (const mode of [null, undefined, "", "utc", "Future", 42])
    await fixture(async ({ module, calls, storage }) => {
      await assert.rejects(module.saveTimestampPreference(mode), /timestamp mode/);
      assert.equal(calls.length, 0);
      assert.equal(storage.get(timestampKey), "Local12Hour");
    });
});

test("failed initialization preserves the previous display preference", async () => {
  await fixture(async ({ module, storage }) => {
    storage.set(timestampKey, "Utc");
    await module.initializeTimestampPreferences();
    assert.equal(storage.get(timestampKey), "Utc");
  }, () => Response.json({ timestampDisplayMode: "Future" }));
});

test("experience persistence keeps the same strict token and redirect boundaries", async () => {
  for (const tokenResponse of [redirect({ requestToken: "synthetic" }), Response.json(null)])
    await fixture(async ({ module, calls }) => {
      await assert.rejects(module.persistExperiencePreferences(validExperience()));
      assert.equal(calls.length, 1);
    }, () => tokenResponse);
  await fixture(async ({ module, calls }) => {
    await assert.rejects(module.persistExperiencePreferences(validExperience()));
    assert.equal(calls.length, 2);
    assertSafeRequests(calls);
  }, url => url === "/bff/antiforgery" ?
    Response.json({ requestToken: "synthetic" }) : redirect(validExperience()));
});
