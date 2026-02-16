using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.DoesTheDogDie.Api;
using Jellyfin.Plugin.DoesTheDogDie.Api.Models;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Providers;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Providers;

public class DtddSeasonProviderTests
{
    private readonly Mock<DtddApiClient> _apiClientMock;
    private readonly Mock<IPluginConfigurationAccessor> _configAccessorMock;
    private readonly Mock<ILogger<DtddSeasonProvider>> _loggerMock;
    private readonly DtddSeasonProvider _provider;
    private readonly MetadataRefreshOptions _defaultOptions;

    public DtddSeasonProviderTests()
    {
        _apiClientMock = new Mock<DtddApiClient>(
            Mock.Of<System.Net.Http.IHttpClientFactory>(),
            Mock.Of<ILogger<DtddApiClient>>());
        _configAccessorMock = new Mock<IPluginConfigurationAccessor>();
        _loggerMock = new Mock<ILogger<DtddSeasonProvider>>();
        _provider = new DtddSeasonProvider(
            _apiClientMock.Object,
            _configAccessorMock.Object,
            _loggerMock.Object);
        _defaultOptions = new MetadataRefreshOptions(Mock.Of<IDirectoryService>());
    }

    [Fact]
    public void Name_ReturnsProviderName()
    {
        Assert.Equal(Constants.ProviderName, _provider.Name);
    }

    [Fact]
    public void Order_ReturnsHighValue()
    {
        Assert.Equal(100, _provider.Order);
    }

    [Fact]
    public async Task FetchAsync_NoConfiguration_ReturnsNone()
    {
        // Arrange
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns((PluginConfiguration?)null);
        var season = CreateSeason();

        // Act
        var result = await _provider.FetchAsync(season, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.None, result);
    }

    [Fact]
    public async Task FetchAsync_SeriesDisabled_ReturnsNone()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration { EnableSeries = false });
        var season = CreateSeason();

        // Act
        var result = await _provider.FetchAsync(season, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.None, result);
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FetchAsync_DtddIdAlreadyExists_CallsGetMediaDetailsAsync()
    {
        // Arrange -- DTDD ID exists but GetMediaDetailsAsync returns null (default mock),
        // so the provider returns None. The key assertion is that it still calls
        // GetMediaDetailsAsync to re-evaluate tags rather than early-returning.
        SetupConfiguration(new PluginConfiguration { EnableSeries = true });
        var season = CreateSeason();
        season.SetProviderId(Constants.ProviderId, "12345");

        // Act
        var result = await _provider.FetchAsync(season, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.None, result);
        _apiClientMock.Verify(
            x => x.GetMediaDetailsAsync(12345, It.IsAny<CancellationToken>()),
            Times.Once);
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FetchAsync_ExistingDtddId_StillUpdatesTags()
    {
        // Arrange -- Season already has a DTDD ID. The provider re-fetches details
        // and updates tags via TagHelper.UpdateWarningTags.
        SetupConfiguration(new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            MinVotesThreshold = 0
        });
        var season = CreateSeason();
        season.SetProviderId(Constants.ProviderId, "12345");

        var details = CreateMediaDetailsWithTriggers(12345, "Test Show");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(season, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.Contains("CW: a dog dies", season.Tags);
    }

    [Fact]
    public async Task FetchAsync_AdminUnticksTriggerCategory_RemovesStaleTag()
    {
        // Arrange -- Season had tags from a prior refresh where Animal + Violence
        // categories were both enabled. Admin now disables the Animal category.
        // The stale "CW: a dog dies" tag should be removed.
        SetupConfiguration(new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            SafeTagPrefix = "Safe:",
            MinVotesThreshold = 0,
            ShowAllTriggers = false,
            EnabledCategoryIds = new List<int> { 3 } // Only Violence enabled
        });
        var season = CreateSeason();
        season.SetProviderId(Constants.ProviderId, "12345");
        season.Tags = new[] { "CW: a dog dies", "CW: blood/gore" };

        var details = CreateMediaDetailsWithMultipleTriggers(12345, "Test Show");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(season, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.DoesNotContain("CW: a dog dies", season.Tags); // Animal category disabled
        Assert.Contains("CW: blood/gore", season.Tags); // Violence category still enabled
    }

    [Fact]
    public async Task FetchAsync_AddWarningTagsDisabled_RemovesExistingTags()
    {
        // Arrange -- Season has existing DTDD tags but config now has AddWarningTags=false.
        // All DTDD-prefixed tags should be removed.
        SetupConfiguration(new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = false,
            TagPrefix = "CW:",
            SafeTagPrefix = "Safe:"
        });
        var season = CreateSeason();
        season.SetProviderId(Constants.ProviderId, "12345");
        season.Tags = new[] { "CW: a dog dies", "Safe: blood/gore", "Custom Tag" };

        var details = CreateMediaDetailsWithTriggers(12345, "Test Show");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(season, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.DoesNotContain("CW: a dog dies", season.Tags);
        Assert.DoesNotContain("Safe: blood/gore", season.Tags);
        Assert.Contains("Custom Tag", season.Tags);
    }

    [Fact]
    public async Task FetchAsync_PreservesNonDtddTags()
    {
        // Arrange -- Season has custom user tags alongside DTDD tags.
        // After update, custom tags must survive.
        SetupConfiguration(new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            SafeTagPrefix = "Safe:",
            MinVotesThreshold = 0
        });
        var season = CreateSeason();
        season.SetProviderId(Constants.ProviderId, "12345");
        season.Tags = new[] { "Favorite", "Sci-Fi" };

        var details = CreateMediaDetailsWithTriggers(12345, "Test Show");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(season, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.Contains("Favorite", season.Tags);
        Assert.Contains("Sci-Fi", season.Tags);
        Assert.Contains("CW: a dog dies", season.Tags);
    }

    [Fact]
    public async Task FetchAsync_CategoryFilter_OnlyIncludesEnabledCategories()
    {
        // Arrange -- Config enables only the Animal category (ID 2).
        // Violence triggers should be excluded.
        SetupConfiguration(new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            SafeTagPrefix = "Safe:",
            MinVotesThreshold = 0,
            ShowAllTriggers = false,
            EnabledCategoryIds = new List<int> { 2 } // Only Animal
        });
        var season = CreateSeason();
        season.SetProviderId(Constants.ProviderId, "12345");

        var details = CreateMediaDetailsWithMultipleTriggers(12345, "Test Show");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(season, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.Contains("CW: a dog dies", season.Tags);
        Assert.DoesNotContain("CW: blood/gore", season.Tags); // Violence category not enabled
    }

    [Fact]
    public async Task FetchAsync_NegativeTriggers_AddsSafeTags()
    {
        // Arrange -- Trigger has more No votes than Yes votes, so it should
        // produce a "Safe:" tag instead of a "CW:" tag.
        SetupConfiguration(new PluginConfiguration
        {
            EnableSeries = true,
            AddWarningTags = true,
            TagPrefix = "CW:",
            SafeTagPrefix = "Safe:",
            MinVotesThreshold = 0
        });
        var season = CreateSeason();
        season.SetProviderId(Constants.ProviderId, "12345");

        var details = CreateMediaDetailsWithSafeTriggers(12345, "Test Show");
        _apiClientMock
            .Setup(x => x.GetMediaDetailsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _provider.FetchAsync(season, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.MetadataDownload, result);
        Assert.Contains("Safe: a dog dies", season.Tags);
        Assert.DoesNotContain("CW: a dog dies", season.Tags);
    }

    [Fact]
    public async Task FetchAsync_NoParentSeries_ReturnsNone()
    {
        // Arrange
        SetupConfiguration(new PluginConfiguration { EnableSeries = true });
        var season = new Season
        {
            Name = "Season 1",
            Tags = System.Array.Empty<string>()
        };
        // Note: season.Series will be null since we don't set it

        // Act
        var result = await _provider.FetchAsync(season, _defaultOptions, CancellationToken.None);

        // Assert
        Assert.Equal(ItemUpdateType.None, result);
        _apiClientMock.Verify(
            x => x.GetMediaDetailsByImdbIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void SetupConfiguration(PluginConfiguration config)
    {
        _configAccessorMock.Setup(x => x.GetConfiguration()).Returns(config);
    }

    private static Season CreateSeason()
    {
        var season = new Season
        {
            Name = "Season 1",
            Tags = System.Array.Empty<string>()
        };
        return season;
    }

    private static DtddMediaDetails CreateMediaDetailsWithTriggers(int id, string name)
    {
        return new DtddMediaDetails
        {
            Item = new DtddMediaItem { Id = id, Name = name },
            TopicItemStats = new System.Collections.Generic.List<DtddTopicItemStat>
            {
                new DtddTopicItemStat
                {
                    TopicItemId = 1, YesSum = 100, NoSum = 10, TopicId = 153,
                    Topic = new DtddTopic { Id = 153, Name = "a dog dies", TopicCategoryId = 2 },
                    TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
                }
            }
        };
    }

    private static DtddMediaDetails CreateMediaDetailsWithMultipleTriggers(int id, string name)
    {
        return new DtddMediaDetails
        {
            Item = new DtddMediaItem { Id = id, Name = name },
            TopicItemStats = new System.Collections.Generic.List<DtddTopicItemStat>
            {
                new DtddTopicItemStat
                {
                    TopicItemId = 1, YesSum = 100, NoSum = 10, TopicId = 153,
                    Topic = new DtddTopic { Id = 153, Name = "a dog dies", TopicCategoryId = 2 },
                    TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
                },
                new DtddTopicItemStat
                {
                    TopicItemId = 2, YesSum = 100, NoSum = 10, TopicId = 101,
                    Topic = new DtddTopic { Id = 101, Name = "blood/gore", TopicCategoryId = 3 },
                    TopicCategory = new DtddTopicCategory { Id = 3, Name = "Violence" }
                }
            }
        };
    }

    private static DtddMediaDetails CreateMediaDetailsWithSafeTriggers(int id, string name)
    {
        return new DtddMediaDetails
        {
            Item = new DtddMediaItem { Id = id, Name = name },
            TopicItemStats = new System.Collections.Generic.List<DtddTopicItemStat>
            {
                new DtddTopicItemStat
                {
                    TopicItemId = 1, YesSum = 10, NoSum = 100, TopicId = 153,
                    Topic = new DtddTopic { Id = 153, Name = "a dog dies", TopicCategoryId = 2 },
                    TopicCategory = new DtddTopicCategory { Id = 2, Name = "Animal" }
                }
            }
        };
    }

    private static DtddMediaDetails CreateMediaDetails(int id, string name)
    {
        return new DtddMediaDetails
        {
            Item = new DtddMediaItem { Id = id, Name = name },
            TopicItemStats = new System.Collections.Generic.List<DtddTopicItemStat>()
        };
    }
}
