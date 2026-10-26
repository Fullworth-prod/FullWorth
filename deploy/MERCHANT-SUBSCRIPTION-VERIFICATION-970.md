# Merchant subscription verification — #970

## Scope and evidence boundary

A single posted payment can start a private subscription review. It does not confirm recurrence,
create a bill, change monitoring choice, alter a ledger row or enter a financial total/forecast.
A review identifies the exact merchant, account/plan label and billing party; names never cause
automatic merging. Users may create separate reviews for multiple accounts, plans and resellers.
Creating a review from a bill references that exact owned bill. Creating from Transactions
references that exact owned posted positive nonremoved debit. Gift-card purchases do not prove redemption.

Service access, renewal intent and billing arrangement are independent:
- Active + renewal No can mean cancelled with access remaining.
- Prepaid + renewal Yes can mean automatic billing resumes after prepaid time.
- OneOff + renewal Yes or Ended + renewal Yes is inconsistent and needs review.
- No next charge is invented; passed next-charge dates become stale rather than rolling forward.
- No forecast/money arithmetic or currency conversion is introduced.

Facts are ServiceStatus, Renewal, BillingArrangement, PaidThrough, NextChargeDate,
NextChargeAmount and CurrencyCode. Amount and currency must be supplied together with identical
evidence times. Missing values are Unknown. These are evidence assertions, not guaranteed future charges.
Labels and amounts are owner declarations; their presence never makes a merchant verification claim.

## Data model, concurrency and privacy

SubscriptionReviews has a revision concurrency token, an owner, private labels and optional
composite-owner bill/payment references. SubscriptionEvidence is append-only, keyed by a
caller-generated submission ID, with composite-owner review and document references.
Each normalized field includes observed and review-by times; server recorded time and source
are retained. At most seven supported fields per submission; 31-day maximum evidence validity.
Old documents may be recorded but are already stale. A successful read does not refresh evidence age.

Latest observation per field/source supersedes that same source. A later upload of an older
document cannot override a newer observation. Fresh sources that disagree produce Conflict,
with no resolved value. Expired sources yield Stale, retaining history. Explicit Unknown
supersedes only the same source. Review-level warnings retain contradictory access/renewal/billing
claims instead of silently selecting a winner.

API sources accepted from users: UserConfirmed and DocumentConfirmed only. The latter requires
an existing upload belonging to the exact owned linked bill. It means the user reviewed a document,
not that the parser or merchant verified renewal. No raw document text, storage path or arbitrary
provider URL enters this feature. Source document download uses the existing authenticated route.

A submission requires the current revision; revision update and new evidence are saved atomically.
Identical explicit retry of the same submission ID is idempotent; changed content conflicts.
Stale writers fail without losing the winning evidence. Failed tracked candidates are detached
and the attempt's revision restored. No automatic write replay in the browser.
A failed/ambiguous save blocks new evidence writes until explicit readback. Create retries reuse
the same client review ID until the result is confirmed.

Existing password/MFA-gated account export includes owner-only reviews/evidence.
Existing account erasure removes those rows before restrictive document/bank/bill dependencies.
No real export or account deletion is performed by development acceptance.
Module ownership scanners register both new entity types and DbSets as Bills-owned; cross-module
payment/document checks use explicit Plaid/Statements contracts.

## Experience

Bills links to Subscription verification. Posted outflows offer Verify subscription.
Bill details link with their exact bill ID and retain the existing document upload flow.
The review page shows evidence state, independent access/renewal/term fields, exact billing party,
source, observed/review-by dates and ten recent entries per detail with an explicit history count.
Document selection uses existing processed-statement source uploads for the linked bill.
Other receipts/documents not supported by the statement pipeline must not be advertised as automatic
subscription extraction. Users can still record clearly labeled personal confirmation.
No mailbox access, password collection, session scraping, merchant outreach or subscription actions.

English/Spanish labels, native controls, keyboard use, explicit busy/pressed states and responsive
320/390px enlarged-text checks preserve the existing visual direction. New Overview concepts remain unselected.
No animations or stylistic redesign are claimed.

## Official capability matrix — researched 2026-10-25

| Source | What documented access can establish | Limits for this consumer feature | Current adapter |
| --- | --- | --- | --- |
| Spotify current-user profile | Subscription tier via product, with applicable profile authorization | Schema does not expose next charge, auto-renew intent or prepaid end date. Premium alone is insufficient. | Unavailable |
| Apple App Store Server API | Status/renewal data for subscriptions in the developer's app | Not arbitrary subscriptions in another merchant's app; requires the authorized app's server credentials/transaction evidence. | Unavailable |
| Google Play subscriptionsv2 | State, expiry and auto-renewing/prepaid plan information for a package/purchase token | Publisher authorization and exact app purchase token required; Google identity login is not general subscription access. | Unavailable |
| PayPal Subscriptions | Merchant subscription details for an authorized merchant subscription ID | Client-credentials merchant API is not a consumer-wide list of other merchants' agreements; required scope also covers management. | Unavailable |
| Existing FullWorth bank feed | Posted payment evidence and recurring-history signals | Does not establish merchant entitlement, cancellation, gift redemption or future renewal. | Existing bank evidence only |
| Existing owned document + personal review | Dated user-confirmed fields linked to that exact bill upload | Historical document, not live provider verification; unsupported facts stay unknown. | User-reviewed evidence |
| Personal confirmation | User-declared current understanding of exact account/plan | Explicitly labeled; not merchant verified. | Supported |

References:
- https://developer.spotify.com/documentation/web-api/reference/get-current-users-profile
- https://support.spotify.com/us/article/gift-cards/ (gift credit can end and regular payments resume)
- https://developer.apple.com/documentation/appstoreserverapi/statusresponse
- https://developer.apple.com/documentation/appstoreserverapi/get-all-subscription-statuses
- https://developers.google.com/android-publisher/api-ref/rest/v3/purchases.subscriptionsv2
- https://developers.google.com/android-publisher/api-ref/rest/v3/purchases.subscriptionsv2/get
- https://developer.paypal.com/api/subscriptions/v1/subscriptions-get/

No supported authorized renewal connector was established from these documented interfaces and
current application adapters. Existing external identity login must not be repurposed as merchant
billing consent. The UI truthfully reports direct merchant verification unavailable.
A future connector requires documented required fields, explicit read-only consent and owner/account
binding, approved access/commercial terms, expiry/revocation, bounded refresh/rate limits and separately
recorded real-provider acceptance. Do not create credentials or add paid services for this goal.

## Verification required before integration

- Domain: independent states; prepaid with/without later renewal; cancellation with access remaining;
  one-off; unknown; stale/conflicting sources; older receipt; passed date; invalid data; amount/currency pairing.
- Authenticated API: anonymous/foreign resources, source spoofing, single posted debit vs pending/credit/
  removed/foreign, source-document ownership, duplicate account labels, idempotent explicit retry,
  stale revision, owner-only export and erasure.
- Disposable real PostgreSQL: additive migration/model parity, composite owner FKs, revision races and
  transaction rollback, durable submission identity, restrictive dependencies, scoped export/erasure.
- Real browser BFF/API on isolated CI stack plus intercepted synthetic stale/error cases in both engines;
  EN/ES phone layouts and no automatic write replay. Inspect resulting screenshots.
- Protected final-head backend/Android/container/dependency checks and applicable security/review gates.

Written verification code is not evidence of execution. Record actual final-head results in the PR/handoff.
No production/master promotion or deployment, paid activation, external account/secret changes,
provider acceptance, physical-device or legal acceptance is claimed by these fixtures.

## Official account-help fallback

The review page offers a disclosure containing four fixed HTTPS destinations, opened only by the
user: Spotify account, Apple billing support, Google Play subscriptions and PayPal automatic-payment
help. They are account-navigation aids, not integrations. No labels, review IDs, transaction details,
tokens or return URLs are appended. Links suppress referrers and opener access. Arbitrary merchant
names never generate links; users of another billing company are directed to its official app or a
trusted bookmark. Opening a link cannot alter evidence, mark verification current or grant access.

Official destinations and supporting navigation were inspected on 2026-10-25:
- https://support.spotify.com/uk/article/canceled-but-still-charged/ links to https://www.spotify.com/account
- https://support.apple.com/billing
- https://support.google.com/googleplay/answer/7018481 links to https://play.google.com/store/account/subscriptions
- https://www.paypal.com/us/cshelp/article/what-is-an-automatic-payment-and-how-do-i-update-or-cancel-one-help240

The Spotify and Google account pages require the user's own sign-in. Their authenticated content was
not inspected. No account action, cancellation, payment, sign-in or merchant contact was performed.
