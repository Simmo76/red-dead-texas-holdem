#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SCRIPT="$ROOT/tools/branch-preview-ready.sh"

if [[ ! -x "$SCRIPT" ]]; then
  echo "branch-preview-ready.test: script not executable" >&2
  exit 1
fi

# On main, the check is a no-op pass.
BRANCH=main bash "$SCRIPT" >/dev/null

# Current checkout should pass when cursor/* includes origin/main (local agent runs).
branch="$(git -C "$ROOT" rev-parse --abbrev-ref HEAD 2>/dev/null || echo main)"
if [[ "$branch" == cursor/* ]]; then
  git -C "$ROOT" fetch origin main --depth=1 2>/dev/null || true
  bash "$SCRIPT" >/dev/null
fi

echo "branch-preview-ready.test: ok"