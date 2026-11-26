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

slug="${branch//\//-}"
preview_url="https://simmo76.github.io/red-dead-texas-holdem/preview/$slug/"

fail_behind() {
  echo "branch-preview-ready: FAIL — $branch is behind origin/main." >&2
  echo "  git fetch origin main && git rebase origin/main && git push --force-with-lease" >&2
  echo "  Preview URL (after deploy): $preview_url" >&2
  exit 1
}

fetch_for_ancestry() {
  git fetch origin main --depth=64
  if [[ -n "${1:-}" ]]; then
    git fetch origin "$1" --depth=64 2>/dev/null || true
  fi
  if [[ -n "${2:-}" ]]; then
    git fetch origin "$2" --depth=64 2>/dev/null || true
  fi
}

# PR workflows check out a shallow merge commit; compare base vs head SHAs from GitHub.
if [[ "${GITHUB_EVENT_NAME:-}" == "pull_request" ]]; then
  base_sha="${GITHUB_BASE_SHA:-}"
  head_sha="${GITHUB_HEAD_SHA:-}"
  if [[ -z "$base_sha" || -z "$head_sha" ]]; then
    echo "branch-preview-ready: skip (pull_request without base/head sha)"
    exit 0
  fi
  fetch_for_ancestry "$base_sha" "$head_sha"
  if git merge-base --is-ancestor "$base_sha" "$head_sha"; then
    echo "branch-preview-ready: OK ($branch includes pull-request base $base_sha)"
    echo "preview: $preview_url (after deploy-preview CI)"
    exit 0
  fi
  fail_behind
fi

if ! git rev-parse --verify origin/main >/dev/null 2>&1; then
  fetch_for_ancestry
else
  fetch_for_ancestry
fi

if ! git merge-base --is-ancestor origin/main HEAD; then
  fail_behind
fi

echo "branch-preview-ready: OK ($branch includes origin/main)"
echo "preview: $preview_url (after deploy-preview CI)"