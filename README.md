# Jellyfin DoesTheDogDie Plugin

![Build Status](https://img.shields.io/badge/build-passing-brightgreen)
![Coverage](https://img.shields.io/badge/coverage-77%25-yellow)
![License](https://img.shields.io/badge/license-GPLv3-blue)
![Jellyfin](https://img.shields.io/badge/Jellyfin-10.11+-purple)

A Jellyfin plugin that integrates [DoesTheDogDie.com](https://www.doesthedogdie.com) content warnings into your media library. Automatically fetches trigger warnings (animal death, violence, jump scares, etc.) and adds them as metadata tags to movies and TV shows.

## Features

- Automatic content warning lookup for movies and TV series
- Adds warnings as searchable/filterable tags (e.g., "CW: Animal Death", "Safe: No Dogs Die")
- Configurable vote threshold to filter low-confidence warnings
- Category and topic filtering to show only warnings you care about
- Background service for automatic lookups on library scan
- Daily scheduled refresh task
- Falls back to title-based search when IMDB ID is unavailable

## Installation

### From Release

1. Download the latest release from the [Releases page](https://github.com/theflanman/dtdd-jellyfin-plugin/releases)
2. Extract the contents to your Jellyfin plugins directory:
   - **Linux:** `~/.local/share/jellyfin/plugins/DoesTheDogDie/`
   - **Windows:** `%LOCALAPPDATA%\jellyfin\plugins\DoesTheDogDie\`
   - **Docker:** `/config/plugins/DoesTheDogDie/`
3. Restart Jellyfin

### From Source

```bash
git clone https://github.com/theflanman/dtdd-jellyfin-plugin.git
cd dtdd-jellyfin-plugin
dotnet publish Jellyfin.Plugin.DoesTheDogDie.sln -c Release
```

Copy the contents of `Jellyfin.Plugin.DoesTheDogDie/bin/Release/net9.0/publish/` to your plugins directory.

## Configuration

Access plugin settings via **Dashboard → Plugins → DoesTheDogDie**.

| Setting | Description | Default |
|---------|-------------|---------|
| Enable Movies | Fetch warnings for movies | `true` |
| Enable Series | Fetch warnings for TV series | `true` |
| Min Votes Threshold | Minimum votes required to include a warning | `3` |
| Tag Prefix | Prefix for warning tags | `CW:` |
| Safe Tag Prefix | Prefix for "confirmed safe" tags | `Safe:` |
| Show All Triggers | Show all triggers vs. filtered selection | `true` |

### Filtering by Category/Topic

When "Show All Triggers" is disabled, you can select specific categories (Animal, Violence, etc.) and topics to display.

## How It Works

1. When media is added or refreshed, the plugin checks for IMDB ID
2. Searches DoesTheDogDie.com by IMDB ID (or falls back to title search)
3. Retrieves trigger data with vote counts
4. Filters triggers based on your configuration
5. Adds matching triggers as tags on the media item

## Development

### Requirements

- .NET 9.0 SDK
- Jellyfin Server 10.11+

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

To display live coverage metrics, you'll need to set up CI/CD integration with a coverage service.

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

3. **Add Codecov token** to repository secrets:
   - Go to Settings → Secrets and variables → Actions
   - Add `CODECOV_TOKEN` from your Codecov dashboard

4. **Update README badge**:
   ```markdown
   ![Coverage](https://codecov.io/gh/theflanman/dtdd-jellyfin-plugin/graph/badge.svg?branch=main)
   ```

### Option 2: Coveralls

1. **Sign up** at [coveralls.io](https://coveralls.io) and enable your repository

2. **Add workflow** (`.github/workflows/ci.yml`):

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

### Option 3: GitHub Actions Badge Only (No External Service)

Generate a badge using test results without external services:

1. **Add workflow** (`.github/workflows/ci.yml`):

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

         - name: Generate coverage report
           run: |
             dotnet tool install -g dotnet-reportgenerator-globaltool
             reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:coverage -reporttypes:JsonSummary

         - name: Extract coverage percentage
           id: coverage
           run: |
             COVERAGE=$(cat coverage/Summary.json | jq '.summary.linecoverage')
             echo "percentage=$COVERAGE" >> $GITHUB_OUTPUT

         - name: Create coverage badge
           uses: schneegans/dynamic-badges-action@v1.7.0
           with:
             auth: ${{ secrets.GIST_TOKEN }}
             gistID: YOUR_GIST_ID
             filename: coverage.json
             label: coverage
             message: ${{ steps.coverage.outputs.percentage }}%
             valColorRange: ${{ steps.coverage.outputs.percentage }}
             maxColorRange: 100
             minColorRange: 0
   ```

2. **Create a GitHub Gist** to store the badge data and note the Gist ID

3. **Create a Personal Access Token** with `gist` scope and add as `GIST_TOKEN` secret

4. **Update README badge**:
   ```markdown
   ![Coverage](https://img.shields.io/endpoint?url=https://gist.githubusercontent.com/theflanman/GIST_ID/raw/coverage.json)
   ```

## API Documentation

See [docs/API_DOCUMENTATION.md](docs/API_DOCUMENTATION.md) for DoesTheDogDie API reference.

## License

This plugin is licensed under the [GNU General Public License v3.0](LICENSE).

## Acknowledgments

- [DoesTheDogDie.com](https://www.doesthedogdie.com) for providing the content warning data
- [Jellyfin](https://jellyfin.org) for the media server platform
