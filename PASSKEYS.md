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
and, when enabled, an authenticator code. Sign-in uses the platform WebAuthn picker,
and preserves the existing authenticator MFA requirement. Password and recovery
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
serializes registration ownership and quota checks before Identity's upsert.

The challenge cache intentionally supports one API process. Restart, routing to a
different replica, or process loss fails closed and requires restarting the ceremony.
Do not deploy multiple API replicas without an atomic shared consume store.
A process-local cache is not an isolation or sandbox guarantee.

Tests generate real ES256 registration and assertion responses, and exercise replay,
origin, verification, authentication, password proof, challenge expiration, ownership
and concurrent consumption. Physical iPhone Safari/installed-PWA enrollment,
Face ID sign-in, cancellation, and an actual FIDO2 USB/NFC key still require hardware
acceptance before production enablement. CI cannot establish those physical results.

Sources:
- https://learn.microsoft.com/aspnet/core/security/authentication/passkeys?view=aspnetcore-10.0
- https://developer.apple.com/passkeys/
