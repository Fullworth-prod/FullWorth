import { privacyAntiforgeryToken, privacyError } from "./account-privacy-requests.js";

let active = null;
const downloads = new Map();

// Leaving the page stops pending export work. It cannot erase an already saved file.
export function cancelAccountExport() {
    active?.abort();
    active = null;
    for (const [url, timer] of downloads) {
        window.clearTimeout(timer);
        URL.revokeObjectURL(url);
    }
    downloads.clear();
}

export async function downloadAccountExport(currentPassword, twoFactorCode) {
    if (typeof currentPassword !== "string" || !currentPassword.trim())
        throw new Error("Enter your current password to export your account data.");
    if (active) throw new Error("An export request is already in progress.");
    const operation = new AbortController();
    active = operation;
    let url = null;
    let anchor = null;
    let requested = false;
    try {
        const token = await privacyAntiforgeryToken(operation.signal);
        if (operation.signal.aborted || active !== operation) return false;
        const response = await fetch("/bff/account/export", {
            method: "POST", credentials: "same-origin",
            headers: { Accept: "application/json", "Content-Type": "application/json", "X-CSRF-TOKEN": token },
            body: JSON.stringify({ currentPassword,
                twoFactorCode: typeof twoFactorCode === "string" && twoFactorCode.trim() ? twoFactorCode.trim() : null }),
            redirect: "error", cache: "no-store", signal: operation.signal
        });
        if (!response.ok) throw new Error(await privacyError(response, "export"));
        const type = (response.headers.get("content-type") ?? "").split(";")[0].trim().toLowerCase();
        if (response.status !== 200 || response.redirected || type !== "application/json")
            throw new Error("FullWorth did not return a JSON export.");
        const blob = await response.blob();
        if (blob.size === 0) throw new Error("FullWorth returned an empty export.");
        if (operation.signal.aborted || active !== operation) return false;
        url = URL.createObjectURL(blob);
        anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = "fullworth-data-export.json";
        anchor.rel = "noopener";
        document.body.appendChild(anchor);
        anchor.click();
        requested = true;
        return true; // A browser download was requested, not proof of a file saved on disk.
    } finally {
        anchor?.remove();
        if (url) {
            if (requested && !operation.signal.aborted) {
                const downloadUrl = url;
                const timer = window.setTimeout(() => {
                    URL.revokeObjectURL(downloadUrl);
                    downloads.delete(downloadUrl);
                }, 1000);
                downloads.set(url, timer);
            } else {
                URL.revokeObjectURL(url);
            }
        }
        if (active === operation) active = null;
    }
}
