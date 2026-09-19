using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Services;
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
    private readonly DtddMetadataService _metadata;
    private readonly IPluginConfigurationAccessor _configAccessor;
    private readonly ILogger<DtddMovieProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DtddMovieProvider"/> class.
    /// </summary>
    /// <param name="metadata">The DtDD metadata service.</param>
    /// <param name="configAccessor">The configuration accessor.</param>
    /// <param name="logger">The logger.</param>
    public DtddMovieProvider(
        DtddMetadataService metadata,
        IPluginConfigurationAccessor configAccessor,
        ILogger<DtddMovieProvider> logger)
    {
        _metadata = metadata;
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
        if (config is null || !config.EnableMovies)
        {
            return ItemUpdateType.None;
        }

        var storedId = item.GetProviderId(Constants.ProviderId);
        var reason = new FetchReason(
            DtddItemKind.Movie,
            IsRefresh: !string.IsNullOrEmpty(storedId),
            IsUserInitiated: options?.ReplaceAllMetadata == true);

        var data = await _metadata.ResolveAsync(
            storedId,
            item.GetProviderId(MetadataProvider.Imdb),
            item.Name,
            item.ProductionYear,
            reason,
            cancellationToken).ConfigureAwait(false);

        if (data is null)
        {
            _logger.LogDebug("No DTDD data found for movie {Name}", item.Name);
            return ItemUpdateType.None;
        }

        item.SetProviderId(Constants.ProviderId, data.ItemId.ToString(CultureInfo.InvariantCulture));

        try
        {
            _metadata.Apply(item, data, config);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Applying tags/overview must not fail the whole refresh for this item. The provider id was
            // already set above and is worth persisting on its own, so MetadataDownload is still returned.
            // A cancellation still propagates: the refresh itself is being aborted.
            _logger.LogWarning(
                "Failed to apply DTDD data for movie {Name}: {Message}",
                item.Name,
                LogSanitizer.Sanitize(ex.ToString(), config.ApiKey));
        }

        _logger.LogInformation("Applied DTDD data for movie {Name} (ID: {DtddId})", item.Name, data.ItemId);
        return ItemUpdateType.MetadataDownload;
    }
}
