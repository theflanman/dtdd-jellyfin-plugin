using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.DoesTheDogDie.Api.Models;

/// <summary>
/// Represents a search response from DoesTheDogDie API.
/// </summary>
public class DtddSearchResponse
{
    /// <summary>
    /// Gets or sets the list of matching media items.
    /// </summary>
    [JsonPropertyName("items")]
    public List<DtddMediaItem> Items { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of matching topics.
    /// </summary>
    [JsonPropertyName("topics")]
    public List<DtddTopic> Topics { get; set; } = new();
}
