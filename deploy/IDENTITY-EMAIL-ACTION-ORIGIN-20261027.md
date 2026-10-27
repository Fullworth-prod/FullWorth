# Identity email action origin — 2026-10-27

The direct password-reset link overload accepted any absolute HTTPS action URL without embedded credentials. Confirmation and reset-code flows already rebuilt links from the configured public Web URL. Code search found no repository caller of the direct overload, so this is a demonstrated sender-contract defect, not a demonstrated production Host-header exploit.

Actual baseline e2432d0e1b2c7b37c78a33adb4e12d59c3f56604: CI40463754079/backend122054161469 executed2,881 .NET tests, with four intended failures,2,877 passed, zero skipped and zero build warnings/errors. Foreign, suffix-confusable, subdomain and changed-port HTTPS origins were sent to the recording HTTP handler instead of being rejected.

After existing secure-URL/configuration checks, require the action's normalized IDN host and effective port to match the configured HTTPS public origin. Reject before constructing/sending the provider request, with a generic error that never includes recovery URLs/tokens. Equivalent host casing/default443, a configured nondefault port/base path, generated code links and disabled no-op remain covered.

All sender tests use an in-memory recording HttpMessageHandler and disposable fake key; no real email, account setting, credential or provider call is authorized or made. Corrected final-head CI is pending. External message receipt/account recovery acceptance remains separate.
