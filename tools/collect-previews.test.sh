#!/usr/bin/env bash
# Exercises the preview budget short-circuit in collect-previews.sh.
# gh is replaced with a fixture that records which artifact zips are downloaded.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SCRIPT="$ROOT/tools/collect-previews.sh"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

BIN="$TMP/bin"
FIX="$TMP/zips"
mkdir -p "$BIN" "$FIX"

cat > "$BIN/gh" << 'EOF'
#!/usr/bin/env bash
set -euo pipefail
cmd="${1:?}"
shift
if [ "$cmd" != "api" ]; then
  echo "unexpected gh $cmd" >&2
  exit 1
fi
url=""
for arg in "$@"; do
  case "$arg" in
    repos/*) url="$arg" ;;
  esac
done
if [[ "$url" == *"/actions/artifacts?"* ]]; then
  cat "${FIXTURE_LIST:?}"
  exit 0
fi
if [[ "$url" == *"/zip" ]]; then
  id="${url##*/artifacts/}"
  id="${id%/zip}"
  echo "$id" >> "${DOWNLOADS:?}"
  cat "${FIXTURE_DIR:?}/$id.zip"
  exit 0
fi
if [[ "$url" == *"/git/ref/heads/"* ]]; then
  branch="${url##*/heads/}"
  if [ -f "${DEAD_BRANCHES:-/nonexistent}/$branch" ]; then
    exit 1
  fi
  exit 0
fi
if [[ "$url" == *"/pulls?"* ]]; then
  echo 1
  exit 0
fi
echo "unexpected gh api $url" >&2
exit 1
EOF
chmod +x "$BIN/gh"

# Payload bytes chosen so du -sm of the unpacked preview is exactly want_mb.
pack() {
  local id="$1" want_mb="$2" branch="${3:-}"
  local dir="$TMP/pack-$id"
  rm -rf "$dir"
  mkdir -p "$dir"
  if [ -n "$branch" ]; then
    printf '{"branch":"%s"}\n' "$branch" > "$dir/version.json"
  else
    echo '{}' > "$dir/version.json"
  fi
  # du -sm rounds up to the next mebibyte. Stay just under the boundary.
  # du -sm counts allocated blocks and rounds up, so the payload sits
  # a block below the mebibyte boundary the test asks for.
  dd if=/dev/zero of="$dir/payload" bs=1 count=$((want_mb * 1048576 - 65536)) status=none
  (cd "$dir" && zip -q -r "$FIX/$id.zip" .)
  local got
  got="$(unzip -q -d "$TMP/check-$id" "$FIX/$id.zip" && du -sm "$TMP/check-$id" | cut -f1)"
  if [ "$got" != "$want_mb" ]; then
    echo "fixture $id unpacked to ${got} MB, wanted ${want_mb}" >&2
    exit 1
  fi
  rm -rf "$TMP/check-$id"
}

assert_eq() {
  local got="$1" want="$2" label="$3"
  if [ "$got" != "$want" ]; then
    echo "FAIL $label: got [$got] want [$want]" >&2
    exit 1
  fi
}

run_collect() {
  local site="$1"
  shift
  mkdir -p "$site"
  : > "$TMP/downloads"
  PATH="$BIN:$PATH" \
    FIXTURE_LIST="$TMP/list.tsv" \
    FIXTURE_DIR="$FIX" \
    DOWNLOADS="$TMP/downloads" \
    DEAD_BRANCHES="$TMP/dead" \
    GITHUB_REPOSITORY="acme/game" \
    PREVIEW_BUDGET_MB="$BUDGET" \
    "$SCRIPT" "$site" "$@"
}

# Same unpacked size as the reported artifact size. After two 2 MB keeps,
# a third 2 MB preview does not fit and must not be downloaded. A later
# 1 MB preview still fits in the leftover megabyte and is downloaded.
pack 1 2
pack 2 2
pack 3 2
pack 4 1
{
  printf 'preview-new\t1\t2026-11-20T03:00:00Z\t%d\n' $((2 * 1048576))
  printf 'preview-mid\t2\t2026-11-20T02:00:00Z\t%d\n' $((2 * 1048576))
  printf 'preview-big-old\t3\t2026-11-20T01:00:00Z\t%d\n' $((2 * 1048576))
  printf 'preview-small-old\t4\t2026-11-20T00:00:00Z\t%d\n' $((1 * 1048576))
} > "$TMP/list.tsv"
BUDGET=5
run_collect "$TMP/site-a" > "$TMP/out-a"
assert_eq "$(grep -c '^kept preview' "$TMP/out-a")" "3" "keeps a smaller older preview"
assert_eq "$(grep -c 'skipping preview big-old' "$TMP/out-a")" "1" "skips the preview that cannot fit"
assert_eq "$(paste -sd, "$TMP/downloads")" "1,2,4" "does not download the preview over the budget"
test -d "$TMP/site-a/preview/small-old"

# Compressed size understates unpacked size. After the first download the
# script scales later artifacts and skips the download once another copy
# would exceed the budget.
pack 5 2
{
  printf 'preview-a\t5\t2026-11-20T03:00:00Z\t%d\n' $((1 * 1048576))
  printf 'preview-b\t5\t2026-11-20T02:00:00Z\t%d\n' $((1 * 1048576))
  printf 'preview-c\t5\t2026-11-20T01:00:00Z\t%d\n' $((1 * 1048576))
} > "$TMP/list.tsv"
# Distinct ids so each zip can be recorded, but the zip bytes are shared.
cp "$FIX/5.zip" "$FIX/6.zip"
cp "$FIX/5.zip" "$FIX/7.zip"
{
  printf 'preview-a\t5\t2026-11-20T03:00:00Z\t%d\n' $((1 * 1048576))
  printf 'preview-b\t6\t2026-11-20T02:00:00Z\t%d\n' $((1 * 1048576))
  printf 'preview-c\t7\t2026-11-20T01:00:00Z\t%d\n' $((1 * 1048576))
} > "$TMP/list.tsv"
BUDGET=5
run_collect "$TMP/site-b" > "$TMP/out-b"
assert_eq "$(paste -sd, "$TMP/downloads")" "5,6" "scales zip size and skips the third download"
assert_eq "$(grep -c '^kept preview' "$TMP/out-b")" "2" "keeps the two that fit"
grep -q 'skipping preview c' "$TMP/out-b"

# Once used space reaches the budget, older previews are not fetched at all.
pack 8 2
cp "$FIX/8.zip" "$FIX/9.zip"
cp "$FIX/8.zip" "$FIX/10.zip"
{
  printf 'preview-a\t8\t2026-11-20T03:00:00Z\t%d\n' $((2 * 1048576))
  printf 'preview-b\t9\t2026-11-20T02:00:00Z\t%d\n' $((2 * 1048576))
  printf 'preview-c\t10\t2026-11-20T01:00:00Z\t%d\n' $((1 * 1048576))
} > "$TMP/list.tsv"
BUDGET=4
run_collect "$TMP/site-c" > "$TMP/out-c"
assert_eq "$(paste -sd, "$TMP/downloads")" "8,9" "stops once the budget is full"
grep -q 'preview budget full' "$TMP/out-c"

# A deleted branch is still inspected (the branch name is inside the zip)
# and teaches the expansion used to skip later downloads.
mkdir -p "$TMP/dead/cursor"
pack 11 2 "cursor/gone"
cp "$FIX/11.zip" "$FIX/12.zip"
touch "$TMP/dead/cursor/gone"
{
  printf 'preview-gone\t11\t2026-11-20T03:00:00Z\t%d\n' $((1 * 1048576))
  printf 'preview-later\t12\t2026-11-20T02:00:00Z\t%d\n' $((1 * 1048576))
} > "$TMP/list.tsv"
BUDGET=1
run_collect "$TMP/site-d" > "$TMP/out-d"
assert_eq "$(paste -sd, "$TMP/downloads")" "11" "dropped preview calibrates the budget"
grep -q 'dropping preview gone' "$TMP/out-d"
grep -q 'skipping preview later' "$TMP/out-d"

echo "collect-previews budget tests passed"
