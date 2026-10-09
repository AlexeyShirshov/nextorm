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

## Tier-1 query-shape scenarios

Added in #188 to cover query shapes the two tables above do not exercise: projection into a fresh DTO,
SQL-side aggregates, ordered paging, streaming (buffered/unbuffered DTO vs. zero-materialization raw
readers) and cross-library JSON/CSV output. Data size: `large_table` (10,000 rows) for
projection/aggregates/paging/streaming; the JSON class seeds its own `json_bench` table and the CSV
class slices `large_table` (10,000 rows). The fair-comparison fixture invariants stay `simple_entity`=10
and `complex_entity`=3 — the "10,000 rows" above is `large_table`, not those fixtures.

Reuse categories are the same as the feature comparison: **Category A** — Nextorm `Prepare()` against the
closest *compiled* form (`EF.CompileAsyncQuery`, `LinqToDB.CompiledQuery.Compile`) plus raw Dapper;
**Category B** — Nextorm's implicit plan cache against regular (non-compiled) EF Core/linq2db and raw
Dapper. Dapper has no compiled query/reader form, so its arms carry both categories.

**Streaming has three groups:** buffered DTO materialization, unbuffered DTO enumeration, and a
**zero-materialization** raw-reader subgroup. The zero subgroup is synchronous only (async reader arms are
deferred): Nextorm `ToDataReader`, Dapper `ExecuteReader` and linq2db `ExecuteReader` (its `DataReaderAsync.Reader`)
all read the same `id, someString` projection through **one shared non-retaining sink**. EF Core is
explicitly **not** in the zero subgroup — it has no LINQ raw-reader counterpart. "Zero-materialization"
means no entity/DTO construction, **not** zero allocations: the reader path still allocates its per-call
command/reader.

For JSON/CSV the competitor arms are a **closest equivalent**: neither Dapper, linq2db nor EF Core has a
non-materialising output terminal, so their arm is materialize → manual serializer. This is not a
zero-materialization claim.

> **Reading these numbers.** `ShortRun` + `InProcessEmitToolchain` is exploratory: treat deltas below ~20%
> as noise and read the full report (error, standard deviation, allocations) before drawing conclusions.
> The tables report BenchmarkDotNet `Mean`/`Allocated` as-is; they are not a universal-win claim.

### Projection — `Select(x => new Dto { Id, Str })`

Reproduce: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkProjection*`.
[Report](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkProjection-report-github.md).

| Arm | Category | Mean | Allocated |
|---|---|---:|---:|
| A_Nextorm_Prepared_ToListAsync | A | 3.653 ms | 1.3 MB |
| A_EFCore_Compiled_ToListAsync | A | 4.904 ms | 3.36 MB |
| A_Linq2Db_Compiled_ToList | A | 5.522 ms | 1.47 MB |
| A_Dapper_ToListAsync | A+B | 6.464 ms | 1.7 MB |
| B_Nextorm_Cached_ToListAsync | B | 3.799 ms | 1.3 MB |
| B_Linq2Db_ToListAsync | B | 6.472 ms | 1.48 MB |
| B_EFCore_ToListAsync | B | 8.533 ms | 3.62 MB |

### Aggregates — SQL-side `Count` / `Sum` / `GroupBy → Count`

Reproduce: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkAggregates*`.
[Report](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkAggregates-report-github.md).

| Operation | Arm | Category | Mean | Allocated |
|---|---|---:|---:|---:|
| Count | A_Nextorm_Prepared_Count | A | 13.77 μs | 408 B |
| Count | A_Linq2Db_Compiled_Count | A | 18.02 μs | 1,280 B |
| Count | A_EFCore_Compiled_Count | A | 38.62 μs | 7,744 B |
| Count | Dapper_Count | A+B | 17.30 μs | 944 B |
| Count | B_Nextorm_Cached_Count | B | 19.31 μs | 3,824 B |
| Count | B_Linq2Db_Count | B | 24.56 μs | 2,864 B |
| Count | B_EFCore_Count | B | 48.88 μs | 10,080 B |
| Sum | A_Nextorm_Prepared_Sum | A | 277.57 μs | 410 B |
| Sum | A_Linq2Db_Compiled_Sum | A | 291.81 μs | 1,290 B |
| Sum | A_EFCore_Compiled_Sum | A | 319.95 μs | 7,770 B |
| Sum | Dapper_Sum | A+B | 291.59 μs | 954 B |
| Sum | B_Nextorm_Cached_Sum | B | 286.43 μs | 4,226 B |
| Sum | B_Linq2Db_Sum | B | 304.90 μs | 3,490 B |
| Sum | B_EFCore_Sum | B | 338.05 μs | 10,978 B |
| GroupBy→Count | A_Nextorm_Prepared_GroupByCount | A | 1,281.91 μs | 4,737 B |
| GroupBy→Count | A_Linq2Db_Compiled_GroupByCount | A | 1,309.32 μs | 7,025 B |
| GroupBy→Count | A_EFCore_Compiled_GroupByCount | A | 1,343.09 μs | 33,089 B |
| GroupBy→Count | Dapper_GroupByCount | A+B | 1,317.46 μs | 12,329 B |
| GroupBy→Count | B_Nextorm_Cached_GroupByCount | B | 1,314.02 μs | 10,577 B |
| GroupBy→Count | B_Linq2Db_GroupByCount | B | 1,393.44 μs | 11,553 B |
| GroupBy→Count | B_EFCore_GroupByCount | B | 1,407.25 μs | 42,641 B |

### Paging — `OrderBy(Id).Offset(5000).Limit(100)`

Reproduce: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkPaging*`.
[Report](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkPaging-report-github.md).

| Arm | Category | Mean | Allocated |
|---|---|---:|---:|
| A_Nextorm_Prepared_PageAsync | A | 87.54 μs | 13.99 KB |
| A_Linq2Db_Compiled_Page | A | 107.44 μs | 17.61 KB |
| A_EFCore_Compiled_PageAsync | A | 130.82 μs | 44.81 KB |
| A_Dapper_PageAsync | A+B | 101.53 μs | 18.15 KB |
| B_Nextorm_Cached_PageAsync | B | 99.68 μs | 22.48 KB |
| B_Linq2Db_PageAsync | B | 138.21 μs | 22.79 KB |
| B_EFCore_PageAsync | B | 185.63 μs | 56.9 KB |

### Streaming — buffered / unbuffered DTO and zero-materialization readers

Reproduce: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkStreaming*`.
[Report](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkStreaming-report-github.md).

Buffered DTO:

| Arm | Category | Mean | Allocated |
|---|---|---:|---:|
| A_Nextorm_Prepared_ToList_Dto | A | 3.743 ms | 1,328.84 KB |
| A_EFCore_Compiled_ToList_Dto | A | 8.012 ms | 3,701.54 KB |
| Linq2Db_Compiled_ToList_Dto | A | 5.637 ms | 1,508.25 KB |
| Dapper_ToList_Dto | A+B | 6.864 ms | 1,742.31 KB |
| B_Nextorm_Cached_ToList_Dto | B | 3.786 ms | 1,332.39 KB |
| B_Linq2Db_ToList_Dto | B | 8.148 ms | 1,511.3 KB |
| B_EFCore_ToList_Dto | B | 8.594 ms | 3,705.59 KB |

Unbuffered DTO enumeration:

| Arm | Category | Mean | Allocated |
|---|---|---:|---:|
| A_Nextorm_Prepared_AsyncStream_Dto | A | 3.663 ms | 1,250.56 KB |
| A_EFCore_Compiled_AsyncStream_Dto | A | 5.047 ms | 3,444.77 KB |
| Dapper_AsyncStream_Dto | A+B | 4.591 ms | 1,485.82 KB |
| B_Nextorm_Cached_AsyncStream_Dto | B | 4.019 ms | 1,254.3 KB |
| B_Linq2Db_AsyncStream_Dto | B | 4.595 ms | 1,254.55 KB |
| B_EFCore_AsyncStream_Dto | B | 5.105 ms | 3,448.89 KB |

Zero-materialization raw readers (synchronous; shared non-retaining sink; EF Core excluded):

| Arm | Category | Mean | Allocated |
|---|---|---:|---:|
| A_Nextorm_Prepared_ToDataReader | A | 4.302 ms | 944.8 KB |
| Linq2Db_ToDataReader | B | 3.762 ms | 943 KB |
| Dapper_ToDataReader | B | 3.761 ms | 942.77 KB |

### JSON / CSV cross-library

Reproduce JSON: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkWriteJson*`.
[Report](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWriteJson-report-github.md).
Reproduce CSV: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkCsv*`.
[Report](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkCsv-report-github.md).

The JSON report covers `RowCount` 1,000 / 10,000 / 100,000; the 10,000-row block is shown (Ratio is
relative to `ToList_Dto` in the same block). Competitor arms are materialize → manual serializer.

| Method (10,000 rows) | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| WriteJson_Array_Scalar | 5,724.3 μs | 0.93 | 38.04 KB |
| ToList_Dto | 6,170.0 μs | 1.00 | 785.21 KB |
| WriteJsonAsync_Array_Scalar | 6,620.2 μs | 1.07 | 38.25 KB |
| Linq2Db_ToList_Json | 9,150.2 μs | 1.48 | 963.56 KB |
| Dapper_ToList_Json | 9,415.9 μs | 1.53 | 1,273.78 KB |
| WriteJson_Array_ScalarPayload | 10,341.2 μs | 1.68 | 11,495.22 KB |
| WriteJson_Array_Dto | 10,351.9 μs | 1.68 | 435.64 KB |
| WriteJsonAsync_Array_ScalarPayload | 11,258.5 μs | 1.83 | 11,495.38 KB |
| EFCore_ToList_Json | 11,303.0 μs | 1.83 | 3,158.67 KB |
| WriteJson_Array_WideDto | 13,801.5 μs | 2.24 | 11,527.94 KB |

CSV (`large_table` slice, 10,000 rows; Ratio relative to `Nextorm_ToList`):

| Method | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| Nextorm_StringProjection_ToList | 2.819 ms | 0.33 | 1,019.52 KB |
| Nextorm_StringProjection_WriteCsv | 4.029 ms | 0.47 | 949.67 KB |
| Nextorm_ToList | 8.558 ms | 1.00 | 2,267.38 KB |
| Nextorm_ToList_ManualCsv | 9.927 ms | 1.16 | 4,141.67 KB |
| Nextorm_WriteCsv | 11.076 ms | 1.29 | 1,743.61 KB |
| Dapper_ToList_ManualCsv | 13.291 ms | 1.55 | 4,865.94 KB |
| Linq2Db_ToList_ManualCsv | 13.385 ms | 1.56 | 4,322.11 KB |
| EFCore_ToList_ManualCsv | 14.364 ms | 1.68 | 6,516.97 KB |

## Reading the numbers

- Every participant uses the same ADO.NET provider (`Microsoft.Data.Sqlite`), so the comparison is
  apples-to-apples; the SQLite database lives on tmpfs so storage I/O does not mask the ORM.
- Nextorm reports both of its reuse paths — the implicit plan cache and explicit `Prepare()` — and the
  end-to-end cell is the fastest of them. See [Query reuse: cache vs Prepare](../infrastructure/01-query-reuse-and-caching.md).
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
- [Query reuse: cache vs Prepare](../infrastructure/01-query-reuse-and-caching.md).
