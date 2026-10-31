import assert from "node:assert/strict";

// Test-owner setup only. Fill and submit native forms in one browser task so
// prerender/interactive replacement cannot erase fields between automation calls.
// Native constraint validation, submit events, antiforgery and server auth remain.
export async function submitDisposableAuthForm(page, action, fields) {
    const url = new URL(page.url());
    assert.ok(["localhost", "127.0.0.1", "[::1]"].includes(url.hostname) || url.hostname.endsWith(".localhost"),
        "Disposable auth setup requires a loopback fixture.");
    assert.ok(["/auth/register", "/auth/login"].includes(action), "Unsupported disposable auth action.");
    const form = page.locator('form.auth-form[action="' + action + '"]');
    await form.waitFor();
    for (let attempt = 0; attempt < 40; attempt++) {
      const submitted = await form.evaluate((form, { action, fields }) => {
        const target = new URL(form.action, location.href);
        // A stale ElementHandle can reach evaluate during WebKit navigation.
        // A read-only reattachment is safe only before any fields/write attempt.
        if (!form.isConnected) return false;
        if (form.method.toUpperCase() !== "POST" ||
            target.origin !== location.origin || target.pathname !== action)
            throw new Error("Disposable auth form action was not verified: " + JSON.stringify({
                method: form.method, targetOrigin: target.origin, documentOrigin: location.origin,
                targetPath: target.pathname, expectedPath: action
            }));
        const token = form.elements.namedItem("__RequestVerificationToken");
        if (!(token instanceof HTMLInputElement) || !token.value.trim())
            throw new Error("Disposable auth form is missing its antiforgery token.");
        for (const [name, value] of Object.entries(fields)) {
            const input = form.elements.namedItem(name);
            if (!(input instanceof HTMLInputElement)) throw new Error("Disposable auth field is missing.");
            if (typeof value === "boolean" && input.type === "checkbox") input.checked = value;
            else if (typeof value === "string" && input.type !== "hidden") input.value = value;
            else throw new Error("Unsupported disposable auth field.");
        }
        const submit = form.querySelector('button[type="submit"]');
        if (!(submit instanceof HTMLButtonElement) || submit.disabled || !form.checkValidity())
            throw new Error("Disposable auth form is not valid and ready.");
        form.requestSubmit(submit); // Exactly one attempt; never replay registration/login.
        return true;
      }, { action, fields });
      if (submitted) return;
      await page.waitForTimeout(25);
    }
    throw new Error("Disposable auth form stayed detached before submission.");
}
