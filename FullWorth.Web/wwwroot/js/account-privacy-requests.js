// Privacy requests are explicit and never automatically replayed.
export async function privacyError(response) {
    let message = "";
    const type = (response.headers.get("content-type") ?? "").split(";")[0].trim().toLowerCase();
    if (type === "application/json" || type === "application/problem+json") {
        try {
            const payload = await response.json();
            message = typeof payload?.message === "string" ? payload.message :
                typeof payload?.title === "string" ? payload.title : "";
        } catch { /* Never reflect arbitrary response bodies into the UI or JS errors. */ }
    }
    const normalized = message.toLowerCase();
    if (normalized.includes("current password is incorrect")) return "Current password is incorrect.";
    if (normalized.includes("authenticator code")) return "A valid current authenticator code is required.";
    if (normalized.includes("staff accounts cannot be self-deleted")) return "Staff accounts cannot be self-deleted.";
    if (normalized.includes("bank connection could not be revoked")) return "A bank connection could not be revoked.";
    if (normalized.includes("stored statement") && normalized.includes("account was not deleted"))
        return "Stored statement preparation failed; account was not deleted.";
    if (response.status === 429) return "Too many attempts. Please wait before trying again.";
    if (response.status === 401) return "FullWorth session expired.";
    return "FullWorth could not confirm this privacy request.";
}

export async function privacyAntiforgeryToken(signal) {
    const response = await fetch("/bff/antiforgery", {
        method: "GET", credentials: "same-origin", headers: { Accept: "application/json" },
        cache: "no-store", signal
    });
    if (!response.ok) throw new Error(await privacyError(response));
    const payload = await response.json();
    if (typeof payload?.requestToken !== "string" || !payload.requestToken.trim())
        throw new Error("FullWorth could not establish a secure request token.");
    return payload.requestToken;
}
