# PWA laboratory baseline — 2026-10-27 UTC

These are actual bounded CI observations on source `09b3fcfd1c10d226e3e16f2a830273e789761760`, tested merge `510dc8efd180b3430627c51ca48378c71be57e31`, integrated through [#990](https://github.com/Fullworth-prod/FullWorth/pull/990) as `59185a8114486662a5f8fab3154f1a9a0225359a`. The source/tested/integrated tree is `e68c70a734e490d4988baa6fd05672ed7fbb99fa`.

Main CI [40572266683](https://github.com/Fullworth-prod/FullWorth/actions/runs/40572266683) passed all applicable checks; backend executed 2,915 .NET and 159 Node tests with zero failures/skips/backend or offline build warnings/errors. The required Android job used its unchanged-native-file condition; it is not a fresh native build. Full browser/HTTP/passkey/disposable encrypted restore checks passed. Synthetic recovery does not establish real current-release recovery.

Artifact `12765923357`, SHA256 `12ecbb25a0f4398347b975afb8c865e4117469be5800ee488e54c19e6eec214e`, was downloaded, checksum-verified and inspected. Its aggregate JSON completed all 21 samples and contained no forbidden payload/credential/storage fields, raw URLs or UUIDs. The GitHub artifact has finite retention; the table below preserves the measured baseline.

## Conditions and interpretation

- Chromium 140.0.7339.186 and WebKit 26.0; already-warm shared server; disposable synthetic account with no populated private histories.
- Balanced profile, Editorial/dark, en-US, root font 16px, OS reduced-motion media preference false. Ordinary viewport 1440×1000; slow sample 390×844.
- Worker-blocked HTTP-cache controls are separate from worker-allowed startup samples. Allowed samples actually had a controller in both engines. No Playwright request routing was used by the probe.
- Slow Chromium sample used 150ms emulated latency, 187,500 download/75,000 upload bytes per second; no CPU or physical-device emulation is claimed.
- Heading observation includes automation overhead and does not mean financial-data readiness. Layout/LCP observations are bounded windows, not final field Core Web Vitals, percentiles or a performance SLA.
- Byte columns are browser Resource Timing reports for same-origin static CSS/JS. They are not socket captures; worker policy and revalidation affect interpretation. Warm body-size metadata is not a new measure of the total rendered stylesheet footprint.
- Bill Detail is explicitly the visible unavailable state for a fixed nonexistent fixture. It is not a successful financial read or populated-detail acceptance. Unsupported/dropped metrics remain unknown.

## Actual samples

| Engine | Case | Worker policy / controlled | Heading ms | CSS reported transfer bytes | JS reported transfer bytes | Bounded shift score |
|---|---|---|---:|---:|---:|---:|
| chromium | overview-cold | block / no | 153.1 | 73319 | 76030 | 0 |
| chromium | overview-warm | block / no | 103 | 6600 | 1500 | 0 |
| chromium | bills | block / no | 56.9 | 0 | 0 | 0 |
| chromium | activity | block / no | 60.8 | 0 | 0 | 0 |
| chromium | account | block / no | 60.9 | 0 | 0 | 0 |
| chromium | transactions | block / no | 49.7 | 1833 | 2410 | 0 |
| chromium | settings | block / no | 60.5 | 4593 | 3808 | 0 |
| chromium | bill-detail-unavailable | block / no | 178.2 | 12426 | 1500 | 0 |
| chromium | overview-cold | allow / yes | 157.1 | 79013 | 76030 | 0 |
| chromium | overview-warm | allow / yes | 105 | 79013 | 58581 | 0 |
| webkit | overview-cold | block / no | 419 | 67127 | 75349 | unknown |
| webkit | overview-warm | block / no | 80 | 6000 | 1500 | unknown |
| webkit | bills | block / no | 117 | 0 | 0 | unknown |
| webkit | activity | block / no | 52 | 0 | 0 | unknown |
| webkit | account | block / no | 202 | 0 | 0 | unknown |
| webkit | transactions | block / no | 74 | 1842 | 2419 | unknown |
| webkit | settings | block / no | 177 | 4611 | 3817 | unknown |
| webkit | bill-detail-unavailable | block / no | 245 | 6000 | 1500 | unknown |
| webkit | overview-cold | allow / yes | 419 | 72839 | 76266 | unknown |
| webkit | overview-warm | allow / yes | 141 | 72839 | 57801 | unknown |
| chromium | overview-cold (slow) | block / no | 1199.6 | 73319 | 76030 | 0.216613 |

## Observed asset and call bounds

Cold Chromium recorded 24 CSS entries (73,319 reported transfer / 66,119 encoded body / 422,170 decoded body bytes) and 18 JS entries (76,030 / 70,630 / 306,328 bytes). Observed DOM counts were 21 stylesheet links and 7 external script elements; Transactions/Settings had 22 links. Every sample had zero duplicate stylesheet hrefs. These are runtime observations, distinct from the authored 20-link/6-script source inventory; Resource Timing entries can also include preload/prefetch work.

Enhanced Bills/Activity/Account samples added zero reported CSS/JS entries. Chromium Transactions added one CSS entry (1,833 reported transfer bytes) and one JS entry (2,410); Settings added one CSS (4,593) and one JS (3,808). Their WebKit counterparts are retained in the original report and table. This does not prove zero module/interop work: only four public profile APIs were instrumented. Transactions recorded one getTransactionLoadLimit and two getExecutionMode calls per engine; Settings recorded one getPreference. Those counts include internal public-method calls and are not a count of all .NET-to-JS invocations.

## Findings still requiring work

The slow 390px Chromium sample recorded bounded layout shift 0.216613, FCP 964ms, latest observed LCP 2,596ms, heading observation 1,199.6ms and observation end 3,460.8ms. No cause or performance acceptance is established. Preserve this finding, repeat under the same controlled conditions when a justified source change runs CI, and capture fixed public page-region labels to locate moving content without exporting DOM/account data. Do not hide the score, lower a gate or invent an SLA.

Physical mid-range Android installed launch (#251), iPhone installed/session/keyboard/safe-area/update behavior (#258), actual served-release timing, reconnect behavior and complete per-route interop/populated-history measurements remain distinct. The same-release provider/security/recovery/legal gates remain in the milestone audit. Neither this baseline nor passing CI closes those acceptance issues.

## Follow-up observation — #992 / 2026-10-28 UTC

Source `de3ff1ef8e2b2b611605e984c2ddd631328e5745` passed all5 checks and2,928 .NET/161 Node (0failed/skipped/backend/offline warnings/errors), then merged as `01cd2e6f361253e0f95d1a0283c023278fba1f45`. Its tested merge `2cdb1c529a41bc9b20c38ef320174d85931f7cff` and integrated tree equal `6ad1429b953dbaa0ef3f7bbd3d7922aeedd7e9bf`. [CI40610180981](https://github.com/Fullworth-prod/FullWorth/actions/runs/40610180981) completed another21 samples.

Artifact12783207418 SHA256`426a9c1f737acaa45778b927db5eba3809ded962bc42b87d76d83853ee228b3c` was downloaded/checksum-verified and privacy-inspected. The slow sample again recorded0.216613, with one event at2,005.1ms and only fixed app-content/overview-loading aliases. Heading1,175.9ms, FCP948ms, latest LCP2,552ms and observation end3,445.1ms are bounded laboratory observations, not a statistical improvement or readiness threshold.

The triage retains at most10 individual events and5 public region aliases/event; unknown regions remain unclassified and unsupported/dropped metrics remain unknown. It never exports DOM text/classes/IDs or financial data. This establishes repeatability and an investigation region; it does not establish a cause or fix. Track recovery/error/passkey copy and enlarged-heading typography as separate source follow-up. Published legal copy, real provider/device/recovery/delivery acceptance and deployment remain outside this evidence.
