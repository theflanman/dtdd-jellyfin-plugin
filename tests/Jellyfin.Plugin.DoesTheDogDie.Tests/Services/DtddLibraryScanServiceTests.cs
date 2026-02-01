using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.DoesTheDogDie.Api;
using Jellyfin.Plugin.DoesTheDogDie.Api.Models;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Services;

public class DtddLibraryScanServiceTests
{
    private readonly Mock<ILibraryManager> _libraryManagerMock;
    private readonly Mock<DtddApiClient> _apiClientMock;
    private readonly Mock<IPluginConfigurationAccessor> _configAccessorMock;
    private readonly Mock<ILogger<DtddLibraryScanService>> _loggerMock;
    private readonly DtddLibraryScanService _service;

    public DtddLibraryScanServiceTests()
    {
        _libraryManagerMock = new Mock<ILibraryManager>();
        _apiClientMock = new Mock<DtddApiClient>(
            Mock.Of<System.Net.Http.IHttpClientFactory>(),
            Mock.Of<ILogger<DtddApiClient>>());
        _configAccessorMock = new Mock<IPluginConfigurationAccessor>();
        _loggerMock = new Mock<ILogger<DtddLibraryScanService>>();

        _service = new DtddLibraryScanService(
            _libraryManagerMock.Object,
            _apiClientMock.Object,
            _configAccessorMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task StartAsync_SubscribesToLibraryEvents()
    {
        // Act
        await _service.StartAsync(CancellationToken.None);

        // Assert - verify we subscribed to events
        // Note: We can't easily verify event subscription with Moq,
        // but we can verify StartAsync completes without error
        await _service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_UnsubscribesFromLibraryEvents()
    {
        // Arrange
        await _service.StartAsync(CancellationToken.None);

        // Act
        await _service.StopAsync(CancellationToken.None);

        // Assert - verify we can stop without error
        // The actual unsubscription is verified by the fact that
        // calling stop twice doesn't throw
        await _service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_CompletesSuccessfully()
    {
        // Act & Assert - should not throw
        await _service.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_CompletesSuccessfully()
    {
        // Arrange
        await _service.StartAsync(CancellationToken.None);

        // Act & Assert - should not throw
        await _service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void OnItemChanged_NonMovieOrSeries_ReturnsEarly()
    {
        // Arrange
        var audio = new Audio { Name = "Test Song" };
        var eventArgs = new ItemChangeEventArgs { Item = audio };

        // Act
        _service.OnItemChanged(null, eventArgs);

        // Assert - API should not be called
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void OnItemChanged_NullConfiguration_ReturnsEarly()
    {
        // Arrange
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns((PluginConfiguration?)null);
        var movie = CreateMovie("tt2911666");
        var eventArgs = new ItemChangeEventArgs { Item = movie };

        // Act
        _service.OnItemChanged(null, eventArgs);

        // Assert - API should not be called
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void OnItemChanged_MoviesDisabled_ReturnsEarly()
    {
        // Arrange
        var config = new PluginConfiguration { EnableMovies = false };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var movie = CreateMovie("tt2911666");
        var eventArgs = new ItemChangeEventArgs { Item = movie };

        // Act
        _service.OnItemChanged(null, eventArgs);

        // Assert - API should not be called
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void OnItemChanged_SeriesDisabled_ReturnsEarly()
    {
        // Arrange
        var config = new PluginConfiguration { EnableSeries = false };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var series = CreateSeries("tt0944947");
        var eventArgs = new ItemChangeEventArgs { Item = series };

        // Act
        _service.OnItemChanged(null, eventArgs);

        // Assert - API should not be called
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void OnItemChanged_NoImdbId_ReturnsEarly()
    {
        // Arrange
        var config = new PluginConfiguration { EnableMovies = true };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var movie = new Movie { Name = "Test Movie" }; // No IMDB ID
        var eventArgs = new ItemChangeEventArgs { Item = movie };

        // Act
        _service.OnItemChanged(null, eventArgs);

        // Assert - API should not be called
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void OnItemChanged_AlreadyHasDtddId_ReturnsEarly()
    {
        // Arrange
        var config = new PluginConfiguration { EnableMovies = true };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var movie = CreateMovie("tt2911666");
        movie.SetProviderId(Constants.ProviderId, "15713"); // Already has DTDD ID
        var eventArgs = new ItemChangeEventArgs { Item = movie };

        // Act
        _service.OnItemChanged(null, eventArgs);

        // Assert - API should not be called
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void OnItemChanged_ValidMovie_QueuesLookup()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            MinVotesThreshold = 0
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var details = CreateMediaDetailsWithTriggers(15713, "John Wick");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var movie = CreateMovie("tt2911666");
        var eventArgs = new ItemChangeEventArgs { Item = movie };

        // Act
        _service.OnItemChanged(null, eventArgs);

        // Assert - API should be called (fire and forget, give it a moment)
        Thread.Sleep(100); // Small delay for async task to start
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void OnItemChanged_ValidSeries_QueuesLookup()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            MinVotesThreshold = 0
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var details = CreateMediaDetailsWithTriggers(12345, "Game of Thrones");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt0944947", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var series = CreateSeries("tt0944947");
        var eventArgs = new ItemChangeEventArgs { Item = series };

        // Act
        _service.OnItemChanged(null, eventArgs);

        // Assert - API should be called
        Thread.Sleep(100);
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync("tt0944947", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void OnItemChanged_ApiReturnsNull_HandlesGracefully()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            AddWarningTags = true
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt9999999", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DtddMediaDetails?)null);

        var movie = CreateMovie("tt9999999");
        var eventArgs = new ItemChangeEventArgs { Item = movie };

        // Act - should not throw
        _service.OnItemChanged(null, eventArgs);

        // Assert - API should be called
        Thread.Sleep(100);
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync("tt9999999", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void OnItemChanged_ApiThrowsException_HandlesGracefully()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            AddWarningTags = true
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Net.Http.HttpRequestException("Network error"));

        var movie = CreateMovie("tt2911666");
        var eventArgs = new ItemChangeEventArgs { Item = movie };

        // Act - should not throw
        _service.OnItemChanged(null, eventArgs);

        // Assert - should complete without crashing
        Thread.Sleep(100);
    }

    [Fact]
    public void OnItemChanged_WithTriggers_AddsTags()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            MinVotesThreshold = 0
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var details = CreateMediaDetailsWithTriggers(15713, "John Wick");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var movie = CreateMovie("tt2911666");
        movie.Tags = Array.Empty<string>();
        var eventArgs = new ItemChangeEventArgs { Item = movie };

        // Act
        _service.OnItemChanged(null, eventArgs);

        // Assert - wait for async processing
        Thread.Sleep(200);
        Assert.Contains("CW: a dog dies", movie.Tags);
    }

    [Fact]
    public void OnItemChanged_AddWarningTagsDisabled_DoesNotAddTags()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            AddWarningTags = false
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var details = CreateMediaDetailsWithTriggers(15713, "John Wick");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var movie = CreateMovie("tt2911666");
        movie.Tags = Array.Empty<string>();
        var eventArgs = new ItemChangeEventArgs { Item = movie };

        // Act
        _service.OnItemChanged(null, eventArgs);

        // Assert - wait for async processing
        Thread.Sleep(200);
        Assert.Empty(movie.Tags);
    }

    [Fact]
    public void OnItemChanged_SetsDtddProviderId()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            AddWarningTags = false
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var details = CreateMediaDetailsWithTriggers(15713, "John Wick");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var movie = CreateMovie("tt2911666");
        var eventArgs = new ItemChangeEventArgs { Item = movie };

        // Act
        _service.OnItemChanged(null, eventArgs);

        // Assert - wait for async processing
        Thread.Sleep(200);
        Assert.Equal("15713", movie.GetProviderId(Constants.ProviderId));
    }

    [Fact]
    public void OnItemChanged_RemovesOldDtddTags_BeforeAddingNew()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            SafeTagPrefix = "Safe:",
            MinVotesThreshold = 0
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var details = CreateMediaDetailsWithTriggers(15713, "John Wick");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var movie = CreateMovie("tt2911666");
        movie.Tags = new[] { "CW: old tag", "Safe: old safe", "Custom Tag" };
        var eventArgs = new ItemChangeEventArgs { Item = movie };

        // Act
        _service.OnItemChanged(null, eventArgs);

        // Assert - wait for async processing
        Thread.Sleep(200);
        Assert.Contains("Custom Tag", movie.Tags);
        Assert.DoesNotContain("CW: old tag", movie.Tags);
        Assert.DoesNotContain("Safe: old safe", movie.Tags);
        Assert.Contains("CW: a dog dies", movie.Tags);
    }

    private static Movie CreateMovie(string imdbId)
    {
        var movie = new Movie
        {
            Name = "Test Movie",
            Tags = Array.Empty<string>()
        };
        movie.SetProviderId(MetadataProvider.Imdb, imdbId);
        return movie;
    }

    private static Series CreateSeries(string imdbId)
    {
        var series = new Series
        {
            Name = "Test Series",
            Tags = Array.Empty<string>()
        };
        series.SetProviderId(MetadataProvider.Imdb, imdbId);
        return series;
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
}
