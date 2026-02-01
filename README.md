# Does The Dog Die - Jellyfin Plugin (Unofficial)

An unofficial Jellyfin plugin that integrates content warnings from [DoesTheDogDie.com](https://www.doesthedogdie.com) into your media library.

> **Note:** This plugin is not affiliated with or endorsed by DoesTheDogDie.com or Jellyfin.

## What It Does

This plugin automatically fetches trigger warnings for movies and TV shows and adds them as tags to your Jellyfin library. It helps viewers make informed decisions about content that might be distressing—whether that's animal death, jump scares, violence, or dozens of other potential triggers.

**Example tags added to a movie:**
- `CW: a dog dies`
- `CW: there are jump scares`
- `Safe: a cat dies` (confirmed safe)

## How It Works

1. When you refresh metadata or add new items to your library, the plugin looks up each movie/series on DoesTheDogDie.com using its IMDB ID
2. It retrieves community-voted trigger data (e.g., "Does the dog die?" → 1,336 yes / 118 no)
3. Based on vote thresholds you configure, it adds warning tags (`CW:`) or safe tags (`Safe:`) to the item
4. Tags appear in Jellyfin's UI and can be used for filtering/searching

## Features

- **Automatic lookups** - Background service detects new library items and fetches warnings
- **Scheduled refresh** - Daily task keeps trigger data up to date
- **Hierarchical filtering** - Choose which trigger categories and specific topics to track
- **Configurable thresholds** - Set minimum vote counts to filter out unreliable data
- **Safe confirmations** - Optionally show when content is confirmed *safe* for specific triggers
- **External links** - Quick links to DoesTheDogDie.com pages from item details

## Installation

1. Download the latest release
2. Extract to your Jellyfin plugins directory
3. Restart Jellyfin
4. Configure in **Dashboard → Plugins → Does The Dog Die**

## Configuration

| Setting | Description |
|---------|-------------|
| Enable Movies/Series | Toggle which media types to process |
| Tag Prefix | Customize warning tag prefix (default: `CW:`) |
| Safe Tag Prefix | Customize safe tag prefix (default: `Safe:`) |
| Min Votes Threshold | Minimum votes required to show a trigger |
| Trigger Categories | Select which categories to track (Animal, Violence, etc.) |

## Requirements

- Jellyfin Server 10.11.0 or later
- Items must have IMDB IDs for lookup to work

## License

GPLv3 - See [LICENSE](LICENSE) for details.
