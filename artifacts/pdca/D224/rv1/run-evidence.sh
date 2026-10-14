#!/usr/bin/env bash
# D224 (#224) evidence runner. Usage: run-evidence.sh <mode>
#   sql          scoped per-project SELECT SQL-gen sweep: each project is run with a
#                --filter built from the scope.json selectors (never an unfiltered
#                whole-project run); zero selected tests is a failure.
#   mapping      criterion->test mapping: granular filtered runs for R1/R3/R7
#                (build once with --no-build) -> mapping.json
#   baselines    D160 raw byte + SHA-256 sidecar consistency (8 pairs) + invariant test
#   integration  nextorm.integration.tests against real DBs (needs DOCKER_HOST)
#   coverage     test payload wrapped by dotnet-coverage (E05)
#   docs         docs/EN+RU layout-note / stale-example / no-specs-link checks (E06)
#   audit        E07 emitter inventory + suppression scan (CHECK audit input)
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

# Build a scoped --filter for one project from scope.json: OR of
# `FullyQualifiedName~<selector>` for every selector in that project's namespace.
# Projects whose suite is exercised as a shared contract but carries no selector
# (clickhouse.extensions, publicextensibility) get a namespace-scoped filter, not a
# bare project run.
filter_for_project() {
  python3 - "$A/scope.json" "$1" <<'PY'
import json, sys
scope = json.load(open(sys.argv[1], encoding="utf-8"))["scope"]
proj = sys.argv[2].rstrip("/").split("/")[-1]
NS = {
    "nextorm.alias.tests": "NextORM.AliasTests",
    "nextorm.clickhouse.tests": "NextORM.ClickHouse.Tests",
    "nextorm.clickhouse.extensions.tests": "NextORM.ClickHouse.Extensions.Tests",
    "nextorm.core.tests": "NextORM.Core.Tests",
    "nextorm.entityframeworkcore.tests": "NextORM.EntityFrameworkCore.Tests",
    "nextorm.mariadb.tests": "NextORM.MariaDb.Tests",
    "nextorm.mysql.tests": "NextORM.MySql.Tests",
    "nextorm.postgres.tests": "NextORM.Postgres.Tests",
    "nextorm.publicextensibility.tests": "NextORM.PublicExtensibility.Tests",
    "nextorm.sqlite.tests": "NextORM.Sqlite.Tests",
    "nextorm.sqlserver.tests": "NextORM.SqlServer.Tests",
}
prefix = NS.get(proj, "")
sels = [s for s in scope["selectors"] if s.startswith(prefix + ".")]
if not sels:
    sels = [prefix]
print("|".join("FullyQualifiedName~" + s for s in sels))
PY
}

json_escape() { python3 -c 'import json,sys; print(json.dumps(sys.argv[1]))' "$1"; }

mode="${1:-}"
case "$mode" in
  sql)
    fail=0
    : > "$A/E02.json"
    : > "$A/E02-summary.txt"
    for p in "${PROJECTS[@]}"; do
      name="$(basename "$p")"
      log="$A/E02-$name.log"
      filter="$(filter_for_project "$p")"
      dotnet test "$p" -c Debug --no-build --filter "$filter" >"$log" 2>&1
      ec=$?
      total=$(grep -oP 'total: \K[0-9]+' "$log" | tail -1)
      failed=$(grep -oP 'failed: \K[0-9]+' "$log" | tail -1)
      skipped=$(grep -oP 'skipped: \K[0-9]+' "$log" | tail -1)
      printf '%s failed:%s total:%s skipped:%s exit=%s\n' \
        "$name" "${failed:-?}" "${total:-?}" "${skipped:-?}" "$ec" | tee -a "$A/E02-summary.txt"
      printf '{"project":"%s","command":["dotnet","test","%s","-c","Debug","--no-build","--filter",%s],"exit_code":%s,"selected_count":%s,"failed":%s,"skipped":%s,"log":"artifacts/pdca/D224/rv1/E02-%s.log"}\n' \
        "$p" "$p" "$(json_escape "$filter")" "$ec" "${total:-0}" "${failed:-0}" "${skipped:-0}" "$name" >> "$A/E02.json"
      [ "$ec" -eq 0 ] || fail=1
      [ "${total:-0}" -gt 0 ] || { echo "ZERO SELECTED: $p"; fail=1; }
    done
    exit $fail
    ;;
  mapping)
    python3 "$A/run-mapping.py"
    exit $?
    ;;
  baselines)
    dir=tests/nextorm.sqlite.tests/Baselines/D160/positional/sqlite
    fail=0
    for f in "$dir"/*.sql; do
      n="$(basename "$f" .sql)"
      computed=$(sha256sum "$f" | cut -d' ' -f1 | tr 'a-f' 'A-F')
      sidecar=$(cut -d' ' -f1 "$dir/$n.sha256")
      [ "$computed" = "$sidecar" ] || { echo "MISMATCH $n"; fail=1; }
      grep -q $'\r' "$f" && { echo "CR IN BASELINE $n"; fail=1; }
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
    # No whitespace-only line inside a ```sql fence (D224 rewrite artefact).
    python3 - "$ROOT" <<'PY' || fail=1
import re, subprocess, sys
from pathlib import Path
root = Path(sys.argv[1])
files = subprocess.run(["git", "-C", str(root), "ls-files", "docs"],
                       capture_output=True, text=True).stdout.split()
fence = re.compile(r"^\s*```(\w*)\s*$")
bad = 0
for rel in files:
    if not rel.endswith(".md") or rel.startswith(("docs/specs/", "docs/api/")):
        continue
    p = root / rel
    if not p.is_file():
        continue
    infence, info = False, ""
    for n, line in enumerate(p.read_bytes().decode("utf-8-sig").split("\r\n"), 1):
        if fence.match(line):
            infence, info = (not infence), (fence.match(line).group(1).lower() if not infence else "")
            continue
        if infence and info == "sql" and line != "" and line.strip() == "":
            print(f"WHITESPACE-ONLY SQL LINE {rel}:{n}")
            bad += 1
sys.exit(1 if bad else 0)
PY
    exit $fail
    ;;
  audit)
    {
      echo "# D224 E07 emitter inventory (generated $(date -u +%Y-%m-%dT%H:%M:%SZ))"
      echo
      echo "## AppendLine() sites in src/"
      rg -n --glob '*.cs' 'AppendLine' src || true
      echo
      echo "## Environment.NewLine sites in src/"
      rg -n --glob '*.cs' 'Environment\.NewLine' src || true
      echo
      echo "## Literal backslash-r sites in src/"
      rg -n --glob '*.cs' -F '\r' src || true
    } > "$A/E07-emitters.txt"
    {
      echo "# D224 E07 suppression scan (generated $(date -u +%Y-%m-%dT%H:%M:%SZ))"
      do_sha="$(git rev-parse HEAD)"
      echo "# scope: added (+) lines of the DO commit $do_sha vs its parent, tests/** + src/**"
      diff="$(git diff "${do_sha}^" "$do_sha" -- tests src)"
      # Every added line must be counted even when indented, so each pattern is
      # anchored as ^\+[[:space:]]*<pat>: added-line marker + optional indentation.
      re_escape() { printf '%s' "$1" | sed 's/[][\\.^$*+?(){}|]/\\&/g'; }
      count_added() { printf '%s\n' "$diff" | grep -cE "^\+[[:space:]]*$(re_escape "$1")" || true; }
      list_added() { printf '%s\n' "$diff" | grep -E "^\+[[:space:]]*$(re_escape "$1")" | head -20 || true; }
      added="$(git diff "${do_sha}^" "$do_sha" --numstat -- tests src | awk '{s+=$1} END {print s+0}')"
      echo "# added-line denominator (tests src diff at $do_sha) = $added"
      echo "# match anchor: ^\+[[:space:]]*<pat>"
      total=0
      for pat in 'Skip =' '[Fact(Skip' '#pragma warning disable' 'Assert.True(true)' '.Only(' 'SuppressMessage' 'ExcludeFromCodeCoverage' '[Obsolete'; do
        n="$(count_added "$pat")"
        printf '## %-28s added=%s\n' "$pat" "$n"
        list_added "$pat"
        total=$((total + n))
      done
      n="$(printf '%s\n' "$diff" | grep -cE '^\+[[:space:]]*.*catch[[:space:]]*\{[[:space:]]*\}' || true)"
      printf '## %-28s added=%s\n' 'empty catch {}' "$n"
      printf '%s\n' "$diff" | grep -E '^\+[[:space:]]*.*catch[[:space:]]*\{[[:space:]]*\}' | head -20 || true
      total=$((total + n))
      echo "# TOTAL_NEW_SUPPRESSIONS=$total"
    } > "$A/E07-suppression.txt"
    echo "E07 audit input written: $A/E07-emitters.txt $A/E07-suppression.txt"
    exit 0
    ;;
  *)
    echo "usage: $0 {sql|mapping|baselines|integration|coverage|docs|audit}" >&2
    exit 2
    ;;
esac
