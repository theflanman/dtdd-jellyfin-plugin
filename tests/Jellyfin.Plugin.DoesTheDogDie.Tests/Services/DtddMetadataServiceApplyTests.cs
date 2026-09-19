using System;
using System.Collections.Generic;
using System.Linq;
using DoesTheDogDie;
using DoesTheDogDie.Api;
using DoesTheDogDie.Statistics;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Services;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Services;

public class DtddMetadataServiceApplyTests
{
    private readonly Mock<IPluginConfigurationAccessor> _configAccessor = new();

    private DtddMetadataService CreateService() => new(
        () => new Tests.Support.FakeDtddClient(),
        _configAccessor.Object,
        NullLogger<DtddMetadataService>.Instance,
        new OverviewFormatter());

    private static TriggerInfo Trigger(string name, int yes, int no, int topicId = 201) =>
        new(
            new Topic { Id = topicId, Name = name, TopicCategoryId = 3 },
            yes,
            no,
            TriggerConfidence.Compute(yes, no));

    private static DtddItemData Data(params TriggerInfo[] triggers) =>
        new(1234, triggers, ResultSource.Live, DateTimeOffset.UnixEpoch);

    private static PluginConfiguration Config() => new()
    {
        ShowAllTriggers = true,
        AddWarningTags = true,
        TagPrefix = "CW:",
        SafeTagPrefix = "Safe:",
    };

    [Fact]
    public void Apply_AddsWarningTagForLikelyPresent()
    {
        var item = new Movie { Name = "John Wick" };

        CreateService().Apply(item, Data(Trigger("a dog dies", 40, 1)), Config());

        Assert.Contains("CW: a dog dies", item.Tags);
    }

    [Fact]
    public void Apply_AddsSafeTagForLikelyAbsent()
    {
        var item = new Movie { Name = "John Wick" };

        CreateService().Apply(item, Data(Trigger("a dog dies", 1, 40)), Config());

        Assert.Contains("Safe: a dog dies", item.Tags);
    }

    [Fact]
    public void Apply_AddsNoTagForUncertain()
    {
        var item = new Movie { Name = "John Wick" };

        CreateService().Apply(item, Data(Trigger("a dog dies", 1, 1)), Config());

        Assert.Empty(item.Tags);
    }

    [Fact]
    public void Apply_RemovesStaleTagsWithConfiguredPrefixes()
    {
        var item = new Movie
        {
            Name = "John Wick",
            Tags = new[] { "CW: something old", "Safe: another old", "Favourites" },
        };

        CreateService().Apply(item, Data(Trigger("a dog dies", 40, 1)), Config());

        Assert.Contains("CW: a dog dies", item.Tags);
        Assert.DoesNotContain("CW: something old", item.Tags);
        Assert.DoesNotContain("Safe: another old", item.Tags);
    }

    [Fact]
    public void Apply_PreservesUnrelatedTags()
    {
        var item = new Movie { Name = "John Wick", Tags = new[] { "Favourites" } };

        CreateService().Apply(item, Data(Trigger("a dog dies", 40, 1)), Config());

        Assert.Contains("Favourites", item.Tags);
    }

    [Fact]
    public void Apply_LeavesTagsAlone_WhenAddWarningTagsDisabled()
    {
        var item = new Movie { Name = "John Wick", Tags = new[] { "CW: something old" } };
        var config = Config();
        config.AddWarningTags = false;

        CreateService().Apply(item, Data(Trigger("a dog dies", 40, 1)), config);

        Assert.Contains("CW: something old", item.Tags);
    }

    [Fact]
    public void Apply_InjectsDescription_WhenEnabled()
    {
        var item = new Movie { Name = "John Wick", Overview = "Base plot." };
        var config = Config();
        config.AddDescriptionWarnings = true;

        CreateService().Apply(item, Data(Trigger("a dog dies", 40, 1)), config);

        Assert.Contains("Content warnings: a dog dies", item.Overview, StringComparison.Ordinal);
        Assert.Contains("Base plot.", item.Overview, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_RemovesDescriptionSection_WhenDisabled()
    {
        var item = new Movie { Name = "John Wick", Overview = "Base plot." };
        var enabled = Config();
        enabled.AddDescriptionWarnings = true;
        var service = CreateService();
        service.Apply(item, Data(Trigger("a dog dies", 40, 1)), enabled);

        var disabled = Config();
        disabled.AddDescriptionWarnings = false;
        service.Apply(item, Data(Trigger("a dog dies", 40, 1)), disabled);

        Assert.Equal("Base plot.", item.Overview);
    }

    [Fact]
    public void Apply_SkipsOverview_WhenOverviewLocked()
    {
        var item = new Movie
        {
            Name = "John Wick",
            Overview = "Base plot.",
            LockedFields = new[] { MetadataField.Overview },
        };
        var config = Config();
        config.AddDescriptionWarnings = true;

        CreateService().Apply(item, Data(Trigger("a dog dies", 40, 1)), config);

        Assert.Equal("Base plot.", item.Overview);
    }

    [Fact]
    public void Apply_DoesNotDuplicateTag_WhenTwoTriggersFormatToSameTagNameByCase()
    {
        var item = new Movie { Name = "John Wick" };

        CreateService().Apply(
            item,
            Data(
                Trigger("a dog dies", 40, 1, topicId: 201),
                Trigger("A DOG DIES", 40, 1, topicId: 202)),
            Config());

        Assert.Single(item.Tags, t => string.Equals(t, "CW: a dog dies", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Apply_SkipsTags_WhenTagsLocked()
    {
        var item = new Movie
        {
            Name = "John Wick",
            Tags = new[] { "CW: something old" },
            LockedFields = new[] { MetadataField.Tags },
        };

        CreateService().Apply(item, Data(Trigger("a dog dies", 40, 1)), Config());

        Assert.Equal(new[] { "CW: something old" }, item.Tags);
    }
}
