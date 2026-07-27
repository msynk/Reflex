using Microsoft.JSInterop;

namespace Reflex.Blazor;

/// <summary>
/// Which Web Storage area a <see cref="BrowserStorage"/> instance targets.
/// </summary>
public enum BrowserStorageKind
{
    /// <summary>Persists across browser sessions (<c>window.localStorage</c>).</summary>
    Local,

    /// <summary>Cleared when the tab/session ends (<c>window.sessionStorage</c>).</summary>
    Session,
}

/// <summary>
/// <see cref="IReflexStorage"/> backed by the browser's <c>localStorage</c>/<c>sessionStorage</c>
/// via a tiny JS bridge. Used by Reflex persistence to survive page reloads in Blazor WebAssembly.
/// </summary>
public sealed class BrowserStorage : IReflexStorage, IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly string _area;
    private IJSObjectReference? _module;

    /// <summary>Creates a storage instance over the given Web Storage area.</summary>
    public BrowserStorage(IJSRuntime js, BrowserStorageKind kind = BrowserStorageKind.Local)
    {
        _js = js;
        _area = kind == BrowserStorageKind.Session ? "session" : "local";
    }

    private async ValueTask<IJSObjectReference> ModuleAsync(CancellationToken ct)
        => _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", ct, "./_content/Reflex.Blazor/reflex-storage.js");

    /// <inheritdoc />
    public async ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var module = await ModuleAsync(cancellationToken);
        return await module.InvokeAsync<string?>("get", cancellationToken, _area, key);
    }

    /// <inheritdoc />
    public async ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var module = await ModuleAsync(cancellationToken);
        await module.InvokeVoidAsync("set", cancellationToken, _area, key, value);
    }

    /// <inheritdoc />
    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var module = await ModuleAsync(cancellationToken);
        await module.InvokeVoidAsync("remove", cancellationToken, _area, key);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_module is not null)
                await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // Circuit gone; nothing to clean up.
        }
    }
}
