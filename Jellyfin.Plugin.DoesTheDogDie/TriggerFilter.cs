using System.Collections.Generic;
using Jellyfin.Plugin.DoesTheDogDie.Api.Models;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;

namespace Jellyfin.Plugin.DoesTheDogDie;

/// <summary>
/// Helper class for filtering triggers based on user configuration.
/// </summary>
public static class TriggerFilter
{
    /// <summary>
    /// Determines if a trigger should be included based on configuration settings.
    /// </summary>
    /// <param name="trigger">The trigger to check.</param>
    /// <param name="config">The plugin configuration.</param>
    /// <returns>True if the trigger should be included, false otherwise.</returns>
    public static bool ShouldIncludeTrigger(DtddTopicItemStat trigger, PluginConfiguration config)
    {
        // Master switch - include everything
        if (config.ShowAllTriggers)
        {
            return true;
        }

        // No categories selected = include everything (with UI warning)
        if (config.EnabledCategoryIds == null || config.EnabledCategoryIds.Count == 0)
        {
            return true;
        }

        var topic = trigger.Topic;
        if (topic == null)
        {
            return false;
        }

        // Get the category ID from topic or directly from the stat
        var categoryId = topic.TopicCategoryId ?? trigger.TopicCategory?.Id;
        if (categoryId == null)
        {
            return false;
        }

        // Category must be enabled
        if (!config.EnabledCategoryIds.Contains(categoryId.Value))
        {
            return false;
        }

        // If no specific topics are selected, include all topics in enabled categories
        if (config.EnabledTopicIds == null || config.EnabledTopicIds.Count == 0)
        {
            return true;
        }

        // Otherwise, topic must be explicitly enabled
        return config.EnabledTopicIds.Contains(topic.Id);
    }

    /// <summary>
    /// Filters a collection of triggers based on configuration settings.
    /// </summary>
    /// <param name="triggers">The triggers to filter.</param>
    /// <param name="config">The plugin configuration.</param>
    /// <returns>Filtered list of triggers.</returns>
    public static IEnumerable<DtddTopicItemStat> FilterTriggers(
        IEnumerable<DtddTopicItemStat> triggers,
        PluginConfiguration config)
    {
        foreach (var trigger in triggers)
        {
            if (ShouldIncludeTrigger(trigger, config))
            {
                yield return trigger;
            }
        }
    }
}
