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
/// Custom metadata provider that fetches DoesTheDogDie content warnings for TV series.
/// </summary>
public class DtddSeriesProvider : ICustomMetadataProvider<Series>, IHasOrder
{
    private readonly DtddApiClient _apiClient;
    private readonly IPluginConfigurationAccessor _configAccessor;
    private readonly ILogger<DtddSeriesProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DtddSeriesProvider"/> class.
    /// </summary>
    /// <param name="apiClient">The DTDD API client.</param>
    /// <param name="configAccessor">The configuration accessor.</param>
    /// <param name="logger">The logger.</param>
    public DtddSeriesProvider(
        DtddApiClient apiClient,
        IPluginConfigurationAccessor configAccessor,
        ILogger<DtddSeriesProvider> logger)
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
        Series item,
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
                _logger.LogDebug("Re-fetching DTDD data for series {Name} (DTDD ID: {DtddId})", item.Name, dtddId);
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
            details = await FetchDtddDetailsAsync(item, cancellationToken).ConfigureAwait(false);
            if (details != null)
            {
                item.SetProviderId(Constants.ProviderId, details.Item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        if (details == null)
        {
            _logger.LogDebug("No DTDD data found for series {Name}", item.Name);
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

        _logger.LogInformation("Updated DTDD data for series {Name} (ID: {DtddId})", item.Name, details.Item.Id);
        return ItemUpdateType.MetadataDownload;
    }

    private async Task<DtddMediaDetails?> FetchDtddDetailsAsync(Series item, CancellationToken cancellationToken)
    {
        // Try IMDB lookup first (most reliable)
        var imdbId = item.GetProviderId(MetadataProvider.Imdb);
        if (!string.IsNullOrEmpty(imdbId))
        {
            _logger.LogDebug("Fetching DTDD data for series {Name} (IMDB: {ImdbId})", item.Name, imdbId);
            var details = await _apiClient.GetMediaDetailsByImdbIdAsync(imdbId, cancellationToken).ConfigureAwait(false);
            if (details != null)
            {
                return details;
            }

            _logger.LogDebug("IMDB lookup failed for series {Name}, trying title search", item.Name);
        }
        else
        {
            _logger.LogDebug("No IMDB ID for series {Name}, trying title search", item.Name);
        }

        // Fall back to title-based search
        _logger.LogDebug("Searching DTDD by title for series {Name} ({Year})", item.Name, item.ProductionYear);
        return await _apiClient.GetMediaDetailsByTitleAsync(
            item.Name,
            item.ProductionYear,
            Constants.DtddItemTypeSeries,
            cancellationToken).ConfigureAwait(false);
    }
}
