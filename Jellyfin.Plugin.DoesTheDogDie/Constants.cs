namespace Jellyfin.Plugin.DoesTheDogDie;

/// <summary>
/// Plugin constants.
/// </summary>
public static class Constants
{
    /// <summary>
    /// The DoesTheDogDie API key.
    /// </summary>
    public const string ApiKey = "37410a353ce46488ec077d0c73ef1c2e";

    /// <summary>
    /// The DoesTheDogDie API base URL.
    /// </summary>
    public const string ApiBaseUrl = "https://www.doesthedogdie.com";

    /// <summary>
    /// The provider name displayed in Jellyfin UI.
    /// </summary>
    public const string ProviderName = "DoesTheDogDie";

    /// <summary>
    /// The provider ID key used in ProviderIds dictionary.
    /// </summary>
    public const string ProviderId = "Dtdd";

    /// <summary>
    /// The HTTP client name for dependency injection.
    /// </summary>
    public const string HttpClientName = "DoesTheDogDie";

    /// <summary>
    /// DTDD item type ID for movies.
    /// </summary>
    public const int DtddItemTypeMovie = 15;

    /// <summary>
    /// DTDD item type ID for TV shows.
    /// </summary>
    public const int DtddItemTypeSeries = 16;
}
