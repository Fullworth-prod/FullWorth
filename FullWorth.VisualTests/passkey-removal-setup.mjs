// Disposable browser-fixture setup only. Actual passkeys.js handles consent,
// proof capture, CSRF and the single write. Never retry after an evaluation
// error because the click/write may already have happened.
export async function removeOwnedFixturePasskey(page, { credentialId, password, twoFactorCode }) {
    if (typeof credentialId !== "string" || !/^[A-Za-z0-9_-]{1,1400}$/.test(credentialId) ||
        typeof password !== "string" || !password.trim() ||
        typeof twoFactorCode !== "string" || !/^\d{6}$/.test(twoFactorCode))
        throw new Error("Invalid synthetic passkey-removal setup.");
    for (let attempt = 0; attempt < 40; attempt++) {
        const submitted = await page.evaluate(proof => {
            const root = document.querySelector('[data-passkeys="manage"][data-loaded="true"]');
            if (!root?.isConnected) return false;
            const matches = [...root.querySelectorAll('[data-passkey-action="remove"]')]
                .filter(button => button.dataset.keyId === proof.credentialId);
            if (matches.length !== 1) return false;
            const button = matches[0];
            const password = root.querySelector('input[name="currentPassword"]');
            const code = root.querySelector('input[name="twoFactorCode"]');
            if (!password || password.type !== "password" || !code ||
                password.disabled || code.disabled || button.disabled) return false;
            button.scrollIntoView({ block: "center" });
            const rect = button.getBoundingClientRect();
            const x = rect.left + rect.width / 2, y = rect.top + rect.height / 2;
            if (rect.width <= 0 || rect.height <= 0 || x < 0 || y < 0 ||
                x >= innerWidth || y >= innerHeight ||
                document.elementFromPoint(x, y)?.closest('[data-passkey-action="remove"]') !== button)
                return false;
            password.value = proof.password;
            code.value = proof.twoFactorCode;
            for (const input of [password, code]) {
                input.dispatchEvent(new Event("input", { bubbles: true }));
                input.dispatchEvent(new Event("change", { bubbles: true }));
            }
            if (!root.isConnected || !root.contains(button) || !root.contains(password) ||
                !root.contains(code) || password.value !== proof.password || code.value !== proof.twoFactorCode)
                return false;
            button.click();
            return true;
        }, { credentialId, password, twoFactorCode });
        if (submitted) return;
        // Read-only readiness/replacement retry; no credential write occurred.
        await page.waitForTimeout(25);
    }
    throw new Error("Current owned passkey controls were not ready; no removal was submitted.");
}
