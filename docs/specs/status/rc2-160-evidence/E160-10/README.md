# E160-10 — coverage (D160.3-6 verification, plan r=3, rv=4)

Reproduced the `.github/workflows/dotnet.yml` coverage steps locally, with `DOCKER_HOST` set so the
container-backed integration tests actually execute (no provider-availability skips). This is the
canonical r=3 run on tree `40b1a159+dirty`; it supersedes the earlier r=2 / STEP-3 numbers.

## Commands and results

```
dotnet build --no-restore
mkdir -p tests/coverage
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura \
    -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"
dotnet tool run reportgenerator -reports:tests/coverage/coverage.cobertura.xml \
  -targetdir:tests/coverage/report -reporttypes:"Html;TextSummary;Cobertura" \
  -riskhotspotassemblyfilters:"+nextorm.*"
```

- build: exit **0**, 0 warnings / 0 errors (`build.log`)
- collect: exit **0**; total 9313 tests, failed **0**, succeeded 9112, skipped 201
  (`coverage-collect.log`); wall 86 s
- reportgenerator: exit **0** (`coverage-report.log`)

## Coverage vs thresholds (line ≥85% / branch ≥75%)

| Metric | Value | Threshold | Verdict |
|--------|-------|-----------|---------|
| Line coverage | **88.2%** (48398 / 54859) | 85% | PASS |
| Branch coverage | **80.2%** (25963 / 32355) | 75% | PASS |

Per-assembly (coverage.settings.xml scope): `nextorm.core` 88%, `nextorm.postgres` 90.1%,
`nextorm.sqlite` 90.5%, `nextorm.sqlserver` 94.9%.

Both thresholds pass on this branch; no D160 coverage gap to close. Full summary: `Summary.txt`;
raw Cobertura: `coverage.cobertura.xml`; per-assembly table: `assembly-coverage.txt`.

> The prior r=2 note (STEP-3 re-run) is superseded: the r=3 units D160.3-1..D160.3-5 changed
> compiled sources and tests, so this run is the current canonical evidence. The earlier
> `Summary.txt` / `coverage.cobertura.xml` were overwritten in place.
