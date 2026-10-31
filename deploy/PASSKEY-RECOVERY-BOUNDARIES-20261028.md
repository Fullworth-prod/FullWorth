# Passkey confirmation and localized recovery — 2026-10-28

Base development: `a21c7b67c8c36949208037d306c9621a8bb8e1e7` (documentation #993; runtime #992).

## Demonstrated defects

The shipped management script accepted a JSON 200 from remove/register and could display Saved after an unchanged list. It also prevented password/2FA-verified removal in a secure browser without WebAuthn. The password-verified second-factor script shared the loose success-response behavior. Server endpoints already enforce credentials/ownership and contract 204 for completion/removal/registration; no server bypass or account takeover is alleged.

## Implemented behavior

Both shipped handlers require nonredirected 204 before credential completion or authenticated continuation; options require JSON 200 with a nonempty challenge and 43-character ceremony identifier. Credential requests use same-origin/no-store/redirect:error and validate antiforgery tokens. Unexpected responses and network ambiguity never replay writes or display success. Ambiguous key changes expose an explicit read-only refresh, so previously verified rows cannot be mistaken for current confirmation. Safe fixed EN/ES guidance replaces raw error text. Secure browsers may remove keys using existing server password/2FA proof even without WebAuthn; enrollment and sign-in still require WebAuthn. Secure-context, feature flags, password/2FA/ownership, list-version and replacement-root safeguards and local-only continuation remain.

Five Razor recovery surfaces now use existing EN/ES localization: keys, error, missing page, reconnect and admin navigation. Thirty-five missing App Spanish entries are added without replacing existing translations. Reconnect DOM IDs/counter and error reference/404 status are preserved. Published legal text is unchanged.

## Executed before PR

Actual source executed in memory without a checkout: 66 new response/recovery cases plus two existing return-URL tests (32 local/unsafe URL variants), all 68 passed; changed JS/module syntax passed. The initial 64-case new suite on original scripts failed 45 assertions (19 passed), including false confirmation and blocked secure-browser removal. Those counts are isolated synthetic DOM/HTTP evidence, not a compiled build, real browser/device or provider acceptance.

## Required next validation

This candidate needs its own final-head protected backend/container/dependency checks, compiled localization and eight HTTP-rendered recovery cases, the full existing visual/security/restore suites, 12 new EN/ES 320/390px read-only browser page checks and real disposable-CI virtual-key enrollment/login/removal. Browser removal uses only the existing disposable CI owner with password/authenticator proof; it verifies removal without local WebAuthn and Spanish UI. Passkeys remain default-off in actual installations; only the existing isolated CI compose override enables the fixture.

Physical Android/iPhone, real owner/provider, delivery, real clean-host recovery and qualified legal acceptance remain separately open. No deployment/master promotion, credential/grant/config change, spending or private export is performed. Repeated slow Overview shift and enlarged Settings heading typography remain source follow-ups; this change does not claim their resolution.
