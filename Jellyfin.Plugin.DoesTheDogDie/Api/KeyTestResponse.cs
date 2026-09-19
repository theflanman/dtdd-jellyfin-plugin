using DoesTheDogDie.Api;

namespace Jellyfin.Plugin.DoesTheDogDie.Api;

/// <summary>
/// The result of testing the configured API key.
/// </summary>
/// <param name="Success">Whether the key was accepted.</param>
/// <param name="Message">A human-readable result message, sanitized of any key material.</param>
/// <param name="Budget">The observed rate-limit budget, when the call succeeded.</param>
public sealed record KeyTestResponse(bool Success, string Message, RateLimitStatus? Budget);
