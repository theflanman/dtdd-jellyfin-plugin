using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Plugin.DoesTheDogDie.E2ETests.Fixtures;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.E2ETests.Tests;

[Trait("Category", "E2E")]
[Collection("Jellyfin")]
public sealed class ConfigPageTests
{
    private readonly JellyfinFixture _fixture;

    public ConfigPageTests(JellyfinFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConfigurationPage_IsServedAsHtml()
    {
        using var resp = await _fixture.Client.GetConfigurationPageAsync("Does The Dog Die");
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "Jellyfin should locate the embedded configPage.html resource by plugin name");
        resp.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

    [Theory]
    [InlineData("DoesTheDogDieConfigForm")]
    [InlineData("DoesTheDogDieConfigPage")]
    [InlineData("EnableMovies")]
    [InlineData("EnableSeries")]
    [InlineData("EnableBooks")]
    [InlineData("MinVotesThreshold")]
    [InlineData("TagPrefix")]
    [InlineData("SafeTagPrefix")]
    [InlineData("ShowAllTriggers")]
    [InlineData("CategoriesContainer")]
    public async Task ConfigurationPage_ContainsExpectedFormElement(string elementId)
    {
        using var resp = await _fixture.Client.GetConfigurationPageAsync("Does The Dog Die");
        var html = await resp.Content.ReadAsStringAsync();
        html.Should().Contain($"id=\"{elementId}\"", $"the config page must wire up the {elementId} control");
    }

    [Fact]
    public async Task ConfigurationPage_HasTitleAndConfigJsHook()
    {
        using var resp = await _fixture.Client.GetConfigurationPageAsync("Does The Dog Die");
        var html = await resp.Content.ReadAsStringAsync();

        html.Should().Contain("<title>Does The Dog Die</title>");
        html.Should().Contain("ApiClient.getPluginConfiguration", "save/load JS must hit the plugin configuration endpoint via the Jellyfin web ApiClient");
        html.Should().Contain("ApiClient.updatePluginConfiguration");
    }
}
