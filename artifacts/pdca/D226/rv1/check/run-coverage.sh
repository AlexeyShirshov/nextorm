#!/usr/bin/env bash
set -u
cd /home/alex/sources/nextorm
OUT=artifacts/pdca/D226/rv1/check
mkdir -p "$OUT/coverage"
rm -f "$OUT/coverage/coverage.cobertura.xml"

dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura \
  -o "$OUT/coverage/coverage.cobertura.xml" \
  bash "$OUT/coverage-inner.sh" \
  > "$OUT/coverage-collect.log" 2>&1
echo "COLLECT_EXIT=$?" >> "$OUT/coverage-collect.log"

dotnet tool run reportgenerator \
  -reports:"$OUT/coverage/coverage.cobertura.xml" \
  -targetdir:"$OUT/coverage-report" \
  -reporttypes:"Html;TextSummary;Cobertura" \
  -riskhotspotassemblyfilters:"+nextorm.*" \
  > "$OUT/coverage-report.log" 2>&1
echo "REPORT_EXIT=$?" >> "$OUT/coverage-report.log"
touch "$OUT/COVERAGE_DONE"
