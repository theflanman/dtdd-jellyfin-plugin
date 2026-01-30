using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.DoesTheDogDie.Api.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DoesTheDogDie.Api;

/// <summary>
/// HTTP client for communicating with the DoesTheDogDie API.
/// </summary>
public class DtddApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DtddApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="DtddApiClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger.</param>
    public DtddApiClient(IHttpClientFactory httpClientFactory, ILogger<DtddApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Searches for media by IMDB ID.
    /// </summary>
    /// <param name="imdbId">The IMDB ID (e.g., "tt2911666").</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The search response, or null if not found or on error.</returns>
    public virtual async Task<DtddSearchResponse?> SearchByImdbIdAsync(string imdbId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imdbId))
        {
            return null;
        }

        var url = $"{Constants.ApiBaseUrl}/dddsearch?imdb={Uri.EscapeDataString(imdbId)}";
        return await SendSearchRequestAsync(url, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Searches for media by title.
    /// </summary>
    /// <param name="title">The media title.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The search response, or null if not found or on error.</returns>
    public virtual async Task<DtddSearchResponse?> SearchByTitleAsync(string title, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var url = $"{Constants.ApiBaseUrl}/dddsearch?q={Uri.EscapeDataString(title)}";
        return await SendSearchRequestAsync(url, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets detailed media information including trigger data.
    /// </summary>
    /// <param name="dtddId">The DoesTheDogDie media ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The media details, or null if not found or on error.</returns>
    public virtual async Task<DtddMediaDetails?> GetMediaDetailsAsync(int dtddId, CancellationToken cancellationToken = default)
    {
        var url = $"{Constants.ApiBaseUrl}/media/{dtddId}";

        try
        {
            var client = CreateHttpClient();
            var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("DTDD API returned {StatusCode} for media ID {DtddId}", response.StatusCode, dtddId);
                return null;
            }

            // Check content type - API returns HTML for invalid IDs
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType != null && contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("DTDD API returned HTML instead of JSON for media ID {DtddId}", dtddId);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<DtddMediaDetails>(content, JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error fetching DTDD media details for ID {DtddId}", dtddId);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "JSON parsing error for DTDD media ID {DtddId}", dtddId);
            return null;
        }
        catch (TaskCanceledException)
        {
            _logger.LogDebug("Request cancelled for DTDD media ID {DtddId}", dtddId);
            return null;
        }
    }

    /// <summary>
    /// Searches for media and returns the first matching result's details.
    /// </summary>
    /// <param name="imdbId">The IMDB ID to search for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The media details if found, otherwise null.</returns>
    public virtual async Task<DtddMediaDetails?> GetMediaDetailsByImdbIdAsync(string imdbId, CancellationToken cancellationToken = default)
    {
        var searchResult = await SearchByImdbIdAsync(imdbId, cancellationToken).ConfigureAwait(false);

        if (searchResult?.Items == null || searchResult.Items.Count == 0)
        {
            _logger.LogDebug("No DTDD results found for IMDB ID {ImdbId}", imdbId);
            return null;
        }

        var firstMatch = searchResult.Items[0];
        return await GetMediaDetailsAsync(firstMatch.Id, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DtddSearchResponse?> SendSearchRequestAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var client = CreateHttpClient();
            var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("DTDD API search returned {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<DtddSearchResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error during DTDD search");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "JSON parsing error during DTDD search");
            return null;
        }
        catch (TaskCanceledException)
        {
            _logger.LogDebug("DTDD search request cancelled");
            return null;
        }
    }

    private HttpClient CreateHttpClient()
    {
        var client = _httpClientFactory.CreateClient(Constants.HttpClientName);
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("X-API-KEY", Constants.ApiKey);
        return client;
    }
}
