using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DoesTheDogDie;
using DoesTheDogDie.Api;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Support;

/// <summary>
/// A hand-written IDtddClient stand-in: canned results by key, or a queued exception to throw.
/// </summary>
public sealed class FakeDtddClient : IDtddClient
{
    /// <inheritdoc />
    public RateLimitStatus? CurrentBudget { get; set; }

    /// <summary>Gets the canned search results, keyed by <see cref="ItemSearch.ToQueryString"/>.</summary>
    public Dictionary<string, IReadOnlyList<Item>> SearchResults { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the canned item details, keyed by item id.</summary>
    public Dictionary<int, ItemDetail> Items { get; } = new();

    /// <summary>Gets the canned taxonomy topics.</summary>
    public List<Topic> Topics { get; } = new();

    /// <summary>Gets the canned taxonomy categories.</summary>
    public List<TopicCategory> Categories { get; } = new();

    /// <summary>Gets or sets an exception to throw from <see cref="GetItemAsync"/>.</summary>
    public Exception? ThrowOnGetItem { get; set; }

    /// <summary>Gets or sets an exception to throw from <see cref="SearchItemsAsync"/>.</summary>
    public Exception? ThrowOnSearch { get; set; }

    /// <summary>Gets or sets an exception to throw from <see cref="GetRatingsAsync"/>.</summary>
    public Exception? ThrowOnGetRatings { get; set; }

    /// <summary>Gets or sets an exception to throw from <see cref="GetTopicsAsync"/>.</summary>
    public Exception? ThrowOnGetTopics { get; set; }

    /// <summary>Gets the canned ratings, keyed by item id.</summary>
    public Dictionary<int, IReadOnlyList<Rating>> Ratings { get; } = new();

    /// <summary>Gets the query strings passed to every <see cref="SearchItemsAsync"/> call.</summary>
    public List<string> SearchCalls { get; } = new();

    /// <summary>Gets the item ids passed to every <see cref="GetItemAsync"/> call.</summary>
    public List<int> GetItemCalls { get; } = new();

    /// <summary>Gets the item ids passed to every <see cref="GetRatingsAsync"/> call.</summary>
    public List<int> GetRatingsCalls { get; } = new();

    /// <inheritdoc />
    public Task<DtddResult<IReadOnlyList<Item>>> SearchItemsAsync(ItemSearch search, CancellationToken ct = default)
    {
        SearchCalls.Add(search.ToQueryString());

        if (ThrowOnSearch is not null)
        {
            return Task.FromException<DtddResult<IReadOnlyList<Item>>>(ThrowOnSearch);
        }

        SearchResults.TryGetValue(search.ToQueryString(), out var items);
        return Task.FromResult(Ok<IReadOnlyList<Item>>(items ?? Array.Empty<Item>()));
    }

    /// <inheritdoc />
    public Task<DtddResult<ItemDetail>> GetItemAsync(int itemId, CancellationToken ct = default)
    {
        GetItemCalls.Add(itemId);

        if (ThrowOnGetItem is not null)
        {
            return Task.FromException<DtddResult<ItemDetail>>(ThrowOnGetItem);
        }

        if (!Items.TryGetValue(itemId, out var detail))
        {
            return Task.FromException<DtddResult<ItemDetail>>(
                new DtddNotFoundException(HttpStatusCode.NotFound, "not_found", $"item {itemId}", null));
        }

        return Task.FromResult(Ok(detail));
    }

    /// <inheritdoc />
    public Task<DtddResult<IReadOnlyList<Rating>>> GetRatingsAsync(int itemId, int? topicId = null, CancellationToken ct = default)
    {
        GetRatingsCalls.Add(itemId);

        if (ThrowOnGetRatings is not null)
        {
            return Task.FromException<DtddResult<IReadOnlyList<Rating>>>(ThrowOnGetRatings);
        }

        Ratings.TryGetValue(itemId, out var ratings);
        return Task.FromResult(Ok<IReadOnlyList<Rating>>(ratings ?? Array.Empty<Rating>()));
    }

    /// <inheritdoc />
    public Task<DtddResult<IReadOnlyList<Topic>>> GetTopicsAsync(CancellationToken ct = default)
    {
        if (ThrowOnGetTopics is not null)
        {
            return Task.FromException<DtddResult<IReadOnlyList<Topic>>>(ThrowOnGetTopics);
        }

        return Task.FromResult(Ok<IReadOnlyList<Topic>>(Topics));
    }

    /// <inheritdoc />
    public Task<DtddResult<IReadOnlyList<ItemType>>> GetItemTypesAsync(CancellationToken ct = default)
        => Task.FromResult(Ok<IReadOnlyList<ItemType>>(Array.Empty<ItemType>()));

    /// <inheritdoc />
    public Task<DtddResult<IReadOnlyList<TopicCategory>>> GetTopicCategoriesAsync(CancellationToken ct = default)
        => Task.FromResult(Ok<IReadOnlyList<TopicCategory>>(Categories));

    /// <inheritdoc />
    public Task<DtddResult<IReadOnlyList<TopicSuperCategory>>> GetTopicSuperCategoriesAsync(CancellationToken ct = default)
        => Task.FromResult(Ok<IReadOnlyList<TopicSuperCategory>>(Array.Empty<TopicSuperCategory>()));

    private static DtddResult<T> Ok<T>(T value)
        => new(value, ResultSource.Live, DateTimeOffset.UnixEpoch);
}
