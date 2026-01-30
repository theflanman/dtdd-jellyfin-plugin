# DoesTheDogDie Jellyfin Plugin - Implementation Plan

## Executive Summary

This document details the interfaces needed to create a Jellyfin metadata plugin that integrates DoesTheDogDie.com content warnings. Based on analysis of the TVDB plugin architecture and Jellyfin's metadata system.

---

## Part 1: TVDB Plugin Interface Analysis

### Interfaces Implemented by TVDB Plugin

| Interface | Purpose | Need for DTDD? | Rationale |
|-----------|---------|----------------|-----------|
| `IRemoteMetadataProvider<T, TInfo>` | Fetches metadata from external sources | **NO** | DTDD doesn't provide core metadata (titles, descriptions, cast) |
| `IRemoteImageProvider` | Fetches images (posters, backdrops) | **NO** | DTDD doesn't provide media images |
| `IExternalId` | Maps external provider IDs | **YES** | Links Jellyfin items to DTDD entries via IMDB/TMDB IDs |
| `IExternalUrlProvider` | Generates deep links to external sites | **YES** | Link to DTDD page for the item |
| `IPluginServiceRegistrator` | Registers services with DI container | **YES** | Register our API client as singleton |
| `IHostedService` | Background services | **MAYBE** | Could pre-fetch/cache warnings on library scan |
| `IScheduledTask` | Scheduled background tasks | **MAYBE** | Periodic refresh of warning data |
| `BasePlugin<TConfig>` | Plugin entry point | **YES** | Required for all plugins |
| `IHasWebPages` | Embedded configuration UI | **YES** | Configure API key, display preferences |

### TVDB Interface Details

#### 1. `IRemoteMetadataProvider<TItemType, TLookupInfoType>`
**What it does:** Fetches complete metadata for Series, Episodes, Movies, Seasons, and People from TVDB.

**Required methods:**
- `Name` - Provider identifier
- `GetSearchResults(TInfo, CancellationToken)` - Search by name/ID
- `GetMetadata(TInfo, CancellationToken)` - Fetch full metadata
- `GetImageResponse(string url, CancellationToken)` - Download images

**DTDD Decision: NOT NEEDED**
- DTDD provides content warnings, not core metadata
- We don't want to replace existing metadata, just supplement it

#### 2. `IRemoteImageProvider`
**What it does:** Fetches artwork (posters, backdrops, banners, logos) for media items.

**Required methods:**
- `Name` - Provider identifier
- `Supports(BaseItem)` - Check item type support
- `GetSupportedImages(BaseItem)` - Return supported image types
- `GetImages(BaseItem, CancellationToken)` - Fetch available images
- `GetImageResponse(string url, CancellationToken)` - Download image

**DTDD Decision: NOT NEEDED**
- DTDD doesn't provide artwork for media

#### 3. `IExternalId`
**What it does:** Maps external provider IDs for cross-referencing and UI display.

**TVDB implements 8 variants:**
- `TvdbSeriesExternalId`, `TvdbSeriesSlugExternalId`
- `TvdbEpisodeExternalId`, `TvdbSeasonExternalId`
- `TvdbMovieExternalId`, `TvdbMovieSlugExternalId`
- `TvdbPersonExternalId`, `TvdbCollectionsExternalId`

**Required members:**
- `ProviderName` - Display name (e.g., "DoesTheDogDie")
- `Key` - Internal key (e.g., "Dtdd")
- `Type` - `ExternalIdMediaType?` (Movie, Series, etc.)
- `Supports(IHasProviderIds)` - Check item type

**DTDD Decision: YES - IMPLEMENT**
- Store DTDD item IDs on Jellyfin items
- Enables quick lookup without re-searching
- Implementations needed:
  - `DtddMovieExternalId`
  - `DtddSeriesExternalId`

#### 4. `IExternalUrlProvider`
**What it does:** Generates clickable links to external websites in the Jellyfin UI.

**Required methods:**
- `Name` - Provider identifier
- `GetExternalUrls(BaseItem)` - Return URLs for the item

**DTDD Decision: YES - IMPLEMENT**
- Link directly to DTDD page: `https://www.doesthedogdie.com/media/{id}`
- Allows users to see full details and contribute

#### 5. `IPluginServiceRegistrator`
**What it does:** Registers services with Jellyfin's dependency injection container.

**Required methods:**
- `RegisterServices(IServiceCollection, IServerApplicationHost)`

**DTDD Decision: YES - IMPLEMENT**
- Register `DtddApiClient` as singleton
- Register any background services

#### 6. `IHostedService`
**What it does:** Background service that runs for the plugin's lifetime.

**TVDB uses:** `TvdbMissingEpisodeProvider` - Detects and creates missing episode placeholders.

**DTDD Decision: MAYBE - CONSIDER**
- Could pre-fetch warnings when items are added to library
- Could cache warning data for offline access
- Lower priority for initial implementation

#### 7. `IScheduledTask`
**What it does:** Scheduled tasks that run periodically.

**TVDB uses:** `UpdateTask` - Polls for metadata updates.

**DTDD Decision: MAYBE - LATER**
- Could periodically refresh warning data
- Lower priority - warnings don't change frequently

---

## Part 2: Jellyfin Interfaces for DTDD

### Primary Interfaces to Implement

#### `ICustomMetadataProvider<TItemType>`
**Location:** `MediaBrowser.Controller/Providers/ICustomMetadataProvider.cs`

**What it does:** Allows custom pre/post-processing of metadata. Called during metadata refresh pipeline.

**Required methods:**
```csharp
Task<ItemUpdateType> FetchAsync(TItemType item, MetadataRefreshOptions options, CancellationToken ct)
```

**DTDD Decision: YES - PRIMARY INTERFACE**
- Fetches DTDD warnings during normal metadata refresh
- Doesn't replace existing metadata, supplements it
- Implementations needed:
  - `DtddMovieProvider : ICustomMetadataProvider<Movie>`
  - `DtddSeriesProvider : ICustomMetadataProvider<Series>`

#### `IHasOrder`
**What it does:** Establishes execution order for providers.

**DTDD Decision: YES**
- Set high `Order` value (e.g., 100) to run after core metadata providers
- Ensures IMDB/TMDB IDs are available when we run

### Storage Approach

Content warnings don't fit standard Jellyfin metadata fields. Options:

1. **Tags** - Store trigger names as tags (e.g., "DTDD:Animal Death", "DTDD:Jump Scares")
2. **Custom Fields** - Use `ProviderIds` dictionary for DTDD ID
3. **External Files** - Store JSON alongside media files
4. **Plugin Data** - Use Jellyfin's plugin data storage API

**Recommended:** Combination approach
- Store DTDD ID in `ProviderIds["Dtdd"]`
- Store summary warnings in Tags
- Cache full data in plugin storage for UI display

---

## Part 3: DoesTheDogDie API Integration

### API Specification

**Base URL:** `https://www.doesthedogdie.com`

**Authentication:**
```
Headers:
  Accept: application/json
  X-API-KEY: {user_api_key}
```

**Endpoints:**

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/dddsearch?q={term}` | GET | Search by title |
| `/dddsearch?imdb={id}` | GET | Search by IMDB ID |
| `/media/{id}` | GET | Get full trigger data |

### Supported Media Types

DoesTheDogDie tracks content warnings for the following media types:

| DTDD Media Type | Jellyfin Equivalent | Support Priority |
|-----------------|---------------------|------------------|
| **Movies** | `Movie` | **Primary** |
| **TV Shows** | `Series` | **Primary** |
| **TV Show Seasons** | `Season` | **Primary** |
| **TV Show Episodes** | `Episode` | **Primary** |
| **Anime** | `Series`/`Movie` | **Primary** - Special case, may be TV or movie |
| **Books** | `Book` | **Secondary** |
| **Comics** | `Book` (ebook format) | **Secondary** |
| **Manga** | `Book` (ebook format) | **Secondary** |
| **Short Stories** | `Book` (ebook format) | **Secondary** |

**Notes:**
- Anime is a special case - DTDD may categorize anime as TV shows or movies
- Books, Comics, Manga, and Short Stories all map to Jellyfin's Book type (ebook libraries)

### Available Data

**Per Media Item:**
- `id` - DTDD internal ID
- `imdbId` - IMDB identifier
- `tmdbId` - TMDB identifier
- `name` - Title
- `releaseYear` - Year
- `mediaType` - movie/tv/book/game/anime/comic/manga/shortstory
- `topicItemStats` - Array of trigger ratings

**Per Trigger:**
- `topic.name` - Trigger name (e.g., "Does a dog die?")
- `topic.smmwDescription` - Short description
- `yesSum` / `noSum` - Community votes
- `comments` - User-submitted details with timestamps

### Trigger Categories (204 total)

Major categories include:
- **Animal** - Pet deaths, animal abuse
- **Violence** - Assault, torture, gore
- **Mental Health** - Suicide, self-harm, eating disorders
- **Sexual Content** - Assault, explicit content
- **Phobias** - Spiders, clowns, needles, trypophobia
- **Jump Scares** - Startling moments
- **LGBTQ+** - Representation concerns
- **Family** - Child harm, family death
- **Medical** - Hospitals, needles, illness

### Matching Strategy

1. **Primary:** Match via IMDB ID (most reliable)
2. **Secondary:** Match via TMDB ID
3. **Fallback:** Search by title + year

---

## Part 4: Implementation Architecture

### Project Structure

```
Jellyfin.Plugin.Dtdd/
├── DtddPlugin.cs                    # BasePlugin<PluginConfiguration>
├── DtddPluginServiceRegistrator.cs  # IPluginServiceRegistrator
├── Configuration/
│   ├── PluginConfiguration.cs       # API key, display settings
│   └── config.html                  # Configuration UI
├── Api/
│   ├── DtddApiClient.cs             # HTTP client wrapper
│   ├── Models/
│   │   ├── DtddSearchResult.cs
│   │   ├── DtddMediaItem.cs
│   │   └── DtddTrigger.cs
├── Providers/
│   ├── DtddMovieProvider.cs         # ICustomMetadataProvider<Movie>
│   ├── DtddSeriesProvider.cs        # ICustomMetadataProvider<Series>
│   ├── DtddExternalUrlProvider.cs   # IExternalUrlProvider
│   └── ExternalIds/
│       ├── DtddMovieExternalId.cs   # IExternalId
│       └── DtddSeriesExternalId.cs  # IExternalId
└── Storage/
    └── DtddDataStore.cs             # Cache trigger data
```

### Class Responsibilities

#### `DtddPlugin`
- Extends `BasePlugin<PluginConfiguration>`
- Implements `IHasWebPages` for config UI
- Defines `ProviderName = "DoesTheDogDie"`
- Defines `ProviderId = "Dtdd"`

#### `DtddApiClient`
- Singleton service (registered via DI)
- Handles authentication headers
- Implements search and fetch methods
- Caches responses (configurable TTL)

#### `DtddMovieProvider` / `DtddSeriesProvider`
- Implements `ICustomMetadataProvider<T>`
- High `Order` value to run after core providers
- Fetches DTDD data using existing provider IDs
- Stores DTDD ID and tags on item

#### `DtddExternalUrlProvider`
- Returns `https://www.doesthedogdie.com/media/{id}`
- Shows in Jellyfin UI external links section

### Data Flow

```
1. User triggers metadata refresh
2. Core providers run (TMDB, TVDB, etc.)
3. Item now has IMDB/TMDB IDs populated
4. DtddMovieProvider.FetchAsync() called
5. DtddApiClient searches by IMDB ID
6. If found: Store DTDD ID, add warning tags
7. Full trigger data cached in plugin storage
8. UI can display cached warnings
```

---

## Part 5: Configuration Options

### Plugin Settings

| Setting | Type | Default | Purpose |
|---------|------|---------|---------|
| `ApiKey` | string | "" | User's DTDD API key |
| `EnableMovies` | bool | true | Fetch warnings for movies |
| `EnableSeries` | bool | true | Fetch warnings for TV series |
| `CacheDurationHours` | int | 168 (7 days) | Cache TTL |
| `MinVotesThreshold` | int | 3 | Minimum votes to display trigger |
| `AddWarningTags` | bool | true | Add triggers as item tags |
| `TagPrefix` | string | "CW:" | Prefix for warning tags |
| `EnabledCategories` | string[] | all | Which trigger categories to track |

### User Experience

1. User installs plugin
2. User obtains API key from doesthedogdie.com/profile
3. User enters API key in plugin settings
4. Plugin automatically fetches warnings during library scans
5. Warnings appear as tags on items
6. External link allows viewing full details on DTDD

---

## Part 6: Interface Summary Table

| Interface | TVDB Uses | DTDD Needs | Implementation |
|-----------|-----------|------------|----------------|
| `BasePlugin<TConfig>` | Yes | **YES** | `DtddPlugin` |
| `IHasWebPages` | Yes | **YES** | Config UI |
| `IPluginServiceRegistrator` | Yes | **YES** | Register API client |
| `IRemoteMetadataProvider` | Yes | **NO** | Not a primary metadata source |
| `IRemoteImageProvider` | Yes | **NO** | No images to provide |
| `ICustomMetadataProvider` | No | **YES** | Supplement existing metadata |
| `IExternalId` | Yes (8) | **YES** (2) | Movie, Series |
| `IExternalUrlProvider` | Yes | **YES** | Link to DTDD page |
| `IHostedService` | Yes | Maybe | Pre-fetch on library scan |
| `IScheduledTask` | Yes | Maybe | Periodic refresh |
| `IHasOrder` | Implicit | **YES** | Run after core providers |

---

## Part 7: Implementation Phases

### Phase 1: Core Infrastructure
- [ ] Create plugin class structure
- [ ] Implement configuration with API key
- [ ] Build `DtddApiClient` with authentication
- [ ] Create API response models

### Phase 2: Metadata Integration
- [ ] Implement `ICustomMetadataProvider<Movie>`
- [ ] Implement `ICustomMetadataProvider<Series>`
- [ ] Store DTDD ID in ProviderIds
- [ ] Add warning tags to items

### Phase 3: UI Integration
- [ ] Implement `IExternalId` providers
- [ ] Implement `IExternalUrlProvider`
- [ ] Create configuration HTML page

### Phase 4: Enhancement (Optional)
- [ ] Add caching layer for trigger data
- [ ] Implement `IHostedService` for pre-fetching
- [ ] Add filtering by trigger categories
- [ ] Create custom UI component for warnings display

---

## Verification Plan

1. **Build:** `dotnet build` succeeds without errors
2. **Install:** Plugin loads in Jellyfin without crashes
3. **Configure:** API key can be entered and saved
4. **Fetch:** Refreshing a movie fetches DTDD data
5. **Display:** DTDD ID appears in item external IDs
6. **Tags:** Warning tags appear on item
7. **Link:** External URL links to correct DTDD page

---

## References

- [TVDB Plugin Source](https://github.com/jellyfin/jellyfin-plugin-tvdb)
- [Jellyfin Source](https://github.com/jellyfin/jellyfin)
- [Jellyfin Plugin Template](https://github.com/jellyfin/jellyfin-plugin-template)
- [DoesTheDogDie API](https://www.doesthedogdie.com/api)
