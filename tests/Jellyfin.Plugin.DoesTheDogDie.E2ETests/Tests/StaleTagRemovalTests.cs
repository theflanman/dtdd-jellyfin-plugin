using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Plugin.DoesTheDogDie.E2ETests.Fixtures;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.E2ETests.Tests;

/// <summary>
/// The one path the unit tests cannot prove end to end: a tag written by an earlier refresh must be
/// REMOVED from the persisted Jellyfin item when a later configuration no longer produces it. The unit
/// tests can only show that ApplyTags builds the right tag set for a given configuration; whether the
/// old set actually leaves the database takes a real refresh against a real item.
/// </summary>
/// <remarks>
/// Stub taxonomy: topic 201 "a dog dies" is in category 3 (Animals); topics 202 "someone dies" and 203
/// "a child dies" are in category 4 (Violence). At the default DecisionThreshold, 201 is LikelyPresent
/// ("CW: a dog dies"), 202 is Uncertain (no tag of either kind) and 203 is LikelyAbsent
/// ("Safe: a child dies"). Restricting to category 4 therefore has to drop the dog tag while keeping a
/// category-4 tag on the item — which distinguishes "the filter worked" from "the refresh wiped
/// everything".
/// </remarks>
[Trait("Category", "E2E")]
[Collection("Jellyfin")]
public sealed class StaleTagRemovalTests
{
    private readonly JellyfinFixture _fixture;

    public StaleTagRemovalTests(JellyfinFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DisablingACategory_RemovesItsTagsOnRefresh()
    {
        var johnWick = await GetJohnWickAsync();

        try
        {
            // Tag everything first, and confirm the tag we are about to invalidate is really there.
            await _fixture.Client.SetPluginConfigurationAsync(
                JellyfinFixture.PluginId,
                TestHelpers.ConfigWith(("AddWarningTags", true), ("ShowAllTriggers", true)));
            await _fixture.Client.RefreshItemMetadataAsync(johnWick.Id, replaceAllMetadata: true);

            await TestHelpers.WaitForAsync(
                async () =>
                {
                    var current = await GetJohnWickAsync();
                    return current.Tags.Contains("CW: a dog dies");
                },
                TimeSpan.FromSeconds(30),
                failureMessage: "Setup: expected the Animals-category tag before narrowing the filter");

            // Now restrict to Violence (category 4) only. "a dog dies" is category 3 and must go.
            await _fixture.Client.SetPluginConfigurationAsync(
                JellyfinFixture.PluginId,
                TestHelpers.ConfigWith(
                    ("AddWarningTags", true),
                    ("ShowAllTriggers", false),
                    ("EnabledCategoryIds", new[] { 4 })));
            await _fixture.Client.RefreshItemMetadataAsync(johnWick.Id, replaceAllMetadata: true);

            await TestHelpers.WaitForAsync(
                async () =>
                {
                    var current = await GetJohnWickAsync();
                    return !current.Tags.Contains("CW: a dog dies");
                },
                TimeSpan.FromSeconds(30),
                failureMessage: "Expected the category-3 tag to be removed once only category 4 is enabled");

            var after = await GetJohnWickAsync();
            after.Tags.Should().NotContain(
                "CW: a dog dies",
                "a tag from a now-disabled category must not linger on the item");
            after.Tags.Should().Contain(
                "Safe: a child dies",
                "the surviving category-4 trigger proves the refresh filtered rather than wiped every tag");
        }
        finally
        {
            await _fixture.Client.SetPluginConfigurationAsync(JellyfinFixture.PluginId, TestHelpers.DefaultPluginConfig());
            await _fixture.Client.RefreshItemMetadataAsync(johnWick.Id, replaceAllMetadata: true);
            await TestHelpers.WaitForAsync(
                async () =>
                {
                    var current = await GetJohnWickAsync();
                    return current.Tags.Contains("CW: a dog dies");
                },
                TimeSpan.FromSeconds(30),
                failureMessage: "Cleanup: tags did not return after restoring the default config");
        }
    }

    private async Task<JellyfinClient.JellyfinItemDto> GetJohnWickAsync()
    {
        var movies = await _fixture.Client.GetItemsAsync("Movie");
        return movies.Single(m => m.Name == "John Wick");
    }
}
