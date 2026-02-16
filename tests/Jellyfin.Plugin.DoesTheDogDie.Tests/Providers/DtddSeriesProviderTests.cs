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

public class DtddSeriesProviderTests
{
    private readonly Mock<DtddApiClient> _apiClientMock;
    private readonly Mock<IPluginConfigurationAccessor> _configAccessorMock;
    private readonly Mock<ILogger<DtddSeriesProvider>> _loggerMock;
    private readonly DtddSeriesProvider _provider;
    private readonly MetadataRefreshOptions _defaultOptions;

    public DtddSeriesProviderTests()
    {
        _apiClientMock = new Mock<DtddApiClient>(
            Mock.Of<System.Net.Http.IHttpClientFactory>(),
            Mock.Of<ILogger<DtddApiClient>>());
        _configAccessorMock = new Mock<IPluginConfigurationAccessor>();
        _loggerMock = new Mock<ILogger<DtddSeriesProvider>>();
        _provider = new DtddSeriesProvider(
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
        var series = CreateSeries("tt0944947");

        // Act
        var result = await _provider.FetchAsync(series, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.None, result);
    }

    [Fact]
    public async Task FetchAsync_NoImdbId_ReturnsNone()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration { EnableSeries = true });
        var series = CreateSeries(null);

        // Act
        var result = await _provider.FetchAsync(series, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.None, result);
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FetchAsync_DtddIdAlreadyExists_ReturnsNone()
    {
        // Arrange - DTDD ID exists but GetMediaDetailsAsync is not mocked so returns null,
        // causing the provider to return None (no details to process).
        SetupConfiguration(new PluginConfiguration { EnableSeries = true });
        var series = CreateSeries("tt0944947");
        series.SetProviderId(Constants.ProviderId, "12345");

        // Act
        var result = await _provider.FetchAsync(series, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.None, result);
        _apiClientMock.Verify(
            x => x.GetMediaDetailsAsync(12345, It.IsAny<CancellationToken>()),
            Times.Once);
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FetchAsync_ExistingDtddId_StillUpdatesTags()
    {
        // Arrange - DTDD ID already exists; provider re-fetches details and updates tags
        SetupConfiguration(new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            MinVotesThreshold = 0
        });
        var series = CreateSeries("tt0944947");
        series.SetProviderId(Constants.ProviderId, "12345");

        var details = CreateMediaDetailsWithTriggers(12345, "Game of Thrones");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(series, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.Contains("CW: violence", series.Tags);
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
        // Arrange - Series already has tags from previous fetch including Violence category.
        // Admin changes config to only enable Animal category (id 2), disabling Violence (id 3).
        // The provider should remove the stale "CW: violence" tag.
        SetupConfiguration(new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            MinVotesThreshold = 0,
            ShowAllTriggers = false,
            EnabledCategoryIds = new System.Collections.Generic.List<int> { 2 } // Only Animal
        });
        var series = CreateSeries("tt0944947");
        series.SetProviderId(Constants.ProviderId, "12345");
        series.Tags = new[] { "CW: violence", "CW: blood/gore", "Custom Tag" };

        var details = CreateMediaDetailsWithMultipleTriggers(12345, "Game of Thrones");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(series, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.Contains("CW: a dog dies", series.Tags);         // Animal category - enabled
        Assert.DoesNotContain("CW: blood/gore", series.Tags);   // Violence category - not enabled, removed
        Assert.DoesNotContain("CW: violence", series.Tags);     // Old stale tag stripped by UpdateWarningTags
        Assert.Contains("Custom Tag", series.Tags);              // Non-DTDD tag preserved
    }

    [Fact]
    public async Task FetchAsync_AddWarningTagsDisabled_RemovesExistingTags()
    {
        // Arrange - Series has existing DTDD tags, but AddWarningTags is now false.
        // Provider should remove DTDD tags but preserve non-DTDD tags.
        SetupConfiguration(new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = false,
            TagPrefix = "CW:"
        });
        var series = CreateSeries("tt0944947");
        series.SetProviderId(Constants.ProviderId, "12345");
        series.Tags = new[] { "CW: violence", "Custom Tag" };

        var details = CreateMediaDetails(12345, "Game of Thrones");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(series, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.DoesNotContain("CW: violence", series.Tags);
        Assert.Contains("Custom Tag", series.Tags);
    }

    [Fact]
    public async Task FetchAsync_PreservesNonDtddTags()
    {
        // Arrange - Series has a custom non-DTDD tag. After fetching triggers,
        // the custom tag should be preserved alongside newly added DTDD tags.
        SetupConfiguration(new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            MinVotesThreshold = 0
        });
        var series = CreateSeries("tt0944947");
        series.SetProviderId(Constants.ProviderId, "12345");
        series.Tags = new[] { "Custom Tag" };

        var details = CreateMediaDetailsWithTriggers(12345, "Game of Thrones");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(series, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.Contains("Custom Tag", series.Tags);
        Assert.Contains("CW: violence", series.Tags);
    }

    [Fact]
    public async Task FetchAsync_DtddIdExists_ReplaceAllMetadata_FetchesData()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration { EnableSeries = true, AddWarningTags = false });
        var series = CreateSeries("tt0944947");
        series.SetProviderId(Constants.ProviderId, "12345");
        var options = new MetadataRefreshOptions(Mock.Of<IDirectoryService>())
        {
            ReplaceAllMetadata = true
        };

        var details = CreateMediaDetails(12345, "Game of Thrones");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt0944947", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(series, options, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync("tt0944947", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchAsync_ApiReturnsNull_ReturnsNone()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration { EnableSeries = true });
        var series = CreateSeries("tt9999999");

        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt9999999", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DtddMediaDetails?)null);

        // Act
        var result = await _provider.FetchAsync(series, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.None, result);
    }

    [Fact]
    public async Task FetchAsync_ApiReturnsData_StoresDtddId()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration { EnableSeries = true, AddWarningTags = false });
        var series = CreateSeries("tt0944947");

        var details = CreateMediaDetails(12345, "Game of Thrones");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt0944947", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(series, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.Equal("12345", series.GetProviderId(Constants.ProviderId));
    }

    [Fact]
    public async Task FetchAsync_AddWarningTagsEnabled_AddsTags()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            MinVotesThreshold = 0
        });
        var series = CreateSeries("tt0944947");

        var details = CreateMediaDetailsWithTriggers(12345, "Game of Thrones");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt0944947", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(series, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.Contains("CW: violence", series.Tags);
    }

    [Fact]
    public async Task FetchAsync_SeriesDisabled_ReturnsNone()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration { EnableSeries = false });
        var series = CreateSeries("tt0944947");

        // Act
        var result = await _provider.FetchAsync(series, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.None, result);
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void SetupConfiguration(PluginConfiguration config)
    {
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);
    }

    private static Series CreateSeries(string? imdbId)
    {
        var series = new Series
        {
            Name = "Test Series",
            Tags = System.Array.Empty<string>()
        };

        if (!string.IsNullOrEmpty(imdbId))
        {
            series.SetProviderId(MetadataProvider.Imdb, imdbId);
        }

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
                    YesSum = 500,
                    NoSum = 50,
                    TopicId = 101,
                    Topic = new DtddTopic
                    {
                        Id = 101,
                        Name = "violence"
                    }
                }
            }
        };
    }

    private static DtddMediaDetails CreateMediaDetailsWithMultipleTriggers(int id, string name)
    {
        return new DtddMediaDetails
        {
            Item = new DtddMediaItem { Id = id, Name = name },
            TopicItemStats = new System.Collections.Generic.List<DtddTopicItemStat>
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
}
