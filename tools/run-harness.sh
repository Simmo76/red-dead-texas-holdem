#!/usr/bin/env bash
# Regression harness: engine NUnit suite + web-layer unit/acceptance checks.
# Run from repo root: tools/run-harness.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

echo "== Engine (dotnet test) =="
dotnet test DotNet/CinematicPoker.Engine.Tests/CinematicPoker.Engine.Tests.csproj --nologo

echo "== Web JS unit & acceptance tests (node) =="
for f in tools/*.test.mjs; do
  echo "-- $f"
  node "$f"
done

echo "== Branch preview policy (cursor/*) =="
bash tools/branch-preview-ready.sh

echo "== Shell acceptance =="
bash tools/collect-previews.test.sh
bash tools/branch-preview-ready.test.sh

echo "Harness: all tiers passed"