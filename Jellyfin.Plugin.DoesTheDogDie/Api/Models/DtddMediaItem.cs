using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.DoesTheDogDie.Api.Models;

/// <summary>
/// Represents a media item from DoesTheDogDie search results.
/// </summary>
public class DtddMediaItem
{
    /// <summary>
    /// Gets or sets the DTDD internal ID.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the display title.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the normalized title (lowercase, no articles).
    /// </summary>
    [JsonPropertyName("cleanName")]
    public string? CleanName { get; set; }

    /// <summary>
    /// Gets or sets the alternate name.
    /// </summary>
    [JsonPropertyName("altName")]
    public string? AltName { get; set; }

    /// <summary>
    /// Gets or sets the primary genre.
    /// </summary>
    [JsonPropertyName("genre")]
    public string? Genre { get; set; }

    /// <summary>
    /// Gets or sets the release year.
    /// </summary>
    [JsonPropertyName("releaseYear")]
    public string? ReleaseYear { get; set; }

    /// <summary>
    /// Gets or sets the TMDB ID.
    /// </summary>
    [JsonPropertyName("tmdbId")]
    public int? TmdbId { get; set; }

    /// <summary>
    /// Gets or sets the IMDB ID.
    /// </summary>
    [JsonPropertyName("imdbId")]
    public string? ImdbId { get; set; }

    /// <summary>
    /// Gets or sets the TMDB poster path.
    /// </summary>
    [JsonPropertyName("posterImage")]
    public string? PosterImage { get; set; }

    /// <summary>
    /// Gets or sets the TMDB backdrop path.
    /// </summary>
    [JsonPropertyName("backgroundImage")]
    public string? BackgroundImage { get; set; }

    /// <summary>
    /// Gets or sets the synopsis.
    /// </summary>
    [JsonPropertyName("overview")]
    public string? Overview { get; set; }

    /// <summary>
    /// Gets or sets the total number of trigger votes.
    /// </summary>
    [JsonPropertyName("numRatings")]
    public int NumRatings { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the content is verified.
    /// </summary>
    [JsonPropertyName("verified")]
    [JsonConverter(typeof(BooleanOrIntConverter))]
    public bool Verified { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the content is staff verified.
    /// </summary>
    [JsonPropertyName("staffVerified")]
    [JsonConverter(typeof(BooleanOrIntConverter))]
    public bool StaffVerified { get; set; }

    /// <summary>
    /// Gets or sets the media type ID.
    /// </summary>
    [JsonPropertyName("ItemTypeId")]
    public int ItemTypeId { get; set; }

    /// <summary>
    /// Gets or sets the media type details.
    /// </summary>
    [JsonPropertyName("itemType")]
    public DtddItemType? ItemType { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
}
