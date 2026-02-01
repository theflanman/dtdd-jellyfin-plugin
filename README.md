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

1. When you refresh metadata or add new items to your library, the plugin looks up each movie/series on DoesTheDogDie.com
2. It searches by IMDB ID first, falling back to title search if needed
3. It retrieves community-voted trigger data (e.g., "Does the dog die?" → 1,336 yes / 118 no)
4. Based on vote thresholds you configure, it adds warning tags (`CW:`) or safe tags (`Safe:`) to the item
5. Tags appear in Jellyfin's UI and can be used for filtering/searching

## Features

- **Automatic lookups** - Background service detects new library items and fetches warnings
- **Scheduled refresh** - Daily task keeps trigger data up to date
- **Hierarchical filtering** - Choose which trigger categories and specific topics to track
- **Configurable thresholds** - Set minimum vote counts to filter out unreliable data
- **Safe confirmations** - Optionally show when content is confirmed *safe* for specific triggers
- **External links** - Quick links to DoesTheDogDie.com pages from item details
- **Title fallback** - Works even without IMDB IDs by searching by title

## Installation

### From Release

1. Download the latest release from the [Releases page](https://github.com/theflanman/dtdd-jellyfin-plugin/releases)
2. Extract to your Jellyfin plugins directory:
   - **Linux:** `~/.local/share/jellyfin/plugins/DoesTheDogDie/`
   - **Windows:** `%LOCALAPPDATA%\jellyfin\plugins\DoesTheDogDie\`
   - **Docker:** `/config/plugins/DoesTheDogDie/`
3. Restart Jellyfin
4. Configure in **Dashboard → Plugins → Does The Dog Die**

### From Source

```bash
git clone https://github.com/theflanman/dtdd-jellyfin-plugin.git
cd dtdd-jellyfin-plugin
dotnet publish Jellyfin.Plugin.DoesTheDogDie.sln -c Release
```

Copy the contents of `Jellyfin.Plugin.DoesTheDogDie/bin/Release/net9.0/publish/` to your plugins directory.

## Configuration

| Setting | Description | Default |
|---------|-------------|---------|
| Enable Movies/Series | Toggle which media types to process | `true` |
| Tag Prefix | Customize warning tag prefix | `CW:` |
| Safe Tag Prefix | Customize safe tag prefix | `Safe:` |
| Min Votes Threshold | Minimum votes required to show a trigger | `3` |
| Trigger Categories | Select which categories to track (Animal, Violence, etc.) | All |

## Requirements

- Jellyfin Server 10.11.0 or later
- .NET 9.0 (for building from source)

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
│   ├── DtddApiClient.cs          # HTTP client for DTDD API
│   ├── DtddPluginController.cs   # REST API endpoints
│   └── Models/                   # API response models
├── Configuration/
│   └── PluginConfiguration.cs    # Plugin settings
├── Providers/
│   ├── DtddMovieProvider.cs      # Movie metadata provider
│   ├── DtddSeriesProvider.cs     # Series metadata provider
│   ├── DtddSeasonProvider.cs     # Season metadata provider
│   └── DtddEpisodeProvider.cs    # Episode metadata provider
├── Services/
│   ├── DtddLibraryScanService.cs # Background scan service
│   └── TriggerCacheService.cs    # Trigger category cache
├── ScheduledTasks/
│   └── DtddRefreshTask.cs        # Daily refresh task
├── TriggerFilter.cs              # Trigger filtering logic
├── Constants.cs                  # Plugin constants
└── Plugin.cs                     # Plugin entry point
```

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
