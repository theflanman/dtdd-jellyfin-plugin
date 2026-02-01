using System;
using System.Diagnostics.CodeAnalysis;
using Jellyfin.Plugin.DoesTheDogDie.Api;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.DoesTheDogDie;

/// <summary>
/// Registers plugin services with the dependency injection container.
/// </summary>
[ExcludeFromCodeCoverage]
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHttpClient(Constants.HttpClientName, client =>
        {
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("X-API-KEY", Constants.ApiKey);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        serviceCollection.AddSingleton<DtddApiClient>();
        serviceCollection.AddSingleton<IPluginConfigurationAccessor, PluginConfigurationAccessor>();
        serviceCollection.AddSingleton<TriggerCacheService>();

        // Background service for automatic DTDD lookup on library changes
        serviceCollection.AddHostedService<DtddLibraryScanService>();

        // Note: IScheduledTask (DtddRefreshTask) is auto-discovered by Jellyfin
        // via assembly scanning, no explicit registration needed
    }
}
