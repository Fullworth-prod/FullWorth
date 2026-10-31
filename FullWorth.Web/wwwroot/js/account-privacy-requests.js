// Privacy requests are explicit and never automatically replayed.
const unknownPrivacyResult = "FullWorth could not confirm this privacy request.";
const deletionFailures = new Map([
    [409, ["FullWorth staff accounts cannot be self-deleted while privileged roles are assigned. Remove the staff roles through the authorized admin workflow first.", "Staff accounts cannot be self-deleted."]],
    [503, ["FullWorth could not safely finish deleting your account because a bank connection could not be revoked. Your FullWorth account was not deleted. Try again shortly.", "A bank connection could not be revoked."]],
    [500, ["FullWorth could not securely prepare stored statement files for deletion. Your FullWorth account was not deleted. Try again.", "Stored statement preparation failed; account was not deleted."]]
]);

export async function privacyError(response, operation) {
    if (response.redirected) return unknownPrivacyResult;
    if (response.status === 429) return "Too many attempts. Please wait before trying again.";
    const proofOperation = operation === "delete" || operation === "export";
    const deletionFailure = operation === "delete" ? deletionFailures.get(response.status) : null;
    // A server failure cannot be recast as incorrect proof. Only the current
    // API's exact, status-bound pre-deletion receipts can declare no deletion.
    if (!(response.status === 401 && proofOperation) && !deletionFailure)
        return response.status === 401 ? "FullWorth session expired." : unknownPrivacyResult;

    let message = "";
    let payload = null;
    const type = (response.headers.get("content-type") ?? "").split(";")[0].trim().toLowerCase();
    if (type === "application/json" || type === "application/problem+json") {
        try {
            const parsed = await response.json();
            if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
                payload = parsed;
                message = typeof payload.message === "string" ? payload.message.trim() :
                    typeof payload.title === "string" ? payload.title.trim() : "";
            }
        } catch { /* Never reflect arbitrary response bodies into UI or JS errors. */ }
    }
    if (response.status === 401) {
        if (message === "Current password is incorrect.") return "Current password is incorrect.";
        if (message === "A current authenticator code is required." ||
            message === "The authenticator code is invalid.")
            return "A valid current authenticator code is required.";
        return "FullWorth session expired.";
    }
    if (deletionFailure && type === "application/json" && payload &&
        Object.keys(payload).length === 1 && typeof payload.message === "string" &&
        message === deletionFailure[0])
        return deletionFailure[1];
    return unknownPrivacyResult;
}

export async function privacyAntiforgeryToken(signal) {
    const response = await fetch("/bff/antiforgery", {
        method: "GET", credentials: "same-origin", headers: { Accept: "application/json" },
        redirect: "error", cache: "no-store", signal
    });
    if (!response.ok) throw new Error(await privacyError(response));
    const type = (response.headers.get("content-type") ?? "").split(";")[0].trim().toLowerCase();
    if (response.status !== 200 || response.redirected || type !== "application/json")
        throw new Error("FullWorth could not establish a secure request token.");
    const payload = await response.json();
    if (!payload || typeof payload !== "object" || Array.isArray(payload) ||
        typeof payload.requestToken !== "string" || !payload.requestToken.trim() ||
        payload.requestToken.length > 4096 || !/^[\x21-\x7e]+$/.test(payload.requestToken))
        throw new Error("FullWorth could not establish a secure request token.");
    return payload.requestToken;
}
