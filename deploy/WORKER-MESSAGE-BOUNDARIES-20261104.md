# Worker message boundary review — 2026-11-04

The service-worker update receiver accepted a synthetic `SKIP_WAITING` message regardless of event origin. Sixteen Node VM cases against the actual source reproduced nine failed assertions (seven passed) before the guard. These are handler-admission counterexamples, not proof that an external website can obtain or message a cross-origin service-worker reference.

The receiver now requires exact equality between `event.origin` and `self.location.origin` before considering its existing explicit command. Foreign, lookalike, scheme/port-mismatched, opaque, empty and absent origins cannot request early activation. Same-origin explicit updates and later valid attempts remain available. No update timeout, replay, navigation, caching, ownership, financial behavior or auto-update policy changes.

The transaction-filter worker is a dedicated Worker, constructed inside the transaction-stream module's private closure. Its messages travel on the implicit private MessagePort. The HTML MessagePort delivery algorithm does not populate `origin`; legitimate messages have empty origin and null source. A Window-style origin comparison here would reject the real owner. No such comparison or static-analysis suppression is added.

## Executable evidence

- `FullWorth.Tests/service-worker-message-boundary.test.mjs`: actual receiver under Node VM; sixteen positive/negative boundary and subsequent-valid-attempt cases. Isolated pre-fix execution: 7 passed / 9 failed. Isolated fixed execution: 16 passed / 0 failed. Only public source paths were remapped; no local checkout.
- `FullWorth.VisualTests/worker-message-boundary.test.mjs`: actual dedicated-worker, service-worker and PWA caller sources with observer-only fixture listeners on two disposable localhost origins. Six scenarios per engine target native empty-origin/null-source delivery; same/cross-origin Window message separation; 500/501 capacity and preservation; native WindowClient metadata/unknown-command refusal; exact waiting-worker activation; actual explicit caller single reload. Initial hosted execution is pending. Results JSON is retained in the existing confirmation-browser artifact.
- Chromium/WebKit fixtures are not physical Android/iPhone, production PWA upgrade, arbitrary cross-origin exploit, provider, private financial-data or release acceptance. Node VM messages can construct otherwise browser-constrained inputs.

## Static review status

The full-candidate diagnostic reports run without alert/database upload, settings changes, grants or suppression. The service-worker warning must be re-evaluated on the final source. The dedicated-worker finding remains raw and requires contextual disposition; no blanket clean verdict is claimed. The separate C# consent-gate finding and remaining compiled/source extraction diagnostics remain open under the existing security/release work.

References: [HTML workers](https://html.spec.whatwg.org/multipage/workers.html), [MessagePort delivery](https://html.spec.whatwg.org/multipage/web-messaging.html), [ServiceWorker postMessage](https://w3c.github.io/ServiceWorker/), [CodeQL missing origin check](https://codeql.github.com/codeql-query-help/javascript/js-missing-origin-check/).
