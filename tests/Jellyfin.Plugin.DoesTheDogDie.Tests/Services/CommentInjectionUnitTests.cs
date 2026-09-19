using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoesTheDogDie;
using DoesTheDogDie.Api;
using DoesTheDogDie.Statistics;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Services;
using Jellyfin.Plugin.DoesTheDogDie.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Services;

public class CommentInjectionUnitTests
{
    private static readonly FetchReason MovieFetch = new(DtddItemKind.Movie, false, false);

    private readonly FakeDtddClient _client = new();
    private readonly Mock<IPluginConfigurationAccessor> _configAccessor = new();

    public CommentInjectionUnitTests()
    {
        _client.Topics.Add(new Topic { Id = 201, Name = "a dog dies", TopicCategoryId = 3 });
        _client.Items[1234] = new ItemDetail
        {
            Id = 1234,
            Name = "John Wick",
            ItemTypeId = 15,
            ItemTypeName = "Movie",
            TopicItemStats = new[]
            {
                new TopicItemStat
                {
                    TopicItemId = 1, YesSum = 40, NoSum = 1, NumComments = 3,
                    TopicId = 201, TopicName = "a dog dies", ItemId = 1234,
                },
            },
        };
    }

    private DtddMetadataService CreateService() => new(
        () => _client,
        _configAccessor.Object,
        NullLogger<DtddMetadataService>.Instance,
        new OverviewFormatter());

    private void UseConfig(bool includeComments, int maxLength = 200)
    {
        _configAccessor.Setup(x => x.GetConfiguration()).Returns(new PluginConfiguration
        {
            ApiKey = "ddd_key",
            ShowAllTriggers = true,
            IncludeTopComment = includeComments,
            MaxCommentLength = maxLength,
        });
    }

    [Fact]
    public async Task ResolveAsync_DoesNotFetchRatings_WhenCommentsDisabled()
    {
        UseConfig(includeComments: false);

        var data = await CreateService().ResolveAsync("1234", null, null, null, MovieFetch, CancellationToken.None);

        Assert.Empty(data!.Comments);
        Assert.Empty(_client.GetRatingsCalls);
    }

    [Fact]
    public async Task ResolveAsync_FetchesTopCommentPerTopic_WhenEnabled()
    {
        UseConfig(includeComments: true);
        _client.Ratings[1234] = new[]
        {
            NewRating(1, topicId: 201, voteSum: 2, "a quieter note"),
            NewRating(2, topicId: 201, voteSum: 9, "the dog dies in the first ten minutes"),
        };

        var data = await CreateService().ResolveAsync("1234", null, null, null, MovieFetch, CancellationToken.None);

        Assert.Equal("the dog dies in the first ten minutes", data!.Comments[201]);
        Assert.Single(_client.GetRatingsCalls);
    }

    [Fact]
    public async Task ResolveAsync_TruncatesLongComments()
    {
        UseConfig(includeComments: true, maxLength: 10);
        _client.Ratings[1234] = new[] { NewRating(1, 201, 5, "a very long comment indeed") };

        var data = await CreateService().ResolveAsync("1234", null, null, null, MovieFetch, CancellationToken.None);

        Assert.EndsWith("...", data!.Comments[201], StringComparison.Ordinal);
        Assert.True(data.Comments[201].Length <= 13);
    }

    [Fact]
    public async Task ResolveAsync_SurvivesRatingsFailure()
    {
        UseConfig(includeComments: true);
        _client.ThrowOnGetRatings = new DtddMinuteRateLimitException(
            System.Net.HttpStatusCode.TooManyRequests, "rate_limit_exceeded", "slow down", null, null);

        var data = await CreateService().ResolveAsync("1234", null, null, null, MovieFetch, CancellationToken.None);

        Assert.NotNull(data);
        Assert.Empty(data!.Comments);
    }

    [Fact]
    public void FormatTriggerSummary_RendersCommentBelowItsTrigger()
    {
        var trigger = new TriggerInfo(
            new Topic { Id = 201, Name = "a dog dies", TopicCategoryId = 3 },
            40,
            1,
            TriggerConfidence.Compute(40, 1));
        var comments = new Dictionary<int, string> { [201] = "the dog dies early" };

        var summary = new OverviewFormatter().FormatTriggerSummary(
            new[] { trigger },
            new PluginConfiguration { ShowAllTriggers = true },
            comments);

        Assert.Contains("the dog dies early", summary, StringComparison.Ordinal);
    }

    private static Rating NewRating(int id, int topicId, int voteSum, string description) => new()
    {
        Id = id,
        Yes = 1,
        No = 0,
        VoteSum = voteSum,
        TriggerDescription = description,
        IsRampant = false,
        Index1 = -1,
        Index2 = -1,
        ItemId = 1234,
        TopicId = topicId,
        IsSceneAlert = false,
    };
}
