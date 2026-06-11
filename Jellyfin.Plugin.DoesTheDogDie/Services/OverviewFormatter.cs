using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Jellyfin.Plugin.DoesTheDogDie.Api.Models;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;

namespace Jellyfin.Plugin.DoesTheDogDie.Services;

/// <summary>
/// Service for formatting DTDD trigger data for injection into item Overview fields.
/// </summary>
public class OverviewFormatter
{
    /// <summary>
    /// The start marker for DTDD content in Overview fields.
    /// </summary>
    public const string DtddStartMarker = "<!-- DTDD_START -->";

    /// <summary>
    /// The end marker for DTDD content in Overview fields.
    /// </summary>
    public const string DtddEndMarker = "<!-- DTDD_END -->";

    /// <summary>
    /// Formats trigger data from DTDD into a summary suitable for appending to an Overview.
    /// </summary>
    /// <param name="details">The media details containing trigger information.</param>
    /// <param name="config">The plugin configuration.</param>
    /// <returns>Formatted trigger summary text, or empty string if no triggers to display.</returns>
    public virtual string FormatTriggerSummary(DtddMediaDetails details, PluginConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(config);

        var sb = new StringBuilder();

        var positiveTriggers = TriggerFilter.FilterTriggers(
            details.GetPositiveTriggers(config.MinVotesThreshold),
            config).ToList();

        var negativeTriggers = TriggerFilter.FilterTriggers(
            details.GetNegativeTriggers(config.MinVotesThreshold),
            config).ToList();

        if (positiveTriggers.Count == 0 && negativeTriggers.Count == 0)
        {
            return string.Empty;
        }

        sb.AppendLine();
        sb.AppendLine("**Content Warnings** (via DoesTheDogDie)");
        sb.AppendLine();

        // Add positive triggers (warnings)
        foreach (var trigger in positiveTriggers)
        {
            if (trigger.Topic == null)
            {
                continue;
            }

            var line = string.Format(
                CultureInfo.InvariantCulture,
                "⚠️ {0} ({1} yes / {2} no)",
                CapitalizeFirst(trigger.Topic.Name),
                trigger.YesSum,
                trigger.NoSum);
            sb.AppendLine(line);

            // Add comment if configured
            if (config.IncludeTopComment && !string.IsNullOrWhiteSpace(trigger.Comment))
            {
                var comment = FormatComment(trigger, config);
                if (!string.IsNullOrEmpty(comment))
                {
                    sb.AppendLine(comment);
                }
            }

            sb.AppendLine();
        }

        // Add negative triggers (safe confirmations)
        foreach (var trigger in negativeTriggers)
        {
            if (trigger.Topic == null)
            {
                continue;
            }

            var line = string.Format(
                CultureInfo.InvariantCulture,
                "✓ Safe: {0} ({1} yes / {2} no)",
                CapitalizeFirst(trigger.Topic.Name),
                trigger.YesSum,
                trigger.NoSum);
            sb.AppendLine(line);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Appends DTDD content to an existing Overview, replacing any existing DTDD section.
    /// </summary>
    /// <param name="existingOverview">The existing Overview text, or null if none.</param>
    /// <param name="dtddContent">The DTDD content to append.</param>
    /// <returns>The updated Overview text.</returns>
    public virtual string AppendToOverview(string? existingOverview, string dtddContent)
    {
        if (string.IsNullOrWhiteSpace(dtddContent))
        {
            return existingOverview ?? string.Empty;
        }

        // Wrap content with markers
        var wrappedContent = $"{DtddStartMarker}{dtddContent}\n{DtddEndMarker}";

        if (string.IsNullOrWhiteSpace(existingOverview))
        {
            return wrappedContent;
        }

        // Remove existing DTDD section if present
        var cleanedOverview = RemoveDtddSection(existingOverview);

        // Append new content
        return cleanedOverview.TrimEnd() + "\n\n" + wrappedContent;
    }

    /// <summary>
    /// Removes the DTDD section from an Overview.
    /// </summary>
    /// <param name="overview">The Overview text.</param>
    /// <returns>The Overview with DTDD section removed.</returns>
    public virtual string RemoveDtddSection(string overview)
    {
        if (string.IsNullOrWhiteSpace(overview))
        {
            return overview ?? string.Empty;
        }

        var startIndex = overview.IndexOf(DtddStartMarker, StringComparison.Ordinal);
        var endIndex = overview.IndexOf(DtddEndMarker, StringComparison.Ordinal);

        if (startIndex == -1 || endIndex == -1 || endIndex < startIndex)
        {
            return overview;
        }

        var before = overview.Substring(0, startIndex);
        var after = overview.Substring(endIndex + DtddEndMarker.Length);

        return (before.TrimEnd() + after.TrimStart()).Trim();
    }

    /// <summary>
    /// Checks if an Overview contains a DTDD section.
    /// </summary>
    /// <param name="overview">The Overview text.</param>
    /// <returns>True if the Overview contains a DTDD section.</returns>
    public virtual bool HasDtddSection(string? overview)
    {
        if (string.IsNullOrWhiteSpace(overview))
        {
            return false;
        }

        return overview.Contains(DtddStartMarker, StringComparison.Ordinal)
            && overview.Contains(DtddEndMarker, StringComparison.Ordinal);
    }

    private static string FormatComment(DtddTopicItemStat trigger, PluginConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(trigger.Comment))
        {
            return string.Empty;
        }

        // Check if comment is a spoiler and should be hidden
        if (config.HideSpoilerComments && trigger.Topic?.IsSpoiler == true)
        {
            return string.Empty;
        }

        var comment = trigger.Comment.Trim();

        // Truncate if necessary
        if (comment.Length > config.MaxCommentLength)
        {
            comment = comment.Substring(0, config.MaxCommentLength).TrimEnd() + "...";
        }

        // Format with author if available
        if (!string.IsNullOrWhiteSpace(trigger.Username))
        {
            return $"  💬 \"{comment}\" - {trigger.Username}";
        }

        return $"  💬 \"{comment}\"";
    }

    private static string CapitalizeFirst(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return char.ToUpper(text[0], CultureInfo.InvariantCulture) + text.Substring(1);
    }
}
