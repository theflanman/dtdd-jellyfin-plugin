using System.Collections.Generic;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.DoesTheDogDie.Providers;

/// <summary>
/// Provides external URLs to DoesTheDogDie.com for movies and series.
/// </summary>
public class DtddExternalUrlProvider : IExternalUrlProvider
{
    /// <inheritdoc />
    public string Name => Constants.ProviderName;

    /// <inheritdoc />
    public IEnumerable<string> GetExternalUrls(BaseItem item)
    {
        // Only provide URLs for movies and series
        if (item is not Movie && item is not Series)
        {
            yield break;
        }

        var dtddId = item.GetProviderId(Constants.ProviderId);
        if (!string.IsNullOrEmpty(dtddId))
        {
            yield return $"https://www.doesthedogdie.com/media/{dtddId}";
        }
    }
}
