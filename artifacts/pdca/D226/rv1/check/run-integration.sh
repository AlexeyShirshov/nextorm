#!/usr/bin/env bash
set -u
cd /home/alex/sources/nextorm
export DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock
OUT=artifacts/pdca/D226/rv1/check

run_full() {
  local log="$1"; shift
  echo "### ARGS: $*" > "$log"
  dotnet run "$@" >> "$log" 2>&1
  echo "EXIT=$?" >> "$log"
}

run_full "$OUT/integration-full.log" --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor
run_full "$OUT/integration-StoredProcedure.log" --project tests/nextorm.integration.tests -c Debug --no-build -- -method "*StoredProcedure*" -noColor
run_full "$OUT/integration-Raw.log" --project tests/nextorm.integration.tests -c Debug --no-build -- -method "*Raw*" -noColor
run_full "$OUT/integration-Batch.log" --project tests/nextorm.integration.tests -c Debug --no-build -- -method "*Batch*" -noColor
touch "$OUT/INTEGRATION_SEQ_DONE"
