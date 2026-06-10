using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.DoesTheDogDie.Api;
using Jellyfin.Plugin.DoesTheDogDie.Api.Models;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Providers;
using Jellyfin.Plugin.DoesTheDogDie.Services;
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
    private readonly Mock<OverviewFormatter> _overviewFormatterMock;
    private readonly Mock<ILogger<DtddSeriesProvider>> _loggerMock;
    private readonly DtddSeriesProvider _provider;
    private readonly MetadataRefreshOptions _defaultOptions;

    public DtddSeriesProviderTests()
    {
        _apiClientMock = new Mock<DtddApiClient>(
            Mock.Of<System.Net.Http.IHttpClientFactory>(),
            Mock.Of<ILogger<DtddApiClient>>());
        _configAccessorMock = new Mock<IPluginConfigurationAccessor>();
        _overviewFormatterMock = new Mock<OverviewFormatter>();
        _loggerMock = new Mock<ILogger<DtddSeriesProvider>>();
        _provider = new DtddSeriesProvider(
            _apiClientMock.Object,
            _configAccessorMock.Object,
            _overviewFormatterMock.Object,
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
        // Arrange
        SetupConfiguration(new PluginConfiguration { EnableSeries = true });
        var series = CreateSeries("tt0944947");
        series.SetProviderId(Constants.ProviderId, "12345");

        // Act
        var result = await _provider.FetchAsync(series, _defaultOptions, CancellationToken.None);

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

    [Fact]
    public async Task FetchAsync_AddDescriptionWarningsEnabled_UpdatesOverview()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = false,
            AddDescriptionWarnings = true,
            MinVotesThreshold = 0
        };
        SetupConfiguration(config);
        var series = CreateSeries("tt0944947");
        series.Overview = "Original description.";

        var details = CreateMediaDetailsWithTriggers(12345, "Game of Thrones");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt0944947", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        _overviewFormatterMock
            .Setup(x => x.FormatTriggerSummary(details, config))
            .Returns("\n**Content Warnings** (via DoesTheDogDie)\nTest trigger");

        _overviewFormatterMock
            .Setup(x => x.AppendToOverview("Original description.", It.IsAny<string>()))
            .Returns("Original description.\n\n<!-- DTDD_START -->\nTest trigger\n<!-- DTDD_END -->");

        // Act
        var result = await _provider.FetchAsync(series, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        _overviewFormatterMock.Verify(
            x => x.FormatTriggerSummary(details, config),
            Times.Once);
        _overviewFormatterMock.Verify(
            x => x.AppendToOverview("Original description.", It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchAsync_OverviewLocked_SkipsDescriptionInjection()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = false,
            AddDescriptionWarnings = true,
            MinVotesThreshold = 0
        };
        SetupConfiguration(config);
        var series = CreateSeries("tt0944947");
        series.Overview = "Original description.";
        series.LockedFields = new[] { MetadataField.Overview };

        var details = CreateMediaDetailsWithTriggers(12345, "Game of Thrones");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt0944947", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(series, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.Equal("Original description.", series.Overview);
        _overviewFormatterMock.Verify(
            x => x.FormatTriggerSummary(It.IsAny<DtddMediaDetails>(), It.IsAny<PluginConfiguration>()),
            Times.Never);
    }

    [Fact]
    public async Task FetchAsync_CachedDtddId_AddDescriptionWarningsEnabled_UpdatesOverview()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = false,
            AddDescriptionWarnings = true,
            MinVotesThreshold = 0
        };
        SetupConfiguration(config);
        var series = CreateSeries("tt0944947");
        series.SetProviderId(Constants.ProviderId, "12345");
        series.Overview = "Original description.";

        var details = CreateMediaDetailsWithTriggers(12345, "Game of Thrones");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        _overviewFormatterMock
            .Setup(x => x.FormatTriggerSummary(details, config))
            .Returns("\nTest trigger");
        _overviewFormatterMock
            .Setup(x => x.AppendToOverview("Original description.", It.IsAny<string>()))
            .Returns("Original description.\n\n<!-- DTDD_START -->\nTest trigger\n<!-- DTDD_END -->");

        // Act
        var result = await _provider.FetchAsync(series, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        _overviewFormatterMock.Verify(
            x => x.AppendToOverview("Original description.", It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchAsync_AddDescriptionWarningsDisabled_RemovesStaleSection()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = false,
            AddDescriptionWarnings = false,
            MinVotesThreshold = 0
        };
        SetupConfiguration(config);
        var series = CreateSeries("tt0944947");
        var staleOverview = "Original description.\n\n<!-- DTDD_START -->\nOld trigger\n<!-- DTDD_END -->";
        series.Overview = staleOverview;

        var details = CreateMediaDetailsWithTriggers(12345, "Game of Thrones");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt0944947", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        _overviewFormatterMock
            .Setup(x => x.HasDtddSection(staleOverview))
            .Returns(true);
        _overviewFormatterMock
            .Setup(x => x.RemoveDtddSection(staleOverview))
            .Returns("Original description.");

        // Act
        var result = await _provider.FetchAsync(series, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.Equal("Original description.", series.Overview);
    }

    [Fact]
    public async Task FetchAsync_AddDescriptionWarningsDisabled_DoesNotUpdateOverview()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = false,
            AddDescriptionWarnings = false,
            MinVotesThreshold = 0
        });
        var series = CreateSeries("tt0944947");
        series.Overview = "Original description.";

        var details = CreateMediaDetailsWithTriggers(12345, "Game of Thrones");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt0944947", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(series, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.Equal("Original description.", series.Overview);
        _overviewFormatterMock.Verify(
            x => x.FormatTriggerSummary(It.IsAny<DtddMediaDetails>(), It.IsAny<PluginConfiguration>()),
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
}
