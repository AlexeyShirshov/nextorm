#!/usr/bin/env bash
# D226 / rv2 — evidence runner: capture a real child exit code without pipeline masking.
#
# Usage: bash artifacts/pdca/D226/rv2/run-evidence.sh SLOT -- <cmd> [args...]
# Writes $R/<SLOT>.log (stdout+stderr) and $R/<SLOT>.exit (the child's real exit code),
# prints the tail of the log and exits with the child's code.
set -uo pipefail

R="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
slot="${1:?usage: run-evidence.sh SLOT -- CMD [args...]}"
shift
if [ "${1:-}" = "--" ]; then shift; fi
if [ $# -eq 0 ]; then
  echo "no command given"
  exit 2
fi

log="$R/$slot.log"
printf '%s\n' "$slot" > "$R/$slot.exit"

"$@" > "$log" 2>&1
rc=$?
printf '%s\n' "$rc" > "$R/$slot.exit"

echo "--- $slot (exit $rc) ---"
tail -8 "$log"
exit "$rc"
