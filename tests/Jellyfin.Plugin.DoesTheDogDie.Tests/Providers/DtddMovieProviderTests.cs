using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.DoesTheDogDie.Api;
using Jellyfin.Plugin.DoesTheDogDie.Api.Models;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Providers;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Providers;

public class DtddMovieProviderTests
{
    private readonly Mock<DtddApiClient> _apiClientMock;
    private readonly Mock<IPluginConfigurationAccessor> _configAccessorMock;
    private readonly Mock<ILogger<DtddMovieProvider>> _loggerMock;
    private readonly DtddMovieProvider _provider;
    private readonly MetadataRefreshOptions _defaultOptions;

    public DtddMovieProviderTests()
    {
        _apiClientMock = new Mock<DtddApiClient>(
            Mock.Of<System.Net.Http.IHttpClientFactory>(),
            Mock.Of<ILogger<DtddApiClient>>());
        _configAccessorMock = new Mock<IPluginConfigurationAccessor>();
        _loggerMock = new Mock<ILogger<DtddMovieProvider>>();
        _provider = new DtddMovieProvider(
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
        var movie = CreateMovie("tt2911666");

        // Act
        var result = await _provider.FetchAsync(movie, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.None, result);
    }

    [Fact]
    public async Task FetchAsync_NoImdbId_ReturnsNone()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration { EnableMovies = true });
        var movie = CreateMovie(null);

        // Act
        var result = await _provider.FetchAsync(movie, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.None, result);
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FetchAsync_DtddIdAlreadyExists_ReturnsNone()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration { EnableMovies = true });
        var movie = CreateMovie("tt2911666");
        movie.SetProviderId(Constants.ProviderId, "15713");

        // Act
        var result = await _provider.FetchAsync(movie, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.None, result);
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FetchAsync_DtddIdExists_ReplaceAllMetadata_FetchesData()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration { EnableMovies = true, AddWarningTags = false });
        var movie = CreateMovie("tt2911666");
        movie.SetProviderId(Constants.ProviderId, "15713");
        var options = new MetadataRefreshOptions(Mock.Of<IDirectoryService>())
        {
            ReplaceAllMetadata = true
        };

        var details = CreateMediaDetails(15713, "John Wick");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(movie, options, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchAsync_ApiReturnsNull_ReturnsNone()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration { EnableMovies = true });
        var movie = CreateMovie("tt9999999");

        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt9999999", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DtddMediaDetails?)null);

        // Act
        var result = await _provider.FetchAsync(movie, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.None, result);
    }

    [Fact]
    public async Task FetchAsync_ApiReturnsData_StoresDtddId()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration { EnableMovies = true, AddWarningTags = false });
        var movie = CreateMovie("tt2911666");

        var details = CreateMediaDetails(15713, "John Wick");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(movie, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.Equal("15713", movie.GetProviderId(Constants.ProviderId));
    }

    [Fact]
    public async Task FetchAsync_AddWarningTagsEnabled_AddsTags()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration
        {
            EnableMovies = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            MinVotesThreshold = 0
        });
        var movie = CreateMovie("tt2911666");

        var details = CreateMediaDetailsWithTriggers(15713, "John Wick");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(movie, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.Contains("CW: a dog dies", movie.Tags);
    }

    [Fact]
    public async Task FetchAsync_MinVotesThreshold_FiltersTriggers()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration
        {
            EnableMovies = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            MinVotesThreshold = 1000
        });
        var movie = CreateMovie("tt2911666");

        var details = CreateMediaDetailsWithTriggers(15713, "John Wick");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(movie, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        // "a dog dies" has 1454 votes (1336+118), should be included
        Assert.Contains("CW: a dog dies", movie.Tags);
        // "low vote trigger" has only 10 votes, should be excluded
        Assert.DoesNotContain("CW: low vote trigger", movie.Tags);
    }

    [Fact]
    public async Task FetchAsync_MoviesDisabled_ReturnsNone()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration { EnableMovies = false });
        var movie = CreateMovie("tt2911666");

        // Act
        var result = await _provider.FetchAsync(movie, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.None, result);
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FetchAsync_NoDuplicateTags()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration
        {
            EnableMovies = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            MinVotesThreshold = 0
        });
        var movie = CreateMovie("tt2911666");
        movie.Tags = new[] { "CW: a dog dies" }; // Pre-existing tag

        var details = CreateMediaDetailsWithTriggers(15713, "John Wick");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(movie, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.Single(movie.Tags, t => t == "CW: a dog dies");
    }

    private void SetupConfiguration(PluginConfiguration config)
    {
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);
    }

    private static Movie CreateMovie(string? imdbId)
    {
        var movie = new Movie
        {
            Name = "Test Movie",
            Tags = System.Array.Empty<string>()
        };

        if (!string.IsNullOrEmpty(imdbId))
        {
            movie.SetProviderId(MetadataProvider.Imdb, imdbId);
        }

        return movie;
    }

    private static DtddMediaDetails CreateMediaDetails(int id, string name)
    {
        return new DtddMediaDetails
        {
            Item = new DtddMediaItem
            {
                Id = id,
                Name = name
            },
            TopicItemStats = new System.Collections.Generic.List<DtddTopicItemStat>()
        };
    }

    private static DtddMediaDetails CreateMediaDetailsWithTriggers(int id, string name)
    {
        return new DtddMediaDetails
        {
            Item = new DtddMediaItem
            {
                Id = id,
                Name = name
            },
            TopicItemStats = new System.Collections.Generic.List<DtddTopicItemStat>
            {
                new DtddTopicItemStat
                {
                    TopicItemId = 1,
                    YesSum = 1336,
                    NoSum = 118,
                    TopicId = 153,
                    Topic = new DtddTopic
                    {
                        Id = 153,
                        Name = "a dog dies",
                        TopicCategoryId = 2
                    },
                    TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
                },
                new DtddTopicItemStat
                {
                    TopicItemId = 2,
                    YesSum = 8,
                    NoSum = 2,
                    TopicId = 999,
                    Topic = new DtddTopic
                    {
                        Id = 999,
                        Name = "low vote trigger",
                        TopicCategoryId = 3
                    },
                    TopicCategory = new DtddTopicCategory { Id = 3, Name = "Violence" }
                }
            }
        };
    }

    private static DtddMediaDetails CreateMediaDetailsWithMultipleTriggers(int id, string name)
    {
        return new DtddMediaDetails
        {
            Item = new DtddMediaItem
            {
                Id = id,
                Name = name
            },
            TopicItemStats = new System.Collections.Generic.List<DtddTopicItemStat>
            {
                new DtddTopicItemStat
                {
                    TopicItemId = 1,
                    YesSum = 100,
                    NoSum = 10,
                    TopicId = 153,
                    Topic = new DtddTopic
                    {
                        Id = 153,
                        Name = "a dog dies",
                        TopicCategoryId = 2
                    },
                    TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
                },
                new DtddTopicItemStat
                {
                    TopicItemId = 2,
                    YesSum = 100,
                    NoSum = 10,
                    TopicId = 154,
                    Topic = new DtddTopic
                    {
                        Id = 154,
                        Name = "a cat dies",
                        TopicCategoryId = 2
                    },
                    TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
                },
                new DtddTopicItemStat
                {
                    TopicItemId = 3,
                    YesSum = 100,
                    NoSum = 10,
                    TopicId = 101,
                    Topic = new DtddTopic
                    {
                        Id = 101,
                        Name = "blood/gore",
                        TopicCategoryId = 3
                    },
                    TopicCategory = new DtddTopicCategory { Id = 3, Name = "Violence" }
                }
            }
        };
    }

    [Fact]
    public async Task FetchAsync_CategoryFilterEnabled_OnlyIncludesEnabledCategories()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration
        {
            EnableMovies = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            MinVotesThreshold = 0,
            ShowAllTriggers = false,
            EnabledCategoryIds = new System.Collections.Generic.List<int> { 2 } // Only Animal
        });
        var movie = CreateMovie("tt2911666");

        var details = CreateMediaDetailsWithMultipleTriggers(15713, "John Wick");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(movie, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.Contains("CW: a dog dies", movie.Tags);
        Assert.Contains("CW: a cat dies", movie.Tags);
        Assert.DoesNotContain("CW: blood/gore", movie.Tags); // Violence category not enabled
    }

    [Fact]
    public async Task FetchAsync_TopicFilterEnabled_OnlyIncludesEnabledTopics()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration
        {
            EnableMovies = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            MinVotesThreshold = 0,
            ShowAllTriggers = false,
            EnabledCategoryIds = new System.Collections.Generic.List<int> { 2 },
            EnabledTopicIds = new System.Collections.Generic.List<int> { 153 } // Only dog dies
        });
        var movie = CreateMovie("tt2911666");

        var details = CreateMediaDetailsWithMultipleTriggers(15713, "John Wick");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(movie, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.Contains("CW: a dog dies", movie.Tags);
        Assert.DoesNotContain("CW: a cat dies", movie.Tags); // Topic not enabled
        Assert.DoesNotContain("CW: blood/gore", movie.Tags); // Category not enabled
    }

    [Fact]
    public async Task FetchAsync_ShowAllTriggers_IncludesAllTriggers()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration
        {
            EnableMovies = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            MinVotesThreshold = 0,
            ShowAllTriggers = true,
            EnabledCategoryIds = new System.Collections.Generic.List<int> { 2 } // This should be ignored
        });
        var movie = CreateMovie("tt2911666");

        var details = CreateMediaDetailsWithMultipleTriggers(15713, "John Wick");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(movie, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.Contains("CW: a dog dies", movie.Tags);
        Assert.Contains("CW: a cat dies", movie.Tags);
        Assert.Contains("CW: blood/gore", movie.Tags); // All triggers included
    }

    [Fact]
    public async Task FetchAsync_NoCategoriesSelected_IncludesAllTriggers()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration
        {
            EnableMovies = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            MinVotesThreshold = 0,
            ShowAllTriggers = false,
            EnabledCategoryIds = new System.Collections.Generic.List<int>() // Empty - no categories selected
        });
        var movie = CreateMovie("tt2911666");

        var details = CreateMediaDetailsWithMultipleTriggers(15713, "John Wick");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(movie, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        // All triggers included (with warning in UI)
        Assert.Contains("CW: a dog dies", movie.Tags);
        Assert.Contains("CW: a cat dies", movie.Tags);
        Assert.Contains("CW: blood/gore", movie.Tags);
    }
}
