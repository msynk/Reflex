using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Reflex.Blazor;

/// <summary>
/// Root component that wires every registered <see cref="IStore"/> into the <see cref="ReflexManager"/>
/// manager and (on first render) connects the Redux DevTools bridge. Place it once near the root of
/// your app, wrapping your routes:
/// <code>&lt;ReflexProvider&gt;&lt;Router ... /&gt;&lt;/ReflexProvider&gt;</code>
/// </summary>
public sealed class ReflexProvider : ComponentBase, IAsyncDisposable
{
    private ReduxDevToolsConnector? _connector;
    private ComponentStatePersistence? _componentState;

    [Inject] private ReflexManager Manager { get; set; } = default!;
    [Inject] private IEnumerable<IStore> Stores { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ReflexOptions Options { get; set; } = default!;
    [Inject] private IServiceProvider Services { get; set; } = default!;

    /// <summary>
    /// Enables the Redux DevTools connection. Defaults to <c>true</c>.
    /// </summary>
    /// <remarks>
    /// When enabled, the entire application state tree is serialized and exposed to the Redux
    /// DevTools browser extension on every action. This is intended for development only; set
    /// this to <c>false</c> in production (for example, bind it to your host environment) to
    /// avoid leaking application state to an installed extension.
    /// </remarks>
    [Parameter] public bool EnableDevTools { get; set; } = true;

    /// <summary>
    /// Hands prerendered store state to the interactive render via Blazor's
    /// <see cref="PersistentComponentState"/>, avoiding the prerender "double render" flicker.
    /// Defaults to <c>true</c>; has no effect when the framework service is unavailable.
    /// </summary>
    [Parameter] public bool PersistComponentState { get; set; } = true;

    /// <summary>The application content rendered inside the provider.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        foreach (var store in Stores)
            Manager.Register(store);

        // Prerender -> interactive handoff (synchronous, before durable restore).
        if (PersistComponentState)
        {
            var pcs = Services.GetService<PersistentComponentState>();
            if (pcs is not null)
            {
                _componentState = new ComponentStatePersistence(pcs, Stores);
                _componentState.TryRestore();
            }
        }

        // Durable persistence (localStorage/sessionStorage), if configured.
        var persistor = Services.GetService<StatePersistor>();
        if (persistor is not null)
            await persistor.StartAsync();

        // In-app undo/redo, if configured.
        Services.GetService<ReflexHistory>()?.Start();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && EnableDevTools)
        {
            _connector = new ReduxDevToolsConnector(Js);
            await _connector.ConnectAsync(Manager, Options.DevToolsName);
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
        => builder.AddContent(0, ChildContent);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _componentState?.Dispose();

        if (_connector is not null)
            await _connector.DisposeAsync();
    }
}
