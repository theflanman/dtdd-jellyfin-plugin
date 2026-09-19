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

    /// <summary>
    /// Replaces the retired MinVotesThreshold test: a configuration knob must still be able to
    /// suppress every content warning on an item.
    /// </summary>
    /// <remarks>
    /// John Wick's three stub stats have these 95% credible intervals (uniform prior):
    /// "a dog dies" 42/1 -> [0.880, 0.994], "someone dies" 3/4 -> [0.157, 0.755],
    /// "a child dies" 1/38 -> [0.006, 0.132]. Raising DecisionThreshold to 0.99 puts it above every
    /// interval's lower bound, so no stat can be LikelyPresent any more and no CW: tag can survive:
    /// "a dog dies" straddles 0.99 and becomes Uncertain (which is tagged neither way), while the two
    /// weaker stats fall entirely below it and become LikelyAbsent (Safe: tags).
    /// <para>
    /// Note the intervals are pairwise disjoint, so no single threshold can make all three Uncertain —
    /// that would require a threshold inside all three intervals at once, and their intersection is
    /// empty. "No tags of either kind" is therefore unreachable for this item by construction; the
    /// assertions below instead pin the verdict flips that actually happen.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task DecisionThreshold_AboveEveryStubsLowerBound_RemovesAllWarningTags()
    {
        var movies = await _fixture.Client.GetItemsAsync("Movie");
        var johnWick = movies.Single(m => m.Name == "John Wick");

        try
        {
            await _fixture.Client.SetPluginConfigurationAsync(
                JellyfinFixture.PluginId,
                TestHelpers.ConfigWith(("DecisionThreshold", 0.99)));
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
                failureMessage: "Expected all CW: tags removed once DecisionThreshold exceeds every stat's interval lower bound");

            var tagged = (await _fixture.Client.GetItemsAsync("Movie")).Single(m => m.Name == "John Wick");
            tagged.Tags.Should().NotContain(
                t => t.EndsWith("a dog dies", StringComparison.Ordinal),
                "42/1 straddles a 0.99 threshold, so it becomes Uncertain and loses its tag entirely");
            tagged.Tags.Should().Contain(
                "Safe: a child dies",
                "1/38 stays entirely below the threshold, so it remains LikelyAbsent");
            tagged.Tags.Should().Contain(
                "Safe: someone dies",
                "3/4's interval tops out at 0.755, so a 0.99 threshold flips it from Uncertain to LikelyAbsent");
        }
        finally
        {
            await _fixture.Client.SetPluginConfigurationAsync(JellyfinFixture.PluginId, TestHelpers.DefaultPluginConfig());
            await _fixture.Client.RefreshItemMetadataAsync(johnWick.Id, replaceAllMetadata: true);
            await TestHelpers.WaitForAsync(
                async () =>
                {
                    var refreshed = (await _fixture.Client.GetItemsAsync("Movie")).Single(m => m.Name == "John Wick");
                    return refreshed.Tags.Contains("CW: a dog dies");
                },
                TimeSpan.FromSeconds(30),
                failureMessage: "Cleanup: tags did not return after restoring default config");
        }
    }
}
