(() => {
    "use strict";
    let busy = false;
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

    async function post(path, body) {
        let token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        if (!token) {
            const csrf = await fetch("/bff/antiforgery", { credentials: "same-origin", cache: "no-store" });
            if (!csrf.ok) throw new Error("Sign in again before changing keys.");
            token = (await csrf.json()).requestToken;
        }
        const response = await fetch(path, { method: "POST", credentials: "same-origin", cache: "no-store",
            headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": token },
            body: JSON.stringify(body) });
        if (!response.ok) throw new Error(response.status === 429
            ? "Too many attempts. Wait a minute and try again."
            : "Could not verify this request. Check your credentials and authenticator code, then start again.");
        return response.status === 204 ? null : response.json();
    }

    async function list(root) {
        const response = await fetch("/bff/account/security/passkeys",
            { credentials: "same-origin", cache: "no-store" });
        if (!response.ok) throw new Error("Could not load your keys. Sign in again or try later.");
        const keys = await response.json();
        const target = root.querySelector("[data-passkey-list]");
        target.replaceChildren();
        for (const key of keys) {
            const item = document.createElement("li");
            const label = document.createElement("span");
            label.textContent = key.name + " — added " + new Date(key.createdAt).toLocaleDateString();
            const button = document.createElement("button");
            button.type = "button"; button.textContent = "Remove";
            button.dataset.passkeyAction = "remove"; button.dataset.keyId = key.id;
            item.append(label, " ", button); target.append(item);
        }
        if (!keys.length) target.textContent = "No keys registered yet.";
        root.dataset.loaded = "true";
    }

    document.addEventListener("click", async event => {
        const button = event.target.closest("[data-passkey-action]");
        const root = button?.closest("[data-passkeys]");
        if (!root || busy) return;
        if (!window.isSecureContext || !window.PublicKeyCredential || !navigator.credentials) {
            message(root, "This browser does not support passkeys. Use your password instead."); return;
        }
        busy = true; button.disabled = true;
        message(root, "Follow your device or security key instructions…");
        try {
            const action = button.dataset.passkeyAction;
            const credentials = { currentPassword: value(root, "currentPassword"),
                twoFactorCode: value(root, "twoFactorCode") || null };
            if (action === "remove") {
                if (!confirm("Remove this key from FullWorth? Your password and other keys will still work.")) return;
                await post("/bff/account/security/passkeys/remove",
                    { ...credentials, credentialId: button.dataset.keyId });
            } else {
                const creating = action === "register";
                const begin = await post(creating ? "/bff/account/security/passkeys/options" : "/auth/passkeys/options",
                    creating ? credentials : {});
                const credential = await navigator.credentials[creating ? "create" : "get"](
                    { publicKey: options(begin.options, creating) });
                if (!credential) throw new Error("No key was selected. Your account has not changed.");
                const body = { ceremonyId: begin.ceremonyId,
                    credentialJson: JSON.stringify(credentialJSON(credential)),
                    name: value(root, "keyName"),
                    twoFactorCode: creating ? null : value(root, "passkeyTwoFactorCode") || null,
                    rememberMe: !!document.querySelector('[name="rememberMe"]')?.checked };
                await post(creating ? "/bff/account/security/passkeys/register" : "/auth/passkeys/login", body);
                if (!creating) { location.assign("/app"); return; }
            }
            await list(root);
            message(root, "Saved. Your password and recovery options still work.");
        } catch (error) {
            message(root, error?.name === "NotAllowedError"
                ? "Cancelled or timed out. You can try again or use your password."
                : error instanceof Error ? error.message : "Unable to complete this request.");
        } finally {
            const password = root.querySelector('[name="currentPassword"]');
            if (password) password.value = "";
            busy = false; button.disabled = false;
        }
    });
    function initialize() {
        document.querySelectorAll('[data-passkeys="manage"]:not([data-loaded])').forEach(root => {
            root.dataset.loaded = "pending";
            list(root).catch(error => message(root, error.message));
        });
    }
    document.addEventListener("DOMContentLoaded", initialize);
    new MutationObserver(initialize).observe(document.documentElement, { childList: true, subtree: true });
    initialize();
})();
