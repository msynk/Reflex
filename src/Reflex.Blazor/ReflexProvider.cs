using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
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

    [Inject] private ReflexManager Manager { get; set; } = default!;
    [Inject] private IEnumerable<IStore> Stores { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ReflexOptions Options { get; set; } = default!;

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

    /// <summary>The application content rendered inside the provider.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        foreach (var store in Stores)
            Manager.Register(store);
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
        if (_connector is not null)
            await _connector.DisposeAsync();
    }
}
