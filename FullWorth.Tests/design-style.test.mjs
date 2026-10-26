import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import vm from "node:vm";

const designSource = await readFile(new URL("../FullWorth.Web/wwwroot/js/design-style.js", import.meta.url), "utf8");
const bootstrapSource = await readFile(new URL("../FullWorth.Web/wwwroot/js/theme-bootstrap.js", import.meta.url), "utf8");
const themeSource = await readFile(new URL("../FullWorth.Web/wwwroot/js/theme.js", import.meta.url), "utf8");
const colors = {
    editorial: { light: "#fafaf8", dark: "#10151d" },
    workspace: { light: "#f7faff", dark: "#0b1728" },
    story: { light: "#faf7f0", dark: "#141210" }
};

function fixture(saved = {}, blocked = false) {
    const writes = [], requests = [], meta = [];
    const root = { dataset: { design: "editorial", theme: "dark" } };
    const storage = {
        getItem(key) { if (blocked) throw new Error("Storage denied"); return saved[key] ?? null; },
        setItem(key, value) { if (blocked) throw new Error("Storage denied"); writes.push([key, value]); saved[key] = value; }
    };
    const document = { documentElement: root, querySelector: selector => {
        assert.equal(selector, 'meta[name="theme-color"]');
        return { setAttribute: (name, value) => meta.push([name, value]) };
    }};
    const context = vm.createContext({
        document, TypeError,
        window: { localStorage: storage, getComputedStyle: () => ({
            getPropertyValue: name => { assert.equal(name, "--pair-canvas"); return colors[root.dataset.design][root.dataset.theme]; }
        }) },
        localStorage: storage,
        fetch: (...args) => { requests.push(args); throw new Error("Appearance must not send requests"); }
    });
    return { context, root, writes, requests, meta };
}
function client(f) {
    vm.runInContext(designSource.replace(/^export /gm, "") +
        "\nglobalThis.client = { initializeDesignStyle, selectDesignStyle };", f.context);
    return f.context.client;
}

for (const style of Object.keys(colors)) for (const mode of ["light", "dark"]) {
    test("Bootstrap preserves " + style + "/" + mode + " before rendering without writes", () => {
        const f = fixture({ "fullworth-design": style, "billwatch-theme": mode });
        vm.runInContext(bootstrapSource, f.context);
        assert.equal(f.root.dataset.design, style);
        assert.equal(f.root.dataset.theme, mode);
        assert.deepEqual(f.meta.at(-1), ["content", colors[style][mode]]);
        assert.equal(f.writes.length, 0);
        assert.equal(f.requests.length, 0);
    });
}
for (const invalid of ["unknown", "__proto__", "<img src=x>", null]) {
    test("Untrusted stored style is rejected: " + String(invalid), () => {
        const f = fixture({ "fullworth-design": invalid, "billwatch-theme": "light" });
        vm.runInContext(bootstrapSource, f.context);
        assert.equal(f.root.dataset.design, "editorial");
        assert.equal(f.root.dataset.theme, "light");
        assert.equal(client(f).initializeDesignStyle(), "editorial");
        assert.equal(f.writes.length, 0);
        assert.equal(f.requests.length, 0);
    });
}
for (const style of Object.keys(colors)) {
    test("Selecting " + style + " changes appearance only, preserving light mode", () => {
        const f = fixture();
        f.root.dataset.theme = "light";
        assert.equal(client(f).selectDesignStyle(style), style);
        assert.equal(f.root.dataset.design, style);
        assert.equal(f.root.dataset.theme, "light");
        assert.deepEqual(f.writes, [["fullworth-design", style]]);
        assert.deepEqual(f.meta.at(-1), ["content", colors[style].light]);
        assert.equal(f.requests.length, 0);
    });
    test("Reload initializes " + style + " without re-saving or a financial call", () => {
        const f = fixture({ "fullworth-design": style });
        assert.equal(client(f).initializeDesignStyle(), style);
        assert.equal(f.root.dataset.design, style);
        assert.equal(f.writes.length, 0);
        assert.equal(f.requests.length, 0);
    });
    test("Existing light/dark toggle retains " + style + " and updates browser chrome", () => {
        const f = fixture({ "billwatch-theme": "light" });
        f.root.dataset.design = style;
        vm.runInContext(themeSource.replace(/^export /gm, "") +
            "\nglobalThis.themeClient = { initializeTheme, toggleTheme };", f.context);
        assert.equal(f.context.themeClient.initializeTheme(), false);
        assert.equal(f.root.dataset.theme, "light");
        assert.deepEqual(f.meta.at(-1), ["content", colors[style].light]);
        assert.equal(f.context.themeClient.toggleTheme(), true);
        assert.equal(f.root.dataset.theme, "dark");
        assert.equal(f.root.dataset.design, style);
        assert.deepEqual(f.meta.at(-1), ["content", colors[style].dark]);
        assert.deepEqual(f.writes, [["billwatch-theme", "dark"]]);
        assert.equal(f.requests.length, 0);
    });
}
test("Blocked storage retains usable session appearance without crashing or sending requests", () => {
    const f = fixture({}, true);
    vm.runInContext(bootstrapSource, f.context);
    assert.equal(f.root.dataset.design, "editorial");
    assert.equal(f.root.dataset.theme, "dark");
    const module = client(f);
    assert.equal(module.selectDesignStyle("workspace"), "workspace");
    assert.equal(module.initializeDesignStyle(), "workspace");
    assert.equal(f.root.dataset.design, "workspace");
    assert.equal(f.requests.length, 0);
});
test("Invalid selections fail before DOM changes or persistence", () => {
    const f = fixture();
    const module = client(f);
    for (const invalid of ["__proto__", "Editorial", "light", null, {}, ["story"]]) {
        assert.throws(() => module.selectDesignStyle(invalid), /Unsupported appearance style/);
        assert.equal(f.root.dataset.design, "editorial");
    }
    assert.equal(f.writes.length, 0);
    assert.equal(f.meta.length, 0);
    assert.equal(f.requests.length, 0);
});
