# Browser request reliability — 2026-10-25 UTC

Status: implemented candidate; protected CI and WebKit verification pending.
Base development: `67cff66012ed79ca1b7b7541664d7763fc876904`.
Development integration and deployment are separate; this record proves neither.

## Reproduced failure and customer consequence

The existing browser BFF response handler returns `null` for a successful HTTP response whose content type is HTML or other non-JSON. An isolated execution of the actual development module reproduced `disconnectBankConnection` resolving successfully with `null` after a synthetic HTML login/proxy response. Account's existing caller then marks the connection disconnected, removes its accounts and displays a success message. A successful HTTP status alone is insufficient evidence of an accepted action.

The existing subscription client also permits malformed security-token values to reach a write and caches a token through request-validation/authorization rejection. Both browser clients previously allowed Fetch's automatic redirect following. No observed production incident, provider outage, redirected private-data disclosure or live payment problem is asserted.

## Implementation

- General BFF reads require a usable JSON response. HTML/text, JSON MIME-prefix impostors, malformed JSON and redirected responses throw a generic failure instead of becoming a successful empty result. JSON MIME matching is case insensitive and accepts charset parameters. Raw invalid JSON is not exposed in parser errors.
- Existing intentional no-content writes remain supported. The API's latest-statement-upload discovery intentionally returns 204 when no attempt exists; only that read opts into no-content handling. Pay-schedule 404 still means no schedule. Statement status, bank/alert reads and actual uploads do not accept unexplained empty success responses.
- All seven Fetch paths in the general BFF module and all five in the subscription module use `redirect: "error"`. A separate response-level guard rejects redirected responses supplied by a mock or alternate implementation. This does not change the intentional validated Checkout/Portal navigation after a JSON response.
- Both clients require a nonblank string security token before sending a write. They discard cached tokens on HTTP 400/401/403 so a later explicit user attempt can obtain a new token. There is no automatic request replay; network uncertainty and rejection remain errors.
- Preserve login return paths, credentials mode, no-store caching, CSRF headers, Link cancellation and throttle metadata. Existing server ownership/authentication/subscription exemptions, financial values and all production flags are unchanged.
- Add durable Node and real-browser regression suites. Extend the existing confirmation workflow with the BFF browser suite, preserving the existing job name and dialog checks. Default browser scope is Chromium plus WebKit.

## Executed evidence

Verification uses the connected GitHub source without a local checkout. A disposable Node v24.22.1 process received source strings through standard input; imported file locations were remapped to in-memory modules. The passkey fixtures' existing source reads were supplied from the same inspected source strings, and the mocked transaction worker's module URL was remapped. Assertions and runtime behavior were preserved. This is isolated source execution, not GitHub CI or a built .NET application.

- The initial 65-case new regression suite failed 58 cases against unchanged source (7 compatibility cases passed), and passed all 65 with the first correction. Additional API-contract cases cover intentional latest-upload 204 and JSON mutation results.
- Final new Node suite: **67 passed**, zero failed/skipped.
- Existing Node suites: login redirect **3**, passkey continuation **2**, Link completion **9**, transaction streaming/filtering including imported regressions **23**. Total existing: **37 passed**, zero failed/skipped.
- Combined Node execution: **104 passed**, zero failed/skipped. The 2 passkey cases each exercise 16 local/unsafe return destinations; this detail is not added to the test-run count.
- Installed headless Chromium executed the actual modules against a disposable loopback HTTP fixture: **37 browser scenarios passed**. Includes all five common redirect status codes for both clients, malformed responses/tokens, explicit 400/403 retries with token replacement, valid token reuse, real multipart upload, and intentional empty-state compatibility. Redirect-target request count remained zero in every redirect case.
- WebKit is not installed in this environment and was not downloaded. **No local WebKit result is claimed.** The committed browser suite defaults to both engines and the existing CI workflow installs the repository-pinned Playwright dependencies and engines.
- No external network/provider/payment requests, credentials, customer data, installed-device acceptance or production changes were involved.

## Scope and remaining verification

Six related files: two existing browser modules, one Node regression file, one browser regression file, the existing browser workflow and this handoff. No file overlap with open #941–#945, #948, #950, #952, #953 or #955 at inspection. Preserve their candidates and source branches.

All required final-head checks remain mandatory, including backend/.NET/Node, Android, production container/browser and dependency review. The existing Actions billing lock prevents jobs from starting; do not infer a protected pass from isolated Node or Chromium results, repeatedly rerun unchanged jobs or merge around protection.

Next: after the account owner restores Actions, run the final-head protected checks and existing confirmation/BFF browser job, inspect actual results/counts and execute both browser engines. Resolve any genuine failures before development integration. Keep #953's three PostgreSQL suites and #955's separate confirmation suite distinct; this change does not validate their combined heads.

No master promotion/deployment, paid activation, account-setting/secret changes, private export, outreach, payouts or destructive operations. #912 provider/unknown-commit recovery, #762/#293 private bill/provider/payroll evidence, #251/#258 physical devices and #291/#669 real immutable clean-host recovery/two-owner/document/security/legal acceptance remain open.
