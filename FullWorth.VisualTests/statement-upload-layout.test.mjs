import assert from "node:assert/strict";

// Synthetic message text exercises the real CSS in the rendered, authenticated
// statement component without uploading or retaining a sensitive document.
export async function verifyStatementUploadLayout(page) {
  const id = "aa111111-1111-4111-8111-111111111111";
  const match = new RegExp("/bff/bill-streams/" + id + "(?:\\?.*)?$");
  const stub = async route => {
    assert.equal(route.request().method(), "GET");
    await route.fulfill({
      json: {
        id, providerName: "Synthetic Test Utilities",
        category: "Utility", isActive: true, source: "Manual",
        hasTransactionEvidence: false, currentAmount: 0,
        previousAverage: 0, statements: [], changes: []
      }
    });
  };
  await page.route(match, stub);
  try {
    for (const width of [320, 390, 430]) {
      await page.setViewportSize({ width, height: 844 });
      await page.goto("/app/bills/" + id, { waitUntil: "domcontentloaded" });
      const panel = page.locator(".statement-upload-panel");
      await panel.waitFor({ state: "visible" });
      await panel.evaluate(node => {
        // Blazor CSS isolation scopes rules to b-* attributes. This synthetic
        // status row must carry the same scope as native Razor-rendered children,
        // or the test measures unstyled injected markup instead of the real UI.
        const scope = [...node.attributes].map(attr => attr.name).find(name => name.startsWith("b-"));
        if (!scope) throw new Error("Expected CSS-isolated statement component");
        const scoped = tag => {
          const element = document.createElement(tag);
          element.setAttribute(scope, "");
          return element;
        };
        const status = scoped("div");
        status.className = "statement-upload-status working";
        const dot = scoped("span");
        dot.className = "statement-status-dot";
        const body = scoped("div");
        const title = scoped("strong");
        title.textContent = "Uploading securely";
        const copy = scoped("span");
        copy.textContent = "Sending the " + "VeryLongUnbrokenStatementFilename".repeat(12)
          + ".pdf statement to FullWorth.";
        body.append(title, copy);
        status.append(dot, body);
        node.insertBefore(status, node.querySelector(".statement-upload-footnote"));
      });
      const metrics = await panel.locator(".statement-upload-status").evaluate(element => {
        const bounds = element.getBoundingClientRect();
        const text = element.querySelector("div > span");
        const content = text.getBoundingClientRect();
        return {
          viewport: innerWidth, bodyScroll: document.documentElement.scrollWidth,
          left: bounds.left, right: bounds.right,
          clientWidth: element.clientWidth, scrollWidth: element.scrollWidth,
          textLeft: content.left, textRight: content.right
        };
      });
      assert.ok(metrics.left >= -1 && metrics.right <= metrics.viewport + 1
          && metrics.scrollWidth <= metrics.clientWidth + 1
          && metrics.textLeft >= -1 && metrics.textRight <= metrics.viewport + 1
          && metrics.bodyScroll <= metrics.viewport + 1,
        "Long statement status messages must reflow without horizontal clipping: "
          + JSON.stringify(metrics));
    }
  } finally {
    await page.unroute(match, stub);
    await page.setViewportSize({ width: 1440, height: 1100 });
  }
}
