# Benchmarks: Nextorm vs Dapper, linq2db and EF Core

Use these results to compare the listed query shapes under the stated SQLite setup. They are
evidence for these scenarios, not a prediction for every provider, workload, or machine.

<!-- benchmarks:conditions:start -->
Benchmark conditions: provider SQLite (`Microsoft.Data.Sqlite`) for every participant; async queries; each
cell is the **fastest method** for that library in the linked BenchmarkDotNet report. Machine: AMD Ryzen 7
5800HS with Radeon Graphics (8 logical / 4 physical cores), .NET 11.0.0, BenchmarkDotNet 0.15.8 (`Job.Default`
+ `InProcessEmitToolchain`). As of **2026-11-16** for the end-to-end and allocations tables; Category B was
run on **2026-11-16**. Category A and Tier-1 keep the dates of their own runs. In each row the leading cell is
**bold** — the fastest in the time tables and the smallest allocation in the allocations table.
<!-- benchmarks:conditions:end -->

> **Feature-level view:** a per-feature, tiered comparison — no `Prepare()` arms, one table per tier,
> competitors as columns — is in [Feature benchmarks](feature-benchmarks.md).

## End-to-end scenarios

<!-- benchmarks:e2e:start -->
| Method | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| [Data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkIteration-report-github.md) | **12.26 μs** | 16.55 μs | 17.17 μs | 38.08 μs |
| [Wide data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkLargeIteration-report-github.md) | **7.921 ms** | 8.528 ms | 8.647 ms | 9.152 ms |
| [Where](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWhere-report-github.md) | **1.063 ms** | 1.503 ms | 1.553 ms | 3.386 ms |
| [Simulate work](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSimulateWork-report-github.md) | **7.893 ms** | 24.616 ms | 364.543 ms | 108.639 ms |
| [Any](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkAny-report-github.md) | **1.059 ms** | 1.509 ms | 1.663 ms | 3.660 ms |
| [FirstOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFirst-report-github.md) | **107.1 μs** | 154.6 μs | 366.3 μs | 355.7 μs |
| [SingleOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSingle-report-github.md) | **107.2 μs** | 148.6 μs | 370.8 μs | 337.2 μs |
<!-- benchmarks:e2e:end -->

### Allocations

Allocations for the same fastest method per library (BenchmarkDotNet `Allocated`):

<!-- benchmarks:allocations:start -->
| Method | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| Data fetch | **1,080 B** | 1,848 B | 2,080 B | 9,776 B |
| Wide data fetch | **2.14 MB** | 2.59 MB | **2.14 MB** | 4.28 MB |
| Where | **102.90 KB** | 179.23 KB | 252.74 KB | 745.41 KB |
| Simulate work | **6.21 MB** | 8.86 MB | 108.02 MB | 13.57 MB |
| Any | **84.38 KB** | 138.29 KB | 232.82 KB | 763.30 KB |
| FirstOrDefault | **8.56 KB** | 16.12 KB | 159.77 KB | 76.03 KB |
| SingleOrDefault | **8.28 KB** | 16.04 KB | 159.77 KB | 76.03 KB |
<!-- benchmarks:allocations:end -->

## Feature-level comparison (fair)

The end-to-end table reports the fastest method for each library, but those methods use different
query-reuse strategies. The feature comparison separates them into two categories:

- **Category A — prepared / compiled / raw:** Nextorm through `Prepare()` against the closest
  *compiled* form of the same query (`EF.CompileAsyncQuery`, `LinqToDB.CompiledQuery.Compile`) and
  raw Dapper, whose IL is cached by SQL text. This compares reusable query forms: compiled against
  compiled, with Dapper's raw-SQL path as its available arm.
- **Category B — warm cached vs regular:** Nextorm through the implicit plan cache. Each call
  creates a fresh fluent command with constants, so the cached plan is the only reusable part. This
  is compared with regular (non-compiled) EF Core and linq2db, and raw Dapper.

Each feature method runs **10 queries**. The tables show per-query mean time and allocated memory:
BenchmarkDotNet `Mean` and `Allocated` divided by 10. See the source reports:
[Category A](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-github.md) ·
[Category B](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFairCached-report-github.md).

### Category A — prepared vs compiled vs raw

<!-- benchmarks:category-a:start -->
As of **2026-11-16**.

| Feature | Nextorm (prepared) | Dapper | linq2db (compiled) | EF Core (compiled) |
|---|---|---|---|---|
| `CASE WHEN` | **10.79 μs / 664 B** | 15.22 μs / 1.34 KB | 16.10 μs / 1.59 KB | 35.25 μs / 7.46 KB |
| CTE | **11.35 μs / 752 B** | 23.01 μs / 1.56 KB | 23.50 μs / 1.77 KB | — |
| `SELECT DISTINCT` | **12.66 μs / 665 B** | 18.81 μs / 1.44 KB | 18.85 μs / 1.59 KB | 40.43 μs / 7.23 KB |
| `EXCEPT` | **12.56 μs / 680 B** | 18.52 μs / 1.43 KB | 21.50 μs / 1.67 KB | 42.52 μs / 8.38 KB |
| IN-list | **14.11 μs / 1.55 KB** | 19.66 μs / 1.26 KB | 45.78 μs / 13.63 KB | 62.91 μs / 14.34 KB |
| `INTERSECT` | **12.13 μs / 665 B** | 17.51 μs / 1.28 KB | 20.79 μs / 1.62 KB | 42.32 μs / 7.50 KB |
| 4-table join | **14.58 μs / 1.03 KB** | 27.04 μs / 2.02 KB | 24.96 μs / 2.06 KB | 48.61 μs / 8.05 KB |
| `LEFT JOIN` | **14.15 μs / 1.16 KB** | 21.17 μs / 2.21 KB | 21.46 μs / 2.31 KB | 41.92 μs / 9.48 KB |
| Recursive CTE | **14.06 μs / 672 B** | 25.65 μs / 1.52 KB | 26.92 μs / 1.77 KB | — |
| String functions | **10.81 μs / 656 B** | 15.30 μs / 1.27 KB | 15.85 μs / 1.63 KB | 34.86 μs / 7.05 KB |
| UDF | **10.84 μs / 688 B** | 15.78 μs / 1.27 KB | 15.53 μs / 1.63 KB | 34.45 μs / 7.05 KB |
| Window functions | **15.75 μs / 793 B** | 30.19 μs / 1.54 KB | 32.76 μs / 1.74 KB | — |
<!-- benchmarks:category-a:end -->

Nextorm prepared is the fastest on **every** feature and has the smallest allocation on 11 of 12 — IN-list is
the exception, where Dapper's cached raw SQL allocates less. The exact ratios are in the table above; they move
with the runner, so treat deltas below ~20 % as noise.

### Category B — warm cached vs regular

<!-- benchmarks:category-b:start -->
| Feature | Nextorm (cached) | Dapper | linq2db | EF Core |
|---|---|---|---|---|
| `CASE WHEN` | **13.63 μs / 2.06 KB** | 15.59 μs / 1.34 KB | 25.31 μs / 3.77 KB | 57.41 μs / 11.46 KB |
| CTE | 32.56 μs / 9.49 KB | **22.91 μs / 1.56 KB** | 56.53 μs / 9.37 KB | — |
| `SELECT DISTINCT` | **17.52 μs / 3.06 KB** | 18.67 μs / 1.44 KB | 30.84 μs / 3.88 KB | 63.18 μs / 11.70 KB |
| `EXCEPT` | **18.34 μs / 4.23 KB** | 18.92 μs / 1.43 KB | 38.30 μs / 5.01 KB | 71.39 μs / 14.73 KB |
| IN-list | 43.73 μs / 8.77 KB | **19.82 μs / 1.26 KB** | 68.29 μs / 17.28 KB | 102.95 μs / 22.05 KB |
| `INTERSECT` | **17.51 μs / 4.22 KB** | 18.01 μs / 1.28 KB | 39.05 μs / 4.96 KB | 70.88 μs / 13.81 KB |
| 4-table join | 35.68 μs / 10.02 KB | **27.48 μs / 2.02 KB** | 65.00 μs / 14.14 KB | 114.91 μs / 23.90 KB |
| `LEFT JOIN` | 26.32 μs / 5.74 KB | **21.79 μs / 2.21 KB** | 69.49 μs / 8.94 KB | 98.61 μs / 19.77 KB |
| Recursive CTE | 36.53 μs / 10.10 KB | **26.70 μs / 1.52 KB** | 63.08 μs / 8.43 KB | — |
| String functions | **15.34 μs / 2.81 KB** | 16.34 μs / 1.27 KB | 31.89 μs / 4.74 KB | 66.92 μs / 12.74 KB |
| UDF | **15.71 μs / 2.90 KB** | 16.26 μs / 1.27 KB | 50.07 μs / 6.75 KB | 66.85 μs / 12.74 KB |
| Window functions | **31.01 μs / 4.09 KB** | 31.99 μs / 1.53 KB | 61.46 μs / 5.79 KB | — |
<!-- benchmarks:category-b:end -->

In the warm path Nextorm beats Dapper on `CASE WHEN` and `SELECT DISTINCT`, and is level on
`INTERSECT`. It trails Dapper by about 1.06–1.20× on string functions, `EXCEPT`, UDF, and window
functions; by 1.38× on `LEFT JOIN`; and by 1.56–1.82× on a 4-table join, CTE, recursive CTE, and
IN-list. It remains ahead of regular linq2db and EF Core on time.

Warm-path allocations exceed the prepared path because the plan key is rebuilt on each call. They
remain far below regular EF Core and below linq2db on most features, but exceed linq2db on CTE,
recursive CTE, `EXCEPT`, and `INTERSECT`.

> **IN-list, Category B.** A captured collection disables the plan cache by design (each call re-renders the
> SQL), so the captured variants pay a full build; the inline variants hit the cache. The four Nextorm
> variants span 37.5–41.4 µs — this is the feature where Dapper's cached raw SQL is furthest ahead.

## Tier-1 query-shape scenarios

Issue #188 added these query shapes, which the first two tables do not cover: projection into a
fresh DTO (data transfer object), SQL-side aggregates, ordered paging, streaming DTO results in
buffered or unbuffered form, zero-materialization raw readers, and JSON/CSV output across libraries.

The data comes from `large_table` (10,000 rows) for projection, aggregates, paging, and streaming.
The JSON class seeds its own `json_bench` table; the CSV class reads a slice of `large_table`
(10,000 rows). The fair-comparison fixture invariants remain `simple_entity`=10 and
`complex_entity`=3; the 10,000 rows refer to `large_table`, not those fixtures.

<!-- benchmarks:tier1-run-info:start -->
Snapshot: **2026-11-16** (measurements **18:10:43–18:51:54 UTC**, from the run log). The six reports below
contain 98 valid rows. The full run included 118 Tier-1 cases, including 20 additional CSV diagnostic cases
not compared here. Machine: AMD Ryzen 7 5800HS with Radeon Graphics (8 logical / 4 physical cores), .NET SDK
11.0.100, runtime .NET 11.0.0, BenchmarkDotNet 0.15.8. 89 report rows used `Job.Default` parameters
(UnrollFactor=16); 9 raw-reader rows used the specialized InvocationCount=1 and UnrollFactor=1. Every row used
`InProcessEmitToolchain`. Source: HEAD `ad2d0daeb6e34bcc5b094f0c9b0b1d1205fb8545`; built from 10 local NextORM
project references and the local source-generator analyzer; all 11 build/runtime hash rows matched those
outputs. The source tree was dirty at measurement time.
<!-- benchmarks:tier1-run-info:end -->

The reuse categories match the feature comparison: **Category A** compares Nextorm `Prepare()` with
the closest *compiled* form (`EF.CompileAsyncQuery`, `LinqToDB.CompiledQuery.Compile`) and raw
Dapper. **Category B** compares Nextorm's implicit plan cache with regular (non-compiled) EF Core
and linq2db, and raw Dapper. Dapper has no separate compiled-query or compiled-reader variant in
this comparison, so its raw-SQL arms appear in both categories. The reader scenario still uses
Dapper's `ExecuteReader` API.

**Streaming has three groups:** buffered DTO materialization, unbuffered DTO enumeration, and a
**zero-materialization** raw-reader group. The zero-materialization group is synchronous only; async
reader arms are deferred. Nextorm `ToDataReader`, Dapper `ExecuteReader`, and linq2db
`ExecuteReader` (its `DataReaderAsync.Reader`) read the same `id, someString` projection through
**one shared non-retaining sink**. The sink does not retain the rows. EF Core is **not** included in
this group because it has no LINQ raw-reader counterpart. Here, zero-materialization means no entity
or DTO construction, **not** zero allocations: each reader call still allocates its command and
reader.

For JSON/CSV, the competitor arms are the **closest equivalent**: Dapper, linq2db, and EF Core have
no non-materializing output terminal, so their arms materialize the rows and then serialize them
manually. These rows do not claim zero materialization.

<!-- benchmarks:tier1-caveats:start -->
> **Reading Tier-1 results.** `Job.Default` with `InProcessEmitToolchain` runs in process; treat differences
> below ~20% as noise and check error, standard deviation, and allocations in the full report. The
> raw-reader runs produced 9 warnings for iteration times below the recommended 100 ms; streaming produced 6
> multimodal-distribution warnings. Failed cases: 0.
<!-- benchmarks:tier1-caveats:end -->

### Projection — `Select(x => new Dto { Id, Str })`

Reproduce: `NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkProjection*`.
[Report](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkProjection-report-github.md).

<!-- benchmarks:tier1-projection:start -->
| Arm | Category | Mean | Allocated |
| --- | --- | ---: | ---: |
| A_Nextorm_Prepared_ToListAsync | A | 3.923 ms | 1.3 MB |
| A_EFCore_Compiled_ToListAsync | A | 4.849 ms | 3.36 MB |
| A_Linq2Db_Compiled_ToList | A | 5.431 ms | 1.47 MB |
| A_Dapper_ToListAsync | A+B | 6.637 ms | 1.7 MB |
| B_Nextorm_Cached_ToListAsync | B | 3.970 ms | 1.3 MB |
| B_Linq2Db_ToListAsync | B | 5.779 ms | 1.48 MB |
| B_EFCore_ToListAsync | B | 8.510 ms | 3.62 MB |
<!-- benchmarks:tier1-projection:end -->

### Aggregates — SQL-side `Count` / `Sum` / `GroupBy → Count`

Reproduce: `NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkAggregates*`.
[Report](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkAggregates-report-github.md).

<!-- benchmarks:tier1-aggregates:start -->
| Operation | Arm | Category | Mean | Allocated |
| --- | --- | --- | ---: | ---: |
| Count | A_Nextorm_Prepared_Count | A | 15.11 μs | 400 B |
| Count | A_Linq2Db_Compiled_Count | A | 19.05 μs | 1,272 B |
| Count | A_EFCore_Compiled_Count | A | 36.69 μs | 7,296 B |
| Sum | A_Nextorm_Prepared_Sum | A | 274.59 μs | 403 B |
| Sum | A_Linq2Db_Compiled_Sum | A | 297.32 μs | 1,283 B |
| Sum | A_EFCore_Compiled_Sum | A | 315.38 μs | 7,323 B |
| GroupBy→Count | A_Nextorm_Prepared_GroupByCount | A | 1,297.07 μs | 4,698 B |
| GroupBy→Count | A_Linq2Db_Compiled_GroupByCount | A | 1,331.58 μs | 7,018 B |
| GroupBy→Count | A_EFCore_Compiled_GroupByCount | A | 1,419.58 μs | 32,642 B |
| Count | Dapper_Count | A+B | 19.55 μs | 936 B |
| Sum | Dapper_Sum | A+B | 290.92 μs | 947 B |
| GroupBy→Count | Dapper_GroupByCount | A+B | 1,336.72 μs | 12,322 B |
| Count | B_Nextorm_Cached_Count | B | 15.99 μs | 400 B |
| Count | B_Linq2Db_Count | B | 24.88 μs | 2,848 B |
| Count | B_EFCore_Count | B | 49.82 μs | 9,616 B |
| Sum | B_Nextorm_Cached_Sum | B | 282.05 μs | 963 B |
| Sum | B_Linq2Db_Sum | B | 305.87 μs | 3,475 B |
| Sum | B_EFCore_Sum | B | 334.58 μs | 10,515 B |
| GroupBy→Count | B_Nextorm_Cached_GroupByCount | B | 1,342.22 μs | 8,650 B |
| GroupBy→Count | B_Linq2Db_GroupByCount | B | 1,377.85 μs | 11,514 B |
| GroupBy→Count | B_EFCore_GroupByCount | B | 1,426.02 μs | 42,154 B |
<!-- benchmarks:tier1-aggregates:end -->

### Paging — `OrderBy(Id).Offset(5000).Limit(100)`

Reproduce: `NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkPaging*`.
[Report](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkPaging-report-github.md).

<!-- benchmarks:tier1-paging:start -->
| Arm | Category | Mean | Allocated |
| --- | --- | ---: | ---: |
| A_Nextorm_Prepared_PageAsync | A | 89.99 μs | 13.95 KB |
| A_Linq2Db_Compiled_Page | A | 107.29 μs | 17.6 KB |
| A_EFCore_Compiled_PageAsync | A | 128.99 μs | 44.37 KB |
| A_Dapper_PageAsync | A+B | 101.65 μs | 18.14 KB |
| B_Nextorm_Cached_PageAsync | B | 100.22 μs | 19.4 KB |
| B_Linq2Db_PageAsync | B | 135.85 μs | 22.71 KB |
| B_EFCore_PageAsync | B | 183.08 μs | 56.28 KB |
<!-- benchmarks:tier1-paging:end -->

### Streaming — buffered / unbuffered DTO and zero-materialization readers

Reproduce: `NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkStreaming*`.
[Report](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkStreaming-report-github.md).

Buffered DTO:

<!-- benchmarks:tier1-streaming-buffered:start -->
| Arm | Category | Mean | Allocated |
| --- | --- | ---: | ---: |
| A_Nextorm_Prepared_ToList_Dto | A | 4,119.13 μs | 1,360,688 B |
| Linq2Db_Compiled_ToList_Dto | A | 5,720.83 μs | 1,544,457 B |
| A_EFCore_Compiled_ToList_Dto | A | 8,678.39 μs | 3,789,917 B |
| Dapper_ToList_Dto | A+B | 6,793.55 μs | 1,784,191 B |
| B_Nextorm_Cached_ToList_Dto | B | 4,165.29 μs | 1,362,858 B |
| B_Linq2Db_ToList_Dto | B | 5,937.36 μs | 1,547,617 B |
| B_EFCore_ToList_Dto | B | 8,780.73 μs | 3,794,231 B |
<!-- benchmarks:tier1-streaming-buffered:end -->

Unbuffered DTO enumeration:

<!-- benchmarks:tier1-streaming-unbuffered:start -->
| Arm | Category | Mean | Allocated |
| --- | --- | ---: | ---: |
| A_Nextorm_Prepared_AsyncStream_Dto | A | 4,115.10 μs | 1,280,890 B |
| A_EFCore_Compiled_AsyncStream_Dto | A | 5,106.04 μs | 3,527,002 B |
| Dapper_AsyncStream_Dto | A+B | 4,655.43 μs | 1,521,474 B |
| B_Nextorm_Cached_AsyncStream_Dto | B | 4,542.01 μs | 1,284,226 B |
| B_Linq2Db_AsyncStream_Dto | B | 4,660.90 μs | 1,284,642 B |
| B_EFCore_AsyncStream_Dto | B | 5,159.78 μs | 3,531,154 B |
<!-- benchmarks:tier1-streaming-unbuffered:end -->

Zero-materialization raw readers (synchronous; shared non-retaining sink; EF Core excluded):

<!-- benchmarks:tier1-streaming-readers:start -->
| Arm | Category | Mean | Allocated |
| --- | --- | ---: | ---: |
| A_Nextorm_Prepared_ToDataReader | A | 4,340.06 μs | 949.37 KB |
| Linq2Db_ToDataReader | B | 3,682.80 μs | 943.65 KB |
| Provider_Direct_ToDataReader | B | 3,708.38 μs | 943.39 KB |
| Dapper_ToDataReader | B | 3,770.33 μs | 943.42 KB |
| B_Nextorm_ToDataReader | B | 4,190.48 μs | 951.91 KB |
<!-- benchmarks:tier1-streaming-readers:end -->

An empty `DbDataReader` measures setup and close overhead without reading rows:

<!-- benchmarks:tier1-streaming-empty-readers:start -->
| Arm | Category | Mean | Allocated |
| --- | --- | ---: | ---: |
| Provider_Direct_ToDataReader_Empty | B | 82.39 μs | 0.00 KB |
| Dapper_ToDataReader_Empty | B | 92.60 μs | 0.00 KB |
| Linq2Db_ToDataReader_Empty | B | 92.66 μs | 0.00 KB |
| B_Nextorm_ToDataReader_Empty | B | 225.84 μs | 16.80 KB |
<!-- benchmarks:tier1-streaming-empty-readers:end -->

### JSON / CSV cross-library

Reproduce JSON: `NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkWriteJson*`.
[Report](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWriteJson-report-github.md).
Reproduce CSV: `NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkCsv*`.
[Report](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkCsv-report-github.md).

The JSON report covers `RowCount` 1,000 / 10,000 / 100,000; the 10,000-row block is shown (Ratio is
relative to `ToList_Dto` in the same block). Competitor arms are materialize → manual serializer.

<!-- benchmarks:tier1-write-json:start -->
| Method (10,000 rows) | Mean | Ratio | Allocated |
| --- | ---: | ---: | ---: |
| WriteJson_Array_Scalar | 4,780.7 μs | 0.74 | 9.76 KB |
| WriteJsonAsync_Array_Scalar | 5,113.1 μs | 0.79 | 9.92 KB |
| ToList_Dto | 6,446.5 μs | 1.00 | 783.76 KB |
| Nextorm_ToList_Dto_Json | 7,419.7 μs | 1.15 | 784.07 KB |
| WriteJson_Array_Dto | 7,880.6 μs | 1.22 | 403.18 KB |
| Linq2Db_ToList_Json | 8,379.2 μs | 1.30 | 963.72 KB |
| WriteJson_Array_ScalarPayload | 8,707.5 μs | 1.35 | 11493.13 KB |
| WriteJsonAsync_Array_ScalarPayload | 9,174.3 μs | 1.42 | 11493.28 KB |
| Dapper_ToList_Json | 9,493.9 μs | 1.47 | 1273.76 KB |
| EFCore_ToList_Json | 10,812.4 μs | 1.68 | 3158.23 KB |
| WriteJson_Array_WideDto | 10,993.8 μs | 1.71 | 11495.97 KB |
<!-- benchmarks:tier1-write-json:end -->

CSV (`large_table` slice, 10,000 rows; Ratio relative to `Nextorm_ToList`):

<!-- benchmarks:tier1-csv:start -->
| Method | Mean | Ratio | Allocated |
| --- | ---: | ---: | ---: |
| Nextorm_StringProjection_ToList | 2.763 ms | 0.33 | 1018.12 KB |
| Nextorm_StringProjection_WriteCsv | 3.469 ms | 0.42 | 949.1 KB |
| Nextorm_ToList | 8.336 ms | 1.00 | 2266.74 KB |
| Nextorm_ToList_ManualCsv | 9.694 ms | 1.16 | 4375.43 KB |
| Nextorm_WriteCsv | 10.095 ms | 1.21 | 1733.92 KB |
| Linq2Db_ToList_ManualCsv | 12.945 ms | 1.55 | 4087.58 KB |
| Dapper_ToList_ManualCsv | 13.250 ms | 1.59 | 5100.61 KB |
| EFCore_ToList_ManualCsv | 14.824 ms | 1.78 | 6751.01 KB |
<!-- benchmarks:tier1-csv:end -->

### NativeAOT startup (reference, not a query benchmark)

AOT moves the cost from every start to publish time. On the `oc-report` case (13 GB SQLite, 3-day window)
the same report runs in 0.42 s under JIT and 0.12 s as a NativeAOT binary; EF Core 10 goes 1.03 s →
0.23 s and Dapper 0.26 s → 0.12 s. The SQL and row counts are identical across the three, and the
measurement is dominated by one `session_message` scan — read it as startup cost, not query throughput.
Only nextorm publishes with `PublishAot=true` alone; the nextorm arm ships as
[`examples/nextorm.examples.oc-report`](https://github.com/AlexeyShirshov/nextorm/tree/main/examples/nextorm.examples.oc-report),
see the [NativeAOT guide](../infrastructure/05-nativeaot.md).

## Reading the numbers

- Every participant uses the same ADO.NET provider (`Microsoft.Data.Sqlite`), which controls for
  provider differences. The SQLite database runs on tmpfs, so storage I/O does not mask the ORM.
- Nextorm reports both reuse paths: the implicit plan cache and explicit `Prepare()`. For Nextorm,
  the end-to-end table shows the faster of its two reuse paths. See [Query reuse: cache vs
  Prepare](../engine/03-plan-cache-and-reuse.md).
- The published results use `Job.Default` parameters with `InProcessEmitToolchain`; the in-process
  run remains exploratory. Treat small differences as noise and
  read the full report—including mean, error, standard deviation, and allocations—before drawing
  conclusions.
- These results cover one machine, SQLite, and a fixed set of scenarios. They do not establish
  performance for every workload; reproduce them with your own data and hardware.

## Reproducing

```bash
export NEXTORM_BENCH_FULL=1

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
`NEXTORM_BENCH_FULL=1` selects the `Job.Default` parameters with `InProcessEmitToolchain`; it does not
launch the benchmarks out of process.

The end-to-end, allocations, Category A/B and Tier-1 tables on this page are **generated** from those
reports — do not edit them by hand. After a run:

```bash
python3 tools/benchmarks/update_benchmark_tables.py          # rewrite readme.md and both article mirrors
python3 tools/benchmarks/update_benchmark_tables.py --check  # CI mode: fail when a table is stale
```

Tier-1 uses the Projection, Aggregates, Paging, Streaming, WriteJson, Csv reports and their
`tier1-run.json` sidecar under `benchmarks/BenchmarkDotNet.Artifacts/results/`. The current workflow
refreshes the end-to-end tables and Category A/B; refresh Tier-1 after a separate run of those classes
and update the sidecar's run date and report hashes. Each report hash is computed after replacing CRLF
line endings with LF, so checkout line-ending settings do not affect validation; all other report bytes
remain covered by the hash.

The generator reads each comparison suite's UTC start date and report hashes from
`suite-run-dates.json`; report modification times are not measurement evidence. After a local run,
record the UTC date captured immediately before each suite with `--record-run-date
CLASS=YYYY-MM-DD --run-evidence SOURCE`, where `SOURCE` is that run's log path or URL. Repeat
`--record-run-date` for every suite you refreshed. The workflow records each suite date and binds it
to the generated CSV and Markdown reports before regenerating the tables.

The same end-to-end refresh runs from `.github/workflows/benchmarks.yml` — automatically after every
merge into `main` that touches the engine (`src/**`) or the benchmark project, and on demand from the
Actions tab. It opens a pull request with the new reports and tables.

## See also

- [Capabilities](capabilities.md) — what each library supports.
- [Query reuse: cache vs Prepare](../engine/03-plan-cache-and-reuse.md).
