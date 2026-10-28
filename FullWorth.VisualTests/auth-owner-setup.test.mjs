import assert from "node:assert/strict";
import { createServer } from "node:http";
import { chromium, webkit } from "playwright";
import { submitDisposableAuthForm } from "./auth-owner-setup.mjs";

const states = new Map();
const markup = action => '<form class="auth-form" method="post" action="' + action + '">' +
    '<input type="hidden" name="__RequestVerificationToken" value="synthetic-csrf">' +
    '<input name="email" type="email" required><input name="password" type="password" required>' +
    (action === "/auth/register" ? '<input name="confirmPassword" type="password" required>' +
    '<input name="acceptedTermsAndPrivacy" type="checkbox" required>' : '') +
    '<button type="submit">Submit</button></form>';
const server = createServer(async (request, response) => {
    const state = states.get(request.headers["x-synthetic-case"]);
    const path = new URL(request.url, "http://localhost").pathname;
    if (path.startsWith("/auth/")) {
        const parts = [];
        for await (const part of request) parts.push(part);
        state.posts++;
        const body = new URLSearchParams(Buffer.concat(parts).toString("utf8"));
        state.proof = body.get("__RequestVerificationToken") === "synthetic-csrf" &&
            body.get("email") === "synthetic@example.test" && body.get("password") === "synthetic-proof" &&
            (path !== "/auth/register" || (body.get("confirmPassword") === "synthetic-proof" &&
                body.get("acceptedTermsAndPrivacy") === "on"));
        response.writeHead(302, { Location: "/done" }); response.end();
        return;
    }
    if (path === "/done") { response.writeHead(200); response.end("done"); return; }
    const form = markup(state.action);
    // Simulate replacement after a password value is set. Old separate fill/
    // click calls lose input state. One-task native submission retains proof.
    const script = '(function(){let replaced=false;const render=()=>{document.getElementById("root").innerHTML=' +
        JSON.stringify(form) + ';};const schedule=()=>{if(replaced)return;replaced=true;queueMicrotask(render);};' +
        'const input=document.querySelector("[name=password]");const value=Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,"value");' +
        'Object.defineProperty(input,"value",{get(){return value.get.call(this);},set(v){value.set.call(this,v);schedule();}});' +
        'input.addEventListener("input",schedule);})();';
    response.writeHead(200, { "Content-Type": "text/html; charset=utf-8" });
    response.end('<!doctype html><html><body><main id="root">' + form + '</main><script>' + script + '</script></body></html>');
});
await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
const origin = "http://127.0.0.1:" + server.address().port;
const engines = (process.env.FULLWORTH_TEST_BROWSERS ?? "chromium,webkit").split(",");
assert.ok(engines.length && engines.every(engine => ["chromium", "webkit"].includes(engine)));
let serial = 0;
try {
    for (const engine of engines) {
        const browser = await ({ chromium, webkit })[engine].launch({ headless: true });
        let passed = 0;
        try {
            async function scenario(action, run) {
                const id = String(++serial), state = { posts: 0, proof: false, action };
                states.set(id, state);
                const context = await browser.newContext({ extraHTTPHeaders: { "X-Synthetic-Case": id } });
                try {
                    const page = await context.newPage(); await page.goto(origin);
                    await run(page, state); passed++;
                } finally { await context.close(); states.delete(id); }
            }
            await scenario("/auth/login", async (page, state) => {
                await page.locator("[name=email]").fill("synthetic@example.test");
                await page.locator("[name=password]").fill("synthetic-proof");
                await page.locator("button").click();
                assert.equal(state.posts, 0, "Counterexample: replacement must expose the old split-call race.");
                assert.equal(page.url(), origin + "/");
            });
            for (const action of ["/auth/login", "/auth/register"]) {
                await scenario(action, async (page, state) => {
                    const fields = { email: "synthetic@example.test", password: "synthetic-proof" };
                    if (action === "/auth/register") Object.assign(fields, {
                        confirmPassword: "synthetic-proof", acceptedTermsAndPrivacy: true });
                    await Promise.all([
                        page.waitForURL(origin + "/done"),
                        submitDisposableAuthForm(page, action, fields)
                    ]);
                    assert.equal(state.posts, 1);
                    assert.equal(state.proof, true, "Keep CSRF, password and registration consent checks.");
                });
            }
            await scenario("/auth/login", async (page, state) => {
                await page.locator("[name=__RequestVerificationToken]").evaluate(input => input.remove());
                await assert.rejects(() => submitDisposableAuthForm(page, "/auth/login",
                    { email: "synthetic@example.test", password: "synthetic-proof" }), /antiforgery token/);
                assert.equal(state.posts, 0);
            });
            await scenario("/auth/login", async (page, state) => {
                await assert.rejects(() => submitDisposableAuthForm(page, "/auth/login",
                    { email: "not-an-email", password: "synthetic-proof" }), /not valid and ready/);
                assert.equal(state.posts, 0);
            });
            await scenario("/auth/login", async (page, state) => {
                await assert.rejects(() => submitDisposableAuthForm(page, "/auth/external/link", {}), /Unsupported disposable/);
                assert.equal(state.posts, 0);
            });
            await assert.rejects(() => submitDisposableAuthForm({ url: () => "https://fullworth.org/login" },
                "/auth/login", {}), /loopback fixture/);
            passed++;
        } finally { await browser.close(); }
        console.log(engine + " disposable auth setup passed: " + passed +
            " scenarios; split-call replacement counterexample, single native submit, proof/consent and safe refusal.");
    }
} finally {
    server.closeAllConnections(); await new Promise(resolve => server.close(resolve));
}
