using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Services;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DoesTheDogDie.Providers;

/// <summary>
/// Custom metadata provider that inherits DoesTheDogDie warnings from an episode's parent series.
/// </summary>
public class DtddEpisodeProvider : ICustomMetadataProvider<Episode>, IHasOrder
{
    private readonly DtddMetadataService _metadata;
    private readonly IPluginConfigurationAccessor _configAccessor;
    private readonly ILogger<DtddEpisodeProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DtddEpisodeProvider"/> class.
    /// </summary>
    /// <param name="metadata">The DtDD metadata service.</param>
    /// <param name="configAccessor">The configuration accessor.</param>
    /// <param name="logger">The logger.</param>
    public DtddEpisodeProvider(
        DtddMetadataService metadata,
        IPluginConfigurationAccessor configAccessor,
        ILogger<DtddEpisodeProvider> logger)
    {
        _metadata = metadata;
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
        if (config is null || !config.EnableSeries)
        {
            return ItemUpdateType.None;
        }

        var seriesDtddId = item.Series?.GetProviderId(Constants.ProviderId);
        if (string.IsNullOrEmpty(seriesDtddId))
        {
            _logger.LogDebug("No parent series DTDD ID for episode {Name}", item.Name);
            return ItemUpdateType.None;
        }

        var data = await _metadata.ResolveAsync(
            seriesDtddId,
            imdbId: null,
            name: null,
            year: null,
            new FetchReason(DtddItemKind.Episode, IsRefresh: true, IsUserInitiated: options?.ReplaceAllMetadata == true),
            cancellationToken).ConfigureAwait(false);

        if (data is null)
        {
            return ItemUpdateType.None;
        }

        item.SetProviderId(Constants.ProviderId, seriesDtddId);

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
                "Failed to apply DTDD data for episode {Name}: {Message}",
                item.Name,
                LogSanitizer.Sanitize(ex.ToString(), config.ApiKey));
        }

        return ItemUpdateType.MetadataDownload;
    }
}
