let activeDialog = null;

// Uses an HTML top-layer dialog, not window.confirm or a native WebView alert.
export function confirmAction({ title, message, cancelText, confirmText }) {
    if (activeDialog) return Promise.resolve(false);
    const dialog = document.createElement("dialog");
    if (typeof dialog.showModal !== "function") {
        return Promise.reject(new Error("This browser does not support in-app confirmation."));
    }

    dialog.className = "fw-confirm-dialog";
    dialog.setAttribute("aria-labelledby", "fw-confirm-title");
    dialog.setAttribute("aria-describedby", "fw-confirm-message");

    const kicker = document.createElement("p");
    kicker.className = "fw-confirm-kicker";
    kicker.textContent = "FullWorth";
    const heading = document.createElement("h2");
    heading.id = "fw-confirm-title";
    heading.textContent = title;
    const description = document.createElement("p");
    description.id = "fw-confirm-message";
    description.className = "fw-confirm-message";
    // Institution names and translated copy are always text, never HTML.
    description.textContent = message;
    const actions = document.createElement("div");
    actions.className = "fw-confirm-actions";
    const cancel = document.createElement("button");
    cancel.type = "button";
    cancel.className = "fw-confirm-cancel";
    cancel.textContent = cancelText;
    cancel.autofocus = true;
    const confirm = document.createElement("button");
    confirm.type = "button";
    confirm.className = "fw-confirm-destructive";
    confirm.textContent = confirmText;
    actions.append(cancel, confirm);
    dialog.append(kicker, heading, description, actions);

    const previousFocus = document.activeElement;
    activeDialog = dialog;

    return new Promise((resolve, reject) => {
        let finished = false;
        function cleanup() {
            window.removeEventListener("pagehide", dismiss);
            window.removeEventListener("popstate", dismiss);
            if (dialog.open) dialog.close();
            dialog.remove();
            if (activeDialog === dialog) activeDialog = null;
            if (previousFocus?.isConnected) previousFocus.focus({ preventScroll: true });
        }
        function finish(accepted) {
            if (finished) return;
            finished = true;
            cleanup();
            resolve(accepted === true);
        }
        function dismiss() { finish(false); }

        cancel.addEventListener("click", dismiss);
        confirm.addEventListener("click", () => finish(true));
        dialog.addEventListener("cancel", event => {
            event.preventDefault();
            dismiss();
        });
        dialog.addEventListener("close", dismiss);
        dialog.addEventListener("keydown", event => {
            if (event.key !== "Tab") return;
            if (event.shiftKey && document.activeElement === cancel) {
                event.preventDefault();
                confirm.focus();
            } else if (!event.shiftKey && document.activeElement === confirm) {
                event.preventDefault();
                cancel.focus();
            }
        });
        dialog.addEventListener("click", event => {
            if (event.target !== dialog) return;
            const rect = dialog.getBoundingClientRect();
            if (event.clientX < rect.left || event.clientX > rect.right ||
                event.clientY < rect.top || event.clientY > rect.bottom) dismiss();
        });
        window.addEventListener("pagehide", dismiss);
        window.addEventListener("popstate", dismiss);
        try {
            document.body.append(dialog);
            dialog.showModal();
            cancel.focus({ preventScroll: true });
        } catch (error) {
            finished = true;
            cleanup();
            reject(error);
        }
    });
}
