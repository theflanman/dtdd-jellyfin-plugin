#!/usr/bin/env bash
# Builds plugin, copies to Jellyfin plugin dir, launches Jellyfin headless.
# Requires ../jellyfin source checkout.
set -euo pipefail

JELLYFIN_DIR="../jellyfin"
DATA_DIR="/tmp/jellyfin-test-data"
CONFIG_DIR="/tmp/jellyfin-test-config"
CACHE_DIR="/tmp/jellyfin-test-cache"
PLUGIN_DIR="$DATA_DIR/plugins/DoesTheDogDie"

# Build plugin
dotnet publish Jellyfin.Plugin.DoesTheDogDie.sln -c Debug

# Copy to plugin dir
mkdir -p "$PLUGIN_DIR"
cp Jellyfin.Plugin.DoesTheDogDie/bin/Debug/net9.0/publish/*.dll "$PLUGIN_DIR/"

# Build and run Jellyfin headless
# --nowebclient disables web UI; API at http://localhost:8096
# Swagger docs at http://localhost:8096/api-docs/swagger
dotnet run --project "$JELLYFIN_DIR/Jellyfin.Server" -- \
    --nowebclient \
    --datadir "$DATA_DIR" \
    --configdir "$CONFIG_DIR" \
    --cachedir "$CACHE_DIR"
