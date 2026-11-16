#!/usr/bin/env bash
# Publishes the Blazor WebAssembly web app and stages it as the Capacitor
# web bundle (www/). Run from Mobile/ios-wrapper. Requires the .NET 8 SDK.
set -euo pipefail

cd "$(dirname "$0")/.."
REPO_ROOT="$(cd ../.. && pwd)"
PUBLISH_DIR="$(mktemp -d)"

dotnet publish "$REPO_ROOT/Web/CinematicPoker.Web/CinematicPoker.Web.csproj" \
  -c Release -o "$PUBLISH_DIR" --nologo

rm -rf www
cp -R "$PUBLISH_DIR/wwwroot" www
rm -rf "$PUBLISH_DIR"

# The app is served from the bundle, not a web server doing content
# negotiation, so the precompressed duplicates are dead weight.
find www \( -name '*.br' -o -name '*.gz' \) -delete

# Stamp version.json (index.html fetches it to bust stale caches on the web;
# in the app it is just informational).
SHA="$(git -C "$REPO_ROOT" rev-parse --short HEAD 2>/dev/null || echo local)"
printf '{"version":"%s","built":"%s"}\n' "$SHA" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > www/version.json

echo "Staged $(du -sh www | cut -f1) into www/"
