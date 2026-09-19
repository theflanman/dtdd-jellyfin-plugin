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
    /// The API key the fixture configures. WireMock does not check it, but it keeps the "ddd_" shape a
    /// real key has so the plugin's log sanitizer has something to redact.
    /// </summary>
    public const string ApiKey = "ddd_e2e_test_key";

    /// <summary>
    /// Default plugin configuration used to reset between mutation tests.
    /// Mirrors PluginConfiguration's properties — a key that is not a real property is silently dropped
    /// by the configuration POST, so a stale entry here would be a test that quietly asserts nothing.
    /// </summary>
    public static IDictionary<string, object> DefaultPluginConfig() => new Dictionary<string, object>
    {
        ["EnableMovies"] = true,
        ["EnableSeries"] = true,
        ["AddWarningTags"] = true,
        ["TagPrefix"] = "CW:",
        ["SafeTagPrefix"] = "Safe:",
        ["ShowAllTriggers"] = true,
        ["EnabledCategoryIds"] = Array.Empty<int>(),
        ["EnabledTopicIds"] = Array.Empty<int>(),
        ["AddDescriptionWarnings"] = false,
        ["IncludeTopComment"] = false,
        ["MaxCommentLength"] = 200,
        ["ApiKey"] = ApiKey,
        ["DecisionThreshold"] = 0.5,
        ["IntervalMass"] = 0.95,
        ["ItemCacheDays"] = 30,
        ["TaxonomyCacheDays"] = 7,
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
