using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoesTheDogDie;
using DoesTheDogDie.Api;
using DoesTheDogDie.Statistics;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DoesTheDogDie.Services;

/// <summary>
/// The plugin's single entry point to DoesTheDogDie data: resolves a Jellyfin item to DtDD triggers
/// and applies them back onto the item.
/// </summary>
public class DtddMetadataService
{
    private const string NoKeyWarningKey = "no-key";
    private const string MonthlyLimitWarningKey = "monthly-limit";
    private const string AuthWarningKey = "auth";
    private const string UpgradeWarningKey = "upgrade";

    private readonly Func<IDtddClient> _clientFactory;
    private readonly IPluginConfigurationAccessor _configAccessor;
    private readonly ILogger<DtddMetadataService> _logger;
    private readonly OverviewFormatter _overviewFormatter;
    private readonly HashSet<string> _warnedKeys = new(StringComparer.Ordinal);
    private int _lastConfigurationVersion = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="DtddMetadataService"/> class.
    /// </summary>
    /// <param name="clientFactory">Supplies the current DtDD client.</param>
    /// <param name="configAccessor">The plugin configuration accessor.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="overviewFormatter">The overview formatter.</param>
    public DtddMetadataService(
        Func<IDtddClient> clientFactory,
        IPluginConfigurationAccessor configAccessor,
        ILogger<DtddMetadataService> logger,
        OverviewFormatter overviewFormatter)
    {
        _clientFactory = clientFactory;
        _configAccessor = configAccessor;
        _logger = logger;
        _overviewFormatter = overviewFormatter;
    }

    /// <summary>
    /// Resolves a Jellyfin item to its DtDD data, trying the stored DtDD id, then IMDb, then exact
    /// name and year. Never issues a free-text search.
    /// </summary>
    /// <param name="storedDtddId">The DtDD id already stored on the item, if any.</param>
    /// <param name="imdbId">The item's IMDb id, if any.</param>
    /// <param name="name">The item's name, if any.</param>
    /// <param name="year">The item's production year, if any.</param>
    /// <param name="reason">The priority hint for this fetch.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resolved data, or null if the item could not be resolved.</returns>
    public virtual async Task<DtddItemData?> ResolveAsync(
        string? storedDtddId,
        string? imdbId,
        string? name,
        int? year,
        FetchReason reason,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ResolveCoreAsync(storedDtddId, imdbId, name, year, reason, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DtddNotFoundException)
        {
            _logger.LogDebug("No DoesTheDogDie entry for this item");
            return null;
        }
        catch (DtddMinuteRateLimitException)
        {
            _logger.LogDebug("DoesTheDogDie per-minute rate limit hit; skipping this item");
            return null;
        }
        catch (DtddMonthlyRateLimitException ex)
        {
            WarnOnce(
                MonthlyLimitWarningKey,
                "DoesTheDogDie monthly request budget is exhausted. Cached items still resolve; new lookups resume after the budget resets. "
                    + LogSanitizer.Sanitize(ex.Message, CurrentApiKey()));
            return null;
        }
        catch (DtddAuthenticationException ex)
        {
            WarnOnce(
                AuthWarningKey,
                "DoesTheDogDie rejected the configured API key. Check it on the plugin's configuration page. "
                    + LogSanitizer.Sanitize(ex.Message, CurrentApiKey()));
            return null;
        }
        catch (DtddUpgradeRequiredException ex)
        {
            WarnOnce(
                UpgradeWarningKey,
                "DoesTheDogDie reports this request needs a paid plan. "
                    + LogSanitizer.Sanitize(ex.Message, CurrentApiKey()));
            return null;
        }
        catch (DtddQueueFullException)
        {
            _logger.LogDebug("DoesTheDogDie request queue is full; skipping this item. The daily refresh task will retry it");
            return null;
        }
        catch (ObjectDisposedException)
        {
            _logger.LogDebug("DoesTheDogDie client was disposed mid-request; skipping this item");
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError("Unexpected DoesTheDogDie failure: {Message}", LogSanitizer.Sanitize(ex.ToString(), CurrentApiKey()));
            return null;
        }
    }

    /// <summary>
    /// Applies resolved DtDD data to a Jellyfin item as tags and, when enabled, an overview section.
    /// </summary>
    /// <param name="item">The Jellyfin item to update.</param>
    /// <param name="data">The resolved DtDD data.</param>
    /// <param name="config">The plugin configuration.</param>
    public virtual void Apply(BaseItem item, DtddItemData data, PluginConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(config);

        if (config.AddWarningTags)
        {
            ApplyTags(item, data, config);
        }

        ApplyOverview(item, data, config);
    }

    /// <summary>
    /// Clears the one-shot warning suppression. Called when configuration changes.
    /// </summary>
    public void ResetWarnings()
    {
        lock (_warnedKeys)
        {
            _warnedKeys.Clear();
        }
    }

    private string? CurrentApiKey() => _configAccessor.GetConfiguration()?.ApiKey;

    private void ResetWarningsIfConfigurationChanged()
    {
        var version = Plugin.ConfigurationVersion;
        if (Interlocked.Exchange(ref _lastConfigurationVersion, version) != version)
        {
            ResetWarnings();
        }
    }

    /// <inheritdoc cref="ResolveAsync"/>
    private async Task<DtddItemData?> ResolveCoreAsync(
        string? storedDtddId,
        string? imdbId,
        string? name,
        int? year,
        FetchReason reason,
        CancellationToken cancellationToken)
    {
        ResetWarningsIfConfigurationChanged();

        var config = _configAccessor.GetConfiguration();
        if (config is null || string.IsNullOrWhiteSpace(config.ApiKey))
        {
            WarnOnce(NoKeyWarningKey, "No DoesTheDogDie API key is configured; skipping lookups. Set one on the plugin's configuration page");
            return null;
        }

        var client = _clientFactory();

        var itemId = await ResolveItemIdAsync(client, storedDtddId, imdbId, name, year, cancellationToken)
            .ConfigureAwait(false);
        if (itemId is null)
        {
            return null;
        }

        var detail = await client.GetItemAsync(itemId.Value, cancellationToken).ConfigureAwait(false);
        var topics = await client.GetTopicsAsync(cancellationToken).ConfigureAwait(false);
        var topicsById = topics.Value.ToDictionary(t => t.Id);

        var options = new ConfidenceOptions
        {
            DecisionThreshold = config.DecisionThreshold,
            IntervalMass = config.IntervalMass,
        };

        var triggers = new List<TriggerInfo>();
        foreach (var stat in detail.Value.TopicItemStats ?? Array.Empty<TopicItemStat>())
        {
            if (!topicsById.TryGetValue(stat.TopicId, out var topic))
            {
                _logger.LogDebug("Dropping stat for unknown topic {TopicId}", stat.TopicId);
                continue;
            }

            triggers.Add(new TriggerInfo(topic, stat.YesSum, stat.NoSum, stat.ToConfidence(options)));
        }

        var comments = config.IncludeTopComment
            ? await FetchTopCommentsAsync(client, detail.Value.Id, config, cancellationToken).ConfigureAwait(false)
            : new Dictionary<int, string>();

        return new DtddItemData(detail.Value.Id, triggers, detail.Source, detail.FetchedAt)
        {
            Comments = comments,
        };
    }

    private async Task<IReadOnlyDictionary<int, string>> FetchTopCommentsAsync(
        IDtddClient client,
        int itemId,
        PluginConfiguration config,
        CancellationToken cancellationToken)
    {
        try
        {
            var ratings = await client.GetRatingsAsync(itemId, topicId: null, cancellationToken)
                .ConfigureAwait(false);

            return ratings.Value
                .Where(r => !string.IsNullOrWhiteSpace(r.TriggerDescription))
                .GroupBy(r => r.TopicId)
                .ToDictionary(
                    g => g.Key,
                    g => Truncate(g.OrderByDescending(r => r.VoteSum).First().TriggerDescription!, config.MaxCommentLength));
        }
        catch (DtddApiException ex)
        {
            _logger.LogDebug(
                "Could not fetch DoesTheDogDie comments for item {ItemId}: {Message}",
                itemId,
                LogSanitizer.Sanitize(ex.Message, config.ApiKey));
            return new Dictionary<int, string>();
        }
    }

    private static string Truncate(string text, int maxLength)
    {
        if (maxLength <= 0 || text.Length <= maxLength)
        {
            return text;
        }

        return string.Concat(text.AsSpan(0, maxLength).TrimEnd(), "...");
    }

    private async Task<int?> ResolveItemIdAsync(
        IDtddClient client,
        string? storedDtddId,
        string? imdbId,
        string? name,
        int? year,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(storedDtddId)
            && int.TryParse(storedDtddId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var stored))
        {
            return stored;
        }

        if (!string.IsNullOrEmpty(imdbId))
        {
            var byImdb = await client.SearchItemsAsync(ItemSearch.ByImdbId(imdbId), cancellationToken)
                .ConfigureAwait(false);
            if (byImdb.Value.Count > 0)
            {
                return byImdb.Value[0].Id;
            }
        }

        if (!string.IsNullOrEmpty(name) && year is not null)
        {
            var byName = await client.SearchItemsAsync(ItemSearch.ByName(name, year), cancellationToken)
                .ConfigureAwait(false);
            if (byName.Value.Count > 0)
            {
                return byName.Value[0].Id;
            }
        }

        return null;
    }

    private static void ApplyTags(BaseItem item, DtddItemData data, PluginConfiguration config)
    {
        if (item.LockedFields.Contains(MetadataField.Tags))
        {
            return;
        }

        var prefixes = new[] { config.TagPrefix, config.SafeTagPrefix }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();

        var tags = item.Tags
            .Where(tag => !prefixes.Any(p => tag.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var trigger in TriggerFilter.Filter(data.Triggers, config))
        {
            var tagName = TriggerTagFormatter.FormatTagName(trigger, config);
            if (tagName is not null && !tags.Contains(tagName, StringComparer.OrdinalIgnoreCase))
            {
                tags.Add(tagName);
            }
        }

        item.Tags = tags.ToArray();
    }

    private void ApplyOverview(BaseItem item, DtddItemData data, PluginConfiguration config)
    {
        if (item.LockedFields.Contains(MetadataField.Overview))
        {
            _logger.LogDebug("Overview is locked for {Name}, skipping description injection", item.Name);
            return;
        }

        if (!config.AddDescriptionWarnings)
        {
            if (_overviewFormatter.HasDtddSection(item.Overview))
            {
                item.Overview = _overviewFormatter.RemoveDtddSection(item.Overview!);
            }

            return;
        }

        var summary = _overviewFormatter.FormatTriggerSummary(data.Triggers, config, data.Comments);
        item.Overview = string.IsNullOrEmpty(summary)
            ? _overviewFormatter.RemoveDtddSection(item.Overview ?? string.Empty)
            : _overviewFormatter.AppendToOverview(item.Overview, summary);
    }

    private void WarnOnce(string key, string message)
    {
        lock (_warnedKeys)
        {
            if (!_warnedKeys.Add(key))
            {
                return;
            }
        }

        _logger.LogWarning("{Message}", message);
    }
}
