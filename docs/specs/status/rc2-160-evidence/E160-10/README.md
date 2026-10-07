# E160-10 — coverage (D160, plan r=2, rv=3)

Reproduced the `.github/workflows/dotnet.yml` coverage steps locally, with `DOCKER_HOST` set so the
container-backed integration tests actually execute (no provider skips).

> **STEP-3 re-run (post-C1).** The C1 core fix (`SqlBuilder.MakeSelect` / `EntityBuilder` alias-state
> rebasing, commit `9d6a9f59`) changed compiled sources after the first coverage run, so the coverage
> run was repeated on the current tree (`4e2f43b2` + D160 evidence). The canonical
> `Summary.txt` / `coverage.cobertura.xml` / `build.log` / `coverage-collect.log` /
> `coverage-report.log` / `assembly-coverage.txt` below are the fresh STEP-3 run. The earlier n=1
> copies were overwritten; there is no separate `-step3` filename for this row.

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
- collect: exit **0**; total 9216 tests, failed **0**, succeeded 9018, skipped 198
  (`coverage-collect.log`); wall 65 s
- reportgenerator: exit **0** (`coverage-report.log`)

## Coverage vs thresholds (line ≥85% / branch ≥75%)

| Metric | Value | Threshold | Verdict |
|--------|-------|-----------|---------|
| Line coverage | **88.1%** (48348 / 54834) | 85% | PASS |
| Branch coverage | **80.2%** (25956 / 32355) | 75% | PASS |

Per-assembly (coverage.settings.xml scope): `nextorm.core` 88%, `nextorm.sqlite` 90.5%,
`nextorm.postgres` 90.1%, `nextorm.sqlserver` 94.9%.

Both thresholds pass on this branch; no D160 coverage gap to close. Full summary: `Summary.txt`;
raw Cobertura: `coverage.cobertura.xml`; per-assembly table: `assembly-coverage.txt`.
