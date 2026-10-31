import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

// Bounded fixture reader for the current literal/star path filters, not a
// general YAML or Actions-event implementation. Real PR runs verify dispatch.
function eventPaths(source, event) {
    const lines = source.split(/\r?\n/);
    const start = lines.findIndex(line => line === "  " + event + ":");
    assert.ok(start >= 0, event + " event is missing");
    let reading = false;
    const paths = [];
    for (const line of lines.slice(start + 1)) {
        if (line.trim() === "" || line.trimStart().startsWith("#")) continue;
        if (/^\S|^  \S/.test(line)) break;
        if (line === "    paths:") { reading = true; continue; }
        if (!reading) continue;
        const item = line.match(/^      - "([^"]+)"$/);
        if (!item) break;
        paths.push(item[1]);
    }
    assert.ok(paths.length > 0, event + " paths are missing");
    return paths;
}

// GitHub's * excludes /, while ** includes it. Fail closed if this bounded
// harness encounters a pattern feature it does not model.
// https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#filter-pattern-cheat-sheet
function matches(pattern, path) {
    assert.doesNotMatch(pattern, /[!?[\]{}]/, "Review new path-pattern syntax");
    let expression = "^";
    for (let i = 0; i < pattern.length; i++) {
        const c = pattern[i];
        if (c === "*" && pattern[i + 1] === "*") {
            if (pattern[i + 2] === "/") { expression += "(?:.*/)?"; i += 2; }
            else { expression += ".*"; i++; }
        } else if (c === "*") expression += "[^/]*";
        else expression += c.replace(/[\\^$.*+?()[\]{}|]/g, "\\$&");
    }
    return new RegExp(expression + "$").test(path);
}

test("Path fixture keeps root-only and recursive patterns distinct", () => {
    assert.equal(matches("*.cs", "MainPage.xaml.cs"), true);
    assert.equal(matches("*.cs", "ViewModels/BillsPageViewModel.cs"), false);
    assert.equal(matches("ViewModels/**", "ViewModels/BillsPageViewModel.cs"), true);
    assert.equal(matches("Services/**", "FullWorth.Web/Services/Example.cs"), false);
});

const workflows = [
    { name: "android", file: new URL("../.github/workflows/android-internal-apk.yml", import.meta.url) },
    { name: "ios", file: new URL("../.github/workflows/ios-internal-simulator.yml", import.meta.url) }
];
for (const workflow of workflows) {
    const source = readFileSync(workflow.file, "utf8");
    for (const event of ["pull_request", "push"]) {
        test(workflow.name + " " + event + " packages native service and view-model changes", () => {
            const patterns = eventPaths(source, event);
            const selected = path => patterns.some(pattern => matches(pattern, path));
            for (const path of ["MainPage.xaml.cs", "FullWorth.csproj",
                "FullWorth.Core/Formatting/FinancialAmountFormatter.cs", "Resources/Styles/Colors.xaml"]) {
                assert.equal(selected(path), true, "Existing package scope: " + path);
            }
            for (const path of ["Services/BillAlertService.cs", "Services/FullWorthApiClient.cs",
                "ViewModels/MainPageViewModel.cs", "ViewModels/BillsPageViewModel.cs"]) {
                assert.equal(selected(path), true, "Native source was omitted: " + path);
            }
            for (const path of ["FullWorth.Web/Services/Example.cs",
                "FullWorth.Tests/Native/ExampleTests.cs", "FULLWORTH_CONTEXT.md"]) {
                assert.equal(selected(path), false, "Unrelated change expanded packaging: " + path);
            }
        });
    }
}
