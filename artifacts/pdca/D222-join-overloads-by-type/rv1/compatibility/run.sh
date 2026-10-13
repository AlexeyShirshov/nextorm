#!/usr/bin/env bash
# T222 (#222) rv=1 compatibility runner.
#
# Invoked by: bash "$B/capture.sh" E222-COMPAT -- bash "$B/compatibility/run.sh"
#
# Builds every fixture listed in compatibility/manifest.json and enforces the
# expected outcome: positive -> exit 0; negative -> non-zero with the expected
# compiler diagnostic present (a restore/importer failure does not count).
set -u

compat="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
B="$(cd "$compat/.." && pwd)"
root="$(git -C "$B" rev-parse --show-toplevel)"
out="$B/E222-COMPAT"
mkdir -p "$out"

mapfile -t rows < <(python3 -c '
import json, sys
m = json.load(open(sys.argv[1]))
for c in m["cases"]:
    print("\t".join([c["id"], c["project"], c["expect"], ",".join(c.get("expected_diagnostics", []))]))
' "$compat/manifest.json")

fail=0
for row in "${rows[@]}"; do
  IFS=$'\t' read -r id proj expect diags <<< "$row"
  log="$out/$id.log"
  ( cd "$root" && dotnet build "$compat/$proj" -c Debug -v:minimal ) > "$log" 2>&1
  rc=$?
  if [[ "$expect" == "success" ]]; then
    if [[ $rc -ne 0 ]]; then
      echo "FAIL $id: expected success, got exit $rc ($log)"
      fail=1
    else
      echo "PASS $id: exit 0"
    fi
  else
    if [[ $rc -eq 0 ]]; then
      echo "FAIL $id: expected failure but build succeeded ($log)"
      fail=1
      continue
    fi
    IFS=',' read -ra want <<< "$diags"
    missing=0
    for d in "${want[@]}"; do
      [[ -z "$d" ]] && continue
      if ! grep -q "$d" "$log"; then
        echo "FAIL $id: missing expected diagnostic $d ($log)"
        missing=1
        fail=1
      fi
    done
    [[ $missing -eq 0 ]] && echo "PASS $id: exit $rc with expected diagnostics [$diags]"
  fi
done

exit $fail
