#!/usr/bin/env bash
# Cycle 9 helper for integration-branch-coverage.
#
# Subcommands:
#   focused sqlite|clickhouse|sqlserver   targeted -class runs (logs + rc under rv1/)
#   scope                                re-record the protected-tree snapshot and diff it
#
# No subcommand prints usage and exits 0.
#
# Evidence root: .pdca/integration-branch-coverage-9/rv1/

set -uo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
RV1="$ROOT/.pdca/integration-branch-coverage-9/rv1"
PROJECT="tests/nextorm.integration.tests"
DOCKER_SOCK="unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock"
SKIP_SQLITE="postgres,sqlserver,mysql,mariadb,clickhouse"
EXPECTED_SRC_SHA="c9ce304778ebcc1dba56366068fd784d3aa28f98"

usage() {
  cat <<'EOF'
usage: integration-branch-coverage-c9.sh <subcommand>

  focused sqlite       PlanEqualityDifferentialBranchCoverage4Tests + SqlitePlanKeyBranchCoverage5Tests
                       (SQLite only: NEXTORM_SKIP_PROVIDERS=postgres,sqlserver,mysql,mariadb,clickhouse)
  focused clickhouse   ClickHouseScalarFunctionsTests (live container, DOCKER_HOST, no skip override)
  focused sqlserver    SqlServerDialectBranchCoverage3Tests (live container, DOCKER_HOST, no skip override)
  scope                re-record git/scope state -> rv1/scope-after.txt and diff against rv1/scope-before.txt

No subcommand: print this usage and exit 0.
EOF
}

run_focused() { # $1 = provider, $2 = FQCN
  local provider="$1" class="$2"
  local name="focused-${provider}-${class##*.}"
  local log="$RV1/${name}.log"
  (
    cd "$ROOT" || exit 2
    if [ "$provider" = "sqlite" ]; then
      export NEXTORM_SKIP_PROVIDERS="$SKIP_SQLITE"
    else
      unset NEXTORM_SKIP_PROVIDERS
      export DOCKER_HOST="$DOCKER_SOCK"
    fi
    dotnet run --project "$PROJECT" -c Debug --no-build -- -noColor -class "$class"
  ) >"$log" 2>&1
  local rc=$?
  printf '%s\n' "$rc" >"$RV1/${name}.rc"
  echo "$name rc=$rc log=$log"
  return "$rc"
}

scope_snapshot() { # $1 = output file
  local out="$1"
  {
    echo "# cycle 9 protected-tree snapshot  $(date -u +%Y-%m-%dT%H:%M:%SZ)"
    echo "## git status --porcelain"
    git -C "$ROOT" status --porcelain
    echo "## git status --porcelain src/"
    git -C "$ROOT" status --porcelain src/
    echo "## git rev-parse HEAD:src"
    git -C "$ROOT" rev-parse HEAD:src
    echo "## git rev-parse HEAD"
    git -C "$ROOT" rev-parse HEAD
    echo "## sha256sum AGENTS.md .github/workflows/dotnet.yml"
    ( cd "$ROOT" && sha256sum AGENTS.md .github/workflows/dotnet.yml )
  } >"$out" 2>&1
}

cmd_scope() {
  local before="$RV1/scope-before.txt"
  local after="$RV1/scope-after.txt"
  local diff="$RV1/scope.diff"
  if [ ! -f "$before" ]; then
    echo "scope: missing $before (bootstrap record)" >&2
    return 2
  fi
  scope_snapshot "$after"
  diff -u "$before" "$after" >"$diff" 2>&1
  local drc=$?
  printf '%s\n' "$drc" >"$RV1/scope.diff.rc"
  echo "scope: diff rc=$drc (1 = files differ, expected as new evidence is added) -> $diff"

  local src_status src_sha
  src_status=$(git -C "$ROOT" status --porcelain src/)
  src_sha=$(git -C "$ROOT" rev-parse HEAD:src)
  if [ -n "$src_status" ]; then
    echo "scope: FAIL src/ is dirty:" >&2
    printf '%s\n' "$src_status" >&2
    return 1
  fi
  if [ "$src_sha" != "$EXPECTED_SRC_SHA" ]; then
    echo "scope: FAIL HEAD:src=$src_sha expected $EXPECTED_SRC_SHA" >&2
    return 1
  fi
  echo "scope: PASS src/ clean, HEAD:src=$src_sha"
  return 0
}

main() {
  local sub="${1:-}"
  case "$sub" in
    "")
      usage
      exit 0
      ;;
    -h|--help|help)
      usage
      exit 0
      ;;
    focused)
      local provider="${2:-}"
      case "$provider" in
        sqlite)
          run_focused sqlite "NextORM.Integration.Tests.PlanEqualityDifferentialBranchCoverage4Tests" || exit $?
          run_focused sqlite "NextORM.Integration.Tests.SqlitePlanKeyBranchCoverage5Tests" || exit $?
          ;;
        clickhouse)
          run_focused clickhouse "NextORM.Integration.Tests.ClickHouseScalarFunctionsTests" || exit $?
          ;;
        sqlserver)
          run_focused sqlserver "NextORM.Integration.Tests.SqlServerDialectBranchCoverage3Tests" || exit $?
          ;;
        *)
          echo "focused: provider must be sqlite|clickhouse|sqlserver" >&2
          usage >&2
          exit 2
          ;;
      esac
      ;;
    scope)
      cmd_scope || exit $?
      ;;
    *)
      echo "unknown subcommand: $sub" >&2
      usage >&2
      exit 2
      ;;
  esac
}

mkdir -p "$RV1"
main "$@"
