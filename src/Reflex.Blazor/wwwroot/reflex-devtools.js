// Bridge between Reflex and the Redux DevTools browser extension.
// Implements the documented extension integration API:
//   window.__REDUX_DEVTOOLS_EXTENSION__.connect(options) -> { init, send, subscribe }
// Time-travel is driven by the extension sending DISPATCH messages back to us.

let connection = null;

export function connect(dotNetRef, name) {
    const ext = window.__REDUX_DEVTOOLS_EXTENSION__;
    if (!ext) {
        console.info("[Reflex] Redux DevTools extension not detected. Time-travel disabled.");
        return false;
    }

    connection = ext.connect({
        name: name || "Reflex",
        features: {
            pause: true,
            export: true,
            import: "custom",
            jump: true,
            skip: false,
            reorder: false,
            dispatch: true,
            test: false
        }
    });

    connection.subscribe(message => {
        // Forward every extension message to .NET, which decides how to time-travel.
        dotNetRef.invokeMethodAsync("HandleMessage", JSON.stringify(message));
    });

    return true;
}

export function init(stateJson) {
    if (!connection) return;
    connection.init(safeParse(stateJson));
}

export function send(actionType, stateJson) {
    if (!connection) return;
    connection.send({ type: actionType }, safeParse(stateJson));
}

export function disconnect() {
    const ext = window.__REDUX_DEVTOOLS_EXTENSION__;
    if (ext && typeof ext.disconnect === "function") {
        ext.disconnect();
    }
    connection = null;
}

function safeParse(json) {
    try {
        return json ? JSON.parse(json) : {};
    } catch {
        return {};
    }
}
