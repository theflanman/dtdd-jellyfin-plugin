using System.Collections.Generic;
using DoesTheDogDie.Api;

namespace Jellyfin.Plugin.DoesTheDogDie.Api;

/// <summary>
/// The taxonomy the config page needs to render its category and topic tree.
/// </summary>
/// <param name="Categories">All topic categories.</param>
/// <param name="Topics">All topics.</param>
public sealed record TaxonomyResponse(
    IReadOnlyList<TopicCategory> Categories,
    IReadOnlyList<Topic> Topics);
