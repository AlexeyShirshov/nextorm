#!/usr/bin/env bash
# T222 (#222) rv=1 six-provider SQL-generation coverage sweep.
#
# Invoked by: bash "$B/capture.sh" E222-SQL -- bash "$B/sql-sweep.sh"
# Runs the full SQL-generation suite of each provider under coverage and records a
# per-provider receipt. Returns non-zero as soon as any provider run fails.
set -u

B="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
mkdir -p "$B/coverage"

providers=(sqlite sqlserver postgres mysql mariadb clickhouse)
rc=0
for p in "${providers[@]}"; do
  echo "=== E222-SQL-$p ==="
  bash "$B/capture.sh" "E222-SQL-$p" -- dotnet tool run dotnet-coverage collect \
    "dotnet test tests/nextorm.$p.tests -c Debug" \
    -s coverage.settings.xml -f xml -o "$B/coverage/$p.xml"
  prc=$?
  echo "E222-SQL-$p exit=$prc"
  if [[ $prc -ne 0 ]]; then
    rc=$prc
  fi
done

exit $rc
