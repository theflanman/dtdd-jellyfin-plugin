using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DoesTheDogDie.Api;
using DoesTheDogDie.Cache;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Services;

public class DtddClientProviderTests : IDisposable
{
    private readonly string _cacheDir =
        Path.Combine(Path.GetTempPath(), "dtdd-tests-" + Guid.NewGuid().ToString("N"));

    private readonly Mock<IPluginConfigurationAccessor> _configAccessor = new();
    private readonly HttpClient _httpClient = new();

    private DtddClientProvider CreateProvider() => new(
        _httpClient,
        _configAccessor.Object,
        _cacheDir,
        NullLogger<DtddClientProvider>.Instance);

    [Fact]
    public void GetClient_ReturnsSameInstance_WhenConfigUnchanged()
    {
        _configAccessor.Setup(x => x.GetConfiguration())
            .Returns(new PluginConfiguration { ApiKey = "ddd_key" });
        using var provider = CreateProvider();

        Assert.Same(provider.GetClient(), provider.GetClient());
    }

    [Fact]
    public void GetClient_RebuildsStack_WhenApiKeyChanges()
    {
        var config = new PluginConfiguration { ApiKey = "ddd_first" };
        _configAccessor.Setup(x => x.GetConfiguration()).Returns(config);
        using var provider = CreateProvider();

        var first = provider.GetClient();
        config.ApiKey = "ddd_second";
        var second = provider.GetClient();

        Assert.NotSame(first, second);
        Assert.Equal("ddd_second", provider.CurrentApiKey);
    }

    [Fact]
    public void GetClient_RebuildsStack_WhenCacheAgesChange()
    {
        var config = new PluginConfiguration { ApiKey = "ddd_key", ItemCacheDays = 30 };
        _configAccessor.Setup(x => x.GetConfiguration()).Returns(config);
        using var provider = CreateProvider();

        var first = provider.GetClient();
        config.ItemCacheDays = 14;

        Assert.NotSame(first, provider.GetClient());
    }

    [Fact]
    public void GetClient_BuildsStack_WhenApiKeyIsEmpty()
    {
        _configAccessor.Setup(x => x.GetConfiguration())
            .Returns(new PluginConfiguration { ApiKey = string.Empty });
        using var provider = CreateProvider();

        Assert.NotNull(provider.GetClient());
    }

    [Fact]
    public void CurrentBudget_IsNull_BeforeAnyRequest()
    {
        _configAccessor.Setup(x => x.GetConfiguration())
            .Returns(new PluginConfiguration { ApiKey = "ddd_key" });
        using var provider = CreateProvider();

        Assert.Null(provider.CurrentBudget);
    }

    [Fact]
    public async Task GetClient_WithEmptyApiKey_FailsLocallyOnCacheMiss_WithoutTouchingTheNetwork()
    {
        using var handler = new FailIfInvokedHandler();
        using var httpClient = new HttpClient(handler);
        _configAccessor.Setup(x => x.GetConfiguration())
            .Returns(new PluginConfiguration { ApiKey = string.Empty });
        using var provider = new DtddClientProvider(
            httpClient,
            _configAccessor.Object,
            _cacheDir,
            NullLogger<DtddClientProvider>.Instance);

        var client = provider.GetClient();

        await Assert.ThrowsAsync<DtddAuthenticationException>(() => client.GetItemAsync(1));
        Assert.False(handler.WasInvoked);
    }

    [Fact]
    public async Task GetClient_WithEmptyApiKey_ResolvesFromCache_WithoutTouchingTheNetwork()
    {
        Directory.CreateDirectory(_cacheDir);
        var dbPath = Path.Combine(_cacheDir, "dtdd-cache.db");
        var seedCache = new SqliteDtddCache($"Data Source={dbPath}");
        var item = new ItemDetail
        {
            Id = 42,
            Name = "Test Movie",
            ItemTypeId = 1,
            ItemTypeName = "Movie",
        };
        await seedCache.PutItemAsync(item, DateTimeOffset.UtcNow);

        using var handler = new FailIfInvokedHandler();
        using var httpClient = new HttpClient(handler);
        _configAccessor.Setup(x => x.GetConfiguration())
            .Returns(new PluginConfiguration { ApiKey = string.Empty });
        using var provider = new DtddClientProvider(
            httpClient,
            _configAccessor.Object,
            _cacheDir,
            NullLogger<DtddClientProvider>.Instance);

        var client = provider.GetClient();
        var result = await client.GetItemAsync(42);

        Assert.Equal("Test Movie", result.Value.Name);
        Assert.False(handler.WasInvoked);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        if (Directory.Exists(_cacheDir))
        {
            Directory.Delete(_cacheDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// An <see cref="HttpMessageHandler"/> that fails the test if it is ever invoked, used to assert that a
    /// code path makes no HTTP request at all.
    /// </summary>
    private sealed class FailIfInvokedHandler : HttpMessageHandler
    {
        public bool WasInvoked { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            WasInvoked = true;
            throw new InvalidOperationException("No HTTP request should have been made for an unconfigured API key.");
        }
    }
}
