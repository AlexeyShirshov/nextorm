#!/usr/bin/env bash
# D226 / rv1 — plan/evidence audit (the plan's A slot). Exit 0 only when every named slot exists,
# EN/RU guide parity holds, the scoped CRLF check passes, no public doc links to docs/specs, and the
# T53 freeze-candidate registration is present.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
R="$REPO/artifacts/pdca/D226/rv1"
STATUS="$REPO/docs/specs/status/D226-records-affected-1.md"
fail=0

check() {
  if [ -e "$1" ]; then
    echo "OK   ${1#"$R"/}"
  else
    echo "MISS ${1#"$R"/}"
    fail=1
  fi
}

echo "== named artifact slots =="
for a in preflight.md d1-observation.md release-probe.json provider-matrix.json branch-delta.json \
         doc-review.md invariants.json test-map.json verify-coverage.sh verify-mutations.sh \
         mutation.log mutation-report.json baseline/coverage.cobertura.xml coverage.xml \
         coverage-report/Summary.txt build.log unit.log integration.log audit.log evidence.json \
         manifest.json logs/boundary-integration-clickhouserecordsaffected.log \
         logs/boundary-integration-recordsaffected.log logs/coverage.log; do
  check "$R/$a"
done

echo "== EN/RU guide parity + CRLF (docs stream owns the files; this only verifies) =="
for pair in "docs/guide/12-raw-sql.md:docs/ru/guide/12-raw-sql.md" "docs/guide/23-sql-batch.md:docs/ru/guide/23-sql-batch.md"; do
  en="${pair%%:*}"; ru="${pair##*:}"
  mentions_en="$(grep -c RecordsAffected "$REPO/$en")"
  mentions_ru="$(grep -c RecordsAffected "$REPO/$ru")"
  if [ "$mentions_en" = "$mentions_ru" ] && [ "$mentions_en" -gt 0 ]; then
    echo "OK   parity $en=$mentions_en $ru=$mentions_ru"
  else
    echo "MISS parity $en=$mentions_en $ru=$mentions_ru"
    fail=1
  fi
  if file "$REPO/$en" "$REPO/$ru" | grep -q CRLF; then
    echo "OK   CRLF $en, $ru"
  else
    echo "MISS CRLF $en, $ru"
    fail=1
  fi
done

echo "== public docs must not link to internal specs =="
if grep -rq "docs/specs" "$REPO/docs/guide" "$REPO/docs/ru/guide" 2>/dev/null; then
  echo "MISS a public guide links to docs/specs"
  fail=1
else
  echo "OK   no docs/specs links in the public guides"
fi

echo "== scoped CRLF check on the T226-touched sources =="
for f in src/nextorm.core/DataContext/CommandReaderOwner.cs \
         src/nextorm.core/DataContext/ProcedureResult.cs \
         src/nextorm.core/Builders/BatchBuilder.cs \
         src/nextorm.core/DataContext/BatchRunner.cs \
         tests/nextorm.core.tests/RecordsAffectedTests.cs \
         tests/nextorm.sqlite.tests/RawCommandTests.cs \
         tests/nextorm.sqlite.tests/RawResultSetTraversalTests.cs \
         tests/nextorm.sqlite.tests/BatchMultipleResultTests.cs \
         tests/nextorm.integration.tests/CommonTestSuite.Raw.cs \
         tests/nextorm.integration.tests/CommonTestSuite.BatchMultipleResults.cs \
         tests/nextorm.integration.tests/ClickHouseRecordsAffectedTests.cs \
         tests/nextorm.integration.tests/SqlServerSpecificTests.cs \
         tests/nextorm.integration.tests/PostgresSpecificTests.cs \
         tests/nextorm.integration.tests/Providers/ITestProvider.cs \
         tests/nextorm.integration.tests/Providers/ClickHouseTestProvider.cs \
         docs/specs/status/D226-records-affected-1.md; do
  if file "$REPO/$f" | grep -q CRLF; then
    echo "OK   CRLF $f"
  else
    echo "MISS CRLF $f"
    fail=1
  fi
done

echo "== T53 freeze-candidate registration (this task's status file only) =="
if grep -q "T53 freeze candidates — D226" "$STATUS"; then
  echo "OK   T53 registration present"
else
  echo "MISS T53 registration"
  fail=1
fi

echo "== variant/branch mapping artifacts =="
for a in branch-delta.json provider-matrix.json test-map.json; do
  python3 -c "import json,sys; json.load(open(sys.argv[1], encoding='utf-8'))" "$R/$a" \
    && echo "OK   valid JSON $a" || { echo "MISS valid JSON $a"; fail=1; }
done

echo "plan_evidence_fail=$fail"
exit "$fail"
