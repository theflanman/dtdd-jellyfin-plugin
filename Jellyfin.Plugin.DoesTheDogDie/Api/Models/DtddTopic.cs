using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.DoesTheDogDie.Api.Models;

/// <summary>
/// Represents a trigger/topic in DoesTheDogDie.
/// </summary>
public class DtddTopic
{
    /// <summary>
    /// Gets or sets the topic ID.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the trigger name (e.g., "a dog dies").
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the negative form (e.g., "no dogs die").
    /// </summary>
    [JsonPropertyName("notName")]
    public string? NotName { get; set; }

    /// <summary>
    /// Gets or sets the survival form (e.g., "the dog survives").
    /// </summary>
    [JsonPropertyName("survivesName")]
    public string? SurvivesName { get; set; }

    /// <summary>
    /// Gets or sets the question form (e.g., "Does the dog die").
    /// </summary>
    [JsonPropertyName("doesName")]
    public string? DoesName { get; set; }

    /// <summary>
    /// Gets or sets the detailed description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this topic contains spoilers.
    /// </summary>
    [JsonPropertyName("isSpoiler")]
    public bool IsSpoiler { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this topic is sensitive.
    /// </summary>
    [JsonPropertyName("isSensitive")]
    public bool IsSensitive { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this topic is visible.
    /// </summary>
    [JsonPropertyName("isVisible")]
    public bool IsVisible { get; set; }

    /// <summary>
    /// Gets or sets the short description.
    /// </summary>
    [JsonPropertyName("smmwDescription")]
    public string? SmmwDescription { get; set; }

    /// <summary>
    /// Gets or sets the topic category ID.
    /// </summary>
    [JsonPropertyName("TopicCategoryId")]
    public int? TopicCategoryId { get; set; }

    /// <summary>
    /// Gets or sets the topic category.
    /// </summary>
    [JsonPropertyName("TopicCategory")]
    public DtddTopicCategory? TopicCategory { get; set; }
}
