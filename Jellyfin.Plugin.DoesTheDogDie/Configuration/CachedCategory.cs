using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.DoesTheDogDie.Configuration;

/// <summary>
/// Represents a cached trigger category.
/// </summary>
public class CachedCategory
{
    /// <summary>
    /// Gets or sets the category ID.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the category name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the topics in this category.
    /// </summary>
    [JsonPropertyName("topics")]
    public List<CachedTopic> Topics { get; set; } = new();
}
