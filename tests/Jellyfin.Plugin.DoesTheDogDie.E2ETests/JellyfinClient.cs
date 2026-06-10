using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.DoesTheDogDie.E2ETests;

/// <summary>
/// Thin REST wrapper around a running Jellyfin instance. Drives the startup wizard,
/// authenticates, manages libraries/plugin config, and triggers + waits for scans.
/// </summary>
internal sealed class JellyfinClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly string _deviceId;
    private string? _accessToken;

    public JellyfinClient(Uri baseAddress)
    {
        _http = new HttpClient { BaseAddress = baseAddress, Timeout = TimeSpan.FromSeconds(60) };
        _deviceId = Guid.NewGuid().ToString("N");
        SetAuthorizationHeader();
    }

    public string? AccessToken => _accessToken;

    public async Task WaitForServerReadyAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                // /Startup/Configuration returns 200 only after the server has finished booting
                // and the startup wizard endpoints are wired up. /System/Info/Public flips to 200
                // much earlier (while the app is still initializing) and produces 503s downstream.
                using var resp = await _http.GetAsync("/Startup/Configuration", ct);
                if (resp.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                last = ex;
            }

            await Task.Delay(1000, ct);
        }

        throw new TimeoutException($"Jellyfin wizard endpoint not ready within {timeout}", last);
    }

    public async Task CompleteStartupWizardAsync(string adminUser, string password, string uiCulture = "en-US", CancellationToken ct = default)
    {
        // The wizard endpoints occasionally return 500/503 in the first second or two after
        // Jellyfin's web pipeline comes up — plugins are still being registered. Retry briefly.
        await SendWithRetryAsync(
            () => _http.PostAsJsonAsync("/Startup/Configuration", new StartupConfigurationDto
            {
                UICulture = uiCulture,
                MetadataCountryCode = "US",
                PreferredMetadataLanguage = "en",
            }, JsonOptions, ct),
            ct: ct);

        // GET /Startup/User triggers _userManager.InitializeAsync which seeds the default user
        // record. POST then mutates that record. Without the GET first, POST throws
        // "Sequence contains no elements" on _userManager.Users.First().
        await SendWithRetryAsync(() => _http.GetAsync("/Startup/User", ct), ct: ct);
        await SendWithRetryAsync(
            () => _http.PostAsJsonAsync("/Startup/User", new StartupUserDto
            {
                Name = adminUser,
                Password = password,
            }, JsonOptions, ct),
            ct: ct);

        await SendWithRetryAsync(
            () => _http.PostAsJsonAsync("/Startup/RemoteAccess", new StartupRemoteAccessDto
            {
                EnableRemoteAccess = false,
                EnableAutomaticPortMapping = false,
            }, JsonOptions, ct),
            ct: ct);

        await SendWithRetryAsync(() => _http.PostAsync("/Startup/Complete", content: null, ct), ct: ct);
    }

    private static async Task SendWithRetryAsync(
        Func<Task<HttpResponseMessage>> send,
        int maxAttempts = 12,
        CancellationToken ct = default)
    {
        HttpResponseMessage? last = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            last?.Dispose();
            try
            {
                last = await send();
                if (last.IsSuccessStatusCode)
                {
                    last.Dispose();
                    return;
                }

                if ((int)last.StatusCode < 500)
                {
                    last.EnsureSuccessStatusCode();
                }
            }
            catch (HttpRequestException) when (attempt < maxAttempts)
            {
                // transient — retry
            }

            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }

        last?.EnsureSuccessStatusCode();
    }

    public async Task LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/Users/AuthenticateByName", new
        {
            Username = username,
            Pw = password,
        }, JsonOptions, ct);
        resp.EnsureSuccessStatusCode();

        var auth = await resp.Content.ReadFromJsonAsync<AuthenticationResultDto>(JsonOptions, ct);
        _accessToken = auth?.AccessToken ?? throw new InvalidOperationException("AuthenticateByName returned no AccessToken");
        SetAuthorizationHeader();
    }

    public async Task AddLibraryAsync(string name, string collectionType, string path, CancellationToken ct = default)
    {
        var url = $"/Library/VirtualFolders?name={Uri.EscapeDataString(name)}&collectionType={collectionType}&paths={Uri.EscapeDataString(path)}&refreshLibrary=false";
        var resp = await _http.PostAsync(url, content: null, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task SetPluginConfigurationAsync(Guid pluginId, object configuration, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"/Plugins/{pluginId:D}/Configuration", configuration, JsonOptions, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<JsonDocument> GetPluginConfigurationAsync(Guid pluginId, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"/Plugins/{pluginId:D}/Configuration", ct);
        resp.EnsureSuccessStatusCode();
        var stream = await resp.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    public async Task TriggerLibraryScanAsync(CancellationToken ct = default)
    {
        var resp = await _http.PostAsync("/Library/Refresh", content: null, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task WaitForScanIdleAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        // First wait for the scan to start (status leaves Idle), then wait for it to return to Idle.
        var observedRunning = false;
        while (DateTime.UtcNow < deadline)
        {
            var task = await GetScanLibraryTaskAsync(ct);
            var state = task?.State ?? "Idle";
            if (!observedRunning && state != "Idle")
            {
                observedRunning = true;
            }
            else if (observedRunning && state == "Idle")
            {
                return;
            }
            else if (observedRunning)
            {
                // still running
            }

            await Task.Delay(500, ct);
        }

        // If we never saw it running, it may have completed instantly — accept Idle as success.
        var final = await GetScanLibraryTaskAsync(ct);
        if (final?.State == "Idle")
        {
            return;
        }

        throw new TimeoutException($"Library scan did not return to Idle within {timeout}");
    }

    public async Task TriggerAndWaitScanAsync(TimeSpan? timeout = null, CancellationToken ct = default)
    {
        await TriggerLibraryScanAsync(ct);
        await WaitForScanIdleAsync(timeout ?? TimeSpan.FromSeconds(120), ct);
    }

    public async Task RefreshItemMetadataAsync(string itemId, bool replaceAllMetadata = false, CancellationToken ct = default)
    {
        var url = $"/Items/{itemId}/Refresh?metadataRefreshMode=FullRefresh&imageRefreshMode=Default&replaceAllMetadata={(replaceAllMetadata ? "true" : "false")}&replaceAllImages=false";
        var resp = await _http.PostAsync(url, content: null, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<HttpResponseMessage> GetConfigurationPageAsync(string pluginDisplayName, CancellationToken ct = default)
    {
        var url = $"/web/ConfigurationPage?name={Uri.EscapeDataString(pluginDisplayName)}";
        return await _http.GetAsync(url, ct);
    }

    public async Task<List<JellyfinItemDto>> GetItemsAsync(string includeItemTypes, CancellationToken ct = default)
    {
        var url = $"/Items?Recursive=true&Fields=Tags,ProviderIds,Overview&IncludeItemTypes={includeItemTypes}";
        var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        var page = await resp.Content.ReadFromJsonAsync<ItemsResultDto>(JsonOptions, ct);
        return page?.Items ?? new List<JellyfinItemDto>();
    }

    public Task LockOverviewAsync(string itemId, CancellationToken ct = default)
        => SetLockedFieldsAsync(itemId, new[] { "Overview" }, ct);

    public Task UnlockAllFieldsAsync(string itemId, CancellationToken ct = default)
        => SetLockedFieldsAsync(itemId, Array.Empty<string>(), ct);

    private async Task SetLockedFieldsAsync(string itemId, string[] lockedFields, CancellationToken ct)
    {
        var itemResp = await _http.GetAsync($"/Items/{itemId}", ct);
        itemResp.EnsureSuccessStatusCode();
        var stream = await itemResp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var node = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(doc.RootElement.GetRawText(), JsonOptions)!;
        node["LockedFields"] = JsonSerializer.SerializeToElement(lockedFields, JsonOptions);

        var update = await _http.PostAsJsonAsync($"/Items/{itemId}", node, JsonOptions, ct);
        update.EnsureSuccessStatusCode();
    }

    public void Dispose() => _http.Dispose();

    private async Task<ScheduledTaskDto?> GetScanLibraryTaskAsync(CancellationToken ct)
    {
        var resp = await _http.GetAsync("/ScheduledTasks", ct);
        resp.EnsureSuccessStatusCode();
        var tasks = await resp.Content.ReadFromJsonAsync<List<ScheduledTaskDto>>(JsonOptions, ct);
        if (tasks is null)
        {
            return null;
        }

        foreach (var t in tasks)
        {
            if (string.Equals(t.Key, "RefreshLibrary", StringComparison.OrdinalIgnoreCase) ||
                (t.Name?.Contains("Scan", StringComparison.OrdinalIgnoreCase) == true && t.Name?.Contains("Library", StringComparison.OrdinalIgnoreCase) == true))
            {
                return t;
            }
        }

        return null;
    }

    private void SetAuthorizationHeader()
    {
        _http.DefaultRequestHeaders.Remove("Authorization");
        var parts = new List<string>
        {
            "Client=\"E2E\"",
            "Device=\"xunit\"",
            $"DeviceId=\"{_deviceId}\"",
            "Version=\"1.0.0\"",
        };
        if (!string.IsNullOrEmpty(_accessToken))
        {
            parts.Insert(0, $"Token=\"{_accessToken}\"");
        }

        _http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "MediaBrowser " + string.Join(", ", parts));
    }

    private sealed class StartupConfigurationDto
    {
        public string? UICulture { get; set; }

        public string? MetadataCountryCode { get; set; }

        public string? PreferredMetadataLanguage { get; set; }
    }

    private sealed class StartupUserDto
    {
        public string? Name { get; set; }

        public string? Password { get; set; }
    }

    private sealed class StartupRemoteAccessDto
    {
        public bool EnableRemoteAccess { get; set; }

        public bool EnableAutomaticPortMapping { get; set; }
    }

    private sealed class AuthenticationResultDto
    {
        public string? AccessToken { get; set; }

        public string? ServerId { get; set; }
    }

    internal sealed class ScheduledTaskDto
    {
        public string? Name { get; set; }

        public string? Key { get; set; }

        public string? State { get; set; }

        public double? CurrentProgressPercentage { get; set; }
    }

    internal sealed class ItemsResultDto
    {
        public List<JellyfinItemDto> Items { get; set; } = new();

        public int TotalRecordCount { get; set; }
    }

    internal sealed class JellyfinItemDto
    {
        public string Id { get; set; } = string.Empty;

        public string? Name { get; set; }

        public string? Type { get; set; }

        public string? Overview { get; set; }

        public string? SeriesId { get; set; }

        public List<string> Tags { get; set; } = new();

        public Dictionary<string, string> ProviderIds { get; set; } = new();
    }
}
