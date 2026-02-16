using System;
using System.Linq;
using Jellyfin.Plugin.DoesTheDogDie.Api.Models;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.DoesTheDogDie;

/// <summary>
/// Shared helper for managing DTDD warning tags on Jellyfin items.
/// Consolidates the strip-then-rebuild tag logic used by providers,
/// the scheduled refresh task, and the library scan service.
/// </summary>
public static class TagHelper
{
    /// <summary>
    /// Strips all existing DTDD tags, then rebuilds from the provided details
    /// using the current filter configuration. Preserves non-DTDD tags.
    /// </summary>
    /// <param name="item">The Jellyfin item to update.</param>
    /// <param name="details">The DTDD media details containing trigger data.</param>
    /// <param name="config">The current plugin configuration.</param>
    /// <returns>True if the tag set changed, false otherwise.</returns>
    public static bool UpdateWarningTags(BaseItem item, DtddMediaDetails details, PluginConfiguration config)
    {
        var originalTagCount = item.Tags.Length;

        // Strip all existing DTDD tags (those starting with our prefixes)
        var existingTags = item.Tags
            .Where(t => !t.StartsWith(config.TagPrefix, StringComparison.OrdinalIgnoreCase) &&
                        !t.StartsWith(config.SafeTagPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var nonDtddTagCount = existingTags.Count;

        // Add positive triggers (content warnings)
        var positiveTriggers = TriggerFilter.FilterTriggers(
            details.GetPositiveTriggers(config.MinVotesThreshold),
            config);

        foreach (var trigger in positiveTriggers)
        {
            if (trigger.Topic == null)
            {
                continue;
            }

            var tagName = $"{config.TagPrefix} {trigger.Topic.Name}";
            if (!existingTags.Contains(tagName, StringComparer.OrdinalIgnoreCase))
            {
                existingTags.Add(tagName);
            }
        }

        // Add negative triggers (safe confirmations)
        var negativeTriggers = TriggerFilter.FilterTriggers(
            details.GetNegativeTriggers(config.MinVotesThreshold),
            config);

        foreach (var trigger in negativeTriggers)
        {
            if (trigger.Topic == null)
            {
                continue;
            }

            var tagName = $"{config.SafeTagPrefix} {trigger.Topic.Name}";
            if (!existingTags.Contains(tagName, StringComparer.OrdinalIgnoreCase))
            {
                existingTags.Add(tagName);
            }
        }

        item.Tags = existingTags.ToArray();

        // Check if tags actually changed
        return existingTags.Count != originalTagCount ||
               (originalTagCount - nonDtddTagCount) != (existingTags.Count - nonDtddTagCount);
    }

    /// <summary>
    /// Removes all DTDD-prefixed tags from the item, preserving non-DTDD tags.
    /// </summary>
    /// <param name="item">The Jellyfin item to clean.</param>
    /// <param name="config">The current plugin configuration.</param>
    /// <returns>True if any tags were removed, false otherwise.</returns>
    public static bool RemoveDtddTags(BaseItem item, PluginConfiguration config)
    {
        var originalCount = item.Tags.Length;

        var cleanedTags = item.Tags
            .Where(t => !t.StartsWith(config.TagPrefix, StringComparison.OrdinalIgnoreCase) &&
                        !t.StartsWith(config.SafeTagPrefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        item.Tags = cleanedTags;

        return cleanedTags.Length != originalCount;
    }
}
