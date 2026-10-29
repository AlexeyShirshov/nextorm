#!/usr/bin/env bash
# T327 D3 surface audit: the char->string-policy fix is internal-only. Base 5b651f38 (D7).
# Zero public/protected declaration delta is required; a src change is expected (one method body).
set -u
BASE=${1:-5b651f38}
cd "$(dirname "$0")/../../../.." || exit 1
WORKTREE=$(pwd)
HEAD=$(git rev-parse --short HEAD)

echo "=== T327 D3 surface audit vs $BASE (internal-only product change) ==="
echo "worktree: $WORKTREE  HEAD: $HEAD  base: $BASE"
echo

echo "--- cmd 0: changed/untracked src paths vs base (expect only BaseExpressionVisitor.cs) ---"
git status --porcelain -- src
echo

tmp=$(mktemp -d)
: > "$tmp/base-a.txt"
: > "$tmp/head-a.txt"

while IFS= read -r f; do
    git show "$BASE:$f" | tr -d '\r' | grep -E '^[[:space:]]*(public|protected)[[:space:]]' >> "$tmp/base-a.txt"
done < <(git ls-tree -r --name-only "$BASE" -- src | grep '\.cs$')

while IFS= read -r f; do
    [ -f "$f" ] || continue
    tr -d '\r' < "$f" | grep -E '^[[:space:]]*(public|protected)[[:space:]]' >> "$tmp/head-a.txt"
done < <( { git ls-files -- src | grep '\.cs$'; git ls-files --others --exclude-standard -- src | grep '\.cs$'; } | sort -u )

sort "$tmp/base-a.txt" -o "$tmp/base-a.txt"
sort "$tmp/head-a.txt" -o "$tmp/head-a.txt"
echo "--- recipe A: anchored public/protected declaration lines ---"
echo "base lines: $(wc -l < "$tmp/base-a.txt")   head lines: $(wc -l < "$tmp/head-a.txt")"
diff "$tmp/base-a.txt" "$tmp/head-a.txt"
echo "DIFF-EXIT:$?"
echo

echo "--- recipe B: public/protected token count ---"
base_tokens=$(git grep -oE '\b(public|protected)\b' "$BASE" -- 'src/**/*.cs' | wc -l)
head_tokens=$(git grep -oE '\b(public|protected)\b' -- 'src/**/*.cs' | wc -l)
echo "base tokens: $base_tokens   head tokens: $head_tokens"
echo

echo "--- cmd E: InternalsVisibleTo entries (base vs head) ---"
b=$(git grep -h "InternalsVisibleTo" "$BASE" -- src ':!*.cs' | wc -l)
h=$(grep -rh "InternalsVisibleTo" --include=*.csproj --include=*.props --include=*.targets src 2>/dev/null | wc -l)
echo "base entries: $b  head entries: $h"
[ "$b" = "$h" ] && echo "MSBUILD-IVT-ENTRIES-IDENTICAL" || echo "MSBUILD-IVT-ENTRIES-DIFFER"
echo

echo "--- cmd F: src diff vs base (expected: BaseExpressionVisitor.cs body only) ---"
git diff --stat "$BASE" -- src
git diff "$BASE" -- src
echo

if diff -q "$tmp/base-a.txt" "$tmp/head-a.txt" > /dev/null && [ "$b" = "$h" ] \
    && [ "$(git status --porcelain -- src | wc -l)" = "1" ] \
    && [ "$(git status --porcelain -- src | awk '{print $2}')" = "src/nextorm.core/Visitors/BaseExpressionVisitor.cs" ]; then
    echo "VERDICT: PASS - zero public/protected declaration delta; only the internal TryEmitValue body changed."
else
    echo "VERDICT: FAIL - public/protected surface delta or unexpected src path change (see above)."
fi

rm -rf "$tmp"
