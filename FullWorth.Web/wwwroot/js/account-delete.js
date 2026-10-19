import { privacyAntiforgeryToken, privacyError } from "./account-privacy-requests.js";

export async function deleteFullWorthAccount(confirmation, currentPassword, twoFactorCode) {
    if (confirmation !== "DELETE")
        throw new Error("Type DELETE to confirm permanent account deletion.");
    if (typeof currentPassword !== "string" || !currentPassword.trim())
        throw new Error("Enter your current password to confirm permanent account deletion.");

    const requestToken = await privacyAntiforgeryToken();
    const response = await fetch("/bff/account", {
        method: "DELETE", credentials: "same-origin",
        headers: { Accept: "application/json", "Content-Type": "application/json", "X-CSRF-TOKEN": requestToken },
        body: JSON.stringify({ confirmation, currentPassword,
            twoFactorCode: typeof twoFactorCode === "string" && twoFactorCode.trim() ? twoFactorCode.trim() : null }),
        cache: "no-store"
    });
    if (!response.ok) throw new Error(await privacyError(response));
    // The API commits account deletion before either response. A 202 still means
    // quarantined file cleanup is pending; this does not certify immediate file erasure.
    if (response.status !== 204 && response.status !== 202)
        throw new Error("FullWorth could not confirm the deletion result.");
    return true;
}
