#!/usr/bin/env bash
# Copy live branch previews into <site>/preview/<slug>/.
#
# GitHub Pages serves one deployment at a time, so each deploy (main or a
# branch preview) has to re-assemble every preview it wants to keep. Each
# preview run uploads its built site as a "preview-<slug>" artifact; the
# newest unexpired one per slug is used.
#
# A preview is kept only while it is plausibly being looked at:
#   - its branch still exists, AND
#   - the branch has an open PR, or the build is fresh (< FRESH_HOURS old,
#     covering branches whose PR has not been opened yet).
# Pages rejects deployments over 1 GB, and every preview is a full copy of
# the game (~140 MB), so previews are also capped by PREVIEW_BUDGET_MB,
# newest first — the budget must leave room for the live site and for the
# current branch's own preview, which the workflow adds after this script.
#
# Needs GH_TOKEN with actions:read and pull_requests:read, and
# GITHUB_REPOSITORY.
set -euo pipefail

SITE="${1:?usage: collect-previews.sh <site-dir> [skip-slug]}"
SKIP="${2:-}"
REPO="${GITHUB_REPOSITORY:?}"
OWNER="${REPO%%/*}"
BUDGET_MB="${PREVIEW_BUDGET_MB:-600}"
FRESH_HOURS="${PREVIEW_FRESH_HOURS:-48}"
WORK="$(mktemp -d)"
TAB="$(printf '\t')"

# Newest unexpired artifact per slug, then newest slugs first so the size
# budget favours recent work.
gh api --paginate "repos/${REPO}/actions/artifacts?per_page=100" \
  --jq '.artifacts[] | select(.expired == false and (.name | startswith("preview-"))) | [.name, (.id | tostring), .created_at] | @tsv' \
  | sort -t "$TAB" -k1,1 -k3,3r \
  | awk -F '\t' '!seen[$1]++' \
  | sort -t "$TAB" -k3,3r > "$WORK/list"

used_mb=0
now_epoch="$(date -u +%s)"

while IFS="$TAB" read -r name id created_at; do
  slug="${name#preview-}"
  if [ -n "$SKIP" ] && [ "$slug" = "$SKIP" ]; then
    continue
  fi
  rm -rf "$WORK/x" && mkdir "$WORK/x"
  if ! gh api "repos/${REPO}/actions/artifacts/${id}/zip" > "$WORK/a.zip" \
      || ! unzip -q "$WORK/a.zip" -d "$WORK/x"; then
    echo "::warning::could not download preview ${slug} (artifact ${id})"
    continue
  fi
  branch="$(jq -r '.branch // empty' "$WORK/x/version.json" 2>/dev/null || true)"
  if [ -n "$branch" ]; then
    if ! gh api "repos/${REPO}/git/ref/heads/${branch}" --silent 2>/dev/null; then
      echo "dropping preview ${slug}: branch ${branch} no longer exists"
      continue
    fi
    open_prs="$(gh api "repos/${REPO}/pulls?state=open&head=${OWNER}:${branch}" --jq 'length' 2>/dev/null || echo 0)"
    created_epoch="$(date -u -d "$created_at" +%s 2>/dev/null || echo 0)"
    age_hours=$(( (now_epoch - created_epoch) / 3600 ))
    if [ "$open_prs" = "0" ] && [ "$age_hours" -ge "$FRESH_HOURS" ]; then
      echo "dropping preview ${slug}: no open PR for ${branch} and build is ${age_hours}h old"
      continue
    fi
  fi
  # Pages serves files verbatim (no content negotiation), so precompressed
  # publish artifacts are dead weight. Older artifacts still contain them.
  find "$WORK/x" \( -name '*.br' -o -name '*.gz' \) -delete
  size_mb="$(du -sm "$WORK/x" | cut -f1)"
  if [ $((used_mb + size_mb)) -gt "$BUDGET_MB" ]; then
    echo "::warning::skipping preview ${slug} (${size_mb} MB): keeping it would exceed the ${BUDGET_MB} MB preview budget"
    continue
  fi
  used_mb=$((used_mb + size_mb))
  mkdir -p "${SITE}/preview/${slug}"
  cp -r "$WORK/x/." "${SITE}/preview/${slug}/"
  echo "kept preview ${slug} (${size_mb} MB, ${used_mb}/${BUDGET_MB} MB used)"
done < "$WORK/list"

rm -rf "$WORK"
