using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Plugin.DoesTheDogDie.E2ETests.Fixtures;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.E2ETests.Tests;

/// <summary>
/// Covers issue #6 (top DTDD comments in Overview): comment lines appear under
/// trigger lines when <c>IncludeTopComment</c> is enabled, spoiler-topic comments
/// are hidden per <c>HideSpoilerComments</c>, and <c>MaxCommentLength</c> truncates.
/// Stub data: "an animal dies" has a non-spoiler comment by dogwatcher,
/// "a major character dies" is a spoiler topic with a comment by spoilerguy.
/// </summary>
[Trait("Category", "E2E")]
[Collection("Jellyfin")]
public sealed class CommentInjectionTests
{
    private const string DtddStartMarker = "<!-- DTDD_START -->";
    private const string AnimalComment = "The dog dies in the first act, off screen but heartbreaking.";
    private const string SpoilerComment = "His wife dies of illness before the film begins.";

    private readonly JellyfinFixture _fixture;

    public CommentInjectionTests(JellyfinFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task IncludeTopComment_AddsCommentLineWithAuthor()
    {
        var johnWick = await GetJohnWickAsync();

        try
        {
            await SetConfigAndRefreshAsync(johnWick.Id, ("AddDescriptionWarnings", true), ("IncludeTopComment", true));
            var refreshed = await WaitForInjectedAsync();

            refreshed.Overview.Should().Contain(
                $"💬 \"{AnimalComment}\" - dogwatcher",
                "non-spoiler trigger comments must be quoted with attribution");
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

            refreshed.Overview.Should().NotContain("💬", "comments must not appear when IncludeTopComment is off");
        }
        finally
        {
            await ResetConfigAndRefreshAsync(johnWick.Id);
        }
    }

    [Fact]
    public async Task HideSpoilerComments_HidesSpoilerTopicComment_KeepsOthers()
    {
        var johnWick = await GetJohnWickAsync();

        try
        {
            await SetConfigAndRefreshAsync(
                johnWick.Id,
                ("AddDescriptionWarnings", true),
                ("IncludeTopComment", true),
                ("HideSpoilerComments", true));
            var refreshed = await WaitForInjectedAsync();

            refreshed.Overview.Should().Contain("A major character dies", "the spoiler trigger line itself is not a comment and stays visible");
            refreshed.Overview.Should().NotContain(SpoilerComment, "comments on spoiler topics must be hidden");
            refreshed.Overview.Should().Contain(AnimalComment, "non-spoiler comments are unaffected by HideSpoilerComments");
        }
        finally
        {
            await ResetConfigAndRefreshAsync(johnWick.Id);
        }
    }

    [Fact]
    public async Task HideSpoilerComments_Disabled_ShowsSpoilerTopicComment()
    {
        var johnWick = await GetJohnWickAsync();

        try
        {
            await SetConfigAndRefreshAsync(
                johnWick.Id,
                ("AddDescriptionWarnings", true),
                ("IncludeTopComment", true),
                ("HideSpoilerComments", false));
            var refreshed = await WaitForInjectedAsync();

            refreshed.Overview.Should().Contain($"💬 \"{SpoilerComment}\" - spoilerguy");
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

            refreshed.Overview.Should().NotContain(AnimalComment, "comments beyond 30 chars must be cut");
            refreshed.Overview.Should().Contain(
                $"💬 \"{AnimalComment.Substring(0, 30).TrimEnd()}...\"",
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
                    && refreshed.Tags.Contains("CW: an animal dies");
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
