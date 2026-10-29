# Account export envelope boundary — 2026-10-29 UTC

## Demonstrated defect

The development BFF's account-only download proxy requires HTTP 200, application/json and a syntactically complete JSON object, but any object qualifies. Isolated execution of the actual AdminBffWriteProxyService at development3913a8d614f6182de0f05064fbaba431b0012f4a returned FileContentHttpResult for {}, a login-error message object and a schemaVersion-only object, with one upstream request each. These are controlled synthetic upstream responses, not evidence of a real private-data export incident.

## Contract

Validate the current API AccountDataExportResult envelope in the already-buffered UTF-8 bytes: schemaVersion1.1; parseable exportedAtUtc; profile with string displayName/email, createdAtUtc, nullable lastLoginAtUtc and boolean isActive; and all13 named record collections. Missing, mistyped, duplicated known fields, unsupported schema versions, non-record collection members, invalid JSON/depth/trailing data fail closed with fixed502 and no downloadable body. Escaped field names participate in duplicate checks. Empty collections/display names, inactive profiles, nullable login dates, additive root/profile metadata and original bytes/filename remain supported.

The validator scans metadata and skips nested records without constructing a second private-data object graph or rewriting the export. It validates the envelope and record-object structure, not every record's business semantics, owner scoping, completeness relative to the database or authenticity. Those remain API duties and controlled same-release two-owner/export acceptance. A future schema-version change requires a coordinated API/BFF contract update; actual DTO serialization is included in positive tests to surface drift.

No authentication/CSRF/sign-out policy, API export builder, private export endpoint access, automatic retry or financial data is changed. Tests use only disposable synthetic values and the real proxy with a fake HTTP handler. Final-head protected CI remains required before development integration. No deployment, real-owner privacy acceptance or recovery/legal acceptance is implied.

## Executed preparation evidence

All202 focused existing/new xUnit cases passed in isolated actual-source execution against ASP.NET10.0.14, including the actual current API record definitions/default web serialization and exact-byte positives.145 cases are new;140 new negative cases fail against the original source (the original already rejects three malformed/trailing JSON cases). This is a controlled regression count, not140 real incidents. The isolated compiler/reference adapter is not the full SDK/protected CI gate.
