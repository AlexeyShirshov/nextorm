# Benchmarks: Nextorm vs Dapper, linq2db and EF Core

<!-- benchmarks:conditions:start -->
Benchmark conditions: provider SQLite (`Microsoft.Data.Sqlite`) for every participant; async queries; each
cell is the **fastest method** for that library in the linked BenchmarkDotNet report. Machine: AMD Ryzen 7
5800HS with Radeon Graphics (8 logical / 4 physical cores), .NET 10.0.12, BenchmarkDotNet 0.15.8 (`ShortRun` +
`InProcessEmitToolchain`). As of **2026-10-17** for the end-to-end, allocations, Category A and Category B
tables (the tier-1 section keeps the date of its own run). In each row the leading cell is **bold** — the
fastest in the time tables and the smallest allocation in the allocations table.
<!-- benchmarks:conditions:end -->

> **New (2026-10-17):** a warm-path-only feature report — no `Prepare()` arms, one table per tier,
> competitors as columns — is in [Feature benchmarks — 2026-10-17](feature-benchmarks-2026-10-17.md).

## End-to-end scenarios

<!-- benchmarks:e2e:start -->
| Method | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| [Data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkIteration-report-github.md) | **10.29 μs** | 14.89 μs | 15.76 μs | 36.64 μs |
| [Wide data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkLargeIteration-report-github.md) | **8.331 ms** | 8.680 ms | 9.366 ms | 9.771 ms |
| [Where](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWhere-report-github.md) | **886.7 μs** | 1,369.6 μs | 1,412.3 μs | 3,199.6 μs |
| [Simulate work](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSimulateWork-report-github.md) | **7.594 ms** | 26.432 ms | 393.986 ms | 110.812 ms |
| [Any](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkAny-report-github.md) | **889.9 μs** | 1,399.7 μs | 1,498.9 μs | 3,620.0 μs |
| [FirstOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFirst-report-github.md) | **91.35 μs** | 146.41 μs | 388.41 μs | 351.03 μs |
| [SingleOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSingle-report-github.md) | **90.87 μs** | 140.79 μs | 394.50 μs | 345.20 μs |
<!-- benchmarks:e2e:end -->

### Allocations

Allocations for the same fastest method per library (BenchmarkDotNet `Allocated`):

<!-- benchmarks:allocations:start -->
| Method | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| Data fetch | **792 B** | 1,904 B | 2,088 B | 10,224 B |
| Wide data fetch | **2.14 MB** | 2.59 MB | **2.14 MB** | 4.28 MB |
| Where | **92.43 KB** | 180.72 KB | 253.53 KB | 789.17 KB |
| Simulate work | **6.25 MB** | 8.90 MB | 121.57 MB | 13.68 MB |
| Any | **85.16 KB** | 139.07 KB | 233.60 KB | 807.05 KB |
| FirstOrDefault | **8.63 KB** | 16.48 KB | 187.58 KB | 80.40 KB |
| SingleOrDefault | **8.36 KB** | 16.40 KB | 187.82 KB | 80.40 KB |
<!-- benchmarks:allocations:end -->

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
[Category A](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) ·
[Category B](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFairCached-report-github.md).

### Category A — prepared vs compiled vs raw

As of **2026-10-17**.

| Feature | Nextorm (prepared) | Dapper | linq2db (compiled) | EF Core (compiled) |
|---|---|---|---|---|
| `CASE WHEN` | **9.26 µs / 744 B** | 14.60 µs / 1.34 KB | 15.08 µs / 1.59 KB | 35.61 µs / 7.90 KB |
| CTE | **9.49 µs / 793 B** | 21.43 µs / 1.57 KB | 21.52 µs / 1.77 KB | — |
| `SELECT DISTINCT` | **11.01 µs / 744 B** | 18.55 µs / 1.56 KB | 17.41 µs / 1.59 KB | 40.60 µs / 7.66 KB |
| `EXCEPT` | **11.07 µs / 760 B** | 17.72 µs / 1.44 KB | 20.80 µs / 1.68 KB | 43.47 µs / 8.81 KB |
| IN-list | **12.32 µs / 1.57 KB** | 18.65 µs / 1.27 KB | 43.53 µs / 15.23 KB | 60.70 µs / 14.77 KB |
| `INTERSECT` | **10.36 µs / 744 B** | 16.77 µs / 1.29 KB | 19.66 µs / 1.63 KB | 41.75 µs / 7.94 KB |
| 4-table join | **12.63 µs / 1.06 KB** | 25.52 µs / 2.02 KB | 23.27 µs / 2.07 KB | 46.82 µs / 8.49 KB |
| `LEFT JOIN` | **12.09 µs / 1.20 KB** | 19.84 µs / 2.22 KB | 20.29 µs / 2.32 KB | 41.64 µs / 9.92 KB |
| Recursive CTE | **12.64 µs / 752 B** | 24.24 µs / 1.53 KB | 25.73 µs / 1.78 KB | — |
| String functions | **8.96 µs / 736 B** | 13.92 µs / 1.28 KB | 15.18 µs / 1.64 KB | 35.08 µs / 7.49 KB |
| UDF | **9.27 µs / 768 B** | 14.08 µs / 1.28 KB | 15.05 µs / 1.64 KB | 34.82 µs / 7.49 KB |
| Window functions | **14.44 µs / 848 B** | 30.04 µs / 1.54 KB | 31.84 µs / 1.74 KB | — |

Nextorm prepared is the fastest on **every** feature and has the smallest allocation on 11 of 12 — IN-list
is the exception (1.57 KB vs Dapper's 1.27 KB). Time is roughly 3.4–4.9× below EF Core and 1.5–2.3× below
Dapper; allocations are 1.7–2.1× below Dapper and 8–12× below EF Core.

### Category B — warm cached vs regular

<!-- benchmarks:category-b:start -->
| Feature | Nextorm (cached) | Dapper | linq2db | EF Core |
|---|---|---|---|---|
| `CASE WHEN` | **13.04 μs / 3.22 KB** | 14.00 μs / 1.34 KB | 25.05 μs / 3.80 KB | 56.78 μs / 11.95 KB |
| CTE | 32.72 μs / 10.89 KB | **19.83 μs / 1.57 KB** | 56.89 μs / 9.06 KB | — |
| `SELECT DISTINCT` | **15.53 μs / 3.80 KB** | 18.41 μs / 1.56 KB | 30.44 μs / 3.91 KB | 64.19 μs / 12.20 KB |
| `EXCEPT` | 17.15 μs / 6.38 KB | **16.96 μs / 1.44 KB** | 38.10 μs / 5.05 KB | 72.82 μs / 15.31 KB |
| IN-list | 31.65 μs / 7.76 KB | **18.12 μs / 1.27 KB** | 69.44 μs / 18.91 KB | 112.63 μs / 22.61 KB |
| `INTERSECT` | 16.79 μs / 6.36 KB | **15.60 μs / 1.29 KB** | 36.77 μs / 5.01 KB | 71.94 μs / 14.38 KB |
| 4-table join | 32.96 μs / 10.67 KB | **25.03 μs / 2.02 KB** | 65.38 μs / 14.10 KB | 114.20 μs / 24.69 KB |
| `LEFT JOIN` | 23.03 μs / 6.16 KB | **19.83 μs / 2.22 KB** | 69.12 μs / 8.60 KB | 94.80 μs / 20.27 KB |
| Recursive CTE | 35.33 μs / 13.31 KB | **24.44 μs / 1.53 KB** | 60.41 μs / 8.39 KB | — |
| String functions | 14.97 μs / 4.20 KB | **13.93 μs / 1.28 KB** | 30.95 μs / 4.78 KB | 66.26 μs / 13.27 KB |
| UDF | 15.53 μs / 4.29 KB | **14.29 μs / 1.28 KB** | 48.46 μs / 6.50 KB | 66.25 μs / 13.27 KB |
| Window functions | 31.55 μs / 5.03 KB | **29.75 μs / 1.55 KB** | 62.11 μs / 5.99 KB | — |
<!-- benchmarks:category-b:end -->

In the warm path Nextorm beats Dapper on `CASE WHEN` and `SELECT DISTINCT` and is close on string
functions; it trails Dapper by about 1.01–1.16× on `EXCEPT`, window functions, string functions,
`INTERSECT`, UDF and `LEFT JOIN`, and by 1.32–1.75× on 4-table join, CTE, recursive CTE and IN-list — while staying well ahead
of regular linq2db and EF Core on time. Warm-path allocations are higher than the prepared path (the plan
key is rebuilt per call); they stay far below regular EF Core and below linq2db on most features, but
exceed linq2db on CTE, recursive CTE, `EXCEPT` and `INTERSECT`.

> **IN-list, Category B.** A captured collection disables the plan cache by design (each call re-renders the
> SQL), so the captured variants pay a full build; the inline variants hit the cache. The four Nextorm
> variants span 31.7–34.9 µs — this is the feature where Dapper's cached raw SQL is furthest ahead.

## Tier-1 query-shape scenarios

Added in #188 to cover query shapes the two tables above do not exercise: projection into a fresh DTO,
SQL-side aggregates, ordered paging, streaming (buffered/unbuffered DTO vs. zero-materialization raw
readers) and cross-library JSON/CSV output. Data size: `large_table` (10,000 rows) for
projection/aggregates/paging/streaming; the JSON class seeds its own `json_bench` table and the CSV
class slices `large_table` (10,000 rows). The fair-comparison fixture invariants stay `simple_entity`=10
and `complex_entity`=3 — the "10,000 rows" above is `large_table`, not those fixtures. As of **2026-10-10**.

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
  end-to-end cell is the fastest of them. See [Query reuse: cache vs Prepare](../engine/03-plan-cache-and-reuse.md).
- `ShortRun` + `InProcessEmitToolchain` is a quick mode: it is stable enough for a signal, but treat small
  differences as noise and read the full report (mean, error, standard deviation and allocations) before
  drawing conclusions.
- This is one machine, one provider (SQLite) and a fixed set of scenarios. It is not a claim about every
  workload — reproduce it with your own data and hardware.

## Reproducing

```bash
# End-to-end scenarios ("*SqliteBenchmarkIteration*" does not match SqliteBenchmarkLargeIteration:
# the type name differs after the "SqliteBenchmark" prefix)
for filter in "*SqliteBenchmarkIteration*" "*SqliteBenchmarkLargeIteration*" "*SqliteBenchmarkWhere*" \
              "*SqliteBenchmarkSimulateWork*" "*SqliteBenchmarkAny*" "*SqliteBenchmarkFirst*" \
              "*SqliteBenchmarkSingle*"; do
  dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter "$filter"
done

# Feature-level fair comparison (Category A / B)
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter "*SqliteBenchmarkFeaturesFair.*"
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter "*SqliteBenchmarkFeaturesFairCached*"
```

Each linked report is the BenchmarkDotNet output committed under `benchmarks/BenchmarkDotNet.Artifacts/results/`.
Set `NEXTORM_BENCH_FULL=1` to run the full out-of-process job instead of `ShortRun`.

The end-to-end, allocations and Category B tables on this page are **generated** from those reports — do not
edit them by hand. After a run:

```bash
python3 eng/perf/update_benchmark_tables.py          # rewrite readme.md and both article mirrors
python3 eng/perf/update_benchmark_tables.py --check  # CI mode: fail when a table is stale
```

The same refresh runs from `.github/workflows/benchmarks.yml` (manual dispatch or monthly) and opens a pull
request with the new reports and tables.

## See also

- [Capabilities](capabilities.md) — what each library supports.
- [Query reuse: cache vs Prepare](../engine/03-plan-cache-and-reuse.md).
