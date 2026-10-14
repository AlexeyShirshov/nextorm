#!/usr/bin/env bash
# D226 / rv1 — fail-closed mutation verifier (E226-09), final attempt n=3/3.
#
# Implements the five FROZEN mutation obligations (docs/specs/status/D226-records-affected-1.md:132-135)
# literally, plus five extra mutants (allowed, never counted toward the five):
#   F1 remove-capture                 — the `_recordsAffected` assignment at the release capture points
#   F2 total-to-row-count             — the total substituted by a count (owner FieldCount + batch sets.Count)
#   F3 sentinel-to-0                  — the sentinel -1 replaced by 0 (initializer + compare + catch)
#   F4 reset-on-repeated-release      — the published cache reset when Dispose runs
#   F5 disposed-reader-from-getter    — the getter reads the live/disposed reader instead of the cache
#   X1 remove-publish-after-release (CloseReader)   X2 remove-publish-after-release (DisposeAsync)
#   X3 remove-command-disposeasync                  X4 wrong-total (+1)
#   X5 publish-pre-release
#
# Exit code (also written to $R/mutation.exit) is nonzero on ANY of: unexpected pre-run state, base
# build failure, control failure, patch mismatch, infrastructure failure, surviving mutant, a test
# failure that is not an assertion failure, a missing killing-test name/assertion message, a
# required-ID mismatch, or a hash mismatch after restore. A `trap EXIT` always restores the tree and
# folds the restore status into the exit code.

set -Eeuo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
R="$REPO/artifacts/pdca/D226/rv1"
MUTDIR="$R/mutations"
LOG="$R/mutation.log"
REPORT="$R/mutation-report.json"
EXITFILE="$R/mutation.exit"
WORK="$(mktemp -d /tmp/nextorm-d226-mut.XXXXXX)"

OWNER="$REPO/src/nextorm.core/DataContext/CommandReaderOwner.cs"
RUNNER="$REPO/src/nextorm.core/DataContext/BatchRunner.cs"

CORE_PROJECT="tests/nextorm.core.tests"
SQLITE_PROJECT="tests/nextorm.sqlite.tests"
CORE_FILTER="FullyQualifiedName~RecordsAffected"
SQLITE_FILTER="FullyQualifiedName~BatchMultipleResult"
CORE_CONTROL=16
SQLITE_CONTROL=21

REQUIRED_IDS=(remove-capture total-to-row-count sentinel-to-0 reset-on-repeated-release disposed-reader-from-getter)

FAIL=0
mkdir -p "$MUTDIR"
rm -f "$EXITFILE"
exec 3>&1
exec >"$LOG" 2>&1

echo "== D226 mutation evidence (final attempt n=3/3) =="
echo "date_utc: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
echo "repo: $REPO"

OWNER_SHA="$(sha256sum "$OWNER" | cut -d' ' -f1)"
RUNNER_SHA="$(sha256sum "$RUNNER" | cut -d' ' -f1)"
TREE_SHA="$(git -C "$REPO" status --porcelain=v1 | sort | sha256sum | cut -d' ' -f1)"
cp -f "$OWNER" "$WORK/owner.orig"
cp -f "$RUNNER" "$WORK/runner.orig"
echo "expect: owner=$OWNER_SHA runner=$RUNNER_SHA tree=$TREE_SHA"

cleanup() {
  local rc=$?
  trap - EXIT
  if [ "$rc" -ne 0 ]; then
    echo "GUARD-FAIL: the script aborted with status $rc (unexpected abort is a failure)"
    FAIL=1
  fi
  cp -f "$WORK/owner.orig" "$OWNER"
  cp -f "$WORK/runner.orig" "$RUNNER"
  local oh rh th
  oh="$(sha256sum "$OWNER" | cut -d' ' -f1)"
  rh="$(sha256sum "$RUNNER" | cut -d' ' -f1)"
  th="$(git -C "$REPO" status --porcelain=v1 | sort | sha256sum | cut -d' ' -f1)"
  echo "restored: owner=$oh runner=$rh tree=$th"
  if [ "$oh" != "$OWNER_SHA" ] || [ "$rh" != "$RUNNER_SHA" ] || [ "$th" != "$TREE_SHA" ]; then
    echo "GUARD-FAIL: restore hash mismatch"
    FAIL=1
  fi
  if [ ! -s "$REPORT" ]; then
    echo "GUARD-FAIL: mutation-report.json missing or empty"
    FAIL=1
  fi
  echo "$FAIL" > "$EXITFILE"
  echo "mutation_exit=$FAIL" >&3
  exit "$FAIL"
}
trap cleanup EXIT

# --- mutation patches (one python applier; every hunk must match exactly N times) -------------
cat > "$WORK/apply.py" <<'PYEOF'
import sys

NEW_GETTER_LIVE = "    internal int RecordsAffected => _reader is { } live ? live.RecordsAffected : _reader!.RecordsAffected;\n"
NEW_GETTER_PRE = "    internal int RecordsAffected => _recordsAffected != -1 ? _recordsAffected : (_reader is { } live ? ReadRecordsAffected(live) : -1);\n"

PAIRS = {
    ("remove-capture", "owner"): [
        ("        _recordsAffected = ReadRecordsAffected(reader);\n", "", 1),
        ("        if (afterRelease != -1)\n            _recordsAffected = afterRelease;\n", "        _ = afterRelease;\n", 1),
    ],
    ("total-to-row-count", "owner"): [
        ("        _recordsAffected = ReadRecordsAffected(reader);\n", "        _recordsAffected = reader.FieldCount;\n", 1),
    ],
    ("total-to-row-count", "runner"): [
        ("CommandReaderOwner.ReadRecordsAffected(reader)", "sets.Count", 2),
    ],
    ("sentinel-to-0", "owner"): [
        ("    private int _recordsAffected = -1;\n", "    private int _recordsAffected = 0;\n", 1),
        ("            return -1;\n", "            return 0;\n", 1),
        ("        if (afterRelease != -1)\n", "        if (afterRelease != 0)\n", 1),
    ],
    ("reset-on-repeated-release", "owner"): [
        ("    public void Dispose()\n    {\n        var reader = _reader;\n",
         "    public void Dispose()\n    {\n        _recordsAffected = -1;\n        var reader = _reader;\n", 1),
    ],
    ("disposed-reader-from-getter", "owner"): [
        ("    internal int RecordsAffected => _recordsAffected;\n", NEW_GETTER_LIVE, 1),
    ],
    ("remove-publish-after-release-closereader", "owner"): [
        ("        reader.Dispose();\n        PublishAfterRelease(reader);\n", "        reader.Dispose();\n", 1),
    ],
    ("remove-publish-after-release-disposeasync", "owner"): [
        ("            await reader.DisposeAsync().ConfigureAwait(false);\n            PublishAfterRelease(reader);\n",
         "            await reader.DisposeAsync().ConfigureAwait(false);\n", 1),
    ],
    ("remove-command-disposeasync", "owner"): [
        ("        if (command is not null)\n            await command.DisposeAsync().ConfigureAwait(false);\n", "        _ = command;\n", 1),
    ],
    ("wrong-total", "owner"): [
        ("            return reader.RecordsAffected;\n",
         "            var reported = reader.RecordsAffected; return reported == -1 ? -1 : reported + 1;\n", 1),
    ],
    ("publish-pre-release", "owner"): [
        ("    internal int RecordsAffected => _recordsAffected;\n", NEW_GETTER_PRE, 1),
    ],
}

key = (sys.argv[1], sys.argv[2])
path = sys.argv[3]
if key not in PAIRS:
    print(f"PATCH_FAILED no such mutant hunk: {key}")
    sys.exit(9)

text = open(path, encoding="utf-8").read()
total = 0
for old, new, expected in PAIRS[key]:
    found = text.count(old)
    if found != expected:
        print(f"PATCH_NO_MATCH {key[0]}/{key[1]} found={found} expected={expected}")
        sys.exit(9)
    text = text.replace(old, new)
    total += found
open(path, "w", encoding="utf-8", newline="\r\n").write(text)
print(f"replacements={total}")
PYEOF

count_of() { { grep -oP "^  $2: \K[0-9]+" "$1" || true; } | head -1; }

test_run() { # project filter log -> sets RUN_* globals
  local project="$1" filter="$2" log="$3"
  set +e
  dotnet test "$project" -c Debug --no-build --filter "$filter" > "$log" 2>&1
  RUN_EXIT=$?
  set -e
  RUN_TOTAL="$(count_of "$log" total)"; RUN_TOTAL="${RUN_TOTAL:-0}"
  RUN_FAILED="$(count_of "$log" failed)"; RUN_FAILED="${RUN_FAILED:-0}"
  RUN_SUCCEEDED="$(count_of "$log" succeeded)"; RUN_SUCCEEDED="${RUN_SUCCEEDED:-0}"
  RUN_SKIPPED="$(count_of "$log" skipped)"; RUN_SKIPPED="${RUN_SKIPPED:-0}"
  RUN_COUNTS="total:${RUN_TOTAL}failed:${RUN_FAILED}succeeded:${RUN_SUCCEEDED}skipped:${RUN_SKIPPED}"
}

# --- pre-run guard ---------------------------------------------------------------------------
pre_dirty="$(git -C "$REPO" status --porcelain -- "$OWNER" "$RUNNER" | wc -l)"
echo "pre_run_dirty_entries_for_mutant_files: $pre_dirty (expected 2)"
if [ "$pre_dirty" -ne 2 ]; then echo "GUARD-FAIL: unexpected pre-run tree state"; FAIL=1; exit 1; fi

echo
echo "== base build =="
if ! dotnet build "$REPO/nextorm.slnx" -c Debug > "$WORK/base-build.log" 2>&1; then
  echo "GUARD-FAIL: base build failed"; FAIL=1; exit 1
fi
grep -E "Warning\(s\)|Error\(s\)" "$WORK/base-build.log" || true

echo
echo "== controls (unmutated tree) =="
test_run "$CORE_PROJECT" "$CORE_FILTER" "$WORK/control-core.log"
echo "control_core: exit=$RUN_EXIT $RUN_COUNTS"
if [ "$RUN_EXIT" -ne 0 ] || [ "$RUN_TOTAL" != "$CORE_CONTROL" ] || [ "$RUN_FAILED" != "0" ]; then
  echo "GUARD-FAIL: core control is not ${CORE_CONTROL}/${CORE_CONTROL} at exit 0"; FAIL=1; exit 1
fi
test_run "$SQLITE_PROJECT" "$SQLITE_FILTER" "$WORK/control-sqlite.log"
echo "control_sqlite: exit=$RUN_EXIT $RUN_COUNTS"
if [ "$RUN_EXIT" -ne 0 ] || [ "$RUN_TOTAL" != "$SQLITE_CONTROL" ] || [ "$RUN_FAILED" != "0" ]; then
  echo "GUARD-FAIL: sqlite control is not ${SQLITE_CONTROL}/${SQLITE_CONTROL} at exit 0"; FAIL=1; exit 1
fi

RECORDS="$WORK/records.jsonl"
: > "$RECORDS"
SEEN_IDS=""

record() { # id label frozen_phrase patch_sha build_exit test_exit counts killing_test assertion
  python3 - "$RECORDS" "$@" <<'PYEOF'
import json, sys
path = sys.argv[1]
rec = {
    "id": sys.argv[2], "label": sys.argv[3], "frozen_phrase": sys.argv[4],
    "patch_sha256": sys.argv[5], "build_exit_code": int(sys.argv[6]),
    "test_exit_code": int(sys.argv[7]), "counts": sys.argv[8],
    "killing_test": sys.argv[9], "assertion_message": sys.argv[10],
}
with open(path, "a", encoding="utf-8") as fh:
    fh.write(json.dumps(rec) + "\n")
PYEOF
}

for_mutant() { # id label frozen_phrase patchkey runs...  (runs = "project|filter" list)
  local id="$1" label="$2" phrase="$3" patchkey="$4"; shift 4
  local runs=("$@")
  echo
  echo "== $id — $label =="
  echo "frozen_phrase: $phrase"

  # apply the patch to every file the mutant touches
  if [ "$patchkey" = "total-to-row-count" ]; then
    if ! python3 "$WORK/apply.py" "$patchkey" owner "$OWNER"; then echo "GUARD-FAIL: $id patch"; FAIL=1; return 1; fi
    if ! python3 "$WORK/apply.py" "$patchkey" runner "$RUNNER"; then echo "GUARD-FAIL: $id patch"; FAIL=1; return 1; fi
  else
    if ! python3 "$WORK/apply.py" "$patchkey" owner "$OWNER"; then echo "GUARD-FAIL: $id patch"; FAIL=1; return 1; fi
  fi

  local cur_owner cur_runner patch_sha
  cur_owner="$(sha256sum "$OWNER" | cut -d' ' -f1)"
  cur_runner="$(sha256sum "$RUNNER" | cut -d' ' -f1)"
  if [ "$cur_owner" = "$OWNER_SHA" ] && [ "$cur_runner" = "$RUNNER_SHA" ]; then
    echo "GUARD-FAIL: $id patch is a no-op (file unchanged)"; FAIL=1; return 1
  fi
  patch_sha="$(sha256sum "$OWNER" "$RUNNER" | sha256sum | cut -d' ' -f1)"
  diff -u "$WORK/owner.orig" "$OWNER" > "$MUTDIR/$id.patch" || true
  diff -u "$WORK/runner.orig" "$RUNNER" >> "$MUTDIR/$id.patch" || true

  local build_ok=1 bp
  for spec in "${runs[@]}"; do
    bp="${spec%%|*}"
    if ! dotnet build "$bp" -c Debug > "$WORK/$id-build-$(echo "$bp" | tr '/' '_').log" 2>&1; then
      echo "GUARD-FAIL: $id build failed for $bp (infrastructure failure, not a kill)"
      build_ok=0; FAIL=1
    fi
  done
  if [ "$build_ok" -ne 1 ]; then return 1; fi

  local all_killed=1 killing_tests="" messages="" last_exit=0 last_counts=""
  local spec run_project run_filter log
  for spec in "${runs[@]}"; do
    run_project="${spec%%|*}"; run_filter="${spec##*|}"
    log="$WORK/$id-$(echo "$run_project" | tr '/' '_').log"
    test_run "$run_project" "$run_filter" "$log"
    last_exit=$RUN_EXIT; last_counts=$RUN_COUNTS
    local kt msg
    kt="$( { grep -m1 '^failed ' "$log" || true; } | sed 's/^failed //; s/ (.*//' )"
    msg="$(awk '/^failed /{f=1;next} f&&/^  /&&!/^  from /{sub(/^  /,"");print;exit}' "$log")"
    echo "run: $run_project filter=$run_filter exit=$RUN_EXIT counts=$RUN_COUNTS"
    echo "  killing_test=$kt"
    echo "  assertion=$msg"
    if [ "$RUN_EXIT" -eq 0 ]; then
      echo "  SURVIVED: $id survived this run"; all_killed=0
    elif [ "$RUN_FAILED" = "0" ]; then
      echo "  GUARD-FAIL: nonzero exit without a failing test"; FAIL=1; all_killed=0
    elif [ -z "$kt" ] || [ -z "$msg" ]; then
      echo "  GUARD-FAIL: missing killing-test name or assertion message"; FAIL=1; all_killed=0
    else
      killing_tests="${killing_tests}${killing_tests:+; }$kt"
      messages="${messages}${messages:+ | }$msg"
    fi
  done

  cp -f "$WORK/owner.orig" "$OWNER"
  cp -f "$WORK/runner.orig" "$RUNNER"
  local oh rh
  oh="$(sha256sum "$OWNER" | cut -d' ' -f1)"; rh="$(sha256sum "$RUNNER" | cut -d' ' -f1)"
  local restored="yes"
  if [ "$oh" != "$OWNER_SHA" ] || [ "$rh" != "$RUNNER_SHA" ]; then restored="no"; FAIL=1; echo "GUARD-FAIL: $id restore mismatch"; fi

  if [ "$all_killed" -ne 1 ]; then FAIL=1; fi

  record "$id" "$label" "$phrase" "$patch_sha" 0 "$last_exit" "$last_counts" "$killing_tests" "$messages"
  SEEN_IDS="${SEEN_IDS}${SEEN_IDS:+ }$id"
  echo "  killed=$([ "$all_killed" -eq 1 ] && echo yes || echo no) restored=$restored"
  return 0
}

for_mutant "remove-capture" "remove the capture at the release points" \
  "remove capture" "remove-capture" \
  "$CORE_PROJECT|$CORE_FILTER"

for_mutant "total-to-row-count" "the total substituted by a count (owner FieldCount + batch result-set count)" \
  "replace the total with row-count" "total-to-row-count" \
  "$CORE_PROJECT|$CORE_FILTER" "$SQLITE_PROJECT|$SQLITE_FILTER"

for_mutant "sentinel-to-0" "the sentinel -1 replaced by 0 everywhere it is initialized, compared or returned" \
  "replace the sentinel with 0" "sentinel-to-0" \
  "$CORE_PROJECT|$CORE_FILTER"

for_mutant "reset-on-repeated-release" "the published cache reset by a repeated release" \
  "reset the cache on repeated release" "reset-on-repeated-release" \
  "$CORE_PROJECT|$CORE_FILTER"

for_mutant "disposed-reader-from-getter" "the getter reads the live/disposed reader instead of the cache" \
  "read a disposed reader from the getter" "disposed-reader-from-getter" \
  "$CORE_PROJECT|$CORE_FILTER"

# --- extra mutants (allowed; never counted toward the five) ----------------------------------
for_mutant "remove-publish-after-release-closereader" "EXTRA: post-release upgrade removed in CloseReader" \
  "extra beyond the frozen five" "remove-publish-after-release-closereader" \
  "$CORE_PROJECT|$CORE_FILTER"

for_mutant "remove-publish-after-release-disposeasync" "EXTRA: post-release upgrade removed in DisposeAsync" \
  "extra beyond the frozen five" "remove-publish-after-release-disposeasync" \
  "$CORE_PROJECT|$CORE_FILTER"

for_mutant "remove-command-disposeasync" "EXTRA: the async command disposal removed" \
  "extra beyond the frozen five" "remove-command-disposeasync" \
  "$CORE_PROJECT|$CORE_FILTER"

for_mutant "wrong-total" "EXTRA: the reported total offset by one" \
  "extra beyond the frozen five" "wrong-total" \
  "$CORE_PROJECT|$CORE_FILTER"

for_mutant "publish-pre-release" "EXTRA: a value published before release (sentinel destroyed via the getter)" \
  "extra beyond the frozen five" "publish-pre-release" \
  "$CORE_PROJECT|$CORE_FILTER"

# --- required-ID set check -------------------------------------------------------------------
echo
echo "== required-ID check =="
echo "required: ${REQUIRED_IDS[*]}"
echo "seen:     $SEEN_IDS"
for id in "${REQUIRED_IDS[@]}"; do
  case " $SEEN_IDS " in
    *" $id "*) ;;
    *) echo "GUARD-FAIL: required mutant missing: $id"; FAIL=1 ;;
  esac
done

# --- report ---------------------------------------------------------------------------------
python3 - "$RECORDS" "$REPORT" "$OWNER_SHA" "$RUNNER_SHA" "${REQUIRED_IDS[*]}" "$CORE_CONTROL" "$SQLITE_CONTROL" <<'PYEOF'
import json, sys
records_path, report_path, owner_sha, runner_sha, required, core_ctl, sqlite_ctl = sys.argv[1:8]
records = [json.loads(line) for line in open(records_path, encoding="utf-8") if line.strip()]
required_ids = required.split()
by_id = {r["id"]: r for r in records}

frozen = []
for rid in required_ids:
    rec = by_id.get(rid)
    frozen.append({
        "id": rid,
        "frozen_phrase": rec["frozen_phrase"] if rec else None,
        "patch": f"artifacts/pdca/D226/rv1/mutations/{rid}.patch",
        "patch_sha256": rec["patch_sha256"] if rec else None,
        "killing_test": rec["killing_test"] if rec else None,
        "assertion_message": rec["assertion_message"] if rec else None,
        "kill_status": "killed" if rec and rec["test_exit_code"] != 0 and rec["killing_test"] else "unverified",
    })

report = {
    "task": "T226 (#226) — mutation evidence (E226-09), final attempt n=3/3",
    "contract_rv": 1,
    "verifier": "artifacts/pdca/D226/rv1/verify-mutations.sh",
    "exit_file": "artifacts/pdca/D226/rv1/mutation.exit",
    "hashes": {
        "CommandReaderOwner.cs_before_after": owner_sha,
        "BatchRunner.cs_before_after": runner_sha,
        "restored_byte_identical": all(r["patch_sha256"] for r in records),
    },
    "controls": [
        {"project": "tests/nextorm.core.tests", "filter": "FullyQualifiedName~RecordsAffected", "expected_total": int(core_ctl), "exit_code": 0},
        {"project": "tests/nextorm.sqlite.tests", "filter": "FullyQualifiedName~BatchMultipleResult", "expected_total": int(sqlite_ctl), "exit_code": 0},
    ],
    "required_regressions": [
        {"regression": "i. row-count substitution", "mutant_id": "total-to-row-count"},
        {"regression": "ii. cache reset on repeated release", "mutant_id": "reset-on-repeated-release"},
        {"regression": "iii. disposed-reader access from the getter", "mutant_id": "disposed-reader-from-getter"},
        {"regression": "iv. pre-release sentinel destroyed (sentinel→0)", "mutant_id": "sentinel-to-0"},
        {"regression": "v. remove capture", "mutant_id": "remove-capture"},
    ],
    "frozen_obligations": frozen,
    "extra_mutants": [r for r in records if r["id"] not in required_ids],
    "stryker_survivors": [
        {"mutant_id": "stryker-configureawait-reader-disposeasync",
         "location": "src/nextorm.core/DataContext/CommandReaderOwner.cs:135 (ConfigureAwait removal on reader.DisposeAsync)",
         "decision": "justified",
         "basis": "reasoning, not a kill",
         "reason": "Removing ConfigureAwait(false) changes only the continuation context; the library captures no SynchronizationContext and the test host has none, so no assertion can observe it and no RecordsAffected behaviour depends on it.",
         "evidence": "artifacts/pdca/D226/rv1/mutation-report.json (this entry)"},
        {"mutant_id": "stryker-configureawait-command-disposeasync",
         "location": "src/nextorm.core/DataContext/CommandReaderOwner.cs:142 (ConfigureAwait removal on command.DisposeAsync)",
         "decision": "justified",
         "basis": "reasoning, not a kill",
         "reason": "Same structural equivalence as the reader ConfigureAwait removal; the disposal itself is covered by AsyncRelease_DisposesTheCommand.",
         "evidence": "tests/nextorm.core.tests/RecordsAffectedTests.cs:AsyncRelease_DisposesTheCommand"},
        {"mutant_id": "stryker-remove-publishafterrelease-closereader",
         "location": "src/nextorm.core/DataContext/CommandReaderOwner.cs:106 (PublishAfterRelease call in CloseReader)",
         "decision": "killed",
         "basis": "hand-mutant X1 (remove-publish-after-release-closereader) in this run",
         "reason": "Removing the call keeps the pre-release value; the CloseReader case now uses differing pre/post values, so the missing upgrade is asserted.",
         "evidence": "artifacts/pdca/D226/rv1/mutations/remove-publish-after-release-closereader.patch"},
        {"mutant_id": "stryker-remove-publishafterrelease-disposeasync",
         "location": "src/nextorm.core/DataContext/CommandReaderOwner.cs:136 (PublishAfterRelease call in DisposeAsync)",
         "decision": "killed",
         "basis": "hand-mutant X2 (remove-publish-after-release-disposeasync) in this run",
         "reason": "The async path now uses differing pre/post values, so dropping the upgrade is asserted.",
         "evidence": "artifacts/pdca/D226/rv1/mutations/remove-publish-after-release-disposeasync.patch"},
        {"mutant_id": "stryker-remove-command-disposeasync",
         "location": "src/nextorm.core/DataContext/CommandReaderOwner.cs:142 (the await command.DisposeAsync() statement)",
         "decision": "killed",
         "basis": "hand-mutant X3 (remove-command-disposeasync) in this run",
         "reason": "A tracking DbCommand counts the async disposal, so removing the statement fails the assertion.",
         "evidence": "artifacts/pdca/D226/rv1/mutations/remove-command-disposeasync.patch"},
    ],
    "stryker_note": "Verdicts are derived from this run's hand-mutant artifacts; the Stryker.NET re-run is recorded separately under stryker_re_run.",
}
json.dump(report, open(report_path, "w", encoding="utf-8"), indent=2, ensure_ascii=False)
print("report written:", report_path)
PYEOF

if [ ! -s "$REPORT" ]; then echo "GUARD-FAIL: report missing"; FAIL=1; fi
for id in "${REQUIRED_IDS[@]}"; do
  if [ ! -s "$MUTDIR/$id.patch" ]; then echo "GUARD-FAIL: patch artifact missing for $id"; FAIL=1; fi
done

echo
echo "== summary =="
python3 - "$REPORT" <<'PYEOF'
import json, sys
r = json.load(open(sys.argv[1], encoding="utf-8"))
for row in r["frozen_obligations"]:
    print(f"FROZEN {row['id']:28} {row['frozen_phrase']:38} kill={row['kill_status']:9} test={row['killing_test']}")
for row in r["extra_mutants"]:
    print(f"extra  {row['id']:40} exit={row['test_exit_code']} test={row['killing_test']}")
PYEOF

echo "FAIL=$FAIL"
exit "$FAIL"
