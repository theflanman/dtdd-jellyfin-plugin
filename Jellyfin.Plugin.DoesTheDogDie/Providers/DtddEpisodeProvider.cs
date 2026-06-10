using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.DoesTheDogDie.Api;
using Jellyfin.Plugin.DoesTheDogDie.Api.Models;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DoesTheDogDie.Providers;

/// <summary>
/// Custom metadata provider that fetches DoesTheDogDie content warnings for TV episodes.
/// Inherits warnings from the parent series since DTDD typically has series-level data.
/// </summary>
public class DtddEpisodeProvider : ICustomMetadataProvider<Episode>, IHasOrder
{
    private readonly DtddApiClient _apiClient;
    private readonly IPluginConfigurationAccessor _configAccessor;
    private readonly ILogger<DtddEpisodeProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DtddEpisodeProvider"/> class.
    /// </summary>
    /// <param name="apiClient">The DTDD API client.</param>
    /// <param name="configAccessor">The configuration accessor.</param>
    /// <param name="logger">The logger.</param>
    public DtddEpisodeProvider(
        DtddApiClient apiClient,
        IPluginConfigurationAccessor configAccessor,
        ILogger<DtddEpisodeProvider> logger)
    {
        _apiClient = apiClient;
        _configAccessor = configAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => Constants.ProviderName;

    /// <inheritdoc />
    public int Order => 100;

    /// <inheritdoc />
    public async Task<ItemUpdateType> FetchAsync(
        Episode item,
        MetadataRefreshOptions options,
        CancellationToken cancellationToken)
    {
        var config = _configAccessor.GetConfiguration();
        if (config == null || !config.EnableSeries)
        {
            return ItemUpdateType.None;
        }

        var existingDtddId = item.GetProviderId(Constants.ProviderId);
        if (!string.IsNullOrEmpty(existingDtddId) && !options.ReplaceAllMetadata)
        {
            if (config.AddWarningTags && int.TryParse(existingDtddId, System.Globalization.CultureInfo.InvariantCulture, out var parsedDtddId))
            {
                var cachedDetails = await _apiClient.GetMediaDetailsAsync(parsedDtddId, cancellationToken)
                    .ConfigureAwait(false);
                if (cachedDetails != null)
                {
                    AddWarningTags(item, cachedDetails, config);
                    return ItemUpdateType.MetadataDownload;
                }
            }

            _logger.LogDebug("DTDD ID already exists for episode {Name}", item.Name);
            return ItemUpdateType.None;
        }

        // Get the parent series to look up DTDD data
        var series = item.Series;
        if (series == null)
        {
            _logger.LogDebug("No parent series for episode {Name}, skipping DTDD lookup", item.Name);
            return ItemUpdateType.None;
        }

        var imdbId = series.GetProviderId(MetadataProvider.Imdb);
        if (string.IsNullOrEmpty(imdbId))
        {
            _logger.LogDebug("No IMDB ID for parent series {SeriesName}, skipping DTDD lookup for episode", series.Name);
            return ItemUpdateType.None;
        }

        _logger.LogDebug(
            "Fetching DTDD data for episode {Name} via series {SeriesName} (IMDB: {ImdbId})",
            item.Name,
            series.Name,
            imdbId);

        var details = await _apiClient.GetMediaDetailsByImdbIdAsync(imdbId, cancellationToken)
            .ConfigureAwait(false);

        if (details == null)
        {
            _logger.LogDebug("No DTDD data found for episode {Name}", item.Name);
            return ItemUpdateType.None;
        }

        // Store the series DTDD ID on the episode
        item.SetProviderId(Constants.ProviderId, details.Item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));

        if (config.AddWarningTags)
        {
            AddWarningTags(item, details, config);
        }

        _logger.LogInformation("Added DTDD data for episode {Name} (ID: {DtddId})", item.Name, details.Item.Id);
        return ItemUpdateType.MetadataDownload;
    }

    private static void AddWarningTags(Episode item, DtddMediaDetails details, PluginConfiguration config)
    {
        var existingTags = item.Tags
            .Where(t => !t.StartsWith(config.TagPrefix, StringComparison.OrdinalIgnoreCase) &&
                        !t.StartsWith(config.SafeTagPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var positiveTriggers = TriggerFilter.FilterTriggers(
            details.GetPositiveTriggers(config.MinVotesThreshold),
            config);

        foreach (var trigger in positiveTriggers)
        {
            if (trigger.Topic == null)
            {
                continue;
            }

            var tagName = TriggerTagFormatter.FormatTagName(config.TagPrefix, trigger, config)!;
            if (!existingTags.Contains(tagName, StringComparer.OrdinalIgnoreCase))
            {
                existingTags.Add(tagName);
            }
        }

        var negativeTriggers = TriggerFilter.FilterTriggers(
            details.GetNegativeTriggers(config.MinVotesThreshold),
            config);

        foreach (var trigger in negativeTriggers)
        {
            if (trigger.Topic == null)
            {
                continue;
            }

            var tagName = TriggerTagFormatter.FormatTagName(config.SafeTagPrefix, trigger, config)!;
            if (!existingTags.Contains(tagName, StringComparer.OrdinalIgnoreCase))
            {
                existingTags.Add(tagName);
            }
        }

        item.Tags = existingTags.ToArray();
    }
}
