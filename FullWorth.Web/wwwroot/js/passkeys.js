(() => {
    "use strict";
    // Only fixed UI messages are translated; key names and credential data are never rewritten.
    const copy = {
        unsupported: ["This browser does not support passkeys. Use your password instead.","Este navegador no admite claves de acceso. Usa tu contraseña."],
        prompt: ["Follow your device or security key instructions…","Sigue las instrucciones de tu dispositivo o llave de seguridad…"],
        signIn: ["Sign in again before changing keys.","Vuelve a iniciar sesión antes de cambiar las claves."],
        rate: ["Too many attempts. Wait a minute and try again.","Demasiados intentos. Espera un minuto y vuelve a intentarlo."],
        verify: ["Could not verify this request. Check your credentials and authenticator code, then start again.","No se pudo verificar esta solicitud. Comprueba tu contraseña y el código del autenticador y vuelve a empezar."],
        unconfirmed: ["FullWorth could not confirm this change. Review your saved keys before trying again.","FullWorth no pudo confirmar este cambio. Revisa tus claves guardadas antes de volver a intentarlo."],
        loginUnconfirmed: ["FullWorth could not confirm this sign-in. Reload the page to check your session before starting again.","FullWorth no pudo confirmar este inicio de sesión. Recarga la página para comprobar tu sesión antes de volver a empezar."],
        options: ["Could not start passkey verification. Start again or use your password.","No se pudo iniciar la verificación de la clave de acceso. Vuelve a empezar o usa tu contraseña."],
        checking: ["Checking saved keys…","Comprobando las claves guardadas…"],
        expired: ["Your session expired. Sign in again to review saved keys.","Tu sesión ha caducado. Vuelve a iniciar sesión para revisar las claves guardadas."],
        load: ["Could not load your keys. Try again.","No se pudieron cargar tus claves. Vuelve a intentarlo."],
        invalid: ["FullWorth returned an invalid key list. Try again.","FullWorth devolvió una lista de claves no válida. Vuelve a intentarlo."],
        remove: ["Remove","Eliminar"],
        added: ["added","añadida el"],
        empty: ["No keys registered yet.","Aún no hay claves registradas."],
        loaded: ["Saved keys loaded.","Se cargaron las claves guardadas."],
        confirm: ["Remove this key from FullWorth? Your password and other keys will still work.","¿Quieres eliminar esta clave de FullWorth? Tu contraseña y las demás claves seguirán funcionando."],
        noKey: ["No key was selected. Your account has not changed.","No se seleccionó ninguna clave. Tu cuenta no ha cambiado."],
        saved: ["Saved. Your password and recovery options still work.","Guardado. Tu contraseña y las opciones de recuperación siguen funcionando."],
        cancelled: ["Cancelled or timed out. You can try again or use your password.","Se canceló o se agotó el tiempo. Puedes volver a intentarlo o usar tu contraseña."]
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
    const listVersions = new WeakMap();
    const message = (root, text) => { root.querySelector("[data-passkey-status]").textContent = text; };
    const value = (root, name) => root.querySelector('[name="' + name + '"]')?.value || "";
    const bytes = text => Uint8Array.from(atob(text.replace(/-/g, "+").replace(/_/g, "/")), c => c.charCodeAt(0));
    const encoded = buffer => btoa(String.fromCharCode(...new Uint8Array(buffer)))
        .replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");

    function options(json, creating) {
        if (creating && PublicKeyCredential.parseCreationOptionsFromJSON)
            return PublicKeyCredential.parseCreationOptionsFromJSON(json);
        if (!creating && PublicKeyCredential.parseRequestOptionsFromJSON)
            return PublicKeyCredential.parseRequestOptionsFromJSON(json);
        const result = { ...json, challenge: bytes(json.challenge) };
        if (creating) result.user = { ...json.user, id: bytes(json.user.id) };
        for (const name of ["excludeCredentials", "allowCredentials"])
            if (json[name]) result[name] = json[name].map(c => ({ ...c, id: bytes(c.id) }));
        return result;
    }

    function credentialJSON(credential) {
        if (credential.toJSON) return credential.toJSON();
        const response = { clientDataJSON: encoded(credential.response.clientDataJSON) };
        for (const name of ["attestationObject", "authenticatorData", "signature", "userHandle"])
            if (credential.response[name]) response[name] = encoded(credential.response[name]);
        if (credential.response.getTransports) response.transports = credential.response.getTransports();
        return { id: credential.id, rawId: encoded(credential.rawId), type: credential.type,
            authenticatorAttachment: credential.authenticatorAttachment,
            response, clientExtensionResults: credential.getClientExtensionResults() };
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

    async function post(path, body, expectsOptions = false) {
        let token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        if (!token) {
            try {
                const csrf = await fetch("/bff/antiforgery",
                    { credentials: "same-origin", cache: "no-store", redirect: "error" });
                if (!jsonResponse(csrf)) fail("signIn");
                const payload = await csrf.json();
                if (!payload || Array.isArray(payload)) fail("signIn");
                token = payload.requestToken;
            } catch { fail("signIn"); }
        }
        if (!validToken(token)) fail("signIn");
        const unconfirmed = path === "/auth/passkeys/login" ? "loginUnconfirmed" : "unconfirmed";
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

    async function list(root) {
        const version = (listVersions.get(root) || 0) + 1;
        listVersions.set(root, version);
        const retry = root.querySelector('[data-passkey-action="retry"]');
        if (retry) { retry.hidden = true; retry.disabled = true; }
        if (root.isConnected) {
            root.dataset.loaded = "pending";
            message(root, text("checking"));
        }

        try {
            const response = await fetch("/bff/account/security/passkeys",
                { credentials: "same-origin", cache: "no-store", redirect: "error" });
            if (response.status === 401)
                fail("expired");
            if (!jsonResponse(response)) fail("load");
            const keys = await response.json();
            if (!Array.isArray(keys) || keys.length > 200 ||
                keys.some(key => !key || typeof key.id !== "string" || !key.id.trim() ||
                    typeof key.name !== "string" || !key.name.trim() ||
                    typeof key.createdAt !== "string" ||
                    !Number.isFinite(Date.parse(key.createdAt))) ||
                new Set(keys.map(key => key.id)).size !== keys.length)
                fail("invalid");

            // An older request or an abandoned hydration root cannot replace
            // the current account's visible list.
            if (!root.isConnected || listVersions.get(root) !== version) return false;
            const target = root.querySelector("[data-passkey-list]");
            const fragment = document.createDocumentFragment();
            for (const key of keys) {
                const item = document.createElement("li");
                const label = document.createElement("span");
                label.textContent = key.name + " — " + text("added") + " " + new Date(key.createdAt).toLocaleDateString(language());
                const button = document.createElement("button");
                button.type = "button"; button.textContent = text("remove");
                button.dataset.passkeyAction = "remove"; button.dataset.keyId = key.id;
                item.append(label, " ", button); fragment.append(item);
            }
            target.replaceChildren(fragment);
            if (!keys.length) target.textContent = text("empty");
            root.dataset.loaded = "true";
            if (retry) retry.hidden = true;
            message(root, keys.length ? text("loaded") : text("empty"));
            return true;
        } catch (error) {
            // Never erase previously verified keys because a later read failed.
            if (root.isConnected && listVersions.get(root) === version) {
                root.dataset.loaded = "error";
                if (retry) retry.hidden = false;
                message(root, knownError(error, "load"));
            }
            return false;
        } finally {
            if (root.isConnected && listVersions.get(root) === version && retry)
                retry.disabled = false;
        }
    }

    document.addEventListener("click", async event => {
        const button = event.target.closest("[data-passkey-action]");
        const root = button?.closest("[data-passkeys]");
        if (!root || busy) return;
        // Listing keys is read-only; no WebAuthn availability or credentials
        // are needed to recover a failed authenticated GET.
        if (button.dataset.passkeyAction === "retry") {
            busy = true; button.disabled = true;
            try { await list(root); }
            finally { busy = false; button.disabled = false; }
            return;
        }
        const action = button.dataset.passkeyAction;
        if (!["remove", "register", "login"].includes(action)) return;
        if (!window.isSecureContext || (action !== "remove" &&
            (!window.PublicKeyCredential || !navigator.credentials))) {
            message(root, text("unsupported")); return;
        }
        busy = true; button.disabled = true;
        message(root, text("prompt"));
        try {
            // Capture user input before the asynchronous OS credential prompt.
            const keyName = value(root, "keyName");
            const credentials = { currentPassword: value(root, "currentPassword"),
                twoFactorCode: value(root, "twoFactorCode") || null };
            if (action === "remove") {
                if (!confirm(text("confirm"))) return;
                await post("/bff/account/security/passkeys/remove",
                    { ...credentials, credentialId: button.dataset.keyId });
            } else {
                const creating = action === "register";
                const begin = await post(creating ? "/bff/account/security/passkeys/options" : "/auth/passkeys/options",
                    creating ? credentials : {}, true);
                const credential = await navigator.credentials[creating ? "create" : "get"](
                    { publicKey: options(begin.options, creating) });
                if (!credential) fail("noKey");
                const body = { ceremonyId: begin.ceremonyId,
                    credentialJson: JSON.stringify(credentialJSON(credential)),
                    name: keyName,
                    twoFactorCode: creating ? null : value(root, "passkeyTwoFactorCode") || null,
                    rememberMe: !!document.querySelector('[name="rememberMe"]')?.checked };
                await post(creating ? "/bff/account/security/passkeys/register" : "/auth/passkeys/login", body);
                if (!creating) { continueAfterPasskey(); return; }
            }
            if (root.isConnected) {
                if (await list(root))
                    message(root, text("saved"));
            } else {
                // Refresh only through the current authenticated BFF session.
                // Do not move credentials or old-operation success messages
                // into a replacement region (possibly a different account).
                document.querySelectorAll('[data-passkeys="manage"]').forEach(current => {
                    void list(current);
                });
            }
        } catch (error) {
            message(root, error?.name === "NotAllowedError"
                ? text("cancelled")
                : knownError(error, action === "login" ? "loginUnconfirmed" : "unconfirmed"));
        } finally {
            const password = root.querySelector('[name="currentPassword"]');
            if (password) password.value = "";
            busy = false; button.disabled = false;
        }
    });
    function initialize() {
        document.querySelectorAll('[data-passkeys="manage"]:not([data-loaded])').forEach(root => {
            root.dataset.loaded = "pending";
            void list(root);
        });
    }
    document.addEventListener("DOMContentLoaded", initialize);
    new MutationObserver(initialize).observe(document.documentElement, { childList: true, subtree: true });
    initialize();
})();
