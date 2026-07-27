using System.Text.Json.Nodes;
using Microsoft.JSInterop;

namespace Blex.Blazor;

/// <summary>
/// Bridges a <see cref="BlexManager"/> to the Redux DevTools browser extension, enabling
/// live action inspection and time-travel. Created and managed by <see cref="BlexProvider"/>.
/// </summary>
public sealed class ReduxDevToolsConnector : IBlexDevTools, IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private DotNetObjectReference<ReduxDevToolsConnector>? _selfRef;
    private BlexManager? _manager;
    private bool _ready;
    private bool _disposed;

    /// <summary>Creates a connector over the supplied JS runtime.</summary>
    public ReduxDevToolsConnector(IJSRuntime js) => _js = js;

    /// <summary>
    /// Loads the JS bridge, connects to the extension and wires the manager. Safe to call when the
    /// extension is absent or the bridge fails to load - it simply becomes a no-op.
    /// </summary>
    public async Task ConnectAsync(BlexManager manager, string name)
    {
        _manager = manager;
        try
        {
            var module = await _js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/Blex.Blazor/blex-devtools.js");
            if (_disposed)
            {
                // Disposed while the import was in flight (fast navigation); don't connect.
                await module.DisposeAsync();
                return;
            }

            _module = module;
            _selfRef = DotNetObjectReference.Create(this);

            var connected = await _module.InvokeAsync<bool>("connect", _selfRef, name);
            if (connected && !_disposed)
            {
                _ready = true;
                manager.ConnectDevTools(this);
            }
        }
        catch (JSDisconnectedException)
        {
            // Circuit gone before the bridge loaded.
        }
        catch (JSException ex)
        {
            // A missing static asset or an extension quirk must not break app startup.
            Console.Error.WriteLine($"[Blex] DevTools bridge unavailable: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public void Init(JsonObject globalState)
    {
        if (_ready && _module is not null)
            FireAndForget(_module.InvokeVoidAsync("init", globalState.ToJsonString()));
    }

    /// <inheritdoc />
    public void Send(string actionName, JsonObject globalState)
        => Send(actionName, globalState, null);

    /// <inheritdoc />
    public void Send(string actionName, JsonObject globalState, JsonObject? payload)
    {
        if (_ready && _module is not null)
            FireAndForget(_module.InvokeVoidAsync("send", actionName, globalState.ToJsonString(), payload?.ToJsonString()));
    }

    // DevTools is a non-critical sink, so interop is dispatched without blocking the dispatch
    // pipeline. We still observe the task so a transient interop failure can't surface as an
    // unobserved task exception.
    private static void FireAndForget(ValueTask task)
    {
        if (task.IsCompletedSuccessfully)
            return;

        _ = Awaited(task);

        static async Task Awaited(ValueTask t)
        {
            try
            {
                await t;
            }
            catch (JSDisconnectedException)
            {
                // Circuit gone; nothing to report.
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Blex] DevTools interop failed: {ex.Message}");
            }
        }
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
        _disposed = true;
        _ready = false;
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync("disconnect");
                await _module.DisposeAsync();
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
        {
            // Circuit teardown can surface any of these from JS interop; nothing to clean up.
        }

        _selfRef?.Dispose();
    }
}
