## Verified integration — 2026-10-28 UTC

Merged #1002 as eee08650724c65338ab4406601d83888eab4852e after all10 final-source checks (de5c94b15889a200f628b0ba7c89946f66c04b75/tested323cefb2bf2c08b20936ff6bc19681f0d442acf7/tree3afa3ec48fb6b4ca66603ea62b908a08abdeef17). Actual3,100 .NET/368 Node; applicable PostgreSQL/container checks passed, native/browser conditional. #1004 subsequently adds raw ASCII-before-casing guards in both parsing paths, merged as a161376fb9f712d332e5a166ee198a67f32d9edc after all10 checks and3,215 .NET/434 Node. Earlier prepared-head notes below are historical. AI remains off, branches retained, master9540 unchanged; no deployment/provider/device/recovery acceptance.

---

# AI statement currency recognition — 2026-10-28

## Reproduced defect and correction

An isolated execution of the actual current validator, candidate contracts and conversion service accepted a candidate currency DUE whose genuine source excerpt was “Total due $104.99”; conversion returned ready for validation with currency DUE. XXX, XTS, ZZZ and ÅBC were also accepted with matching source excerpts. USD and CAD controls passed. The probe called no provider, model or private document and did not enable AI extraction.

Move the existing deterministic parser's bounded recognized-currency set into an internal statement policy shared by deterministic parsing and AI candidate validation. AI proposals now need a recognized code in addition to the existing whole-value/source evidence checks. Unknown/noncurrency/reserved test/Unicode lookalike codes fail before conversion; missing currency stays incomplete. Recognized units normalize without conversion or changing the numeric amount. Deterministic parsing retains its exact recognition behavior, explicit conflict guards and documented legacy no-declaration USD-format fallback.

Recognition uses installed .NET globalization currency metadata with USD retained and XXX/XTS excluded; this is conservative recognition, not a complete permanent ISO registry or international-format parser. Unsupported legitimate units require review. This change does not prove a whole source document's currency interpretation, resolve conflicting semantic context, retag historical records, or change the default-off AI activation gate.

## Regression scope and required verification

32 new conversion/validator scenarios cover 14 invalid codes with real source evidence, 10 recognized/normalized units with exact amounts, 4 embedded-token rejections, an invented excerpt rejection and 3 incomplete currency states. Existing deterministic currency and AI precision/evidence suites must also pass. In-memory actual-source execution is focused evidence only; final-head protected SDK CI and applicable PostgreSQL suites must pass before development integration.

No provider/device/real private financial acceptance, production recovery, legal acceptance, deployment, credentials/grants, spending or outreach is established or authorized. Development integration is not deployment.

## Focused execution before PR

77 actual xUnit assertions/cases passed in an isolated in-memory compilation: all 32 new currency cases, 38 existing validator cases and 7 existing conversion cases. Actual validator/converter/contracts/structured models were used; file-scoped namespaces and implicit usings were adapted for a single compilation. The host-only CS1701 reference-unification warning was suppressed; repository warning gates are unchanged. This is not a full SDK build, provider call, production document or activation. Final-head CI remains required.

## Source-boundary review before integration

The initial prepared head5892 accepted five additional clipped source cases: an excerpt USD could be cropped from USDA/XUSD/USD1/1USD, or Currency: USD cropped from Currency: USDA. Whole-value checks inside the excerpt alone did not validate its surrounding source characters. Currency evidence now also requires a whole excerpt occurrence within the normalized source document. Other fact types and intentional account-suffix evidence rules are unchanged. Five negative cases and a valid later whole-occurrence control were added (38 new currency cases total). All83 AI-focused cases plus74 deterministic parser cases passed in isolated execution; initial5892 CI is historical, and the revised source requires its own final-head checks.
