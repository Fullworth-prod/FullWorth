import assert from "node:assert/strict";
import http from "node:http";
import { readFile } from "node:fs/promises";
import { chromium, webkit } from "playwright";
import { removeOwnedFixturePasskey } from "./passkey-removal-setup.mjs";

const source = await readFile(new URL("../FullWorth.Web/wwwroot/js/passkeys.js", import.meta.url), "utf8");
const credentialId = "c3ludGhldGljLXNhdmVkLWtleQ";
const proof = { credentialId, password: "Synthetic!Proof123", twoFactorCode: "123456" };
let state;
const html = '<!doctype html><html lang="en"><head><meta charset="utf-8"></head><body><section data-passkeys="manage">' +
    '<input name="currentPassword" type="password"><input name="twoFactorCode">' +
    '<input name="keyName"><p data-passkey-status></p>' +
    '<button data-passkey-action="retry" hidden>Retry</button><ul data-passkey-list></ul>' +
    '</section><script src="/passkeys.js"></script></body></html>';
const server = http.createServer(async (request, response) => {
    let body = ""; for await (const chunk of request) body += chunk;
    const send = (status, type, value) => { response.writeHead(status, { "content-type": type }); response.end(value); };
    if (request.url === "/") return send(200, "text/html", html);
    if (request.url === "/passkeys.js") return send(200, "text/javascript", source);
    if (request.url === "/bff/antiforgery")
        return send(200, "application/json", '{"requestToken":"synthetic-csrf"}');
    if (request.url === "/bff/account/security/passkeys")
        return send(200, "application/json", JSON.stringify(state.removed ? [] :
            [{ id: credentialId, name: "Synthetic key", createdAt: "2026-10-01T00:00:00Z" }]));
    if (request.url === "/bff/account/security/passkeys/remove") {
        state.writes++;
        const data = JSON.parse(body);
        state.correctPassword = data.currentPassword === proof.password;
        state.correctCode = data.twoFactorCode === proof.twoFactorCode;
        state.correctId = data.credentialId === credentialId;
        const valid = state.correctPassword && state.correctCode && state.correctId;
        state.status = valid ? 204 : 400;
        state.removed = valid;
        return send(state.status, "application/json", valid ? "" : '{"errors":{"CurrentPassword":["required"]}}');
    }
    send(404, "text/plain", "");
});
await new Promise(done => server.listen(0, "127.0.0.1", done));
const baseURL = "http://127.0.0.1:" + server.address().port;
try {
    for (const [name, engine] of [["Chromium", chromium], ["WebKit", webkit]]) {
        const browser = await engine.launch();
        try {
        let cases = 0;
        const run = async body => {
            state = { writes: 0, removed: false };
            const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
            const page = await context.newPage();
            let dialogs = 0;
            page.on("dialog", async dialog => { dialogs++; await dialog.accept(); });
            try {
                await page.goto(baseURL);
                await page.locator('[data-passkeys="manage"][data-loaded="true"]').waitFor();
                await page.evaluate(() => Object.defineProperty(window, "PublicKeyCredential",
                    { configurable: true, value: undefined }));
                assert.equal(await page.evaluate(() => isSecureContext), true);
                await body(page, () => dialogs);
                cases++;
            } finally { await context.close(); }
        };
        const replace = page => page.evaluate(() => {
            const root = document.querySelector('[data-passkeys="manage"]');
            const next = root.cloneNode(true);
            next.removeAttribute("data-loaded");
            for (const input of next.querySelectorAll("input")) input.value = "";
            root.replaceWith(next);
        });
        await run(async (page, dialogs) => {
            await page.locator('[name="currentPassword"]').fill(proof.password);
            await replace(page);
            await page.locator('[data-passkeys="manage"][data-loaded="true"]').waitFor();
            await page.locator('[name="twoFactorCode"]').fill(proof.twoFactorCode);
            const response = page.waitForResponse(r => r.url().endsWith("/passkeys/remove"));
            await page.locator('[data-passkey-action="remove"]').click();
            assert.equal((await response).status(), 400, "Split setup must demonstrate the missing-proof counterexample.");
            assert.equal(state.correctPassword, false);
            assert.equal(state.writes, 1);
            assert.equal(dialogs(), 1);
        });
        await run(async (page, dialogs) => {
            await replace(page);
            const response = page.waitForResponse(r => r.url().endsWith("/passkeys/remove"));
            await removeOwnedFixturePasskey(page, proof);
            assert.equal((await response).status(), 204);
            assert.equal(state.correctPassword && state.correctCode && state.correctId, true);
            assert.equal(state.writes, 1);
            assert.equal(dialogs(), 1, "Actual production consent remains exercised.");
        });
        await run(async page => {
            await page.evaluate(() => document.querySelector('[name="currentPassword"]').addEventListener("input", () => {
                const root = document.querySelector('[data-passkeys="manage"]');
                const next = root.cloneNode(true); next.removeAttribute("data-loaded");
                for (const input of next.querySelectorAll("input")) input.value = "";
                root.replaceWith(next);
            }, { once: true }));
            const response = page.waitForResponse(r => r.url().endsWith("/passkeys/remove"));
            await removeOwnedFixturePasskey(page, proof);
            assert.equal((await response).status(), 204);
            assert.equal(state.writes, 1, "Replacement during setup must not produce a write from abandoned controls.");
        });
        await run(async page => {
            await assert.rejects(removeOwnedFixturePasskey(page, { ...proof, credentialId: "d3Jvbmc" }),
                /no removal was submitted/);
            assert.equal(state.writes, 0);
        });
        await run(async page => {
            await page.locator('[data-passkey-action="remove"]').evaluate(button => button.disabled = true);
            await assert.rejects(removeOwnedFixturePasskey(page, proof), /no removal was submitted/);
            assert.equal(state.writes, 0);
        });
        await run(async page => {
            await assert.rejects(removeOwnedFixturePasskey(page, { ...proof, password: "" }), /Invalid synthetic/);
            assert.equal(state.writes, 0);
        });
        await run(async page => {
            await page.evaluate(() => document.documentElement.lang = "es");
            const response = page.waitForResponse(r => r.url().endsWith("/passkeys/remove"));
            await removeOwnedFixturePasskey(page, proof);
            assert.equal((await response).status(), 204);
            await page.getByText("Guardado. Tu contraseña y las opciones de recuperación siguen funcionando.",
                { exact: true }).waitFor();
            assert.equal(state.writes, 1);
        });
        await run(async page => {
            await page.evaluate(() => document.documentElement.lang = "es");
            let release;
            const held = new Promise(done => release = done);
            await page.route("**/bff/account/security/passkeys/remove", async route => {
                await held; await route.continue();
            });
            const request = page.waitForRequest(r => r.url().endsWith("/passkeys/remove"));
            const response = page.waitForResponse(r => r.url().endsWith("/passkeys/remove"));
            try {
                await removeOwnedFixturePasskey(page, proof);
                await request;
                await replace(page);
                await page.locator('[data-passkeys="manage"][data-loaded="true"]').waitFor();
                assert.equal(await page.locator('[name="currentPassword"]').inputValue(), "");
            } finally { release(); }
            assert.equal((await response).status(), 204);
            await page.getByText("Aún no hay claves registradas.", { exact: true }).first().waitFor();
            assert.equal(await page.locator("[data-passkey-status]").innerText(),
                "Aún no hay claves registradas.");
            assert.equal(await page.getByText(
                "Guardado. Tu contraseña y las opciones de recuperación siguen funcionando.",
                { exact: true }).count(), 0, "An abandoned root's success must not move into a replacement region.");
            assert.equal(state.writes, 1);
        });
        await run(async page => {
            const response = page.waitForResponse(r => r.url().endsWith("/passkeys/remove"));
            const lostObservation = { evaluate: async (...args) => {
                await page.evaluate(...args);
                throw new Error("synthetic observation lost after click");
            }, waitForTimeout: ms => page.waitForTimeout(ms) };
            await assert.rejects(removeOwnedFixturePasskey(lostObservation, proof), /observation lost/);
            assert.equal((await response).status(), 204);
            assert.equal(state.writes, 1, "An evaluation failure after click must not replay removal.");
        });
        console.log(name + ": " + cases + " passkey-removal fixture cases passed; replacement400, atomic proof, consent and no replay.");
        } finally { await browser.close(); }
    }
} finally { server.closeAllConnections(); await new Promise(done => server.close(done)); }
