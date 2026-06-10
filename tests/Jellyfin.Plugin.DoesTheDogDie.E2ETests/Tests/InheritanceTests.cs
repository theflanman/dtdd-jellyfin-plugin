using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Plugin.DoesTheDogDie.E2ETests.Fixtures;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.E2ETests.Tests;

[Trait("Category", "E2E")]
[Collection("Jellyfin")]
public sealed class InheritanceTests
{
    private readonly JellyfinFixture _fixture;

    public InheritanceTests(JellyfinFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Episode_InheritsDtddIdFromSeries()
    {
        var episodes = await _fixture.Client.GetItemsAsync("Episode");
        var pilot = episodes.SingleOrDefault(e => e.Name == "Pilot");

        pilot.Should().NotBeNull("Breaking Bad S01E01 fixture should have been scanned");
        pilot!.ProviderIds.Should().ContainKey("Dtdd")
            .WhoseValue.Should().Be("5678", "episode should inherit parent series' DTDD id");
    }

    [Fact]
    public async Task Season_InheritsDtddIdFromSeries()
    {
        var seasons = await _fixture.Client.GetItemsAsync("Season");
        var s1 = seasons.SingleOrDefault(s => s.SeriesId is not null);

        s1.Should().NotBeNull();
        s1!.ProviderIds.Should().ContainKey("Dtdd").WhoseValue.Should().Be("5678");
    }
}
