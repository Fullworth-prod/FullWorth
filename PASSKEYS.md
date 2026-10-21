# Passkeys and FIDO2 security keys

This implementation supports discoverable device passkeys (including iPhone Face ID)
and discoverable-credential/PIN/biometric-capable FIDO2 roaming keys. FullWorth never stores biometric data
or private keys. User verification is required; legacy touch-only U2F keys are not
advertised as supported.

## Activation (not a deployment instruction)

The feature is OFF by default. Apply the reviewed AddPasskeys migration through the
normal release process before enabling `Passkeys:Enabled=true` in both API and Web.
Configure API `Passkeys:Origin` to the exact public HTTPS origin, normally
`https://fullworth.org`. Origin is not inferred from client-supplied forwarded
headers. Changing the origin/RP ID invalidates access to existing keys on the old
domain. No other hosts or embedded cross-origin frames are accepted.

Settings → Passkeys & security keys enrolls/removes keys after current password
and, when enabled, an authenticator code. When the feature is enabled, the normal
Web sign-in page also offers **Sign in with Face ID or a passkey** for a previously
enrolled key, with password fallback and an optional authenticator-code field.
That field is **required** if the account has authenticator MFA enabled; a passkey
does not silently bypass an enrolled second factor. The sign-in page's explicit
"Keep me signed in" choice applies to both methods (30-day persistent cookie
when selected versus a 12-hour nonpersistent Web session). Neither option
automatically renews an expired session or shares Safari cookies with the MAUI
WebView. Standalone API passkey sign-in still preserves the existing authenticator MFA
requirement. The website now verifies the password once, then offers authenticator,
recovery code, or (only when enabled and already enrolled) a user-verified passkey
for that same account. The passkey must match a fresh challenge bound to the
single-use pending password proof. This does not disable the account's 2FA flag
or weaken sensitive account-change reauthentication requirements. Password and recovery
remain available. Removing a key prevents new assertions but does not revoke
already issued sessions; use Revoke all sessions separately when needed.
External-only accounts need a supported recent-authentication mechanism before
enrollment; this version requires an existing local password.

## Boundaries and acceptance

WebAuthn cryptography uses .NET 10 Identity's IPasskeyHandler, not custom parsing or
signature verification. Registration state remains server-side and is bound to the
authenticated account and security stamp. At most 2048 pending ceremonies exist per
API process; they expire after three minutes and are atomically consumed on every
completion attempt, including failures. Capacity returns 503 rather than evicting
active ceremonies. Registration is capped at 10 keys per account. PostgreSQL transaction advisory locking
serializes credential writes, including assertion verification and signature-counter
updates, removal, and registration ownership/quota checks before Identity's upsert.
This prevents a concurrent assertion from re-adding a just-removed credential.

The challenge cache intentionally supports one API process. Restart, routing to a
different replica, or process loss fails closed and requires restarting the ceremony.
Do not deploy multiple API replicas without an atomic shared consume store.
A process-local cache is not an isolation or sandbox guarantee.

For the installed iPhone app's WKWebView, Apple requires a signed associated-domains
entitlement with `webcredentials:fullworth.org`, a live matching
`/.well-known/apple-app-site-association` file listing the actual signed Apple
Team ID and final app bundle ID, and physical-device Face ID acceptance. The
existing unsigned personal IPA and temporary bundle ID do not provide that
trust relationship. Do not invent an Apple team identifier, publish a fabricated
association, or turn on production passkeys before these are verified. Apple:
https://developer.apple.com/documentation/authenticationservices/supporting-passkeys

Tests generate real ES256 registration and assertion responses, and exercise replay,
origin, verification, authentication, password proof, challenge expiration, ownership
and concurrent consumption. Physical iPhone Safari/installed-PWA enrollment,
Face ID sign-in, cancellation, and an actual FIDO2 USB/NFC key still require hardware
acceptance before production enablement. CI cannot establish those physical results.

Sources:
- https://learn.microsoft.com/aspnet/core/security/authentication/passkeys?view=aspnetcore-10.0
- https://developer.apple.com/passkeys/

## Password-first Web continuation

The browser receives only a purpose-separated Data Protection encrypted HttpOnly,
Secure, SameSite=Strict, host-only pending cookie. No password or session bearer/
refresh token is retained in it. The API keeps password proof for at most three
minutes, with three verification attempts, atomic per-attempt consumption and
non-renewing expiry. Failed attempts rotate proof IDs; expired, replayed, locked,
inactive, stamp-changed or MFA-changed accounts fail closed. No normal session is
issued until verification succeeds and refresh-family enrollment completes.

A passkey remains a device-/security-key-mediated proof; Face ID is not collected
by FullWorth and does not replace WebAuthn signature/origin/user-verification checks.
Production feature enablement and actual iPhone/security-key acceptance remain
separate gates. Existing production settings are not changed by this implementation.

## Isolated browser regression

`FullWorth.VisualTests/passkey-acceptance.mjs` runs only in GitHub Actions against
`https://web.localhost`. CI temporarily applies the explicitly named
`FullWorth.VisualTests/compose.passkeys-ci.yml` override to its disposable stack,
then restores default feature flags even if the test fails. Production Compose
and production passkey defaults are unchanged.

Chromium's virtual user-verified resident authenticator exercises real browser
enrollment and signed assertion through WebAuthn, the Web BFF and PostgreSQL API.
The test verifies passwordless first-step sign-in with a registered passkey
(including cancellation/password fallback and persistent-session opt-in),
password-once continuation, simulated OS-prompt cancellation with
authenticator fallback, and clearing pending sign-in state after success.
It captures the mobile verification-choice screen before entering any code.
This is not physical Face ID, iPhone WebView/Safari or USB/NFC key acceptance.
