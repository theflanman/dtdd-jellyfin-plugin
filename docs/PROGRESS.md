# DoesTheDogDie Plugin - Progress Tracker

## Implementation Phases

| Phase | Description | Status | Notes |
|-------|-------------|--------|-------|
| **Phase 0** | API reverse engineering | ✅ Complete | API documented in `API_DOCUMENTATION.md` |
| **Phase 1** | Core infrastructure | ✅ Complete | Plugin, config, API client |
| **Phase 1.5** | API client unit tests | ✅ Complete | 20 tests |
| **Phase 2** | Metadata providers | ✅ Complete | Movie, Series, Season, Episode |
| **Phase 2.5** | Provider tests | ✅ Complete | 34 tests |
| **Phase 3** | Background services | ✅ Complete | IHostedService, IScheduledTask |
| **Phase 4** | UI integration | ✅ Complete | `IExternalId`, `IExternalUrlProvider`, real `configPage.html` |
| **Phase 5** | Description injection | ✅ Complete | `OverviewFormatter` + 4 config flags on `feature/description-injection` |
| **Phase 6** | Automated E2E suite | ✅ Complete | xUnit + Testcontainers + WireMock (8 tests) |

---

## Test Coverage

**Last Updated:** 2026-05-10

| Metric | Value |
|--------|-------|
| Unit Tests | 204 |
| E2E Tests | 31 (Testcontainers; Docker required) |
| Line Coverage (unit) | ~75% |
| Branch Coverage (unit) | ~70% |

### Coverage by Component

| Component | Coverage | Notes |
|-----------|----------|-------|
| DtddApiClient | High | Well tested |
| DtddMovieProvider | High | Well tested |
| DtddSeriesProvider | High | Well tested |
| DtddSeasonProvider | 44% (unit) | Parent series lookup untestable in unit tests; E2E covers real inheritance |
| DtddEpisodeProvider | 44% (unit) | Same — E2E covers inheritance |
| DtddLibraryScanService | Partial | Event subscription path tested |
| DtddRefreshTask | High | Properties and triggers tested |
| OverviewFormatter | High | 430-line dedicated test file |
| Plugin.cs | 0% | Excluded - bootstrap code |
| PluginServiceRegistrator | 0% | Excluded - DI registration |

---

## What's Working

- [x] API client fetches data from DoesTheDogDie.com
- [x] `DTDD_API_BASE_URL` env override (used by E2E to redirect to WireMock)
- [x] Movie metadata provider adds DTDD ID and warning tags
- [x] Series metadata provider adds DTDD ID and warning tags
- [x] Season/Episode providers inherit from parent series
- [x] Configuration options (EnableMovies, EnableSeries, TagPrefix, MinVotesThreshold, AddDescriptionWarnings, IncludeTopComment, MaxCommentLength, HideSpoilerComments)
- [x] Warning tags respect vote threshold filtering
- [x] Description injection (Overview field) with marker-bounded section + locked-field respect
- [x] Background library scan service (auto-fetch for new items with IMDB IDs)
- [x] Scheduled refresh task (daily at 2 AM)
- [x] External ID display in Jellyfin UI (`IExternalId`)
- [x] External URL links to DTDD website (`IExternalUrlProvider`)
- [x] Real configuration page (`configPage.html`, ~500 LOC) incl. Description Injection section (`AddDescriptionWarnings`, `IncludeTopComment`, `HideSpoilerComments`, `MaxCommentLength`)
- [x] Automated E2E harness (Testcontainers + WireMock) — verifies real Jellyfin integration without manual setup

---

## What's Not Yet Implemented

- [ ] Full trigger data cache (file-based; current `TriggerCacheService` is in-memory)
- [ ] Live (non-mocked) DTDD smoke test in CI to catch upstream schema drift

---

## Next Steps

1. **Merge `feature/description-injection`** — open PR, fast-forward to `main` (or `development`).
2. **Live integration sanity check** — install the published plugin on the prod-stage Jellyfin once (E2E covers the automated case; this just confirms it lands cleanly on a real install).
3. **(Optional) Add a hybrid live-API smoke test** as a third trait so the harness can guard against DTDD JSON shape changes without paying that cost on every run.

---

## Known Issues / Blockers

| Issue | Impact | Workaround |
|-------|--------|------------|
| Season/Episode parent lookup untestable in unit tests | Low unit coverage on these providers | E2E suite covers real Jellyfin parent-series inheritance |
| Plugin.cs requires Jellyfin runtime | 0% unit coverage | Excluded from coverage; E2E exercises it via the real container |
| `DtddMovieProvider.FetchAsync` short-circuits when ProviderId set + `ReplaceAllMetadata=false` and both `AddWarningTags`/`AddDescriptionWarnings` are off | DTDD ID is kept but nothing is re-applied on partial refresh | Enable either option, or refresh with `replaceAllMetadata=true` |
| Plugin meta.json status defaults to Disabled | Won't load without explicit `"status": "Active"` | E2E fixture writes a correct meta.json automatically |

---

## Commands

```bash
# Build
dotnet build

# Unit tests only (fast, no Docker)
dotnet test --filter "Category!=E2E"

# E2E tests (Testcontainers spawns Jellyfin + WireMock)
dotnet test --filter "Category=E2E"

# Everything
dotnet test

# Unit tests with coverage
dotnet test --collect:"XPlat Code Coverage" --filter "Category!=E2E"

# Generate coverage report (requires ReportGenerator)
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" \
  -targetdir:"TestResults/CoverageReport" -reporttypes:Html
```
