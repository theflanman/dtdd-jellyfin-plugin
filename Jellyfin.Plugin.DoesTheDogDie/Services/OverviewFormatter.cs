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

        var groups = new StringBuilder();
        AppendGroup(groups, "Content warnings", included, TriggerVerdict.LikelyPresent, comments);
        AppendGroup(groups, "Possible", included, TriggerVerdict.Uncertain, comments);
        AppendGroup(groups, "Reported Safe", included, TriggerVerdict.LikelyAbsent, comments);

        if (groups.Length == 0)
        {
            return string.Empty;
        }

        // Kept at H3-or-lower so the injected section never outranks the item's own tagline,
        // which Jellyfin renders as H3.
        var sb = new StringBuilder();
        sb.Append("### Content warnings\n\n");
        sb.Append(CultureInfo.InvariantCulture, $"{DtddAttribution.Phrase} — {DtddAttribution.Url}\n");
        sb.Append(groups);

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

        // Wrap content with markers. The blank line after DtddStartMarker matters: without it, the
        // leading "#### Content warnings" heading sits glued to the "<!-- DTDD_START -->" HTML
        // comment on the same line, so the Markdown parser never sees it at the start of a block
        // and renders it as literal text instead of a heading.
        var wrappedContent = $"{DtddStartMarker}\n\n{dtddContent}\n{DtddEndMarker}";

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

        sb.Append('\n').Append("#### ").Append(heading).Append('\n');

        foreach (var trigger in matching)
        {
            sb.Append("* ").Append(FormatTrigger(trigger)).Append('\n');

            if (comments is not null && comments.TryGetValue(trigger.Topic.Id, out var comment))
            {
                sb.Append("  * ").Append(FlattenComment(comment)).Append('\n');
            }
        }
    }

    /// <summary>
    /// Collapses embedded line breaks in a comment to spaces. A raw DTDD comment can contain
    /// newlines, which would otherwise split it across multiple lines mid-list-item and break
    /// the surrounding Markdown list (and, with it, every heading level that follows).
    /// </summary>
    private static string FlattenComment(string comment) =>
        string.Join(' ', comment.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string FormatTrigger(TriggerInfo trigger)
    {
        var low = (int)Math.Round(trigger.Confidence.Lower * 100);
        var high = (int)Math.Round(trigger.Confidence.Upper * 100);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{trigger.Topic.Name} ({trigger.YesSum}/{trigger.TotalVotes}, {low}–{high}%)");
    }
}
