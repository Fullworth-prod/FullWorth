# FullWorth personal iPhone testing

This is an owner-controlled development experiment, not a production release.
The MAUI native client is separate from the Web/PWA client: the Web redesign and
pending Web passkeys do not automatically become native features.

## Build without persistent Apple credentials

The FullWorth iOS Personal Device IPA workflow compiles net10.0-ios / ios-arm64
on GitHub's macos-15 runner with the same Xcode 26.0.1 baseline as Simulator CI.
NuGet vulnerability warnings remain errors. It packages Payload/FullWorth.app,
checks the bundle's iPhoneOS metadata and main executable's Mach-O CPU/platform,
and retains the unsigned IPA, SHA-256 checksum and source SHA for seven days.

The temporary org.fullworth.personal ID is for personal testing only. It does
not register or choose the final App Store identifier. Keep the signing tool's
chosen bundle ID stable across reinstalls to avoid losing app-local state.

An unsigned IPA cannot be installed directly. This workflow uses no Apple
credentials, no signing service, no provisioning profile and no upload to Apple.
A successful build is not evidence of installation, launch, signing validity,
provider compatibility, or production security. The validator checks the main
executable, not all embedded libraries or every aspect of Apple's package rules.

## Windows owner-controlled signing

Sideloadly documents Windows support and signing with a free Apple account:
https://sideloadly.io/ and https://sideloadly.io/faq

It is a third-party tool, not Apple's TestFlight. Review its credential handling
before using it. The owner enters credentials/2FA directly into the trusted local
tool, never chat, GitHub, build logs, or scripts. Do not silently save credentials.
Do not use remote signing sites, purchased/shared enterprise certificates,
jailbreaks, security bypasses, or alternate accounts to evade a restriction.

Before installation, verify the IPA checksum and source SHA, inspect the official
installer's publisher and device-driver prerequisites, and connect the owner's
unlocked iPhone using USB. Do not uninstall existing iTunes/iCloud automatically:
provider instructions may require different versions; explain any such change.

Phone-side Trust, developer-profile authorization and Developer Mode (if required)
are explicit owner actions, not automation. Explain the risk before asking the
owner to enable them. Never disable Windows endpoint protection to install a tool.

Free-account signing expires after seven days. A rejected paid-enrollment identity
check is not proof that personal signing succeeds or fails. Attempt it only through
the ordinary authorized Apple flow. If Apple refuses, stop at that exact error and
continue the existing support case; do not claim a workaround has succeeded.

Apple's personal-team limits:
https://developer.apple.com/help/account/basics/about-your-developer-account

## Physical acceptance

After signing/installing, open FullWorth on the physical iPhone and record the
source SHA, iOS version, installation/launch result and sanitized errors. Test
login/session persistence, logout, API access, bank connection handoff, and account
deletion using controlled test accounts/data. Test MFA-enabled accounts separately.
Do not use a financial production account as a disposable deletion test.

Existing Simulator success does not close these device gates. This personal build
does not bypass Apple Developer enrollment or App Review and is not an App Store
submission. Keep the existing production application, data and security unchanged.
