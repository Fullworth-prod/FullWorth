# Bound explicit PWA update attempts — 2026-10-29 UTC

## Demonstrated failure

The actual pwa.js update helper resolves timeout after10seconds but leaves a controllerchange callback registered. That callback calls window.location.reload even when its finish function finds the attempt already settled. An unrelated/later activation can therefore reload after the timeout; a subsequent explicit retry can accumulate reload callbacks. A worker.postMessage exception also leaves the old callback behind. No real-user loss or physical-device incident is asserted from this synthetic reproduction.

## Change

Each pending explicit update attempt owns one listener and timer. Settling removes the listener and clears the timer; reload runs only when an unsettled attempt succeeds. A redundant-worker message failure settles unavailable and cleans up without an automatic retry. Existing user-triggered update discovery, successful reload, unavailable browser/registration behavior and install flow remain supported. Registration/update-check failures retain the caller's existing exception handling. Offline caching and financial request behavior are not changed.

Eleven actual-script Node VM cases cover success, timeout then late activation, explicit retry, worker message failure, synchronous activation, missing/discovered waiting worker, failed update check, repeated timeouts, unsupported service worker and rejected registration. Seven fail against original source; all11 pass with cleanup. Fake timers/events isolate the lifecycle and do not establish installed Android/iPhone or real service-worker acceptance. The required backend Node glob executes this file. Full final-head protected and browser/container checks are still required before development integration; no production deployment is authorized.
