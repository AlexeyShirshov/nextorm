#!/usr/bin/env bash
# T221 (#221) frozen coverage driver (rv=1).
#
# Usage: bash artifacts/pdca/D221/rv1/coverage-driver.sh <baseline|after>
#
# The driver is executed *inside* `dotnet-coverage collect`, so every dotnet test
# invocation below is instrumented. It records the comparable existing selectors in
# both phases and the added feature selectors (Category=T221) only in the `after`
# phase, so baseline and after numbers stay comparable.
#
# Exit code is nonzero as soon as any instrumented test command fails.
set -u
phase="${1:-}"
if [[ "$phase" != "baseline" && "$phase" != "after" ]]; then
  echo "usage: $0 <baseline|after>" >&2
  exit 2
fi

run() {
  echo "## [coverage-driver:$phase] $*"
  "$@"
  local rc=$?
  if [[ $rc -ne 0 ]]; then
    echo "## [coverage-driver:$phase] FAILED (exit $rc): $*" >&2
    exit $rc
  fi
}

DOCKER_HOST="${DOCKER_HOST:-unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock}"
export DOCKER_HOST

# --- comparable existing selectors (both phases) ---
run dotnet test tests/nextorm.core.tests -c Debug --no-build \
  --filter "FullyQualifiedName~NextORM.Core.Tests.InterceptorBuilderTests|FullyQualifiedName~NextORM.Core.Tests.DependencyInjectionTests"
run dotnet run --project tests/nextorm.sqlite.tests -c Debug --no-build -- \
  -class NextORM.Sqlite.Tests.InterceptorTests -noColor
run dotnet test tests/nextorm.integration.tests -c Debug --no-build \
  --filter "FullyQualifiedName~CoreApiContractTests"

# --- added feature selectors (after phase only) ---
if [[ "$phase" == "after" ]]; then
  run dotnet run --project tests/nextorm.core.tests -c Debug --no-build -- -trait "Category=T221" -noColor
  run dotnet run --project tests/nextorm.sqlite.tests -c Debug --no-build -- -trait "Category=T221" -noColor
  run dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter "Category=T221"
fi

echo "## [coverage-driver:$phase] done"
