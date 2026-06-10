using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Plugin.DoesTheDogDie.E2ETests.Fixtures;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.E2ETests.Tests;

[Trait("Category", "E2E")]
[Collection("Jellyfin")]
public sealed class OverviewInjectionTests
{
    private const string DtddStartMarker = "<!-- DTDD_START -->";
    private const string DtddEndMarker = "<!-- DTDD_END -->";

    private readonly JellyfinFixture _fixture;

    public OverviewInjectionTests(JellyfinFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AddDescriptionWarnings_InjectsMarkersIntoOverview()
    {
        var movies = await _fixture.Client.GetItemsAsync("Movie");
        var johnWick = movies.Single(m => m.Name == "John Wick");
        var originalOverview = johnWick.Overview;

        try
        {
            await _fixture.Client.SetPluginConfigurationAsync(
                JellyfinFixture.PluginId,
                TestHelpers.ConfigWith(("AddDescriptionWarnings", true)));
            await _fixture.Client.RefreshItemMetadataAsync(johnWick.Id, replaceAllMetadata: true);

            await TestHelpers.WaitForAsync(
                async () =>
                {
                    var refreshed = (await _fixture.Client.GetItemsAsync("Movie")).Single(m => m.Name == "John Wick");
                    return refreshed.Overview is not null
                        && refreshed.Overview.Contains(DtddStartMarker, StringComparison.Ordinal)
                        && refreshed.Overview.Contains(DtddEndMarker, StringComparison.Ordinal);
                },
                TimeSpan.FromSeconds(30),
                failureMessage: "Expected DTDD overview markers after enabling AddDescriptionWarnings");

            var result = (await _fixture.Client.GetItemsAsync("Movie")).Single(m => m.Name == "John Wick");
            result.Overview.Should().Contain("An animal dies", "trigger names (capitalized) should appear in injected overview text");
        }
        finally
        {
            await _fixture.Client.SetPluginConfigurationAsync(JellyfinFixture.PluginId, TestHelpers.DefaultPluginConfig());
            await _fixture.Client.RefreshItemMetadataAsync(johnWick.Id, replaceAllMetadata: true);
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
    }

    [Fact]
    public async Task LockedOverview_NotModified_WhenAddDescriptionWarningsEnabled()
    {
        var movies = await _fixture.Client.GetItemsAsync("Movie");
        var johnWick = movies.Single(m => m.Name == "John Wick");

        try
        {
            await _fixture.Client.LockOverviewAsync(johnWick.Id);
            await _fixture.Client.SetPluginConfigurationAsync(
                JellyfinFixture.PluginId,
                TestHelpers.ConfigWith(("AddDescriptionWarnings", true)));
            await _fixture.Client.RefreshItemMetadataAsync(johnWick.Id, replaceAllMetadata: true);

            // Give the refresh a chance to (incorrectly) write — but we expect it not to.
            await Task.Delay(TimeSpan.FromSeconds(5));

            var refreshed = (await _fixture.Client.GetItemsAsync("Movie")).Single(m => m.Name == "John Wick");
            refreshed.Overview.Should().NotContain(DtddStartMarker, "locked Overview must not be modified");
        }
        finally
        {
            await _fixture.Client.UnlockAllFieldsAsync(johnWick.Id);
            await _fixture.Client.SetPluginConfigurationAsync(JellyfinFixture.PluginId, TestHelpers.DefaultPluginConfig());
            await _fixture.Client.RefreshItemMetadataAsync(johnWick.Id, replaceAllMetadata: true);
        }
    }
}
