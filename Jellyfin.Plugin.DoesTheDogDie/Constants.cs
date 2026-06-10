using System;

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
    /// The default DoesTheDogDie API base URL.
    /// </summary>
    public const string DefaultApiBaseUrl = "https://www.doesthedogdie.com";

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

    /// <summary>
    /// Gets the DoesTheDogDie API base URL. Honors the DTDD_API_BASE_URL environment variable
    /// for E2E test redirection; falls back to <see cref="DefaultApiBaseUrl"/>.
    /// </summary>
    public static string ApiBaseUrl =>
        Environment.GetEnvironmentVariable("DTDD_API_BASE_URL") ?? DefaultApiBaseUrl;
}
