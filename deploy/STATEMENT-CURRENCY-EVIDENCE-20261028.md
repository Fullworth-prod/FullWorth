# Explicit statement currency evidence — 2026-10-28 UTC

## Reproduced defect

The actual deterministic parser returned TotalAmount104.99, CurrencyCodeUSD and IsReadyForPersistencetrue for a complete synthetic statement explicitly declaring Currency:CAD and Total Amount Due:$104.99. Existing validation accepts a well-formed USD code, so the incorrect unit could reach automatic persistence. No private statement/provider was used.

## Correction

Recognize explicit Currency:, Statement Currency:, Billing Currency: and Currency code: lines plus a three-letter currency suffix on the same labeled total line. Normalize declared codes without converting or changing amounts. Equivalent repeated declarations remain usable. Conflicting declarations/suffixes return no currency and include CurrencyCode in conflict/missing metadata; malformed or empty explicit declarations never fall back to USD. Existing readiness and currency validation then withhold automatic persistence.

The suffix accepts horizontal whitespace (including nonbreaking/thin spaces) on its own amount line; it cannot borrow an unrelated three-letter word from the next line. Existing amount precision/range, amount-label priority, date parsing, field conflict, ownership and storage policies remain.

This is explicit currency evidence handling within the existing deterministic number/date format, not a general international statement/localization expansion. The historical USD-style fallback for documents with no recognized declaration remains. Unsupported formats and real representative-provider accuracy remain external acceptance work; no FX conversion or global currency inference is claimed.

## Executed validation

56 actual existing/new parser-validation cases passed in isolated C# execution, including35 new currency cases. Coverage includes original CAD mislabel, four declaration labels, suffixes, duplicate evidence, order-independent conflicting codes, declaration/suffix conflicts, invalid/empty codes, CRLF, no next-line borrowing, fractional precision, existing USD compatibility and the real validation boundary.

Local execution used the actual parser, validation service, storage policy and xUnit2.9.3 test classes. GeneratedRegex methods were adapted to ordinary Regex construction with the exact same patterns/options solely because the isolated Add-Type compiler does not run SDK source generators; namespace/implicit-usings and framework-compatibility warning1701 adapters are also local only. Normal protected CI must compile the actual source-generated regex and run the complete solution/applicable relational suites on the final source before integration. These results are not predeclared here.

The change does not establish private provider/document accuracy, real bank visibility, physical-device, delivery, same-release real recovery or legal acceptance. No paid inference/activation, provider contact, production/account/secret change, real private export or deployment occurred.

Six new HTTP/upload/background PDF pipeline cases are also prepared: three declared CAD/EUR/GBP units must persist exactly with the synthetic owner/bill; three unusable declarations must retain review status and create no statement/change/alert. These have not been executed locally and require the final-head normal CI run. The change does not retrospectively audit or retag historical statement rows; any previously affected private upload needs controlled comparison with its original document before correction.
