using System.Collections.Generic;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Services;

namespace Jellyfin.Plugin.DoesTheDogDie;

/// <summary>
/// Helper class for filtering triggers based on user configuration.
/// </summary>
public static class TriggerFilter
{
    /// <summary>
    /// Determines whether a trigger passes the configured category and topic filters.
    /// </summary>
    /// <param name="trigger">The trigger to check.</param>
    /// <param name="config">The plugin configuration.</param>
    /// <returns>True when the trigger should be included.</returns>
    public static bool ShouldInclude(TriggerInfo trigger, PluginConfiguration config)
    {
        if (config.ShowAllTriggers)
        {
            return true;
        }

        if (config.EnabledCategoryIds is null || config.EnabledCategoryIds.Count == 0)
        {
            return true;
        }

        if (!config.EnabledCategoryIds.Contains(trigger.CategoryId))
        {
            return false;
        }

        if (config.EnabledTopicIds is null || config.EnabledTopicIds.Count == 0)
        {
            return true;
        }

        return config.EnabledTopicIds.Contains(trigger.Topic.Id);
    }

    /// <summary>
    /// Filters a sequence of triggers by the configured category and topic selections.
    /// </summary>
    /// <param name="triggers">The triggers to filter.</param>
    /// <param name="config">The plugin configuration.</param>
    /// <returns>The triggers that pass the filter.</returns>
    public static IEnumerable<TriggerInfo> Filter(IEnumerable<TriggerInfo> triggers, PluginConfiguration config)
    {
        foreach (var trigger in triggers)
        {
            if (ShouldInclude(trigger, config))
            {
                yield return trigger;
            }
        }
    }
}
