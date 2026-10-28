import assert from "node:assert/strict";
import path from "node:path";

// Read-only localized recovery pages in the existing disposable browser stack.
// No account/feature setting or credential operation is performed.
export async function verifyRecoveryLocalization(page, outputDir) {
    const origin = new URL(page.url()).origin;
    assert.equal(new URL(origin).hostname, "web.localhost");
    const state = await page.context().storageState();
    let cases = 0;
    for (const language of ["en-US", "es"]) {
        const context = await page.context().browser().newContext({
            baseURL: origin, ignoreHTTPSErrors: true, serviceWorkers: "block",
            locale: language, storageState: state
        });
        try {
            await context.addCookies([{
                name: ".AspNetCore.Culture", value: "c=en-US|uic=" + language,
                domain: "web.localhost", path: "/", secure: true, sameSite: "Lax"
            }]);
            const current = await context.newPage();
            for (const width of [320, 390]) {
                await current.setViewportSize({ width, height: 844 });
                for (const [route, selector, expected] of [
                    ["/missing-recovery-fixture", "#not-found-title",
                        language === "es" ? "No pudimos encontrar esa página." : "We couldn't find that page."],
                    ["/Error", "#error-title",
                        language === "es" ? "FullWorth tuvo un problema." : "FullWorth hit a snag."],
                    ["/app/account/security-keys", ".settings-header h1",
                        language === "es" ? "Claves de acceso y llaves de seguridad" : "Passkeys & security keys"]
                ]) {
                    const response = await current.goto(route, { waitUntil: "domcontentloaded" });
                    assert.equal(response.status(), route.startsWith("/missing") ? 404 : 200);
                    await current.locator(selector).waitFor();
                    assert.equal((await current.locator(selector).innerText()).trim(), expected);
                    await current.evaluate(() => document.fonts.ready);
                    const geometry = await current.evaluate(() => ({
                        viewport: document.documentElement.clientWidth,
                        document: document.documentElement.scrollWidth
                    }));
                    assert.ok(geometry.document <= geometry.viewport + 1,
                        "Localized recovery must not scroll sideways: " + JSON.stringify({ language, route, ...geometry }));
                    assert.equal(await current.locator("#components-reconnect-modal").getAttribute("aria-label"),
                        language === "es" ? "Estado de la conexión" : "Connection status");
                    assert.equal(await current.locator("#components-seconds-to-next-attempt").count(), 1);
                    assert.equal(await current.locator("#components-reconnect-button").count(), 1);
                    assert.equal(await current.locator("#components-resume-button").count(), 1);
                    if (route.includes("security-keys")) {
                        assert.equal(await current.locator('[data-passkeys="manage"]').count(), 0,
                            "Default-off passkeys must remain disabled.");
                        assert.ok((await current.locator(".settings-page").innerText()).includes(language === "es"
                            ? "Tu método de inicio de sesión actual sigue funcionando."
                            : "Your existing sign-in still works."));
                    }
                    const name = route.includes("security-keys") ? "keys-disabled" :
                        route === "/Error" ? "error" : "not-found";
                    await current.screenshot({ path: path.join(outputDir, "recovery-" + name + "-" + language + "-" + width + ".png"),
                        fullPage: true });
                    cases++;
                }
            }
        } finally { await context.close(); }
    }
    console.log("PASS: " + cases + " read-only EN/ES recovery pages at 320/390px; reconnect controls and default-off keys preserved.");
}
