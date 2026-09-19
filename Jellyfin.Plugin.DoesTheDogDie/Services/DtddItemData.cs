using System;
using System.Collections.Generic;
using DoesTheDogDie;

namespace Jellyfin.Plugin.DoesTheDogDie.Services;

/// <summary>
/// Everything the plugin needs about one DtDD item, resolved and ready to apply to a Jellyfin item.
/// </summary>
/// <param name="ItemId">The DtDD item id.</param>
/// <param name="Triggers">The item's triggers, joined to taxonomy and scored.</param>
/// <param name="Source">Whether the data came from the API, the cache, or a stale cache entry.</param>
/// <param name="FetchedAt">When the underlying data was last fetched from the DtDD API.</param>
public sealed record DtddItemData(
    int ItemId,
    IReadOnlyList<TriggerInfo> Triggers,
    ResultSource Source,
    DateTimeOffset FetchedAt)
{
    /// <summary>Gets the top comment per topic id, empty when comment injection is disabled.</summary>
    public IReadOnlyDictionary<int, string> Comments { get; init; } =
        new Dictionary<int, string>();
}
