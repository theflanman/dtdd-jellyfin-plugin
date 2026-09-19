using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Configuration;

public class PluginConfigurationTests
{
    [Fact]
    public void Constructor_SetsDtddClientDefaults()
    {
        var config = new PluginConfiguration();

        Assert.Equal(string.Empty, config.ApiKey);
        Assert.Equal(0.5, config.DecisionThreshold);
        Assert.Equal(0.95, config.IntervalMass);
        Assert.Equal(30, config.ItemCacheDays);
        Assert.Equal(7, config.TaxonomyCacheDays);
    }
}
