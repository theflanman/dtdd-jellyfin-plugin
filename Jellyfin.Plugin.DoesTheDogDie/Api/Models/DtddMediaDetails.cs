using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.DoesTheDogDie.Api.Models;

/// <summary>
/// Represents detailed media information including trigger data from DoesTheDogDie.
/// </summary>
public class DtddMediaDetails
{
    /// <summary>
    /// Gets or sets the media item information.
    /// </summary>
    [JsonPropertyName("item")]
    public DtddMediaItem Item { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of trigger statistics for this media.
    /// </summary>
    [JsonPropertyName("topicItemStats")]
    public List<DtddTopicItemStat> TopicItemStats { get; set; } = new();

    /// <summary>
    /// Gets triggers that have positive votes (trigger applies).
    /// </summary>
    /// <param name="minVotes">Minimum total votes required.</param>
    /// <returns>Filtered list of positive triggers.</returns>
    public IEnumerable<DtddTopicItemStat> GetPositiveTriggers(int minVotes = 0)
    {
        return TopicItemStats
            .Where(t => t.IsPositive && t.TotalVotes >= minVotes)
            .OrderByDescending(t => t.YesSum);
    }

    /// <summary>
    /// Gets triggers that have negative votes (trigger does not apply).
    /// </summary>
    /// <param name="minVotes">Minimum total votes required.</param>
    /// <returns>Filtered list of negative triggers.</returns>
    public IEnumerable<DtddTopicItemStat> GetNegativeTriggers(int minVotes = 0)
    {
        return TopicItemStats
            .Where(t => !t.IsPositive && t.TotalVotes >= minVotes)
            .OrderByDescending(t => t.NoSum);
    }
}
