using System.Collections.Generic;
using Jellyfin.Plugin.DoesTheDogDie.Api.Models;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests;

public class TriggerFilterTests
{
    [Fact]
    public void ShouldIncludeTrigger_ShowAllTriggersTrue_ReturnsTrue()
    {
        // Arrange
        var config = new PluginConfiguration { ShowAllTriggers = true };
        var trigger = CreateTrigger(categoryId: 2, topicId: 153);

        // Act
        var result = TriggerFilter.ShouldIncludeTrigger(trigger, config);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTrigger_ShowAllTriggersFalse_NoCategoriesSelected_ReturnsTrue()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            ShowAllTriggers = false,
            EnabledCategoryIds = new List<int>()
        };
        var trigger = CreateTrigger(categoryId: 2, topicId: 153);

        // Act
        var result = TriggerFilter.ShouldIncludeTrigger(trigger, config);

        // Assert
        Assert.True(result); // Should include all when no categories selected (with warning)
    }

    [Fact]
    public void ShouldIncludeTrigger_CategoryEnabled_ReturnsTrue()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            ShowAllTriggers = false,
            EnabledCategoryIds = new List<int> { 2 }, // Animal category
            EnabledTopicIds = new List<int>()
        };
        var trigger = CreateTrigger(categoryId: 2, topicId: 153);

        // Act
        var result = TriggerFilter.ShouldIncludeTrigger(trigger, config);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTrigger_CategoryDisabled_ReturnsFalse()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            ShowAllTriggers = false,
            EnabledCategoryIds = new List<int> { 3 }, // Violence category, not Animal
            EnabledTopicIds = new List<int>()
        };
        var trigger = CreateTrigger(categoryId: 2, topicId: 153); // Animal category

        // Act
        var result = TriggerFilter.ShouldIncludeTrigger(trigger, config);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldIncludeTrigger_TopicEnabled_ReturnsTrue()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            ShowAllTriggers = false,
            EnabledCategoryIds = new List<int> { 2 },
            EnabledTopicIds = new List<int> { 153 }
        };
        var trigger = CreateTrigger(categoryId: 2, topicId: 153);

        // Act
        var result = TriggerFilter.ShouldIncludeTrigger(trigger, config);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTrigger_TopicDisabled_ReturnsFalse()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            ShowAllTriggers = false,
            EnabledCategoryIds = new List<int> { 2 },
            EnabledTopicIds = new List<int> { 154 } // Different topic
        };
        var trigger = CreateTrigger(categoryId: 2, topicId: 153);

        // Act
        var result = TriggerFilter.ShouldIncludeTrigger(trigger, config);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldIncludeTrigger_NullTopic_ReturnsFalse()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            ShowAllTriggers = false,
            EnabledCategoryIds = new List<int> { 2 }
        };
        var trigger = new DtddTopicItemStat { Topic = null };

        // Act
        var result = TriggerFilter.ShouldIncludeTrigger(trigger, config);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldIncludeTrigger_NullCategoryId_ReturnsFalse()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            ShowAllTriggers = false,
            EnabledCategoryIds = new List<int> { 2 }
        };
        var trigger = new DtddTopicItemStat
        {
            Topic = new DtddTopic
            {
                Id = 153,
                Name = "test",
                TopicCategoryId = null
            },
            TopicCategory = null
        };

        // Act
        var result = TriggerFilter.ShouldIncludeTrigger(trigger, config);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldIncludeTrigger_CategoryFromTopicCategory_ReturnsTrue()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            ShowAllTriggers = false,
            EnabledCategoryIds = new List<int> { 2 }
        };
        var trigger = new DtddTopicItemStat
        {
            Topic = new DtddTopic
            {
                Id = 153,
                Name = "test",
                TopicCategoryId = null // No category on topic
            },
            TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" } // Category from stat
        };

        // Act
        var result = TriggerFilter.ShouldIncludeTrigger(trigger, config);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void FilterTriggers_FiltersCorrectly()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            ShowAllTriggers = false,
            EnabledCategoryIds = new List<int> { 2 }, // Only Animal
            EnabledTopicIds = new List<int>()
        };

        var triggers = new List<DtddTopicItemStat>
        {
            CreateTrigger(categoryId: 2, topicId: 153, name: "a dog dies"),
            CreateTrigger(categoryId: 3, topicId: 101, name: "blood/gore"),
            CreateTrigger(categoryId: 2, topicId: 154, name: "a cat dies")
        };

        // Act
        var filtered = TriggerFilter.FilterTriggers(triggers, config);

        // Assert
        var result = new List<DtddTopicItemStat>(filtered);
        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.Equal(2, t.Topic?.TopicCategoryId));
    }

    [Fact]
    public void FilterTriggers_ShowAllTriggers_ReturnsAll()
    {
        // Arrange
        var config = new PluginConfiguration { ShowAllTriggers = true };
        var triggers = new List<DtddTopicItemStat>
        {
            CreateTrigger(categoryId: 2, topicId: 153),
            CreateTrigger(categoryId: 3, topicId: 101),
            CreateTrigger(categoryId: 4, topicId: 201)
        };

        // Act
        var filtered = TriggerFilter.FilterTriggers(triggers, config);

        // Assert
        var result = new List<DtddTopicItemStat>(filtered);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void FilterTriggers_WithTopicFilter_FiltersTopics()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            ShowAllTriggers = false,
            EnabledCategoryIds = new List<int> { 2 },
            EnabledTopicIds = new List<int> { 153 } // Only dog dies
        };

        var triggers = new List<DtddTopicItemStat>
        {
            CreateTrigger(categoryId: 2, topicId: 153, name: "a dog dies"),
            CreateTrigger(categoryId: 2, topicId: 154, name: "a cat dies"),
            CreateTrigger(categoryId: 2, topicId: 155, name: "a horse dies")
        };

        // Act
        var filtered = TriggerFilter.FilterTriggers(triggers, config);

        // Assert
        var result = new List<DtddTopicItemStat>(filtered);
        Assert.Single(result);
        Assert.Equal("a dog dies", result[0].Topic?.Name);
    }

    private static DtddTopicItemStat CreateTrigger(int categoryId, int topicId, string name = "test trigger")
    {
        return new DtddTopicItemStat
        {
            TopicItemId = topicId * 100,
            YesSum = 100,
            NoSum = 10,
            TopicId = topicId,
            Topic = new DtddTopic
            {
                Id = topicId,
                Name = name,
                TopicCategoryId = categoryId,
                TopicCategory = new DtddTopicCategory
                {
                    Id = categoryId,
                    Name = $"Category {categoryId}"
                }
            },
            TopicCategory = new DtddTopicCategory
            {
                Id = categoryId,
                Name = $"Category {categoryId}"
            }
        };
    }
}
