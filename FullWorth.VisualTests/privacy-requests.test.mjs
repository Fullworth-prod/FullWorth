import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

// Browser-independent tests of the shipped modules; never real credentials or APIs.
export async function verifyPrivacyRequestModules() {
  const source = name => readFile(new URL("../FullWorth.Web/wwwroot/js/" + name, import.meta.url), "utf8");
  const data = text => "data:text/javascript;base64," + Buffer.from(text).toString("base64");
  const sharedUrl = data(await source("account-privacy-requests.js"));
  const exportModule = await import(data((await source("account-export.js")).replace("./account-privacy-requests.js", sharedUrl)));
  const deleteModule = await import(data((await source("account-delete.js")).replace("./account-privacy-requests.js", sharedUrl)));
  const originals = { fetch: globalThis.fetch, window: globalThis.window, document: globalThis.document,
    create: URL.createObjectURL, revoke: URL.revokeObjectURL };
  let requests, anchors, revoked, timers, clickFails, handler;
  let token = 0;
  const reset = () => {
    exportModule.cancelAccountExport();
    requests = []; anchors = []; revoked = []; timers = new Map(); clickFails = false;
    handler = async url => url === "/bff/antiforgery" ?
      Response.json({ requestToken: "synthetic-csrf" }) :
      Response.json({ synthetic: "export" });
  };
  const proof = "Synthetic!Proof123";
  const deferred = () => { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; };
  try {
    globalThis.window = {
      setTimeout: callback => { const id = ++token; timers.set(id, callback); return id; },
      clearTimeout: id => timers.delete(id)
    };
    globalThis.document = {
      createElement: tag => {
        assert.equal(tag, "a");
        const anchor = { appended: false, removed: false, clicks: 0,
          click() { if (clickFails) throw Error("synthetic click failure"); this.clicks++; },
          remove() { this.removed = true; } };
        anchors.push(anchor);
        return anchor;
      },
      body: { appendChild: anchor => { anchor.appended = true; } }
    };
    URL.createObjectURL = blob => { assert.ok(blob.size > 0); return "blob:synthetic-" + (++token); };
    URL.revokeObjectURL = url => revoked.push(url);
    globalThis.fetch = async (url, options) => { requests.push({ url, options }); return handler(url, options); };
    requests = []; anchors = []; revoked = []; timers = new Map();
    reset();

    for (const bad of [null, "", " ", 42, {}]) {
      await assert.rejects(exportModule.downloadAccountExport(bad, null));
      await assert.rejects(deleteModule.deleteFullWorthAccount("DELETE", bad, null));
    }
    for (const bad of ["delete", "DELETE ", "", null]) {
      await assert.rejects(deleteModule.deleteFullWorthAccount(bad, proof, null));
    }
    assert.equal(requests.length, 0, "Invalid local proof must not fetch.");

    assert.equal(await exportModule.downloadAccountExport(proof, " 123456 "), true);
    assert.deepEqual(requests.map(request => request.url), ["/bff/antiforgery", "/bff/account/export"]);
    const request = requests[1].options;
    assert.equal(request.method, "POST");
    assert.equal(request.credentials, "same-origin");
    assert.equal(request.cache, "no-store");
    assert.equal(request.headers["X-CSRF-TOKEN"], "synthetic-csrf");
    assert.deepEqual(JSON.parse(request.body), { currentPassword: proof, twoFactorCode: "123456" });
    assert.equal(anchors[0].download, "fullworth-data-export.json");
    assert.equal(anchors[0].clicks, 1);
    assert.equal(anchors[0].removed, true);
    assert.equal(timers.size, 1);
    exportModule.cancelAccountExport();
    assert.equal(timers.size, 0);
    assert.equal(revoked.length, 1);

    for (const reply of [
      () => new Response("<html>sign in</html>", { headers: { "content-type": "text/html" } }),
      () => new Response("", { headers: { "content-type": "application/json" } }),
      () => new Response(null, { status: 204 }),
      () => { const response = Response.json({ synthetic: true }); Object.defineProperty(response, "redirected", { value: true }); return response; }
    ]) {
      reset();
      handler = async url => url === "/bff/antiforgery" ? Response.json({ requestToken: "synthetic" }) : reply();
      await assert.rejects(exportModule.downloadAccountExport(proof, null));
      assert.equal(anchors.length, 0, "HTML, empty and redirected responses must not create downloads.");
      assert.equal(requests.length, 2, "No automatic retry.");
    }

    reset();
    clickFails = true;
    await assert.rejects(exportModule.downloadAccountExport(proof, null));
    assert.equal(anchors[0].removed, true);
    assert.equal(revoked.length, 1, "A failed click must release its object URL.");

    for (const stage of ["token", "response"]) {
      reset();
      const delayed = deferred();
      handler = async url => {
        if ((stage === "token" && url === "/bff/antiforgery") ||
            (stage === "response" && url === "/bff/account/export")) return delayed.promise;
        return Response.json({ requestToken: "synthetic" });
      };
      const pending = exportModule.downloadAccountExport(proof, null);
      const deadline = Date.now() + 2000;
      while (requests.length < (stage === "token" ? 1 : 2) && Date.now() < deadline)
        await new Promise(done => setImmediate(done));
      assert.equal(requests.length, stage === "token" ? 1 : 2);
      await assert.rejects(exportModule.downloadAccountExport(proof, null), /already in progress/);
      exportModule.cancelAccountExport();
      delayed.resolve(Response.json(stage === "token" ? { requestToken: "synthetic" } : { synthetic: true }));
      assert.equal(await pending, false);
      assert.equal(anchors.length, 0, "A detached export must not start a late browser download.");
      assert.equal(requests.length, stage === "token" ? 1 : 2);
    }

    for (const requestToken of [null, "", " ", 42, {}]) {
      reset();
      handler = async () => Response.json({ requestToken });
      await assert.rejects(deleteModule.deleteFullWorthAccount("DELETE", proof, null));
      assert.equal(requests.length, 1, "Invalid CSRF response must not send DELETE.");
    }

    for (const status of [202, 204]) {
      reset();
      handler = async url => url === "/bff/antiforgery" ? Response.json({ requestToken: "synthetic" }) :
        new Response(null, { status });
      assert.equal(await deleteModule.deleteFullWorthAccount("DELETE", proof, " 123456 "), true);
      assert.equal(requests[1].options.method, "DELETE");
      assert.equal(requests[1].options.cache, "no-store");
      assert.equal(requests[1].options.credentials, "same-origin");
      assert.deepEqual(JSON.parse(requests[1].options.body),
        { confirmation: "DELETE", currentPassword: proof, twoFactorCode: "123456" });
    }
    for (const status of [200, 401, 429, 503]) {
      reset();
      handler = async url => url === "/bff/antiforgery" ? Response.json({ requestToken: "synthetic" }) :
        new Response("private details must never surface", { status, headers: { "content-type": "text/plain" } });
      await assert.rejects(deleteModule.deleteFullWorthAccount("DELETE", proof, null), error =>
        !error.message.includes("private details"));
      assert.equal(requests.length, 2, "A deletion failure is never automatically retried.");
    }
    for (const title of ["Current password is incorrect.", "The authenticator code is invalid."]) {
      reset();
      handler = async url => url === "/bff/antiforgery" ? Response.json({ requestToken: "synthetic" }) :
        new Response(JSON.stringify({ title }), { status: 401, headers: { "content-type": "application/problem+json" } });
      await assert.rejects(deleteModule.deleteFullWorthAccount("DELETE", proof, null), error =>
        /password|authenticator/.test(error.message));
    }
    console.log("Privacy request modules: proof, CSRF, no-retry, MIME/empty/redirect rejection, cancellation and object-URL cleanup passed.");
  } finally {
    exportModule.cancelAccountExport();
    globalThis.fetch = originals.fetch; globalThis.window = originals.window; globalThis.document = originals.document;
    URL.createObjectURL = originals.create; URL.revokeObjectURL = originals.revoke;
  }
}
