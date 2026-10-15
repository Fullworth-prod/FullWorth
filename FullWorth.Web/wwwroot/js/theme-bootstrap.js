(() => {
    const storageKey = "billwatch-theme";
    const themeColors = {
        dark: "#060910",
        light: "#F4F6FB"
    };
    let theme = "dark";

    try {
        const savedTheme = window.localStorage.getItem(storageKey);

        if (savedTheme === "light" || savedTheme === "dark") {
            theme = savedTheme;
        }
    }
    catch {
        // Restricted storage contexts still receive the secure dark default.
    }

    document.documentElement.dataset.theme = theme;

    const themeColor =
        document.querySelector(
            'meta[name="theme-color"]');

    if (themeColor) {
        themeColor.setAttribute(
            "content",
            themeColors[theme]);
    }
})();
