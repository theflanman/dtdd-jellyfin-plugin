using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.DoesTheDogDie.Api;
using Jellyfin.Plugin.DoesTheDogDie.Api.Models;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DoesTheDogDie.Services;

/// <summary>
/// Background service that listens for library changes and triggers DTDD lookups
/// for new items that have IMDB IDs but no DTDD data.
/// </summary>
public class DtddLibraryScanService : IHostedService
{
    private readonly ILibraryManager _libraryManager;
    private readonly DtddApiClient _apiClient;
    private readonly IPluginConfigurationAccessor _configAccessor;
    private readonly ILogger<DtddLibraryScanService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DtddLibraryScanService"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="apiClient">The DTDD API client.</param>
    /// <param name="configAccessor">The configuration accessor.</param>
    /// <param name="logger">The logger.</param>
    public DtddLibraryScanService(
        ILibraryManager libraryManager,
        DtddApiClient apiClient,
        IPluginConfigurationAccessor configAccessor,
        ILogger<DtddLibraryScanService> logger)
    {
        _libraryManager = libraryManager;
        _apiClient = apiClient;
        _configAccessor = configAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded += OnItemChanged;
        _libraryManager.ItemUpdated += OnItemChanged;
        _logger.LogInformation("DTDD Library Scan Service started");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded -= OnItemChanged;
        _libraryManager.ItemUpdated -= OnItemChanged;
        _logger.LogInformation("DTDD Library Scan Service stopped");
        return Task.CompletedTask;
    }

    private void OnItemChanged(object? sender, ItemChangeEventArgs e)
    {
        var item = e.Item;

        // Only process Movies and Series
        if (item is not Movie && item is not Series)
        {
            return;
        }

        // Check configuration
        var config = _configAccessor.GetConfiguration();
        if (config == null)
        {
            return;
        }

        if (item is Movie && !config.EnableMovies)
        {
            return;
        }

        if (item is Series && !config.EnableSeries)
        {
            return;
        }

        // Must have IMDB ID for lookup
        var imdbId = item.GetProviderId(MetadataProvider.Imdb);
        if (string.IsNullOrEmpty(imdbId))
        {
            return;
        }

        // Skip if already has DTDD ID (already processed)
        var existingDtddId = item.GetProviderId(Constants.ProviderId);
        if (!string.IsNullOrEmpty(existingDtddId))
        {
            return;
        }

        _logger.LogDebug("Queueing DTDD lookup for {ItemName} (IMDB: {ImdbId})", item.Name, imdbId);

        // Fire and forget - don't block the library scan
        _ = ProcessItemAsync(item, imdbId, config);
    }

    private async Task ProcessItemAsync(BaseItem item, string imdbId, PluginConfiguration config)
    {
        try
        {
            var details = await _apiClient.GetMediaDetailsByImdbIdAsync(imdbId, CancellationToken.None)
                .ConfigureAwait(false);

            if (details == null)
            {
                _logger.LogDebug("No DTDD data found for {ItemName}", item.Name);
                return;
            }

            // Store the DTDD ID
            item.SetProviderId(
                Constants.ProviderId,
                details.Item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));

            // Add warning tags if enabled
            if (config.AddWarningTags)
            {
                AddWarningTags(item, details, config);
            }

            _logger.LogInformation(
                "Added DTDD data for {ItemName} (DTDD ID: {DtddId})",
                item.Name,
                details.Item.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch DTDD data for {ItemName}", item.Name);
        }
    }

    private static void AddWarningTags(BaseItem item, DtddMediaDetails details, PluginConfiguration config)
    {
        // Add positive triggers (content warnings)
        var positiveTriggers = details.GetPositiveTriggers(config.MinVotesThreshold);
        foreach (var trigger in positiveTriggers)
        {
            if (trigger.Topic == null)
            {
                continue;
            }

            var tagName = $"{config.TagPrefix} {trigger.Topic.Name}";
            if (!item.Tags.Contains(tagName, StringComparer.OrdinalIgnoreCase))
            {
                item.Tags = item.Tags.Append(tagName).ToArray();
            }
        }

        // Add negative triggers (safe confirmations)
        var negativeTriggers = details.GetNegativeTriggers(config.MinVotesThreshold);
        foreach (var trigger in negativeTriggers)
        {
            if (trigger.Topic == null)
            {
                continue;
            }

            var tagName = $"{config.SafeTagPrefix} {trigger.Topic.Name}";
            if (!item.Tags.Contains(tagName, StringComparer.OrdinalIgnoreCase))
            {
                item.Tags = item.Tags.Append(tagName).ToArray();
            }
        }
    }
}
