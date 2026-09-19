using System;
using System.Collections.Generic;
using DoesTheDogDie.Api;
using DoesTheDogDie.Statistics;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Services;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Services;

public class OverviewFormatterVerdictTests
{
    private readonly OverviewFormatter _formatter = new();

    private static TriggerInfo Trigger(string name, int yes, int no, int topicId = 201) =>
        new(
            new Topic { Id = topicId, Name = name, TopicCategoryId = 3 },
            yes,
            no,
            TriggerConfidence.Compute(yes, no));

    private static PluginConfiguration Config() => new() { ShowAllTriggers = true };

    [Fact]
    public void FormatTriggerSummary_GroupsLikelyPresentUnderContentWarnings()
    {
        var summary = _formatter.FormatTriggerSummary(new List<TriggerInfo> { Trigger("a dog dies", 40, 1) }, Config());

        Assert.Contains("Content warnings: a dog dies", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatTriggerSummary_GroupsUncertainUnderPossible()
    {
        var summary = _formatter.FormatTriggerSummary(new List<TriggerInfo> { Trigger("a dog dies", 4, 3) }, Config());

        Assert.Contains("Possible: a dog dies", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatTriggerSummary_GroupsLikelyAbsentUnderReportedSafe()
    {
        var summary = _formatter.FormatTriggerSummary(new List<TriggerInfo> { Trigger("a dog dies", 1, 40) }, Config());

        Assert.Contains("Reported safe: a dog dies", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatTriggerSummary_IncludesVoteCountsAndInterval()
    {
        var summary = _formatter.FormatTriggerSummary(new List<TriggerInfo> { Trigger("a dog dies", 18, 2) }, Config());

        Assert.Contains("(18/20, ", summary, StringComparison.Ordinal);
        Assert.Contains("–", summary, StringComparison.Ordinal); // en dash between bounds
        Assert.Contains("%)", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatTriggerSummary_OmitsEmptyGroups()
    {
        var summary = _formatter.FormatTriggerSummary(new List<TriggerInfo> { Trigger("a dog dies", 40, 1) }, Config());

        Assert.DoesNotContain("Possible:", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Reported safe:", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatTriggerSummary_AlwaysIncludesAttribution()
    {
        var summary = _formatter.FormatTriggerSummary(new List<TriggerInfo> { Trigger("a dog dies", 40, 1) }, Config());

        Assert.Contains(DtddAttribution.Phrase, summary, StringComparison.Ordinal);
        Assert.Contains(DtddAttribution.Url, summary, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatTriggerSummary_ReturnsEmpty_WhenNoTriggersSurviveFiltering()
    {
        var config = new PluginConfiguration
        {
            ShowAllTriggers = false,
            EnabledCategoryIds = new List<int> { 99 },
        };

        Assert.Equal(string.Empty, _formatter.FormatTriggerSummary(new List<TriggerInfo> { Trigger("a dog dies", 40, 1) }, config));
    }

    [Fact]
    public void AppendToOverview_ReplacesExistingSectionInPlace()
    {
        var first = _formatter.AppendToOverview("Base plot.", "FIRST");
        var second = _formatter.AppendToOverview(first, "SECOND");

        Assert.Contains("SECOND", second, StringComparison.Ordinal);
        Assert.DoesNotContain("FIRST", second, StringComparison.Ordinal);
        Assert.Contains("Base plot.", second, StringComparison.Ordinal);
    }

    [Fact]
    public void AppendToOverview_NullOverview_ReturnsJustDtdd()
    {
        var dtddContent = "Test content";

        var result = _formatter.AppendToOverview(null, dtddContent);

        Assert.Contains(OverviewFormatter.DtddStartMarker, result, StringComparison.Ordinal);
        Assert.Contains(OverviewFormatter.DtddEndMarker, result, StringComparison.Ordinal);
        Assert.Contains(dtddContent, result, StringComparison.Ordinal);
    }

    [Fact]
    public void AppendToOverview_EmptyOverview_ReturnsJustDtdd()
    {
        var dtddContent = "Test content";

        var result = _formatter.AppendToOverview(string.Empty, dtddContent);

        Assert.Contains(OverviewFormatter.DtddStartMarker, result, StringComparison.Ordinal);
        Assert.Contains(OverviewFormatter.DtddEndMarker, result, StringComparison.Ordinal);
        Assert.Contains(dtddContent, result, StringComparison.Ordinal);
    }

    [Fact]
    public void AppendToOverview_ExistingOverview_Appends()
    {
        var existingOverview = "This is the original movie description.";
        var dtddContent = "Test content";

        var result = _formatter.AppendToOverview(existingOverview, dtddContent);

        Assert.StartsWith("This is the original movie description.", result, StringComparison.Ordinal);
        Assert.Contains(OverviewFormatter.DtddStartMarker, result, StringComparison.Ordinal);
        Assert.Contains(OverviewFormatter.DtddEndMarker, result, StringComparison.Ordinal);
        Assert.Contains(dtddContent, result, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoveDtddSection_WithSection_RemovesCleanly()
    {
        var overview = $"Original description.\n\n{OverviewFormatter.DtddStartMarker}\nDTDD content\n{OverviewFormatter.DtddEndMarker}";

        var result = _formatter.RemoveDtddSection(overview);

        Assert.Equal("Original description.", result);
        Assert.DoesNotContain(OverviewFormatter.DtddStartMarker, result, StringComparison.Ordinal);
        Assert.DoesNotContain(OverviewFormatter.DtddEndMarker, result, StringComparison.Ordinal);
        Assert.DoesNotContain("DTDD content", result, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoveDtddSection_WithoutSection_ReturnsOriginal()
    {
        var overview = "Original description without DTDD section.";

        var result = _formatter.RemoveDtddSection(overview);

        Assert.Equal(overview, result);
    }

    [Fact]
    public void RemoveDtddSection_NullOverview_ReturnsEmpty()
    {
        var result = _formatter.RemoveDtddSection(null!);

        Assert.Empty(result);
    }

    [Fact]
    public void RemoveDtddSection_EmptyOverview_ReturnsEmpty()
    {
        var result = _formatter.RemoveDtddSection(string.Empty);

        Assert.Empty(result);
    }

    [Fact]
    public void HasDtddSection_WithSection_ReturnsTrue()
    {
        var overview = $"Description\n\n{OverviewFormatter.DtddStartMarker}\nContent\n{OverviewFormatter.DtddEndMarker}";

        var result = _formatter.HasDtddSection(overview);

        Assert.True(result);
    }

    [Fact]
    public void HasDtddSection_WithoutSection_ReturnsFalse()
    {
        var overview = "Just a plain description.";

        var result = _formatter.HasDtddSection(overview);

        Assert.False(result);
    }

    [Fact]
    public void HasDtddSection_NullOverview_ReturnsFalse()
    {
        var result = _formatter.HasDtddSection(null);

        Assert.False(result);
    }

    [Fact]
    public void HasDtddSection_EmptyOverview_ReturnsFalse()
    {
        var result = _formatter.HasDtddSection(string.Empty);

        Assert.False(result);
    }

    [Fact]
    public void HasDtddSection_OnlyStartMarker_ReturnsFalse()
    {
        var overview = $"Description with {OverviewFormatter.DtddStartMarker} only";

        var result = _formatter.HasDtddSection(overview);

        Assert.False(result);
    }
}
