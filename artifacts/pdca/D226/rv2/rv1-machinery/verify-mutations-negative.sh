#!/usr/bin/env bash
# D226 / rv1 — negative controls proving verify-mutations.sh is fail-closed (E226-09, unit 3).
#
# Each control is a temporary COPY of the verifier with exactly one perturbation, run for real; every
# one must exit nonzero. No hook is added to the verifier itself and no product file is touched:
#  * the copy's REPO is pinned to the real repository (a copy in /tmp cannot derive it), and
#  * its artifact root R is redirected into $WORK, so a control can never clobber the real artifacts;
#  * the "corrupted restore" control perturbs the *expected* hash, so the post-restore guard fires
#    while the real source stays intact.
set -Eeuo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
R="$REPO/artifacts/pdca/D226/rv1/"
MAIN="$R/verify-mutations.sh"
WORK="$(mktemp -d /tmp/nextorm-d226-negctl.XXXXXX)"
OUT="$R/mutation-negative-controls.json"
FAIL=0

echo "== D226 negative controls =="
echo "date_utc: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
echo "work: $WORK"

RECORDS=()

run_case() { # name perturbation-description pairs-json
  local name="$1" perturbation="$2" pairs="$3"
  local copy="$WORK/$name.sh"
  local ctl_r="$WORK/ctl-artifacts-$name"
  cp -f "$MAIN" "$copy"

  echo
  echo "== control $name =="
  echo "perturbation: $perturbation"

  if ! python3 - "$copy" "$REPO" "$ctl_r" "$pairs" > "$WORK/$name.perturb.log" 2>&1 <<'PYEOF'
import json, sys
path, repo, ctl_root, pairs_json = sys.argv[1:5]
text = open(path, encoding="utf-8").read()

base = [
    ('REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"', f'REPO="{repo}"'),
    ('R="$REPO/artifacts/pdca/D226/rv1"', f'R="{ctl_root}"'),
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
    echo "GUARD-FAIL: the negative-control fixture could not be built"
    FAIL=1
    RECORDS+=("{\"control\":\"$name\",\"perturbation\":\"$perturbation\",\"exit_code\":null,\"nonzero\":\"no\",\"guard_hit\":\"fixture-failed\"}")
    return
  fi

  mkdir -p "$ctl_r"

  set +e
  bash "$copy" > "$WORK/$name.out" 2>&1
  local ec=$?
  set -e

  local guard=""
  if [ -f "$ctl_r/mutation.log" ]; then
    guard="$(grep -m1 -E 'GUARD-FAIL|PATCH_NO_MATCH|PATCH_FAILED|SURVIVED|aborted with status' "$ctl_r/mutation.log" || true)"
  fi
  if [ -z "$guard" ]; then
    guard="$(grep -m1 -E 'GUARD-FAIL|PATCH_NO_MATCH|PATCH_FAILED|SURVIVED|aborted with status|No such file' "$WORK/$name.out" || true)"
  fi

  local nonzero="no"
  [ "$ec" -ne 0 ] && nonzero="yes"
  [ "$nonzero" = "no" ] && FAIL=1
  echo "exit=$ec nonzero=$nonzero"
  echo "guard_hit: ${guard:-<none>}"

  RECORDS+=("$(python3 - "$name" "$perturbation" "$ec" "$nonzero" "${guard:-<none>}" <<'PYEOF'
import json, sys
print(json.dumps({"control": sys.argv[1], "perturbation": sys.argv[2], "exit_code": int(sys.argv[3]),
                  "nonzero": sys.argv[4], "guard_hit": sys.argv[5]}))
PYEOF
)")
}

run_case "N1-no-match-patch" \
  "the remove-capture patch needle replaced by a line that does not exist in the source" \
  '[["(\"        _recordsAffected = ReadRecordsAffected(reader);\\n\", \"\", 1)", "(\"        NO_SUCH_LINE_IN_THIS_FILE;\\n\", \"\", 1)"]]'

run_case "N2-noop-surviving-mutant" \
  "the single-hunk reset-on-repeated-release patch turned into a comment-only change, so no assertion can fail (a surviving mutant)" \
  '[["\"    public void Dispose()\\n    {\\n        _recordsAffected = -1;\\n        var reader = _reader;\\n\"", "\"    public void Dispose()\\n    {\\n        // mutant: no reset\\n        var reader = _reader;\\n\""]]'

run_case "N3-hash-mismatch-detection" \
  "the expected owner hash replaced by a wrong constant, so the post-restore hash guard must fire (the real source is not corrupted)" \
  '[["OWNER_SHA=\"$(sha256sum \"$OWNER\" | cut -d'"'"' '"'"' -f1)\"", "OWNER_SHA=0000000000000000000000000000000000000000000000000000000000000000"]]'

run_case "N4-failing-control" \
  "the core control expectation changed from 16 to 99, so the control guard must fire" \
  '[["CORE_CONTROL=16", "CORE_CONTROL=99"]]'

run_case "N5-invalid-artifact-path" \
  "the log path pointed at a non-existent directory, so the artifact/schema guard must fire at startup" \
  '[["LOG=\"$R/mutation.log\"", "LOG=\"$R/no-such-directory/mutation.log\""]]'

python3 - "$OUT" "${RECORDS[@]}" <<'PYEOF'
import json, sys
path = sys.argv[1]
rows = [json.loads(x) for x in sys.argv[2:]]
doc = {
    "task": "T226 (#226) — negative controls for verify-mutations.sh",
    "contract_rv": 1,
    "verifier": "artifacts/pdca/D226/rv1/verify-mutations.sh",
    "controls": rows,
    "all_controls_nonzero": all(r.get("nonzero") == "yes" for r in rows),
    "note": "Each control is a temporary copy of the verifier with exactly one perturbation, run for real with its artifact root redirected to a temp directory; all controls must exit nonzero for the verifier to count as fail-closed.",
}
json.dump(doc, open(path, "w", encoding="utf-8"), indent=2, ensure_ascii=False)
print("negative-controls written:", path)
PYEOF

echo
echo "controls_fail=$FAIL"
exit "$FAIL"
