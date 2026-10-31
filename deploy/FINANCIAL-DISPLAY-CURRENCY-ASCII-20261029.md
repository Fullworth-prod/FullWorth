## Verified development integration — 2026-10-29 UTC

Merged as 7eb783bec319142ff7c5874249e83adbcb9d9e7a. All17 final-head checks passed on source 08adf89f387eef65c049765fce7b85700e3e8514; tested candidate abdc2307f776b4b8672c2920f8d32320027083cf and integrated tree 953f297b19e2e8502d086dc52499bcfd613bac40 match. Actual main CI40820676492 passed 3,538 .NET/748 Node, zero failed/skipped and zero backend/offline/actual Android warnings/errors. Applicable PostgreSQL bank, consumed-token/durable Hosted Link, monitoring, confirmation and subscription checks passed, as did Android APK/emulator and iOS simulator/unsigned device packaging. Full container HTTP/parser/secret/cgroup/disposable restore verification passed (89330973bf507a566dcb734287563e5a39d6a837dcc7834f7f49597ad3b9e835); rendered browser steps were conditional for this Core/API scope. Source branch remains present. Packaging/emulation is not physical-device acceptance, and disposable recovery is not real private recovery acceptance. No deployment or master promotion.

The pending language in the preparation record below is historical.

---

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
