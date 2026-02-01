using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.DoesTheDogDie.Api.Models;

/// <summary>
/// Represents a media type in DoesTheDogDie.
/// </summary>
public class DtddItemType
{
    /// <summary>
    /// Gets or sets the item type ID.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the item type name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
