using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DoesTheDogDie.Api;
using Jellyfin.Plugin.DoesTheDogDie.Api;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Services;
using Jellyfin.Plugin.DoesTheDogDie.Tests.Support;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Api;

public class DtddPluginControllerTests
{
    private readonly FakeDtddClient _client = new();
    private readonly Mock<IPluginConfigurationAccessor> _configAccessor = new();
    private readonly PluginConfiguration _configuration = new() { ApiKey = "ddd_key" };

    public DtddPluginControllerTests()
    {
        _client.Topics.Add(new Topic { Id = 201, Name = "a dog dies", TopicCategoryId = 3 });
        _client.Categories.Add(new TopicCategory { Id = 3, Name = "Animals", TopicSuperCategoryId = 1 });
        _configAccessor.Setup(x => x.GetConfiguration()).Returns(() => _configuration);
    }

    private DtddPluginController CreateController() =>
        new(() => _client, () => _client.CurrentBudget, _configAccessor.Object);

    [Fact]
    public async Task GetTopics_ReturnsCategoriesAndTopics()
    {
        var result = await CreateController().GetTopics(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<TaxonomyResponse>(ok.Value);
        Assert.Single(payload.Topics);
        Assert.Single(payload.Categories);
        Assert.Equal(3, payload.Topics[0].TopicCategoryId);
    }

    [Fact]
    public async Task TestKey_ReturnsSuccess_WhenTopicsFetchSucceeds()
    {
        var result = await CreateController().TestKey(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<KeyTestResponse>(ok.Value);
        Assert.True(payload.Success);
    }

    [Fact]
    public async Task TestKey_ReturnsFailureMessage_WhenKeyRejected()
    {
        _client.ThrowOnGetTopics = new DtddAuthenticationException(HttpStatusCode.Unauthorized, "invalid_api_key", "rejected ddd_secret", rateLimit: null);

        var result = await CreateController().TestKey(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<KeyTestResponse>(ok.Value);
        Assert.False(payload.Success);
        Assert.DoesNotContain("ddd_secret", payload.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestKey_RedactsConfiguredKey_EvenWhenNotDddShaped()
    {
        _configuration.ApiKey = "not-a-ddd-shaped-key";
        _client.ThrowOnGetTopics = new DtddAuthenticationException(
            HttpStatusCode.Unauthorized,
            "invalid_api_key",
            "rejected key not-a-ddd-shaped-key",
            rateLimit: null);

        var result = await CreateController().TestKey(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<KeyTestResponse>(ok.Value);
        Assert.False(payload.Success);
        Assert.DoesNotContain("not-a-ddd-shaped-key", payload.Message, System.StringComparison.Ordinal);
        Assert.Contains(LogSanitizer.Redacted, payload.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestKey_DoesNotThrow_WhenConfigurationUnavailable()
    {
        _configAccessor.Setup(x => x.GetConfiguration()).Returns((PluginConfiguration?)null);
        _client.ThrowOnGetTopics = new DtddAuthenticationException(
            HttpStatusCode.Unauthorized,
            "invalid_api_key",
            "rejected ddd_secret",
            rateLimit: null);

        var result = await CreateController().TestKey(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<KeyTestResponse>(ok.Value);
        Assert.DoesNotContain("ddd_secret", payload.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public void GetBudget_ReturnsNull_WhenNothingObserved()
    {
        var result = CreateController().GetBudget();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Null(ok.Value);
    }

    [Fact]
    public void Controller_RequiresElevation()
    {
        var attribute = Assert.Single(
            typeof(DtddPluginController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true))
            as AuthorizeAttribute;

        Assert.NotNull(attribute);
        Assert.Equal(Policies.RequiresElevation, attribute!.Policy);
    }
}
