#!/usr/bin/env bash
# D226 / rv2 — fail-closed mutation verifier entry point (P226-R2-03).
#
# Delegates to mutation-runner.py (the fail-closed implementation: actual per-restore byte
# comparisons, a verified kill classifier, infrastructure rejection, report written after the final
# restoration) and propagates its exit code unchanged — no pipeline, no `tee`, no masking.
#
# Usage: bash artifacts/pdca/D226/rv2/verify-mutations.sh
# Artifacts: $R/mutation.log, $R/mutation-report.json, $R/mutation.exit, $R/mutations/*.patch
set -Eeuo pipefail

R="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec python3 "$R/mutation-runner.py"
