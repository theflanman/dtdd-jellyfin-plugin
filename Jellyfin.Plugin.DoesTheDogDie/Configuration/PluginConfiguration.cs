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
        AddWarningTags = true;
        TagPrefix = "CW:";
        SafeTagPrefix = "Safe:";
        ShowConfidenceInTags = false;
        ShowAllTriggers = false;
        EnabledCategoryIds = new List<int>();
        EnabledTopicIds = new List<int>();
        AddDescriptionWarnings = false;
        IncludeTopComment = false;
        MaxCommentLength = 200;
        ApiKey = string.Empty;
        DecisionThreshold = 0.5;
        IntervalMass = 0.95;
        ItemCacheDays = 30;
        TaxonomyCacheDays = 7;
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
    /// Gets or sets a value indicating whether to append the confidence
    /// percentage to tag names, e.g. "CW: a dog dies (95%)".
    /// </summary>
    public bool ShowConfidenceInTags { get; set; }

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

    /// <summary>
    /// Gets or sets a value indicating whether to inject warnings into description.
    /// </summary>
    public bool AddDescriptionWarnings { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include top comments.
    /// </summary>
    public bool IncludeTopComment { get; set; }

    /// <summary>
    /// Gets or sets the maximum length for included comments.
    /// </summary>
    public int MaxCommentLength { get; set; }

    /// <summary>
    /// Gets or sets the user's DoesTheDogDie API key, sent as the X-API-KEY header.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the probability threshold used to decide a trigger's verdict.
    /// Maps to ConfidenceOptions.DecisionThreshold.
    /// </summary>
    public double DecisionThreshold { get; set; }

    /// <summary>
    /// Gets or sets the probability mass covered by the credible interval.
    /// Maps to ConfidenceOptions.IntervalMass.
    /// </summary>
    public double IntervalMass { get; set; }

    /// <summary>
    /// Gets or sets how many days a cached item detail stays fresh.
    /// Maps to CachePolicy.ItemMaxAge.
    /// </summary>
    public int ItemCacheDays { get; set; }

    /// <summary>
    /// Gets or sets how many days cached taxonomy data stays fresh.
    /// Maps to CachePolicy.TaxonomyMaxAge.
    /// </summary>
    public int TaxonomyCacheDays { get; set; }
}
