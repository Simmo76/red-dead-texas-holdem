#!/usr/bin/env bash
# Copy every live branch preview into <site>/preview/<slug>/.
#
# GitHub Pages serves one deployment at a time, so each deploy (main or a
# branch preview) has to re-assemble every preview it wants to keep. Each
# preview run uploads its built site as a "preview-<slug>" artifact; the
# newest unexpired one per slug is used, and previews whose branch has been
# deleted are dropped.
#
# Needs GH_TOKEN with actions:read, and GITHUB_REPOSITORY.
set -euo pipefail

SITE="${1:?usage: collect-previews.sh <site-dir> [skip-slug]}"
SKIP="${2:-}"
REPO="${GITHUB_REPOSITORY:?}"
WORK="$(mktemp -d)"
TAB="$(printf '\t')"

gh api --paginate "repos/${REPO}/actions/artifacts?per_page=100" \
  --jq '.artifacts[] | select(.expired == false and (.name | startswith("preview-"))) | [.name, (.id | tostring), .created_at] | @tsv' \
  | sort -t "$TAB" -k1,1 -k3,3r \
  | awk -F '\t' '!seen[$1]++ { print $1 "\t" $2 }' > "$WORK/list"

while IFS="$TAB" read -r name id; do
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
  if [ -n "$branch" ] && ! gh api "repos/${REPO}/git/ref/heads/${branch}" --silent 2>/dev/null; then
    echo "dropping preview ${slug}: branch ${branch} no longer exists"
    continue
  fi
  mkdir -p "${SITE}/preview/${slug}"
  cp -r "$WORK/x/." "${SITE}/preview/${slug}/"
  echo "kept preview ${slug}"
done < "$WORK/list"

rm -rf "$WORK"
