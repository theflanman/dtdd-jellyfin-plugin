using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Plugin.DoesTheDogDie.E2ETests.Fixtures;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.E2ETests.Tests;

[Trait("Category", "E2E")]
[Collection("Jellyfin")]
public sealed class ConfigurationTests
{
    private readonly JellyfinFixture _fixture;

    public ConfigurationTests(JellyfinFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MinVotesThreshold_FiltersAllTriggers_WhenSetAboveStubVoteCount()
    {
        var movies = await _fixture.Client.GetItemsAsync("Movie");
        var johnWick = movies.Single(m => m.Name == "John Wick");

        try
        {
            await _fixture.Client.SetPluginConfigurationAsync(
                JellyfinFixture.PluginId,
                TestHelpers.ConfigWith(("MinVotesThreshold", 10000)));
            // replaceAllMetadata=true so DtddMovieProvider re-runs even though Dtdd ProviderId
            // is already set from the initial scan.
            await _fixture.Client.RefreshItemMetadataAsync(johnWick.Id, replaceAllMetadata: true);

            await TestHelpers.WaitForAsync(
                async () =>
                {
                    var refreshed = (await _fixture.Client.GetItemsAsync("Movie")).Single(m => m.Name == "John Wick");
                    return !refreshed.Tags.Any(t => t.StartsWith("CW:", StringComparison.Ordinal));
                },
                TimeSpan.FromSeconds(30),
                failureMessage: "Expected all CW: tags removed once MinVotesThreshold exceeds stub vote counts");
        }
        finally
        {
            await _fixture.Client.SetPluginConfigurationAsync(JellyfinFixture.PluginId, TestHelpers.DefaultPluginConfig());
            await _fixture.Client.RefreshItemMetadataAsync(johnWick.Id, replaceAllMetadata: true);
            await TestHelpers.WaitForAsync(
                async () =>
                {
                    var refreshed = (await _fixture.Client.GetItemsAsync("Movie")).Single(m => m.Name == "John Wick");
                    return refreshed.Tags.Contains("CW: an animal dies");
                },
                TimeSpan.FromSeconds(30),
                failureMessage: "Cleanup: tags did not return after restoring default config");
        }
    }
}
