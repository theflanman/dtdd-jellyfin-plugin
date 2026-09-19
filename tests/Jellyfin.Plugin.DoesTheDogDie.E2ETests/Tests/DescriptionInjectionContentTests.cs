using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Plugin.DoesTheDogDie.E2ETests.Fixtures;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.E2ETests.Tests;

/// <summary>
/// Asserts the exact content shape that OverviewFormatter injects between the
/// <c>&lt;!-- DTDD_START --&gt;</c> / <c>&lt;!-- DTDD_END --&gt;</c> markers — one group per verdict,
/// each trigger rendered with its vote counts and credible interval, plus the required attribution.
/// </summary>
[Trait("Category", "E2E")]
[Collection("Jellyfin")]
public sealed class DescriptionInjectionContentTests
{
    private const string DtddStartMarker = "<!-- DTDD_START -->";
    private const string DtddEndMarker = "<!-- DTDD_END -->";

    // Rendered from the John Wick stub stats at DecisionThreshold=0.5 / IntervalMass=0.95.
    // The bound separator is an en dash (U+2013), as OverviewFormatter emits it.
    private const string DogLine = "a dog dies (42/43, 88–99%)";
    private const string SomeoneLine = "someone dies (3/7, 16–76%)";
    private const string ChildLine = "a child dies (1/39, 1–13%)";

    private readonly JellyfinFixture _fixture;

    public DescriptionInjectionContentTests(JellyfinFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task InjectedOverview_ContainsOneHeadingPerVerdict()
    {
        var johnWick = await GetJohnWickAsync();

        try
        {
            await EnableInjectionAndRefreshAsync(johnWick.Id);
            var refreshed = await WaitForInjectedAsync();

            refreshed.Overview.Should().Contain("#### Content warnings\n", "LikelyPresent triggers head the section");
            refreshed.Overview.Should().Contain("#### Possible\n", "Uncertain triggers are shown under their own heading");
            refreshed.Overview.Should().Contain("#### Reported Safe\n", "LikelyAbsent triggers are shown under their own heading");
        }
        finally
        {
            await DisableInjectionAndRefreshAsync(johnWick.Id);
        }
    }

    [Fact]
    public async Task InjectedOverview_ContainsTriggerLines_WithVoteCountsAndIntervals()
    {
        var johnWick = await GetJohnWickAsync();

        try
        {
            await EnableInjectionAndRefreshAsync(johnWick.Id);
            var refreshed = await WaitForInjectedAsync();

            refreshed.Overview.Should().Contain("* " + DogLine);
            refreshed.Overview.Should().Contain("* " + SomeoneLine);
            refreshed.Overview.Should().Contain("* " + ChildLine);
        }
        finally
        {
            await DisableInjectionAndRefreshAsync(johnWick.Id);
        }
    }

    [Fact]
    public async Task InjectedOverview_CarriesRequiredAttribution()
    {
        var johnWick = await GetJohnWickAsync();

        try
        {
            await EnableInjectionAndRefreshAsync(johnWick.Id);
            var refreshed = await WaitForInjectedAsync();

            refreshed.Overview.Should().Contain(
                "Powered by DoesTheDogDie.com — https://www.doesthedogdie.com",
                "DtDD's terms require the attribution phrase and link on every view that shows their data");
        }
        finally
        {
            await DisableInjectionAndRefreshAsync(johnWick.Id);
        }
    }

    /// <summary>
    /// Replaces the retired MinVotesThreshold omission test. Under the verdict model a trigger that
    /// loses its LikelyPresent verdict is not dropped from the description — it moves to the
    /// "Possible:" group, which is deliberate: uncertain triggers are still shown to the user.
    /// </summary>
    [Fact]
    public async Task InjectedOverview_MovesTriggerToPossible_WhenDecisionThresholdRaised()
    {
        var johnWick = await GetJohnWickAsync();

        try
        {
            // "a dog dies" 42/1 has a 95% credible interval of [0.880, 0.994], which straddles 0.99,
            // so it demotes from LikelyPresent to Uncertain. "someone dies" (upper 0.755) and
            // "a child dies" (upper 0.132) both fall wholly below 0.99 and become LikelyAbsent.
            await _fixture.Client.SetPluginConfigurationAsync(
                JellyfinFixture.PluginId,
                TestHelpers.ConfigWith(("AddDescriptionWarnings", true), ("DecisionThreshold", 0.99)));
            await _fixture.Client.RefreshItemMetadataAsync(johnWick.Id, replaceAllMetadata: true);

            await TestHelpers.WaitForAsync(
                async () =>
                {
                    var current = (await _fixture.Client.GetItemsAsync("Movie")).Single(m => m.Name == "John Wick");
                    return current.Overview is not null
                        && current.Overview.Contains("#### Possible\n* " + DogLine, StringComparison.Ordinal);
                },
                TimeSpan.FromSeconds(30),
                failureMessage: "Expected the demoted trigger to appear under the Possible heading at DecisionThreshold=0.99");

            var refreshed = (await _fixture.Client.GetItemsAsync("Movie")).Single(m => m.Name == "John Wick");
            refreshed.Overview.Should().Contain(DtddStartMarker, "the DTDD section stays present when triggers remain");
            refreshed.Overview.Should().NotContain("#### Content warnings\n", "nothing is LikelyPresent at a 0.99 threshold");
            refreshed.Overview.Should().Contain("#### Reported Safe\n", "the two weaker stats drop below the threshold entirely");
        }
        finally
        {
            await DisableInjectionAndRefreshAsync(johnWick.Id);
        }
    }

    /// <summary>
    /// Replaces the retired "MinVotesThreshold above every stub vote" test: when nothing survives
    /// filtering the formatter must emit no section at all rather than an empty one. The surviving
    /// knob that can empty the set is category filtering.
    /// </summary>
    [Fact]
    public async Task InjectedOverview_OmitsSection_WhenNoTriggersSurviveCategoryFilter()
    {
        var johnWick = await GetJohnWickAsync();

        try
        {
            // Category 999 matches none of the stub topics (which live in categories 3 and 4).
            await _fixture.Client.SetPluginConfigurationAsync(
                JellyfinFixture.PluginId,
                TestHelpers.ConfigWith(
                    ("AddDescriptionWarnings", true),
                    ("ShowAllTriggers", false),
                    ("EnabledCategoryIds", new[] { 999 })));
            await _fixture.Client.RefreshItemMetadataAsync(johnWick.Id, replaceAllMetadata: true);

            // Give the refresh a window to (incorrectly) inject markers; OverviewFormatter
            // returns empty when no triggers survive filtering, so no DTDD markers should appear.
            await TestHelpers.WaitForAsync(
                async () =>
                {
                    var current = (await _fixture.Client.GetItemsAsync("Movie")).Single(m => m.Name == "John Wick");
                    return !current.Tags.Any(t => t.StartsWith("CW:", StringComparison.Ordinal));
                },
                TimeSpan.FromSeconds(30),
                failureMessage: "Expected the category filter to drop every trigger (observed via tag removal)");

            var refreshed = (await _fixture.Client.GetItemsAsync("Movie")).Single(m => m.Name == "John Wick");
            (refreshed.Overview ?? string.Empty).Should().NotContain(DtddStartMarker,
                "no triggers survive filtering, so the formatter must not emit a DTDD section");
            (refreshed.Overview ?? string.Empty).Should().NotContain(DtddEndMarker);
        }
        finally
        {
            await DisableInjectionAndRefreshAsync(johnWick.Id);
        }
    }

    [Fact]
    public async Task InjectedOverview_IsBoundedByDtddMarkers()
    {
        var johnWick = await GetJohnWickAsync();

        try
        {
            await EnableInjectionAndRefreshAsync(johnWick.Id);
            var refreshed = await WaitForInjectedAsync();

            var overview = refreshed.Overview!;
            var start = overview.IndexOf(DtddStartMarker, StringComparison.Ordinal);
            var end = overview.IndexOf(DtddEndMarker, StringComparison.Ordinal);

            start.Should().BeGreaterThanOrEqualTo(0);
            end.Should().BeGreaterThan(start, "end marker must follow start marker so the section is well-formed");

            var section = overview.Substring(start, (end + DtddEndMarker.Length) - start);
            section.Should().Contain("#### Content warnings\n", "the heading must live inside the marker-bounded section");
            section.Should().Contain(DogLine, "trigger lines must live inside the marker-bounded section, not before/after");
        }
        finally
        {
            await DisableInjectionAndRefreshAsync(johnWick.Id);
        }
    }

    private async Task<JellyfinClient.JellyfinItemDto> GetJohnWickAsync()
    {
        var movies = await _fixture.Client.GetItemsAsync("Movie");
        return movies.Single(m => m.Name == "John Wick");
    }

    private async Task EnableInjectionAndRefreshAsync(string itemId)
    {
        await _fixture.Client.SetPluginConfigurationAsync(
            JellyfinFixture.PluginId,
            TestHelpers.ConfigWith(("AddDescriptionWarnings", true)));
        await _fixture.Client.RefreshItemMetadataAsync(itemId, replaceAllMetadata: true);
    }

    private async Task DisableInjectionAndRefreshAsync(string itemId)
    {
        await _fixture.Client.SetPluginConfigurationAsync(JellyfinFixture.PluginId, TestHelpers.DefaultPluginConfig());
        await _fixture.Client.RefreshItemMetadataAsync(itemId, replaceAllMetadata: true);
        await TestHelpers.WaitForAsync(
            async () =>
            {
                var refreshed = (await _fixture.Client.GetItemsAsync("Movie")).Single(m => m.Name == "John Wick");

                // Tags must be restored too: the test body may have raised DecisionThreshold or filtered
                // by category, stripping CW: tags; later tests rely on the default-config tag state.
                return (refreshed.Overview is null
                    || !refreshed.Overview.Contains(DtddStartMarker, StringComparison.Ordinal))
                    && refreshed.Tags.Contains("CW: a dog dies");
            },
            TimeSpan.FromSeconds(30),
            failureMessage: "Cleanup: DTDD markers should be removed and CW: tags restored after resetting config");
    }

    private async Task<JellyfinClient.JellyfinItemDto> WaitForInjectedAsync()
    {
        await TestHelpers.WaitForAsync(
            async () =>
            {
                var current = (await _fixture.Client.GetItemsAsync("Movie")).Single(m => m.Name == "John Wick");
                return current.Overview is not null
                    && current.Overview.Contains(DtddStartMarker, StringComparison.Ordinal)
                    && current.Overview.Contains(DtddEndMarker, StringComparison.Ordinal);
            },
            TimeSpan.FromSeconds(30),
            failureMessage: "Expected DTDD markers in Overview after enabling AddDescriptionWarnings");

        return (await _fixture.Client.GetItemsAsync("Movie")).Single(m => m.Name == "John Wick");
    }
}
