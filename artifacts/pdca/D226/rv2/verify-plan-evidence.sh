#!/usr/bin/env bash
# D226 / rv2 — plan/evidence audit (P226-R2-06). Exit 0 only with zero MISS: every rv2 slot present,
# production bytes unchanged, rv1 frozen, T53 registration intact, mapping JSONs valid, all frozen
# mutants killed and all six negative controls intended.
set -uo pipefail

REPO="/home/alex/sources/nextorm"
R="$REPO/artifacts/pdca/D226/rv2"
R1="$REPO/artifacts/pdca/D226/rv1"
S="$REPO/docs/specs/status/D226-records-affected-1.md"
fail=0
miss=0

check() {
  if [ -e "$1" ]; then echo "OK   ${1#"$R"/}"
  else echo "MISS ${1#"$R"/}"; fail=1; miss=$((miss + 1)); fi
}

echo "== rv2 artifact slots =="
for a in run-evidence.sh verify-mutations.sh mutation-runner.py verify-mutations-negative.sh \
         verify-accepted-evidence.py verify-r2-delta.py verify-provenance.py verify-plan-evidence.sh \
         validate-manifest.sh validator-command.json validation.log r2-summary.md \
         mutation-report.json mutation.log mutation.exit mutation-traceability.json \
         mutation-negative-controls.json negative-controls/N6-infrastructure-failure.evidence.json \
         inherited-contract.json accepted-evidence.json r1-status.md rv1-frozen.sha256 \
         row-count-scope.md provenance.md branch-delta.json branch-before.patch branch-after.patch \
         branch-delta.patch production-before.sha256 production-after.sha256 r2-start.json \
         build.log build.exit core-tests.log core-tests.exit sqlite-tests.log sqlite-tests.exit \
         row-count-control.log row-count-control.exit integration-tests.log integration-tests.exit \
         clickhouse-tests.log clickhouse-tests.exit batch-integration-control.log \
         batch-integration-control.exit audit.log evidence.json manifest.json \
         mutations/total-to-row-count.patch; do
  check "$R/$a"
done

echo "== inherited accepted evidence =="
if python3 "$R/verify-accepted-evidence.py" --all > "$R/accepted-evidence-run.log" 2>&1; then
  echo "OK   verify-accepted-evidence --all"
else
  echo "MISS verify-accepted-evidence --all"; fail=1; miss=$((miss + 1))
fi

echo "== provenance =="
if python3 "$R/verify-provenance.py" > "$R/provenance-run.log" 2>&1; then
  echo "OK   verify-provenance"
else
  echo "MISS verify-provenance"; fail=1; miss=$((miss + 1))
fi

echo "== r2 delta =="
if python3 "$R/verify-r2-delta.py" check > "$R/delta-run.log" 2>&1; then
  echo "OK   verify-r2-delta check"
else
  echo "MISS verify-r2-delta check"; fail=1; miss=$((miss + 1))
fi

echo "== mutation report: frozen mutants killed, controls intended =="
python3 - "$R" <<'PYEOF' || { echo "MISS mutation report assertions"; fail=1; miss=$((miss + 1)); }
import json, sys
R = sys.argv[1]
rep = json.load(open(f"{R}/mutation-report.json", encoding="utf-8"))
frozen = {"remove-capture", "total-to-row-count", "sentinel-to-0", "reset-on-repeated-release",
          "disposed-reader-from-getter"}
by_id = {m["id"]: m for m in rep["mutants"]}
assert frozen <= set(by_id), "missing frozen mutants"
for fid in frozen:
    m = by_id[fid]
    assert m["kill_status"] == "killed", f"{fid} not killed"
    assert m["killing_test"], f"{fid} has no killing test"
    assert m["assertion_message"], f"{fid} has no assertion message"
    assert m["restore_ok"], f"{fid} restore not ok"
assert rep["restoration"]["compared_byte_identically"] is True
assert rep["restoration"]["working_tree_diff_unchanged"] is True
assert rep["guards"] == []
ctl = json.load(open(f"{R}/mutation-negative-controls.json", encoding="utf-8"))
assert ctl["all_controls_exit_1_intended"] is True
assert len(ctl["controls"]) == 6
n6 = [c for c in ctl["controls"] if c["control"] == "N6-infrastructure-failure"]
assert n6 and n6[0]["guard_token"] == "infrastructure diagnostic present"
print("OK   mutation report assertions (5 frozen killed, restoration compared, 6 controls intended)")
PYEOF

echo "== production byte-identity and rv1 freeze =="
if python3 - "$R" <<'PYEOF'
import hashlib, json, sys
from pathlib import Path
R = Path(sys.argv[1]); REPO = R.parents[3]
before = dict(l.split("  ")[::-1] for l in (R / "production-before.sha256").read_text().splitlines() if l.strip())
after = dict(l.split("  ")[::-1] for l in (R / "production-after.sha256").read_text().splitlines() if l.strip())
assert before == after, "production hashes changed"
for rel, digest in after.items():
    assert hashlib.sha256((REPO / rel).read_bytes()).hexdigest() == digest, f"production changed: {rel}"
changed = []
for line in (R / "rv1-frozen.sha256").read_text().splitlines():
    if not line.strip():
        continue
    digest, _, rel = line.partition("  ")
    p = REPO / rel
    if not p.is_file() or hashlib.sha256(p.read_bytes()).hexdigest() != digest:
        changed.append(rel)
assert not changed, f"rv1 changed: {changed[:3]}"
print(f"OK   production unchanged ({len(after)} files), rv1 byte-identical (0 changed)")
PYEOF
then :; else echo "MISS production/rv1 byte-identity"; fail=1; miss=$((miss + 1)); fi

echo "== T53 registration + mappings valid =="
if grep -q "T53 freeze candidates — D226" "$S"; then echo "OK   T53 registration"; else echo "MISS T53 registration"; fail=1; miss=$((miss + 1)); fi
for j in inherited-contract.json accepted-evidence.json mutation-traceability.json branch-delta.json manifest.json evidence.json; do
  if python3 -c "import json,sys;json.load(open(sys.argv[1],encoding='utf-8'))" "$R/$j" 2>/dev/null; then
    echo "OK   valid JSON $j"
  else
    echo "MISS valid JSON $j"; fail=1; miss=$((miss + 1))
  fi
done

echo "== scoped CRLF check on the permanently changed files =="
for f in tests/nextorm.sqlite.tests/BatchMultipleResultTests.cs docs/specs/status/D226-records-affected-1.md; do
  if file "$REPO/$f" | grep -q CRLF; then echo "OK   CRLF $f"; else echo "MISS CRLF $f"; fail=1; miss=$((miss + 1)); fi
done

echo "audit_miss=$miss plan_evidence_fail=$fail"
exit "$fail"
