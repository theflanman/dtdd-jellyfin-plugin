# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Jellyfin plugin that integrates DoesTheDogDie.com content warnings into media libraries. Fetches trigger warnings (animal death, violence, jump scares, etc.) and adds them as metadata tags to movies and TV shows.

## Build Commands

**Requires the .NET 10 SDK**, installed user-locally at `~/.dotnet` (the system `dotnet` may only have 8/9). NuGet restore walks every target framework of the referenced `DoesTheDogDie` library before framework negotiation happens — it multi-targets `net9.0;net10.0` — so a net9-only SDK fails restore with `NETSDK1045`. Prefix every dotnet command with:

```bash
export PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet DOTNET_ROLL_FORWARD=LatestMajor
```

`DOTNET_ROLL_FORWARD=LatestMajor` is needed because that SDK root's `shared/` has ASP.NET Core 10 but no 9.x runtime, and the `net9.0` unit-test testhost needs one to roll forward to. The plugin itself still targets and ships `net9.0` — only the build tooling needs .NET 10.

The plugin's csproj resolves the library via a `DtddClientPath` MSBuild property (default `../../../dtdd-client`, i.e. checked out as a sibling of this repo's parent directory) and falls back to a `PackageReference` on the [`DtDDNetClient`](https://www.nuget.org/packages/DtDDNetClient) NuGet package if that path doesn't exist. Pass `-p:DtddClientPath=<path>` to point at the library elsewhere — e.g. from a worktree, where the default relative path won't resolve. The fallback version defaults to `0.1.0` via `DtddClientVersion`; pass `-p:DtddClientVersion=<version>` to pin a different one.

The generated DocFX output committed at `_site/` (69 tracked files) documents pre-migration types that no longer exist, and one page still displays the retired bundled API key. It is stale; regenerate it or add it to `.gitignore` rather than treating it as current.

```bash
# Build
dotnet build Jellyfin.Plugin.DoesTheDogDie.sln

# Publish for deployment
dotnet publish Jellyfin.Plugin.DoesTheDogDie.sln

# Run unit tests only (default, fast, no Docker)
dotnet test --filter "Category!=E2E"

# Run E2E suite (requires Docker; starts WireMock + Jellyfin containers)
dotnet test --filter "Category=E2E"

# Run everything
dotnet test

# Run single test
dotnet test --filter "FullyQualifiedName~DtddApiClientTests.SearchByImdbIdAsync_ValidId_ReturnsResponse"

# Tests with coverage
dotnet test --collect:"XPlat Code Coverage" --filter "Category!=E2E"

# Generate HTML coverage report (requires: dotnet tool install -g dotnet-reportgenerator-globaltool)
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" \
  -targetdir:"TestResults/CoverageReport" -reporttypes:Html
```

## Testing with Jellyfin

### Automated (preferred)

`tests/Jellyfin.Plugin.DoesTheDogDie.E2ETests/` spins up `lscr.io/linuxserver/jellyfin:10.11.8` + `wiremock/wiremock:3.10.0` via Testcontainers, drives the startup wizard, adds NFO-backed fixture libraries, scans, and asserts plugin behavior. DTDD API is stubbed via WireMock — no real traffic.

```bash
dotnet test --filter "Category=E2E"
```

Plugin DLLs are auto-published before the run via a `BeforeTargets="Build"` step in the E2E csproj, so it always tests the current source.

### Manual install against a running Jellyfin

1. `dotnet publish`
2. Copy `Jellyfin.Plugin.DoesTheDogDie/bin/Debug/net9.0/publish/` into `<jellyfin-data>/plugins/DoesTheDogDie_<version>/`
3. Add a `meta.json` (the E2E fixture's copy in `tests/.../Fixtures/JellyfinFixture.cs::WriteMetaJson` is a working reference; `status` must be `"Active"`). The plugin directory is mounted read-write in the E2E container because Jellyfin itself writes `meta.json` into it, not just reads it.
4. Restart Jellyfin server

### Redirecting DTDD calls

`DtddClientProvider.ResolveBaseAddress()` honors the `DTDD_API_BASE_URL` env var and falls back to `https://www.doesthedogdie.com/api/v3/`. The override is used verbatim as the client's base address, so it must include the full `/api/v3/` path (e.g. `http://wiremock:8080/api/v3/`) and a trailing slash. A value that is not an absolute URI is logged and ignored. Used by the E2E harness to point at WireMock; safe to leave unset in production.

## Architecture

### Data Flow

1. **Metadata providers** (`ICustomMetadataProvider<T>`) run after TMDB/TVDB providers (Order=100)
2. Provider calls `DtddMetadataService.ResolveAsync()`, which resolves the item against the `DoesTheDogDie` library's `IDtddClient` in priority order: a DtDD id already stored on the item, then IMDb id, then exact name+year. It never issues a free-text search.
3. The resolved item detail's `TopicItemStats` are joined against the library's topic taxonomy (`IDtddClient.GetTopicsAsync()`) and run through the library's Beta-distribution confidence model to produce a verdict (likely present / likely absent / uncertain) and credible interval per trigger
4. `TriggerFilter` applies user configuration (category/topic allow-lists; `ShowAllTriggers`)
5. `DtddMetadataService.Apply()` writes warnings as tags (e.g., "CW: a dog dies", "Safe: no dogs die") and, if enabled, an `OverviewFormatter`-rendered summary into the item description

### Key Components

| Component | Purpose |
|-----------|---------|
| `Plugin.cs` | Entry point, extends `BasePlugin<PluginConfiguration>`, GUID: `eb5d7894-8eef-4b36-aa6f-5d124e828ce1` |
| `DtddMetadataService` | The plugin's single entry point to DtDD data: resolves an item to triggers and applies them back onto it. Facade over the library's `IDtddClient` |
| `DtddClientProvider` | Owns the `DoesTheDogDie` client stack (HTTP → throttle → cache) and its lifecycle; rebuilds the stack when API key or cache settings change |
| `DtddMovieProvider`, `DtddSeriesProvider` | `ICustomMetadataProvider` implementations that fetch and apply warnings |
| `DtddSeasonProvider`, `DtddEpisodeProvider` | Inherit DTDD ID and warnings from parent Series |
| `TriggerFilter` | Filters triggers by category/topic |
| `DtddLibraryScanService` | `IHostedService` - auto-fetches DTDD data when items with IMDB IDs are added |
| `DtddRefreshTask` | `IScheduledTask` - daily refresh at 2 AM |

### Configuration Options (`PluginConfiguration`)

- `EnableMovies`/`EnableSeries` - Enable per media type (both true by default)
- `AddWarningTags` - Add `CW:`/`Safe:` tags to items (default: true)
- `TagPrefix`/`SafeTagPrefix` - Tag prefixes (default: "CW:", "Safe:")
- `ShowConfidenceInTags` - Append confidence percent to tag names (default: false)
- `ShowAllTriggers` - Master switch; when false, uses category/topic filtering
- `EnabledCategoryIds`/`EnabledTopicIds` - Filter to specific triggers
- `AddDescriptionWarnings` - Inject a grouped trigger summary into the overview (default: false)
- `IncludeTopComment` - Include each trigger's top comment; costs one extra API request per title (default: false)
- `MaxCommentLength` - Truncate included comments (default: 200)
- `ApiKey` - The user's DoesTheDogDie API key, sent as `X-API-KEY` (required; no default)
- `DecisionThreshold` - Probability threshold for a trigger's verdict (default: 0.5)
- `IntervalMass` - Probability mass covered by the reported credible interval (default: 0.95)
- `ItemCacheDays` - How long cached item/rating data stays fresh (default: 30)
- `TaxonomyCacheDays` - How long cached topic/category data stays fresh (default: 7)

Removed in the v3 migration: `MinVotesThreshold`, `EnableBooks`, `CacheDurationHours`, `RefreshIntervalHours`, `UseConfidenceScoring`, `MinConfidenceThreshold`, `HideSpoilerComments`.

### DoesTheDogDie API

The plugin no longer talks HTTP to DtDD directly — all of this is inside the `DoesTheDogDie` client library (`../../dtdd-client`, referenced via `DtddClientPath`). Base URL: `https://www.doesthedogdie.com/api/v3`.

Endpoints the library exposes via `IDtddClient` (all GET, `X-API-KEY` header):
- `/items?imdb={id}` / `?name={n}&releaseYear={y}` / `?q={term}` - Search (the plugin only ever uses the first two forms; see Data Flow)
- `/items/{itemId}` - Item detail including `topicItemStats[]`
- `/items/{itemId}/ratings` - Per-item comments/ratings (used for `IncludeTopComment`)
- `/topics`, `/itemtypes`, `/topiccategories`, `/topicsupercategories` - Taxonomy

See `../../dtdd-client/CLAUDE.md` for the authoritative v3 reference (models, error codes, rate-limit headers). The library's `DtddApiClient` handles the Content-Type/HTML-vs-JSON distinction internally; the plugin never sees it.

## Code Style

- .NET 9.0, nullable reference types enabled
- StyleCop/Roslyn analyzers enforced (`jellyfin.ruleset`), warnings as errors
- Private fields: `_camelCase`
- XML docs required for public members
- Bootstrap code (`Plugin.cs`, `PluginServiceRegistrator.cs`) marked `[ExcludeFromCodeCoverage]`

## Testing Patterns

`IDtddClient` is a real interface from the library; the plugin's test double is `tests/Jellyfin.Plugin.DoesTheDogDie.Tests/Support/FakeDtddClient.cs`, not a mock. `DtddMetadataService`'s methods (`ResolveAsync`, `Apply`) remain `virtual` for Moq where a test wants to stub the service itself rather than drive it through a fake client. Use `IPluginConfigurationAccessor` for config mocking:

```csharp
_configAccessorMock.Setup(x => x.GetConfiguration())
    .Returns(new PluginConfiguration { EnableMovies = true, ApiKey = "test-key" });
```

### Known Test Limitation

Season/Episode providers get IMDB ID from parent Series via `item.Series.GetProviderId()`. The `Series` property is null in unit tests (no public setter), so only the "no parent series" path is testable (~44% coverage on these providers). E2E tests cover the real inheritance path against a running Jellyfin.

### E2E Internals

- **Fixture:** `tests/Jellyfin.Plugin.DoesTheDogDie.E2ETests/Fixtures/JellyfinFixture.cs` — Testcontainers network + WireMock + Jellyfin LSIO containers, runs startup wizard, adds libraries, triggers initial scan.
- **REST wrapper:** `JellyfinClient.cs` — wraps wizard, login, library mgmt, plugin config, refresh + poll. Retries 5xx during wizard window. Authenticates via `MediaBrowser` auth header.
- **Stubs:** `Stubs/wiremock-mappings/*.json` — canned DtDD **v3** responses under `/api/v3/*` (`items`, `items/{id}`, `items/{id}/ratings`, `topics`, `topiccategories`, `topicsupercategories`, `itemtypes`) for known IMDB IDs (`tt2911666`, `tt0903747`).
- **Fixture media:** `Fixtures/media/{movies,tv}/` — minimal NFO-only tree (stub `.mkv` files); IMDB IDs match the WireMock stubs.
- **meta.json:** must include `"status": "Active"` and live at `/config/data/plugins/<Name>_<Version>/`. Without `Active`, `PluginManager` treats the plugin as Disabled and silently skips loading it.

## Implementation Status

Phases 0-4 complete (core infrastructure, metadata providers, background services, UI integration: `IExternalId`, `IExternalUrlProvider`, real config page). Description injection (`OverviewFormatter`) added on `feature/description-injection`. Automated E2E harness in place. Migrated the homegrown DTDD API layer onto the external `DoesTheDogDie` client library (v3 API, `feature/dtdd-client-migration`): 173/173 unit tests, 47/47 E2E tests passing. The plugin is not yet releasable — see `docs/PROGRESS.md`.

## Documentation

- [docs/PROGRESS.md](./docs/PROGRESS.md) - Implementation status and test coverage
- [docs/API_DOCUMENTATION.md](./docs/API_DOCUMENTATION.md) - DTDD API reference with response schemas
- [docs/IMPLEMENTATION_PLAN.md](./docs/IMPLEMENTATION_PLAN.md) - Original architecture decisions
- [docs/TESTING.md](./docs/TESTING.md) - Testing guide and patterns
