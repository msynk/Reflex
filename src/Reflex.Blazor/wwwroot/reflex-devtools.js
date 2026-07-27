// Bridge between Reflex and the Redux DevTools browser extension.
// Implements the documented extension integration API:
//   window.__REDUX_DEVTOOLS_EXTENSION__.connect(options) -> { init, send, subscribe }
// Time-travel is driven by the extension sending DISPATCH messages back to us.

let connection = null;
let unsubscribe = null;

export function connect(dotNetRef, name) {
    const ext = window.__REDUX_DEVTOOLS_EXTENSION__;
    if (!ext) {
        console.info("[Reflex] Redux DevTools extension not detected. Time-travel disabled.");
        return false;
    }

    connection = ext.connect({
        name: name || "Reflex",
        // Only advertise what the .NET side actually implements.
        features: {
            pause: false,
            export: true,
            import: "custom",
            jump: true,
            skip: false,
            reorder: false,
            dispatch: false,
            test: false
        }
    });

    unsubscribe = connection.subscribe(message => {
        // Forward every extension message to .NET, which decides how to time-travel.
        dotNetRef.invokeMethodAsync("HandleMessage", JSON.stringify(message));
    });

    return true;
}

export function init(stateJson) {
    if (!connection) return;
    connection.init(safeParse(stateJson));
}

export function send(actionType, stateJson, payloadJson) {
    if (!connection) return;
    const action = { type: actionType };
    if (payloadJson) {
        action.payload = safeParse(payloadJson);
    }
    connection.send(action, safeParse(stateJson));
}

export function disconnect() {
    // Detach only this connection; ext.disconnect() would kill every DevTools
    // instance on the page (including other libraries').
    if (typeof unsubscribe === "function") {
        try { unsubscribe(); } catch { /* extension already gone */ }
    }
    unsubscribe = null;
    connection = null;
}

function safeParse(json) {
    try {
        return json ? JSON.parse(json) : {};
    } catch {
        return {};
    }
}
