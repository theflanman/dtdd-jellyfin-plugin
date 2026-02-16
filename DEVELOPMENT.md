# Development Guide

## Versioning

Version scheme: `(major breaking changes).(new backwards compatible features).(bugfixes).(dependency updates)`

Current version: `0.1.1.0`

## Git Workflow

Branching model based on git-flow:

### Long-lived branches

- `main` — Production-ready code. Every commit to main is a tagged release.
- `development` — Integration branch for in-progress work.

### Short-lived branches

- **Feature branches** (`feature/*`) — Branch from `development`, merge back into `development` via PR.
- **Release branches** (`release/x.x.x.x`) — Branch from `development` when preparing a release. Merge into `main` via PR, then merge `main` back into `development`. Tag on `main` after merge (e.g., `0.1.1.0`).
- **Hotfix branches** (`hotfix/*`) — Branch from `main` for urgent production fixes. Merge into `main` via PR, then merge `main` back into `development`.

### Flow

```
feature/* ──→ development ──→ release/* ──→ main ──→ development
                                              ↑           ↓
                                         hotfix/* ──→ main ──→ development
```

### Rules

1. `main` is always deployable. Every merge to `main` gets a version tag.
2. `development` is the integration target for all feature work.
3. After any merge to `main`, merge `main` back into `development` to keep them in sync.
4. Direct commits to `main` or `development` are not allowed — use PRs.

## TDD Strategy

1. Write failing tests first (red)
2. Implement fixes (green)
3. Verify no regressions
4. Run `dotnet test` after each implementation step

## Build

```bash
dotnet build
```

## Test

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML coverage report
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"TestResults/CoverageReport" -reporttypes:Html
```

## Project Structure

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
├── TagHelper.cs                  # Shared tag strip/rebuild logic
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

3. **Add Codecov token** to repository secrets (Settings > Secrets > Actions)

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

## Integration Testing with Jellyfin

Use the headless test script to build the plugin and run it against a local Jellyfin instance:

```bash
./scripts/run-jellyfin-test.sh
```

This requires a `../jellyfin` source checkout. The script:
- Builds the plugin
- Copies DLLs to an isolated plugin directory
- Launches Jellyfin headless (no web client)
- API available at `http://localhost:8096`
- Swagger docs at `http://localhost:8096/api-docs/swagger`
