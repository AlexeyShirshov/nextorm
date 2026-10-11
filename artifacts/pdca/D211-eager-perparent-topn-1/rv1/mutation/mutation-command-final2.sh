#!/usr/bin/env bash
# T211 final mutation re-run (DO correction r1 n2/3, refresh) — spans recomputed from
# `git diff -U0 b527be6f -- src/**/*.cs` on the FINAL corrected tree (post-02:35 edits),
# mapped to CRLF raw-byte offsets by gen-mutate-spans.py. Core+sqlite test projects,
# run from the mutation dir, no DOCKER_HOST. Output goes to run-final2/; the canonical
# mutation-report.{json,html} are refreshed afterwards.
set -u
cd "$(dirname "$0")"
exec dotnet tool run dotnet-stryker -- \
  --config-file stryker-config.corrected.json \
  --output run-final2 \
  --concurrency 4 \
  --skip-version-check \
  --verbosity info
