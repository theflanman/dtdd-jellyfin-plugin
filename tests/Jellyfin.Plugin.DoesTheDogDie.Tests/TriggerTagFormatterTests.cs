using Jellyfin.Plugin.DoesTheDogDie.Api.Models;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests;

public class TriggerTagFormatterTests
{
    [Fact]
    public void PluginConfiguration_ShowConfidenceInTags_DefaultsToFalse()
    {
        // Arrange / Act
        var config = new PluginConfiguration();

        // Assert
        Assert.False(config.ShowConfidenceInTags);
    }

    [Fact]
    public void FormatTagName_ConfidenceDisabled_ReturnsPlainTag()
    {
        // Arrange
        var config = new PluginConfiguration { ShowConfidenceInTags = false };
        var trigger = CreateTrigger("a dog dies", yesSum: 100, noSum: 0);

        // Act
        var result = TriggerTagFormatter.FormatTagName("CW:", trigger, config);

        // Assert
        Assert.Equal("CW: a dog dies", result);
    }

    [Fact]
    public void FormatTagName_ConfidenceEnabled_AppendsPercentage()
    {
        // Arrange - 100 yes / 0 no => Wilson confidence 0.963 => 96.3% => 95%
        var config = new PluginConfiguration { ShowConfidenceInTags = true };
        var trigger = CreateTrigger("a dog dies", yesSum: 100, noSum: 0);

        // Act
        var result = TriggerTagFormatter.FormatTagName("CW:", trigger, config);

        // Assert
        Assert.Equal("CW: a dog dies (95%)", result);
    }

    [Theory]
    [InlineData(10, 0, "70%")] // 0.722460 => 72.2% => 70%
    [InlineData(1336, 118, "90%")] // 0.903680 => 90.4% => 90%
    [InlineData(1, 0, "20%")] // 0.206543 => 20.7% => 20%
    public void FormatTagName_ConfidenceEnabled_RoundsToNearestFivePercent(
        int yesSum,
        int noSum,
        string expectedPercent)
    {
        // Arrange
        var config = new PluginConfiguration { ShowConfidenceInTags = true };
        var trigger = CreateTrigger("a dog dies", yesSum, noSum);

        // Act
        var result = TriggerTagFormatter.FormatTagName("CW:", trigger, config);

        // Assert
        Assert.Equal($"CW: a dog dies ({expectedPercent})", result);
    }

    [Fact]
    public void FormatTagName_NegativeTrigger_UsesMajorityDirectionConfidence()
    {
        // Arrange - 0 yes / 100 no: safe confirmation with confidence 0.963 => 95%
        var config = new PluginConfiguration { ShowConfidenceInTags = true };
        var trigger = CreateTrigger("a cat dies", yesSum: 0, noSum: 100);

        // Act
        var result = TriggerTagFormatter.FormatTagName("Safe:", trigger, config);

        // Assert
        Assert.Equal("Safe: a cat dies (95%)", result);
    }

    [Fact]
    public void FormatTagName_NullTopic_ReturnsNull()
    {
        // Arrange
        var config = new PluginConfiguration();
        var trigger = new DtddTopicItemStat { Topic = null };

        // Act
        var result = TriggerTagFormatter.FormatTagName("CW:", trigger, config);

        // Assert
        Assert.Null(result);
    }

    private static DtddTopicItemStat CreateTrigger(string name, int yesSum, int noSum)
    {
        return new DtddTopicItemStat
        {
            YesSum = yesSum,
            NoSum = noSum,
            Topic = new DtddTopic { Id = 153, Name = name }
        };
    }
}
