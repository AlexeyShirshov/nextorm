#!/usr/bin/env bash
# D226 / rv2 — strict adapter to the installed pdca-dotnet manifest validator.
# Frozen argv is recorded in validator-command.json; argv + result are logged in validation.log.
# No replacement validator, no bypass: this only invokes scripts/validate_inner_loop.py manifest.
set -uo pipefail

R="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$R/../../../.."
cd "$REPO" || exit 2

ARGV=(python3 scripts/validate_inner_loop.py manifest "artifacts/pdca/D226/rv2/manifest.json")
{
  echo "argv: ${ARGV[*]}"
  echo "cwd: $REPO"
  "${ARGV[@]}"
  rc=$?
  echo "result_exit_code: $rc"
  echo "validated_at_utc: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "validator_script: scripts/validate_inner_loop.py (installed pdca-dotnet validator, unmodified)"
} > "$R/validation.log" 2>&1
rc=$("${ARGV[@]}" > /dev/null 2>&1; echo $?)
echo "validator_exit=$rc"
exit "$rc"
