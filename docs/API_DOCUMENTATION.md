# DoesTheDogDie API — Plugin-Specific Notes

**Last Updated:** 2026-09-18

As of the v3 client library migration, this plugin no longer talks HTTP to DoesTheDogDie.com
directly. All API access — endpoints, models, authentication, error handling, rate limiting — lives
in the separate `DoesTheDogDie` client library at `../../dtdd-client` (referenced via the
`DtddClientPath` MSBuild property; see `CLAUDE.md`).

**The authoritative v3 API reference is [`../../dtdd-client/CLAUDE.md`](../../dtdd-client/CLAUDE.md)**
("DtDD API v3 reference" section) — endpoint table, model field lists, error codes, and rate-limit
header names all live there and should not be duplicated here. This file only documents what is
plugin-specific: how the plugin uses that library.

## What the plugin calls, and why

`DtddMetadataService` (`Jellyfin.Plugin.DoesTheDogDie/Services/DtddMetadataService.cs`) is the
plugin's only caller into `IDtddClient`. Per item, in order, it:

1. Resolves a DtDD item id: tries the id already stored on the item (`Dtdd` provider id), then an
   IMDb lookup (`IDtddClient.SearchItemsAsync(ItemSearch.ByImdbId(...))`), then an exact
   name+year lookup (`ItemSearch.ByName(...)`). It never issues a free-text search — DtDD's
   free-text results are too imprecise to auto-apply to a library item without user review.
2. Fetches item detail (`GetItemAsync`) and the full topic taxonomy (`GetTopicsAsync`), and joins
   `TopicItemStat` rows onto their `Topic` by id (a stat referencing an unrecognized topic id is
   dropped and logged at Debug, not treated as an error).
3. Runs each `TopicItemStat` through the library's Beta-distribution confidence model
   (`TopicItemStat.ToConfidence(ConfidenceOptions)`, configured from `PluginConfiguration.DecisionThreshold`
   / `IntervalMass`) to get a `TriggerVerdict` (likely present / likely absent / uncertain) and a
   credible interval.
4. If `IncludeTopComment` is enabled, fetches `GetRatingsAsync(itemId, topicId: null)` — a second
   request per title — and keeps the highest-voted comment per topic that has one.

## Error handling the plugin relies on

`DtddMetadataService.ResolveAsync` catches the library's typed exception hierarchy
(`DtddNotFoundException`, `DtddMinuteRateLimitException`, `DtddMonthlyRateLimitException`,
`DtddAuthenticationException`, `DtddUpgradeRequiredException`, `DtddQueueFullException`) and
degrades to "skip this item" rather than throwing into the metadata provider pipeline. A monthly
rate-limit, auth failure, or upgrade-required response logs a one-shot warning (not spammed per
item) until the plugin configuration next changes.

With no API key configured, `DtddClientProvider` never touches the network for a cache miss: it
builds the stack with a local `UnconfiguredApiClient` that throws `DtddAuthenticationException`
synchronously, so cache hits still resolve but every miss fails immediately and locally. See the
README's "How the plugin behaves" section for the user-facing description of this.

## Known ids used in the E2E fixtures

The E2E WireMock stubs (`tests/Jellyfin.Plugin.DoesTheDogDie.E2ETests/Stubs/wiremock-mappings/`) use
fictional ids chosen for readability, not real DtDD ids:

| Stub | Fictional id |
|------|---------------|
| John Wick item (`tt2911666`) | item id `1234` |
| Topics | `201`/`202`/`203` in categories `3`/`4` |

These do not need to match production DtDD ids — WireMock intercepts the request before it reaches
doesthedogdie.com — but see `docs/superpowers/specs/2026-09-18-dtdd-client-migration-design.md` for
what a live-API check against real ids found.

## Attribution

DtDD's terms require the exact phrase "Powered by DoesTheDogDie.com", linked to
`https://www.doesthedogdie.com`, on every view that displays DtDD data. The library exposes this as
`DoesTheDogDie.Api.DtddAttribution` (`Phrase`, `Url`, `Html`); the plugin renders it at the end of
every injected description (`OverviewFormatter`) and as a static link on the configuration page
(`Configuration/configPage.html`).
