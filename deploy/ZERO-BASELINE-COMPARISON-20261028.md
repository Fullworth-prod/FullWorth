# Undefined percentage baseline — 2026-10-28

The actual shared BillComparisonService returned PercentageChange0 for both0→0 and0→10. BillAmount permits zero, so a free/prior-zero monthly statement is valid input; a relative percentage has no defined zero denominator. This is a confirmed comparison-model defect, not a claim that a particular production screen showed it.

Change only the comparison result percentage to decimal? and return null for a zero previous amount. Keep known monthly/annualized dollar deltas, positive-baseline rounding and paid→free -100% unchanged. Null means undefined, not unchanged or infinite. Callers retain actual currency and complete-month evidence rules. No database migration, billing operation or financial evidence mutation.

Executed without a checkout: original two source files compiled in memory reproduced both false zero percentages. Corrected source compiled and executed0→0,0→10,10→0 and79.99→104.99: percentages null/null/-100/31.25 with unchanged0/10/-10/25 monthly and0/120/-120/300 annualized amounts. Twelve actual shared sources, with only namespace/usings remapped for isolated compilation, also executed complete consecutive EUR monthly statements0→10: percentage null, explained10, monthly10, annualized120, Confirmed line-item explanation and EUR text preserved. This is not a solution/CI/provider/private-statement acceptance claim.

Four .NET regression cases cover both zero-baseline outcomes and JSON null, paid→free and complete-month analysis propagation. The candidate needs fresh required backend/native/container/dependency and any triggered relational/browser checks before development integration. No production/master promotion, deployment, private export, credential/configuration or commercial change.
