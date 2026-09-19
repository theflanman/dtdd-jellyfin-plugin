using System.Collections.Generic;
using System.Linq;
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
    [InlineData("TagPrefix")]
    [InlineData("SafeTagPrefix")]
    [InlineData("ShowAllTriggers")]
    [InlineData("CategoriesContainer")]
    [InlineData("ApiKey")]
    [InlineData("DecisionThreshold")]
    [InlineData("IntervalMass")]
    [InlineData("ItemCacheDays")]
    [InlineData("TaxonomyCacheDays")]
    [InlineData("TestKeyButton")]
    [InlineData("BudgetReadout")]
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
    public async Task TopicsEndpoint_ReturnsTaxonomyForConfigPage()
    {
        using var http = new System.Net.Http.HttpClient { BaseAddress = _fixture.JellyfinBaseAddress };
        http.DefaultRequestHeaders.Add("X-Emby-Token", _fixture.Client.AccessToken);

        using var resp = await http.GetAsync("/Plugins/DoesTheDogDie/Topics");
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "Topics endpoint must be accessible and return the trigger taxonomy");
        resp.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var json = await resp.Content.ReadAsStringAsync();
        var taxonomy = JsonSerializer.Deserialize<TaxonomyDto>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        taxonomy.Should().NotBeNull();

        // TaxonomyResponse is a flat pair of lists: categories, and topics carrying TopicCategoryId.
        taxonomy!.Categories.Should().NotBeEmpty("the config page needs categories to group triggers under");
        taxonomy.Topics.Should().NotBeEmpty("the config page needs topics to offer per-trigger selection");

        taxonomy.Categories.Should().AllSatisfy(c =>
        {
            c.Id.Should().BeGreaterThan(0);
            c.Name.Should().NotBeNullOrEmpty();
        });

        taxonomy.Topics.Should().AllSatisfy(t =>
        {
            t.Id.Should().BeGreaterThan(0);
            t.Name.Should().NotBeNullOrEmpty();
            taxonomy.Categories.Select(c => c.Id).Should().Contain(
                t.TopicCategoryId,
                "every topic must resolve to a category the page can nest it under");
        });
    }

    private sealed class TaxonomyDto
    {
        public List<CategoryDto> Categories { get; set; } = new();

        public List<TopicDto> Topics { get; set; } = new();
    }

    private sealed class CategoryDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public int TopicSuperCategoryId { get; set; }
    }

    private sealed class TopicDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public int TopicCategoryId { get; set; }
    }
}
