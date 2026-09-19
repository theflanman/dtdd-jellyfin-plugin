using System;
using System.Globalization;
using DoesTheDogDie.Statistics;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Services;

namespace Jellyfin.Plugin.DoesTheDogDie;

/// <summary>
/// Helper class for building tag names from triggers.
/// </summary>
public static class TriggerTagFormatter
{
    /// <summary>
    /// Builds the tag name for a trigger, or null when the verdict is uncertain.
    /// </summary>
    /// <param name="trigger">The trigger to format.</param>
    /// <param name="config">The plugin configuration.</param>
    /// <returns>The tag name, or null when the trigger should not be tagged.</returns>
    public static string? FormatTagName(TriggerInfo trigger, PluginConfiguration config)
    {
        var prefix = trigger.Verdict switch
        {
            TriggerVerdict.LikelyPresent => config.TagPrefix,
            TriggerVerdict.LikelyAbsent => config.SafeTagPrefix,
            _ => null,
        };

        if (prefix is null)
        {
            return null;
        }

        var tagName = $"{prefix} {trigger.Topic.Name}";

        if (config.ShowConfidenceInTags)
        {
            var percent = (int)(Math.Round(trigger.Confidence.ProbabilityPresent * 100 / 5.0) * 5);
            tagName += string.Create(CultureInfo.InvariantCulture, $" ({percent}%)");
        }

        return tagName;
    }
}
