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
                    if (!route.includes("security-keys")) {
                        for (const enlarged of [false, true]) {
                        await current.evaluate(enlarged => { document.documentElement.style.fontSize = enlarged ? "200%" : ""; }, enlarged);
                        await current.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
                        if (enlarged) assert.ok(await current.evaluate(() => parseFloat(getComputedStyle(document.documentElement).fontSize)) >= 31.9);
                        const links = current.locator(".footer-legal-links a");
                        assert.equal(await links.count(), 2);
                        assert.deepEqual(await links.evaluateAll(items => items.map(item => item.getAttribute("href"))),
                            ["/terms", "/privacy"], "Published legal destinations stay unchanged.");
                        const targets = await links.evaluateAll(items => items.map(item => {
                            const r = item.getBoundingClientRect();
                            return { left: r.left, right: r.right, top: r.top, bottom: r.bottom,
                                width: r.width, height: r.height };
                        }));
                        for (const target of targets) {
                            assert.ok(target.width >= 43.9 && target.height >= 43.9,
                                "Legal navigation needs usable tap targets: " + JSON.stringify(target));
                            assert.ok(target.left >= -1 && target.right <= width + 1,
                                "Legal navigation remains within the narrow page.");
                        }
                        const gap = targets[1].top >= targets[0].bottom - 1
                            ? targets[1].top - targets[0].bottom : targets[1].left - targets[0].right;
                        assert.ok(gap >= 19, "Legal destinations must not run together.");
                        await links.nth(0).focus();
                        await current.keyboard.press("Tab");
                        assert.equal(await links.nth(1).evaluate(item => item === document.activeElement), true,
                            "Both legal destinations remain reachable in keyboard order.");
                        }
                        await current.evaluate(() => { document.documentElement.style.fontSize = ""; });
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
    console.log("PASS: " + cases + " read-only EN/ES recovery pages at 320/390px; legal navigation targets/spacing/focus, reconnect controls and default-off keys preserved.");
}
