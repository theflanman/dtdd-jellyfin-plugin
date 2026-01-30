using System.Diagnostics.CodeAnalysis;

namespace Jellyfin.Plugin.DoesTheDogDie.Configuration;

/// <summary>
/// Interface for accessing plugin configuration.
/// Enables dependency injection and testability.
/// </summary>
public interface IPluginConfigurationAccessor
{
    /// <summary>
    /// Gets the current plugin configuration.
    /// </summary>
    /// <returns>The plugin configuration, or null if the plugin is not loaded.</returns>
    PluginConfiguration? GetConfiguration();
}

/// <summary>
/// Default implementation that reads from Plugin.Instance.
/// </summary>
[ExcludeFromCodeCoverage]
public class PluginConfigurationAccessor : IPluginConfigurationAccessor
{
    /// <inheritdoc />
    public PluginConfiguration? GetConfiguration()
    {
        return Plugin.Instance?.Configuration;
    }
}
