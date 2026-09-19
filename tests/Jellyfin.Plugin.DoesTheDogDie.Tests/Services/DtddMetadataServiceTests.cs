using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DoesTheDogDie;
using DoesTheDogDie.Api;
using DoesTheDogDie.Statistics;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Services;
using Jellyfin.Plugin.DoesTheDogDie.Tests.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Services;

public class DtddMetadataServiceTests
{
    private static readonly FetchReason MovieFetch = new(DtddItemKind.Movie, false, false);

    private readonly FakeDtddClient _client = new();
    private readonly Mock<IPluginConfigurationAccessor> _configAccessor = new();

    public DtddMetadataServiceTests()
    {
        _configAccessor.Setup(x => x.GetConfiguration())
            .Returns(new PluginConfiguration { ApiKey = "ddd_key" });

        _client.Topics.Add(new Topic { Id = 201, Name = "a dog dies", TopicCategoryId = 3 });
        _client.Categories.Add(new TopicCategory { Id = 3, Name = "Animals", TopicSuperCategoryId = 1 });
    }

    private DtddMetadataService CreateService(ILogger<DtddMetadataService>? logger = null) => new(
        () => _client,
        _configAccessor.Object,
        logger ?? NullLogger<DtddMetadataService>.Instance,
        new OverviewFormatter());

    private static ItemDetail DetailWith(int id, params TopicItemStat[] stats) => new()
    {
        Id = id,
        Name = "John Wick",
        ItemTypeId = 15,
        ItemTypeName = "Movie",
        TopicItemStats = stats,
    };

    private static TopicItemStat Stat(int topicId, int yes, int no) => new()
    {
        TopicItemId = 1,
        YesSum = yes,
        NoSum = no,
        NumComments = 0,
        TopicId = topicId,
        TopicName = "a dog dies",
        ItemId = 1234,
    };

    [Fact]
    public async Task ResolveAsync_UsesStoredId_WithoutSearching()
    {
        _client.Items[1234] = DetailWith(1234, Stat(201, 18, 2));

        var result = await CreateService().ResolveAsync("1234", "tt2911666", "John Wick", 2014, MovieFetch, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1234, result!.ItemId);
        Assert.Empty(_client.SearchCalls);
    }

    [Fact]
    public async Task ResolveAsync_FallsBackToImdbSearch_WhenNoStoredId()
    {
        _client.SearchResults["?imdb=tt2911666"] = new[] { new Item { Id = 1234, Name = "John Wick", ItemTypeId = 15, ItemTypeName = "Movie" } };
        _client.Items[1234] = DetailWith(1234, Stat(201, 18, 2));

        var result = await CreateService().ResolveAsync(null, "tt2911666", "John Wick", 2014, MovieFetch, CancellationToken.None);

        Assert.Equal(1234, result!.ItemId);
        Assert.Contains("?imdb=tt2911666", _client.SearchCalls);
    }

    [Fact]
    public async Task ResolveAsync_FallsBackToNameAndYear_WhenImdbMisses()
    {
        _client.SearchResults["?name=John%20Wick&releaseYear=2014"] =
            new[] { new Item { Id = 1234, Name = "John Wick", ItemTypeId = 15, ItemTypeName = "Movie" } };
        _client.Items[1234] = DetailWith(1234, Stat(201, 18, 2));

        var result = await CreateService().ResolveAsync(null, "tt0000000", "John Wick", 2014, MovieFetch, CancellationToken.None);

        Assert.Equal(1234, result!.ItemId);
    }

    [Fact]
    public async Task ResolveAsync_NeverIssuesFreeTextSearch()
    {
        var result = await CreateService().ResolveAsync(null, null, "John Wick", null, MovieFetch, CancellationToken.None);

        Assert.Null(result);
        Assert.DoesNotContain(_client.SearchCalls, c => c.StartsWith("?q=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResolveAsync_JoinsStatsToTaxonomyTopics()
    {
        _client.Items[1234] = DetailWith(1234, Stat(201, 18, 2));

        var result = await CreateService().ResolveAsync("1234", null, null, null, MovieFetch, CancellationToken.None);

        var trigger = Assert.Single(result!.Triggers);
        Assert.Equal(201, trigger.Topic.Id);
        Assert.Equal(3, trigger.CategoryId);
        Assert.Equal(TriggerVerdict.LikelyPresent, trigger.Verdict);
    }

    [Fact]
    public async Task ResolveAsync_DropsStatsWithNoMatchingTopic()
    {
        _client.Items[1234] = DetailWith(1234, Stat(999, 18, 2));

        var result = await CreateService().ResolveAsync("1234", null, null, null, MovieFetch, CancellationToken.None);

        Assert.Empty(result!.Triggers);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNull_WhenApiKeyMissing()
    {
        _configAccessor.Setup(x => x.GetConfiguration())
            .Returns(new PluginConfiguration { ApiKey = string.Empty });
        _client.Items[1234] = DetailWith(1234, Stat(201, 18, 2));

        var result = await CreateService().ResolveAsync("1234", null, null, null, MovieFetch, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNull_OnNotFound()
    {
        _client.ThrowOnGetItem = new DtddNotFoundException(HttpStatusCode.NotFound, "not_found", "no such item", null);

        var result = await CreateService().ResolveAsync("1234", null, null, null, MovieFetch, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNull_OnMonthlyRateLimit()
    {
        _client.ThrowOnGetItem = new DtddMonthlyRateLimitException(
            HttpStatusCode.TooManyRequests, "monthly_limit_exceeded", "out of budget", null, null);

        var result = await CreateService().ResolveAsync("1234", null, null, null, MovieFetch, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_KeepsServingAfterMonthlyLimit_WhenCacheAnswers()
    {
        var service = CreateService();
        _client.ThrowOnGetItem = new DtddMonthlyRateLimitException(
            HttpStatusCode.TooManyRequests, "monthly_limit_exceeded", "out of budget", null, null);
        Assert.Null(await service.ResolveAsync("1234", null, null, null, MovieFetch, CancellationToken.None));

        // A later item that the cache can answer must still be attempted and must still resolve.
        _client.ThrowOnGetItem = null;
        _client.Items[5678] = DetailWith(5678, Stat(201, 18, 2));

        var result = await service.ResolveAsync("5678", null, null, null, MovieFetch, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(5678, result!.ItemId);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNull_OnAuthenticationFailure()
    {
        _client.ThrowOnGetItem = new DtddAuthenticationException(HttpStatusCode.Unauthorized, "invalid_api_key", "bad key", null);

        var result = await CreateService().ResolveAsync("1234", null, null, null, MovieFetch, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNull_OnQueueFull()
    {
        _client.ThrowOnGetItem = new DtddQueueFullException(10);

        var result = await CreateService().ResolveAsync("1234", null, null, null, MovieFetch, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNull_OnUnexpectedException()
    {
        _client.ThrowOnGetItem = new InvalidOperationException("boom ddd_secret999");

        var result = await CreateService().ResolveAsync("1234", null, null, null, MovieFetch, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNull_OnObjectDisposed()
    {
        _client.ThrowOnGetItem = new ObjectDisposedException(nameof(FakeDtddClient));

        var result = await CreateService().ResolveAsync("1234", null, null, null, MovieFetch, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_DoesNotSwallowCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _client.ThrowOnGetItem = new OperationCanceledException(cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService().ResolveAsync("1234", null, null, null, MovieFetch, cts.Token));
    }

    [Fact]
    public async Task ResolveAsync_WarnsOnlyOnce_ForRepeatedMonthlyRateLimit()
    {
        var logger = new CapturingLogger<DtddMetadataService>();
        var service = CreateService(logger);
        _client.ThrowOnGetItem = new DtddMonthlyRateLimitException(
            HttpStatusCode.TooManyRequests, "monthly_limit_exceeded", "out of budget", null, null);

        Assert.Null(await service.ResolveAsync("1234", null, null, null, MovieFetch, CancellationToken.None));
        Assert.Null(await service.ResolveAsync("1234", null, null, null, MovieFetch, CancellationToken.None));

        Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
    }
}
