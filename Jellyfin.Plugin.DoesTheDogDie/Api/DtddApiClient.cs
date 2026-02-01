using System;
using System.Linq;
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

    /// <summary>
    /// Searches for media by title and returns the best matching result's details.
    /// </summary>
    /// <param name="title">The media title to search for.</param>
    /// <param name="year">The release year for disambiguation (optional).</param>
    /// <param name="itemTypeId">The DTDD item type ID to filter by (15=Movie, 16=TV Show).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The media details if found, otherwise null.</returns>
    public virtual async Task<DtddMediaDetails?> GetMediaDetailsByTitleAsync(
        string title,
        int? year,
        int itemTypeId,
        CancellationToken cancellationToken = default)
    {
        var searchResult = await SearchByTitleAsync(title, cancellationToken).ConfigureAwait(false);

        if (searchResult?.Items == null || searchResult.Items.Count == 0)
        {
            _logger.LogDebug("No DTDD results found for title {Title}", title);
            return null;
        }

        var bestMatch = FindBestMatch(searchResult.Items, title, year, itemTypeId);

        if (bestMatch == null)
        {
            _logger.LogDebug("No matching DTDD result for title {Title} (year: {Year}, type: {TypeId})", title, year, itemTypeId);
            return null;
        }

        _logger.LogDebug("Found DTDD match for title {Title}: {MatchName} ({MatchYear})", title, bestMatch.Name, bestMatch.ReleaseYear);
        return await GetMediaDetailsAsync(bestMatch.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Finds the best matching item from search results based on title, year, and type.
    /// </summary>
    /// <param name="items">The search result items.</param>
    /// <param name="title">The title to match.</param>
    /// <param name="year">The release year (optional).</param>
    /// <param name="itemTypeId">The item type ID to filter by.</param>
    /// <returns>The best matching item, or null if no suitable match found.</returns>
    internal static DtddMediaItem? FindBestMatch(
        System.Collections.Generic.List<DtddMediaItem> items,
        string title,
        int? year,
        int itemTypeId)
    {
        // Filter by item type first
        var typeMatches = items.Where(i => i.ItemTypeId == itemTypeId).ToList();

        if (typeMatches.Count == 0)
        {
            return null;
        }

        // Normalize search title for comparison
        var normalizedTitle = NormalizeTitle(title);

        // Score each match
        var scored = typeMatches.Select(item =>
        {
            int score = 0;

            // Exact title match (case-insensitive)
            if (string.Equals(item.Name, title, StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }
            else if (string.Equals(NormalizeTitle(item.Name), normalizedTitle, StringComparison.OrdinalIgnoreCase))
            {
                score += 80;
            }
            else if (item.CleanName != null && string.Equals(item.CleanName, normalizedTitle, StringComparison.OrdinalIgnoreCase))
            {
                score += 70;
            }

            // Year match
            if (year.HasValue && !string.IsNullOrEmpty(item.ReleaseYear))
            {
                if (int.TryParse(item.ReleaseYear, out int itemYear))
                {
                    if (itemYear == year.Value)
                    {
                        score += 50;
                    }
                    else if (Math.Abs(itemYear - year.Value) == 1)
                    {
                        // Allow 1 year tolerance
                        score += 25;
                    }
                }
            }

            // Prefer verified content
            if (item.StaffVerified)
            {
                score += 10;
            }
            else if (item.Verified)
            {
                score += 5;
            }

            // Prefer items with more ratings (more data)
            if (item.NumRatings > 100)
            {
                score += 5;
            }

            return new { Item = item, Score = score };
        })
        .OrderByDescending(x => x.Score)
        .ToList();

        // Only return if we have a reasonable match (at least partial title match)
        var best = scored.FirstOrDefault();
        return best != null && best.Score >= 70 ? best.Item : null;
    }

    /// <summary>
    /// Normalizes a title for comparison by removing articles and converting to lowercase.
    /// </summary>
    /// <param name="title">The title to normalize.</param>
    /// <returns>The normalized title.</returns>
    internal static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var normalized = title.Trim().ToLowerInvariant();

        // Remove common articles from the beginning
        string[] articles = { "the ", "a ", "an " };
        foreach (var article in articles)
        {
            if (normalized.StartsWith(article, StringComparison.Ordinal))
            {
                normalized = normalized[article.Length..];
                break;
            }
        }

        return normalized;
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
