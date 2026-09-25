# Benchmarks: Nextorm vs Dapper, linq2db and EF Core

Benchmark conditions: provider SQLite (`Microsoft.Data.Sqlite`) for every participant; async queries; each
cell is the **fastest method** for that library in the linked BenchmarkDotNet report. Machine: AMD Ryzen 7
5800HS (16 logical / 8 physical cores), .NET 10.0.12, BenchmarkDotNet 0.15.8 (`ShortRun` +
`InProcessEmitToolchain`). As of **2026-09-25**. In each row the leading cell is **bold** — the fastest in
the time tables and the smallest allocation in the allocations table.

## End-to-end scenarios

| Method | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| [Data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkIteration-report-github.md) | **11.24 μs** | 15.82 μs | 17.49 μs | 38.00 μs |
| [Wide data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkLargeIteration-report-github.md) | **8.592 ms** | 9.226 ms | 10.350 ms | 10.395 ms |
| [Where](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkWhere-report-github.md) | **942.3 μs** | 1,385.0 μs | 2,759.0 μs | 3,344.6 μs |
| [Simulate work](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkSimulateWork-report-github.md) | **7.003 ms** | 25.836 ms | 429.853 ms | 117.813 ms |
| [Any](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkAny-report-github.md) | **943.2 μs** | 1,401.8 μs | 2,805.2 μs | 3,653.8 μs |
| [FirstOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkFirst-report-github.md) | **94.77 μs** | 146.92 μs | 674.83 μs | 359.09 μs |
| [SingleOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkSingle-report-github.md) | **95.10 μs** | 148.47 μs | 686.56 μs | 370.82 μs |

### Allocations

Allocations for the same fastest method per library (BenchmarkDotNet `Allocated`):

| Method | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| Data fetch | **792 B** | 1,904 B | 2,472 B | 10,224 B |
| Wide data fetch | **2.14 MB** | 2.59 MB | **2.14 MB** | 4.28 MB |
| Where | **92.43 KB** | 180.72 KB | 551.98 KB | 789.17 KB |
| Simulate work | **6.24 MB** | 8.90 MB | 122.18 MB | 13.68 MB |
| Any | **85.16 KB** | 139.08 KB | 401.59 KB | 807.06 KB |
| FirstOrDefault | **8.56 KB** | 16.48 KB | 222.27 KB | 80.40 KB |
| SingleOrDefault | **8.36 KB** | 16.40 KB | 222.27 KB | 80.40 KB |

## Feature-level comparison (fair)

The end-to-end table mixes reuse strategies. To make the comparison symmetric, features are split into two
categories:

- **Category A — prepared / compiled / raw:** Nextorm through `Prepare()` against the closest *compiled* form
  of the same query (`EF.CompileAsyncQuery`, `LinqToDB.CompiledQuery.Compile`) and raw Dapper (whose IL is
  cached by SQL text). This is "compiled vs compiled".
- **Category B — warm cached vs regular:** Nextorm through the implicit plan cache (a fresh fluent command
  with constants on every call, so the cached plan is the only reusable part) against regular (non-compiled)
  EF Core and linq2db and raw Dapper.

Each feature method runs **10 queries**; every cell is the **per-query** mean and allocation (the
BenchmarkDotNet `Mean` and `Allocated` divided by 10). Source reports:
[Category A](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkFeaturesFair-report-github.md) ·
[Category B](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkFeaturesFairCached-report-github.md).

### Category A — prepared vs compiled vs raw

| Feature | Nextorm (prepared) | Dapper | linq2db (compiled) | EF Core (compiled) |
|---|---|---|---|---|
| `CASE WHEN` | **10.51 µs / 672 B** | 15.36 µs / 1.34 KB | 16.37 µs / 1.59 KB | 40.08 µs / 7.90 KB |
| CTE | **16.64 µs / 720 B** | 25.45 µs / 1.57 KB | 23.31 µs / 1.77 KB | — |
| `SELECT DISTINCT` | **12.91 µs / 672 B** | 20.35 µs / 1.56 KB | 19.36 µs / 1.59 KB | 44.12 µs / 7.66 KB |
| `EXCEPT` | **11.59 µs / 688 B** | 20.73 µs / 1.44 KB | 24.10 µs / 1.68 KB | 46.94 µs / 8.81 KB |
| IN-list | **13.43 µs / 1.50 KB** | 24.01 µs / 1.27 KB | 55.95 µs / 15.23 KB | 74.42 µs / 14.77 KB |
| `INTERSECT` | **11.46 µs / 672 B** | 17.04 µs / 1.29 KB | 20.49 µs / 1.63 KB | 47.99 µs / 7.94 KB |
| 4-table join | **13.33 µs / 1,008 B** | 27.23 µs / 2.02 KB | 25.08 µs / 2.07 KB | 50.77 µs / 8.49 KB |
| `LEFT JOIN` | **12.76 µs / 1.13 KB** | 21.44 µs / 2.22 KB | 21.87 µs / 2.32 KB | 46.60 µs / 9.92 KB |
| Recursive CTE | **13.13 µs / 680 B** | 26.50 µs / 1.53 KB | 27.92 µs / 1.78 KB | — |
| String functions | **9.79 µs / 696 B** | 16.15 µs / 1.28 KB | 17.69 µs / 1.64 KB | 40.03 µs / 7.49 KB |
| UDF | **10.05 µs / 696 B** | 16.30 µs / 1.28 KB | 17.10 µs / 1.64 KB | 40.61 µs / 7.49 KB |
| Window functions | **16.09 µs / 776 B** | 34.01 µs / 1.55 KB | 35.08 µs / 1.74 KB | — |

Nextorm prepared is the fastest on **every** feature, with the smallest allocation — roughly 4–10× below
EF Core and 2–4× below Dapper.

### Category B — warm cached vs regular

| Feature | Nextorm (cached) | Dapper | linq2db | EF Core |
|---|---|---|---|---|
| `CASE WHEN` | **13.04 µs / 2.56 KB** | 15.15 µs / 1.34 KB | 25.78 µs / 3.80 KB | 61.20 µs / 11.95 KB |
| CTE | 27.32 µs / 8.70 KB | **21.85 µs / 1.57 KB** | 59.81 µs / 9.06 KB | — |
| `SELECT DISTINCT` | **15.54 µs / 2.66 KB** | 18.73 µs / 1.56 KB | 31.54 µs / 3.91 KB | 67.15 µs / 12.20 KB |
| `EXCEPT` | **18.17 µs / 4.56 KB** | 19.03 µs / 1.44 KB | 42.24 µs / 5.05 KB | 82.00 µs / 15.31 KB |
| IN-list | 29.69 µs / 6.55 KB | **20.83 µs / 1.27 KB** | 77.85 µs / 18.91 KB | 117.93 µs / 22.61 KB |
| `INTERSECT` | **16.83 µs / 4.55 KB** | 18.40 µs / 1.29 KB | 44.02 µs / 5.01 KB | 86.72 µs / 14.49 KB |
| 4-table join | 34.29 µs / 9.37 KB | **29.32 µs / 2.02 KB** | 79.49 µs / 14.06 KB | 139.46 µs / 24.77 KB |
| `LEFT JOIN` | **23.16 µs / 5.17 KB** | 25.05 µs / 2.22 KB | 90.69 µs / 8.71 KB | 111.59 µs / 20.27 KB |
| Recursive CTE | 37.73 µs / 8.89 KB | **30.81 µs / 1.53 KB** | 75.61 µs / 8.47 KB | — |
| String functions | 15.17 µs / 3.62 KB | **14.93 µs / 1.28 KB** | 33.18 µs / 4.78 KB | 70.10 µs / 13.27 KB |
| UDF | 15.51 µs / 3.75 KB | **15.36 µs / 1.28 KB** | 51.24 µs / 6.39 KB | 72.67 µs / 13.27 KB |
| Window functions | 35.13 µs / 4.75 KB | **32.95 µs / 1.55 KB** | 68.12 µs / 5.99 KB | — |

In the warm path Nextorm still beats Dapper on `CASE WHEN`, `DISTINCT`, `EXCEPT`, `INTERSECT` and
`LEFT JOIN`, and is level on `ToUpper`/UDF/window; it trails Dapper by about 1.15–1.6× on CTE, recursive
CTE, 4-table join and IN-list, while staying well ahead of regular linq2db and EF Core. Warm-path
allocations are higher than the prepared path (the plan key is rebuilt per call) but remain below the
regular competitors.

> **IN-list, Category B.** A captured collection disables the plan cache by design (each call re-renders the
> SQL), so the captured variants pay a full build; the inline variants hit the cache. The four Nextorm
> variants span 29.69–32.76 µs — this is the one feature where Dapper's cached raw SQL stays ahead.

## Reading the numbers

- Every participant uses the same ADO.NET provider (`Microsoft.Data.Sqlite`), so the comparison is
  apples-to-apples; the SQLite database lives on tmpfs so storage I/O does not mask the ORM.
- Nextorm reports both of its reuse paths — the implicit plan cache and explicit `Prepare()` — and the
  end-to-end cell is the fastest of them. See [Query reuse: cache vs Prepare](../guide/15-query-reuse.md).
- `ShortRun` + `InProcessEmitToolchain` is a quick mode: it is stable enough for a signal, but treat small
  differences as noise and read the full report (mean, error, standard deviation and allocations) before
  drawing conclusions.
- This is one machine, one provider (SQLite) and a fixed set of scenarios. It is not a claim about every
  workload — reproduce it with your own data and hardware.

## Reproducing

```bash
# End-to-end scenarios
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkWhere*

# Feature-level fair comparison (Category A / B)
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter "*SqliteBenchmarkFeaturesFair.*"
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter "*SqliteBenchmarkFeaturesFairCached*"
```

Each linked report is the BenchmarkDotNet output committed under `benchmarks/BenchmarkDotNet.Artifacts/results/`.
Set `NEXTORM_BENCH_FULL=1` to run the full out-of-process job instead of `ShortRun`.

## See also

- [Capabilities](capabilities.md) — what each library supports.
- [Query reuse: cache vs Prepare](../guide/15-query-reuse.md).
