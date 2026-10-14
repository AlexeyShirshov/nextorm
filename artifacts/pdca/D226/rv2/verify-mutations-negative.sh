#!/usr/bin/env bash
# D226 / rv2 — negative controls for the fail-closed mutation verifier (N1..N6).
#
# Every control is an isolated copy of $R/verify-mutations.sh + $R/mutation-runner.py under
# /tmp/opencode with (a) REPO pinned to the real repository, (b) the artifact root R redirected to the
# copy's own directory (so a control can never clobber the real artifacts or the real source bytes),
# (c) the mutant list reduced to the first mutant to bound the runtime, and (d) exactly one
# perturbation under test. The aggregate exits 0 only when ALL SIX controls exit 1 for the intended
# reason. No worktree, no product edit, no hook inside the real verifier.
set -uo pipefail

REPO="/home/alex/sources/nextorm"
R="$REPO/artifacts/pdca/D226/rv2"
OUT="$R/mutation-negative-controls.json"
CTLDIR="$R/negative-controls"
W="$(mktemp -d /tmp/opencode/d226-r2-negctl.XXXXXX)"
FAIL=0
ROWS=()

echo "== D226 rv2 negative controls N1..N6 =="
echo "date_utc: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
echo "work: $W"
mkdir -p "$CTLDIR"

run_case() { # name intended-reason perturbation-json
  local name="$1" intended="$2" pairs="$3"
  local dir="$W/$name"
  mkdir -p "$dir/mutations"
  cp -f "$R/mutation-runner.py" "$dir/mutation-runner.py"
  cp -f "$R/verify-mutations.sh" "$dir/verify-mutations.sh"
  cp -f "$R"/mutations/*.patch "$dir/mutations/" 2>/dev/null || true

  echo
  echo "== control $name =="
  echo "intended_reason: $intended"

  if ! python3 - "$dir/mutation-runner.py" "$REPO" "$dir" "$pairs" > "$dir/perturb.log" 2>&1 <<'PYEOF'
import json, sys
path, repo, rdir, pairs_json = sys.argv[1:5]
text = open(path, encoding="utf-8").read()
base = [
    ("REPO = R.parents[3]", f'REPO = __import__("pathlib").Path("{repo}")'),
    ("for mutant in MUTANTS:", "for mutant in MUTANTS[:1]:"),
]
for old, new in base + [tuple(p) for p in json.loads(pairs_json)]:
    n = text.count(old)
    if n != 1:
        print(f"PERTURB_NO_MATCH needle={old[:60]!r} found={n}")
        sys.exit(9)
    text = text.replace(old, new)
open(path, "w", encoding="utf-8", newline="").write(text)
print("perturbed=ok")
PYEOF
  then
    echo "GUARD-FAIL: fixture could not be built for $name"
    FAIL=1
    ROWS+=("{\"control\":\"$name\",\"intended_reason\":\"$intended\",\"exit_code\":null,\"result\":\"fixture-failed\"}")
    return
  fi

  set +e
  bash "$dir/verify-mutations.sh" > "$dir/stdout.log" 2>&1
  local ec=$?
  set -e
  local token="" outcome="mismatch"
  for candidate in \
      "PATCH_NO_MATCH" \
      "kill condition failed: mutated_exit_nonzero" \
      "restore did not compare byte-identically" \
      "clean control core-recordsaffected did not pass" \
      "unexpected exception" \
      "infrastructure diagnostic present"; do
    if grep -qF "$candidate" "$dir/mutation.log" "$dir/stdout.log" 2>/dev/null; then token="$candidate"; fi
  done

  case "$name" in
    N1-no-match-patch)                     [ "$ec" -eq 1 ] && [ "$token" = "PATCH_NO_MATCH" ] && outcome="intended" ;;
    N2-surviving-mutant)                   [ "$ec" -eq 1 ] && [ "$token" = "kill condition failed: mutated_exit_nonzero" ] && outcome="intended" ;;
    N3-restore-comparison-failure)         [ "$ec" -eq 1 ] && [ "$token" = "restore did not compare byte-identically" ] && outcome="intended" ;;
    N4-failing-control)                    [ "$ec" -eq 1 ] && [ "$token" = "clean control core-recordsaffected did not pass" ] && outcome="intended" ;;
    N5-invalid-artifact-path)              [ "$ec" -eq 1 ] && [ "$token" = "unexpected exception" ] && outcome="intended" ;;
    N6-infrastructure-failure)             [ "$ec" -eq 1 ] && [ "$token" = "infrastructure diagnostic present" ] && outcome="intended" ;;
  esac
  [ "$outcome" = "intended" ] || FAIL=1

  echo "exit=$ec token=${token:-<none>} outcome=$outcome"
  cp -f "$dir/mutation.log" "$CTLDIR/$name.log" 2>/dev/null || true
  cp -f "$dir/mutation.exit" "$CTLDIR/$name.exit" 2>/dev/null || true
  # N5 perturbs the artifact path on purpose, so its log is intentionally not produced: record that
  # fact and link the .exit evidence instead of referencing a file that must not exist.
  local log_field log_status
  if [ -f "$CTLDIR/$name.log" ]; then
    log_field="\"artifacts/pdca/D226/rv2/negative-controls/$name.log\""
    log_status="produced"
  else
    log_field="null"
    log_status="not-produced-by-design: this control intentionally points the log artifact path at a non-existent directory, so no log can exist; the control's exit evidence is $name.exit and the guard token is recorded above (integrity: no fabricated log file)."
  fi
  cat > "$CTLDIR/$name.evidence.json" <<JSONEOF
{"control": "$name", "intended_reason": "$intended", "exit_code": $ec,
 "guard_token": "${token:-<none>}", "outcome": "$outcome",
 "artifact_log": $log_field,
 "artifact_log_status": "$log_status",
 "artifact_exit": "artifacts/pdca/D226/rv2/negative-controls/$name.exit"}
JSONEOF
  ROWS+=("{\"control\":\"$name\",\"intended_reason\":\"$intended\",\"exit_code\":$ec,\"guard_token\":\"${token:-<none>}\",\"outcome\":\"$outcome\"}")
}

run_case "N1-no-match-patch" "a patch whose needle does not exist must be refused (PATCH_NO_MATCH), never counted as a kill" \
  '[["CommandReaderOwner.ReadRecordsAffected(reader));\\n\",", "NO_SUCH_SITE_IN_THE_SOURCE));\\n\","]]'

run_case "N2-surviving-mutant" "a behaviour-neutral (comment-only) patch changes bytes without changing behaviour, so nothing fails: the mutant must NOT be counted as killed" \
  '[["System.Linq.Enumerable.Sum(sets, static set => set.Count));\\n\",", "CommandReaderOwner.ReadRecordsAffected(reader)); // mutant\\n\","]]'

run_case "N3-restore-comparison-failure" "the restore comparison outcome forced to failure: the guard must fire and the exit must be nonzero" \
  '[["same = actual == data", "same = False"]]'

run_case "N4-failing-control" "the clean control expectation changed from 16 to 99: the control guard must fire" \
  '[["\"expected_total\": 16", "\"expected_total\": 99"]]'

run_case "N5-invalid-artifact-path" "the log artifact path pointed at a non-existent directory: the artifact guard must fire" \
  '[["LOG = R / \"mutation.log\"", "LOG = R / \"no-such-directory\" / \"mutation.log\""]]'

run_case "N6-infrastructure-failure" "a genuine assertion-failure record tainted with an infrastructure diagnostic must be classified as infrastructure failure, not as a kill" \
  '[["log_text = out_test", "log_text = out_test + \"\\\\nGUARD-FAIL: base build failed\\\\n\""]]'

python3 - "$OUT" "${ROWS[@]}" <<'PYEOF'
import json, sys
path = sys.argv[1]
rows = [json.loads(x) for x in sys.argv[2:]]
doc = {
    "task": "T226 (#226) rv2 — negative controls for verify-mutations.sh (N1..N6)",
    "contract_rv": 2,
    "verifier": "artifacts/pdca/D226/rv2/verify-mutations.sh",
    "controls": rows,
    "all_controls_exit_1_intended": all(r.get("outcome") == "intended" for r in rows),
    "note": "Each control is an isolated copy of the verifier with REPO pinned, the artifact root redirected to the copy, the mutant list reduced to the first mutant and exactly one perturbation; the aggregate exits 0 only when all six controls exit 1 for the intended reason.",
}
json.dump(doc, open(path, "w", encoding="utf-8"), indent=2, ensure_ascii=False)
print("negative-controls written:", path)
PYEOF

echo
echo "controls_fail=$FAIL"
exit "$FAIL"
