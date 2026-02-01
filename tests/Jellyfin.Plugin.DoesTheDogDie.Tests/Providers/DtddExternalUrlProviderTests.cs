using System.Linq;
using Jellyfin.Plugin.DoesTheDogDie.Providers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Episode = MediaBrowser.Controller.Entities.TV.Episode;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Providers;

public class DtddExternalUrlProviderTests
{
    private readonly DtddExternalUrlProvider _provider;

    public DtddExternalUrlProviderTests()
    {
        _provider = new DtddExternalUrlProvider();
    }

    [Fact]
    public void Name_ReturnsProviderName()
    {
        Assert.Equal(Constants.ProviderName, _provider.Name);
    }

    [Fact]
    public void GetExternalUrls_MovieWithDtddId_ReturnsUrl()
    {
        // Arrange
        var movie = new Movie { Name = "Test Movie" };
        movie.SetProviderId(Constants.ProviderId, "15713");

        // Act
        var urls = _provider.GetExternalUrls(movie).ToList();

        // Assert
        Assert.Single(urls);
        Assert.Equal("https://www.doesthedogdie.com/media/15713", urls[0]);
    }

    [Fact]
    public void GetExternalUrls_SeriesWithDtddId_ReturnsUrl()
    {
        // Arrange
        var series = new Series { Name = "Test Series" };
        series.SetProviderId(Constants.ProviderId, "12345");

        // Act
        var urls = _provider.GetExternalUrls(series).ToList();

        // Assert
        Assert.Single(urls);
        Assert.Equal("https://www.doesthedogdie.com/media/12345", urls[0]);
    }

    [Fact]
    public void GetExternalUrls_MovieWithoutDtddId_ReturnsEmpty()
    {
        // Arrange
        var movie = new Movie { Name = "Test Movie" };

        // Act
        var urls = _provider.GetExternalUrls(movie).ToList();

        // Assert
        Assert.Empty(urls);
    }

    [Fact]
    public void GetExternalUrls_SeriesWithoutDtddId_ReturnsEmpty()
    {
        // Arrange
        var series = new Series { Name = "Test Series" };

        // Act
        var urls = _provider.GetExternalUrls(series).ToList();

        // Assert
        Assert.Empty(urls);
    }

    [Fact]
    public void GetExternalUrls_UnsupportedItemType_ReturnsEmpty()
    {
        // Arrange
        var audio = new Audio { Name = "Test Audio" };
        audio.SetProviderId(Constants.ProviderId, "12345");

        // Act
        var urls = _provider.GetExternalUrls(audio).ToList();

        // Assert
        Assert.Empty(urls);
    }

    [Fact]
    public void GetExternalUrls_Episode_ReturnsEmpty()
    {
        // Arrange
        var episode = new Episode { Name = "Test Episode" };
        episode.SetProviderId(Constants.ProviderId, "12345");

        // Act
        var urls = _provider.GetExternalUrls(episode).ToList();

        // Assert
        Assert.Empty(urls); // Episodes are not supported, only Movies and Series
    }
}
