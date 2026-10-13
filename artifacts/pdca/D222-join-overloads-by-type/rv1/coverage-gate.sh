#!/usr/bin/env bash
# T222 (#222) rv=1 merged coverage gate.
#
# Invoked by: bash "$B/capture.sh" E222-COVERAGE -- bash "$B/coverage-gate.sh"
#
# Produces three views and gates on the CI-comparable one:
#   1. contract subset  : the four in-scope xmls (core, sqlite, postgres, sqlserver).
#   2. full sweep       : every executed test project's xml merged (adds mysql/mariadb/
#                         clickhouse/integration, which also exercise the in-scope assemblies).
#   3. CI cobertura     : the whole-solution test run under `-f cobertura` (this is the format
#                         that carries branch coverage; `-f xml` does not).
# The contract's `-f xml` path cannot express branch coverage, so the authoritative line+branch
# gate uses (3); (1) and (2) are printed for provenance.
set -u

B="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
out="$B/E222-COVERAGE"
mkdir -p "$out"

# 1. contract subset
dotnet tool run dotnet-coverage merge \
  "$B/coverage/core.xml" "$B/coverage/sqlite.xml" "$B/coverage/postgres.xml" "$B/coverage/sqlserver.xml" \
  -f xml -o "$B/coverage/merged.xml" || exit $?
dotnet tool run reportgenerator \
  "-reports:$B/coverage/merged.xml" "-targetdir:$out/report" \
  "-reporttypes:Cobertura;TextSummary" || exit $?

# 2. full sweep (all executed test projects)
dotnet tool run dotnet-coverage merge \
  "$B/coverage/core.xml" "$B/coverage/sqlite.xml" "$B/coverage/sqlserver.xml" "$B/coverage/postgres.xml" \
  "$B/coverage/mysql.xml" "$B/coverage/mariadb.xml" "$B/coverage/clickhouse.xml" "$B/coverage/integration.xml" \
  -f xml -o "$B/coverage/merged-all.xml" || exit $?
dotnet tool run reportgenerator \
  "-reports:$B/coverage/merged-all.xml" "-targetdir:$out/report-all" \
  "-reporttypes:Cobertura;TextSummary" || exit $?

# 3. CI-style cobertura (whole solution tests; carries branch coverage)
if [[ ! -f "$B/coverage/all.cobertura.xml" ]]; then
  echo "FAIL: missing $B/coverage/all.cobertura.xml (CI-style cobertura collect)"
  exit 2
fi
dotnet tool run reportgenerator \
  "-reports:$B/coverage/all.cobertura.xml" "-targetdir:$out/report-all-cobertura" \
  "-reporttypes:Cobertura;TextSummary" || exit $?

sub_line="$(grep -oP 'Line coverage: \K[0-9.]+' "$out/report/Summary.txt" | head -1)"
full_line="$(grep -oP 'Line coverage: \K[0-9.]+' "$out/report-all/Summary.txt" | head -1)"
ci_line="$(grep -oP 'Line coverage: \K[0-9.]+' "$out/report-all-cobertura/Summary.txt" | head -1)"
ci_branch="$(grep -oP 'Branch coverage: \K[0-9.]+' "$out/report-all-cobertura/Summary.txt" | head -1)"

echo "contract-subset(4 xml) line=$sub_line% (below-threshold note: subset omits test projects that cover the in-scope assemblies)"
echo "full-sweep(8 xml)      line=$full_line%"
echo "ci-cobertura(all tests) line=$ci_line% branch=$ci_branch%"

rc=0
awk -v c="$ci_line" 'BEGIN { exit (c + 0 < 85) }' || { echo "FAIL: line $ci_line% < 85%"; rc=1; }
awk -v c="$ci_branch" 'BEGIN { exit (c + 0 < 75) }' || { echo "FAIL: branch $ci_branch% < 75%"; rc=1; }
exit $rc
