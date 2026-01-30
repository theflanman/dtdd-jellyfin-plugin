# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Jellyfin plugin that integrates DoesTheDogDie.com content warnings into Jellyfin media libraries. It fetches trigger warnings (animal death, violence, jump scares, etc.) and adds them as metadata tags to movies and TV shows.

## Build Commands

```bash
# Build the plugin
dotnet build Jellyfin.Plugin.DoesTheDogDie.sln

# Build with full paths for IDE integration
dotnet build Jellyfin.Plugin.DoesTheDogDie.sln /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary

# Publish for deployment
dotnet publish Jellyfin.Plugin.DoesTheDogDie.sln
```

## Testing with Jellyfin

The plugin must be tested within a running Jellyfin instance:

1. Build the plugin: `dotnet publish`
2. Copy output from `Jellyfin.Plugin.DoesTheDogDie/bin/Debug/net9.0/publish/` to Jellyfin's plugin directory
3. Restart Jellyfin server
4. Plugin appears in Dashboard > Plugins

For Docker-based testing, see `docker-compose.test.yml` (to be created).

## Architecture

### Plugin Entry Point
- `Plugin.cs` - Main plugin class extending `BasePlugin<PluginConfiguration>`, implements `IHasWebPages`
- Plugin GUID: `eb5d7894-8eef-4b36-aa6f-5d124e828ce1`
- Plugin Name: "Does The Dog Die"

### Key Jellyfin Interfaces to Implement

| Interface | Purpose |
|-----------|---------|
| `ICustomMetadataProvider<T>` | Supplements existing metadata during refresh (primary interface) |
| `IExternalId` | Maps DTDD IDs to Jellyfin items |
| `IExternalUrlProvider` | Generates links to doesthedogdie.com |
| `IPluginServiceRegistrator` | Registers DI services (API client) |
| `IHostedService` | Pre-fetches warnings on library changes |
| `IScheduledTask` | Periodic metadata refresh |

### Planned Directory Structure
```
Jellyfin.Plugin.DoesTheDogDie/
├── Plugin.cs                      # Entry point
├── PluginServiceRegistrator.cs    # DI registration
├── Constants.cs                   # API key, provider names
├── Configuration/
│   ├── PluginConfiguration.cs     # Settings
│   └── configPage.html            # UI
├── Api/
│   ├── DtddApiClient.cs           # HTTP client
│   └── Models/                    # API response DTOs
├── Providers/                     # ICustomMetadataProvider implementations
├── Services/                      # IHostedService, IScheduledTask
└── Storage/                       # Cache management
```

### DoesTheDogDie API

Base URL: `https://www.doesthedogdie.com`

Headers required:
- `Accept: application/json`
- `X-API-KEY: {key}` (from `.env` file as `DTDD_API_KEY`)

Key endpoints:
- `/dddsearch?q={term}` - Search by title
- `/dddsearch?imdb={id}` - Search by IMDB ID
- `/media/{id}` - Get trigger data

### Data Storage Strategy

1. **ProviderIds**: Store DTDD ID in `item.ProviderIds["Dtdd"]`
2. **Tags**: Add warnings as tags with configurable prefix (e.g., "CW: Animal Death")
3. **Plugin Cache**: Store full trigger data as JSON files

## Code Style

- Target: .NET 9.0
- Nullable reference types enabled
- StyleCop and Roslyn analyzers enforced (see `jellyfin.ruleset`)
- Warnings treated as errors
- Private fields use `_camelCase` prefix
- XML documentation required for public members

## Environment Variables

The `.env` file contains:
- `DTDD_API_KEY` - DoesTheDogDie API key (embedded in builds)
- `JELLYFIN_API_KEY` - For testing against local Jellyfin instance

## Key References

- [TVDB Plugin](https://github.com/jellyfin/jellyfin-plugin-tvdb) - Reference implementation for metadata plugins
- [Jellyfin Plugin Docs](https://jellyfin.org/docs/general/server/plugins/) - Official documentation
- [IMPLEMENTATION_PLAN.md](./IMPLEMENTATION_PLAN.md) - Detailed implementation plan with interface analysis
