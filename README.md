# Does The Dog Die - Jellyfin Plugin (Unofficial)

![Build Status](https://img.shields.io/badge/build-passing-brightgreen)
![Coverage](https://img.shields.io/badge/coverage-77%25-yellow)
![License](https://img.shields.io/badge/license-GPLv3-blue)
![Jellyfin](https://img.shields.io/badge/Jellyfin-10.11+-purple)

An unofficial Jellyfin plugin that integrates content warnings from [DoesTheDogDie.com](https://www.doesthedogdie.com) into your media library.

> **Note:** This plugin is not affiliated with or endorsed by DoesTheDogDie.com or Jellyfin.

## What It Does

This plugin automatically fetches trigger warnings for movies and TV shows and adds them as tags to your Jellyfin library. It helps viewers make informed decisions about content that might be distressing—whether that's animal death, jump scares, violence, or dozens of other potential triggers.

**Example tags added to a movie:**
- `CW: a dog dies`
- `CW: there are jump scares`
- `Safe: a cat dies` (confirmed safe)

## How It Works

1. When you refresh metadata or add new items to your library, the plugin resolves each movie/series against DoesTheDogDie.com: it tries a DtDD id already stored on the item first, then an IMDb ID lookup, then an exact name+year lookup. It never issues a free-text search.
2. It retrieves community-voted trigger data (e.g., "Does the dog die?" → 1,336 yes / 118 no) and folds it into a Beta-distribution confidence model, producing a verdict (likely present / likely absent / uncertain) and a credible interval per trigger.
3. Based on that verdict, it adds warning tags (`CW:`) or safe tags (`Safe:`) to the item — see "Tags assert only confident findings" below.
4. Tags appear in Jellyfin's UI and can be used for filtering/searching; optionally, a grouped summary with vote counts and intervals is appended to the item's description.

## Features

- **Automatic lookups** - Background service detects new library items and fetches warnings
- **Scheduled refresh** - Daily task keeps trigger data up to date
- **Hierarchical filtering** - Choose which trigger categories and specific topics to track
- **Confidence-based verdicts** - A Beta-distribution model turns raw vote counts into likely-present / likely-absent / uncertain, with a reported credible interval
- **Safe confirmations** - Optionally show when content is confirmed *safe* for specific triggers
- **External links** - Quick links to DoesTheDogDie.com pages from item details
- **Local caching** - SQLite-backed cache keeps repeat lookups off the DtDD monthly request budget

## Installation

### From Release

1. Download the latest release from the [Releases page](https://github.com/theflanman/dtdd-jellyfin-plugin/releases)
2. Extract to your Jellyfin plugins directory:
   - **Linux:** `~/.local/share/jellyfin/plugins/DoesTheDogDie/`
   - **Windows:** `%LOCALAPPDATA%\jellyfin\plugins\DoesTheDogDie\`
   - **Docker:** `<jellyfin data dir>/plugins/DoesTheDogDie_<version>/`, where `<jellyfin data dir>` is whichever directory the image passes to Jellyfin as `--datadir`/`JELLYFIN_DATA_DIR` — **this is genuinely different per image, not just a mount-point naming difference:**
     - `jellyfin/jellyfin` (official image): `/config/plugins/DoesTheDogDie_<version>/`
     - `lscr.io/linuxserver/jellyfin`: `/config/data/plugins/DoesTheDogDie_<version>/`
     - `hotio/jellyfin`: `/config/data/plugins/DoesTheDogDie_<version>/`
     - Other images: check what that image sets for `--datadir`/`JELLYFIN_DATA_DIR` (see [jellyfin/jellyfin#3717](https://github.com/jellyfin/jellyfin/issues/3717) for background on why official and linuxserver.io diverge here)

     A plugin dropped in the wrong directory is silently never loaded, with no error in the log.
3. Restart Jellyfin
4. Configure in **Dashboard → Plugins → Does The Dog Die**

### From Source

```bash
git clone https://github.com/theflanman/dtdd-jellyfin-plugin.git
cd dtdd-jellyfin-plugin
dotnet publish Jellyfin.Plugin.DoesTheDogDie.sln -c Release
```

Copy the contents of `Jellyfin.Plugin.DoesTheDogDie/bin/Release/net9.0/publish/` to your plugins directory.

## How the plugin behaves

### 1. An API key is required

You need a free DoesTheDogDie.com account and its API key (from your profile page). Paste it into **Dashboard → Plugins → Does The Dog Die → API Key**. It is stored in Jellyfin's plugin configuration XML in **plaintext**, like any other Jellyfin plugin setting. With no key configured, the plugin makes no network calls and adds no tags or descriptions — it silently no-ops rather than hammering the API with unauthenticated requests.

### 2. The plugin owns its tag namespace

Tags beginning with the configured prefixes (`CW:` and `Safe:` by default) belong to the plugin. On every refresh it removes *every* tag with those prefixes before writing the current set — so a hand-written tag that happens to start with `CW:` will be silently removed on the next scan. If that's a problem, change `Tag Prefix` / `Safe Tag Prefix` to something you don't otherwise use.

**Turning the two injection settings off behaves differently, on purpose.** The tag sweep is gated on *Add Warning Tags*: with it off the plugin does not touch tags at all, so any `CW:`/`Safe:` tags it wrote previously stay on your items permanently until you remove them yourself (or turn the setting back on and let one refresh rewrite them). *Add Description Warnings* is the opposite — with it off, the next refresh actively strips the injected description block back out. This asymmetry is deliberate and covered by a test: descriptions are one block the plugin owns end to end and can safely reclaim, whereas tags live in a namespace shared with whatever else you or other plugins have tagged, and a disabled plugin deleting tags in bulk is the more destructive default.

### 3. Tags assert only confident findings

`CW:` means the trigger is *likely present*. `Safe:` means it is *likely absent*. A trigger the DtDD data is uncertain about gets no tag at all — it is silently omitted, not tagged either way. This is deliberate:

- Filtering **against** `CW: a dog dies` still surfaces uncertain titles (they just weren't excluded).
- Searching **for** `Safe: a dog dies` will not surface uncertain titles — `Safe:` is a strong claim, not "nobody complained."

### 4. Description format

When **Add Description Warnings** is enabled, a section like this is appended to the item's overview:

```
Content warnings: a dog dies (18/20, 72–96%)
Reported safe: someone smokes (2/15, 4–28%)

Powered by DoesTheDogDie.com — https://www.doesthedogdie.com
```

`(18/20, 72–96%)` means 18 of 20 votes said the trigger is present, with a 95% credible interval of 72–96% for the true rate. The threshold used to sort a trigger into "Content warnings" / "Possible" / "Reported safe", and the width of the interval, are both configurable (`Decision Threshold`, `Interval Mass`).

### 5. Attribution

"Powered by DoesTheDogDie.com", linked to doesthedogdie.com, appears in every injected description and on the plugin's configuration page. This is not optional — it is required by DoesTheDogDie's terms of service for any view that displays their data, and the plugin cannot be configured to remove it.

### 6. Comment injection costs an extra request per title

Enabling **Include Top Comment** fetches each item's ratings (to pull the top user comment per trigger) as a *second* API request per title, against DtDD's free-tier budget of 5,000 requests/month. It is **off by default**. The plugin's cache avoids repeating this cost for items that don't need re-checking, but the first full library scan still pays it once per title if enabled.

### 7. `HideSpoilerComments` has been removed

The DoesTheDogDie v3 API exposes no "is this a spoiler" flag on any model it returns, so this setting could not be honored — it has been removed rather than kept as a control that silently does nothing.

### 8. Upgrading? Re-select your category filters

**If you used *Enabled Category IDs* before this release, clear and re-select them.**

DoesTheDogDie restructured its topic categories in v3. The old API grouped topics into a handful of coarse
categories; v3 has 43, and topics moved between them — "a dog dies", for example, was category 2 ("Animal")
and is now category 56. Your saved selection is a list of *old* category IDs, and the plugin now compares it
against the *new* numbering, so it will match the wrong categories.

The plugin does not reset the setting for you, and the consequence is worse than just missing new warnings:
because every refresh removes *all* `CW:`/`Safe:` tags before rewriting the current set (see §2), a stale
category selection that now matches nothing means your existing content-warning tags are **deleted** on the
first refresh after upgrading, not merely left as they were. Open **Dashboard → Plugins →
Does The Dog Die**, clear the category selection, and pick again.

Two related settings are unaffected: *Enabled Topic IDs* still means exactly what you chose (topic IDs did not
change), and stored DtDD item IDs still resolve, so no re-scan is needed.

## Configuration

| Setting | Description | Default |
|---------|-------------|---------|
| Enable Movies/Series | Toggle which media types to process | `true` |
| Add Warning Tags | Add `CW:` / `Safe:` tags to items | `true` |
| Tag Prefix | Customize warning tag prefix | `CW:` |
| Safe Tag Prefix | Customize safe tag prefix | `Safe:` |
| Show Confidence In Tags | Append confidence to tag names, e.g. `CW: a dog dies (95%)` | `false` |
| Show All Triggers | Master switch; when off, uses the category/topic filtering below | `false` |
| Enabled Category IDs / Enabled Topic IDs | Restrict tagging/description to specific DtDD categories or topics. **Upgrading: re-select categories — see above** | none (all) |
| Add Description Warnings | Inject a grouped trigger summary into the item overview | `false` |
| Include Top Comment | Include each trigger's top user comment (costs one extra API request per title) | `false` |
| Max Comment Length | Truncate included comments to this many characters | `200` |
| API Key | Your DoesTheDogDie.com API key (required; stored in plaintext) | *(empty)* |
| Decision Threshold | Probability threshold used to decide a trigger's verdict | `0.5` |
| Interval Mass | Probability mass covered by the reported credible interval | `0.95` |
| Item Cache Days | How long cached item/rating data stays fresh | `30` |
| Taxonomy Cache Days | How long cached topic/category data stays fresh | `7` |

Removed in the v3 migration: `Min Votes Threshold`, `Enable Books`, `Cache Duration Hours`, `Refresh Interval Hours`, `Use Confidence Scoring`, `Min Confidence Threshold`, `Hide Spoiler Comments`.

## Requirements

- Jellyfin Server 10.11.0 or later
- **The .NET 10 SDK** to build from source (the plugin still targets and ships `net9.0`; the SDK is required because restore evaluates every target framework of the `DoesTheDogDie` library it depends on, which multi-targets `net9.0;net10.0`). See [CLAUDE.md](CLAUDE.md) for the exact build environment.
- **A DoesTheDogDie.com API key** — see "How the plugin behaves" above. Nothing is tagged without one.

### A packaging note: native SQLite libraries are stripped at publish

Jellyfin's `PluginManager` loads every `.dll` it finds recursively under a plugin's directory as a managed
assembly, including native ones, and throws `BadImageFormatException` on them — which disables the whole
plugin. To avoid that, the plugin's publish step removes bundled native libraries matching
`runtimes/*/native/*.dll`. Only `.dll` files are removed; `.so` and `.dylib` payloads are never scanned by
`PluginManager`, so they are left in place.

This does not cost you the cache. The plugin opens its SQLite cache file on Windows, Linux and macOS alike,
and cached DtDD data survives a Jellyfin restart on all three. If the cache file genuinely cannot be opened
— a read-only or missing data directory, a permissions problem, no usable SQLite provider on the host — the
plugin logs a warning and falls back to an in-memory cache. It keeps working, but cached data is lost on
restart and every restart re-spends API budget re-fetching previously-seen items. Search your Jellyfin log
for `falling back to an in-memory cache` if you suspect this.

### Containerized deployments: make sure the cache directory is on a persistent volume

The cache file lives at `<jellyfin data dir>/plugins/Jellyfin.Plugin.DoesTheDogDie/dtdd-cache.db` (plus its `-wal`/`-shm` journal files) — note this is keyed by the plugin's *name*, not its `Name_<version>` install directory, so it's a separate location from the plugin binaries. `<jellyfin data dir>` is the same per-image `--datadir`/`JELLYFIN_DATA_DIR` path from the Installation section above (`/config` on official `jellyfin/jellyfin`, `/config/data` on linuxserver.io and hotio). It has no dedicated volume of its own; it persists automatically as long as that directory — via whatever volume it happens to fall under, typically the same `/config` mount used for everything else — is a bind mount or named volume.

If only the plugin's own install directory is bind-mounted (e.g. for iterating on a build) while the Jellyfin data directory itself is not — or if it isn't persisted at all — the cache database is created fresh inside the container's ephemeral layer. It survives a plain `docker restart` (the writable layer isn't touched), but is silently lost on `docker rm` / container recreation / `docker-compose down` / an image update that replaces the container, and every previously-cached item re-spends DtDD API budget being re-fetched on the next scan. There is no warning when this happens — check that your Jellyfin data directory maps to a bind mount or named volume in your compose file or `docker run` command.

## Development

### Build

```bash
dotnet build
```

### Test

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML coverage report
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"TestResults/CoverageReport" -reporttypes:Html
```

### Project Structure

```
Jellyfin.Plugin.DoesTheDogDie/
├── Api/
│   ├── DtddPluginController.cs   # REST API endpoints (incl. TestKey)
│   ├── KeyTestResponse.cs
│   └── TaxonomyResponse.cs
├── Configuration/
│   └── PluginConfiguration.cs    # Plugin settings
├── Providers/
│   ├── DtddMovieProvider.cs      # Movie metadata provider
│   ├── DtddSeriesProvider.cs     # Series metadata provider
│   ├── DtddSeasonProvider.cs     # Season metadata provider
│   └── DtddEpisodeProvider.cs    # Episode metadata provider
├── Services/
│   ├── DtddClientProvider.cs     # Owns the DoesTheDogDie library client stack + its lifecycle
│   ├── DtddMetadataService.cs    # Resolves an item to DtDD data (facade over IDtddClient) and applies it
│   ├── DtddLibraryScanService.cs # Background scan service
│   └── OverviewFormatter.cs      # Renders the grouped description summary
├── ScheduledTasks/
│   └── DtddRefreshTask.cs        # Daily refresh task
├── TriggerFilter.cs              # Trigger filtering logic
├── TriggerTagFormatter.cs        # Tag name formatting
├── Constants.cs                  # Plugin constants
└── Plugin.cs                     # Plugin entry point
```

DoesTheDogDie API access (HTTP, throttling, caching, confidence math) lives in the separate [`DoesTheDogDie`](../../dtdd-client) client library, referenced as a project reference during development (see [CLAUDE.md](CLAUDE.md)).

## Setting Up Dynamic Coverage Badges

To display live coverage metrics, set up CI/CD integration with a coverage service.

### Option 1: Codecov (Recommended)

1. **Sign up** at [codecov.io](https://codecov.io) and link your GitHub repository

2. **Add GitHub Actions workflow** (`.github/workflows/ci.yml`):

   ```yaml
   name: CI

   on:
     push:
       branches: [main, development]
     pull_request:

   jobs:
     build-and-test:
       runs-on: ubuntu-latest

       steps:
         - uses: actions/checkout@v4

         - name: Setup .NET
           uses: actions/setup-dotnet@v4
           with:
             dotnet-version: '9.0.x'

         - name: Restore dependencies
           run: dotnet restore

         - name: Build
           run: dotnet build --no-restore

         - name: Test with coverage
           run: dotnet test --no-build --collect:"XPlat Code Coverage"

         - name: Upload coverage to Codecov
           uses: codecov/codecov-action@v4
           with:
             token: ${{ secrets.CODECOV_TOKEN }}
             files: '**/coverage.cobertura.xml'
             fail_ci_if_error: true
   ```

3. **Add Codecov token** to repository secrets (Settings → Secrets → Actions)

4. **Update README badge**:
   ```markdown
   ![Coverage](https://codecov.io/gh/theflanman/dtdd-jellyfin-plugin/graph/badge.svg?branch=main)
   ```

### Option 2: Coveralls

1. **Sign up** at [coveralls.io](https://coveralls.io) and enable your repository

2. **Use the same workflow** but replace the Codecov step with:

   ```yaml
   - name: Upload coverage to Coveralls
     uses: coverallsapp/github-action@v2
     with:
       github-token: ${{ secrets.GITHUB_TOKEN }}
       files: '**/coverage.cobertura.xml'
   ```

3. **Update README badge**:
   ```markdown
   ![Coverage](https://coveralls.io/repos/github/theflanman/dtdd-jellyfin-plugin/badge.svg?branch=main)
   ```

## API Documentation

See [docs/API_DOCUMENTATION.md](docs/API_DOCUMENTATION.md) for DoesTheDogDie API reference.

## License

GPLv3 - See [LICENSE](LICENSE) for details.

## Acknowledgments

- [DoesTheDogDie.com](https://www.doesthedogdie.com) for providing the content warning data
- [Jellyfin](https://jellyfin.org) for the media server platform
