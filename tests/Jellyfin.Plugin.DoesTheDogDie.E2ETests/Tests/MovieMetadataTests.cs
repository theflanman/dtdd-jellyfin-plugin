using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Plugin.DoesTheDogDie.E2ETests.Fixtures;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.E2ETests.Tests;

[Trait("Category", "E2E")]
[Collection("Jellyfin")]
public sealed class MovieMetadataTests
{
    private readonly JellyfinFixture _fixture;

    public MovieMetadataTests(JellyfinFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task JohnWick_GetsDtddProviderId()
    {
        var movies = await _fixture.Client.GetItemsAsync("Movie");
        var johnWick = movies.SingleOrDefault(m => m.Name == "John Wick");

        johnWick.Should().NotBeNull("John Wick fixture should have been scanned");
        johnWick!.ProviderIds.Should().ContainKey("Dtdd")
            .WhoseValue.Should().Be("1234", "WireMock stub returns DTDD id 1234 for tt2911666");
    }

    [Fact]
    public async Task JohnWick_GetsCwTagsForPositiveTriggers()
    {
        var movies = await _fixture.Client.GetItemsAsync("Movie");
        var johnWick = movies.Single(m => m.Name == "John Wick");

        johnWick.Tags.Should().Contain(t => t.StartsWith("CW:"), "stub triggers exceed default vote threshold");
        johnWick.Tags.Should().Contain("CW: an animal dies");
        johnWick.Tags.Should().Contain("CW: someone is shot");
    }
}
