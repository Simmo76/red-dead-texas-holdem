#!/usr/bin/env bash
# Copy the static iPhone-landscape topbar layout harness into a Pages tree.
set -euo pipefail

SITE="${1:?usage: copy-topbar-preview.sh <site-dir>}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DEST="$SITE/topbar-preview"

rm -rf "$DEST"
mkdir -p "$DEST"
cp -r "$ROOT/tools/settings-preview/." "$DEST/"
touch "$SITE/.nojekyll"