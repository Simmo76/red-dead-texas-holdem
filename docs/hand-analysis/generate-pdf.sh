#!/usr/bin/env bash
# Regenerate PDF exports from the committed markdown hand logs.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

if ! command -v pandoc >/dev/null; then
  echo "Install pandoc and wkhtmltopdf (e.g. apt install pandoc wkhtmltopdf)." >&2
  exit 1
fi

pandoc hands-seed42-100.md -o hands-seed42-100.pdf \
  --pdf-engine=wkhtmltopdf \
  --metadata title="Hand sequence log (seed 42, 100 hands)"

pandoc hands-seed42-1000.md -o hands-seed42-1000.pdf \
  --pdf-engine=wkhtmltopdf \
  --metadata title="Hand sequence log (seed 42, 1000 hands)"

echo "Wrote hands-seed42-100.pdf and hands-seed42-1000.pdf"
