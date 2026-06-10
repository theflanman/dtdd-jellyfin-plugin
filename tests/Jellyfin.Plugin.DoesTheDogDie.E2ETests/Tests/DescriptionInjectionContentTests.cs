using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Plugin.DoesTheDogDie.E2ETests.Fixtures;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.E2ETests.Tests;

/// <summary>
/// Asserts the exact content shape that OverviewFormatter injects between the
/// <c>&lt;!-- DTDD_START --&gt;</c> / <c>&lt;!-- DTDD_END --&gt;</c> markers — heading,
/// per-trigger warning lines with vote counts, and content omission when triggers
/// fall under MinVotesThreshold.
/// </summary>
[Trait("Category", "E2E")]
[Collection("Jellyfin")]
public sealed class DescriptionInjectionContentTests
{
    private const string DtddStartMarker = "<!-- DTDD_START -->";
    private const string DtddEndMarker = "<!-- DTDD_END -->";

    private readonly JellyfinFixture _fixture;

    public DescriptionInjectionContentTests(JellyfinFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(Skip = "Requires feature/description-injection to be merged")]
    public async Task InjectedOverview_ContainsContentWarningsHeading()
    {
        var johnWick = await GetJohnWickAsync();

        try
        {
            await EnableInjectionAndRefreshAsync(johnWick.Id);
            var refreshed = await WaitForInjectedAsync();
            refreshed.Overview.Should().Contain("**Content Warnings** (via DoesTheDogDie)",
                "OverviewFormatter writes a fixed markdown heading above the trigger lines");
        }
        finally
        {
            await DisableInjectionAndRefreshAsync(johnWick.Id);
        }
    }

    [Fact(Skip = "Requires feature/description-injection to be merged")]
    public async Task InjectedOverview_ContainsTriggerLines_WithCapitalizedNameAndVoteCounts()
    {
        var johnWick = await GetJohnWickAsync();

        try
        {
            await EnableInjectionAndRefreshAsync(johnWick.Id);
            var refreshed = await WaitForInjectedAsync();

            // Stub yesSum/noSum: "an animal dies" 87/4, "someone is shot" 256/8.
            // OverviewFormatter capitalizes first letter and emits "⚠️ {Name} ({Yes} yes / {No} no)".
            refreshed.Overview.Should().Contain("⚠️ An animal dies (87 yes / 4 no)");
            refreshed.Overview.Should().Contain("⚠️ Someone is shot (256 yes / 8 no)");
        }
        finally
        {
            await DisableInjectionAndRefreshAsync(johnWick.Id);
        }
    }

    [Fact(Skip = "Requires feature/description-injection to be merged")]
    public async Task InjectedOverview_OmitsLowVoteTrigger_WhenAboveMinVotesThreshold()
    {
        var johnWick = await GetJohnWickAsync();

        try
        {
            // "an animal dies" total = 91, "someone is shot" total = 264.
            // Threshold 100 must drop the first but keep the second.
            await _fixture.Client.SetPluginConfigurationAsync(
                JellyfinFixture.PluginId,
                TestHelpers.ConfigWith(("AddDescriptionWarnings", true), ("MinVotesThreshold", 100)));
            await _fixture.Client.RefreshItemMetadataAsync(johnWick.Id, replaceAllMetadata: true);

            await TestHelpers.WaitForAsync(
                async () =>
                {
                    var current = (await _fixture.Client.GetItemsAsync("Movie")).Single(m => m.Name == "John Wick");
                    return current.Overview is not null
                        && current.Overview.Contains("Someone is shot", StringComparison.Ordinal);
                },
                TimeSpan.FromSeconds(30),
                failureMessage: "Expected high-vote trigger to appear after refresh with MinVotesThreshold=100");

            var refreshed = (await _fixture.Client.GetItemsAsync("Movie")).Single(m => m.Name == "John Wick");
            refreshed.Overview.Should().Contain(DtddStartMarker, "high-vote trigger should still keep the DTDD section present");
            refreshed.Overview.Should().Contain("Someone is shot", "264-total trigger should pass a 100-vote threshold");
            refreshed.Overview.Should().NotContain("An animal dies", "91-total trigger should be filtered out at MinVotesThreshold=100");
        }
        finally
        {
            await DisableInjectionAndRefreshAsync(johnWick.Id);
        }
    }

    [Fact]
    public async Task InjectedOverview_OmitsAllTriggers_WhenMinVotesExceedsAllStubVotes()
    {
        var johnWick = await GetJohnWickAsync();

        try
        {
            await _fixture.Client.SetPluginConfigurationAsync(
                JellyfinFixture.PluginId,
                TestHelpers.ConfigWith(("AddDescriptionWarnings", true), ("MinVotesThreshold", 100000)));
            await _fixture.Client.RefreshItemMetadataAsync(johnWick.Id, replaceAllMetadata: true);

            // Give the refresh a window to (incorrectly) inject markers; OverviewFormatter
            // returns empty when no triggers survive filtering, so AppendToOverview leaves
            // the existing overview untouched — no DTDD markers should appear.
            await Task.Delay(TimeSpan.FromSeconds(5));

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

    [Fact(Skip = "Requires feature/description-injection to be merged")]
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
            section.Should().Contain("**Content Warnings**", "the heading must live inside the marker-bounded section");
            section.Should().Contain("An animal dies", "trigger lines must live inside the marker-bounded section, not before/after");
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
                return refreshed.Overview is null
                    || !refreshed.Overview.Contains(DtddStartMarker, StringComparison.Ordinal);
            },
            TimeSpan.FromSeconds(30),
            failureMessage: "Cleanup: DTDD markers should be removed after disabling AddDescriptionWarnings");
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
