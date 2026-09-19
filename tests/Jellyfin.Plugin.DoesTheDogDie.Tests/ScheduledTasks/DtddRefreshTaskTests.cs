using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DoesTheDogDie;
using DoesTheDogDie.Api;
using DoesTheDogDie.Statistics;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.ScheduledTasks;
using Jellyfin.Plugin.DoesTheDogDie.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.ScheduledTasks;

public class DtddRefreshTaskTests
{
    private readonly Mock<ILibraryManager> _libraryManagerMock;
    private readonly Mock<DtddMetadataService> _metadata;
    private readonly Mock<IPluginConfigurationAccessor> _configAccessorMock;
    private readonly DtddRefreshTask _task;

    public DtddRefreshTaskTests()
    {
        _libraryManagerMock = new Mock<ILibraryManager>();
        _configAccessorMock = new Mock<IPluginConfigurationAccessor>();
        _metadata = new Mock<DtddMetadataService>(
            (Func<IDtddClient>)(() => new Support.FakeDtddClient()),
            _configAccessorMock.Object,
            NullLogger<DtddMetadataService>.Instance,
            new OverviewFormatter());

        _task = CreateTask();
    }

    private DtddRefreshTask CreateTask() =>
        new(
            _libraryManagerMock.Object,
            _metadata.Object,
            _configAccessorMock.Object,
            NullLogger<DtddRefreshTask>.Instance);

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
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns((PluginConfiguration?)null);
        var progress = new Mock<IProgress<double>>();

        await _task.ExecuteAsync(progress.Object, CancellationToken.None);

        _libraryManagerMock.Verify(
            x => x.GetItemList(It.IsAny<InternalItemsQuery>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoItemsWithDtddId_CompletesSuccessfully()
    {
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            EnableSeries = true
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        _libraryManagerMock
            .Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(Array.Empty<BaseItem>());

        var progress = new Mock<IProgress<double>>();

        await _task.ExecuteAsync(progress.Object, CancellationToken.None);

        progress.Verify(x => x.Report(100), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            EnableSeries = true
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        _libraryManagerMock
            .Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(Array.Empty<BaseItem>());

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await _task.ExecuteAsync(new Mock<IProgress<double>>().Object, cts.Token);
    }

    [Fact]
    public async Task ExecuteAsync_WithMovies_ResolvesAndApplies()
    {
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            EnableSeries = false,
            AddWarningTags = true,
            TagPrefix = "CW:",
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var movie = CreateMovie("tt2911666", "15713");
        _libraryManagerMock
            .Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new BaseItem[] { movie });

        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Data());

        var progress = new Mock<IProgress<double>>();

        await _task.ExecuteAsync(progress.Object, CancellationToken.None);

        progress.Verify(x => x.Report(100), Times.Once);
        _metadata.Verify(x => x.Apply(movie, It.IsAny<DtddItemData>(), config), Times.Once);
        Assert.Equal("1234", movie.GetProviderId(Constants.ProviderId));
    }

    [Fact]
    public async Task ExecuteAsync_WithSeries_ResolvesAndApplies()
    {
        var config = new PluginConfiguration
        {
            EnableMovies = false,
            EnableSeries = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var series = CreateSeries("tt0944947", "12345");
        _libraryManagerMock
            .Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new BaseItem[] { series });

        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Data());

        var progress = new Mock<IProgress<double>>();

        await _task.ExecuteAsync(progress.Object, CancellationToken.None);

        progress.Verify(x => x.Report(100), Times.Once);
        _metadata.Verify(x => x.Apply(series, It.IsAny<DtddItemData>(), config), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ResolveReturnsNull_HandlesGracefully()
    {
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

        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DtddItemData?)null);

        var progress = new Mock<IProgress<double>>();

        await _task.ExecuteAsync(progress.Object, CancellationToken.None);

        progress.Verify(x => x.Report(100), Times.Once);
        _metadata.Verify(x => x.Apply(It.IsAny<BaseItem>(), It.IsAny<DtddItemData>(), It.IsAny<PluginConfiguration>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ResolveThrowsUnexpectedException_ContinuesRun()
    {
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

        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var progress = new Mock<IProgress<double>>();

        await _task.ExecuteAsync(progress.Object, CancellationToken.None);

        progress.Verify(x => x.Report(100), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotSwallowCancellation()
    {
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

        // The token passed to ExecuteAsync is deliberately left uncancelled: the point of this test is that
        // a dependency (RefreshItemAsync -> ResolveAsync) throwing OperationCanceledException on its own
        // must propagate out of the per-item try/catch rather than being logged and swallowed. If the
        // outer token were already cancelled, the loop's leading ThrowIfCancellationRequested() would throw
        // first and the catch's exception filter would never be exercised.
        using var innerCts = new CancellationTokenSource();
        await innerCts.CancelAsync();

        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(innerCts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _task.ExecuteAsync(new Mock<IProgress<double>>().Object, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesAfterAnItemFailsToResolve()
    {
        _metadata
            .SetupSequence(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DtddItemData?)null)
            .ReturnsAsync(Data());

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

        await CreateTask().ExecuteAsync(new Progress<double>(), CancellationToken.None);

        _metadata.Verify(
            x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteAsync_AddWarningTagsDisabled_StillAppliesOverview()
    {
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

        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Data());

        var progress = new Mock<IProgress<double>>();

        await _task.ExecuteAsync(progress.Object, CancellationToken.None);

        _metadata.Verify(x => x.Apply(movie, It.IsAny<DtddItemData>(), config), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_AddWarningTagsDisabled_StillCallsUpdateToRepositoryAsync()
    {
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            EnableSeries = false,
            AddWarningTags = false
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var movieMock = new Mock<Movie> { CallBase = true };
        movieMock.Object.Name = "Test Movie";
        movieMock.Object.Tags = Array.Empty<string>();
        movieMock.Object.SetProviderId(MetadataProvider.Imdb, "tt2911666");
        movieMock.Object.SetProviderId(Constants.ProviderId, "15713");
        movieMock.Setup(x => x.UpdateToRepositoryAsync(It.IsAny<ItemUpdateType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _libraryManagerMock
            .Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new BaseItem[] { movieMock.Object });

        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Data());

        await _task.ExecuteAsync(new Mock<IProgress<double>>().Object, CancellationToken.None);

        // The persistence call is unconditional: it must fire even when AddWarningTags is false,
        // since the resolved DTDD provider id (and any overview change) still needs saving.
        movieMock.Verify(
            x => x.UpdateToRepositoryAsync(ItemUpdateType.MetadataDownload, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReportsProgressCorrectly()
    {
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

        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Data());

        var reportedValues = new List<double>();
        var progress = new Mock<IProgress<double>>();
        progress.Setup(x => x.Report(It.IsAny<double>()))
            .Callback<double>(v => reportedValues.Add(v));

        await _task.ExecuteAsync(progress.Object, CancellationToken.None);

        Assert.Contains(0, reportedValues);
        Assert.Contains(50, reportedValues);
        Assert.Contains(100, reportedValues);
    }

    [Fact]
    public async Task ExecuteAsync_PassesRefreshFetchReason()
    {
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

        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Data());

        await _task.ExecuteAsync(new Mock<IProgress<double>>().Object, CancellationToken.None);

        _metadata.Verify(
            x => x.ResolveAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.Is<FetchReason>(r => r.Kind == DtddItemKind.Movie && r.IsRefresh && !r.IsUserInitiated),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_CallsUpdateToRepositoryAsync_WhenResolved()
    {
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            EnableSeries = false,
            AddWarningTags = true,
            TagPrefix = "CW:",
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var movieMock = new Mock<Movie> { CallBase = true };
        movieMock.Object.Name = "Test Movie";
        movieMock.Object.Tags = Array.Empty<string>();
        movieMock.Object.SetProviderId(MetadataProvider.Imdb, "tt2911666");
        movieMock.Object.SetProviderId(Constants.ProviderId, "15713");
        movieMock.Setup(x => x.UpdateToRepositoryAsync(It.IsAny<ItemUpdateType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _libraryManagerMock
            .Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new BaseItem[] { movieMock.Object });

        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Data());

        await _task.ExecuteAsync(new Mock<IProgress<double>>().Object, CancellationToken.None);

        movieMock.Verify(
            x => x.UpdateToRepositoryAsync(ItemUpdateType.MetadataDownload, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ResolveReturnsNull_DoesNotCallUpdateToRepositoryAsync()
    {
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            EnableSeries = false,
            AddWarningTags = true
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var movieMock = new Mock<Movie> { CallBase = true };
        movieMock.Object.Name = "Test Movie";
        movieMock.Object.Tags = Array.Empty<string>();
        movieMock.Object.SetProviderId(MetadataProvider.Imdb, "tt9999999");
        movieMock.Setup(x => x.UpdateToRepositoryAsync(It.IsAny<ItemUpdateType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _libraryManagerMock
            .Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new BaseItem[] { movieMock.Object });

        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DtddItemData?)null);

        await _task.ExecuteAsync(new Mock<IProgress<double>>().Object, CancellationToken.None);

        movieMock.Verify(
            x => x.UpdateToRepositoryAsync(It.IsAny<ItemUpdateType>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ItemWithOnlyImdbId_IsProcessedOnFreshInstall()
    {
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            EnableSeries = false,
            AddWarningTags = true,
            TagPrefix = "CW:",
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        var movie = new Movie { Name = "Test Movie", Tags = Array.Empty<string>() };
        movie.SetProviderId(MetadataProvider.Imdb, "tt2911666");

        _libraryManagerMock
            .Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new BaseItem[] { movie });

        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Data());

        await _task.ExecuteAsync(new Mock<IProgress<double>>().Object, CancellationToken.None);

        _metadata.Verify(x => x.Apply(movie, It.IsAny<DtddItemData>(), config), Times.Once);
        Assert.Equal("1234", movie.GetProviderId(Constants.ProviderId));
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
}
