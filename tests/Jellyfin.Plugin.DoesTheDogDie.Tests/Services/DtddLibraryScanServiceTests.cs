using System;
using System.Threading;
using System.Threading.Tasks;
using DoesTheDogDie;
using DoesTheDogDie.Api;
using DoesTheDogDie.Statistics;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Services;

public class DtddLibraryScanServiceTests
{
    private readonly Mock<ILibraryManager> _libraryManagerMock;
    private readonly Mock<DtddMetadataService> _metadata;
    private readonly Mock<IPluginConfigurationAccessor> _configAccessorMock;
    private readonly DtddLibraryScanService _service;

    public DtddLibraryScanServiceTests()
    {
        _libraryManagerMock = new Mock<ILibraryManager>();
        _configAccessorMock = new Mock<IPluginConfigurationAccessor>();
        _metadata = new Mock<DtddMetadataService>(
            (Func<IDtddClient>)(() => new Support.FakeDtddClient()),
            _configAccessorMock.Object,
            NullLogger<DtddMetadataService>.Instance,
            new OverviewFormatter());

        _service = CreateService();
    }

    private DtddLibraryScanService CreateService() =>
        new(
            _libraryManagerMock.Object,
            _metadata.Object,
            _configAccessorMock.Object,
            NullLogger<DtddLibraryScanService>.Instance);

    private static DtddItemData Data() => new(
        1234,
        new[]
        {
            new TriggerInfo(
                new Topic { Id = 201, Name = "a dog dies", TopicCategoryId = 3 },
                40,
                1,
                TriggerConfidence.Compute(40, 1)),
        },
        ResultSource.Live,
        DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task StartAsync_SubscribesToLibraryEvents()
    {
        await _service.StartAsync(CancellationToken.None);
        await _service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_UnsubscribesFromLibraryEvents()
    {
        await _service.StartAsync(CancellationToken.None);
        await _service.StopAsync(CancellationToken.None);
        await _service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void OnItemChanged_NonMovieOrSeries_ReturnsEarly()
    {
        var audio = new Audio { Name = "Test Song" };
        var eventArgs = new ItemChangeEventArgs { Item = audio };

        _service.OnItemChanged(null, eventArgs);

        _metadata.Verify(
            x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void OnItemChanged_NullConfiguration_ReturnsEarly()
    {
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns((PluginConfiguration?)null);
        var movie = CreateMovie("tt2911666");
        var eventArgs = new ItemChangeEventArgs { Item = movie };

        _service.OnItemChanged(null, eventArgs);

        _metadata.Verify(
            x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void OnItemChanged_MoviesDisabled_ReturnsEarly()
    {
        var config = new PluginConfiguration { EnableMovies = false };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var movie = CreateMovie("tt2911666");
        var eventArgs = new ItemChangeEventArgs { Item = movie };

        _service.OnItemChanged(null, eventArgs);

        _metadata.Verify(
            x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void OnItemChanged_SeriesDisabled_ReturnsEarly()
    {
        var config = new PluginConfiguration { EnableSeries = false };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var series = CreateSeries("tt0944947");
        var eventArgs = new ItemChangeEventArgs { Item = series };

        _service.OnItemChanged(null, eventArgs);

        _metadata.Verify(
            x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void OnItemChanged_NoImdbId_ReturnsEarly()
    {
        var config = new PluginConfiguration { EnableMovies = true };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var movie = new Movie { Name = "Test Movie" };
        var eventArgs = new ItemChangeEventArgs { Item = movie };

        _service.OnItemChanged(null, eventArgs);

        _metadata.Verify(
            x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void OnItemChanged_AlreadyHasDtddId_ReturnsEarly()
    {
        var config = new PluginConfiguration { EnableMovies = true };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var movie = CreateMovie("tt2911666");
        movie.SetProviderId(Constants.ProviderId, "15713");
        var eventArgs = new ItemChangeEventArgs { Item = movie };

        _service.OnItemChanged(null, eventArgs);

        _metadata.Verify(
            x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void OnItemChanged_ValidMovie_QueuesLookup()
    {
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), "tt2911666", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Data());

        var movie = CreateMovie("tt2911666");
        var eventArgs = new ItemChangeEventArgs { Item = movie };

        _service.OnItemChanged(null, eventArgs);

        Thread.Sleep(100);
        _metadata.Verify(
            x => x.ResolveAsync(It.IsAny<string>(), "tt2911666", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void OnItemChanged_ValidSeries_QueuesLookup()
    {
        var config = new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), "tt0944947", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Data());

        var series = CreateSeries("tt0944947");
        var eventArgs = new ItemChangeEventArgs { Item = series };

        _service.OnItemChanged(null, eventArgs);

        Thread.Sleep(100);
        _metadata.Verify(
            x => x.ResolveAsync(It.IsAny<string>(), "tt0944947", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void OnItemChanged_ResolveReturnsNull_HandlesGracefully()
    {
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            AddWarningTags = true
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), "tt9999999", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DtddItemData?)null);

        var movie = CreateMovie("tt9999999");
        var eventArgs = new ItemChangeEventArgs { Item = movie };

        _service.OnItemChanged(null, eventArgs);

        Thread.Sleep(100);
        _metadata.Verify(
            x => x.ResolveAsync(It.IsAny<string>(), "tt9999999", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _metadata.Verify(x => x.Apply(It.IsAny<BaseItem>(), It.IsAny<DtddItemData>(), It.IsAny<PluginConfiguration>()), Times.Never);
    }

    [Fact]
    public void OnItemChanged_SetsDtddProviderIdAndApplies()
    {
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            AddWarningTags = false
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), "tt2911666", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Data());

        var movie = CreateMovie("tt2911666");
        var eventArgs = new ItemChangeEventArgs { Item = movie };

        _service.OnItemChanged(null, eventArgs);

        Thread.Sleep(200);
        Assert.Equal("1234", movie.GetProviderId(Constants.ProviderId));
        _metadata.Verify(x => x.Apply(movie, It.IsAny<DtddItemData>(), config), Times.Once);
    }

    [Fact]
    public void OnItemChanged_PassesFetchReason_NotUserInitiated()
    {
        var config = new PluginConfiguration { EnableMovies = true };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), "tt2911666", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Data());

        var movie = CreateMovie("tt2911666");
        var eventArgs = new ItemChangeEventArgs { Item = movie };

        _service.OnItemChanged(null, eventArgs);

        Thread.Sleep(200);
        _metadata.Verify(
            x => x.ResolveAsync(
                It.IsAny<string>(),
                "tt2911666",
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.Is<FetchReason>(r => r.Kind == DtddItemKind.Movie && !r.IsUserInitiated),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessItemAsync_CallsUpdateToRepositoryAsync_AfterSuccessfulResolve()
    {
        var config = new PluginConfiguration { EnableMovies = true };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Data());

        var movieMock = new Mock<Movie> { CallBase = true };
        movieMock.Object.Name = "Test Movie";
        movieMock.Object.Tags = Array.Empty<string>();
        movieMock.Object.SetProviderId(MetadataProvider.Imdb, "tt2911666");
        movieMock.Setup(x => x.UpdateToRepositoryAsync(It.IsAny<ItemUpdateType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.ProcessItemAsync(movieMock.Object, CancellationToken.None);

        movieMock.Verify(
            x => x.UpdateToRepositoryAsync(ItemUpdateType.MetadataDownload, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessItemAsync_ResolveReturnsNull_DoesNotCallUpdateToRepositoryAsync()
    {
        var config = new PluginConfiguration { EnableMovies = true };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DtddItemData?)null);

        var movieMock = new Mock<Movie> { CallBase = true };
        movieMock.Object.Name = "Test Movie";
        movieMock.Object.Tags = Array.Empty<string>();
        movieMock.Setup(x => x.UpdateToRepositoryAsync(It.IsAny<ItemUpdateType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.ProcessItemAsync(movieMock.Object, CancellationToken.None);

        movieMock.Verify(
            x => x.UpdateToRepositoryAsync(It.IsAny<ItemUpdateType>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessItemAsync_ApplyThrows_ExceptionDoesNotPropagate()
    {
        var config = new PluginConfiguration { EnableMovies = true };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Data());
        _metadata
            .Setup(x => x.Apply(It.IsAny<BaseItem>(), It.IsAny<DtddItemData>(), It.IsAny<PluginConfiguration>()))
            .Throws(new InvalidOperationException("boom"));

        // Should not throw.
        await _service.ProcessItemAsync(new Movie { Name = "Test" }, CancellationToken.None);
    }

    [Fact]
    public async Task ProcessItemAsync_ResolveThrowsUnexpectedException_ExceptionDoesNotPropagate()
    {
        var config = new PluginConfiguration { EnableMovies = true };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        // Should not throw.
        await _service.ProcessItemAsync(new Movie { Name = "Test" }, CancellationToken.None);
    }

    [Fact]
    public async Task ProcessItemAsync_DoesNotSwallowCancellation()
    {
        var config = new PluginConfiguration { EnableMovies = true };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.ProcessItemAsync(new Movie { Name = "Test" }, cts.Token));
    }

    [Fact]
    public async Task ProcessItem_KeepsProcessingSubsequentItems_AfterAResolveFailure()
    {
        var calls = 0;
        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                calls++;
                return calls == 1 ? null : Data();
            });

        var config = new PluginConfiguration { EnableMovies = true };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var service = CreateService();
        await service.ProcessItemAsync(new Movie { Name = "First" }, CancellationToken.None);
        await service.ProcessItemAsync(new Movie { Name = "Second" }, CancellationToken.None);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ProcessItemAsync_NullConfiguration_ReturnsEarlyWithoutResolving()
    {
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns((PluginConfiguration?)null);

        await _service.ProcessItemAsync(new Movie { Name = "Test" }, CancellationToken.None);

        _metadata.Verify(
            x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
}
