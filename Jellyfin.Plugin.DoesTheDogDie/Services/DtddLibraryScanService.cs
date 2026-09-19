using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly DtddMetadataService _metadata;
    private readonly IPluginConfigurationAccessor _configAccessor;
    private readonly ILogger<DtddLibraryScanService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DtddLibraryScanService"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="metadata">The DtDD metadata service.</param>
    /// <param name="configAccessor">The configuration accessor.</param>
    /// <param name="logger">The logger.</param>
    public DtddLibraryScanService(
        ILibraryManager libraryManager,
        DtddMetadataService metadata,
        IPluginConfigurationAccessor configAccessor,
        ILogger<DtddLibraryScanService> logger)
    {
        _libraryManager = libraryManager;
        _metadata = metadata;
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

    internal void OnItemChanged(object? sender, ItemChangeEventArgs e)
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
        _ = ProcessItemAsync(item, CancellationToken.None);
    }

    /// <summary>
    /// Resolves and applies DtDD data for a single item. <see cref="DtddMetadataService.ResolveAsync"/>
    /// returning null is the only signal this method gets, whether the cause is a missing match, a rate
    /// limit, or an exhausted request budget, and the correct response is always to skip this item and let
    /// the caller move on to the next one. Nothing here tracks budget state or short-circuits future calls:
    /// the client stack's cache sits above its throttle, so items not yet reached may still resolve for
    /// free even while the throttle is exhausted.
    /// </summary>
    /// <param name="item">The item to process.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal virtual async Task ProcessItemAsync(BaseItem item, CancellationToken cancellationToken)
    {
        var config = _configAccessor.GetConfiguration();
        if (config is null)
        {
            return;
        }

        try
        {
            var storedId = item.GetProviderId(Constants.ProviderId);
            var kind = item switch
            {
                Movie => DtddItemKind.Movie,
                Series => DtddItemKind.Series,
                Season => DtddItemKind.Season,
                Episode => DtddItemKind.Episode,
                _ => DtddItemKind.Movie,
            };

            var data = await _metadata.ResolveAsync(
                storedId,
                item.GetProviderId(MetadataProvider.Imdb),
                item.Name,
                item.ProductionYear,
                new FetchReason(kind, IsRefresh: !string.IsNullOrEmpty(storedId), IsUserInitiated: false),
                cancellationToken).ConfigureAwait(false);

            if (data is null)
            {
                return;
            }

            item.SetProviderId(Constants.ProviderId, data.ItemId.ToString(CultureInfo.InvariantCulture));
            _metadata.Apply(item, data, config);

            // This service is not part of Jellyfin's ICustomMetadataProvider pipeline, which persists
            // automatically after a provider runs. It is a standalone hosted service reacting to library
            // events, so the provider id/tags/overview computed above must be saved explicitly or they are
            // silently discarded.
            //
            // MetadataDownload, not MetadataEdit: this is downloaded metadata, not a human edit. Metadata
            // savers write NFO files unconditionally on MetadataEdit but only when the user has opted into
            // saving metadata into media folders on MetadataDownload, and MetadataEdit additionally marks
            // the item as user-edited, which can suppress later refreshes from other providers. The four
            // providers and the scheduled refresh task all use MetadataDownload too.
            await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataDownload, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                "Failed to process DTDD data for {ItemName}: {Message}",
                item.Name,
                LogSanitizer.Sanitize(ex.ToString(), config.ApiKey));
        }
    }
}
