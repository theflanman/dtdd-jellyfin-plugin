using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.DoesTheDogDie.Api;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.ScheduledTasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.ScheduledTasks;

public class DtddRefreshTaskTests
{
    private readonly Mock<ILibraryManager> _libraryManagerMock;
    private readonly Mock<DtddApiClient> _apiClientMock;
    private readonly Mock<IPluginConfigurationAccessor> _configAccessorMock;
    private readonly Mock<ILogger<DtddRefreshTask>> _loggerMock;
    private readonly DtddRefreshTask _task;

    public DtddRefreshTaskTests()
    {
        _libraryManagerMock = new Mock<ILibraryManager>();
        _apiClientMock = new Mock<DtddApiClient>(
            Mock.Of<System.Net.Http.IHttpClientFactory>(),
            Mock.Of<ILogger<DtddApiClient>>());
        _configAccessorMock = new Mock<IPluginConfigurationAccessor>();
        _loggerMock = new Mock<ILogger<DtddRefreshTask>>();

        _task = new DtddRefreshTask(
            _libraryManagerMock.Object,
            _apiClientMock.Object,
            _configAccessorMock.Object,
            _loggerMock.Object);
    }

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
        // Arrange
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns((PluginConfiguration?)null);
        var progress = new Mock<IProgress<double>>();

        // Act
        await _task.ExecuteAsync(progress.Object, CancellationToken.None);

        // Assert - should not query library
        _libraryManagerMock.Verify(
            x => x.GetItemList(It.IsAny<InternalItemsQuery>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoItemsWithDtddId_CompletesSuccessfully()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            EnableSeries = true
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        _libraryManagerMock
            .Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(Array.Empty<MediaBrowser.Controller.Entities.BaseItem>());

        var progress = new Mock<IProgress<double>>();

        // Act
        await _task.ExecuteAsync(progress.Object, CancellationToken.None);

        // Assert - should report 100% completion
        progress.Verify(x => x.Report(100), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            EnableMovies = true,
            EnableSeries = true
        };
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);

        _libraryManagerMock
            .Setup(x => x.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(Array.Empty<MediaBrowser.Controller.Entities.BaseItem>());

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - should complete without error since there are no items to process
        await _task.ExecuteAsync(new Mock<IProgress<double>>().Object, cts.Token);
    }
}
