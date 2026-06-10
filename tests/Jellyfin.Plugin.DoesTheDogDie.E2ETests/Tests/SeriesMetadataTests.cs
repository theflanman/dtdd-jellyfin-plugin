using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Plugin.DoesTheDogDie.E2ETests.Fixtures;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.E2ETests.Tests;

[Trait("Category", "E2E")]
[Collection("Jellyfin")]
public sealed class SeriesMetadataTests
{
    private readonly JellyfinFixture _fixture;

    public SeriesMetadataTests(JellyfinFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BreakingBad_Series_GetsDtddProviderIdAndTag()
    {
        var series = await _fixture.Client.GetItemsAsync("Series");
        var breakingBad = series.SingleOrDefault(s => s.Name == "Breaking Bad");

        breakingBad.Should().NotBeNull("Breaking Bad fixture should have been scanned");
        breakingBad!.ProviderIds.Should().ContainKey("Dtdd").WhoseValue.Should().Be("5678");
        breakingBad.Tags.Should().Contain("CW: someone dies");
    }
}
