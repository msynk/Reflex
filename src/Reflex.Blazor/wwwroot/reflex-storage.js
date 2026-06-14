// Thin bridge over the browser's Web Storage areas used by Reflex persistence.
// `area` is "local" or "session"; failures (private mode, quota) degrade to no-ops.

function store(area) {
    return area === "session" ? window.sessionStorage : window.localStorage;
}

export function get(area, key) {
    try {
        return store(area).getItem(key);
    } catch {
        return null;
    }
}

export function set(area, key, value) {
    try {
        store(area).setItem(key, value);
    } catch (e) {
        console.warn("[Reflex] storage write failed:", e && e.message);
    }
}

export function remove(area, key) {
    try {
        store(area).removeItem(key);
    } catch {
        // ignore
    }
}
