import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

export async function verifyNativeSelectLayout(browser, engine) {
  const fixtures = [
    {
      name: "bills",
      css: await readFile(new URL("../FullWorth.Web/Components/Pages/App/Bills.razor.css", import.meta.url), "utf8"),
      html: '<section class="bill-workspace-tools"><div><label for="choice">Sort active bills</label><select id="choice"><option value="attention">Changes and evidence gaps first</option><option value="name">Provider name</option><option value="latest">Latest charge within currency</option></select></div></section>'
    },
    {
      name: "planning",
      css: await readFile(new URL("../FullWorth.Web/Components/Pages/App/Planning.razor.css", import.meta.url), "utf8"),
      html: '<section class="planning-card"><fieldset class="planning-bill-row"><div class="planning-bill-copy"><strong>Synthetic utility</strong></div><label for="choice">Sueldos de anticipación<select id="choice"><option value="">Usar predeterminado</option><option value="1">1</option><option value="2">2</option></select></label></fieldset></section>'
    }
  ];
  const context = await browser.newContext({ viewport: { width: 320, height: 900 } });
  let passed = 0;
  try {
    const page = await context.newPage();
    for (const fixture of fixtures)
    for (const fontFamily of ["serif", "sans-serif", "monospace"]) {
      await page.setContent('<style>body{margin:0;font-family:' + fontFamily + '}.app-page{width:calc(100% - 24px);margin:auto}' +
        fixture.css + '</style><main class="app-page">' + fixture.html + '</main>');
      const select = page.locator("#choice");
      const options = await select.locator("option").allTextContents();
      await page.evaluate(() => {
        window.syntheticChanges = 0;
        document.querySelector("select").addEventListener("change", () => window.syntheticChanges++);
      });
      await page.keyboard.press("Tab");
      assert.equal(await select.evaluate(element => element === document.activeElement), true,
        engine + " native control must remain keyboard-focusable");
      await select.selectOption({ index: 1 });
      assert.equal(await select.evaluate(element => element.selectedIndex), 1);
      assert.equal(await page.evaluate(() => window.syntheticChanges), 1);
      await select.selectOption({ index: 0 });
      for (const scale of [1, 2, 1]) {
        await page.evaluate(scale => document.documentElement.style.fontSize = scale * 100 + "%", scale);
        for (const width of [320, 375, 390, 430, 768, 1024, 1440]) {
          await page.setViewportSize({ width, height: 900 });
          await page.evaluate(() => new Promise(resolve =>
            requestAnimationFrame(() => requestAnimationFrame(resolve))));
          const result = await select.evaluate(element => {
            const box = element.getBoundingClientRect();
            return { viewport: innerWidth, body: document.body.scrollWidth,
              document: document.documentElement.scrollWidth, left: box.left, right: box.right,
              font: parseFloat(getComputedStyle(document.documentElement).fontSize),
              appearance: getComputedStyle(element).appearance };
          });
          assert.ok(result.document <= width + 1 && result.body <= width + 1 &&
            result.left >= 0 && result.right <= width + 1,
            engine + "/" + fixture.name + "/" + fontFamily + "/" + scale + ": " + JSON.stringify(result));
          assert.ok(result.font >= 16 * scale, "Do not shrink enlarged text");
          assert.notEqual(result.appearance, "none", "Keep native dropdown affordances");
          assert.deepEqual(await select.locator("option").allTextContents(), options,
            "Full native option labels must survive layout correction");
          passed++;
        }
      }
    }
  } finally { await context.close(); }
  console.log("Native select layout passed: " + passed + " cases; engine: " + engine);
}
