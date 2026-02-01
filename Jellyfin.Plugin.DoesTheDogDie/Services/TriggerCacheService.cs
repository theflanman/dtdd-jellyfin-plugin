using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.DoesTheDogDie.Api;
using Jellyfin.Plugin.DoesTheDogDie.Api.Models;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DoesTheDogDie.Services;

/// <summary>
/// Service for managing the trigger category/topic cache.
/// </summary>
public class TriggerCacheService
{
    private readonly DtddApiClient _apiClient;
    private readonly ILogger<TriggerCacheService> _logger;
    private readonly string _cachePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Seed IMDB IDs for diverse trigger coverage during cache refresh.
    /// These titles are known to have a wide variety of triggers.
    /// </summary>
    private static readonly string[] SeedImdbIds =
    {
        "tt2911666",  // John Wick - Animal, Violence
        "tt0944947",  // Game of Thrones - Violence, Sexual, Animal
        "tt6644200",  // A Quiet Place - Jump Scares, Family
        "tt1837492",  // 13 Reasons Why - Mental Health, Substance Abuse
        "tt7784604",  // Hereditary - Horror, Phobias
        "tt5834204",  // The Handmaid's Tale - Abuse, Sexual
        "tt1520211",  // The Walking Dead - Violence, Gore
        "tt0903747",  // Breaking Bad - Violence, Substance Abuse
        "tt4574334",  // Stranger Things - Jump Scares, Violence
        "tt0468569",  // The Dark Knight - Violence
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerCacheService"/> class.
    /// </summary>
    /// <param name="apiClient">The DTDD API client.</param>
    /// <param name="logger">The logger.</param>
    public TriggerCacheService(DtddApiClient apiClient, ILogger<TriggerCacheService> logger)
    {
        _apiClient = apiClient;
        _logger = logger;

        // Get the plugin data path from the plugin instance
        var pluginDataPath = Plugin.Instance?.DataFolderPath
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "jellyfin", "plugins", "DoesTheDogDie");

        Directory.CreateDirectory(pluginDataPath);
        _cachePath = Path.Combine(pluginDataPath, "trigger-cache.json");
    }

    /// <summary>
    /// Gets the cached triggers, optionally refreshing if the cache is empty or stale.
    /// </summary>
    /// <param name="forceRefresh">Force a refresh even if cache exists.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The trigger cache.</returns>
    public virtual async Task<TriggerCache> GetOrRefreshCacheAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (!forceRefresh)
        {
            var existingCache = LoadCache();
            if (existingCache != null && existingCache.Categories.Count > 0)
            {
                return existingCache;
            }
        }

        return await RefreshCacheAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Refreshes the trigger cache by fetching data from multiple seed titles.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The refreshed trigger cache.</returns>
    public virtual async Task<TriggerCache> RefreshCacheAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refreshing trigger cache from DTDD API...");

        var allCategories = new Dictionary<int, CachedCategory>();

        foreach (var imdbId in SeedImdbIds)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var details = await _apiClient.GetMediaDetailsByImdbIdAsync(imdbId, cancellationToken)
                    .ConfigureAwait(false);

                if (details?.TopicItemStats == null)
                {
                    continue;
                }

                ExtractCategoriesAndTopics(details.TopicItemStats, allCategories);

                // Small delay to avoid hammering the API
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Error fetching triggers for seed IMDB {ImdbId}", imdbId);
            }
        }

        var cache = new TriggerCache
        {
            LastRefreshed = DateTime.UtcNow,
            Categories = allCategories.Values
                .OrderBy(c => c.Name)
                .Select(c => new CachedCategory
                {
                    Id = c.Id,
                    Name = c.Name,
                    Topics = c.Topics.OrderBy(t => t.Name).ToList()
                })
                .ToList()
        };

        SaveCache(cache);

        _logger.LogInformation(
            "Trigger cache refreshed: {CategoryCount} categories, {TopicCount} topics",
            cache.Categories.Count,
            cache.Categories.Sum(c => c.Topics.Count));

        return cache;
    }

    /// <summary>
    /// Loads the trigger cache from disk.
    /// </summary>
    /// <returns>The cached triggers, or null if not found.</returns>
    public TriggerCache? LoadCache()
    {
        try
        {
            if (!File.Exists(_cachePath))
            {
                return null;
            }

            var json = File.ReadAllText(_cachePath);
            return JsonSerializer.Deserialize<TriggerCache>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading trigger cache from {Path}", _cachePath);
            return null;
        }
    }

    /// <summary>
    /// Saves the trigger cache to disk.
    /// </summary>
    /// <param name="cache">The cache to save.</param>
    public void SaveCache(TriggerCache cache)
    {
        try
        {
            var json = JsonSerializer.Serialize(cache, JsonOptions);
            File.WriteAllText(_cachePath, json);
            _logger.LogDebug("Trigger cache saved to {Path}", _cachePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error saving trigger cache to {Path}", _cachePath);
        }
    }

    internal static void ExtractCategoriesAndTopics(
        List<DtddTopicItemStat> topicItemStats,
        Dictionary<int, CachedCategory> categories)
    {
        foreach (var stat in topicItemStats)
        {
            var topic = stat.Topic;
            if (topic == null)
            {
                continue;
            }

            var categoryId = topic.TopicCategoryId ?? stat.TopicCategory?.Id ?? 0;
            var categoryName = topic.TopicCategory?.Name ?? stat.TopicCategory?.Name ?? "Other";

            if (categoryId == 0)
            {
                continue;
            }

            if (!categories.TryGetValue(categoryId, out var category))
            {
                category = new CachedCategory
                {
                    Id = categoryId,
                    Name = categoryName,
                    Topics = new List<CachedTopic>()
                };
                categories[categoryId] = category;
            }

            // Add topic if not already present
            if (!category.Topics.Any(t => t.Id == topic.Id))
            {
                category.Topics.Add(new CachedTopic
                {
                    Id = topic.Id,
                    Name = topic.Name,
                    DoesName = topic.DoesName
                });
            }
        }
    }
}
