using System.Text.Json.Nodes;
using Microsoft.JSInterop;

namespace Reflex.Blazor;

/// <summary>
/// Bridges a <see cref="ReflexStore"/> to the Redux DevTools browser extension, enabling
/// live action inspection and time-travel. Created and managed by <see cref="ReflexProvider"/>.
/// </summary>
public sealed class ReduxDevToolsConnector : IReflexDevTools, IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private DotNetObjectReference<ReduxDevToolsConnector>? _selfRef;
    private ReflexStore? _manager;
    private bool _ready;

    /// <summary>Creates a connector over the supplied JS runtime.</summary>
    public ReduxDevToolsConnector(IJSRuntime js) => _js = js;

    /// <summary>
    /// Loads the JS bridge, connects to the extension and wires the manager. Safe to call when the
    /// extension is absent - it simply becomes a no-op.
    /// </summary>
    public async Task ConnectAsync(ReflexStore manager, string name)
    {
        _manager = manager;
        _module = await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Reflex.Blazor/reflex-devtools.js");
        _selfRef = DotNetObjectReference.Create(this);

        var connected = await _module.InvokeAsync<bool>("connect", _selfRef, name);
        if (connected)
        {
            _ready = true;
            manager.ConnectDevTools(this);
        }
    }

    /// <inheritdoc />
    public void Init(JsonObject globalState)
    {
        if (_ready && _module is not null)
            _ = _module.InvokeVoidAsync("init", globalState.ToJsonString());
    }

    /// <inheritdoc />
    public void Send(string actionName, JsonObject globalState)
    {
        if (_ready && _module is not null)
            _ = _module.InvokeVoidAsync("send", actionName, globalState.ToJsonString());
    }

    /// <summary>Invoked from JS for every message the extension sends back (drives time-travel).</summary>
    [JSInvokable]
    public Task HandleMessage(string messageJson)
    {
        _manager?.HandleDevToolsMessage(messageJson);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync("disconnect");
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // Circuit already gone; nothing to clean up.
        }

        _selfRef?.Dispose();
    }
}
