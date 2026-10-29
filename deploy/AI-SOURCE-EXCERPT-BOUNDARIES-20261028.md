## Verified integration status — 2026-10-29 UTC

Verified in development through #1006 (3913a8d614f6182de0f05064fbaba431b0012f4a), source018d6e3f07ab21bd65610d32e45411d549988a50/testeda484530db83e55a98d96607221068317968751ab/tree08586b779c923fb18be9b029e57dd61c5e52f4e8 equal. All10 final-head checks passed;3,362 .NET/434 Node, zero failed/skipped/backend/offline warnings/errors; scoped relational/container checks. AI remains off; real document/provider semantics remain separate.

The detailed current ledger is in MILESTONE-CLEANUP-AUDIT-20261027.md. Original preparation notes below are preserved as history; this documentation change requires its own checks. No production deployment or new real acceptance is implied.

---

# AI source excerpt boundaries — 2026-10-28

## Actual-source counterexamples

Fresh development78ff26decbc8df92cd93d6a052ecb4e595ad46dd accepted four proposed dates cropped from invalid five-digit years (e.g.08/10/20260→08/10/2026), a claimed account suffix3456 cropped from the middle of1234567890, and shorter provider/plan/usage words cropped from larger source tokens. The actual validator/converter marked all eight synthetic candidates ready. No model/provider/private document was used, and AI remains off.

Against the full new147-case suite, prior source produced98 negative assertion failures and49 compatible passes. Those failures are reproducible accepted bad-evidence cases, not98 separate production incidents.

## Correction

Require non-monetary excerpts to occur at complete source word/identifier boundaries. An account suffix may omit the identifier prefix but must end the actual identifier. Within-excerpt checks also recognize Unicode letters/numbers/marks/connectors using full scalar categories, including preceding surrogate pairs, preventing underscores, combining marks or non-BMP digits from faking boundaries. Preserve punctuation, normalized whitespace, complete later occurrences and genuine bare account suffixes.

Monetary facts retain the complete signed numeric-source-span checks introduced in#1004 rather than generic word boundaries, preserving supported adjacent labels such as Paid$1.23. This is a boundary/provenance correction, not proof of semantic field labeling, every invoice/date format or real provider/document acceptance.

## Verification and limits

147 new tests span all four dates, provider/account/plan/usage/currency/line-item-description facts, Unicode source boundaries, genuine suffixes, complete/later occurrences, internal identifier continuation and an adjacent-label money control. All345 focused actual-source xUnit cases passed (147 new+198 previous). In-memory namespace/implicit-using/same-pattern Regex adapters and host-only CS1701 suppression were used; full SDK/generated-regex/protected/container/applicable PostgreSQL CI is still required before integration.

No AI activation, production/master promotion, spending, credentials/grants/account changes, private export, outreach, destructive operations or real provider/device/recovery/legal acceptance. Preserve branches/history and the complete active milestone goal. Earlier financial and ownership safeguards remain.
