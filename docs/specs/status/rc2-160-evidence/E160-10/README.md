# E160-10 — coverage (D160, plan r=2, rv=3)

Reproduced the `.github/workflows/dotnet.yml` coverage steps locally, with `DOCKER_HOST` set so the
container-backed integration tests actually execute (no provider skips).

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
- collect: exit **0**; total 9189 tests, failed **0**, succeeded 8991, skipped 198
  (`coverage-collect.log`); wall 65 s
- reportgenerator: exit **0** (`coverage-report.log`)

## Coverage vs thresholds (line ≥85% / branch ≥75%)

| Metric | Value | Threshold | Verdict |
|--------|-------|-----------|---------|
| Line coverage | **88.1%** (48262 / 54745) | 85% | PASS |
| Branch coverage | **80.1%** (25884 / 32287) | 75% | PASS |

Per-assembly (coverage.settings.xml scope): `nextorm.core` 87.9%, `nextorm.sqlite` 90.5%,
`nextorm.postgres` 90.1%, `nextorm.sqlserver` 94.9%.

Both thresholds pass on this branch; no D160 coverage gap to close. Full summary: `Summary.txt`;
raw Cobertura: `coverage.cobertura.xml`; per-assembly table: `assembly-coverage.txt`.
