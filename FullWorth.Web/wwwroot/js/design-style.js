const storageKey = "fullworth-design";
const styles = new Set(["editorial", "workspace", "story"]);

function currentStyle() {
    const value = document.documentElement.dataset.design;
    return styles.has(value) ? value : "editorial";
}

function applyStyle(value) {
    document.documentElement.dataset.design = value;
    const color = window.getComputedStyle?.(document.documentElement)
        .getPropertyValue("--pair-canvas").trim();
    if (color && /^#[0-9a-f]{6}$/i.test(color)) {
        document.querySelector('meta[name="theme-color"]')?.setAttribute("content", color);
    }
    return value;
}

export function initializeDesignStyle() {
    let value = currentStyle();
    try {
        const saved = window.localStorage.getItem(storageKey);
        if (styles.has(saved)) value = saved;
    } catch {
        // The current session remains usable when browser storage is blocked.
    }
    return applyStyle(value);
}

export function selectDesignStyle(value) {
    if (!styles.has(value)) throw new TypeError("Unsupported appearance style.");
    applyStyle(value);
    try { window.localStorage.setItem(storageKey, value); }
    catch { /* Session-only appearance; no account or financial request. */ }
    return value;
}
