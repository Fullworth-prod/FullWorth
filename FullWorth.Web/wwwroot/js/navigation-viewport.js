// Presentation only: reserve the actual, font-dependent tab-bar height.
// Never reads account data, storage, cookies or the network.
const leases = new Map();
const owners = new WeakMap();
let sequence = 0;

export function observeNavigation(anchor) {
    const shell = anchor?.closest?.(".app-shell");
    const bar = shell?.querySelector(".app-sidebar");
    const nav = shell?.querySelector(".mobile-bottom-nav");
    if (!shell || !bar || !nav) return 0;
    const previous = owners.get(shell);
    if (previous) disconnectNavigation(previous);
    const id = ++sequence;
    let stopped = false, frame = 0;
    let resize, mutation;
    const measure = () => {
        frame = 0;
        if (stopped) return;
        const visible = getComputedStyle(nav).display !== "none";
        const bounds = bar.getBoundingClientRect();
        const bottom = Number.parseFloat(getComputedStyle(bar).bottom);
        const inset = visible && bounds.height > 0 ?
            Math.ceil(bounds.height + (Number.isFinite(bottom) ? Math.max(0, bottom) : 0) + 20) : 0;
        const value = inset + "px";
        if (shell.style.getPropertyValue("--fw-navigation-inset") !== value)
            shell.style.setProperty("--fw-navigation-inset", value);
    };
    const schedule = () => {
        if (!stopped && !frame) frame = requestAnimationFrame(measure);
    };
    const cleanup = () => {
        stopped = true;
        if (frame) cancelAnimationFrame(frame);
        resize?.disconnect();
        mutation?.disconnect();
        window.removeEventListener("resize", schedule);
        window.removeEventListener("orientationchange", schedule);
        if (owners.get(shell) === id) {
            owners.delete(shell);
            shell.style.removeProperty("--fw-navigation-inset");
        }
    };
    owners.set(shell, id);
    leases.set(id, cleanup);
    try {
        if (typeof ResizeObserver === "function") {
            resize = new ResizeObserver(schedule);
            resize.observe(nav);
            resize.observe(bar);
        }
        if (typeof MutationObserver === "function") {
            mutation = new MutationObserver(schedule);
            mutation.observe(document.documentElement, { attributes: true,
                attributeFilter: ["style", "data-fullworth-text-size", "class"] });
        }
        window.addEventListener("resize", schedule);
        window.addEventListener("orientationchange", schedule);
        document.fonts?.ready?.then(schedule, () => {});
        measure();
        return id;
    } catch (error) {
        disconnectNavigation(id);
        throw error;
    }
}

export function disconnectNavigation(id) {
    const cleanup = leases.get(id);
    if (!cleanup) return;
    leases.delete(id);
    cleanup();
}
