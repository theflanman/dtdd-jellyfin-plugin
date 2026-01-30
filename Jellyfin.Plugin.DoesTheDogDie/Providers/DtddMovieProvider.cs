using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.DoesTheDogDie.Api;
using Jellyfin.Plugin.DoesTheDogDie.Api.Models;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DoesTheDogDie.Providers;

/// <summary>
/// Custom metadata provider that fetches DoesTheDogDie content warnings for movies.
/// </summary>
public class DtddMovieProvider : ICustomMetadataProvider<Movie>, IHasOrder
{
    private readonly DtddApiClient _apiClient;
    private readonly IPluginConfigurationAccessor _configAccessor;
    private readonly ILogger<DtddMovieProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DtddMovieProvider"/> class.
    /// </summary>
    /// <param name="apiClient">The DTDD API client.</param>
    /// <param name="configAccessor">The configuration accessor.</param>
    /// <param name="logger">The logger.</param>
    public DtddMovieProvider(
        DtddApiClient apiClient,
        IPluginConfigurationAccessor configAccessor,
        ILogger<DtddMovieProvider> logger)
    {
        _apiClient = apiClient;
        _configAccessor = configAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => Constants.ProviderName;

    /// <inheritdoc />
    /// <remarks>
    /// High order value ensures we run after TMDB/TVDB providers
    /// which populate the IMDB ID we need for lookups.
    /// </remarks>
    public int Order => 100;

    /// <inheritdoc />
    public async Task<ItemUpdateType> FetchAsync(
        Movie item,
        MetadataRefreshOptions options,
        CancellationToken cancellationToken)
    {
        var config = _configAccessor.GetConfiguration();
        if (config == null || !config.EnableMovies)
        {
            return ItemUpdateType.None;
        }

        var imdbId = item.GetProviderId(MetadataProvider.Imdb);
        if (string.IsNullOrEmpty(imdbId))
        {
            _logger.LogDebug("No IMDB ID for movie {Name}, skipping DTDD lookup", item.Name);
            return ItemUpdateType.None;
        }

        var existingDtddId = item.GetProviderId(Constants.ProviderId);
        if (!string.IsNullOrEmpty(existingDtddId) && !options.ReplaceAllMetadata)
        {
            _logger.LogDebug("DTDD ID already exists for movie {Name}", item.Name);
            return ItemUpdateType.None;
        }

        _logger.LogDebug("Fetching DTDD data for movie {Name} (IMDB: {ImdbId})", item.Name, imdbId);

        var details = await _apiClient.GetMediaDetailsByImdbIdAsync(imdbId, cancellationToken)
            .ConfigureAwait(false);

        if (details == null)
        {
            _logger.LogDebug("No DTDD data found for movie {Name}", item.Name);
            return ItemUpdateType.None;
        }

        item.SetProviderId(Constants.ProviderId, details.Item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));

        if (config.AddWarningTags)
        {
            AddWarningTags(item, details, config);
        }

        _logger.LogInformation("Added DTDD data for movie {Name} (ID: {DtddId})", item.Name, details.Item.Id);
        return ItemUpdateType.MetadataDownload;
    }

    private static void AddWarningTags(Movie item, DtddMediaDetails details, PluginConfiguration config)
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
