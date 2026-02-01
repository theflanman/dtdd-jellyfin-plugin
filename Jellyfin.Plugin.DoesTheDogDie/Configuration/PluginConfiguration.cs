using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.DoesTheDogDie.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        EnableMovies = true;
        EnableSeries = true;
        EnableBooks = true;
        CacheDurationHours = 168; // 7 days
        MinVotesThreshold = 3;
        AddWarningTags = true;
        TagPrefix = "CW:";
        SafeTagPrefix = "Safe:";
        RefreshIntervalHours = 24;
        ShowAllTriggers = true;
        EnabledCategoryIds = new List<int>();
        EnabledTopicIds = new List<int>();
    }

    /// <summary>
    /// Gets or sets a value indicating whether to fetch warnings for movies.
    /// </summary>
    public bool EnableMovies { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to fetch warnings for TV series.
    /// </summary>
    public bool EnableSeries { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to fetch warnings for books.
    /// </summary>
    public bool EnableBooks { get; set; }

    /// <summary>
    /// Gets or sets the cache duration in hours.
    /// </summary>
    public int CacheDurationHours { get; set; }

    /// <summary>
    /// Gets or sets the minimum number of votes required to display a trigger.
    /// </summary>
    public int MinVotesThreshold { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to add warning tags to items.
    /// </summary>
    public bool AddWarningTags { get; set; }

    /// <summary>
    /// Gets or sets the prefix for warning tags (content has this trigger).
    /// </summary>
    public string TagPrefix { get; set; }

    /// <summary>
    /// Gets or sets the prefix for safe tags (content confirmed safe from this trigger).
    /// </summary>
    public string SafeTagPrefix { get; set; }

    /// <summary>
    /// Gets or sets the refresh interval in hours for the scheduled task.
    /// </summary>
    public int RefreshIntervalHours { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include all triggers.
    /// When false, uses category/topic filtering.
    /// </summary>
    public bool ShowAllTriggers { get; set; }

    /// <summary>
    /// Gets or sets the list of enabled category IDs.
    /// When ShowAllTriggers is false and this list is empty, all triggers are shown with a warning.
    /// </summary>
    public List<int> EnabledCategoryIds { get; set; }

    /// <summary>
    /// Gets or sets the list of enabled topic IDs for fine-grained control.
    /// When empty, all topics in enabled categories are shown.
    /// </summary>
    public List<int> EnabledTopicIds { get; set; }
}
