#!/usr/bin/env bash
# D226 / rv1 — D1 real-driver probe runner.
#
# Reproduces the D1 evidence from tree-bound sources:
#   bash artifacts/pdca/D226/rv1/run-d1-probe.sh
#
# Writes:
#   artifacts/pdca/D226/rv1/release-probe.json   (the probe's JSON document)
#   artifacts/pdca/D226/rv1/logs/d1-probe.log    (build + run transcript, exit codes)
#
# Providers (NEXTORM_PROBE_PROVIDERS): sqlite (in-process, always available), postgres,
# sqlserver, mysql, clickhouse (Testcontainers, requires the Podman socket via DOCKER_HOST).
set -uo pipefail

R="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOG="$R/logs/d1-probe.log"
JSON="$R/release-probe.json"
export DOCKER_HOST="${DOCKER_HOST:-unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock}"

mkdir -p "$R/logs"

{
  echo "== D226 D1 probe =="
  echo "date_utc: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "dotnet: $(dotnet --version)"
  echo "DOCKER_HOST: $DOCKER_HOST"
  echo "NEXTORM_PROBE_PROVIDERS: ${NEXTORM_PROBE_PROVIDERS:-<default: all>}"
  echo
  echo "== build =="
  dotnet build "$R/probe/Probe.csproj" -c Debug
  build_exit=$?
  echo "build_exit_code: $build_exit"
  if [ "$build_exit" -ne 0 ]; then
    echo "probe_aborted: build failed"
    exit "$build_exit"
  fi

  echo
  echo "== run =="
  dotnet run --project "$R/probe/Probe.csproj" -c Debug --no-build -- "$JSON" > "$R/logs/d1-probe-stdout.log"
  run_exit=$?
  echo "run_exit_code: $run_exit"
  echo
  echo "== json summary =="
  cat "$JSON"
  echo
  echo "probe_exit_code: $run_exit"
} 2>&1 | tee "$LOG" > /dev/null

# The script's own exit code mirrors the probe run's exit code.
probe_exit="$(sed -n 's/^probe_exit_code: //p' "$LOG" | tail -1)"
echo "probe_exit_code: ${probe_exit:-1}"
exit "${probe_exit:-1}"
