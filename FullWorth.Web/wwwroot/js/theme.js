const storageKey = "billwatch-theme";
const themeColors = {
    dark: "#060910",
    light: "#F4F6FB"
};

function applyTheme(theme) {
    document.documentElement.dataset.theme = theme;

    const themeColor =
        document.querySelector(
            'meta[name="theme-color"]');

    if (themeColor) {
        themeColor.setAttribute(
            "content",
            window.getComputedStyle?.(document.documentElement)
                .getPropertyValue("--pair-canvas").trim() || themeColors[theme]);
    }
}

function getSavedTheme() {
    try {
        const savedTheme = localStorage.getItem(storageKey);

        if (savedTheme === "light" || savedTheme === "dark") {
            return savedTheme;
        }
    } catch {
        // Local storage may be unavailable in restricted browser contexts.
    }

    return null;
}

function saveTheme(theme) {
    try {
        localStorage.setItem(storageKey, theme);
    } catch {
        // Theme still works for this session if local storage is unavailable.
    }
}

export function initializeTheme() {
    const savedTheme = getSavedTheme();

    // FullWorth defaults to dark mode.
    const theme = savedTheme ?? "dark";

    applyTheme(theme);

    return theme === "dark";
}

export function toggleTheme() {
    const currentTheme =
        document.documentElement.dataset.theme === "light"
            ? "light"
            : "dark";

    const nextTheme =
        currentTheme === "dark"
            ? "light"
            : "dark";

    applyTheme(nextTheme);
    saveTheme(nextTheme);

    return nextTheme === "dark";
}