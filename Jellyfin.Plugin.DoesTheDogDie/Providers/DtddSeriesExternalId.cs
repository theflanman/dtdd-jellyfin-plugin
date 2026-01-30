using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.DoesTheDogDie.Providers;

/// <summary>
/// External ID provider for DoesTheDogDie TV series IDs.
/// </summary>
public class DtddSeriesExternalId : IExternalId
{
    /// <summary>
    /// Gets the provider name displayed in the UI.
    /// </summary>
    public string ProviderName => Constants.ProviderName;

    /// <summary>
    /// Gets the key used to store this external ID in provider IDs.
    /// </summary>
    public string Key => Constants.ProviderId;

    /// <summary>
    /// Gets the media type this external ID applies to.
    /// </summary>
    public ExternalIdMediaType? Type => ExternalIdMediaType.Series;

    /// <summary>
    /// Gets the URL format string for linking to the external site.
    /// </summary>
    public string? UrlFormatString => "https://www.doesthedogdie.com/media/{0}";

    /// <summary>
    /// Determines if this external ID provider supports the given item type.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True if the item is a TV series, false otherwise.</returns>
    public bool Supports(IHasProviderIds item) => item is Series;
}
