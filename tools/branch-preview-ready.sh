#!/usr/bin/env bash
# cursor/* branches shared on GitHub Pages must include latest main so preview
# URLs show current HUD, assets, and workflows — not a stale fork point.
# Agents: run this (or full harness) before posting a branch preview link.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

branch="${GITHUB_HEAD_REF:-${BRANCH:-$(git rev-parse --abbrev-ref HEAD 2>/dev/null || echo "")}}"

if [[ "$branch" == "main" || -z "$branch" || "$branch" == "HEAD" ]]; then
  echo "branch-preview-ready: skip (not a feature branch)"
  exit 0
fi

if [[ "$branch" != cursor/* ]]; then
  echo "branch-preview-ready: skip ($branch is not cursor/*)"
  exit 0
fi

if ! git rev-parse --verify origin/main >/dev/null 2>&1; then
  git fetch origin main --depth=1
fi

if ! git merge-base --is-ancestor origin/main HEAD; then
  echo "branch-preview-ready: FAIL — $branch is behind origin/main." >&2
  echo "  git fetch origin main && git rebase origin/main && git push --force-with-lease" >&2
  echo "  Preview URL (after deploy): https://\${OWNER}.github.io/red-dead-texas-holdem/preview/${branch//\//-}/" >&2
  exit 1
fi

slug="${branch//\//-}"
echo "branch-preview-ready: OK ($branch includes origin/main)"
echo "preview: https://simmo76.github.io/red-dead-texas-holdem/preview/$slug/ (after deploy-preview CI)"