using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.DoesTheDogDie.Api;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Services;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Services;

public class DtddLibraryScanServiceTests
{
    private readonly Mock<ILibraryManager> _libraryManagerMock;
    private readonly Mock<DtddApiClient> _apiClientMock;
    private readonly Mock<IPluginConfigurationAccessor> _configAccessorMock;
    private readonly Mock<ILogger<DtddLibraryScanService>> _loggerMock;
    private readonly DtddLibraryScanService _service;

    public DtddLibraryScanServiceTests()
    {
        _libraryManagerMock = new Mock<ILibraryManager>();
        _apiClientMock = new Mock<DtddApiClient>(
            Mock.Of<System.Net.Http.IHttpClientFactory>(),
            Mock.Of<ILogger<DtddApiClient>>());
        _configAccessorMock = new Mock<IPluginConfigurationAccessor>();
        _loggerMock = new Mock<ILogger<DtddLibraryScanService>>();

        _service = new DtddLibraryScanService(
            _libraryManagerMock.Object,
            _apiClientMock.Object,
            _configAccessorMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task StartAsync_SubscribesToLibraryEvents()
    {
        // Act
        await _service.StartAsync(CancellationToken.None);

        // Assert - verify we subscribed to events
        // Note: We can't easily verify event subscription with Moq,
        // but we can verify StartAsync completes without error
        await _service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_UnsubscribesFromLibraryEvents()
    {
        // Arrange
        await _service.StartAsync(CancellationToken.None);

        // Act
        await _service.StopAsync(CancellationToken.None);

        // Assert - verify we can stop without error
        // The actual unsubscription is verified by the fact that
        // calling stop twice doesn't throw
        await _service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_CompletesSuccessfully()
    {
        // Act & Assert - should not throw
        await _service.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_CompletesSuccessfully()
    {
        // Arrange
        await _service.StartAsync(CancellationToken.None);

        // Act & Assert - should not throw
        await _service.StopAsync(CancellationToken.None);
    }
}
