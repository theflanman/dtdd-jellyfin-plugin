using System.Collections.Generic;
using System.Linq;
using DoesTheDogDie.Api;
using DoesTheDogDie.Statistics;
using Jellyfin.Plugin.DoesTheDogDie;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Services;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests;

public class TriggerFilterVerdictTests
{
    private static TriggerInfo Trigger(int topicId, int categoryId, int yes = 18, int no = 2) =>
        new(
            new Topic { Id = topicId, Name = "a dog dies", TopicCategoryId = categoryId },
            yes,
            no,
            TriggerConfidence.Compute(yes, no));

    [Fact]
    public void ShouldInclude_ReturnsTrue_WhenShowAllTriggersIsSet()
    {
        var config = new PluginConfiguration { ShowAllTriggers = true, EnabledCategoryIds = new List<int> { 99 } };

        Assert.True(TriggerFilter.ShouldInclude(Trigger(201, 3), config));
    }

    [Fact]
    public void ShouldInclude_ReturnsTrue_WhenNoCategoriesSelected()
    {
        var config = new PluginConfiguration { ShowAllTriggers = false, EnabledCategoryIds = new List<int>() };

        Assert.True(TriggerFilter.ShouldInclude(Trigger(201, 3), config));
    }

    [Fact]
    public void ShouldInclude_ReturnsFalse_WhenCategoryNotEnabled()
    {
        var config = new PluginConfiguration { ShowAllTriggers = false, EnabledCategoryIds = new List<int> { 5 } };

        Assert.False(TriggerFilter.ShouldInclude(Trigger(201, 3), config));
    }

    [Fact]
    public void ShouldInclude_ReturnsTrue_WhenCategoryEnabledAndNoTopicsSelected()
    {
        var config = new PluginConfiguration { ShowAllTriggers = false, EnabledCategoryIds = new List<int> { 3 } };

        Assert.True(TriggerFilter.ShouldInclude(Trigger(201, 3), config));
    }

    [Fact]
    public void ShouldInclude_ReturnsFalse_WhenTopicNotExplicitlyEnabled()
    {
        var config = new PluginConfiguration
        {
            ShowAllTriggers = false,
            EnabledCategoryIds = new List<int> { 3 },
            EnabledTopicIds = new List<int> { 999 },
        };

        Assert.False(TriggerFilter.ShouldInclude(Trigger(201, 3), config));
    }

    [Fact]
    public void ShouldInclude_DoesNotFilterByConfidence()
    {
        var config = new PluginConfiguration { ShowAllTriggers = true };

        // A 1/1 split is Uncertain, but filtering is category-based only.
        Assert.True(TriggerFilter.ShouldInclude(Trigger(201, 3, yes: 1, no: 1), config));
    }

    [Fact]
    public void Filter_KeepsOnlyIncludedTriggers()
    {
        var config = new PluginConfiguration { ShowAllTriggers = false, EnabledCategoryIds = new List<int> { 3 } };
        var triggers = new[] { Trigger(201, 3), Trigger(202, 5) };

        var kept = TriggerFilter.Filter(triggers, config).ToList();

        Assert.Single(kept);
        Assert.Equal(201, kept[0].Topic.Id);
    }
}
