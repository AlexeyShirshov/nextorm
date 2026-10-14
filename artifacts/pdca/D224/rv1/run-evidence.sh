#!/usr/bin/env bash
# D224 (#224) evidence runner. Usage: run-evidence.sh <mode>
#   sql          full unfiltered SQL-gen sweep of the 11 affected test projects
#   baselines    D160 raw byte + SHA-256 sidecar consistency (8 pairs) + invariant test
#   integration  nextorm.integration.tests against real DBs (needs DOCKER_HOST)
#   coverage     test payload wrapped by dotnet-coverage (E05)
#   docs         docs/EN+RU layout-note / stale-example / no-specs-link checks (E06)
#   audit        E07 placeholder filled by CHECK
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
A="$ROOT/artifacts/pdca/D224/rv1"
cd "$ROOT"

PROJECTS=(
  tests/nextorm.core.tests tests/nextorm.sqlite.tests tests/nextorm.postgres.tests
  tests/nextorm.sqlserver.tests tests/nextorm.mysql.tests tests/nextorm.mariadb.tests
  tests/nextorm.clickhouse.tests tests/nextorm.clickhouse.extensions.tests
  tests/nextorm.entityframeworkcore.tests tests/nextorm.publicextensibility.tests
  tests/nextorm.alias.tests
)

mode="${1:-}"
case "$mode" in
  sql)
    fail=0
    : > "$A/E02.json"
    for p in "${PROJECTS[@]}"; do
      name="$(basename "$p")"
      log="$A/E02-$name.log"
      dotnet test "$p" -c Debug --no-build >"$log" 2>&1
      ec=$?
      total=$(grep -oP 'total: \K[0-9]+' "$log" | tail -1)
      failed=$(grep -oP 'failed: \K[0-9]+' "$log" | tail -1)
      skipped=$(grep -oP 'skipped: \K[0-9]+' "$log" | tail -1)
      printf '%s failed:%s total:%s skipped:%s exit=%s\n' "$name" "${failed:-?}" "${total:-?}" "${skipped:-?}" "$ec" | tee -a "$A/E02-summary.txt"
      printf '{"project":"%s","command":["dotnet","test","%s","-c","Debug","--no-build"],"exit_code":%s,"selected_count":%s,"failed":%s,"skipped":%s,"log":"artifacts/pdca/D224/rv1/E02-%s.log"}\n' \
        "$p" "$p" "$ec" "${total:-0}" "${failed:-0}" "${skipped:-0}" "$name" >> "$A/E02.json"
      [ "$ec" -eq 0 ] || fail=1
    done
    exit $fail
    ;;
  baselines)
    dir=tests/nextorm.sqlite.tests/Baselines/D160/positional/sqlite
    fail=0
    for f in "$dir"/*.sql; do
      n="$(basename "$f" .sql)"
      computed=$(sha256sum "$f" | cut -d' ' -f1 | tr 'a-f' 'A-F')
      sidecar=$(cut -d' ' -f1 "$dir/$n.sha256")
      [ "$computed" = "$sidecar" ] || { echo "MISMATCH $n"; fail=1; }
    done
    dotnet test tests/nextorm.sqlite.tests -c Debug --no-build \
      --filter "FullyQualifiedName~PositionalChainSqlInvariantTests" >"$A/E03-invariant.log" 2>&1
    ec=$?
    [ "$ec" -eq 0 ] || fail=1
    echo "baselines fail=$fail invariant_exit=$ec"
    exit $fail
    ;;
  integration)
    DOCKER_HOST="${DOCKER_HOST:-unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock}" \
      dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor >"$A/integration.log" 2>&1
    exit $?
    ;;
  coverage)
    dotnet test nextorm.slnx -c Debug --no-build --verbosity normal
    exit $?
    ;;
  docs)
    fail=0
    grep -q '`FROM`/`JOIN`' docs/advanced/limitations.md || { echo "EN layout note missing"; fail=1; }
    grep -q '`FROM`/`JOIN`' docs/ru/advanced/limitations.md || { echo "RU layout note missing"; fail=1; }
    [ -f docs/guide/23-sql-batch.md ] || { echo "EN 23 missing"; fail=1; }
    [ -f docs/ru/guide/23-sql-batch.md ] || { echo "RU 23 missing"; fail=1; }
    if grep -rnE '\]\([^)]*docs/specs/|/docs/specs/' docs/guide docs/advanced docs/scalar-functions docs/ru 2>/dev/null; then
      echo "public docs link to spec"; fail=1
    fi
    exit $fail
    ;;
  audit)
    echo "E07 audit is produced by CHECK (Roslyn emitter inventory + reviewed diff)."
    exit 3
    ;;
  *)
    echo "usage: $0 {sql|baselines|integration|coverage|docs|audit}" >&2
    exit 2
    ;;
esac
