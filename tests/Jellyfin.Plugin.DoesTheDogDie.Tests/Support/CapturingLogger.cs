using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Support;

/// <summary>
/// An <see cref="ILogger{TCategoryName}"/> test double that records every log entry so tests can
/// assert on log call counts and levels (e.g. one-shot warning suppression), rather than swallowing
/// them like <c>NullLogger</c>.
/// </summary>
/// <typeparam name="T">The logger's category type.</typeparam>
public sealed class CapturingLogger<T> : ILogger<T>
{
    /// <summary>Gets the entries recorded so far, in order.</summary>
    public List<(LogLevel Level, string Message)> Entries { get; } = new();

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        Entries.Add((logLevel, formatter(state, exception)));
    }
}
