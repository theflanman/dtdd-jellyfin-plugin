using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.DoesTheDogDie.Api;
using Jellyfin.Plugin.DoesTheDogDie.Api.Models;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Services;

/// <summary>
/// Tests for the TriggerCacheService.
/// Note: Full integration tests require file system access and Plugin.Instance.
/// These tests focus on the mockable aspects.
/// </summary>
public class TriggerCacheServiceTests
{
    private readonly Mock<DtddApiClient> _apiClientMock;
    private readonly Mock<ILogger<TriggerCacheService>> _loggerMock;

    public TriggerCacheServiceTests()
    {
        _apiClientMock = new Mock<DtddApiClient>(
            Mock.Of<System.Net.Http.IHttpClientFactory>(),
            Mock.Of<ILogger<DtddApiClient>>());
        _loggerMock = new Mock<ILogger<TriggerCacheService>>();
    }

    [Fact]
    public void TriggerCache_RoundTrip_SerializesCorrectly()
    {
        // Arrange
        var cache = new TriggerCache
        {
            LastRefreshed = new DateTime(2024, 1, 30, 12, 0, 0, DateTimeKind.Utc),
            Categories = new List<CachedCategory>
            {
                new CachedCategory
                {
                    Id = 2,
                    Name = "Animal",
                    Topics = new List<CachedTopic>
                    {
                        new CachedTopic { Id = 153, Name = "a dog dies", DoesName = "Does the dog die" }
                    }
                }
            }
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(cache);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<TriggerCache>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(cache.LastRefreshed, deserialized.LastRefreshed);
        Assert.Single(deserialized.Categories);
        Assert.Equal("Animal", deserialized.Categories[0].Name);
        Assert.Single(deserialized.Categories[0].Topics);
        Assert.Equal("a dog dies", deserialized.Categories[0].Topics[0].Name);
    }

    [Fact]
    public void CachedCategory_Properties_SetCorrectly()
    {
        // Arrange & Act
        var category = new CachedCategory
        {
            Id = 2,
            Name = "Animal",
            Topics = new List<CachedTopic>
            {
                new CachedTopic { Id = 153, Name = "a dog dies" }
            }
        };

        // Assert
        Assert.Equal(2, category.Id);
        Assert.Equal("Animal", category.Name);
        Assert.Single(category.Topics);
    }

    [Fact]
    public void CachedTopic_Properties_SetCorrectly()
    {
        // Arrange & Act
        var topic = new CachedTopic
        {
            Id = 153,
            Name = "a dog dies",
            DoesName = "Does the dog die"
        };

        // Assert
        Assert.Equal(153, topic.Id);
        Assert.Equal("a dog dies", topic.Name);
        Assert.Equal("Does the dog die", topic.DoesName);
    }

    [Fact]
    public void TriggerCache_EmptyCategories_InitializesEmpty()
    {
        // Arrange & Act
        var cache = new TriggerCache();

        // Assert
        Assert.NotNull(cache.Categories);
        Assert.Empty(cache.Categories);
    }

    [Fact]
    public void CachedCategory_EmptyTopics_InitializesEmpty()
    {
        // Arrange & Act
        var category = new CachedCategory();

        // Assert
        Assert.NotNull(category.Topics);
        Assert.Empty(category.Topics);
    }

    [Fact]
    public async Task MockedService_GetOrRefreshCacheAsync_CanBeMocked()
    {
        // Arrange
        var mockService = new Mock<TriggerCacheService>(_apiClientMock.Object, _loggerMock.Object);
        var expectedCache = new TriggerCache
        {
            LastRefreshed = DateTime.UtcNow,
            Categories = new List<CachedCategory>
            {
                new CachedCategory { Id = 2, Name = "Animal", Topics = new List<CachedTopic>() }
            }
        };

        mockService
            .Setup(x => x.GetOrRefreshCacheAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCache);

        // Act
        var result = await mockService.Object.GetOrRefreshCacheAsync(false, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Categories);
        Assert.Equal("Animal", result.Categories[0].Name);
    }

    [Fact]
    public async Task MockedService_RefreshCacheAsync_CanBeMocked()
    {
        // Arrange
        var mockService = new Mock<TriggerCacheService>(_apiClientMock.Object, _loggerMock.Object);
        var expectedCache = new TriggerCache
        {
            LastRefreshed = DateTime.UtcNow,
            Categories = new List<CachedCategory>
            {
                new CachedCategory { Id = 3, Name = "Violence", Topics = new List<CachedTopic>() }
            }
        };

        mockService
            .Setup(x => x.RefreshCacheAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCache);

        // Act
        var result = await mockService.Object.RefreshCacheAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Categories);
        Assert.Equal("Violence", result.Categories[0].Name);
    }

    [Fact]
    public void ExtractCategoriesAndTopics_ExtractsFromTopicItemStats()
    {
        // Arrange
        var categories = new Dictionary<int, CachedCategory>();
        var stats = new List<DtddTopicItemStat>
        {
            new DtddTopicItemStat
            {
                Topic = new DtddTopic
                {
                    Id = 153,
                    Name = "a dog dies",
                    TopicCategoryId = 2,
                    TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
                },
                TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
            }
        };

        // Act
        TriggerCacheService.ExtractCategoriesAndTopics(stats, categories);

        // Assert
        Assert.Single(categories);
        Assert.True(categories.ContainsKey(2));
        Assert.Equal("Animal", categories[2].Name);
        Assert.Single(categories[2].Topics);
        Assert.Equal("a dog dies", categories[2].Topics[0].Name);
    }

    [Fact]
    public void ExtractCategoriesAndTopics_SkipsNullTopics()
    {
        // Arrange
        var categories = new Dictionary<int, CachedCategory>();
        var stats = new List<DtddTopicItemStat>
        {
            new DtddTopicItemStat { Topic = null }
        };

        // Act
        TriggerCacheService.ExtractCategoriesAndTopics(stats, categories);

        // Assert
        Assert.Empty(categories);
    }

    [Fact]
    public void ExtractCategoriesAndTopics_SkipsZeroCategoryId()
    {
        // Arrange
        var categories = new Dictionary<int, CachedCategory>();
        var stats = new List<DtddTopicItemStat>
        {
            new DtddTopicItemStat
            {
                Topic = new DtddTopic
                {
                    Id = 153,
                    Name = "a dog dies",
                    TopicCategoryId = null  // No category ID
                },
                TopicCategory = null  // No category from stat either
            }
        };

        // Act
        TriggerCacheService.ExtractCategoriesAndTopics(stats, categories);

        // Assert
        Assert.Empty(categories);
    }

    [Fact]
    public void ExtractCategoriesAndTopics_UsesCategoryFromStatWhenTopicCategoryIdIsNull()
    {
        // Arrange
        var categories = new Dictionary<int, CachedCategory>();
        var stats = new List<DtddTopicItemStat>
        {
            new DtddTopicItemStat
            {
                Topic = new DtddTopic
                {
                    Id = 153,
                    Name = "a dog dies",
                    TopicCategoryId = null  // No category ID on topic
                },
                TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }  // Category from stat
            }
        };

        // Act
        TriggerCacheService.ExtractCategoriesAndTopics(stats, categories);

        // Assert
        Assert.Single(categories);
        Assert.True(categories.ContainsKey(2));
        Assert.Equal("Animal", categories[2].Name);
    }

    [Fact]
    public void ExtractCategoriesAndTopics_DeduplicatesTopics()
    {
        // Arrange
        var categories = new Dictionary<int, CachedCategory>();
        var stats = new List<DtddTopicItemStat>
        {
            new DtddTopicItemStat
            {
                Topic = new DtddTopic
                {
                    Id = 153,
                    Name = "a dog dies",
                    TopicCategoryId = 2,
                    TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
                },
                TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
            },
            new DtddTopicItemStat
            {
                Topic = new DtddTopic
                {
                    Id = 153,  // Same topic ID
                    Name = "a dog dies",
                    TopicCategoryId = 2,
                    TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
                },
                TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
            }
        };

        // Act
        TriggerCacheService.ExtractCategoriesAndTopics(stats, categories);

        // Assert
        Assert.Single(categories);
        Assert.Single(categories[2].Topics);  // Should not duplicate
    }

    [Fact]
    public void ExtractCategoriesAndTopics_HandlesMultipleCategories()
    {
        // Arrange
        var categories = new Dictionary<int, CachedCategory>();
        var stats = new List<DtddTopicItemStat>
        {
            new DtddTopicItemStat
            {
                Topic = new DtddTopic
                {
                    Id = 153,
                    Name = "a dog dies",
                    TopicCategoryId = 2,
                    TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
                },
                TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
            },
            new DtddTopicItemStat
            {
                Topic = new DtddTopic
                {
                    Id = 101,
                    Name = "blood/gore",
                    TopicCategoryId = 3,
                    TopicCategory = new DtddTopicCategory { Id = 3, Name = "Violence" }
                },
                TopicCategory = new DtddTopicCategory { Id = 3, Name = "Violence" }
            }
        };

        // Act
        TriggerCacheService.ExtractCategoriesAndTopics(stats, categories);

        // Assert
        Assert.Equal(2, categories.Count);
        Assert.True(categories.ContainsKey(2));
        Assert.True(categories.ContainsKey(3));
        Assert.Equal("Animal", categories[2].Name);
        Assert.Equal("Violence", categories[3].Name);
    }

    [Fact]
    public void ExtractCategoriesAndTopics_HandlesMultipleTopicsInSameCategory()
    {
        // Arrange
        var categories = new Dictionary<int, CachedCategory>();
        var stats = new List<DtddTopicItemStat>
        {
            new DtddTopicItemStat
            {
                Topic = new DtddTopic
                {
                    Id = 153,
                    Name = "a dog dies",
                    TopicCategoryId = 2,
                    TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
                },
                TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
            },
            new DtddTopicItemStat
            {
                Topic = new DtddTopic
                {
                    Id = 154,
                    Name = "a cat dies",
                    TopicCategoryId = 2,
                    TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
                },
                TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
            }
        };

        // Act
        TriggerCacheService.ExtractCategoriesAndTopics(stats, categories);

        // Assert
        Assert.Single(categories);
        Assert.Equal(2, categories[2].Topics.Count);
    }

    [Fact]
    public void ExtractCategoriesAndTopics_PreservesDoesName()
    {
        // Arrange
        var categories = new Dictionary<int, CachedCategory>();
        var stats = new List<DtddTopicItemStat>
        {
            new DtddTopicItemStat
            {
                Topic = new DtddTopic
                {
                    Id = 153,
                    Name = "a dog dies",
                    DoesName = "Does the dog die",
                    TopicCategoryId = 2,
                    TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
                },
                TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
            }
        };

        // Act
        TriggerCacheService.ExtractCategoriesAndTopics(stats, categories);

        // Assert
        Assert.Equal("Does the dog die", categories[2].Topics[0].DoesName);
    }

    [Fact]
    public void TriggerCache_WithLastRefreshed_SerializesDateCorrectly()
    {
        // Arrange
        var timestamp = new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Utc);
        var cache = new TriggerCache
        {
            LastRefreshed = timestamp,
            Categories = new List<CachedCategory>()
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(cache);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<TriggerCache>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(timestamp, deserialized.LastRefreshed);
    }

    [Fact]
    public void TriggerCache_WithMultipleCategoriesAndTopics_SerializesCorrectly()
    {
        // Arrange
        var cache = new TriggerCache
        {
            LastRefreshed = DateTime.UtcNow,
            Categories = new List<CachedCategory>
            {
                new CachedCategory
                {
                    Id = 2,
                    Name = "Animal",
                    Topics = new List<CachedTopic>
                    {
                        new CachedTopic { Id = 153, Name = "a dog dies", DoesName = "Does the dog die" },
                        new CachedTopic { Id = 154, Name = "a cat dies", DoesName = "Does the cat die" }
                    }
                },
                new CachedCategory
                {
                    Id = 3,
                    Name = "Violence",
                    Topics = new List<CachedTopic>
                    {
                        new CachedTopic { Id = 101, Name = "blood/gore", DoesName = "Is there blood/gore" }
                    }
                }
            }
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(cache);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<TriggerCache>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Categories.Count);
        Assert.Equal(2, deserialized.Categories[0].Topics.Count);
        Assert.Single(deserialized.Categories[1].Topics);
    }

    [Fact]
    public void CachedTopic_WithNullDoesName_SerializesCorrectly()
    {
        // Arrange
        var topic = new CachedTopic
        {
            Id = 153,
            Name = "a dog dies",
            DoesName = null
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(topic);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<CachedTopic>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Null(deserialized.DoesName);
    }
}
