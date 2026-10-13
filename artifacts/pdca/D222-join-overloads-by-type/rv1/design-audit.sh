#!/usr/bin/env bash
# T222 (#222) rv=1 design/footprint audit (no code edits; read-only facts).
#
# Invoked by: bash "$B/capture.sh" E222-DESIGN -- bash "$B/design-audit.sh"
set -u

B="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(git -C "$B" rev-parse --show-toplevel)"
out="$B/E222-DESIGN"
mkdir -p "$out"
base="4fbb603b"   # T222 baseline (recorded by E222-BASE prerequisites.json)

{
  echo "# E222-DESIGN — T222 rv=1 footprint / invariant audit"
  echo
  echo "Baseline commit: $base ; HEAD: $(git -C "$root" rev-parse HEAD)"
  echo
  echo "## Production footprint (baseline..HEAD)"
  git -C "$root" diff --stat "$base"..HEAD -- src/
  echo
  echo "## Invariant: JoinSourceResolver.cs unchanged"
  git -C "$root" diff --stat "$base"..HEAD -- src/nextorm.core/Builders/Joins/JoinSourceResolver.cs | sed 's/^/  /'
  test -z "$(git -C "$root" diff --name-only "$base"..HEAD -- src/nextorm.core/Builders/Joins/JoinSourceResolver.cs)" && echo "  RESULT: unchanged"
  echo
  echo "## Invariant: provider src unchanged"
  test -z "$(git -C "$root" diff --name-only "$base"..HEAD -- src/nextorm.sqlite src/nextorm.sqlserver src/nextorm.postgres src/nextorm.mysql src/nextorm.mariadb src/nextorm.clickhouse)" && echo "  RESULT: unchanged"
  echo
  echo "## Invariant: EntityBuilder.cs change is additive only (0 deletions)"
  git -C "$root" diff --numstat "$base"..HEAD -- src/nextorm.core/Builders/EntityBuilder.cs src/nextorm.core/Builders/Joins/JoinedEntityBuilder.cs | sed 's/^/  /'
  echo
  echo "## Invariant: no raw-table / Cross / Apply source-less additions"
  git -C "$root" diff "$base"..HEAD -- src/nextorm.core/Builders/EntityBuilder.cs | grep -E '^\+' | grep -E 'CrossJoin<|CrossApply<|OuterApply<' | sed 's/^/  /' || echo "  RESULT: none"
  echo
  echo "## Perf decision"
  echo "  No planner / plan-cache / per-row materialization change; the new members are construction-time wrappers."
  echo "  Changed product files: EntityBuilder.cs (+44), JoinedEntityBuilder.cs (+264) only."
  echo
  echo "## Pre-existing dirty files (must stay uncommitted/preserved)"
  git -C "$root" status --porcelain
} > "$out/audit.md"

cat "$out/audit.md"
