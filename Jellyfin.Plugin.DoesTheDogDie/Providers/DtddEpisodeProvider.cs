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
        DtddMediaDetails? details;

        if (!string.IsNullOrEmpty(existingDtddId) && !options.ReplaceAllMetadata)
        {
            // DTDD ID already exists — skip search but re-fetch details to re-evaluate tags
            if (int.TryParse(existingDtddId, out var dtddId))
            {
                _logger.LogDebug("Re-fetching DTDD data for episode {Name} (DTDD ID: {DtddId})", item.Name, dtddId);
                details = await _apiClient.GetMediaDetailsAsync(dtddId, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                return ItemUpdateType.None;
            }
        }
        else
        {
            details = await FetchDtddDetailsFromSeriesAsync(item, cancellationToken).ConfigureAwait(false);
            if (details != null)
            {
                item.SetProviderId(Constants.ProviderId, details.Item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        if (details == null)
        {
            _logger.LogDebug("No DTDD data found for episode {Name}", item.Name);
            return ItemUpdateType.None;
        }

        if (config.AddWarningTags)
        {
            TagHelper.UpdateWarningTags(item, details, config);
        }
        else
        {
            TagHelper.RemoveDtddTags(item, config);
        }

        _logger.LogInformation("Updated DTDD data for episode {Name} (ID: {DtddId})", item.Name, details.Item.Id);
        return ItemUpdateType.MetadataDownload;
    }

    private async Task<DtddMediaDetails?> FetchDtddDetailsFromSeriesAsync(Episode item, CancellationToken cancellationToken)
    {
        // Get the parent series to look up DTDD data
        var series = item.Series;
        if (series == null)
        {
            _logger.LogDebug("No parent series for episode {Name}, skipping DTDD lookup", item.Name);
            return null;
        }

        var imdbId = series.GetProviderId(MetadataProvider.Imdb);
        if (string.IsNullOrEmpty(imdbId))
        {
            _logger.LogDebug("No IMDB ID for parent series {SeriesName}, skipping DTDD lookup for episode", series.Name);
            return null;
        }

        _logger.LogDebug(
            "Fetching DTDD data for episode {Name} via series {SeriesName} (IMDB: {ImdbId})",
            item.Name,
            series.Name,
            imdbId);

        return await _apiClient.GetMediaDetailsByImdbIdAsync(imdbId, cancellationToken)
            .ConfigureAwait(false);
    }
}
