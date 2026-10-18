// Release notes are curated for each user-facing update. Publishing a build
// without new notes must not pretend it has new features to announce.
const endpoint = "/release-notes.json";
const seenKey = "fullworth.whats-new.seen-notes";
const checkIntervalMs = 5 * 60 * 1000;
const allowedIcons = new Set(["scan", "trend-up", "shield", "check"]);

let callback = null;
let intervalId = null;
let pendingCheck = null;
let announcedId = null;
let seenInMemory = null;

function validNotes(notes) {
    return notes && typeof notes === "object" &&
        typeof notes.id === "string" &&
        /^[a-z0-9][a-z0-9._-]{3,79}$/.test(notes.id) &&
        typeof notes.title === "string" && notes.title.length > 0 && notes.title.length <= 110 &&
        typeof notes.summary === "string" && notes.summary.length > 0 && notes.summary.length <= 400 &&
        typeof notes.version === "string" && notes.version.length > 0 && notes.version.length <= 90 &&
        Array.isArray(notes.highlights) && notes.highlights.length >= 1 && notes.highlights.length <= 6 &&
        notes.highlights.every(item =>
            item && allowedIcons.has(item.icon) &&
            typeof item.title === "string" && item.title.length > 0 && item.title.length <= 110 &&
            typeof item.description === "string" && item.description.length > 0 && item.description.length <= 450);
}

function alreadyRead(id) {
    if (seenInMemory === id) return true;

    try {
        return window.localStorage.getItem(seenKey) === id;
    }
    catch {
        // Private-mode/storage restrictions should not break the app.
        return false;
    }
}

async function requestNotes() {
    if (!callback || document.visibilityState === "hidden") return;

    try {
        const response = await fetch(endpoint, {
            cache: "no-store",
            credentials: "same-origin",
            headers: { Accept: "application/json" }
        });

        if (!response.ok) return;

        const notes = await response.json();
        if (!validNotes(notes) || alreadyRead(notes.id) || announcedId === notes.id) return;

        // Do not repeatedly prompt during periodic checks while it is open.
        announcedId = notes.id;
        try {
            await callback.invokeMethodAsync("OnNotesAvailable", notes);
        }
        catch {
            // Reconnected Blazor circuits can subscribe again and retry.
            announcedId = null;
        }
    }
    catch {
        // Network loss and JSON errors are non-fatal; retry on return/focus.
    }
}

function check() {
    if (pendingCheck) return pendingCheck;
    pendingCheck = requestNotes().finally(() => { pendingCheck = null; });
    return pendingCheck;
}

function onVisible() {
    if (document.visibilityState !== "hidden") void check();
}

function onReturn() {
    void check();
}

export async function start(dotNetCallback) {
    stop();
    callback = dotNetCallback;
    announcedId = null;
    document.addEventListener("visibilitychange", onVisible);
    window.addEventListener("focus", onReturn);
    window.addEventListener("pageshow", onReturn);
    intervalId = window.setInterval(() => { void check(); }, checkIntervalMs);
    await check();
}

export function acknowledge(id) {
    if (typeof id !== "string") return;
    seenInMemory = id;
    try {
        window.localStorage.setItem(seenKey, id);
    }
    catch {
        // The current session still dismisses even without storage access.
    }
}

export function stop() {
    callback = null;
    document.removeEventListener("visibilitychange", onVisible);
    window.removeEventListener("focus", onReturn);
    window.removeEventListener("pageshow", onReturn);
    if (intervalId !== null) window.clearInterval(intervalId);
    intervalId = null;
}
