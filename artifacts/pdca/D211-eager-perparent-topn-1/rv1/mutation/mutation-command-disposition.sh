#!/usr/bin/env bash
# T211 D:06 mutation disposition re-run: corrected config (byte-span mutate scope, core+sqlite test
# projects), run from the mutation dir, no DOCKER_HOST. Includes the two new Feature=D211 branch
# follow-up tests. Output goes to run-survivors/; the canonical mutation-report.{json,html} copies
# are refreshed afterwards by the caller.
set -u
cd "$(dirname "$0")"
exec dotnet tool run dotnet-stryker -- \
  --config-file stryker-config.corrected.json \
  --output run-survivors \
  --concurrency 4 \
  --skip-version-check \
  --verbosity info
