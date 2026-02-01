using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.DoesTheDogDie.Api;
using Jellyfin.Plugin.DoesTheDogDie.Api.Models;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
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
    private readonly DtddApiClient _apiClient;
    private readonly IPluginConfigurationAccessor _configAccessor;
    private readonly ILogger<DtddRefreshTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DtddRefreshTask"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="apiClient">The DTDD API client.</param>
    /// <param name="configAccessor">The configuration accessor.</param>
    /// <param name="logger">The logger.</param>
    public DtddRefreshTask(
        ILibraryManager libraryManager,
        DtddApiClient apiClient,
        IPluginConfigurationAccessor configAccessor,
        ILogger<DtddRefreshTask> logger)
    {
        _libraryManager = libraryManager;
        _apiClient = apiClient;
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

        var refreshed = 0;
        var failed = 0;
        var unchanged = 0;

        for (int i = 0; i < total; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var percentComplete = (double)i / total * 100;
            progress?.Report(percentComplete);

            var item = itemsToRefresh[i];

            try
            {
                var wasUpdated = await RefreshItemAsync(item, config, cancellationToken).ConfigureAwait(false);
                if (wasUpdated)
                {
                    refreshed++;
                }
                else
                {
                    unchanged++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh DTDD data for {ItemName}", item.Name);
                failed++;
            }

            // Rate limiting - wait 200ms between API calls
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }

        progress?.Report(100);

        _logger.LogInformation(
            "DTDD refresh complete: {Refreshed} updated, {Unchanged} unchanged, {Failed} failed out of {Total} items",
            refreshed,
            unchanged,
            failed,
            total);
    }

    private List<BaseItem> GetItemsWithDtddId(PluginConfiguration config)
    {
        var items = new List<BaseItem>();

        if (config.EnableMovies)
        {
            var movies = _libraryManager.GetItemList(new InternalItemsQuery
            {
                Recursive = true,
                IncludeItemTypes = new[] { BaseItemKind.Movie },
                HasAnyProviderId = new Dictionary<string, string>
                {
                    { Constants.ProviderId, string.Empty }
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
                    { Constants.ProviderId, string.Empty }
                }
            });
            items.AddRange(series);
        }

        return items;
    }

    private async Task<bool> RefreshItemAsync(
        BaseItem item,
        PluginConfiguration config,
        CancellationToken cancellationToken)
    {
        var imdbId = item.GetProviderId(MetadataProvider.Imdb);
        if (string.IsNullOrEmpty(imdbId))
        {
            _logger.LogDebug("Item {ItemName} has DTDD ID but no IMDB ID, skipping", item.Name);
            return false;
        }

        var details = await _apiClient.GetMediaDetailsByImdbIdAsync(imdbId, cancellationToken)
            .ConfigureAwait(false);

        if (details == null)
        {
            _logger.LogDebug("No DTDD data found for {ItemName}", item.Name);
            return false;
        }

        // Update the DTDD ID (in case it changed)
        item.SetProviderId(
            Constants.ProviderId,
            details.Item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));

        // Update warning tags if enabled
        if (config.AddWarningTags)
        {
            var tagsChanged = UpdateWarningTags(item, details, config);
            return tagsChanged;
        }

        return false;
    }

    private static bool UpdateWarningTags(BaseItem item, DtddMediaDetails details, PluginConfiguration config)
    {
        // First, remove all existing DTDD tags (those starting with our prefixes)
        var existingTags = item.Tags
            .Where(t => !t.StartsWith(config.TagPrefix, StringComparison.OrdinalIgnoreCase) &&
                        !t.StartsWith(config.SafeTagPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var originalTagCount = item.Tags.Length;
        var nonDtddTagCount = existingTags.Count;

        // Add positive triggers (content warnings)
        var positiveTriggers = TriggerFilter.FilterTriggers(
            details.GetPositiveTriggers(config.MinVotesThreshold),
            config);

        foreach (var trigger in positiveTriggers)
        {
            if (trigger.Topic == null)
            {
                continue;
            }

            var tagName = $"{config.TagPrefix} {trigger.Topic.Name}";
            if (!existingTags.Contains(tagName, StringComparer.OrdinalIgnoreCase))
            {
                existingTags.Add(tagName);
            }
        }

        // Add negative triggers (safe confirmations)
        var negativeTriggers = TriggerFilter.FilterTriggers(
            details.GetNegativeTriggers(config.MinVotesThreshold),
            config);

        foreach (var trigger in negativeTriggers)
        {
            if (trigger.Topic == null)
            {
                continue;
            }

            var tagName = $"{config.SafeTagPrefix} {trigger.Topic.Name}";
            if (!existingTags.Contains(tagName, StringComparer.OrdinalIgnoreCase))
            {
                existingTags.Add(tagName);
            }
        }

        // Check if tags actually changed (either count changed or we removed/added DTDD tags)
        var tagsChanged = existingTags.Count != originalTagCount ||
                          (originalTagCount - nonDtddTagCount) != (existingTags.Count - nonDtddTagCount);

        item.Tags = existingTags.ToArray();

        return tagsChanged;
    }
}
