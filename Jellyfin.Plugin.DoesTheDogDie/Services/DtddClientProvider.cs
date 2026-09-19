using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DoesTheDogDie;
using DoesTheDogDie.Api;
using DoesTheDogDie.Cache;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DoesTheDogDie.Services;

/// <summary>
/// Owns the DoesTheDogDie client stack and rebuilds it when the settings it was built from change.
/// </summary>
/// <remarks>
/// A rebuild (triggered by a config change) never disposes the stack it replaces. A caller may already be
/// awaiting a call on it (<see cref="GetClient"/> hands out a reference with no lease/refcount, so there is
/// no way to know when the last caller is done with it), and disposing it synchronously would mean blocking
/// the config-changing caller for however long that in-flight call and any queued work ahead of it takes —
/// unbounded, since <see cref="ThrottledDtddClient"/> paces calls through a single background consumer. The
/// superseded stack is instead parked and disposed only when the provider itself is disposed. This is cheap
/// to leave parked: an idle <see cref="ThrottledDtddClient"/> is just a parked channel reader, and
/// <see cref="SqliteDtddCache"/> opens a connection per operation rather than holding one open, so a parked
/// stack costs effectively nothing while idle. The number of parked stacks is bounded by how many times a
/// human edits the plugin's configuration in one server session.
/// </remarks>
public sealed class DtddClientProvider : IDisposable, IAsyncDisposable
{
    private static readonly Uri DefaultBaseAddress = new("https://www.doesthedogdie.com/api/v3/");

    private readonly HttpClient _httpClient;
    private readonly IPluginConfigurationAccessor _configAccessor;
    private readonly string _cacheDirectory;
    private readonly ILogger<DtddClientProvider> _logger;
    private readonly object _lock = new();
    private readonly List<(CachedDtddClient Client, ThrottledDtddClient Throttled)> _superseded = [];

    private CachedDtddClient? _client;
    private ThrottledDtddClient? _throttled;
    private string _builtSignature = string.Empty;
    private string? _currentApiKey;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DtddClientProvider"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client used for API calls.</param>
    /// <param name="configAccessor">The plugin configuration accessor.</param>
    /// <param name="cacheDirectory">Directory in which the SQLite cache file is created.</param>
    /// <param name="logger">The logger.</param>
    public DtddClientProvider(
        HttpClient httpClient,
        IPluginConfigurationAccessor configAccessor,
        string cacheDirectory,
        ILogger<DtddClientProvider> logger)
    {
        _httpClient = httpClient;
        _configAccessor = configAccessor;
        _cacheDirectory = cacheDirectory;
        _logger = logger;
    }

    /// <summary>
    /// Gets the API key the current stack was built with, or null if no stack has been built.
    /// </summary>
    public string? CurrentApiKey
    {
        get
        {
            lock (_lock)
            {
                return _currentApiKey;
            }
        }
    }

    /// <summary>
    /// Gets the most recently observed rate-limit budget, or null if none has been observed.
    /// </summary>
    public RateLimitStatus? CurrentBudget
    {
        get
        {
            lock (_lock)
            {
                return _client?.CurrentBudget;
            }
        }
    }

    /// <summary>
    /// Gets the current client, rebuilding the stack if the configuration it depends on has changed.
    /// </summary>
    /// <returns>The current <see cref="IDtddClient"/>.</returns>
    public IDtddClient GetClient()
    {
        var config = _configAccessor.GetConfiguration() ?? new PluginConfiguration();
        var signature = BuildSignature(config);

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_client is not null && string.Equals(_builtSignature, signature, StringComparison.Ordinal))
            {
                return _client;
            }

            var (newClient, newThrottled) = BuildStack(config);

            // Park, don't dispose — see the class remarks for why.
            if (_client is not null && _throttled is not null)
            {
                _superseded.Add((_client, _throttled));
            }

            _client = newClient;
            _throttled = newThrottled;
            _builtSignature = signature;
            _currentApiKey = config.ApiKey;
            return _client;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        var toDispose = TakeAllForDisposal();
        if (toDispose is null)
        {
            return;
        }

        foreach (var (client, throttled) in toDispose)
        {
            client.DisposeAsync().AsTask().GetAwaiter().GetResult();
            throttled.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Asynchronously disposes the current and every parked stack, for hosts (such as the DI container at
    /// shutdown) that can await disposal instead of draining it synchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        var toDispose = TakeAllForDisposal();
        if (toDispose is null)
        {
            return;
        }

        foreach (var (client, throttled) in toDispose)
        {
            await client.DisposeAsync().ConfigureAwait(false);
            await throttled.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Marks the provider disposed and returns every stack (current plus parked) that needs disposing, or
    /// null if the provider was already disposed. Only ever called once, by whichever of <see cref="Dispose"/>
    /// / <see cref="DisposeAsync"/> wins the race under <see cref="_lock"/>.
    /// </summary>
    private List<(CachedDtddClient Client, ThrottledDtddClient Throttled)>? TakeAllForDisposal()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return null;
            }

            var toDispose = new List<(CachedDtddClient Client, ThrottledDtddClient Throttled)>(_superseded);
            if (_client is not null && _throttled is not null)
            {
                toDispose.Add((_client, _throttled));
            }

            _superseded.Clear();
            _client = null;
            _throttled = null;
            _disposed = true;
            return toDispose;
        }
    }

    /// <summary>
    /// Builds a signature identifying the settings the stack would be built from, so <see cref="GetClient"/>
    /// can detect a change cheaply. The API key is hashed rather than embedded directly: hashing produces a
    /// fixed-length, delimiter-free component, so a key containing the field separator can never be
    /// misparsed into colliding with a different cache-age/base-address combination (and the raw key is not
    /// retained in <see cref="_builtSignature"/> as a side effect).
    /// </summary>
    private string BuildSignature(PluginConfiguration config)
    {
        var apiKeyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(config.ApiKey ?? string.Empty)));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{apiKeyHash}|{config.ItemCacheDays}|{config.TaxonomyCacheDays}|{ResolveBaseAddress()}");
    }

    /// <summary>
    /// Resolves the DtDD API base address from the <c>DTDD_API_BASE_URL</c> environment variable, falling
    /// back to the default and logging a warning if the variable is set but not a valid absolute URI. The
    /// provider must always be able to hand back a client (see the class remarks), so this never throws.
    /// </summary>
    private Uri ResolveBaseAddress()
    {
        var overrideUrl = Environment.GetEnvironmentVariable("DTDD_API_BASE_URL");
        if (string.IsNullOrWhiteSpace(overrideUrl))
        {
            return DefaultBaseAddress;
        }

        if (Uri.TryCreate(overrideUrl, UriKind.Absolute, out var uri))
        {
            return uri;
        }

        _logger.LogWarning(
            "DTDD_API_BASE_URL value '{OverrideUrl}' is not a valid absolute URI; falling back to the default DoesTheDogDie API base address.",
            overrideUrl);
        return DefaultBaseAddress;
    }

    /// <summary>
    /// Builds a new client/throttle pair entirely into locals, only handing back once every step has
    /// succeeded. If a later step throws, whatever was already constructed is disposed before rethrowing, so
    /// a failed rebuild (e.g. an unexpected cache-open failure) can never leave an orphaned
    /// <see cref="ThrottledDtddClient"/> — and its live background loop — unreachable and undisposed.
    /// </summary>
    private (CachedDtddClient Client, ThrottledDtddClient Throttled) BuildStack(PluginConfiguration config)
    {
        // DtddApiClient's constructor rejects a null/empty/whitespace API key outright, and even if it didn't,
        // sending real HTTP requests with no real key would just hammer doesthedogdie.com with 401s (one per
        // cache miss, unbounded for an unconfigured plugin scanning a large library). Instead, with no key
        // configured, the API layer of the stack is a local null object (UnconfiguredApiClient) that fails
        // every call instantly with no network I/O. In practice DtddMetadataService short-circuits before it
        // ever reaches the client when no key is set, so this only backstops the configuration page's own
        // endpoints.
        IDtddApiClient api = string.IsNullOrWhiteSpace(config.ApiKey)
            ? new UnconfiguredApiClient()
            : new DtddApiClient(
                _httpClient,
                new DtddApiOptions { ApiKey = config.ApiKey, BaseAddress = ResolveBaseAddress() },
                TimeProvider.System);

        var throttled = new ThrottledDtddClient(api);

        try
        {
            var policy = new CachePolicy
            {
                ItemMaxAge = TimeSpan.FromDays(config.ItemCacheDays),
                RatingsMaxAge = TimeSpan.FromDays(config.ItemCacheDays),
                TaxonomyMaxAge = TimeSpan.FromDays(config.TaxonomyCacheDays),
            };

            var cache = CreateCache(policy);
            var client = new CachedDtddClient(throttled, cache, policy, TimeProvider.System);
            return (client, throttled);
        }
        catch
        {
            // The new throttle is brand new and has never served a call, so draining it here is fast and
            // bounded — unlike disposing a superseded, possibly-busy stack (see the class remarks), which is
            // exactly why this path is safe to run synchronously.
            throttled.DisposeAsync().AsTask().GetAwaiter().GetResult();
            throw;
        }
    }

    private IDtddCache CreateCache(CachePolicy policy)
    {
        try
        {
            Directory.CreateDirectory(_cacheDirectory);
            var dbPath = Path.Combine(_cacheDirectory, "dtdd-cache.db");
            return new SqliteDtddCache($"Data Source={dbPath}", policy, TimeProvider.System);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
            or TypeInitializationException or DllNotFoundException or DbException)
        {
            _logger.LogWarning(
                ex,
                "Could not open the SQLite DtDD cache; falling back to an in-memory cache. Cached data will not survive a restart");
            return new InMemoryDtddCache(policy, TimeProvider.System);
        }
    }

    /// <summary>
    /// A null-object <see cref="IDtddApiClient"/> used when no API key is configured. Every member fails
    /// immediately with <see cref="DtddAuthenticationException"/>, without ever touching the network, so an
    /// unconfigured plugin cannot spam doesthedogdie.com with junk-keyed requests.
    /// </summary>
    private sealed class UnconfiguredApiClient : IDtddApiClient
    {
        /// <inheritdoc />
        public Task<ApiResponse<IReadOnlyList<Item>>> SearchItemsAsync(ItemSearch search, CancellationToken ct = default) =>
            throw CreateException();

        /// <inheritdoc />
        public Task<ApiResponse<ItemDetail>> GetItemAsync(int itemId, CancellationToken ct = default) =>
            throw CreateException();

        /// <inheritdoc />
        public Task<ApiResponse<IReadOnlyList<Rating>>> GetRatingsAsync(int itemId, int? topicId = null, CancellationToken ct = default) =>
            throw CreateException();

        /// <inheritdoc />
        public Task<ApiResponse<IReadOnlyList<Topic>>> GetTopicsAsync(CancellationToken ct = default) =>
            throw CreateException();

        /// <inheritdoc />
        public Task<ApiResponse<IReadOnlyList<ItemType>>> GetItemTypesAsync(CancellationToken ct = default) =>
            throw CreateException();

        /// <inheritdoc />
        public Task<ApiResponse<IReadOnlyList<TopicCategory>>> GetTopicCategoriesAsync(CancellationToken ct = default) =>
            throw CreateException();

        /// <inheritdoc />
        public Task<ApiResponse<IReadOnlyList<TopicSuperCategory>>> GetTopicSuperCategoriesAsync(CancellationToken ct = default) =>
            throw CreateException();

        private static DtddAuthenticationException CreateException() =>
            new(HttpStatusCode.Unauthorized, "missing_api_key", "No DoesTheDogDie API key is configured.", rateLimit: null);
    }
}
