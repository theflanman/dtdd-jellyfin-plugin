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

public class DtddSeriesProviderTests
{
    private readonly Mock<DtddMetadataService> _metadata;
    private readonly Mock<IPluginConfigurationAccessor> _configAccessor = new();

    public DtddSeriesProviderTests()
    {
        _metadata = new Mock<DtddMetadataService>(
            (Func<IDtddClient>)(() => new Support.FakeDtddClient()),
            _configAccessor.Object,
            NullLogger<DtddMetadataService>.Instance,
            new OverviewFormatter());

        _configAccessor.Setup(x => x.GetConfiguration())
            .Returns(new PluginConfiguration { EnableSeries = true, ApiKey = "ddd_key" });
    }

    private DtddSeriesProvider CreateProvider() =>
        new(_metadata.Object, _configAccessor.Object, NullLogger<DtddSeriesProvider>.Instance);

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
    public async Task FetchAsync_ReturnsNone_WhenSeriesDisabled()
    {
        _configAccessor.Setup(x => x.GetConfiguration())
            .Returns(new PluginConfiguration { EnableSeries = false });
        var item = new Series { Name = "Breaking Bad" };

        var result = await CreateProvider().FetchAsync(item, new MetadataRefreshOptions(Mock.Of<IDirectoryService>()), CancellationToken.None);

        Assert.Equal(ItemUpdateType.None, result);
    }

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
        var item = new Series { Name = "Breaking Bad" };

        var result = await CreateProvider().FetchAsync(item, new MetadataRefreshOptions(Mock.Of<IDirectoryService>()), CancellationToken.None);

        Assert.Equal(ItemUpdateType.None, result);
    }

    [Fact]
    public async Task FetchAsync_ReturnsNone_WhenNothingResolves()
    {
        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DtddItemData?)null);
        var item = new Series { Name = "Breaking Bad" };

        var result = await CreateProvider().FetchAsync(item, new MetadataRefreshOptions(Mock.Of<IDirectoryService>()), CancellationToken.None);

        Assert.Equal(ItemUpdateType.None, result);
    }

    [Fact]
    public async Task FetchAsync_SetsProviderIdAndApplies_WhenResolved()
    {
        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Data());
        var item = new Series { Name = "Breaking Bad" };

        var result = await CreateProvider().FetchAsync(item, new MetadataRefreshOptions(Mock.Of<IDirectoryService>()), CancellationToken.None);

        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.Equal("1234", item.GetProviderId(Constants.ProviderId));
        _metadata.Verify(x => x.Apply(item, It.IsAny<DtddItemData>(), It.IsAny<PluginConfiguration>()), Times.Once);
    }

    [Fact]
    public async Task FetchAsync_AlwaysResolves_EvenWhenProviderIdAlreadySet()
    {
        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Data());
        var item = new Series { Name = "Breaking Bad" };
        item.SetProviderId(Constants.ProviderId, "1234");

        await CreateProvider().FetchAsync(item, new MetadataRefreshOptions(Mock.Of<IDirectoryService>()), CancellationToken.None);

        _metadata.Verify(
            x => x.ResolveAsync("1234", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchAsync_PassesSeriesFetchReason()
    {
        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Data());
        var item = new Series { Name = "Breaking Bad" };

        await CreateProvider().FetchAsync(item, new MetadataRefreshOptions(Mock.Of<IDirectoryService>()), CancellationToken.None);

        _metadata.Verify(
            x => x.ResolveAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.Is<FetchReason>(r => r.Kind == DtddItemKind.Series),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchAsync_ReturnsMetadataDownload_WhenApplyThrows()
    {
        _metadata
            .Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<FetchReason>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Data());
        _metadata
            .Setup(x => x.Apply(It.IsAny<BaseItem>(), It.IsAny<DtddItemData>(), It.IsAny<PluginConfiguration>()))
            .Throws(new InvalidOperationException("apply blew up"));
        var item = new Series { Name = "Breaking Bad" };

        var result = await CreateProvider().FetchAsync(item, new MetadataRefreshOptions(Mock.Of<IDirectoryService>()), CancellationToken.None);

        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.Equal("1234", item.GetProviderId(Constants.ProviderId));
    }
}
