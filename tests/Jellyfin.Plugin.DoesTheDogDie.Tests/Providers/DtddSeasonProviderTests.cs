using System;
using System.Threading;
using System.Threading.Tasks;
using DoesTheDogDie;
using DoesTheDogDie.Api;
using DoesTheDogDie.Statistics;
using Jellyfin.Plugin.DoesTheDogDie;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Providers;
using Jellyfin.Plugin.DoesTheDogDie.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Providers;

[Collection("BaseItemStatics")]
public class DtddSeasonProviderTests
{
    private readonly Mock<DtddMetadataService> _metadata;
    private readonly Mock<IPluginConfigurationAccessor> _configAccessor = new();

    public DtddSeasonProviderTests()
    {
        _metadata = new Mock<DtddMetadataService>(
            (Func<IDtddClient>)(() => new Support.FakeDtddClient()),
            _configAccessor.Object,
            NullLogger<DtddMetadataService>.Instance,
            new OverviewFormatter());

        _configAccessor.Setup(x => x.GetConfiguration())
            .Returns(new PluginConfiguration { EnableSeries = true, ApiKey = "ddd_key" });
    }

    private DtddSeasonProvider CreateProvider() =>
        new(_metadata.Object, _configAccessor.Object, NullLogger<DtddSeasonProvider>.Instance);

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
    public void Order_Is100()
    {
        Assert.Equal(100, CreateProvider().Order);
    }

    [Fact]
    public void Name_IsProviderName()
    {
        Assert.Equal(Constants.ProviderName, CreateProvider().Name);
    }

    [Fact]
    public async Task FetchAsync_ReturnsNone_WhenConfigurationMissing()
    {
        _configAccessor.Setup(x => x.GetConfiguration())
            .Returns((PluginConfiguration?)null);

        var result = await CreateProvider().FetchAsync(
            new Season { Name = "Season 1" },
            new MetadataRefreshOptions(Mock.Of<IDirectoryService>()),
            CancellationToken.None);

        Assert.Equal(ItemUpdateType.None, result);
    }

    [Fact]
    public async Task FetchAsync_ReturnsNone_WhenSeriesDisabled()
    {
        _configAccessor.Setup(x => x.GetConfiguration())
            .Returns(new PluginConfiguration { EnableSeries = false });

        var result = await CreateProvider().FetchAsync(
            new Season { Name = "Season 1" },
            new MetadataRefreshOptions(Mock.Of<IDirectoryService>()),
            CancellationToken.None);

        Assert.Equal(ItemUpdateType.None, result);
    }

    [Fact]
    public async Task FetchAsync_ReturnsNone_WhenNoParentSeries()
    {
        _configAccessor.Setup(x => x.GetConfiguration())
            .Returns(new PluginConfiguration { EnableSeries = true });

        var result = await CreateProvider().FetchAsync(
            new Season { Name = "Season 1" },
            new MetadataRefreshOptions(Mock.Of<IDirectoryService>()),
            CancellationToken.None);

        Assert.Equal(ItemUpdateType.None, result);
        _metadata.Verify(
            x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <remarks>
    /// The parent-series lookup normally makes this path unreachable in a unit test (Season/Episode expose
    /// <c>Series</c> as a non-virtual, setter-less property). It is reachable here by pointing the static
    /// <see cref="BaseItem.LibraryManager"/> at a stub that resolves <c>SeriesId</c>, which is why this class
    /// shares a non-parallel xUnit collection with the other provider test that does the same.
    /// </remarks>
    [Fact]
    public async Task FetchAsync_ReturnsMetadataDownload_WhenApplyThrows()
    {
        var previousLibraryManager = BaseItem.LibraryManager;
        try
        {
            var series = new Series { Name = "Breaking Bad", Id = Guid.NewGuid() };
            series.SetProviderId(Constants.ProviderId, "1234");

            var libraryManager = new Mock<ILibraryManager>();
            libraryManager.Setup(x => x.GetItemById(series.Id)).Returns(series);
            BaseItem.LibraryManager = libraryManager.Object;

            _metadata
                .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Data());
            _metadata
                .Setup(x => x.Apply(It.IsAny<BaseItem>(), It.IsAny<DtddItemData>(), It.IsAny<PluginConfiguration>()))
                .Throws(new InvalidOperationException("apply blew up"));

            var item = new Season { Name = "Season 1", SeriesId = series.Id };

            var result = await CreateProvider().FetchAsync(
                item,
                new MetadataRefreshOptions(Mock.Of<IDirectoryService>()),
                CancellationToken.None);

            Assert.Equal(ItemUpdateType.MetadataDownload, result);
            Assert.Equal("1234", item.GetProviderId(Constants.ProviderId));
        }
        finally
        {
            BaseItem.LibraryManager = previousLibraryManager;
        }
    }
}
