(() => {
    const storageKey = "billwatch-theme";
    const themeColors = {
        dark: "#060910",
        light: "#F4F6FB"
    };
    let theme = "dark";
    let design = "editorial";

    try {
        const savedTheme = window.localStorage.getItem(storageKey);
        const savedDesign = window.localStorage.getItem("fullworth-design");
        if (["editorial", "workspace", "story"].includes(savedDesign)) design = savedDesign;

        if (savedTheme === "light" || savedTheme === "dark") {
            theme = savedTheme;
        }
    }
    catch {
        // Restricted storage contexts still receive the secure dark default.
    }

    document.documentElement.dataset.design = design;
    document.documentElement.dataset.theme = theme;

    const themeColor =
        document.querySelector(
            'meta[name="theme-color"]');

    if (themeColor) {
        themeColor.setAttribute(
            "content",
            ({
                editorial: { dark: "#10151d", light: "#fafaf8" },
                workspace: { dark: "#0b1728", light: "#f7faff" },
                story: { dark: "#141210", light: "#faf7f0" }
            })[design]?.[theme] ?? themeColors[theme]);
    }
})();
