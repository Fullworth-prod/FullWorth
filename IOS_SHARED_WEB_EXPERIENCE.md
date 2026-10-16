# iPhone uses FullWorth.Web

## Architecture
The iOS app's default entry point is HostedWebPage, a MAUI WebView at
https://fullworth.org/app. It loads the currently deployed FullWorth.Web server
application: the same Razor pages, CSS, server-side sessions, anti-forgery forms,
MFA flow, logout and account controls as the website. No second copy of the UI
or native login form is used in hosted mode. Website changes become visible
after their normal reviewed web release; this does not deploy development.

This is hosted reuse, not an offline bundle or a refactoring of Razor components
into a shared native library. It requires the website, API and server connection
to be available. The website's existing BFF continues to hold API tokens
server-side. The shell neither injects bearer tokens nor reads/export cookies,
passwords or codes; old native secure-storage tokens are not imported.

FullWorthUseHostedWebShell defaults to true for iOS (including iPad), false on
other targets. Set -p:FullWorthUseHostedWebShell=false to build the retained native
fallback. Personal iPhone packaging explicitly selects hosted mode.
No database migration, secret change or production deployment is needed.

## Boundaries
- Only HTTPS fullworth.org on port 443, without userinfo, stays in the WebView.
- Other HTTPS DNS origins are cancelled and offered through an explicit
  Open link in browser action. They never silently replace the in-app origin.
- Non-HTTPS schemes, IP literals, nonstandard ports and credential-bearing URLs
  are blocked. Host lookalikes and subdomains are not treated as fullworth.org.
- This is a top-level navigation policy, not subresource isolation, parser
  sandboxing or a substitute for the website's CSP and server authorization.
- No JS/native message bridge, client-side secret storage, TLS bypass, ATS
  exception or custom user-agent impersonation is introduced.
- Loading failures and process termination provide a retry. Retry starts a GET
  at /app; it never intentionally replays a form submission.
- Safe-area handling and no native Shell tab bar avoid two navigation systems.
- This does not promise Safari's existing login is shared with WKWebView.
  You may need to sign in once inside the app.

## Acceptance gates before general distribution
- Physical iPhone: website visual parity and bottom navigation, keyboard,
  rotation, large text, VoiceOver, email/password and MFA sign-in, reload,
  relaunch session persistence, logout and session expiry.
- Verify web uploads/downloads and file picker in the actual WebView.
- External-provider OAuth must have an explicitly designed, tested system-browser
  callback/handoff. Opening a provider in Safari alone does not transfer a session
  back into this WebView. This change does not implement that handoff.
- Plaid/Stripe popups, new windows and bank-app redirects also need end-to-end
  device acceptance and any required native routing; do not claim they work from
  a successful compilation. The generic shell does not add popup handling.
- Face ID/passkeys/security keys are NOT enabled by this wrapper. Associated
  domains, signing entitlements, server association and actual device tests
  remain separate work. Draft web passkey PR #754 is not merged/deployed here.
- There is no new offline cache, push-notification support or native billing.
- App Store Review 4.2 requires utility beyond a repackaged website; a buildable
  wrapper is not proof of App Store eligibility. Do not submit as release-ready.

## Verification scope
Core navigation regression tests validate origin classification. Existing
server tests continue to cover website authentication and authorization.
CI builds the iOS device and simulator artifacts. Simulator launch alone does
not establish website rendering or authenticated device acceptance.

References:
- https://learn.microsoft.com/dotnet/maui/user-interface/controls/webview?view=net-maui-10.0
- https://learn.microsoft.com/dotnet/maui/ios/platform-specifics/page-safe-area-layout?view=net-maui-10.0
- https://developer.apple.com/app-store/review/guidelines/#minimum-functionality
