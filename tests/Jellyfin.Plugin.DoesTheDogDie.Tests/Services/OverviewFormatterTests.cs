using System.Collections.Generic;
using Jellyfin.Plugin.DoesTheDogDie.Api.Models;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Services;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Services;

public class OverviewFormatterTests
{
    private readonly OverviewFormatter _formatter;

    public OverviewFormatterTests()
    {
        _formatter = new OverviewFormatter();
    }

    [Fact]
    public void FormatTriggerSummary_WithPositiveTriggers_FormatsCorrectly()
    {
        // Arrange
        var details = CreateMediaDetailsWithTriggers();
        var config = new PluginConfiguration { MinVotesThreshold = 0 };

        // Act
        var result = _formatter.FormatTriggerSummary(details, config);

        // Assert
        Assert.Contains("**Content Warnings** (via DoesTheDogDie)", result);
        Assert.Contains("A dog dies (1336 yes / 118 no)", result);
    }

    [Fact]
    public void FormatTriggerSummary_WithNegativeTriggers_IncludesSafe()
    {
        // Arrange
        var details = CreateMediaDetailsWithNegativeTrigger();
        var config = new PluginConfiguration { MinVotesThreshold = 0 };

        // Act
        var result = _formatter.FormatTriggerSummary(details, config);

        // Assert
        Assert.Contains("Safe:", result);
        Assert.Contains("A cat dies (12 yes / 458 no)", result);
    }

    [Fact]
    public void FormatTriggerSummary_WithComment_IncludesComment()
    {
        // Arrange
        var details = CreateMediaDetailsWithComment();
        var config = new PluginConfiguration
        {
            MinVotesThreshold = 0,
            IncludeTopComment = true,
            MaxCommentLength = 200,
            HideSpoilerComments = false
        };

        // Act
        var result = _formatter.FormatTriggerSummary(details, config);

        // Assert
        Assert.Contains("The dog dies in the first 10 minutes", result);
        Assert.Contains("user123", result);
    }

    [Fact]
    public void FormatTriggerSummary_WithSpoilerComment_HidesWhenConfigured()
    {
        // Arrange
        var details = CreateMediaDetailsWithSpoilerComment();
        var config = new PluginConfiguration
        {
            MinVotesThreshold = 0,
            IncludeTopComment = true,
            MaxCommentLength = 200,
            HideSpoilerComments = true
        };

        // Act
        var result = _formatter.FormatTriggerSummary(details, config);

        // Assert
        Assert.DoesNotContain("spoiler comment", result);
    }

    [Fact]
    public void FormatTriggerSummary_NoTriggers_ReturnsEmpty()
    {
        // Arrange
        var details = new DtddMediaDetails
        {
            Item = new DtddMediaItem { Id = 1, Name = "Test" },
            TopicItemStats = new List<DtddTopicItemStat>()
        };
        var config = new PluginConfiguration { MinVotesThreshold = 0 };

        // Act
        var result = _formatter.FormatTriggerSummary(details, config);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void FormatTriggerSummary_TruncatesLongComment()
    {
        // Arrange
        var details = new DtddMediaDetails
        {
            Item = new DtddMediaItem { Id = 1, Name = "Test" },
            TopicItemStats = new List<DtddTopicItemStat>
            {
                new DtddTopicItemStat
                {
                    TopicItemId = 1,
                    YesSum = 100,
                    NoSum = 10,
                    Topic = new DtddTopic
                    {
                        Id = 1,
                        Name = "test trigger",
                        IsSpoiler = false
                    },
                    Comment = new string('a', 300)
                }
            }
        };
        var config = new PluginConfiguration
        {
            MinVotesThreshold = 0,
            IncludeTopComment = true,
            MaxCommentLength = 50,
            HideSpoilerComments = false
        };

        // Act
        var result = _formatter.FormatTriggerSummary(details, config);

        // Assert
        Assert.Contains("...", result);
        Assert.DoesNotContain(new string('a', 300), result);
    }

    [Fact]
    public void AppendToOverview_NullOverview_ReturnsJustDtdd()
    {
        // Arrange
        var dtddContent = "Test content";

        // Act
        var result = _formatter.AppendToOverview(null, dtddContent);

        // Assert
        Assert.Contains(OverviewFormatter.DtddStartMarker, result);
        Assert.Contains(OverviewFormatter.DtddEndMarker, result);
        Assert.Contains(dtddContent, result);
    }

    [Fact]
    public void AppendToOverview_EmptyOverview_ReturnsJustDtdd()
    {
        // Arrange
        var dtddContent = "Test content";

        // Act
        var result = _formatter.AppendToOverview(string.Empty, dtddContent);

        // Assert
        Assert.Contains(OverviewFormatter.DtddStartMarker, result);
        Assert.Contains(OverviewFormatter.DtddEndMarker, result);
        Assert.Contains(dtddContent, result);
    }

    [Fact]
    public void AppendToOverview_ExistingOverview_Appends()
    {
        // Arrange
        var existingOverview = "This is the original movie description.";
        var dtddContent = "Test content";

        // Act
        var result = _formatter.AppendToOverview(existingOverview, dtddContent);

        // Assert
        Assert.StartsWith("This is the original movie description.", result);
        Assert.Contains(OverviewFormatter.DtddStartMarker, result);
        Assert.Contains(OverviewFormatter.DtddEndMarker, result);
        Assert.Contains(dtddContent, result);
    }

    [Fact]
    public void AppendToOverview_ExistingDtddSection_Replaces()
    {
        // Arrange
        var existingOverview = $"Original description.\n\n{OverviewFormatter.DtddStartMarker}\nOld content\n{OverviewFormatter.DtddEndMarker}";
        var dtddContent = "New content";

        // Act
        var result = _formatter.AppendToOverview(existingOverview, dtddContent);

        // Assert
        Assert.Contains("Original description.", result);
        Assert.Contains("New content", result);
        Assert.DoesNotContain("Old content", result);
        // Should only have one set of markers
        Assert.Equal(1, CountOccurrences(result, OverviewFormatter.DtddStartMarker));
        Assert.Equal(1, CountOccurrences(result, OverviewFormatter.DtddEndMarker));
    }

    [Fact]
    public void AppendToOverview_EmptyDtddContent_ReturnsOriginal()
    {
        // Arrange
        var existingOverview = "Original description.";

        // Act
        var result = _formatter.AppendToOverview(existingOverview, string.Empty);

        // Assert
        Assert.Equal(existingOverview, result);
    }

    [Fact]
    public void RemoveDtddSection_WithSection_RemovesCleanly()
    {
        // Arrange
        var overview = $"Original description.\n\n{OverviewFormatter.DtddStartMarker}\nDTDD content\n{OverviewFormatter.DtddEndMarker}";

        // Act
        var result = _formatter.RemoveDtddSection(overview);

        // Assert
        Assert.Equal("Original description.", result);
        Assert.DoesNotContain(OverviewFormatter.DtddStartMarker, result);
        Assert.DoesNotContain(OverviewFormatter.DtddEndMarker, result);
        Assert.DoesNotContain("DTDD content", result);
    }

    [Fact]
    public void RemoveDtddSection_WithoutSection_ReturnsOriginal()
    {
        // Arrange
        var overview = "Original description without DTDD section.";

        // Act
        var result = _formatter.RemoveDtddSection(overview);

        // Assert
        Assert.Equal(overview, result);
    }

    [Fact]
    public void RemoveDtddSection_NullOverview_ReturnsEmpty()
    {
        // Act
        var result = _formatter.RemoveDtddSection(null!);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void HasDtddSection_WithSection_ReturnsTrue()
    {
        // Arrange
        var overview = $"Description\n\n{OverviewFormatter.DtddStartMarker}\nContent\n{OverviewFormatter.DtddEndMarker}";

        // Act
        var result = _formatter.HasDtddSection(overview);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasDtddSection_WithoutSection_ReturnsFalse()
    {
        // Arrange
        var overview = "Just a plain description.";

        // Act
        var result = _formatter.HasDtddSection(overview);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasDtddSection_NullOverview_ReturnsFalse()
    {
        // Act
        var result = _formatter.HasDtddSection(null);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasDtddSection_OnlyStartMarker_ReturnsFalse()
    {
        // Arrange
        var overview = $"Description with {OverviewFormatter.DtddStartMarker} only";

        // Act
        var result = _formatter.HasDtddSection(overview);

        // Assert
        Assert.False(result);
    }

    private static DtddMediaDetails CreateMediaDetailsWithTriggers()
    {
        return new DtddMediaDetails
        {
            Item = new DtddMediaItem { Id = 15713, Name = "John Wick" },
            TopicItemStats = new List<DtddTopicItemStat>
            {
                new DtddTopicItemStat
                {
                    TopicItemId = 1,
                    YesSum = 1336,
                    NoSum = 118,
                    TopicId = 153,
                    Topic = new DtddTopic
                    {
                        Id = 153,
                        Name = "a dog dies",
                        TopicCategoryId = 2
                    },
                    TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
                }
            }
        };
    }

    private static DtddMediaDetails CreateMediaDetailsWithNegativeTrigger()
    {
        return new DtddMediaDetails
        {
            Item = new DtddMediaItem { Id = 1, Name = "Test" },
            TopicItemStats = new List<DtddTopicItemStat>
            {
                new DtddTopicItemStat
                {
                    TopicItemId = 1,
                    YesSum = 12,
                    NoSum = 458,
                    TopicId = 154,
                    Topic = new DtddTopic
                    {
                        Id = 154,
                        Name = "a cat dies",
                        TopicCategoryId = 2
                    },
                    TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
                }
            }
        };
    }

    private static DtddMediaDetails CreateMediaDetailsWithComment()
    {
        return new DtddMediaDetails
        {
            Item = new DtddMediaItem { Id = 1, Name = "Test" },
            TopicItemStats = new List<DtddTopicItemStat>
            {
                new DtddTopicItemStat
                {
                    TopicItemId = 1,
                    YesSum = 100,
                    NoSum = 10,
                    TopicId = 153,
                    Topic = new DtddTopic
                    {
                        Id = 153,
                        Name = "a dog dies",
                        TopicCategoryId = 2,
                        IsSpoiler = false
                    },
                    Comment = "The dog dies in the first 10 minutes",
                    Username = "user123"
                }
            }
        };
    }

    private static DtddMediaDetails CreateMediaDetailsWithSpoilerComment()
    {
        return new DtddMediaDetails
        {
            Item = new DtddMediaItem { Id = 1, Name = "Test" },
            TopicItemStats = new List<DtddTopicItemStat>
            {
                new DtddTopicItemStat
                {
                    TopicItemId = 1,
                    YesSum = 100,
                    NoSum = 10,
                    TopicId = 153,
                    Topic = new DtddTopic
                    {
                        Id = 153,
                        Name = "a dog dies",
                        TopicCategoryId = 2,
                        IsSpoiler = true
                    },
                    Comment = "This is a spoiler comment",
                    Username = "user123"
                }
            }
        };
    }

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, System.StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }
}
