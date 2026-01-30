using Jellyfin.Plugin.DoesTheDogDie.Providers;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Providers;

public class DtddExternalIdTests
{
    #region DtddMovieExternalId Tests

    [Fact]
    public void MovieExternalId_ProviderName_ReturnsCorrectName()
    {
        var externalId = new DtddMovieExternalId();
        Assert.Equal(Constants.ProviderName, externalId.ProviderName);
    }

    [Fact]
    public void MovieExternalId_Key_ReturnsCorrectKey()
    {
        var externalId = new DtddMovieExternalId();
        Assert.Equal(Constants.ProviderId, externalId.Key);
    }

    [Fact]
    public void MovieExternalId_Type_ReturnsMovie()
    {
        var externalId = new DtddMovieExternalId();
        Assert.Equal(ExternalIdMediaType.Movie, externalId.Type);
    }

    [Fact]
    public void MovieExternalId_UrlFormatString_ContainsMediaPath()
    {
        var externalId = new DtddMovieExternalId();
        Assert.NotNull(externalId.UrlFormatString);
        Assert.Contains("doesthedogdie.com/media/{0}", externalId.UrlFormatString);
    }

    [Fact]
    public void MovieExternalId_Supports_Movie_ReturnsTrue()
    {
        var externalId = new DtddMovieExternalId();
        var movie = new Movie { Name = "Test Movie" };
        Assert.True(externalId.Supports(movie));
    }

    [Fact]
    public void MovieExternalId_Supports_Series_ReturnsFalse()
    {
        var externalId = new DtddMovieExternalId();
        var series = new Series { Name = "Test Series" };
        Assert.False(externalId.Supports(series));
    }

    #endregion

    #region DtddSeriesExternalId Tests

    [Fact]
    public void SeriesExternalId_ProviderName_ReturnsCorrectName()
    {
        var externalId = new DtddSeriesExternalId();
        Assert.Equal(Constants.ProviderName, externalId.ProviderName);
    }

    [Fact]
    public void SeriesExternalId_Key_ReturnsCorrectKey()
    {
        var externalId = new DtddSeriesExternalId();
        Assert.Equal(Constants.ProviderId, externalId.Key);
    }

    [Fact]
    public void SeriesExternalId_Type_ReturnsSeries()
    {
        var externalId = new DtddSeriesExternalId();
        Assert.Equal(ExternalIdMediaType.Series, externalId.Type);
    }

    [Fact]
    public void SeriesExternalId_UrlFormatString_ContainsMediaPath()
    {
        var externalId = new DtddSeriesExternalId();
        Assert.NotNull(externalId.UrlFormatString);
        Assert.Contains("doesthedogdie.com/media/{0}", externalId.UrlFormatString);
    }

    [Fact]
    public void SeriesExternalId_Supports_Series_ReturnsTrue()
    {
        var externalId = new DtddSeriesExternalId();
        var series = new Series { Name = "Test Series" };
        Assert.True(externalId.Supports(series));
    }

    [Fact]
    public void SeriesExternalId_Supports_Movie_ReturnsFalse()
    {
        var externalId = new DtddSeriesExternalId();
        var movie = new Movie { Name = "Test Movie" };
        Assert.False(externalId.Supports(movie));
    }

    #endregion
}
