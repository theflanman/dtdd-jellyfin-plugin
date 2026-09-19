using DoesTheDogDie.Api;
using DoesTheDogDie.Statistics;

namespace Jellyfin.Plugin.DoesTheDogDie.Services;

/// <summary>
/// A single DtDD topic as it applies to one item, joined to its taxonomy entry and confidence assessment.
/// </summary>
/// <param name="Topic">The topic, from the cached taxonomy.</param>
/// <param name="YesSum">Votes saying the trigger is present.</param>
/// <param name="NoSum">Votes saying the trigger is absent.</param>
/// <param name="Confidence">The confidence assessment derived from the vote counts.</param>
public sealed record TriggerInfo(Topic Topic, int YesSum, int NoSum, TriggerConfidence Confidence)
{
    /// <summary>Gets the topic's category id.</summary>
    public int CategoryId => Topic.TopicCategoryId;

    /// <summary>Gets the total number of votes cast.</summary>
    public int TotalVotes => YesSum + NoSum;

    /// <summary>Gets the verdict for this trigger.</summary>
    public TriggerVerdict Verdict => Confidence.Verdict;
}
