#!/usr/bin/env bash
# D226 / rv1 — coverage threshold verifier (the plan's `$R/verify-coverage.sh` slot).
#
# Usage: bash artifacts/pdca/D226/rv1/verify-coverage.sh <coverage.xml> [min_line] [min_branch]
# Runs reportgenerator next to the XML, prints line/branch coverage and exits 1 when either
# percentage is below its threshold (same arithmetic as .github/workflows/dotnet.yml).
set -uo pipefail

XML="${1:?usage: verify-coverage.sh <coverage.xml> [min_line] [min_branch]}"
MIN_LINE="${2:-85}"
MIN_BRANCH="${3:-75}"
OUT="$(dirname "$XML")/coverage-report"

dotnet tool run reportgenerator \
  -reports:"$XML" \
  -targetdir:"$OUT" \
  -reporttypes:"TextSummary;Cobertura" \
  -riskhotspotassemblyfilters:"+nextorm.*" > /dev/null 2>&1
report_exit=$?

SUMMARY="$OUT/Summary.txt"
if [ ! -f "$SUMMARY" ]; then
  echo "reportgenerator failed (exit $report_exit) and produced no Summary.txt"
  exit 1
fi

LINE="$(grep -oP 'Line coverage: \K[0-9.]+' "$SUMMARY")"
BRANCH="$(grep -oP 'Branch coverage: \K[0-9.]+' "$SUMMARY")"

FAIL=0
awk -v c="$LINE" -v m="$MIN_LINE" 'BEGIN { exit (c + 0 < m) }' || FAIL=1
awk -v c="$BRANCH" -v m="$MIN_BRANCH" 'BEGIN { exit (c + 0 < m) }' || FAIL=1

echo "coverage_xml: $XML"
echo "reportgenerator_exit: $report_exit"
echo "Line coverage: ${LINE}% (threshold: ${MIN_LINE}%)"
echo "Branch coverage: ${BRANCH}% (threshold: ${MIN_BRANCH}%)"
echo "threshold_fail: $FAIL"
exit "$FAIL"
