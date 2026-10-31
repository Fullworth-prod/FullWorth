## Verified development integration — 2026-10-29 UTC

#1010 merged as9b2780fa5043bb5c0995268fd8d2e1d1d640a55b after allfour required source4efe8eebe5aebe5ce8d32ffd664d5e3b764ab6ed checks passed; tested33f3f4fc2dbb661c1f3d2869a60c67f172aaeeaf and integrated treed92fabdf5c26120a5bf7160d3446c5a2a7c0b1eb match. Main40816899055 passed3,507 .NET/668 Node, zero failed/skipped/backend/offline warnings/errors. Full rendered/virtual passkey/HTTP/parser/disposable restoreb4d574a1e9016131c35ec47388d95ec69a76d690c393a174bdba1cadcd112c5c passed; Android steps conditional. Separate installed-Chromium loopback with two real workers confirmed late activation does not reload, explicit retry reloads once into the target, and only one activation message is sent. The helper timeout was shortened in that local probe; it is not pinned/physical-device acceptance. Branch retained, no deployment/master promotion.

Pending statements below are the historical preparation record; physical installed-device gates stay open. [Full ledger](MILESTONE-CLEANUP-AUDIT-20261027.md).

---

# Explicit PWA recovery for the requested worker — 2026-10-29 UTC

## Reproduction against verified development

The update listener cleanup from #1007 correctly prevents automatic reload after timeout. If that exact requested worker activates afterward, registration.waiting becomes null, but a fresh click on Update only checks for another waiting worker and returns unavailable. The user cannot complete that update through the existing button. Separately, an active attempt accepted any controllerchange as success, even for a different worker. Both gaps reproduce against actual current pwa.js in isolated synthetic event execution; no real-device incident or financial-data loss is asserted.

## Change

Remember only the worker from a timed-out attempt. No listener or timer survives settlement. A fresh explicit action may reload when there is no newer waiting worker and navigator.serviceWorker.controller is exactly the remembered worker. Late activation by itself does not reload, and the active worker receives no repeated SKIP_WAITING message. Failed attempts clear this recovery candidate. A newer waiting worker takes priority.

An active attempt now confirms only its requested worker; unrelated controller changes keep waiting until the correct target arrives or the original timeout expires. Cleanup remains bounded and idempotent. Object identity is intentional: the [Service Workers specification](https://w3c.github.io/ServiceWorker/spec/service_worker/#getting-service-worker-instances) maps a worker to one JavaScript object within the environment, and the controller getter uses that map. Sharing a script URL does not prove sharing a worker/version.

## Verification and limits

The16-case actual-script Node suite includes five new scenarios: late activation plus explicit retry, same-URL/different-worker rejection, failed-message recovery rejection, newer-waiting-worker priority, and unrelated-controller rejection followed by the real target. Two new assertions fail current source; all16 pass with the change. The existing fake-event adapter now sets the controller when simulating activation, matching the browser receipt that the implementation checks. Existing success/timeout/cleanup/no-replay/unavailable cases remain.

The required Node glob executes this suite. Full final-head SDK, container/browser and protected checks remain required before development integration. VM fixtures and existing CI browser flows do not establish physical Android/iPhone installation/update acceptance. There is no new timer, automatic update/reload retry, private data/network export, financial mutation, service-worker cache change, production deployment, grant or spending. All original goal and release safeguards remain active.
