# Raw currency validation at display and due-alert boundaries — 2026-10-29 UTC

## Demonstrated defect

Actual FinancialAmountFormatter source at development9b/816 accepts the non-ASCII string uſd, normalizes it to USD and displays100 as $100.00. Validating after Unicode case folding therefore relabels an invalid stated unit as a real currency. The statement parser/AI raw-unit guards from #1004 are separate and remain intact; this is the shared presentation helper and a due-alert caller that pre-normalized before using it.

The31-case actual-source suite yields20 compatible passes/11 controlled failures against original code. These are synthetic regression cases, not11 production incidents. No private transaction, bank record or statement was inspected or retagged.

## Correction

Validate exactly three raw ASCII letters after the existing outer whitespace trim, then normalize ASCII case. Both strict NormalizeCurrency/Format and tolerant TryNormalizeCurrency/FormatOrUnspecified/FormatTotals share the same check. Invalid units stay unavailable/excluded rather than creating a false dollar bucket. Existing lowercase/whitespace handling, valid ASCII units, explicit unknown ASCII labels such as XYZ, per-currency grouping and amounts remain unchanged; no conversion is added.

Payment-due reconciliation now calls the same raw validator instead of case folding first. Invalid input raises the existing InvalidOperationException before any reconciliation is staged. Ownership and missing-due/zero/clear behavior remain protected by existing tests; no earlier alert is replaced with a falsely denominated reminder.

## Verification and limits

31 local actual-source xUnit cases passed:13 invalid-unit cases,8 ASCII compatibility controls, one mixed-total control,6 due-alert rejection cases and3 due-alert currency controls. The real formatter/service/contracts were compiled with an isolated enum/implicit-using/reference adapter and test gateway fakes, not a local checkout; host-only CS1701 unification suppression is not the full SDK gate. Full final-head .NET/Node/native/container and applicable PostgreSQL checks remain required.

Historical records previously canonicalized elsewhere are not repaired by this formatting change. Any private original-unit reconciliation still needs its authorized expected-fact evidence. This does not establish real provider/document/device/recovery/legal acceptance, deploy production, activate AI/creator/paid features, create grants or export private financial data.
