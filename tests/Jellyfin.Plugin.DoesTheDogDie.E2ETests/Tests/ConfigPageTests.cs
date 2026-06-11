using System.Net;
using System.Text.Json;
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

    [Fact]
    public async Task TopicsEndpoint_ReturnsPopulatedCategories()
    {
        using var http = new System.Net.Http.HttpClient { BaseAddress = _fixture.JellyfinBaseAddress };
        http.DefaultRequestHeaders.Add("X-Emby-Token", _fixture.Client.AccessToken);

        using var resp = await http.GetAsync("/Plugins/DoesTheDogDie/Topics");
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "Topics endpoint must be accessible and return trigger categories");
        resp.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var categories = doc.RootElement.GetProperty("categories");
        categories.GetArrayLength().Should().BeGreaterThan(0, "Topics endpoint must return populated categories for the config page to display triggers");

        // Verify structure: each category should have id, name, and topics array
        foreach (var category in categories.EnumerateArray())
        {
            category.GetProperty("id").GetInt32().Should().BeGreaterThan(0);
            category.GetProperty("name").GetString().Should().NotBeNullOrEmpty();
            category.GetProperty("topics").ValueKind.Should().Be(JsonValueKind.Array);
        }
    }
}
