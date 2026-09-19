using System;
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
    public async Task JohnWick_GetsTagsPerVerdict()
    {
        var movies = await _fixture.Client.GetItemsAsync("Movie");
        var johnWick = movies.Single(m => m.Name == "John Wick");

        // Stub stats at the default DecisionThreshold=0.5 / IntervalMass=0.95:
        //   "a dog dies"   42/1  -> 95% CI [0.880, 0.994] -> LikelyPresent -> CW: tag
        //   "someone dies"  3/4  -> 95% CI [0.157, 0.755] -> Uncertain     -> no tag at all
        //   "a child dies"  1/38 -> 95% CI [0.006, 0.132] -> LikelyAbsent  -> Safe: tag
        johnWick.Tags.Should().Contain("CW: a dog dies");
        johnWick.Tags.Should().Contain("Safe: a child dies");
        johnWick.Tags.Should().NotContain(
            t => t.EndsWith("someone dies", StringComparison.Ordinal),
            "an Uncertain verdict must not produce a tag of either kind");
    }
}
