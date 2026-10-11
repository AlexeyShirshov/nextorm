#!/usr/bin/env bash
# T211 final mutation re-run (DO correction r1 n2/3): corrected CRLF byte-span config, core+sqlite
# test projects, run from the mutation dir, no DOCKER_HOST. Includes the final Feature=D211 tests
# (composite, Skip-only, ordinal/plan-cache and the two r1-n2 plan-key / null-parent-order tests).
# Output goes to run-final/; the canonical mutation-report.{json,html} are refreshed afterwards.
set -u
cd "$(dirname "$0")"
exec dotnet tool run dotnet-stryker -- \
  --config-file stryker-config.corrected.json \
  --output run-final \
  --concurrency 4 \
  --skip-version-check \
  --verbosity info
