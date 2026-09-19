using System;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.DoesTheDogDie.Services;

/// <summary>
/// Removes DoesTheDogDie API keys from text before it reaches a log sink.
/// </summary>
public static partial class LogSanitizer
{
    /// <summary>
    /// The placeholder substituted for any detected API key.
    /// </summary>
    public const string Redacted = "***REDACTED***";

    /// <summary>
    /// Redacts the configured API key and any DtDD-shaped key literal from a message.
    /// </summary>
    /// <param name="message">The message to sanitize. Null is treated as empty.</param>
    /// <param name="apiKey">The currently configured API key, if known.</param>
    /// <returns>The message with any API key replaced by <see cref="Redacted"/>.</returns>
    public static string Sanitize(string? message, string? apiKey)
    {
        if (string.IsNullOrEmpty(message))
        {
            return string.Empty;
        }

        var result = message;

        if (!string.IsNullOrEmpty(apiKey))
        {
            result = result.Replace(apiKey, Redacted, StringComparison.Ordinal);
        }

        return KeyPattern().Replace(result, Redacted);
    }

    [GeneratedRegex(@"ddd_[A-Za-z0-9]+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex KeyPattern();
}
