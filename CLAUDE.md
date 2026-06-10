# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Jellyfin plugin that integrates DoesTheDogDie.com content warnings into media libraries. Fetches trigger warnings (animal death, violence, jump scares, etc.) and adds them as metadata tags to movies and TV shows.

## Build Commands

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
3. Add a `meta.json` (the E2E fixture's copy in `tests/.../Fixtures/JellyfinFixture.cs::WriteMetaJson` is a working reference; `status` must be `"Active"`)
4. Restart Jellyfin server

### Redirecting DTDD calls

`Constants.ApiBaseUrl` honors the `DTDD_API_BASE_URL` env var and falls back to `https://www.doesthedogdie.com`. Used by the E2E harness to point at WireMock; safe to leave unset in production.

## Architecture

### Data Flow

1. **Metadata providers** (`ICustomMetadataProvider<T>`) run after TMDB/TVDB providers (Order=100)
2. Provider gets IMDB ID from item, calls `DtddApiClient.GetMediaDetailsByImdbIdAsync()`
3. API client searches DTDD by IMDB ID, then fetches full media details with triggers
4. `TriggerFilter` applies user configuration (categories, topics, vote threshold)
5. Warnings added as tags (e.g., "CW: Animal Death", "Safe: No Dogs Die")

### Key Components

| Component | Purpose |
|-----------|---------|
| `Plugin.cs` | Entry point, extends `BasePlugin<PluginConfiguration>`, GUID: `eb5d7894-8eef-4b36-aa6f-5d124e828ce1` |
| `DtddApiClient` | HTTP client for DTDD API (search + media details) |
| `DtddMovieProvider`, `DtddSeriesProvider` | `ICustomMetadataProvider` implementations that fetch and apply warnings |
| `DtddSeasonProvider`, `DtddEpisodeProvider` | Inherit DTDD ID and warnings from parent Series |
| `TriggerFilter` | Filters triggers by category/topic/vote threshold |
| `DtddLibraryScanService` | `IHostedService` - auto-fetches DTDD data when items with IMDB IDs are added |
| `DtddRefreshTask` | `IScheduledTask` - daily refresh at 2 AM |
| `TriggerCacheService` | Caches trigger categories/topics from API |

### Configuration Options (`PluginConfiguration`)

- `EnableMovies/EnableSeries/EnableBooks` - Enable per media type (all true by default)
- `MinVotesThreshold` - Minimum votes to include a trigger (default: 3)
- `TagPrefix`/`SafeTagPrefix` - Tag prefixes (default: "CW:", "Safe:")
- `ShowAllTriggers` - Master switch; when false, uses category/topic filtering
- `EnabledCategoryIds`/`EnabledTopicIds` - Filter to specific triggers

### DoesTheDogDie API

Base URL: `https://www.doesthedogdie.com`

Key endpoints:
- `/dddsearch?imdb={id}` - Search by IMDB ID (preferred)
- `/dddsearch?q={term}` - Search by title
- `/media/{id}` - Get trigger data with vote counts

Headers: `Accept: application/json`, `X-API-KEY: {key}`

**Important:** Invalid media IDs return HTML (not JSON), so `DtddApiClient.GetMediaDetailsAsync()` checks Content-Type header.

## Code Style

- .NET 9.0, nullable reference types enabled
- StyleCop/Roslyn analyzers enforced (`jellyfin.ruleset`), warnings as errors
- Private fields: `_camelCase`
- XML docs required for public members
- Bootstrap code (`Plugin.cs`, `PluginServiceRegistrator.cs`) marked `[ExcludeFromCodeCoverage]`

## Testing Patterns

API client methods are `virtual` for mocking. Use `IPluginConfigurationAccessor` for config mocking:

```csharp
_apiClientMock.Setup(x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()))
    .ReturnsAsync(details);

_configAccessorMock.Setup(x => x.GetConfiguration())
    .Returns(new PluginConfiguration { EnableMovies = true });
```

### Known Test Limitation

Season/Episode providers get IMDB ID from parent Series via `item.Series.GetProviderId()`. The `Series` property is null in unit tests (no public setter), so only the "no parent series" path is testable (~44% coverage on these providers). E2E tests cover the real inheritance path against a running Jellyfin.

### E2E Internals

- **Fixture:** `tests/Jellyfin.Plugin.DoesTheDogDie.E2ETests/Fixtures/JellyfinFixture.cs` — Testcontainers network + WireMock + Jellyfin LSIO containers, runs startup wizard, adds libraries, triggers initial scan.
- **REST wrapper:** `JellyfinClient.cs` — wraps wizard, login, library mgmt, plugin config, refresh + poll. Retries 5xx during wizard window. Authenticates via `MediaBrowser` auth header.
- **Stubs:** `Stubs/wiremock-mappings/*.json` — canned `DtddSearchResponse` / `DtddMediaDetails` for known IMDB IDs (`tt2911666`, `tt0903747`).
- **Fixture media:** `Fixtures/media/{movies,tv}/` — minimal NFO-only tree (stub `.mkv` files); IMDB IDs match the WireMock stubs.
- **Refresh gotcha:** `DtddMovieProvider` / `DtddSeriesProvider` skip work when a `Dtdd` ProviderId is already set unless the refresh sets `replaceAllMetadata=true`. Mutation tests must pass that flag to see config-driven changes.
- **meta.json:** must include `"status": "Active"` and live at `/config/data/plugins/<Name>_<Version>/`. Without `Active`, `PluginManager` treats the plugin as Disabled and silently skips loading it.

## Implementation Status

Phases 0-4 complete (core infrastructure, metadata providers, background services, UI integration: `IExternalId`, `IExternalUrlProvider`, real config page). Description injection (`OverviewFormatter`) added on `feature/description-injection`. Automated E2E harness in place.

## Documentation

- [docs/PROGRESS.md](./docs/PROGRESS.md) - Implementation status and test coverage
- [docs/API_DOCUMENTATION.md](./docs/API_DOCUMENTATION.md) - DTDD API reference with response schemas
- [docs/IMPLEMENTATION_PLAN.md](./docs/IMPLEMENTATION_PLAN.md) - Original architecture decisions
- [docs/TESTING.md](./docs/TESTING.md) - Testing guide and patterns
