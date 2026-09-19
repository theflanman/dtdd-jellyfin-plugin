using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using DoesTheDogDie;
using DoesTheDogDie.Api;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        serviceCollection.AddSingleton<IPluginConfigurationAccessor, PluginConfigurationAccessor>();
        serviceCollection.AddSingleton<OverviewFormatter>();

        serviceCollection.AddSingleton(sp => new DtddClientProvider(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(Constants.HttpClientName),
            sp.GetRequiredService<IPluginConfigurationAccessor>(),
            Plugin.Instance?.DataFolderPath ?? Path.GetTempPath(),
            sp.GetRequiredService<ILogger<DtddClientProvider>>()));

        serviceCollection.AddSingleton<Func<IDtddClient>>(sp =>
            () => sp.GetRequiredService<DtddClientProvider>().GetClient());

        serviceCollection.AddSingleton<Func<RateLimitStatus?>>(sp =>
            () => sp.GetRequiredService<DtddClientProvider>().CurrentBudget);

        serviceCollection.AddSingleton<DtddMetadataService>();

        serviceCollection.AddHostedService<DtddLibraryScanService>();

        // Note: IScheduledTask (DtddRefreshTask) is auto-discovered by Jellyfin
        // via assembly scanning, no explicit registration needed
    }
}
