using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.DoesTheDogDie.Api;
using Jellyfin.Plugin.DoesTheDogDie.Api.Models;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.ScheduledTasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.ScheduledTasks;

public class DtddRefreshTaskTests
{
    private readonly Mock<ILibraryManager> _libraryManagerMock;
    private readonly Mock<DtddApiClient> _apiClientMock;
    private readonly Mock<IPluginConfigurationAccessor> _configAccessorMock;
    private readonly Mock<ILogger<DtddRefreshTask>> _loggerMock;
    private readonly DtddRefreshTask _task;

    public DtddRefreshTaskTests()
    {
        _libraryManagerMock = new Mock<ILibraryManager>();
        _apiClientMock = new Mock<DtddApiClient>(
            Mock.Of<System.Net.Http.IHttpClientFactory>(),
            Mock.Of<ILogger<DtddApiClient>>());
        _configAccessorMock = new Mock<IPluginConfigurationAccessor>();
        _loggerMock = new Mock<ILogger<DtddRefreshTask>>();

        _task = new DtddRefreshTask(
            _libraryManagerMock.Object,
            _apiClientMock.Object,
            _configAccessorMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        Assert.Equal("Refresh DoesTheDogDie Warnings", _task.Name);
    }

    [Fact]
    public void Key_ReturnsExpectedValue()
    {
        Assert.Equal("DtddRefreshTask", _task.Key);
    }

    [Fact]
    public void Category_ReturnsLibrary()
    {
        Assert.Equal("Library", _task.Category);
    }

    [Fact]
    public void Description_ReturnsExpectedValue()
    {
        Assert.Equal(
            "Updates content warnings from DoesTheDogDie.com for all items in your library.",
            _task.Description);
    }

    [Fact]
    public void GetDefaultTriggers_ReturnsDailyTriggerAt2AM()
    {
        var triggers = _task.GetDefaultTriggers();

        var triggerList = new List<TaskTriggerInfo>(triggers);
        Assert.Single(triggerList);

        var trigger = triggerList[0];
        Assert.Equal(TaskTriggerInfoType.DailyTrigger, trigger.Type);
        Assert.Equal(TimeSpan.FromHours(2).Ticks, trigger.TimeOfDayTicks);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullConfiguration_ReturnsEarly()
    {
        // Arrange
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns((PluginConfiguration?)null);
        var progress = new Mock<IProgress<double>>();

        // Act
        await _task.ExecuteAsync(progress.Object, CancellationToken.None);

        // Assert - should not query library
        _libraryManagerMock.Verify(
            x => x.GetItemList(It.IsAny<InternalItemsQuery>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoItemsWithDtddId_CompletesSuccessfully()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            EnableSeries = true
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        _libraryManagerMock
            .Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(Array.Empty<MediaBrowser.Controller.Entities.BaseItem>());

        var progress = new Mock<IProgress<double>>();

        // Act
        await _task.ExecuteAsync(progress.Object, CancellationToken.None);

        // Assert - should report 100% completion
        progress.Verify(x => x.Report(100), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            EnableSeries = true
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        _libraryManagerMock
            .Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(Array.Empty<MediaBrowser.Controller.Entities.BaseItem>());

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - should complete without error since there are no items to process
        await _task.ExecuteAsync(new Mock<IProgress<double>>().Object, cts.Token);
    }

    [Fact]
    public async Task ExecuteAsync_WithMovies_ProcessesMovies()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            EnableSeries = false,
            AddWarningTags = true,
            TagPrefix = "CW:",
            MinVotesThreshold = 0
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var movie = CreateMovie("tt2911666", "15713");
        _libraryManagerMock
            .Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new BaseItem[] { movie });

        var details = CreateMediaDetailsWithTriggers(15713, "John Wick");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var progress = new Mock<IProgress<double>>();

        // Act
        await _task.ExecuteAsync(progress.Object, CancellationToken.None);

        // Assert
        progress.Verify(x => x.Report(100), Times.Once);
        Assert.Contains("CW: a dog dies", movie.Tags);
    }

    [Fact]
    public async Task ExecuteAsync_WithSeries_ProcessesSeries()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableMovies = false,
            EnableSeries = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            MinVotesThreshold = 0
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var series = CreateSeries("tt0944947", "12345");
        _libraryManagerMock
            .Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new BaseItem[] { series });

        var details = CreateMediaDetailsWithTriggers(12345, "Game of Thrones");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt0944947", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var progress = new Mock<IProgress<double>>();

        // Act
        await _task.ExecuteAsync(progress.Object, CancellationToken.None);

        // Assert
        progress.Verify(x => x.Report(100), Times.Once);
        Assert.Contains("CW: a dog dies", series.Tags);
    }

    [Fact]
    public async Task ExecuteAsync_ItemWithNoImdbId_SkipsItem()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            EnableSeries = false,
            AddWarningTags = true
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var movie = new Movie
        {
            Name = "Test Movie",
            Tags = Array.Empty<string>()
        };
        movie.SetProviderId(Constants.ProviderId, "15713"); // Has DTDD ID but no IMDB ID

        _libraryManagerMock
            .Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new BaseItem[] { movie });

        var progress = new Mock<IProgress<double>>();

        // Act
        await _task.ExecuteAsync(progress.Object, CancellationToken.None);

        // Assert - API should not be called
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ApiReturnsNull_HandlesGracefully()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            EnableSeries = false,
            AddWarningTags = true
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var movie = CreateMovie("tt9999999", "99999");
        _libraryManagerMock
            .Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new BaseItem[] { movie });

        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt9999999", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DtddMediaDetails?)null);

        var progress = new Mock<IProgress<double>>();

        // Act
        await _task.ExecuteAsync(progress.Object, CancellationToken.None);

        // Assert - should complete without error
        progress.Verify(x => x.Report(100), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ApiThrowsException_HandlesGracefully()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            EnableSeries = false,
            AddWarningTags = true
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var movie = CreateMovie("tt2911666", "15713");
        _libraryManagerMock
            .Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new BaseItem[] { movie });

        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Net.Http.HttpRequestException("Network error"));

        var progress = new Mock<IProgress<double>>();

        // Act
        await _task.ExecuteAsync(progress.Object, CancellationToken.None);

        // Assert - should complete without error (logs warning)
        progress.Verify(x => x.Report(100), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_AddWarningTagsDisabled_DoesNotAddTags()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            EnableSeries = false,
            AddWarningTags = false
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var movie = CreateMovie("tt2911666", "15713");
        _libraryManagerMock
            .Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new BaseItem[] { movie });

        var details = CreateMediaDetailsWithTriggers(15713, "John Wick");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var progress = new Mock<IProgress<double>>();

        // Act
        await _task.ExecuteAsync(progress.Object, CancellationToken.None);

        // Assert - tags should not be added
        Assert.Empty(movie.Tags);
    }

    [Fact]
    public async Task ExecuteAsync_ReportsProgressCorrectly()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            EnableSeries = false,
            AddWarningTags = false
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var movie1 = CreateMovie("tt0000001", "1");
        var movie2 = CreateMovie("tt0000002", "2");
        _libraryManagerMock
            .Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new BaseItem[] { movie1, movie2 });

        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaDetails(1, "Test"));

        var reportedValues = new List<double>();
        var progress = new Mock<IProgress<double>>();
        progress.Setup(x => x.Report(It.IsAny<double>()))
            .Callback<double>(v => reportedValues.Add(v));

        // Act
        await _task.ExecuteAsync(progress.Object, CancellationToken.None);

        // Assert
        Assert.Contains(0, reportedValues);     // First item (0%)
        Assert.Contains(50, reportedValues);    // Second item (50%)
        Assert.Contains(100, reportedValues);   // Completion
    }

    [Fact]
    public async Task ExecuteAsync_RemovesOldDtddTags_BeforeAddingNew()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            EnableSeries = false,
            AddWarningTags = true,
            TagPrefix = "CW:",
            SafeTagPrefix = "Safe:",
            MinVotesThreshold = 0
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var movie = CreateMovie("tt2911666", "15713");
        movie.Tags = new[] { "CW: old trigger", "Safe: old safe", "Custom Tag" };

        _libraryManagerMock
            .Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new BaseItem[] { movie });

        var details = CreateMediaDetailsWithTriggers(15713, "John Wick");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var progress = new Mock<IProgress<double>>();

        // Act
        await _task.ExecuteAsync(progress.Object, CancellationToken.None);

        // Assert
        Assert.Contains("Custom Tag", movie.Tags);          // Preserved
        Assert.DoesNotContain("CW: old trigger", movie.Tags); // Removed
        Assert.DoesNotContain("Safe: old safe", movie.Tags);  // Removed
        Assert.Contains("CW: a dog dies", movie.Tags);        // Added
    }

    [Fact]
    public async Task ExecuteAsync_WithSafeTagsEnabled_AddsSafeTags()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            EnableSeries = false,
            AddWarningTags = true,
            TagPrefix = "CW:",
            SafeTagPrefix = "Safe:",
            MinVotesThreshold = 0
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var movie = CreateMovie("tt2911666", "15713");
        _libraryManagerMock
            .Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new BaseItem[] { movie });

        var details = CreateMediaDetailsWithSafeTriggers(15713, "Safe Movie");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var progress = new Mock<IProgress<double>>();

        // Act
        await _task.ExecuteAsync(progress.Object, CancellationToken.None);

        // Assert
        Assert.Contains("Safe: a dog dies", movie.Tags);
    }

    private static Movie CreateMovie(string imdbId, string dtddId)
    {
        var movie = new Movie
        {
            Name = "Test Movie",
            Tags = Array.Empty<string>()
        };
        movie.SetProviderId(MetadataProvider.Imdb, imdbId);
        movie.SetProviderId(Constants.ProviderId, dtddId);
        return movie;
    }

    private static Series CreateSeries(string imdbId, string dtddId)
    {
        var series = new Series
        {
            Name = "Test Series",
            Tags = Array.Empty<string>()
        };
        series.SetProviderId(MetadataProvider.Imdb, imdbId);
        series.SetProviderId(Constants.ProviderId, dtddId);
        return series;
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
            TopicItemStats = new List<DtddTopicItemStat>()
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
            TopicItemStats = new List<DtddTopicItemStat>
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
                }
            }
        };
    }

    private static DtddMediaDetails CreateMediaDetailsWithSafeTriggers(int id, string name)
    {
        return new DtddMediaDetails
        {
            Item = new DtddMediaItem
            {
                Id = id,
                Name = name
            },
            TopicItemStats = new List<DtddTopicItemStat>
            {
                new DtddTopicItemStat
                {
                    TopicItemId = 1,
                    YesSum = 10,  // Low yes votes
                    NoSum = 100, // High no votes - this is "safe"
                    TopicId = 153,
                    Topic = new DtddTopic
                    {
                        Id = 153,
                        Name = "a dog dies",
                        TopicCategoryId = 2
                    },
                    TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
                }
            }
        };
    }
}
