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

public class DtddSeasonProviderTests
{
    private readonly Mock<DtddApiClient> _apiClientMock;
    private readonly Mock<IPluginConfigurationAccessor> _configAccessorMock;
    private readonly Mock<ILogger<DtddSeasonProvider>> _loggerMock;
    private readonly DtddSeasonProvider _provider;
    private readonly MetadataRefreshOptions _defaultOptions;

    public DtddSeasonProviderTests()
    {
        _apiClientMock = new Mock<DtddApiClient>(
            Mock.Of<System.Net.Http.IHttpClientFactory>(),
            Mock.Of<ILogger<DtddApiClient>>());
        _configAccessorMock = new Mock<IPluginConfigurationAccessor>();
        _loggerMock = new Mock<ILogger<DtddSeasonProvider>>();
        _provider = new DtddSeasonProvider(
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
        var season = CreateSeason();

        // Act
        var result = await _provider.FetchAsync(season, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.None, result);
    }

    [Fact]
    public async Task FetchAsync_SeriesDisabled_ReturnsNone()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration { EnableSeries = false });
        var season = CreateSeason();

        // Act
        var result = await _provider.FetchAsync(season, _defaultOptions, CancellationToken.None);

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
        var season = CreateSeason();
        season.SetProviderId(Constants.ProviderId, "12345");

        // Act
        var result = await _provider.FetchAsync(season, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.None, result);
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FetchAsync_NoParentSeries_ReturnsNone()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration { EnableSeries = true });
        var season = new Season
        {
            Name = "Season 1",
            Tags = System.Array.Empty<string>()
        };
        // Note: season.Series will be null since we don't set it

        // Act
        var result = await _provider.FetchAsync(season, _defaultOptions, CancellationToken.None);

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

    private static Season CreateSeason()
    {
        var season = new Season
        {
            Name = "Season 1",
            Tags = System.Array.Empty<string>()
        };
        return season;
    }
}
