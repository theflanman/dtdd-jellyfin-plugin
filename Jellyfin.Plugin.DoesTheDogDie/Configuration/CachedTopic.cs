using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.DoesTheDogDie.Configuration;

/// <summary>
/// Represents a cached trigger topic.
/// </summary>
public class CachedTopic
{
    /// <summary>
    /// Gets or sets the topic ID.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the topic name (e.g., "a dog dies").
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the question form (e.g., "Does the dog die").
    /// </summary>
    [JsonPropertyName("doesName")]
    public string? DoesName { get; set; }
}
