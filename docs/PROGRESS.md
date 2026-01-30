# DoesTheDogDie Plugin - Progress Tracker

## Implementation Phases

| Phase | Description | Status | Notes |
|-------|-------------|--------|-------|
| **Phase 0** | API reverse engineering | ✅ Complete | API documented in `API_DOCUMENTATION.md` |
| **Phase 1** | Core infrastructure | ✅ Complete | Plugin, config, API client |
| **Phase 1.5** | API client unit tests | ✅ Complete | 20 tests |
| **Phase 2** | Metadata providers | ✅ Complete | Movie, Series, Season, Episode |
| **Phase 2.5** | Provider tests | ✅ Complete | 34 tests |
| **Phase 3** | Background services | ❌ Not Started | IHostedService, IScheduledTask |
| **Phase 4** | UI integration | ❌ Not Started | ExternalIds, URLs, config page |

---

## Test Coverage

**Last Updated:** 2026-01-30

| Metric | Value |
|--------|-------|
| Total Tests | 54 |
| Line Coverage | 69.6% |
| Branch Coverage | 68.75% |

### Coverage by Component

| Component | Coverage | Notes |
|-----------|----------|-------|
| DtddApiClient | High | Well tested |
| DtddMovieProvider | High | Well tested |
| DtddSeriesProvider | High | Well tested |
| DtddSeasonProvider | 44% | Parent series lookup untestable |
| DtddEpisodeProvider | 44% | Parent series lookup untestable |
| Plugin.cs | 0% | Excluded - bootstrap code |
| PluginServiceRegistrator | 0% | Excluded - DI registration |

---

## What's Working

- [x] API client fetches data from DoesTheDogDie.com
- [x] Movie metadata provider adds DTDD ID and warning tags
- [x] Series metadata provider adds DTDD ID and warning tags
- [x] Season/Episode providers inherit from parent series
- [x] Configuration options (EnableMovies, EnableSeries, TagPrefix, MinVotesThreshold)
- [x] Warning tags respect vote threshold filtering

---

## What's Not Yet Implemented

- [ ] Background library scan service (auto-fetch for new items)
- [ ] Scheduled refresh task (periodic updates)
- [ ] External ID display in Jellyfin UI
- [ ] External URL links to DTDD website
- [ ] Configuration page HTML
- [ ] Full trigger data cache

---

## Next Steps

1. **Phase 3: Background Services**
   - Implement `DtddLibraryScanService` (IHostedService)
   - Implement `DtddRefreshTask` (IScheduledTask)
   - Add tests for background services

2. **Phase 4: UI Integration**
   - Implement `IExternalId` providers
   - Implement `IExternalUrlProvider`
   - Create configuration HTML page

---

## Known Issues / Blockers

| Issue | Impact | Workaround |
|-------|--------|------------|
| Season/Episode parent lookup untestable | Low coverage | Integration tests needed |
| Plugin.cs requires Jellyfin runtime | 0% coverage | Excluded from coverage |

---

## Commands

```bash
# Build
dotnet build

# Run tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate coverage report (requires ReportGenerator)
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" \
  -targetdir:"TestResults/CoverageReport" -reporttypes:Html
```
