# E07 — iteration-14 allocation gate (C7), issue #166

Args (exact): `["python3", "eng/perf/iteration14_gate.py"]`

Env: `DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0`, `timeout 900` (outer cap).

- exit code = **0**
- gate runs `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter <3 classes>
  --job short --exporters json`; benchmark sub-run exit code = **0**, wall = **843.3 s** (under the
  900 s outer cap; gate default `--timeout 1800`)
- **All 56 row/job verdicts within budget.**
- log: `docs/specs/status/nested-read-cte-1-evidence/E07-gate.log`

## Zero-budget CTE rows (must be exactly 0 B/op, both toolchains)

| Row | Toolchain | B/op | Budget |
|-----|-----------|-----:|-------:|
| `Cte_Warm_Reused` | Default | 0.00 | 0 |
| `Cte_Warm_Reused` | InProcessEmitToolchain | 0.00 | 0 |
| `RecursiveCte_Warm_Reused` | Default | 0.00 | 0 |
| `RecursiveCte_Warm_Reused` | InProcessEmitToolchain | 0.00 | 0 |
| `Join4_Warm_Reused` | Default | 0.00 | 0 |
| `Join4_Warm_Reused` | InProcessEmitToolchain | 0.00 | 0 |

Flat/recursive warm-reuse zero CTE budgets preserved. Archived raw BDN JSON reports (copied into
the evidence dir before the root artifacts dir was restored):

- `E07-SqliteBenchmarkWarmDecompose-report-full-compressed.json`
- `E07-SqliteBenchmarkCachedPlan-report-full-compressed.json`
- `E07-SqliteBenchmarkFeaturePlanBuild-report-full-compressed.json`

Root artifacts dir (`BenchmarkDotNet.Artifacts`, the fallback path because `nextorm.sln` does not
exist — the repo has `nextorm.slnx`) was restored with `git checkout -- BenchmarkDotNet.Artifacts`
afterwards; pre-existing untracked files under `benchmarks/BenchmarkDotNet.Artifacts` were not
touched.
