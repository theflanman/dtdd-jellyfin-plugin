using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.DoesTheDogDie.Api;
using Jellyfin.Plugin.DoesTheDogDie.Api.Models;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Providers;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Providers;

public class DtddEpisodeProviderTests
{
    private readonly Mock<DtddApiClient> _apiClientMock;
    private readonly Mock<IPluginConfigurationAccessor> _configAccessorMock;
    private readonly Mock<ILogger<DtddEpisodeProvider>> _loggerMock;
    private readonly DtddEpisodeProvider _provider;
    private readonly MetadataRefreshOptions _defaultOptions;

    public DtddEpisodeProviderTests()
    {
        _apiClientMock = new Mock<DtddApiClient>(
            Mock.Of<System.Net.Http.IHttpClientFactory>(),
            Mock.Of<ILogger<DtddApiClient>>());
        _configAccessorMock = new Mock<IPluginConfigurationAccessor>();
        _loggerMock = new Mock<ILogger<DtddEpisodeProvider>>();
        _provider = new DtddEpisodeProvider(
            _apiClientMock.Object,
            _configAccessorMock.Object,
            _loggerMock.Object);
        _defaultOptions = new MetadataRefreshOptions(Mock.Of<IDirectoryService>());
    }

    [Fact]
    public void Name_ReturnsProviderName()
    {
        Assert.Equal(Constants.ProviderName, _provider.Name);
    }

    [Fact]
    public void Order_ReturnsHighValue()
    {
        Assert.Equal(100, _provider.Order);
    }

    [Fact]
    public async Task FetchAsync_NoConfiguration_ReturnsNone()
    {
        // Arrange
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns((PluginConfiguration?)null);
        var episode = CreateEpisode();

        // Act
        var result = await _provider.FetchAsync(episode, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.None, result);
    }

    [Fact]
    public async Task FetchAsync_SeriesDisabled_ReturnsNone()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration { EnableSeries = false });
        var episode = CreateEpisode();

        // Act
        var result = await _provider.FetchAsync(episode, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.None, result);
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FetchAsync_DtddIdAlreadyExists_ReturnsNone()
    {
        // Arrange - DTDD ID exists so provider calls GetMediaDetailsAsync(12345) to re-fetch.
        // Mock is not set up for GetMediaDetailsAsync, so it returns null, which means no
        // details found and the provider returns None.
        SetupConfiguration(new PluginConfiguration { EnableSeries = true });
        var episode = CreateEpisode();
        episode.SetProviderId(Constants.ProviderId, "12345");

        // Act
        var result = await _provider.FetchAsync(episode, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.None, result);
        // Verify it called GetMediaDetailsAsync (the re-fetch path) instead of early-returning
        _apiClientMock.Verify(
            x => x.GetMediaDetailsAsync(12345, It.IsAny<CancellationToken>()),
            Times.Once);
        // Verify it did NOT call the IMDB-based lookup (no need to re-search)
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FetchAsync_NoParentSeries_ReturnsNone()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration { EnableSeries = true });
        var episode = new Episode
        {
            Name = "Episode 1",
            Tags = System.Array.Empty<string>()
        };
        // Note: episode.Series will be null since we don't set it

        // Act
        var result = await _provider.FetchAsync(episode, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.None, result);
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FetchAsync_ExistingDtddId_StillUpdatesTags()
    {
        // Arrange - Episode already has a DTDD ID. The provider should re-fetch details
        // via GetMediaDetailsAsync and update tags (not skip the episode).
        SetupConfiguration(new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            MinVotesThreshold = 0
        });
        var episode = CreateEpisode();
        episode.SetProviderId(Constants.ProviderId, "12345");

        var details = CreateMediaDetailsWithTriggers(12345, "Test Series");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(episode, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.Contains("CW: a dog dies", episode.Tags);
        // Verify it used GetMediaDetailsAsync (re-fetch by DTDD ID), not the IMDB search path
        _apiClientMock.Verify(
            x => x.GetMediaDetailsAsync(12345, It.IsAny<CancellationToken>()),
            Times.Once);
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FetchAsync_AdminUnticksTriggerCategory_RemovesStaleTag()
    {
        // Arrange - Episode has tags from a prior refresh that included both Animal and
        // Violence categories. Admin now disables the Animal category. The stale Animal
        // tag should be removed while the Violence tag is preserved.
        SetupConfiguration(new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            SafeTagPrefix = "Safe:",
            MinVotesThreshold = 0,
            ShowAllTriggers = false,
            EnabledCategoryIds = new List<int> { 3 } // Only Violence enabled
        });
        var episode = CreateEpisode();
        episode.SetProviderId(Constants.ProviderId, "12345");
        episode.Tags = new[] { "CW: a dog dies", "CW: blood/gore" };

        var details = CreateMediaDetailsWithMultipleTriggers(12345, "Test Series");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(episode, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.DoesNotContain("CW: a dog dies", episode.Tags); // Animal category disabled
        Assert.Contains("CW: blood/gore", episode.Tags); // Violence category still enabled
    }

    [Fact]
    public async Task FetchAsync_AddWarningTagsDisabled_RemovesExistingTags()
    {
        // Arrange - Episode has existing DTDD tags from a prior refresh. Admin disables
        // AddWarningTags. All DTDD-prefixed tags should be removed.
        SetupConfiguration(new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = false,
            TagPrefix = "CW:",
            SafeTagPrefix = "Safe:",
            MinVotesThreshold = 0
        });
        var episode = CreateEpisode();
        episode.SetProviderId(Constants.ProviderId, "12345");
        episode.Tags = new[] { "CW: a dog dies", "Safe: blood/gore", "Genre: Action" };

        var details = CreateMediaDetails(12345, "Test Series");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(episode, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.DoesNotContain("CW: a dog dies", episode.Tags);
        Assert.DoesNotContain("Safe: blood/gore", episode.Tags);
        // Non-DTDD tags should be preserved
        Assert.Contains("Genre: Action", episode.Tags);
    }

    [Fact]
    public async Task FetchAsync_PreservesNonDtddTags()
    {
        // Arrange - Episode has custom (non-DTDD) tags alongside DTDD tags. After a
        // re-fetch, the custom tags should be preserved while DTDD tags are rebuilt.
        SetupConfiguration(new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            SafeTagPrefix = "Safe:",
            MinVotesThreshold = 0
        });
        var episode = CreateEpisode();
        episode.SetProviderId(Constants.ProviderId, "12345");
        episode.Tags = new[] { "Genre: Drama", "Favorite", "CW: old stale tag" };

        var details = CreateMediaDetailsWithTriggers(12345, "Test Series");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(episode, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        // Custom tags preserved
        Assert.Contains("Genre: Drama", episode.Tags);
        Assert.Contains("Favorite", episode.Tags);
        // New DTDD tag added
        Assert.Contains("CW: a dog dies", episode.Tags);
        // Stale DTDD tag removed (it was stripped and not re-added by new trigger data)
        Assert.DoesNotContain("CW: old stale tag", episode.Tags);
    }

    [Fact]
    public async Task FetchAsync_CategoryFilter_OnlyIncludesEnabledCategories()
    {
        // Arrange - Tests that the episode provider now respects TriggerFilter
        // (it did not before the refactor). Only enabled categories produce tags.
        SetupConfiguration(new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            SafeTagPrefix = "Safe:",
            MinVotesThreshold = 0,
            ShowAllTriggers = false,
            EnabledCategoryIds = new List<int> { 3 } // Only Violence category (ID 3)
        });
        var episode = CreateEpisode();
        episode.SetProviderId(Constants.ProviderId, "12345");

        var details = CreateMediaDetailsWithMultipleTriggers(12345, "Test Series");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(episode, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.Contains("CW: blood/gore", episode.Tags); // Violence category enabled
        Assert.DoesNotContain("CW: a dog dies", episode.Tags); // Animal category not enabled
    }

    [Fact]
    public async Task FetchAsync_NegativeTriggers_AddsSafeTags()
    {
        // Arrange - Tests that the episode provider now adds Safe: prefix tags for
        // negative triggers (it did not before the refactor).
        SetupConfiguration(new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            SafeTagPrefix = "Safe:",
            MinVotesThreshold = 0
        });
        var episode = CreateEpisode();
        episode.SetProviderId(Constants.ProviderId, "12345");

        var details = CreateMediaDetailsWithSafeTriggers(12345, "Test Series");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(episode, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        // Negative trigger (NoSum > YesSum) should produce a Safe: tag
        Assert.Contains("Safe: a dog dies", episode.Tags);
        // Should NOT produce a CW: tag for a negative trigger
        Assert.DoesNotContain("CW: a dog dies", episode.Tags);
    }

    private void SetupConfiguration(PluginConfiguration config)
    {
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);
    }

    private static Episode CreateEpisode()
    {
        var episode = new Episode
        {
            Name = "Episode 1",
            Tags = System.Array.Empty<string>()
        };
        return episode;
    }

    private static DtddMediaDetails CreateMediaDetails(int id, string name)
    {
        return new DtddMediaDetails
        {
            Item = new DtddMediaItem { Id = id, Name = name },
            TopicItemStats = new List<DtddTopicItemStat>()
        };
    }

    private static DtddMediaDetails CreateMediaDetailsWithTriggers(int id, string name)
    {
        return new DtddMediaDetails
        {
            Item = new DtddMediaItem { Id = id, Name = name },
            TopicItemStats = new List<DtddTopicItemStat>
            {
                new DtddTopicItemStat
                {
                    TopicItemId = 1, YesSum = 100, NoSum = 10, TopicId = 153,
                    Topic = new DtddTopic { Id = 153, Name = "a dog dies", TopicCategoryId = 2 },
                    TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
                }
            }
        };
    }

    private static DtddMediaDetails CreateMediaDetailsWithMultipleTriggers(int id, string name)
    {
        return new DtddMediaDetails
        {
            Item = new DtddMediaItem { Id = id, Name = name },
            TopicItemStats = new List<DtddTopicItemStat>
            {
                new DtddTopicItemStat
                {
                    TopicItemId = 1, YesSum = 100, NoSum = 10, TopicId = 153,
                    Topic = new DtddTopic { Id = 153, Name = "a dog dies", TopicCategoryId = 2 },
                    TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
                },
                new DtddTopicItemStat
                {
                    TopicItemId = 2, YesSum = 100, NoSum = 10, TopicId = 101,
                    Topic = new DtddTopic { Id = 101, Name = "blood/gore", TopicCategoryId = 3 },
                    TopicCategory = new DtddTopicCategory { Id = 3, Name = "Violence" }
                }
            }
        };
    }

    private static DtddMediaDetails CreateMediaDetailsWithSafeTriggers(int id, string name)
    {
        return new DtddMediaDetails
        {
            Item = new DtddMediaItem { Id = id, Name = name },
            TopicItemStats = new List<DtddTopicItemStat>
            {
                new DtddTopicItemStat
                {
                    TopicItemId = 1, YesSum = 10, NoSum = 100, TopicId = 153,
                    Topic = new DtddTopic { Id = 153, Name = "a dog dies", TopicCategoryId = 2 },
                    TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
                }
            }
        };
    }
}
