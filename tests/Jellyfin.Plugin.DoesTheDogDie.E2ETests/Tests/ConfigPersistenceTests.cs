using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Plugin.DoesTheDogDie.E2ETests.Fixtures;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.E2ETests.Tests;

[Trait("Category", "E2E")]
[Collection("Jellyfin")]
public sealed class ConfigPersistenceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly JellyfinFixture _fixture;

    public ConfigPersistenceTests(JellyfinFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BoolFlag_EnableMovies_RoundTripsAfterPost()
    {
        try
        {
            await _fixture.Client.SetPluginConfigurationAsync(
                JellyfinFixture.PluginId,
                TestHelpers.ConfigWith(("EnableMovies", false)));

            var fetched = await GetConfigAsync();
            fetched.EnableMovies.Should().BeFalse("POSTed EnableMovies=false should be observable on next GET");
            fetched.EnableSeries.Should().BeTrue("untouched defaults should still round-trip alongside the flipped flag");
        }
        finally
        {
            await ResetConfigAsync();
        }
    }

    [Fact]
    public async Task IntValue_MinVotesThreshold_RoundTripsAfterPost()
    {
        try
        {
            await _fixture.Client.SetPluginConfigurationAsync(
                JellyfinFixture.PluginId,
                TestHelpers.ConfigWith(("MinVotesThreshold", 42)));

            var fetched = await GetConfigAsync();
            fetched.MinVotesThreshold.Should().Be(42);
        }
        finally
        {
            await ResetConfigAsync();
        }
    }

    [Fact]
    public async Task StringValue_TagPrefix_RoundTripsAfterPost()
    {
        try
        {
            await _fixture.Client.SetPluginConfigurationAsync(
                JellyfinFixture.PluginId,
                TestHelpers.ConfigWith(("TagPrefix", "TRIGGER:"), ("SafeTagPrefix", "OK:")));

            var fetched = await GetConfigAsync();
            fetched.TagPrefix.Should().Be("TRIGGER:");
            fetched.SafeTagPrefix.Should().Be("OK:");
        }
        finally
        {
            await ResetConfigAsync();
        }
    }

    [Fact]
    public async Task ListValue_EnabledCategoryIds_RoundTripsAfterPost()
    {
        try
        {
            await _fixture.Client.SetPluginConfigurationAsync(
                JellyfinFixture.PluginId,
                TestHelpers.ConfigWith(
                    ("ShowAllTriggers", false),
                    ("EnabledCategoryIds", new[] { 7, 13, 21 })));

            var fetched = await GetConfigAsync();
            fetched.ShowAllTriggers.Should().BeFalse();
            fetched.EnabledCategoryIds.Should().BeEquivalentTo(new[] { 7, 13, 21 });
        }
        finally
        {
            await ResetConfigAsync();
        }
    }

    [Fact(Skip = "Requires feature/description-injection to be merged")]
    public async Task DescriptionInjectionFlags_RoundTripTogetherAfterPost()
    {
        try
        {
            await _fixture.Client.SetPluginConfigurationAsync(
                JellyfinFixture.PluginId,
                TestHelpers.ConfigWith(
                    ("AddDescriptionWarnings", true),
                    ("IncludeTopComment", true),
                    ("MaxCommentLength", 77),
                    ("HideSpoilerComments", false)));

            var fetched = await GetConfigAsync();
            fetched.AddDescriptionWarnings.Should().BeTrue();
            fetched.IncludeTopComment.Should().BeTrue();
            fetched.MaxCommentLength.Should().Be(77);
            fetched.HideSpoilerComments.Should().BeFalse();
        }
        finally
        {
            await ResetConfigAsync();
        }
    }

    [Fact]
    public async Task Configuration_SurvivesItemRefresh()
    {
        var movies = await _fixture.Client.GetItemsAsync("Movie");
        var johnWick = movies.Single(m => m.Name == "John Wick");

        try
        {
            await _fixture.Client.SetPluginConfigurationAsync(
                JellyfinFixture.PluginId,
                TestHelpers.ConfigWith(("MinVotesThreshold", 123), ("TagPrefix", "WARN:")));

            // Refresh exercises the provider pipeline; config must not be reset by it.
            await _fixture.Client.RefreshItemMetadataAsync(johnWick.Id, replaceAllMetadata: true);

            var fetched = await GetConfigAsync();
            fetched.MinVotesThreshold.Should().Be(123, "item refresh must not clobber plugin config");
            fetched.TagPrefix.Should().Be("WARN:");
        }
        finally
        {
            await ResetConfigAsync();
            await _fixture.Client.RefreshItemMetadataAsync(johnWick.Id, replaceAllMetadata: true);
        }
    }

    private async Task<PluginConfigDto> GetConfigAsync()
    {
        using var doc = await _fixture.Client.GetPluginConfigurationAsync(JellyfinFixture.PluginId);
        var dto = JsonSerializer.Deserialize<PluginConfigDto>(doc.RootElement.GetRawText(), JsonOptions);
        dto.Should().NotBeNull("Jellyfin should return a JSON object for the plugin configuration");
        return dto!;
    }

    private async Task ResetConfigAsync()
    {
        await _fixture.Client.SetPluginConfigurationAsync(
            JellyfinFixture.PluginId,
            TestHelpers.DefaultPluginConfig());
    }

    private sealed class PluginConfigDto
    {
        public bool EnableMovies { get; set; }

        public bool EnableSeries { get; set; }

        public bool EnableBooks { get; set; }

        public int MinVotesThreshold { get; set; }

        public string TagPrefix { get; set; } = string.Empty;

        public string SafeTagPrefix { get; set; } = string.Empty;

        public bool ShowAllTriggers { get; set; }

        public int[] EnabledCategoryIds { get; set; } = System.Array.Empty<int>();

        public int[] EnabledTopicIds { get; set; } = System.Array.Empty<int>();

        public bool AddDescriptionWarnings { get; set; }

        public bool IncludeTopComment { get; set; }

        public int MaxCommentLength { get; set; }

        public bool HideSpoilerComments { get; set; }
    }
}
