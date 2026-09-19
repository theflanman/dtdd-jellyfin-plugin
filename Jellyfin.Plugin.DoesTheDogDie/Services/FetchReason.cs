namespace Jellyfin.Plugin.DoesTheDogDie.Services;

/// <summary>
/// The kind of Jellyfin item a DtDD fetch is being made for.
/// </summary>
public enum DtddItemKind
{
    /// <summary>A movie.</summary>
    Movie,

    /// <summary>A TV series.</summary>
    Series,

    /// <summary>A season of a TV series.</summary>
    Season,

    /// <summary>An episode of a TV series.</summary>
    Episode,
}

/// <summary>
/// A priority hint describing why a fetch is being made. Ignored today; becomes a pass-through
/// to the client library's prioritized work queue when that lands.
/// </summary>
/// <param name="Kind">The kind of item being fetched.</param>
/// <param name="IsRefresh">True when the item already has DtDD data being refreshed.</param>
/// <param name="IsUserInitiated">True when a user action triggered the fetch, rather than a background scan.</param>
public readonly record struct FetchReason(DtddItemKind Kind, bool IsRefresh, bool IsUserInitiated);
