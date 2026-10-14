#!/usr/bin/env bash
set -u
cd /home/alex/sources/nextorm
export DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock
dotnet test tests/nextorm.core.tests -c Debug --no-build --verbosity normal
dotnet test tests/nextorm.sqlite.tests -c Debug --no-build --verbosity normal
dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter FullyQualifiedName~RecordsAffected --verbosity normal
