#!/usr/bin/env bash
# Deterministic check of the pdca-collection git model (spec §6):
#   - per-task branch chain inside a group, auto-commit per task
#   - group branch fast-forwarded to the last task tip
#   - `git merge --no-ff` of each group branch into the current branch
#   - a conflicting merge is detected, not silently ignored
# No product code, no network, temp repos only.
#
# Branch naming (must be siblings — git forbids a ref that is a prefix of
# another ref):  group `collection/<id>/group-<g>`, task `collection/<id>/task-<g>-<k>`.
set -euo pipefail

say() { printf '%s\n' "$*"; }

# ---------- Case 0: artifact branch-naming consistency ----------
# The skill and the lane agent must not use the illegal nested scheme
# `collection/<id>/<group>/t<k>` (a ref that is a prefix of another ref).
for a in "$HOME/.config/opencode/skills/pdca-collection/SKILL.md" \
         "$HOME/.config/opencode/agents/lane.md"; do
  if [ ! -f "$a" ]; then say "SKIP: $a not installed"; continue; fi
  if rg -q 'collection/<id>/<group>/' "$a"; then
    say "FAIL: stale nested branch naming in $a"; exit 1
  fi
  rg -q 'collection/<id>/group-<g>|collection/<id>/task-<g>-<k>' "$a" \
    || { say "FAIL: new branch naming missing in $a"; exit 1; }
done
say "OK: artifact branch naming consistent"

# ---------- Case 1: happy path ----------
base=$(mktemp -d); repo="$base/repo"
mkdir "$repo"; git -C "$repo" init -q -b main
git -C "$repo" config user.email t@t; git -C "$repo" config user.name t
echo base > "$repo/base.txt"; git -C "$repo" add -A; git -C "$repo" commit -qm base

for g in A B; do
  git -C "$repo" worktree add -q -b "collection/x/group-$g" "$base/wt$g" main
done

# Group A: task branches chain t1 -> t2; group branch ff-equal to last tip.
(
  cd "$base/wtA"
  git checkout -q -b "collection/x/task-A-1"
  echo a1 > a1.txt; git add -A; git commit -qm "A t1"
  git branch -f collection/x/group-A HEAD
  git checkout -q -b "collection/x/task-A-2"
  echo a2 > a2.txt; git add -A; git commit -qm "A t2"
  git branch -f collection/x/group-A HEAD
)

# Group B: task branches chain t1 -> t2.
(
  cd "$base/wtB"
  git checkout -q -b "collection/x/task-B-1"
  echo b1 > b1.txt; git add -A; git commit -qm "B t1"
  git branch -f collection/x/group-B HEAD
  git checkout -q -b "collection/x/task-B-2"
  echo b2 > b2.txt; git add -A; git commit -qm "B t2"
  git branch -f collection/x/group-B HEAD
)

cd "$repo"

# Branch chain + group tip == last task tip.
git rev-parse --verify -q collection/x/group-A >/dev/null
git rev-parse --verify -q collection/x/task-A-1   >/dev/null
git rev-parse --verify -q collection/x/task-A-2   >/dev/null
git merge-base --is-ancestor collection/x/task-A-1 collection/x/task-A-2
[ "$(git rev-parse collection/x/group-A)" = "$(git rev-parse collection/x/task-A-2)" ]
[ "$(git rev-parse collection/x/group-B)" = "$(git rev-parse collection/x/task-B-2)" ]

git merge --no-ff -q collection/x/group-A -m "merge group-A"
git merge --no-ff -q collection/x/group-B -m "merge group-B"

merges=$(git rev-list --merges --count main)
ok=1
for f in a1.txt a2.txt b1.txt b2.txt; do [ -f "$f" ] || ok=0; done
[ "$merges" -eq 2 ] || ok=0
if [ "$ok" -eq 1 ]; then
  say "OK: group-A merged, group-B merged, 2 merge commits"
else
  say "FAIL: happy path (merges=$merges)"; exit 1
fi

# ---------- Case 2: conflicting merges ----------
base2=$(mktemp -d); repo2="$base2/repo"
mkdir "$repo2"; git -C "$repo2" init -q -b main
git -C "$repo2" config user.email t@t; git -C "$repo2" config user.name t
printf 'line\n' > "$repo2/shared.txt"; git -C "$repo2" add -A; git -C "$repo2" commit -qm base
for g in A B; do
  git -C "$repo2" worktree add -q -b "collection/x/group-$g" "$base2/wt$g" main
done
( cd "$base2/wtA"; echo 'A-changed' > shared.txt; git add -A; git commit -qm "A change" )
( cd "$base2/wtB"; echo 'B-changed' > shared.txt; git add -A; git commit -qm "B change" )

cd "$repo2"
git merge --no-ff -q collection/x/group-A -m "merge group-A"
if git merge --no-ff collection/x/group-B -m "merge group-B" >/dev/null 2>&1; then
  say "FAIL: expected conflict, merge group-B succeeded"; exit 1
else
  git merge --abort 2>/dev/null || true
  say "OK: conflict detected, group marked failed"
fi
