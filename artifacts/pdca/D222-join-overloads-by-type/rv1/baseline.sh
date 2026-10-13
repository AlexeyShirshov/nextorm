#!/usr/bin/env bash
# T222 (#222) rv=1 shared dirty baseline + prerequisites.
#
# Invoked by: bash "$B/capture.sh" E222-BASE -- bash "$B/baseline.sh"
set -u

B="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(git -C "$B" rev-parse --show-toplevel)"
out="$B/E222-BASE"
mkdir -p "$out"

git -C "$root" status --short > "$out/git-status-short.txt"
git -C "$root" status --porcelain=v1 > "$out/git-status-porcelain.txt"
git -C "$root" diff -- src/nextorm.core/Builders docs/guide/02-joins.md docs/ru/guide/02-joins.md docs/specs/status/collection-1.0.9-todo.md > "$B/baseline.patch"

head="$(git -C "$root" rev-parse HEAD)"
branch="$(git -C "$root" rev-parse --abbrev-ref HEAD)"
modified="$(git -C "$root" status --porcelain=v1 | awk '{print $2}' | paste -sd, -)"

cat > "$out/prerequisites.json" <<EOF
{
  "task": "T222",
  "issue": "#222",
  "rv": 1,
  "cycle": 1,
  "plan_revision": 1,
  "branch": "$branch",
  "base_head": "$head",
  "tree_revision": "$head",
  "dirty_paths": "$modified",
  "shared_dirty_preserved": [
    "docs/assets/diagrams/nextorm-command-execution.svg",
    "docs/ru/assets/diagrams/nextorm-command-execution.svg"
  ],
  "t213_handoff": "confirmed (collection status: T213 done, plan r=6 rv=5; Builders/Joins/** handed off)",
  "parallel_writer": false,
  "single_lane": true,
  "t53_state": "held",
  "write_footprint_unchanged_before_do": true
}
EOF

echo "baseline: head=$head branch=$branch"
echo "dirty: $modified"
echo "baseline.patch -> $B/baseline.patch"
