# Bill-detail reads and historical explanation qualification — 2026-10-29 UTC

## Demonstrated issues

The actual native detail client accepts mismatched route identities, null collections, null/duplicate/empty row IDs and unconfirmed successful responses. These may reach page bindings as apparently valid bill detail. Isolated execution of18 actual-client cases yields five compatible passes/13 controlled failures against the previous client. These are synthetic counterexamples, not13 real incidents.

Separately, BillExplanationService generates monthly/yearly Summary text; BillStatementChangeDetectionService.BuildDescription persists that summary with its cause evidence. Both detail consumers showed stored Description verbatim even when the new per-change annual basis was unavailable. Hiding the annual amount alone does not qualify those saved words.

## Corrections and preserved behavior

Require HTTP200 application/json, the exact requested bill ID, a provider name, nonnull statement/change lists and nonnull unique nonempty row IDs before the native page consumes a detail snapshot. Existing failed-status handling and invalid-request-ID guards remain. Valid empty histories and old responses without annual flags remain usable. Preserve unsupported/quarterly/unlinked historical facts for review rather than inventing their annual basis; no strict reference-completeness or currency retagging is added.

Native/Web explanations with an unavailable annual basis are explicitly labeled as earlier saved explanations whose monthly/yearly estimates are unverified. Web also uses an earlier-saved heading. The saved words and valid causes remain intact; they are not parsed as free-form money, rewritten or deleted. Supported same-unit annual evidence retains its previous explanation. Invalid currency still prevents a verified-rate presentation even if a response claims the flag. Spanish qualifier/heading resources are added with one key each.

## Executed local verification and limits

18 actual native API-client tests passed, covering invalid response boundaries, valid empty histories, absent old annual flags and rejection before network for an empty requested ID. Native property execution passed seven cases (including four prior chronology/annual controls and three qualifier/unchanged-word cases). Six actual Web helper/DTO source cases passed with an English lookup substitute; this is not full Razor/browser or Spanish-runtime proof. Local source/namespace/device-shell adapters are not a full SDK or physical device.

The committed native18 and added Web3 regressions require actual SDK execution. Rendered1440/320 cases retain a valid annual-amount assertion and verify that the old-response/unknown-unit explanations carry a qualifier while preserving their saved rate/cause words. Node syntax validation is only a parser check; require final-head rendered execution, screenshots and relevant native/relational/protected checks before integration. No full integration success is predeclared.

Baseline: verified development d482a162f34b2346687247e3ac79d4491037ac36 after #1017, whose source/tested/integrated tree58a8b6d362af05ddf4dc21ae7db175bc005c90cd matched and all18 checks passed. This follow-up needs independent final-head evidence. Source branches/history stay retained.

No production deployment/master promotion, financial-data export, new credentials/grants, account/secret changes, spending, outreach/delivery or destructive operation. Sunday candidate freeze and package completion remain pending journey review; real provider/device/delivery/recovery/two-owner/legal acceptance stays separate. AI/creator gates remain off where required.
