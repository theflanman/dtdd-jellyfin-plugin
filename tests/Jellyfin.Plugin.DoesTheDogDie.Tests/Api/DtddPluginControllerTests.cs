using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.DoesTheDogDie.Api;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Api;

public class DtddPluginControllerTests
{
    private readonly Mock<TriggerCacheService> _cacheServiceMock;
    private readonly DtddPluginController _controller;

    public DtddPluginControllerTests()
    {
        var apiClientMock = new Mock<DtddApiClient>(
            Mock.Of<System.Net.Http.IHttpClientFactory>(),
            Mock.Of<ILogger<DtddApiClient>>());
        var loggerMock = new Mock<ILogger<TriggerCacheService>>();

        _cacheServiceMock = new Mock<TriggerCacheService>(apiClientMock.Object, loggerMock.Object);
        _controller = new DtddPluginController(_cacheServiceMock.Object);
    }

    [Fact]
    public async Task GetTopics_ReturnsOkWithCache()
    {
        // Arrange
        var cache = CreateSampleCache();
        _cacheServiceMock
            .Setup(x => x.GetOrRefreshCacheAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cache);

        // Act
        var result = await _controller.GetTopics(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedCache = Assert.IsType<TriggerCache>(okResult.Value);
        Assert.Equal(2, returnedCache.Categories.Count);
    }

    [Fact]
    public async Task GetTopics_CallsServiceWithForceRefreshFalse()
    {
        // Arrange
        _cacheServiceMock
            .Setup(x => x.GetOrRefreshCacheAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TriggerCache());

        // Act
        await _controller.GetTopics(CancellationToken.None);

        // Assert
        _cacheServiceMock.Verify(
            x => x.GetOrRefreshCacheAsync(false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RefreshTopics_ReturnsOkWithRefreshedCache()
    {
        // Arrange
        var cache = CreateSampleCache();
        _cacheServiceMock
            .Setup(x => x.RefreshCacheAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(cache);

        // Act
        var result = await _controller.RefreshTopics(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedCache = Assert.IsType<TriggerCache>(okResult.Value);
        Assert.Equal(2, returnedCache.Categories.Count);
    }

    [Fact]
    public async Task RefreshTopics_CallsRefreshCacheAsync()
    {
        // Arrange
        _cacheServiceMock
            .Setup(x => x.RefreshCacheAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TriggerCache());

        // Act
        await _controller.RefreshTopics(CancellationToken.None);

        // Assert
        _cacheServiceMock.Verify(
            x => x.RefreshCacheAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetTopics_ReturnsEmptyCache_WhenNoData()
    {
        // Arrange
        var emptyCache = new TriggerCache
        {
            LastRefreshed = DateTime.UtcNow,
            Categories = new List<CachedCategory>()
        };
        _cacheServiceMock
            .Setup(x => x.GetOrRefreshCacheAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyCache);

        // Act
        var result = await _controller.GetTopics(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedCache = Assert.IsType<TriggerCache>(okResult.Value);
        Assert.Empty(returnedCache.Categories);
    }

    private static TriggerCache CreateSampleCache()
    {
        return new TriggerCache
        {
            LastRefreshed = DateTime.UtcNow,
            Categories = new List<CachedCategory>
            {
                new CachedCategory
                {
                    Id = 2,
                    Name = "Animal",
                    Topics = new List<CachedTopic>
                    {
                        new CachedTopic { Id = 153, Name = "a dog dies" },
                        new CachedTopic { Id = 154, Name = "a cat dies" }
                    }
                },
                new CachedCategory
                {
                    Id = 3,
                    Name = "Violence",
                    Topics = new List<CachedTopic>
                    {
                        new CachedTopic { Id = 101, Name = "blood/gore" }
                    }
                }
            }
        };
    }
}
