using DoesTheDogDie.Api;
using DoesTheDogDie.Statistics;
using Jellyfin.Plugin.DoesTheDogDie;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Services;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests;

public class TriggerTagFormatterVerdictTests
{
    private static TriggerInfo Trigger(int yes, int no) =>
        new(
            new Topic { Id = 201, Name = "a dog dies", TopicCategoryId = 3 },
            yes,
            no,
            TriggerConfidence.Compute(yes, no));

    [Fact]
    public void FormatTagName_UsesWarningPrefix_ForLikelyPresent()
    {
        var config = new PluginConfiguration { TagPrefix = "CW:", ShowConfidenceInTags = false };

        Assert.Equal("CW: a dog dies", TriggerTagFormatter.FormatTagName(Trigger(40, 1), config));
    }

    [Fact]
    public void FormatTagName_UsesSafePrefix_ForLikelyAbsent()
    {
        var config = new PluginConfiguration { SafeTagPrefix = "Safe:", ShowConfidenceInTags = false };

        Assert.Equal("Safe: a dog dies", TriggerTagFormatter.FormatTagName(Trigger(1, 40), config));
    }

    [Fact]
    public void FormatTagName_ReturnsNull_ForUncertain()
    {
        var config = new PluginConfiguration { TagPrefix = "CW:", SafeTagPrefix = "Safe:" };

        Assert.Null(TriggerTagFormatter.FormatTagName(Trigger(1, 1), config));
    }

    [Fact]
    public void FormatTagName_AppendsConfidence_WhenEnabled()
    {
        var config = new PluginConfiguration { TagPrefix = "CW:", ShowConfidenceInTags = true };

        var tag = TriggerTagFormatter.FormatTagName(Trigger(40, 1), config);

        Assert.StartsWith("CW: a dog dies (", tag, System.StringComparison.Ordinal);
        Assert.EndsWith("%)", tag, System.StringComparison.Ordinal);
    }
}
