using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Plugin.DoesTheDogDie.E2ETests.Fixtures;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.E2ETests.Tests;

/// <summary>
/// Covers the key-handling paths that only exist once the plugin is hosted by a real Jellyfin: the
/// unconfigured-key behaviour, the plugin's own TestKey/Budget endpoints, and the check that the
/// client library's SQLite cache actually opens inside the Jellyfin host process.
/// </summary>
[Trait("Category", "E2E")]
[Collection("Jellyfin")]
public sealed class ApiKeyTests
{
    private const string DtddStartMarker = "<!-- DTDD_START -->";

    /// <summary>
    /// The distinctive part of the warning DtddClientProvider.CreateCache logs when opening the SQLite
    /// cache throws and it falls back to InMemoryDtddCache. Kept in sync with that message by hand: a
    /// test assembly cannot see the string constant, since it is inlined into a logging call.
    /// </summary>
    private const string InMemoryFallbackWarning = "falling back to an in-memory cache";

    private readonly JellyfinFixture _fixture;

    public ApiKeyTests(JellyfinFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// With no API key the plugin must not decorate anything: DtddMetadataService short-circuits before
    /// it ever reaches the client, so no verdict tags and no injected overview section may survive a
    /// user-initiated refresh.
    /// </summary>
    [Fact]
    public async Task NoApiKey_LeavesItemsUntagged()
    {
        var johnWick = await GetJohnWickAsync();

        // Precondition: the item IS tagged with the key configured, so the assertions below are not
        // vacuously true against an item that never had tags in the first place.
        await RestoreDefaultConfigAndRefreshAsync(johnWick.Id);

        try
        {
            await _fixture.Client.SetPluginConfigurationAsync(
                JellyfinFixture.PluginId,
                TestHelpers.ConfigWith(("ApiKey", string.Empty), ("AddDescriptionWarnings", true)));
            await _fixture.Client.RefreshItemMetadataAsync(johnWick.Id, replaceAllMetadata: true);

            await TestHelpers.WaitForAsync(
                async () =>
                {
                    var current = await GetJohnWickAsync();
                    return !current.Tags.Any(IsDtddTag);
                },
                TimeSpan.FromSeconds(30),
                failureMessage: "Expected every DTDD tag to disappear once the API key is cleared");

            var refreshed = await GetJohnWickAsync();
            refreshed.Tags.Should().NotContain(
                t => t.StartsWith("CW:", StringComparison.Ordinal),
                "an unconfigured plugin must not claim content warnings");
            refreshed.Tags.Should().NotContain(
                t => t.StartsWith("Safe:", StringComparison.Ordinal),
                "an unconfigured plugin must not claim an item is safe either");
            (refreshed.Overview ?? string.Empty).Should().NotContain(
                DtddStartMarker,
                "no DTDD section may be injected without a key, even with AddDescriptionWarnings on");
        }
        finally
        {
            // Shared fixture: every later test assumes the default config and the tags it produces.
            await RestoreDefaultConfigAndRefreshAsync(johnWick.Id);
        }
    }

    /// <summary>
    /// The configuration page's "test key" button round-trips through the plugin's own controller and
    /// the client library down to the (stubbed) API.
    /// </summary>
    [Fact]
    public async Task TestKeyEndpoint_ReportsSuccess_WithConfiguredKey()
    {
        await _fixture.Client.SetPluginConfigurationAsync(JellyfinFixture.PluginId, TestHelpers.DefaultPluginConfig());

        var result = await _fixture.Client.TestDtddKeyAsync();

        result.Success.Should().BeTrue(
            "the fixture's key is accepted by the WireMock stubs; message was: {0}",
            result.Message);
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// The Budget endpoint must surface the rate-limit figures the client library parsed out of the API
    /// response headers, not zeros or nulls.
    /// </summary>
    [Fact]
    public async Task BudgetEndpoint_ReportsRateLimitsParsedFromApiHeaders()
    {
        try
        {
            // A zero-day taxonomy max-age does two things this test needs: it changes the client
            // stack's signature (forcing a rebuild, so no budget observed by an earlier test can be
            // mistaken for this one's), and it marks every cached taxonomy entry stale, so TestKey's
            // GetTopics call reaches WireMock instead of being answered from the SQLite cache.
            await _fixture.Client.SetPluginConfigurationAsync(
                JellyfinFixture.PluginId,
                TestHelpers.ConfigWith(("TaxonomyCacheDays", 0)));

            var keyTest = await _fixture.Client.TestDtddKeyAsync();
            keyTest.Success.Should().BeTrue("message was: {0}", keyTest.Message);

            JellyfinClient.RateLimitBudgetDto? budget = null;
            await TestHelpers.WaitForAsync(
                async () =>
                {
                    budget = await _fixture.Client.GetDtddBudgetAsync();
                    return budget is not null;
                },
                TimeSpan.FromSeconds(30),
                failureMessage: "Budget endpoint never reported a rate-limit status after a live API call");

            // Every stub sends the same limits; the remaining counts differ per endpoint, and which
            // call was observed last is not deterministic, so assert the stubbed range rather than one
            // exact figure.
            budget!.MinuteLimit.Should().Be(30, "X-RateLimit-Limit-Minute is 30 on every stub");
            budget.MonthLimit.Should().Be(5000, "X-RateLimit-Limit-Month is 5000 on every stub");
            budget.MinuteRemaining.Should().BeInRange(24, 29, "the stubs report 24-29 remaining this minute");
            budget.MonthRemaining.Should().BeInRange(4994, 4999, "the stubs report 4994-4999 remaining this month");
        }
        finally
        {
            await _fixture.Client.SetPluginConfigurationAsync(JellyfinFixture.PluginId, TestHelpers.DefaultPluginConfig());
        }
    }

    /// <summary>
    /// The spec's open risk: SQLitePCLRaw's native bundle has to load inside the Jellyfin host, which
    /// ships its own SQLite stack. If it does not, DtddClientProvider.CreateCache silently degrades to
    /// an in-memory cache — the plugin keeps working but re-fetches everything after each restart, and
    /// the only visible sign is that warning in Jellyfin's log.
    /// </summary>
    [Fact]
    public async Task SqliteCache_InitializesInsideJellyfin()
    {
        var johnWick = await GetJohnWickAsync();

        await _fixture.Client.SetPluginConfigurationAsync(JellyfinFixture.PluginId, TestHelpers.DefaultPluginConfig());
        await _fixture.Client.RefreshItemMetadataAsync(johnWick.Id, replaceAllMetadata: true);

        // Wait for the refresh to actually exercise the cache before reading the log.
        await TestHelpers.WaitForAsync(
            async () =>
            {
                var current = await GetJohnWickAsync();
                return current.Tags.Contains("CW: a dog dies");
            },
            TimeSpan.FromSeconds(30),
            failureMessage: "Refresh did not re-apply tags, so the cache path may not have been exercised");

        var logs = await _fixture.GetJellyfinLogsAsync();

        // Guard against a vacuous pass: an empty or truncated log would satisfy the NotContain below
        // without ever having observed the plugin start.
        logs.Should().Contain(
            "Jellyfin.Plugin.DoesTheDogDie.dll",
            "the captured log must actually cover the plugin's startup for the fallback check to mean anything");

        logs.Should().NotContain(
            InMemoryFallbackWarning,
            "SQLitePCLRaw must load inside the Jellyfin host; the in-memory fallback means cached DtDD data is lost on every restart");
    }

    private static bool IsDtddTag(string tag)
        => tag.StartsWith("CW:", StringComparison.Ordinal) || tag.StartsWith("Safe:", StringComparison.Ordinal);

    private async Task<JellyfinClient.JellyfinItemDto> GetJohnWickAsync()
    {
        var movies = await _fixture.Client.GetItemsAsync("Movie");
        return movies.Single(m => m.Name == "John Wick");
    }

    private async Task RestoreDefaultConfigAndRefreshAsync(string itemId)
    {
        await _fixture.Client.SetPluginConfigurationAsync(JellyfinFixture.PluginId, TestHelpers.DefaultPluginConfig());
        await _fixture.Client.RefreshItemMetadataAsync(itemId, replaceAllMetadata: true);
        await TestHelpers.WaitForAsync(
            async () =>
            {
                var current = await GetJohnWickAsync();
                return current.Tags.Contains("CW: a dog dies")
                    && (current.Overview is null || !current.Overview.Contains(DtddStartMarker, StringComparison.Ordinal));
            },
            TimeSpan.FromSeconds(30),
            failureMessage: "Cleanup: CW: tags should return and the DTDD section should be gone under the default config");
    }
}
