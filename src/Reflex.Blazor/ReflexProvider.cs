using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Reflex.Blazor;

/// <summary>
/// Root component that wires every registered <see cref="IStore"/> into the <see cref="ReflexStore"/>
/// manager and (on first render) connects the Redux DevTools bridge. Place it once near the root of
/// your app, wrapping your routes:
/// <code>&lt;ReflexProvider&gt;&lt;Router ... /&gt;&lt;/ReflexProvider&gt;</code>
/// </summary>
public sealed class ReflexProvider : ComponentBase, IAsyncDisposable
{
    private ReduxDevToolsConnector? _connector;

    [Inject] private ReflexStore Manager { get; set; } = default!;
    [Inject] private IEnumerable<IStore> Stores { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ReflexOptions Options { get; set; } = default!;

    /// <summary>Disables the DevTools connection when set to <c>false</c>. Defaults to <c>true</c>.</summary>
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
