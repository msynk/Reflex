using Microsoft.JSInterop;

namespace Blex.Blazor;

/// <summary>
/// Which Web Storage area a <see cref="BrowserStorageBlex"/> instance targets.
/// </summary>
public enum BrowserStorageKindBlex
{
    /// <summary>Persists across browser sessions (<c>window.localStorage</c>).</summary>
    Local,

    /// <summary>Cleared when the tab/session ends (<c>window.sessionStorage</c>).</summary>
    Session,
}
