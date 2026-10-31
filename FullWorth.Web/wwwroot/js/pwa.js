(() => {
    let deferredInstallPrompt = null;
    let serviceWorkerRegistration = null;
    let updateCallback = null;
    let updateAvailable = false;
    let timedOutUpdateWorker = null;

    function announceUpdate() {
        if (updateAvailable) {
            return;
        }

        updateAvailable = true;

        if (updateCallback) {
            void updateCallback.invokeMethodAsync(
                "OnPwaUpdateAvailableAsync");
        }
    }

    function watchRegistration(registration) {
        serviceWorkerRegistration = registration;

        if (registration.waiting &&
            navigator.serviceWorker.controller) {
            announceUpdate();
        }

        registration.addEventListener(
            "updatefound",
            () => {
                const worker = registration.installing;

                if (!worker) {
                    return;
                }

                worker.addEventListener(
                    "statechange",
                    () => {
                        if (worker.state === "installed" &&
                            navigator.serviceWorker.controller) {
                            announceUpdate();
                        }
                    });
            });
    }

    function isStandalone() {
        return Boolean(
            window.matchMedia?.(
                "(display-mode: standalone)")?.matches ||
            window.navigator.standalone === true);
    }

    function isIosLike() {
        const userAgent =
            navigator.userAgent ?? "";

        const isClassicIos =
            /iPad|iPhone|iPod/i.test(
                userAgent);

        const isTouchMac =
            navigator.platform ===
                "MacIntel" &&
            navigator.maxTouchPoints >
                1;

        return isClassicIos ||
            isTouchMac;
    }

    window.addEventListener(
        "beforeinstallprompt",
        event => {
            event.preventDefault();

            deferredInstallPrompt =
                event;
        });

    window.addEventListener(
        "appinstalled",
        () => {
            deferredInstallPrompt =
                null;
        });

    window.FullWorthPwa = {
        isStandalone,

        setUpdateCallback(callback) {
            updateCallback = callback;

            if (updateAvailable && updateCallback) {
                void updateCallback.invokeMethodAsync(
                    "OnPwaUpdateAvailableAsync");
            }
        },

        async applyUpdate() {
            const registration =
                serviceWorkerRegistration;

            if (!registration) {
                return "unavailable";
            }

            // A timed-out request may have activated since its listener expired.
            // Only a fresh explicit action may reload, and only for that exact worker.
            if (!registration.waiting && timedOutUpdateWorker &&
                navigator.serviceWorker.controller === timedOutUpdateWorker) {
                timedOutUpdateWorker = null;
                window.location.reload();
                return "reloaded";
            }

            if (!registration.waiting) {
                await registration.update();
            }

            const waitingWorker =
                registration.waiting;

            if (!waitingWorker) {
                return "unavailable";
            }

            return await new Promise(resolve => {
                let settled = false;
                let timeout = null;

                const onControllerChange = () => {
                    if (navigator.serviceWorker.controller === waitingWorker) {
                        finish("reloaded");
                    }
                };
                const finish = result => {
                    if (settled) {
                        return;
                    }

                    settled = true;
                    timedOutUpdateWorker = result === "timeout" ? waitingWorker : null;
                    navigator.serviceWorker.removeEventListener(
                        "controllerchange", onControllerChange);
                    if (timeout !== null) {
                        window.clearTimeout(timeout);
                    }
                    resolve(result);

                    // Only the currently pending, explicit update attempt may
                    // reload. A timed-out/failed attempt must not reload later.
                    if (result === "reloaded") {
                        window.location.reload();
                    }
                };

                navigator.serviceWorker.addEventListener(
                    "controllerchange", onControllerChange);
                timeout = window.setTimeout(
                    () => finish("timeout"), 10000);

                try {
                    waitingWorker.postMessage({
                        type: "SKIP_WAITING"
                    });
                } catch {
                    // A worker can become redundant before the message is sent.
                    // Clean up this attempt; leave any retry to the user.
                    finish("unavailable");
                }
            });
        },

        async install() {
            if (isStandalone()) {
                return "installed";
            }

            if (deferredInstallPrompt) {
                const prompt =
                    deferredInstallPrompt;

                deferredInstallPrompt =
                    null;

                await prompt.prompt();

                const choice =
                    await prompt.userChoice;

                return choice?.outcome ===
                    "accepted"
                    ? "accepted"
                    : "dismissed";
            }

            if (isIosLike()) {
                return "ios";
            }

            return "browser";
        }
    };

    if (!("serviceWorker" in navigator)) {
        return;
    }

    window.addEventListener(
        "load",
        async () => {
            try {
                const registration =
                    await navigator.serviceWorker.register(
                    "/service-worker.js",
                    {
                        scope: "/",
                        updateViaCache: "none"
                    });

                watchRegistration(registration);
            }
            catch {
                // PWA registration is progressive enhancement.
                // A registration failure must never block FullWorth.
            }
        });
})();
