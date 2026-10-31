(() => {
    "use strict";
    const copy = {
        unsupported: ["This browser does not support passkeys. Use your authenticator code instead.","Este navegador no admite claves de acceso. Usa tu código del autenticador."],
        prompt: ["Follow your device or security key instructions…","Sigue las instrucciones de tu dispositivo o llave de seguridad…"],
        signIn: ["Your sign-in expired. Start again.","Tu inicio de sesión ha caducado. Vuelve a empezar."],
        rate: ["Too many attempts. Wait a minute and try again.","Demasiados intentos. Espera un minuto y vuelve a intentarlo."],
        verify: ["Unable to verify this passkey. Use an authenticator code or start sign-in again.","No se pudo verificar esta clave de acceso. Usa un código del autenticador o vuelve a iniciar sesión."],
        unconfirmed: ["FullWorth could not confirm this sign-in. Reload the page to check your session before starting again.","FullWorth no pudo confirmar este inicio de sesión. Recarga la página para comprobar tu sesión antes de volver a empezar."],
        options: ["Could not start passkey verification. Use an authenticator code or start sign-in again.","No se pudo iniciar la verificación de la clave de acceso. Usa un código del autenticador o vuelve a iniciar sesión."],
        noKey: ["No passkey was selected. You can still use an authenticator code.","No se seleccionó ninguna clave de acceso. Puedes usar un código del autenticador."],
        cancelled: ["Cancelled or timed out. You can try again or use an authenticator code.","Se canceló o se agotó el tiempo. Puedes volver a intentarlo o usar un código del autenticador."]
    };
    const language = () => /^es(?:-|$)/i.test(document.documentElement.lang || "") ? "es" : "en";
    const text = key => copy[key][language() === "es" ? 1 : 0];
    class PasskeyError extends Error {
        constructor(key) { super(key); this.key = key; }
    }
    const fail = key => { throw new PasskeyError(key); };
    const knownError = (error, fallback) => error instanceof PasskeyError ? text(error.key) : text(fallback);
    const jsonResponse = response => response.status === 200 && !response.redirected &&
        /^application\/(?:json|[a-z0-9.+-]+\+json)(?:\s*;|$)/i.test(response.headers.get("content-type") || "");
    const validToken = token => typeof token === "string" && !!token.trim() && !/[\x00-\x1f\x7f]/.test(token);

    let busy = false;
    const bytes = value => Uint8Array.from(atob(value.replace(/-/g, "+").replace(/_/g, "/")), c => c.charCodeAt(0));
    const encode = value => btoa(String.fromCharCode(...new Uint8Array(value)))
        .replaceAll("+", "-").replaceAll("/", "_").replace(/=+$/, "");
    function options(json) {
        if (PublicKeyCredential.parseRequestOptionsFromJSON)
            return PublicKeyCredential.parseRequestOptionsFromJSON(json);
        return { ...json, challenge: bytes(json.challenge),
            ...(json.allowCredentials ? { allowCredentials: json.allowCredentials.map(c => ({ ...c, id: bytes(c.id) })) } : {}) };
    }
    function credentialJSON(key) {
        if (key.toJSON) return key.toJSON();
        return { id: key.id, rawId: encode(key.rawId), type: key.type,
            authenticatorAttachment: key.authenticatorAttachment,
            response: { clientDataJSON: encode(key.response.clientDataJSON),
                authenticatorData: encode(key.response.authenticatorData),
                signature: encode(key.response.signature),
                userHandle: key.response.userHandle ? encode(key.response.userHandle) : null },
            clientExtensionResults: key.getClientExtensionResults() };
    }
    async function post(path, body, expectsOptions = false) {
        let token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        if (!validToken(token)) fail("signIn");
        const unconfirmed = "unconfirmed";
        let response;
        try {
            response = await fetch(path, { method: "POST", credentials: "same-origin", cache: "no-store",
                redirect: "error",
                headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": token },
                body: JSON.stringify(body) });
        } catch { fail(expectsOptions ? "options" : unconfirmed); }
        if (response.redirected) fail(expectsOptions ? "options" : unconfirmed);
        if (response.status === 429) fail("rate");
        if ([400, 401, 403, 404, 409, 422].includes(response.status)) fail("verify");
        // Register, remove and login have explicit 204 contracts. A successful-looking
        // JSON/HTML response cannot prove a credential write or authenticate navigation.
        if (!expectsOptions) {
            if (response.status !== 204) fail(unconfirmed);
            return null;
        }
        if (!jsonResponse(response)) fail("options");
        let begin;
        try { begin = await response.json(); } catch { fail("options"); }
        if (!begin || Array.isArray(begin) || typeof begin.ceremonyId !== "string" ||
            begin.ceremonyId.length !== 43 || !begin.options || Array.isArray(begin.options) ||
            typeof begin.options !== "object" || typeof begin.options.challenge !== "string" ||
            !begin.options.challenge.trim()) fail("options");
        return begin;
    }

    // Never trust a hidden returnUrl after a browser credential ceremony.
    // Match the server's local-only policy, including nested percent encodings.
    function localLoginDestination(value) {
        if (typeof value !== "string" || !value.startsWith("/") || value.length > 2048)
            return "/app";
        let decoded = value;
        try {
            for (let round = 0; round < 4; round++) {
                if (!decoded.startsWith("/") || decoded.startsWith("//") ||
                    decoded.includes("\\") || /[\x00-\x1f\x7f]/.test(decoded))
                    return "/app";
                const next = decodeURIComponent(decoded);
                if (next === decoded) return value;
                decoded = next;
            }
        } catch {
            return "/app";
        }
        return "/app";
    }

    function continueAfterPasskey() {
        // A GET form is a browser navigation, not a JavaScript URL sink.
        // The authenticated server endpoint revalidates the return target.
        const form = document.createElement("form");
        form.method = "get";
        form.action = "/auth/passkeys/continue";
        form.hidden = true;
        const destination = document.createElement("input");
        destination.type = "hidden";
        destination.name = "returnUrl";
        destination.value = localLoginDestination(
            document.querySelector('input[name="returnUrl"]')?.value);
        form.append(destination);
        document.body.append(form);
        form.submit();
    }

    document.addEventListener("click", async event => {
        const button = event.target.closest("[data-password-passkey-button]");
        const root = button?.closest("[data-password-passkey]");
        if (!root || busy) return;
        const status = root.querySelector("[data-password-passkey-status]");
        if (!window.isSecureContext || !window.PublicKeyCredential || !navigator.credentials) {
            status.textContent = text("unsupported");
            return;
        }
        busy = true;
        button.disabled = true;
        status.textContent = text("prompt");
        try {
            const begin = await post("/auth/login/passkey-options", {}, true);
            const credential = await navigator.credentials.get({ publicKey: options(begin.options) });
            if (!credential) fail("noKey");
            await post("/auth/login/passkey-complete", { ceremonyId: begin.ceremonyId,
                credentialJson: JSON.stringify(credentialJSON(credential)) });
            continueAfterPasskey();
        } catch (error) {
            status.textContent = error?.name === "NotAllowedError"
                ? text("cancelled")
                : knownError(error, "unconfirmed");
        } finally {
            busy = false;
            if (button.isConnected) button.disabled = false;
        }
    });
})();
