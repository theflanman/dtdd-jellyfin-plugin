using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.DoesTheDogDie.Configuration;

/// <summary>
/// Represents the cached trigger categories and topics from DTDD.
/// </summary>
public class TriggerCache
{
    /// <summary>
    /// Gets or sets when the cache was last refreshed.
    /// </summary>
    [JsonPropertyName("lastRefreshed")]
    public DateTime LastRefreshed { get; set; }

    /// <summary>
    /// Gets or sets the list of cached categories.
    /// </summary>
    [JsonPropertyName("categories")]
    public List<CachedCategory> Categories { get; set; } = new();
}
