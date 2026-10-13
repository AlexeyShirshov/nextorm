#!/usr/bin/env bash
# T221 (#221) additive coverage driver (rv=1 corrective): the added test selectors only.
# Complements the frozen coverage-driver.sh so the added-test contribution is separable from the
# comparable existing-selector coverage.
set -u
run() {
  echo "## [coverage-added] $*"
  "$@"
  local rc=$?
  if [[ $rc -ne 0 ]]; then echo "## [coverage-added] FAILED (exit $rc)" >&2; exit $rc; fi
}
DOCKER_HOST="${DOCKER_HOST:-unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock}"
export DOCKER_HOST

run dotnet run --project tests/nextorm.core.tests -c Debug --no-build -- -trait "Category=T221" -noColor
run dotnet run --project tests/nextorm.sqlite.tests -c Debug --no-build -- -trait "Category=T221" -noColor
run dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter "Category=T221"
echo "## [coverage-added] done"
