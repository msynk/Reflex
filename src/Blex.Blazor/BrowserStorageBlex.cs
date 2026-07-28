using Microsoft.JSInterop;

namespace Blex.Blazor;

/// <summary>
/// <see cref="IStorageBlex"/> backed by the browser's <c>localStorage</c>/<c>sessionStorage</c>
/// via a tiny JS bridge. Used by Blex persistence to survive page reloads in Blazor WebAssembly.
/// </summary>
public sealed class BrowserStorageBlex : IStorageBlex, IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly string _area;
    private IJSObjectReference? _module;

    /// <summary>Creates a storage instance over the given Web Storage area.</summary>
    public BrowserStorageBlex(IJSRuntime js, BrowserStorageKindBlex kind = BrowserStorageKindBlex.Local)
    {
        _js = js;
        _area = kind == BrowserStorageKindBlex.Session ? "session" : "local";
    }

    private async ValueTask<IJSObjectReference> ModuleAsync(CancellationToken ct)
        => _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", ct, "./_content/Blex.Blazor/blex-storage.js");

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
