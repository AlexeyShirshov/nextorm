#!/usr/bin/env bash
# T222 (#222) frozen rv=1 capture runner.
#
# Usage: bash "$B/capture.sh" <row-id> -- <command...>
#
# Writes B/<row-id>/{command.txt,stdout.log,stderr.log,exitcode.txt,receipt.json}
# and returns the invoked command's exit code (never masks pipeline failures).
set -u

B="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

row="${1:-}"
if [[ -z "$row" || "${2:-}" != "--" || $# -lt 3 ]]; then
  echo "usage: $0 <row-id> -- <command...>" >&2
  exit 2
fi
shift 2
cmd=("$@")

out="$B/$row"
mkdir -p "$out"
printf '%s\n' "${cmd[@]}" > "$out/command.txt"

rev="$(git -C "$B" rev-parse HEAD 2>/dev/null || echo unknown)"
branch="$(git -C "$B" rev-parse --abbrev-ref HEAD 2>/dev/null || echo unknown)"
started="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

"${cmd[@]}" > "$out/stdout.log" 2> "$out/stderr.log"
rc=$?

finished="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
printf '%s\n' "$rc" > "$out/exitcode.txt"

json_cmd="$(printf '%s\n' "${cmd[@]}" \
  | python3 -c 'import json,sys; print(json.dumps(sys.stdin.read().splitlines()))')"

cat > "$out/receipt.json" <<EOF
{
  "row_id": "$row",
  "rv": 1,
  "task": "T222",
  "issue": "#222",
  "cwd": "$PWD",
  "tree_revision": "$rev",
  "branch": "$branch",
  "started_utc": "$started",
  "finished_utc": "$finished",
  "command": $json_cmd,
  "exit_code": $rc,
  "artifacts": {
    "command": "$out/command.txt",
    "stdout": "$out/stdout.log",
    "stderr": "$out/stderr.log",
    "exitcode": "$out/exitcode.txt"
  }
}
EOF

echo "capture[$row] exit=$rc -> $out"
exit $rc
