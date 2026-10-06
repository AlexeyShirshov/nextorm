# D168 / #168 acceptance perf gate (D4) — ABBA BenchmarkDotNet

- baseline SHA: `6bf362958ee4336e83a2a7550e49038184ff1caf` (via `git archive`, no worktree/commit)
- candidate: current working tree (`1.0.9-rc1`, uncommitted D168 change)
- SDK: `10.0.401`; Release; in-process emit toolchain; fresh process per run
- rounds: 3 x A-B-B-A; iterations=20; warmups=5; filter `--filter *SqlServerBufferedNumericAcceptanceBenchmark* --anyCategories=acceptance`
- `[MemoryDiagnoser]`, `[BenchmarkCategory("acceptance")]`, 10,000 rows x 11 numeric columns (int/long/short/byte/decimal/double/float + int?/decimal? null and non-null), `OperationsPerInvoke=10000` (per-row units)

## Per-run results

| run | tree | Mean ns/row | StdErr | StdDev | Allocated B/row | Gen0 /1000 | GetValue calls (boxing) | typed reads | log |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| rr1-A1 | baseline | 201.14 | 0.762 | 3.407 | 464 | 142.000 | 9 | 0 | `/home/alex/sources/nextorm/artifacts/d168/bdn-rr1-A1.log` |
| rr1-B1 | candidate | 68.86 | 0.019 | 0.080 | 0 | 0.000 | 0 | 9 | `/home/alex/sources/nextorm/artifacts/d168/bdn-rr1-B1.log` |
| rr1-B2 | candidate | 68.64 | 0.044 | 0.195 | 0 | 0.000 | 0 | 9 | `/home/alex/sources/nextorm/artifacts/d168/bdn-rr1-B2.log` |
| rr1-A2 | baseline | 240.35 | 0.412 | 1.794 | 464 | 142.000 | 9 | 0 | `/home/alex/sources/nextorm/artifacts/d168/bdn-rr1-A2.log` |
| rr2-A1 | baseline | 209.34 | 0.641 | 2.795 | 464 | 142.000 | 9 | 0 | `/home/alex/sources/nextorm/artifacts/d168/bdn-rr2-A1.log` |
| rr2-B1 | candidate | 69.40 | 0.115 | 0.513 | 0 | 0.000 | 0 | 9 | `/home/alex/sources/nextorm/artifacts/d168/bdn-rr2-B1.log` |
| rr2-B2 | candidate | 68.88 | 0.157 | 0.703 | 0 | 0.000 | 0 | 9 | `/home/alex/sources/nextorm/artifacts/d168/bdn-rr2-B2.log` |
| rr2-A2 | baseline | 199.48 | 0.382 | 1.619 | 464 | 142.000 | 9 | 0 | `/home/alex/sources/nextorm/artifacts/d168/bdn-rr2-A2.log` |
| rr3-A1 | baseline | 200.18 | 0.311 | 1.391 | 464 | 142.000 | 9 | 0 | `/home/alex/sources/nextorm/artifacts/d168/bdn-rr3-A1.log` |
| rr3-B1 | candidate | 69.58 | 0.064 | 0.285 | 0 | 0.000 | 0 | 9 | `/home/alex/sources/nextorm/artifacts/d168/bdn-rr3-B1.log` |
| rr3-B2 | candidate | 69.17 | 0.064 | 0.280 | 0 | 0.000 | 0 | 9 | `/home/alex/sources/nextorm/artifacts/d168/bdn-rr3-B2.log` |
| rr3-A2 | baseline | 196.86 | 0.301 | 1.313 | 464 | 142.000 | 9 | 0 | `/home/alex/sources/nextorm/artifacts/d168/bdn-rr3-A2.log` |

## Per-round ratio (candidate mean / baseline mean)

| round | ratio |
| --- | ---: |
| r1 | 0.3114 |
| r2 | 0.3382 |
| r3 | 0.3495 |
| **median** | **0.3382** |

## Gate verdict

- **Allocations**: candidate 0 B/row vs baseline 464 B/row — **drop** (baseline 464 B/row, Gen0 142/1000; candidate 0 B/row, Gen0 0).
- **Numeric-source boxing count** (strict-reader `GetValue` calls per row): candidate **0**, baseline 9 — **zero**.
- **Time**: median round ratio 0.3382 <= 1.05, no round > 1.20 (all ratios < 1).
- **VERDICT: PASS** — median ratio 0.3382 <= 1.05, no round > 1.20.

Raw JSON: `artifacts/d168/bdn-gate-report.json`; console: `artifacts/d168/bdn-gate-console.log`; per-run logs: `artifacts/d168/bdn-r*.log`.

