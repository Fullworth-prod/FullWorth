# AI monetary source evidence boundaries — 2026-10-28

## Reproduced failure

Actual current validator/converter/contracts execution on synthetic documents accepted and marked ready eight wrong-total examples: 110.005 cropped to10.00; a negative or parenthesized104.99 cropped to positive104.99; 1,104.99 or1104.99 cropped to104.99;104.995 cropped to104.99;0.005 cropped to0.00; and an omitted sign after the currency symbol. Storage precision validation did not help when the proposed smaller/cropped amount already fit two decimals. Generic source-substring and within-excerpt matching were insufficient.

The full new regression suite against the original validator produced122 passes and56 failures: all55 cropped-source monetary cases were wrongly accepted plus a dense misleading-source rejection. The other39 new controls and83 previous tests passed. These are regression assertions, not56 independent production incidents. No model/provider/private document was called, and AI extraction remains disabled.

## Change

Tokenize the normalized complete source once, masking dates without shifting positions. Monetary evidence must align its excerpt's numeric span with a complete source token with the exact signed decimal value. This prevents excerpts from dropping larger digits, extra fractional precision, signs, parentheses, separators or date context. Reject unmatched monetary parentheses. Preserve exact valid signed amounts and value-equivalent trailing zeros, and search later genuine complete occurrences rather than accepting a misleading earlier substring.

Use binary lookup over ordered, nonoverlapping source numeric spans to bound repeated-match work. Existing currency/source/ownership/deterministic math and two-decimal storage policies remain. This does not prove semantic field labeling, classify every international money format or resolve every ambiguous invoice context; unsupported evidence stays outside trusted financial persistence, with real document/provider acceptance still required.

## Verification and integration gate

95 new cases:55 cropped amount/date/parenthesis scenarios across previous balance, payments, current charges, total due and line items;38 complete positive/signed/normalized/date-context controls;2 repeated misleading-source cases. All178 focused cases passed after the fix (95 new+38 currency+38 existing validator+7 conversion) using actual source in an isolated in-memory xUnit compilation. Namespace/implicit-using adapters and host-only CS1701 suppression were used; no checkout or repository warning policy changes.

Final-head protected SDK/backend/container and applicable PostgreSQL gates, review/thread/rules checks and integrated-tree verification remain required. Synthetic CI is not private statement/provider/device/delivery/recovery/legal acceptance. Preserve branches/history. No AI activation, production/master deployment, paid activity, credentials/grants/account changes, private export or destructive operation.
