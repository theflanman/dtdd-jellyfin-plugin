using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.DoesTheDogDie.E2ETests;

internal static class TestHelpers
{
    /// <summary>
    /// Polls <paramref name="condition"/> until it returns true, or throws on timeout.
    /// Used to wait for asynchronous Jellyfin metadata refreshes to land.
    /// </summary>
    public static async Task WaitForAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout,
        TimeSpan? pollInterval = null,
        string? failureMessage = null,
        CancellationToken ct = default)
    {
        var poll = pollInterval ?? TimeSpan.FromMilliseconds(500);
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(poll, ct);
        }

        throw new TimeoutException(failureMessage ?? $"Condition not met within {timeout}");
    }

    /// <summary>
    /// Default plugin configuration used to reset between mutation tests.
    /// Mirrors PluginConfiguration's constructor defaults.
    /// </summary>
    public static IDictionary<string, object> DefaultPluginConfig() => new Dictionary<string, object>
    {
        ["EnableMovies"] = true,
        ["EnableSeries"] = true,
        ["EnableBooks"] = true,
        ["CacheDurationHours"] = 168,
        ["MinVotesThreshold"] = 3,
        ["AddWarningTags"] = true,
        ["TagPrefix"] = "CW:",
        ["SafeTagPrefix"] = "Safe:",
        ["RefreshIntervalHours"] = 24,
        ["ShowAllTriggers"] = true,
        ["EnabledCategoryIds"] = Array.Empty<int>(),
        ["EnabledTopicIds"] = Array.Empty<int>(),
        ["AddDescriptionWarnings"] = false,
        ["IncludeTopComment"] = false,
        ["MaxCommentLength"] = 200,
        ["HideSpoilerComments"] = true,
    };

    public static IDictionary<string, object> ConfigWith(params (string Key, object Value)[] overrides)
    {
        var cfg = DefaultPluginConfig();
        foreach (var (k, v) in overrides)
        {
            cfg[k] = v;
        }

        return cfg;
    }
}
