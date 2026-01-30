using System;
using Jellyfin.Plugin.DoesTheDogDie.Api;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.DoesTheDogDie;

/// <summary>
/// Registers plugin services with the dependency injection container.
/// </summary>
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
    }
}
