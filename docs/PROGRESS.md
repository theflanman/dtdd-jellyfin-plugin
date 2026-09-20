# DoesTheDogDie Plugin - Progress Tracker

**Current release:** [0.2.0.0](https://github.com/theflanman/dtdd-jellyfin-plugin/releases/tag/0.2.0.0),
published 2026-09-19. `main` and `development` are level with it.

The plugin ships. The gh-pages manifest advertises 0.2.0.0 against `targetAbi 10.11.0.0`, and release
builds restore cleanly in CI: the `DoesTheDogDie` client library is published to NuGet as
[`DtDDNetClient`](https://www.nuget.org/packages/DtDDNetClient) `0.1.0`, so the csproj's
`PackageReference` fallback resolves without a checked-out sibling repo. The `DtddClientPath`
`ProjectReference` is now a local-development convenience, not a release blocker.

## Implementation Phases

| Phase | Description | Status | Notes |
|-------|-------------|--------|-------|
| **Phase 0** | API reverse engineering | ✅ Complete | Superseded by the v3 client library |
| **Phase 1** | Core infrastructure | ✅ Complete | Plugin, config, API client |
| **Phase 1.5** | API client unit tests | ✅ Complete | |
| **Phase 2** | Metadata providers | ✅ Complete | Movie, Series, Season, Episode |
| **Phase 2.5** | Provider tests | ✅ Complete | |
| **Phase 3** | Background services | ✅ Complete | `IHostedService`, `IScheduledTask` |
| **Phase 4** | UI integration | ✅ Complete | `IExternalId`, `IExternalUrlProvider`, real `configPage.html` |
| **Phase 5** | Description injection | ✅ Complete | `OverviewFormatter` + config flags (PR #33) |
| **Phase 6** | Automated E2E suite | ✅ Complete | xUnit + Testcontainers + WireMock |
| **Phase 7** | DoesTheDogDie v3 client library migration | ✅ Complete | Homegrown API layer replaced by the external `DoesTheDogDie` library; v3 API, user-supplied API key, verdict-driven tags via a Beta-distribution confidence model, grouped descriptions with credible intervals, mandatory attribution, optional comment injection |
| **Phase 8** | Release 0.2.0.0 | ✅ Complete | NuGet fallback, published manifest, release assets |

---

## Test Coverage

**Last measured:** 2026-09-20, on `main` at 5593674.

| Metric | Value |
|--------|-------|
| Unit tests | 175 passing, 0 failing |
| E2E tests | 47 passing, 0 failing (Testcontainers; Docker required) |
| Line coverage — plugin assembly | 92.6% |
| Branch coverage — plugin assembly | 81.6% |

**Reading the coverage number:** when `DtddClientPath` resolves, the client library builds as a
`ProjectReference` and lands in the same coverage run. The combined figure
(41.2% line / 44.7% branch) is diluted by the library's own code, which this repo's tests barely
touch and which has its own suite. Report the `Jellyfin.Plugin.DoesTheDogDie` package figure above,
not the run total:

```bash
python3 -c "
import xml.etree.ElementTree as ET, glob, os
f=sorted(glob.glob('tests/**/coverage.cobertura.xml', recursive=True), key=os.path.getmtime)[-1]
for p in ET.parse(f).getroot().iter('package'):
    print(p.get('name'), round(float(p.get('line-rate'))*100,1), round(float(p.get('branch-rate'))*100,1))
"
```

### Coverage by Component

| Component | Line coverage | Notes |
|-----------|---------------|-------|
| `DtddMetadataService` | 100% (76-100% on async state machines) | |
| `DtddMovieProvider` / `DtddSeriesProvider` | 100% | |
| `DtddSeasonProvider` / `DtddEpisodeProvider` | 100% (91% on `FetchAsync`) | See note below |
| `DtddLibraryScanService` | 100% (92% on `ProcessItemAsync`) | |
| `DtddRefreshTask` | 100% (88% on `RefreshItemAsync`) | |
| `DtddClientProvider` | 83% | `UnconfiguredApiClient` at 25%, `DisposeAsync` at 0% |
| `OverviewFormatter` | 94% | |
| `TriggerFilter` / `TriggerTagFormatter` / `LogSanitizer` | 100% | |
| `DtddPluginController` | 100% | |
| `Plugin.cs`, `PluginServiceRegistrator` | Excluded | Bootstrap code, `[ExcludeFromCodeCoverage]`; E2E exercises it in a real container |

**On the Season/Episode providers:** earlier revisions of this document recorded ~44% coverage on
these, because `item.Series` has no public setter and the parent-series inheritance path could not be
reached from unit tests. That is no longer the shape of the numbers — both sit at 100% line coverage
with 91% on `FetchAsync`. The untestable branch is a small remainder, and E2E still covers real
parent-series inheritance against a running Jellyfin.

---

## What's Working

- [x] Item resolution against DtDD in priority order: stored DtDD id → IMDb id → exact name+year (never free-text search)
- [x] `DTDD_API_BASE_URL` env override (used by E2E to redirect to WireMock)
- [x] Movie and Series metadata providers add DTDD ID and warning tags
- [x] Season/Episode providers inherit from parent series
- [x] Warning tags assert only confident verdicts; uncertain triggers get no tag
- [x] Stale tag removal — every refresh strips all `CW:`/`Safe:`-prefixed tags before rewriting the current set
- [x] Description injection (Overview field) with marker-bounded section + locked-field respect
- [x] Optional top-comment injection per trigger
- [x] Background library scan service (auto-fetch for new items with IMDB IDs)
- [x] Scheduled refresh task (daily at 2 AM)
- [x] External ID display and external URL links (`IExternalId`, `IExternalUrlProvider`)
- [x] Real configuration page: API key + test button, live topic/category picker, budget readout, description-injection controls
- [x] Persistent SQLite cache on Windows, Linux and macOS, with an in-memory fallback when the cache file cannot be opened
- [x] Automated E2E harness (Testcontainers + WireMock)

---

## Open Work

Tracked on GitHub. The two near-term milestones:

- **[Milestone 8: 0.2.1 Post-Release Polish](https://github.com/theflanman/dtdd-jellyfin-plugin/milestone/8)**
  — #36 orphaned tags when `AddWarningTags` is disabled, #37 config page formatting, #38 documentation
  corrections.
- **[Milestone 9: Jellyfin 12 Support](https://github.com/theflanman/dtdd-jellyfin-plugin/milestone/9)**
  — #39 multi-target `net9.0;net10.0` (keeping 10.11), #40 `CA1873`, #41 two-artifact release, #42 E2E
  against 12.x.

Not yet implemented, unmilestoned:

- **#43 — budget-aware library scanning.** The client library's Phase 5 (prioritized work queue,
  budget forecasting) is unbuilt. Lookups are issued in arrival order with no forecasting against
  DtDD's 5,000/month free tier, so a first scan of a real library can exhaust the month. The migration
  design spec names this a prerequisite for real-sized libraries; 0.2.0 shipped without it.
- **Live (non-mocked) DTDD smoke test in CI** to catch upstream schema drift. WireMock stubs cannot
  detect a JSON shape change at DtDD.

Longer horizon: milestones 3 (per-user preferences), 4 (rating fields alongside tags), 5 (auto-play
interruption), 6 (user filtering) and 7 (poster overlays). #11, #18, #19 and #26 across three of those
share one unsolved prerequisite — how this plugin injects UI/JS into the Jellyfin web client.

---

## Known Issues / Limitations

| Issue | Impact | Workaround |
|-------|--------|------------|
| Disabling `AddWarningTags` never strips existing tags (#36) | Tags written earlier stay on items permanently | Leave tagging on and clear the topic selection instead |
| Provider short-circuits when a DTDD ID is stored, `ReplaceAllMetadata=false`, and both `AddWarningTags`/`AddDescriptionWarnings` are off | Nothing re-applied on partial refresh | Enable either option, or refresh with `replaceAllMetadata=true` |
| No budget prioritization (#43) | A first scan of a large library can exhaust the monthly API budget | Filter to fewer categories/topics; leave `IncludeTopComment` off |
| Category IDs from 0.1.x don't carry forward | A stale selection deletes existing tags on first refresh | Clear and re-select after upgrading — see the 0.2.0.0 release notes |
| Plugin `meta.json` status defaults to Disabled | Won't load without explicit `"status": "Active"` | E2E fixture writes a correct meta.json automatically |
| `Plugin.cs` requires the Jellyfin runtime | 0% unit coverage | Excluded from coverage; E2E exercises it in the real container |

---

## Commands

All dotnet commands need the .NET 10 SDK on `PATH` — see [CLAUDE.md](../CLAUDE.md):

```bash
export PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet DOTNET_ROLL_FORWARD=LatestMajor

# Build
dotnet build Jellyfin.Plugin.DoesTheDogDie.sln

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
