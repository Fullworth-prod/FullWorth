import assert from "node:assert/strict";
import path from "node:path";

// Real components, synthetic intercepted responses; never run against production.
export async function verifyAuditBacklogRecovery(page, outputDir) {
    assert.match(new URL(page.url()).hostname, /\.localhost$/);
    const bill = "a8240000-0000-0000-0000-000000000001";
    const upload = "a8240000-0000-0000-0000-000000000002";
    const accountA = "a8240000-0000-0000-0000-000000000003";
    const accountB = "a8240000-0000-0000-0000-000000000004";
    let posts = 0, statusReads = 0, synced = false;
    const detail = route => route.fulfill({ json: { id: bill, providerName: "Synthetic audit utility",
        category: "Internet", source: "Manual", isActive: true, hasTransactionEvidence: false,
        statements: [], changes: [] } });
    const uploadRoute = route => {
        assert.equal(route.request().method(), "POST");
        assert.ok(route.request().headers()["x-csrf-token"]);
        posts++;
        return route.fulfill({ status: 201, json: { id: upload, billStreamId: bill, status: "Uploaded" } });
    };
    const statusRoute = route => {
        assert.equal(route.request().method(), "GET");
        return ++statusReads === 1 ? route.fulfill({ status: 503 }) :
            route.fulfill({ json: { id: upload, billStreamId: bill, status: "Processed" } });
    };
    const transaction = (id, account, name) => ({ id, bankAccountId: account, institutionName: "Synthetic bank",
        accountName: name, name, amount: 12, isoCurrencyCode: "USD", postedDate: "2026-10-01", isPending: false });
    const transactions = route => {
        assert.equal(new URL(route.request().url()).searchParams.get("take"), "100");
        return route.fulfill({ json: synced ?
            [transaction("a8240000-0000-0000-0000-000000000006", accountB, "Account B")] :
            [transaction("a8240000-0000-0000-0000-000000000005", accountA, "Account A"),
             transaction("a8240000-0000-0000-0000-000000000006", accountB, "Account B")] });
    };
    const sync = route => { synced = true; return route.fulfill({ json: {} }); };
    const adminReads = [];
    let programWrites = 0, grants = 0, grantOperation;
    const admin = route => {
        const method = route.request().method();
        if (method !== "GET") {
            assert.ok(route.request().headers()["x-csrf-token"]);
            const body = route.request().postDataJSON();
            assert.equal(body.currentPassword, "Synthetic!Proof123");
            if (method === "PUT") {
                assert.match(route.request().url(), /\/programs\/BetaTester$/);
                assert.equal(body.isActive, false);
                programWrites++;
                return route.fulfill({ status: 204 });
            }
            assert.equal(method, "POST");
            assert.match(route.request().url(), /\/entitlements$/);
            assert.match(body.operationId, /^[a-f0-9-]{36}$/i);
            grants++;
            if (grants === 1) { grantOperation = body.operationId; return route.abort("failed"); }
            assert.equal(body.operationId, grantOperation, "retry must retain the queued grant identity");
            return route.fulfill({ status: 201, json: { entitlementId: grantOperation } });
        }
        const url = new URL(route.request().url()), skip = Number(url.searchParams.get("skip"));
        if (url.pathname.endsWith("/users") && url.searchParams.get("take") === "1")
            return route.fulfill({ json: [] }); // AppLayout's independent staff-access probe.
        assert.equal(url.searchParams.get("take"), "50");
        adminReads.push([url.pathname, skip]);
        const users = url.pathname.endsWith("/users");
        const rows = Array.from({ length: skip === 0 ? 50 : 1 }, (_, i) => {
            const n = skip + i + 1;
            return { id: "b8240000-0000-0000-0000-" + String(n).padStart(12, "0"),
                email: "synthetic-" + n + "@example.test", roles: [], programs: ["BetaTester"], isActive: true,
                displayPrefix: "SYN" + n, purpose: "Complimentary", tier: "Standard", status: "Active",
                action: "SyntheticAudit" + n, subjectType: "Synthetic",
                createdAtUtc: "2026-10-01T00:00:00Z", items: [] };
        });
        return route.fulfill({ json: users ? rows : { skip, take: 50, totalCount: 51, items: rows } });
    };
    const bindings = [
        ["**/bff/bill-streams/" + bill, detail],
        ["**/bff/bill-streams/" + bill + "/statement-uploads", uploadRoute],
        ["**/bff/bill-streams/" + bill + "/statement-uploads/" + upload, statusRoute],
        ["**/bff/bank-transactions?*", transactions],
        ["**/bff/bill-monitoring/refresh", sync],
        ["**/bff/admin/**", admin]
    ];
    for (const [pattern, handler] of bindings) await page.route(pattern, handler);
    try {
        await page.goto("/app/bills/" + bill, { waitUntil: "domcontentloaded" });
        await page.locator('input[type="file"]').setInputFiles({
            name: "synthetic-audit.pdf", mimeType: "application/pdf", buffer: Buffer.from("%PDF-synthetic-fixture") });
        const send = page.getByRole("button", { name: "Upload statement", exact: true });
        await send.click();
        await page.getByText("Status unavailable", { exact: true }).waitFor();
        assert.equal(posts, 1);
        assert.equal(await send.isDisabled(), true);
        await page.getByRole("button", { name: "Check statement status", exact: true }).click();
        await page.getByText("Statement processed", { exact: true }).waitFor();
        assert.equal(posts, 1, "status recovery never resubmits the accepted file");
        assert.equal(statusReads, 2);
        await page.screenshot({ path: path.join(outputDir, "audit-backlog-statement-recovered.png"), fullPage: true });

        await page.addInitScript(() => {
            let performance;
            Object.defineProperty(window, "FullWorthPerformance", { configurable: true,
                get: () => performance,
                set: value => { performance = { ...value, getTransactionLoadLimit: () => {
                    if (sessionStorage.getItem("auditPerformanceFailure") === "1") throw new Error("synthetic helper failure");
                    return value.getTransactionLoadLimit();
                } }; }
            });
        });
        await page.evaluate(() => sessionStorage.setItem("auditPerformanceFailure", "1"));
        await page.goto("/app/account/transactions", { waitUntil: "domcontentloaded" });
        await page.locator(".transaction-row").first().waitFor();
        await page.getByLabel("Filter by account", { exact: true }).selectOption(accountA);
        await page.waitForFunction(() => document.querySelectorAll(".transaction-row").length === 1 &&
            document.querySelector(".transaction-row")?.textContent.includes("Account A"));
        await page.getByRole("button", { name: "Sync now", exact: true }).click();
        await page.waitForFunction(() => document.querySelector(".transaction-account-filter")?.value === "" &&
            [...document.querySelectorAll(".transaction-row")].some(row => row.textContent.includes("Account B")));
        assert.equal(await page.locator(".transaction-row").count(), 1);
        await page.evaluate(() => sessionStorage.removeItem("auditPerformanceFailure"));

        await page.goto("/app/admin", { waitUntil: "domcontentloaded" });
        const groups = page.getByRole("group", { name: "Page controls", exact: true });
        await groups.first().waitFor();
        assert.equal(await groups.count(), 3);
        const firstUser = page.locator(".admin-user-row").first();
        await firstUser.getByRole("button", { name: "Remove beta tester", exact: true }).click();
        await firstUser.getByLabel("Current password", { exact: true }).fill("Synthetic!Proof123");
        await Promise.all([page.waitForEvent("domcontentloaded"),
            firstUser.getByRole("button", { name: "Confirm", exact: true }).click()]);
        await groups.first().waitFor();
        await firstUser.getByRole("button", { name: "Grant 30 days", exact: true }).click();
        await firstUser.getByLabel("Current password", { exact: true }).fill("Synthetic!Proof123");
        await firstUser.getByRole("button", { name: "Confirm", exact: true }).click();
        await firstUser.getByText("The grant could not be confirmed. Retry this confirmation with fresh credentials to recover the same grant, or cancel and review the account.", { exact: true }).waitFor();
        assert.equal(await firstUser.getByLabel("Current password", { exact: true }).inputValue(), "");
        await firstUser.getByLabel("Current password", { exact: true }).fill("Synthetic!Proof123");
        await Promise.all([page.waitForEvent("domcontentloaded"),
            firstUser.getByRole("button", { name: "Confirm", exact: true }).click()]);
        await groups.first().waitFor();
        assert.equal(programWrites, 1);
        assert.equal(grants, 2);
        for (let i = 0; i < 3; i++) {
            await groups.nth(i).getByRole("button", { name: "Next page", exact: true }).click();
            await groups.nth(i).getByText("Records 51–51", { exact: true }).waitFor();
            assert.equal(await groups.nth(i).getByRole("button", { name: "Next page", exact: true }).isDisabled(), true);
            await groups.nth(i).getByRole("button", { name: "Previous page", exact: true }).click();
            await groups.nth(i).getByText("Records 1–50", { exact: true }).waitFor();
        }
        assert.equal(adminReads.filter(([, skip]) => skip === 50).length, 3);
        console.log("Audit backlog recovery passed: accepted statement read recovery with one POST, optional performance fallback, vanished account filter, and required program-removal proof, stable grant retries, and all three bounded admin pagers.");
    } catch (error) {
        await page.screenshot({ path: path.join(outputDir, "audit-backlog-recovery-failure.png"), fullPage: true });
        throw error;
    } finally {
        await page.evaluate(() => sessionStorage.removeItem("auditPerformanceFailure")).catch(() => {});
        for (const [pattern, handler] of bindings) await page.unroute(pattern, handler);
        await page.goto("/app", { waitUntil: "domcontentloaded" });
    }
}
