using System;
using System.Globalization;
using Jellyfin.Plugin.DoesTheDogDie.Api.Models;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;

namespace Jellyfin.Plugin.DoesTheDogDie;

/// <summary>
/// Helper class for building tag names from triggers.
/// </summary>
public static class TriggerTagFormatter
{
    /// <summary>
    /// Builds the tag name for a trigger, optionally appending the confidence
    /// percentage (rounded to the nearest 5%) when enabled in configuration.
    /// </summary>
    /// <param name="prefix">The tag prefix (e.g. "CW:" or "Safe:").</param>
    /// <param name="trigger">The trigger to format.</param>
    /// <param name="config">The plugin configuration.</param>
    /// <returns>The formatted tag name, or null if the trigger has no topic.</returns>
    public static string? FormatTagName(string prefix, DtddTopicItemStat trigger, PluginConfiguration config)
    {
        if (trigger.Topic == null)
        {
            return null;
        }

        var tagName = $"{prefix} {trigger.Topic.Name}";

        if (config.ShowConfidenceInTags)
        {
            var confidence = TriggerFilter.GetConfidence(trigger);
            var percent = (int)(Math.Round(confidence * 100 / 5.0) * 5);
            tagName += string.Create(CultureInfo.InvariantCulture, $" ({percent}%)");
        }

        return tagName;
    }
}
