const storageKey = "billwatch.timestamp-display-mode";
const themeStorageKey = "billwatch-theme";
const experienceThemeInitializedKey = "fullworth.experience-theme-initialized";
const localMode = "Local12Hour";
const utcMode = "Utc";

let observer = null;
let preferenceRevision = 0;

function normalizeThemePreference(value) {
    switch (String(value ?? "").toLowerCase()) {
        case "light":
            return "Light";
        case "dark":
            return "Dark";
        default:
            return "System";
    }
}

function normalizeTextSizePreference(value) {
    switch (String(value ?? "").toLowerCase()) {
        case "large":
            return "Large";
        case "extralarge":
        case "extra-large":
            return "ExtraLarge";
        default:
            return "Standard";
    }
}

function resolveTheme(preference) {
    const normalized = normalizeThemePreference(preference);

    if (normalized === "Light") {
        return "light";
    }

    if (normalized === "Dark") {
        return "dark";
    }

    return window.matchMedia?.("(prefers-color-scheme: light)")?.matches
        ? "light"
        : "dark";
}

function readStorage(key) {
    try { return window.localStorage.getItem(key); } catch { return null; }
}

function writeStorage(key, value) {
    try { window.localStorage.setItem(key, value); return true; } catch { return false; }
}

const focusOrder = ["BillChanges", "Spending", "RecurringCosts", "AccountOverview", "Statements"];

function validExperienceResponse(value) {
    return value !== null && typeof value === "object" &&
        typeof value.experienceSetupComplete === "boolean" &&
        ["en-US", "es"].includes(value.preferredUiLanguage) &&
        ["System", "Light", "Dark"].includes(value.themePreference) &&
        ["Standard", "Large", "ExtraLarge"].includes(value.textSizePreference) &&
        typeof value.highContrastEnabled === "boolean" && typeof value.reduceMotionEnabled === "boolean" &&
        Array.isArray(value.experienceFocus) && value.experienceFocus.length <= focusOrder.length &&
        value.experienceFocus.every(item => focusOrder.includes(item)) &&
        new Set(value.experienceFocus).size === value.experienceFocus.length &&
        (!value.experienceSetupComplete || value.experienceFocus.length > 0);
}

function applyThemePreference(preference, force = false) {
    if (!force && readStorage(experienceThemeInitializedKey) === "1") return true;
    const theme = resolveTheme(preference);
    document.documentElement.dataset.theme = theme;
    document.querySelector('meta[name="theme-color"]')?.setAttribute(
        "content", theme === "light" ? "#F4F6FB" : "#060910");
    const remembered = writeStorage(themeStorageKey, theme);
    const initialized = writeStorage(experienceThemeInitializedKey, "1");
    return remembered && initialized;
}

function applyLanguagePreference(language) {
    try {
        const cookieValue = encodeURIComponent(`c=en-US|uic=${language}`);
        document.cookie = `.AspNetCore.Culture=${cookieValue}; Path=/; Max-Age=31536000; SameSite=Lax; Secure`;
        // Cookie writes may be silently rejected by browser privacy settings.
        return document.cookie.split(";").some(part =>
            part.trim() === `.AspNetCore.Culture=${cookieValue}`);
    } catch { return false; }
}

export function applyExperiencePreferences(preference, forceTheme = false) {
    if (forceTheme) preferenceRevision++;
    if (!validExperienceResponse(preference) || preference.experienceSetupComplete !== true) return false;
    document.documentElement.dataset.fullworthTextSize = preference.textSizePreference === "ExtraLarge" ?
        "extra-large" : preference.textSizePreference.toLowerCase();
    document.documentElement.dataset.fullworthHighContrast = preference.highContrastEnabled ? "true" : "false";
    document.documentElement.dataset.fullworthReduceMotion = preference.reduceMotionEnabled ? "true" : "false";
    const themeApplied = applyThemePreference(preference.themePreference, forceTheme);
    const languageApplied = applyLanguagePreference(preference.preferredUiLanguage);
    return themeApplied && languageApplied;
}

function normalizeMode(value) {
    return value === utcMode
        ? utcMode
        : localMode;
}

function getStoredMode() {
    return normalizeMode(
        readStorage(storageKey));
}

function setStoredMode(mode) {
    writeStorage(storageKey, normalizeMode(mode));
}

function parseDate(value) {
    if (!value) {
        return null;
    }

    const date = new Date(value);

    return Number.isNaN(date.getTime())
        ? null
        : date;
}

function pad(value) {
    return String(value).padStart(2, "0");
}

function formatUtc(date, includeTime = true) {
    const dateText =
        `${pad(date.getUTCMonth() + 1)}/${pad(date.getUTCDate())}/${date.getUTCFullYear()}`;

    if (!includeTime) {
        return `${dateText} UTC`;
    }

    return `${dateText} ${pad(date.getUTCHours())}:${pad(date.getUTCMinutes())} UTC`;
}

function formatLocal(date, includeTime = true) {
    const dateText =
        `${pad(date.getMonth() + 1)}/${pad(date.getDate())}/${date.getFullYear()}`;

    if (!includeTime) {
        return dateText;
    }

    const hours = date.getHours();
    const displayHour = hours % 12 || 12;
    const period = hours >= 12 ? "PM" : "AM";

    return `${dateText} ${displayHour}:${pad(date.getMinutes())} ${period}`;
}

function formatByMode(value, includeTime = true) {
    const date = parseDate(value);

    if (!date) {
        return null;
    }

    return getStoredMode() === utcMode
        ? formatUtc(date, includeTime)
        : formatLocal(date, includeTime);
}

function renderTimestampElement(element) {
    const value = element.getAttribute("datetime") ??
        element.dataset.bwTimestamp;
    const formatted = formatByMode(value, true);

    if (formatted) {
        element.textContent = formatted;
    }
}

export function renderTimestamps(root = document) {
    if (root instanceof Element &&
        (root.matches("time[datetime]") ||
         root.matches("[data-bw-timestamp]"))) {
        renderTimestampElement(root);
    }

    root.querySelectorAll?.("time[datetime], [data-bw-timestamp]")
        .forEach(renderTimestampElement);
}

export async function getTimestampPreference() {
    const response = await fetch(
        "/bff/account/preferences",
        {
            credentials: "same-origin",
            cache: "no-store",
            headers: {
                Accept: "application/json"
            }
        });

    if (!response.ok) {
        throw new Error("Could not load timestamp preference.");
    }

    return await response.json();
}

export function getExperiencePreferences() {
    return getTimestampPreference();
}

export async function persistExperiencePreferences(preference) {
    const request = {
        preferredUiLanguage: preference?.preferredUiLanguage, themePreference: preference?.themePreference,
        textSizePreference: preference?.textSizePreference, highContrastEnabled: preference?.highContrastEnabled,
        reduceMotionEnabled: preference?.reduceMotionEnabled,
        experienceFocus: Array.isArray(preference?.experienceFocus) ? [...preference.experienceFocus] : null
    };
    if (!validExperienceResponse({ ...request, experienceSetupComplete: true }))
        throw new Error("Invalid FullWorth experience setup.");
    const antiforgeryResponse = await fetch("/bff/antiforgery", {
        credentials: "same-origin", cache: "no-store", headers: { Accept: "application/json" }
    });
    if (!antiforgeryResponse.ok) throw new Error("Could not initialize secure experience setup.");
    const antiforgery = await antiforgeryResponse.json();
    if (typeof antiforgery?.requestToken !== "string" || !antiforgery.requestToken.trim())
        throw new Error("Could not initialize secure experience setup.");
    const response = await fetch("/bff/account/preferences/experience", {
        method: "PUT", credentials: "same-origin", cache: "no-store",
        headers: { Accept: "application/json", "Content-Type": "application/json", "X-CSRF-TOKEN": antiforgery.requestToken },
        body: JSON.stringify(request)
    });
    if (response.status !== 200 || response.redirected) throw new Error("Could not confirm your FullWorth setup.");
    const saved = await response.json();
    if (!validExperienceResponse(saved) || !saved.experienceSetupComplete ||
        ["preferredUiLanguage", "themePreference", "textSizePreference", "highContrastEnabled", "reduceMotionEnabled"]
            .some(key => saved[key] !== request[key]) ||
        saved.experienceFocus.length !== request.experienceFocus.length ||
        !saved.experienceFocus.every(value => request.experienceFocus.includes(value)))
        throw new Error("Could not confirm your FullWorth setup.");
    return saved; // Server confirmation is deliberately separate from browser application.
}

// Compatibility for older consumers. Browser storage cannot turn a saved server
// choice into a failed write or trigger a repeated PUT.
export async function saveExperiencePreferences(preference) {
    const saved = await persistExperiencePreferences(preference);
    applyExperiencePreferences(saved, true);
    return saved;
}

export async function saveTimestampPreference(mode) {
    const normalizedMode = normalizeMode(mode);

    const antiforgeryResponse = await fetch(
        "/bff/antiforgery",
        {
            credentials: "same-origin",
            cache: "no-store",
            headers: {
                Accept: "application/json"
            }
        });

    if (!antiforgeryResponse.ok) {
        throw new Error("Could not initialize secure preference update.");
    }

    const antiforgery = await antiforgeryResponse.json();

    const response = await fetch(
        "/bff/account/preferences",
        {
            method: "PUT",
            credentials: "same-origin",
            cache: "no-store",
            headers: {
                Accept: "application/json",
                "Content-Type": "application/json",
                "X-CSRF-TOKEN": antiforgery.requestToken
            },
            body: JSON.stringify({
                timestampDisplayMode: normalizedMode
            })
        });

    if (!response.ok) {
        throw new Error("Could not save timestamp preference.");
    }

    const saved = await response.json();
    preferenceRevision++;
    setStoredMode(saved.timestampDisplayMode);
    renderTimestamps(document);

    return saved;
}

export async function initializeTimestampPreferences() {
    const revision = preferenceRevision;
    try {
        const preference = await getTimestampPreference();
        if (revision === preferenceRevision) {
            setStoredMode(preference.timestampDisplayMode);
            applyExperiencePreferences(preference);
        }
    }
    catch {
        if (readStorage(storageKey) === null) {
            setStoredMode(localMode);
        }
    }

    renderTimestamps(document);

    if (observer) {
        observer.disconnect();
    }

    observer = new MutationObserver(mutations => {
        for (const mutation of mutations) {
            mutation.addedNodes.forEach(node => {
                if (node instanceof Element) {
                    renderTimestamps(node);
                }
            });
        }
    });

    observer.observe(
        document.body,
        {
            childList: true,
            subtree: true
        });
}

export function formatLocalDate(value) {
    return formatByMode(value, false);
}

export function formatLocalDateTime(value) {
    return formatByMode(value, true);
}

export function formatConnectionTimestamps(entries) {
    if (!Array.isArray(entries)) {
        return [];
    }

    return entries.map(entry => [
        formatByMode(entry?.[0], false),
        entry?.[1]
            ? formatByMode(entry[1], true)
            : null
    ]);
}
