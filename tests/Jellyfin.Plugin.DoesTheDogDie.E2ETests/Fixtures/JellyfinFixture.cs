using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.E2ETests.Fixtures;

/// <summary>
/// Spins up WireMock + Jellyfin LSIO containers on a shared Docker network,
/// drives the startup wizard, authenticates, and adds two libraries pointing
/// at the synthetic NFO fixtures.
/// </summary>
public sealed class JellyfinFixture : IAsyncLifetime
{
    public static readonly Guid PluginId = Guid.Parse("eb5d7894-8eef-4b36-aa6f-5d124e828ce1");

    private const string WireMockImage = "wiremock/wiremock:3.10.0";
    private const string JellyfinImage = "lscr.io/linuxserver/jellyfin:10.11.8";

    private INetwork? _network;
    private IContainer? _wiremock;
    private IContainer? _jellyfin;

    internal JellyfinClient Client { get; private set; } = null!;

    internal Uri JellyfinBaseAddress { get; private set; } = null!;

    internal string? JellyfinLogFile { get; private set; }

    public async Task InitializeAsync()
    {
        var paths = ResolvePaths();
        EnsurePluginPublished(paths.PluginPublishDir);
        WriteMetaJson(paths.PluginPublishDir);

        _network = new NetworkBuilder()
            .WithName($"dtdd-e2e-{Guid.NewGuid():N}")
            .Build();
        await _network.CreateAsync();

        _wiremock = new ContainerBuilder()
            .WithImage(WireMockImage)
            .WithNetwork(_network)
            .WithNetworkAliases("wiremock")
            .WithBindMount(paths.StubsDir, "/home/wiremock/mappings", AccessMode.ReadOnly)
            .WithCommand("--global-response-templating", "--verbose")
            .WithPortBinding(8080, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r.ForPort(8080).ForPath("/__admin/health")))
            .Build();
        await _wiremock.StartAsync();

        var logFile = Path.Combine(Path.GetTempPath(), $"jellyfin-dtdd-e2e-{Guid.NewGuid():N}.log");
        var consumer = new FileLogConsumer(logFile);

        _jellyfin = new ContainerBuilder()
            .WithImage(JellyfinImage)
            .WithNetwork(_network)
            .WithNetworkAliases("jellyfin")
            .WithEnvironment("PUID", "1000")
            .WithEnvironment("PGID", "1000")
            .WithEnvironment("TZ", "Etc/UTC")
            .WithEnvironment("JELLYFIN_PublishedServerUrl", "http://localhost")
            // DtddApiOptions.BaseAddress is normalized to end with '/' and every request is built
            // relative to it ("items?imdb=...", "items/{id}", "topics", ...), so this must carry the
            // full /api/v3/ path INCLUDING the trailing slash. Without it the relative URIs resolve
            // against the wrong path segment and miss every WireMock mapping.
            .WithEnvironment("DTDD_API_BASE_URL", "http://wiremock:8080/api/v3/")
            // The genuine publish output is mounted as-is: the suite must exercise the artifact users
            // actually install, not a sanitised copy of it. Mounted read-WRITE on purpose, because a
            // real plugin directory is writable and Jellyfin's PluginManager writes meta.json whenever
            // it changes a plugin's state. On a read-only mount that write throws IOException from
            // inside ApplicationHost.DiscoverTypes, which is fatal to the whole server — it never
            // finishes starting, and the suite hangs instead of failing.
            .WithBindMount(paths.PluginPublishDir, "/config/data/plugins/DoesTheDogDie_0.1.0.0", AccessMode.ReadWrite)
            .WithBindMount(paths.MoviesDir, "/data/movies", AccessMode.ReadOnly)
            .WithBindMount(paths.TvDir, "/data/tvshows", AccessMode.ReadOnly)
            .WithPortBinding(8096, true)
            .WithOutputConsumer(consumer)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r.ForPort(8096).ForPath("/System/Info/Public").WithMethod(System.Net.Http.HttpMethod.Get)))
            .Build();
        JellyfinLogFile = logFile;
        await _jellyfin.StartAsync();

        var mappedPort = _jellyfin.GetMappedPublicPort(8096);
        JellyfinBaseAddress = new Uri($"http://localhost:{mappedPort}");

        Client = new JellyfinClient(JellyfinBaseAddress);
        await Client.WaitForServerReadyAsync(TimeSpan.FromSeconds(180));
        await Client.CompleteStartupWizardAsync(adminUser: "test", password: "test");
        await Client.LoginAsync("test", "test");

        // Configure the plugin BEFORE the first scan: every provider no-ops without an API key, so a
        // fixture that skipped this would produce a green-but-empty run in which every assertion about
        // tags, provider ids and overviews passes vacuously. Read it back and fail loudly if it did not
        // stick.
        await AssertPluginLoadedAsync();
        await Client.SetPluginConfigurationAsync(PluginId, TestHelpers.DefaultPluginConfig());
        await AssertApiKeyConfiguredAsync();

        await Client.AddLibraryAsync("Movies", "movies", "/data/movies");
        await Client.AddLibraryAsync("Shows", "tvshows", "/data/tvshows");

        // Initial scan so subsequent tests see items present.
        await Client.TriggerAndWaitScanAsync(TimeSpan.FromSeconds(120));
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_jellyfin is not null)
        {
            await _jellyfin.DisposeAsync();
        }

        if (_wiremock is not null)
        {
            await _wiremock.DisposeAsync();
        }

        if (_network is not null)
        {
            await _network.DeleteAsync();
        }
    }

    /// <summary>
    /// Fails fast, with the plugin list in the message, unless Jellyfin actually loaded the plugin AND
    /// left it Active. Everything downstream (configuration, providers, the plugin's own API) 404s
    /// otherwise, and the resulting failures say nothing about the real cause.
    /// </summary>
    /// <remarks>
    /// Presence in /Plugins is NOT enough. A plugin Jellyfin loaded and then disabled — the packaging
    /// failure this guard exists to catch — is still listed, just with a non-Active Status, so a
    /// presence-only check would wave it through and the suite would fail later with unrelated-looking
    /// 404s and vacuously-empty metadata.
    /// </remarks>
    private async Task AssertPluginLoadedAsync()
    {
        using var plugins = await Client.GetPluginsAsync();

        foreach (var plugin in plugins.RootElement.EnumerateArray())
        {
            if (!IsDtddPlugin(plugin))
            {
                continue;
            }

            // Jellyfin's naming policy for this payload is not contractual, so match case-insensitively.
            // The Status may serialize as the enum's name or as its numeric value depending on the
            // server's converter set, so read whichever form arrives and report it verbatim on failure.
            string? status = null;
            foreach (var property in plugin.EnumerateObject())
            {
                if (string.Equals(property.Name, "Status", StringComparison.OrdinalIgnoreCase))
                {
                    status = property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString()
                        : property.Value.GetRawText();
                    break;
                }
            }

            if (!string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Jellyfin listed the DoesTheDogDie plugin but its Status is '{status ?? "<missing>"}', not 'Active'. "
                    + "A disabled plugin is still listed by /Plugins while none of its providers, services or endpoints run. "
                    + "Plugin entry: " + plugin.GetRawText());
            }

            return;
        }

        throw new InvalidOperationException(
            "Jellyfin did not load the DoesTheDogDie plugin. Installed plugins: "
            + plugins.RootElement.GetRawText());
    }

    private static bool IsDtddPlugin(JsonElement plugin)
    {
        foreach (var property in plugin.EnumerateObject())
        {
            if (string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(property.Value.GetString(), out var id)
                && id == PluginId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the Jellyfin container's captured stdout and stderr, so a test can assert on what the
    /// plugin logged inside the real host (for example, that the SQLite cache opened normally rather
    /// than falling back to the in-memory one).
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The container's combined log output.</returns>
    internal async Task<string> GetJellyfinLogsAsync(CancellationToken ct = default)
    {
        if (_jellyfin is null)
        {
            throw new InvalidOperationException("Jellyfin container has not been started");
        }

        var (stdout, stderr) = await _jellyfin.GetLogsAsync(timestampsEnabled: false, ct: ct);
        return stdout + Environment.NewLine + stderr;
    }

    /// <summary>
    /// Reads the plugin configuration back and throws unless a non-empty API key survived the POST.
    /// </summary>
    private async Task AssertApiKeyConfiguredAsync()
    {
        using var config = await Client.GetPluginConfigurationAsync(PluginId);

        // Jellyfin's naming policy for plugin configuration payloads is not contractual, so match the
        // property case-insensitively rather than assuming PascalCase.
        string? key = null;
        foreach (var property in config.RootElement.EnumerateObject())
        {
            if (string.Equals(property.Name, "ApiKey", StringComparison.OrdinalIgnoreCase))
            {
                key = property.Value.GetString();
                break;
            }
        }

        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException(
                "E2E fixture failed to set the DtDD API key. Every provider would silently no-op and the suite would pass while asserting nothing.");
        }
    }

    private static FixturePaths ResolvePaths()
    {
        var baseDir = AppContext.BaseDirectory;
        // bin/Debug/net9.0 → up 5 to repo root
        var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        return new FixturePaths(
            StubsDir: Path.Combine(baseDir, "Stubs", "wiremock-mappings"),
            MoviesDir: Path.Combine(baseDir, "Fixtures", "media", "movies"),
            TvDir: Path.Combine(baseDir, "Fixtures", "media", "tv"),
            PluginPublishDir: Path.Combine(repoRoot, "Jellyfin.Plugin.DoesTheDogDie", "bin", "Debug", "net9.0", "publish"));
    }

    private static void EnsurePluginPublished(string publishDir)
    {
        if (!Directory.Exists(publishDir) || Directory.GetFiles(publishDir, "Jellyfin.Plugin.DoesTheDogDie.dll").Length == 0)
        {
            throw new InvalidOperationException(
                $"Plugin publish output not found at {publishDir}. " +
                "The E2E csproj should run `dotnet publish` BeforeTargets=Build; check the build log.");
        }
    }

    private static void WriteMetaJson(string publishDir)
    {
        // Jellyfin's plugin loader needs meta.json to register a plugin. Defaults observed in
        // installed plugins on a real server: status="Active" (otherwise PluginManager treats
        // the plugin as Disabled), autoUpdate=true, assemblies=[]. Without "Active" the plugin
        // never reaches the IsEnabledAndSupported gate and never loads.
        var meta = """
        {
          "guid": "eb5d7894-8eef-4b36-aa6f-5d124e828ce1",
          "name": "Does The Dog Die",
          "overview": "Display content warnings from DoesTheDogDie.com",
          "description": "Integrates with DoesTheDogDie.com to display content warnings and trigger information.",
          "category": "General",
          "owner": "theflanman",
          "targetAbi": "10.11.0.0",
          "version": "0.1.0.0",
          "status": "Active",
          "autoUpdate": false,
          "imagePath": "",
          "assemblies": [],
          "changelog": ""
        }
        """;
        File.WriteAllText(Path.Combine(publishDir, "meta.json"), meta);
    }

    private sealed record FixturePaths(string StubsDir, string MoviesDir, string TvDir, string PluginPublishDir);

    private sealed class FileLogConsumer : DotNet.Testcontainers.Configurations.IOutputConsumer
    {
        private readonly System.IO.FileStream _stdout;
        private readonly System.IO.FileStream _stderr;

        public FileLogConsumer(string path)
        {
            _stdout = System.IO.File.Open(path, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.Read);
            _stderr = _stdout;
        }

        public bool Enabled => true;

        public System.IO.Stream Stdout => _stdout;

        public System.IO.Stream Stderr => _stderr;

        public void Dispose() => _stdout.Dispose();
    }
}

[CollectionDefinition("Jellyfin", DisableParallelization = true)]
public sealed class JellyfinCollection : ICollectionFixture<JellyfinFixture>
{
}
