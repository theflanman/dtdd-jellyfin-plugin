using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Plugin.DoesTheDogDie.E2ETests.Fixtures;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.E2ETests.Tests;

/// <summary>
/// Covers issue #6 (top DTDD comments in Overview): a comment line appears under the trigger group
/// when <c>IncludeTopComment</c> is enabled, and <c>MaxCommentLength</c> truncates it.
/// Comments now come from <c>/items/{id}/ratings</c>: the highest <c>VoteSum</c> rating per topic that
/// carries a <c>triggerDescription</c> wins. The stub gives topic 201 ("a dog dies") two competing
/// ratings so the top-pick is actually exercised.
/// </summary>
[Trait("Category", "E2E")]
[Collection("Jellyfin")]
public sealed class CommentInjectionTests
{
    private const string DtddStartMarker = "<!-- DTDD_START -->";
    private const string DogComment = "The dog dies in the first act, off screen but heartbreaking.";
    private const string LoserComment = "A lower-voted report that must never win the top-comment pick.";

    private readonly JellyfinFixture _fixture;

    public CommentInjectionTests(JellyfinFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task IncludeTopComment_AddsHighestVotedCommentForTopic()
    {
        var johnWick = await GetJohnWickAsync();

        try
        {
            await SetConfigAndRefreshAsync(johnWick.Id, ("AddDescriptionWarnings", true), ("IncludeTopComment", true));
            var refreshed = await WaitForInjectedAsync();

            refreshed.Overview.Should().Contain(
                $"• a dog dies: {DogComment}",
                "the top-voted rating's description is attached to its trigger");
            refreshed.Overview.Should().NotContain(
                LoserComment,
                "only the highest VoteSum rating per topic is used");
        }
        finally
        {
            await ResetConfigAndRefreshAsync(johnWick.Id);
        }
    }

    [Fact]
    public async Task IncludeTopComment_Disabled_OmitsCommentLines()
    {
        var johnWick = await GetJohnWickAsync();

        try
        {
            await SetConfigAndRefreshAsync(johnWick.Id, ("AddDescriptionWarnings", true), ("IncludeTopComment", false));
            var refreshed = await WaitForInjectedAsync();

            refreshed.Overview.Should().NotContain("•", "comments must not appear when IncludeTopComment is off");
            refreshed.Overview.Should().NotContain(DogComment);
        }
        finally
        {
            await ResetConfigAndRefreshAsync(johnWick.Id);
        }
    }

    [Fact]
    public async Task MaxCommentLength_TruncatesLongComments()
    {
        var johnWick = await GetJohnWickAsync();

        try
        {
            await SetConfigAndRefreshAsync(
                johnWick.Id,
                ("AddDescriptionWarnings", true),
                ("IncludeTopComment", true),
                ("MaxCommentLength", 30));
            var refreshed = await WaitForInjectedAsync();

            refreshed.Overview.Should().NotContain(DogComment, "comments beyond 30 chars must be cut");
            refreshed.Overview.Should().Contain(
                $"• a dog dies: {DogComment.Substring(0, 30).TrimEnd()}...",
                "truncated comments end with an ellipsis");
        }
        finally
        {
            await ResetConfigAndRefreshAsync(johnWick.Id);
        }
    }

    private async Task<JellyfinClient.JellyfinItemDto> GetJohnWickAsync()
    {
        var movies = await _fixture.Client.GetItemsAsync("Movie");
        return movies.Single(m => m.Name == "John Wick");
    }

    private async Task SetConfigAndRefreshAsync(string itemId, params (string Key, object Value)[] overrides)
    {
        await _fixture.Client.SetPluginConfigurationAsync(JellyfinFixture.PluginId, TestHelpers.ConfigWith(overrides));
        await _fixture.Client.RefreshItemMetadataAsync(itemId, replaceAllMetadata: true);
    }

    private async Task ResetConfigAndRefreshAsync(string itemId)
    {
        await _fixture.Client.SetPluginConfigurationAsync(JellyfinFixture.PluginId, TestHelpers.DefaultPluginConfig());
        await _fixture.Client.RefreshItemMetadataAsync(itemId, replaceAllMetadata: true);
        await TestHelpers.WaitForAsync(
            async () =>
            {
                var refreshed = (await _fixture.Client.GetItemsAsync("Movie")).Single(m => m.Name == "John Wick");
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
                    && current.Overview.Contains(DtddStartMarker, StringComparison.Ordinal);
            },
            TimeSpan.FromSeconds(30),
            failureMessage: "Expected DTDD markers in Overview after enabling AddDescriptionWarnings");

        return (await _fixture.Client.GetItemsAsync("Movie")).Single(m => m.Name == "John Wick");
    }
}
