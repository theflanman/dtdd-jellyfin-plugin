using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.DoesTheDogDie.Api.Models;

/// <summary>
/// Represents trigger statistics for a specific media item.
/// </summary>
public class DtddTopicItemStat
{
    /// <summary>
    /// Gets or sets the unique ID for this topic-item pair.
    /// </summary>
    [JsonPropertyName("topicItemId")]
    public int TopicItemId { get; set; }

    /// <summary>
    /// Gets or sets the number of "Yes" votes.
    /// </summary>
    [JsonPropertyName("yesSum")]
    public int YesSum { get; set; }

    /// <summary>
    /// Gets or sets the number of "No" votes.
    /// </summary>
    [JsonPropertyName("noSum")]
    public int NoSum { get; set; }

    /// <summary>
    /// Gets or sets the number of comments.
    /// </summary>
    [JsonPropertyName("numComments")]
    public int NumComments { get; set; }

    /// <summary>
    /// Gets or sets the topic ID.
    /// </summary>
    [JsonPropertyName("TopicId")]
    public int TopicId { get; set; }

    /// <summary>
    /// Gets or sets the media item ID.
    /// </summary>
    [JsonPropertyName("ItemId")]
    public int ItemId { get; set; }

    /// <summary>
    /// Gets or sets the top-voted comment.
    /// </summary>
    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    /// <summary>
    /// Gets or sets the comment author username.
    /// </summary>
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the topic details.
    /// </summary>
    [JsonPropertyName("topic")]
    public DtddTopic? Topic { get; set; }

    /// <summary>
    /// Gets or sets the topic category.
    /// </summary>
    [JsonPropertyName("TopicCategory")]
    public DtddTopicCategory? TopicCategory { get; set; }

    /// <summary>
    /// Gets or sets the URL-friendly topic name.
    /// </summary>
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    /// <summary>
    /// Gets the total number of votes.
    /// </summary>
    [JsonIgnore]
    public int TotalVotes => YesSum + NoSum;

    /// <summary>
    /// Gets a value indicating whether the trigger applies (more yes than no votes).
    /// </summary>
    [JsonIgnore]
    public bool IsPositive => YesSum > NoSum;

    /// <summary>
    /// Gets the confidence percentage (0-100) for the majority vote.
    /// </summary>
    [JsonIgnore]
    public double Confidence => TotalVotes > 0
        ? (double)Math.Max(YesSum, NoSum) / TotalVotes * 100
        : 0;
}
