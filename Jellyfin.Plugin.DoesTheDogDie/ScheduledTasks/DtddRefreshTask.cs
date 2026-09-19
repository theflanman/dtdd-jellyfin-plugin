using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DoesTheDogDie.ScheduledTasks;

/// <summary>
/// Scheduled task that periodically refreshes DTDD data for all items
/// that already have DTDD provider IDs.
/// </summary>
public class DtddRefreshTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly DtddMetadataService _metadata;
    private readonly IPluginConfigurationAccessor _configAccessor;
    private readonly ILogger<DtddRefreshTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DtddRefreshTask"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="metadata">The DtDD metadata service.</param>
    /// <param name="configAccessor">The configuration accessor.</param>
    /// <param name="logger">The logger.</param>
    public DtddRefreshTask(
        ILibraryManager libraryManager,
        DtddMetadataService metadata,
        IPluginConfigurationAccessor configAccessor,
        ILogger<DtddRefreshTask> logger)
    {
        _libraryManager = libraryManager;
        _metadata = metadata;
        _configAccessor = configAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Refresh DoesTheDogDie Warnings";

    /// <inheritdoc />
    public string Description => "Updates content warnings from DoesTheDogDie.com for all items in your library.";

    /// <inheritdoc />
    public string Key => "DtddRefreshTask";

    /// <inheritdoc />
    public string Category => "Library";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Run daily at 2 AM by default
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.DailyTrigger,
                TimeOfDayTicks = TimeSpan.FromHours(2).Ticks
            }
        };
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = _configAccessor.GetConfiguration();
        if (config == null)
        {
            _logger.LogWarning("Plugin configuration not available, skipping refresh");
            return;
        }

        var itemsToRefresh = GetItemsWithDtddId(config);
        var total = itemsToRefresh.Count;

        if (total == 0)
        {
            _logger.LogInformation("No items with DTDD IDs found to refresh");
            progress?.Report(100);
            return;
        }

        _logger.LogInformation("Starting DTDD refresh for {Count} items", total);

        var failed = 0;

        for (int i = 0; i < total; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var percentComplete = (double)i / total * 100;
            progress?.Report(percentComplete);

            var item = itemsToRefresh[i];

            try
            {
                await RefreshItemAsync(item, config, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One bad item must not abort the whole scheduled run; log and move on to the next one.
                // A cancellation, however, must propagate: the scheduled task itself is being aborted.
                // The exception is logged as a sanitized string rather than as an exception object: the
                // API key can appear in a DtDD error message or URL, and only the string form goes
                // through LogSanitizer.
                _logger.LogWarning(
                    "Failed to refresh DTDD data for {ItemName}: {Message}",
                    item.Name,
                    LogSanitizer.Sanitize(ex.ToString(), config.ApiKey));
                failed++;
            }

            // Rate limiting - wait 200ms between API calls
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }

        progress?.Report(100);

        _logger.LogInformation(
            "DTDD refresh complete: {Failed} failed out of {Total} items",
            failed,
            total);
    }

    private List<BaseItem> GetItemsWithDtddId(PluginConfiguration config)
    {
        var items = new List<BaseItem>();

        // Select all movies/series that have an IMDB ID. This lets the task do the
        // initial population on existing libraries (which have no DTDD ID yet) as well
        // as refresh items already tagged. Items without an IMDB ID are skipped later.
        if (config.EnableMovies)
        {
            var movies = _libraryManager.GetItemList(new InternalItemsQuery
            {
                Recursive = true,
                IncludeItemTypes = new[] { BaseItemKind.Movie },
                HasAnyProviderId = new Dictionary<string, string>
                {
                    { MetadataProvider.Imdb.ToString(), string.Empty }
                }
            });
            items.AddRange(movies);
        }

        if (config.EnableSeries)
        {
            var series = _libraryManager.GetItemList(new InternalItemsQuery
            {
                Recursive = true,
                IncludeItemTypes = new[] { BaseItemKind.Series },
                HasAnyProviderId = new Dictionary<string, string>
                {
                    { MetadataProvider.Imdb.ToString(), string.Empty }
                }
            });
            items.AddRange(series);
        }

        return items;
    }

    private async Task RefreshItemAsync(BaseItem item, PluginConfiguration config, CancellationToken cancellationToken)
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
            new FetchReason(kind, IsRefresh: true, IsUserInitiated: false),
            cancellationToken).ConfigureAwait(false);

        if (data is null)
        {
            // Budget exhaustion, a queue overflow or a miss. Move on: the next item may still
            // be answerable from cache, and suppression is log-only inside DtddMetadataService.
            return;
        }

        item.SetProviderId(Constants.ProviderId, data.ItemId.ToString(CultureInfo.InvariantCulture));
        _metadata.Apply(item, data, config);
        await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataDownload, cancellationToken).ConfigureAwait(false);
    }
}
