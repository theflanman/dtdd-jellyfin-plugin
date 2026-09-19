using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DoesTheDogDie.Api;
using DoesTheDogDie.Statistics;
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
    /// Renders a trigger summary grouped by verdict, with vote counts, credible intervals and
    /// optionally the top user comment per trigger.
    /// </summary>
    /// <param name="triggers">The triggers for the item.</param>
    /// <param name="config">The plugin configuration.</param>
    /// <param name="comments">Top comment per topic id; empty when comment injection is off.</param>
    /// <returns>The formatted summary, or an empty string when nothing survives filtering.</returns>
    public virtual string FormatTriggerSummary(
        IReadOnlyList<TriggerInfo> triggers,
        PluginConfiguration config,
        IReadOnlyDictionary<int, string>? comments = null)
    {
        var included = TriggerFilter.Filter(triggers, config).ToList();
        if (included.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        AppendGroup(sb, "Content warnings", included, TriggerVerdict.LikelyPresent, comments);
        AppendGroup(sb, "Possible", included, TriggerVerdict.Uncertain, comments);
        AppendGroup(sb, "Reported safe", included, TriggerVerdict.LikelyAbsent, comments);

        if (sb.Length == 0)
        {
            return string.Empty;
        }

        sb.Append('\n');
        sb.Append(CultureInfo.InvariantCulture, $"{DtddAttribution.Phrase} — {DtddAttribution.Url}");

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

    private static void AppendGroup(
        StringBuilder sb,
        string heading,
        IReadOnlyList<TriggerInfo> triggers,
        TriggerVerdict verdict,
        IReadOnlyDictionary<int, string>? comments)
    {
        var matching = triggers.Where(t => t.Verdict == verdict).ToList();
        if (matching.Count == 0)
        {
            return;
        }

        sb.Append(heading).Append(": ");
        sb.AppendJoin(", ", matching.Select(FormatTrigger));
        sb.Append('\n');

        if (comments is not null)
        {
            foreach (var trigger in matching)
            {
                if (comments.TryGetValue(trigger.Topic.Id, out var comment))
                {
                    sb.Append("  • ").Append(trigger.Topic.Name).Append(": ").Append(comment).Append('\n');
                }
            }
        }
    }

    private static string FormatTrigger(TriggerInfo trigger)
    {
        var low = (int)Math.Round(trigger.Confidence.Lower * 100);
        var high = (int)Math.Round(trigger.Confidence.Upper * 100);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{trigger.Topic.Name} ({trigger.YesSum}/{trigger.TotalVotes}, {low}–{high}%)");
    }
}
