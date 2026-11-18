# Benchmarks: Nextorm vs Dapper, linq2db and EF Core

Use these results to compare the listed query shapes under the stated SQLite setup. They are
evidence for these scenarios, not a prediction for every provider, workload, or machine.

<!-- benchmarks:conditions:start -->
Benchmark conditions: provider SQLite (`Microsoft.Data.Sqlite`) for every participant; async queries; each
cell is the **fastest method** for that library in the linked BenchmarkDotNet report. Machine: AMD EPYC 9V45
(4 logical / 2 physical cores), .NET 11.0.0, BenchmarkDotNet 0.15.8 (`ShortRun` + `InProcessEmitToolchain`).
As of **2026-11-18** for the end-to-end and allocations tables; Category B was run on **2026-11-18**. Category
A and Tier-1 keep the dates of their own runs. In each row the leading cell is **bold** — the fastest in the
time tables and the smallest allocation in the allocations table.
<!-- benchmarks:conditions:end -->

> **Feature-level view:** a per-feature, tiered comparison — no `Prepare()` arms, one table per tier,
> competitors as columns — is in [Feature benchmarks](feature-benchmarks.md).

## End-to-end scenarios

<!-- benchmarks:e2e:start -->
| Method | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| [Data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkIteration-report-github.md) | **8.248 μs** | 10.785 μs | 11.084 μs | 21.973 μs |
| [Wide data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkLargeIteration-report-github.md) | **6.632 ms** | 7.467 ms | 7.585 ms | 7.659 ms |
| [Where](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWhere-report-github.md) | **705.0 μs** | 896.1 μs | 926.8 μs | 1,924.2 μs |
| [Simulate work](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSimulateWork-report-github.md) | **5.654 ms** | 14.806 ms | 107.557 ms | 59.253 ms |
| [Any](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkAny-report-github.md) | **728.6 μs** | 921.6 μs | 968.5 μs | 1,950.4 μs |
| [FirstOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFirst-report-github.md) | **74.11 μs** | 94.52 μs | 150.02 μs | 200.65 μs |
| [SingleOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSingle-report-github.md) | **72.72 μs** | 90.03 μs | 150.76 μs | 186.52 μs |
<!-- benchmarks:e2e:end -->

### Allocations

Allocations for the same fastest method per library (BenchmarkDotNet `Allocated`):

<!-- benchmarks:allocations:start -->
| Method | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| Data fetch | **968 B** | 1,896 B | 2,080 B | 9,776 B |
| Wide data fetch | **2.14 MB** | 2.59 MB | **2.14 MB** | 4.28 MB |
| Where | **102.58 KB** | 179.22 KB | 252.74 KB | 745.39 KB |
| Simulate work | **6.21 MB** | 8.86 MB | 107.44 MB | 13.56 MB |
| Any | **84.38 KB** | 138.29 KB | 232.81 KB | 763.28 KB |
| FirstOrDefault | **8.55 KB** | 16.12 KB | 159.77 KB | 76.02 KB |
| SingleOrDefault | **8.28 KB** | 16.04 KB | 159.77 KB | 76.02 KB |
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
As of **2026-11-18**.

| Feature | Nextorm (prepared) | Dapper | linq2db (compiled) | EF Core (compiled) |
|---|---|---|---|---|
| `CASE WHEN` | **7.48 μs / 664 B** | 9.49 μs / 1.34 KB | 9.76 μs / 1.59 KB | 19.71 μs / 7.46 KB |
| CTE | **7.79 μs / 752 B** | 12.33 μs / 1.56 KB | 12.39 μs / 1.77 KB | — |
| `SELECT DISTINCT` | **8.32 μs / 664 B** | 10.24 μs / 1.44 KB | 10.58 μs / 1.59 KB | 20.71 μs / 7.23 KB |
| `EXCEPT` | **8.80 μs / 680 B** | 11.71 μs / 1.43 KB | 12.65 μs / 1.67 KB | 23.45 μs / 8.38 KB |
| IN-list | **9.09 μs / 1.55 KB** | 10.77 μs / 1.26 KB | 16.12 μs / 13.63 KB | 24.12 μs / 14.34 KB |
| `INTERSECT` | **7.94 μs / 665 B** | 10.45 μs / 1.28 KB | 11.71 μs / 1.62 KB | 21.08 μs / 7.50 KB |
| 4-table join | **10.01 μs / 1.03 KB** | 16.03 μs / 2.02 KB | 14.64 μs / 2.06 KB | 25.06 μs / 8.05 KB |
| `LEFT JOIN` | **9.98 μs / 1.16 KB** | 13.03 μs / 2.21 KB | 13.02 μs / 2.31 KB | 22.16 μs / 9.48 KB |
| Recursive CTE | **9.08 μs / 672 B** | 13.44 μs / 1.52 KB | 13.98 μs / 1.77 KB | — |
| String functions | **7.13 μs / 656 B** | 9.23 μs / 1.27 KB | 9.27 μs / 1.63 KB | 18.23 μs / 7.05 KB |
| UDF | **7.22 μs / 688 B** | 9.34 μs / 1.27 KB | 9.55 μs / 1.63 KB | 18.57 μs / 7.05 KB |
| Window functions | **10.23 μs / 793 B** | 14.65 μs / 1.54 KB | 14.60 μs / 1.73 KB | — |
<!-- benchmarks:category-a:end -->

Nextorm prepared is the fastest on **every** feature and has the smallest allocation on 11 of 12 — IN-list is
the exception, where Dapper's cached raw SQL allocates less. The exact ratios are in the table above; they move
with the runner, so treat deltas below ~20 % as noise.

### Category B — warm cached vs regular

<!-- benchmarks:category-b:start -->
| Feature | Nextorm (cached) | Dapper | linq2db | EF Core |
|---|---|---|---|---|
| `CASE WHEN` | **8.82 μs / 2.06 KB** | 9.65 μs / 1.34 KB | 11.59 μs / 3.77 KB | 21.98 μs / 11.46 KB |
| CTE | 14.28 μs / 9.26 KB | **12.34 μs / 1.56 KB** | 19.76 μs / 9.31 KB | — |
| `SELECT DISTINCT` | **9.69 μs / 3.03 KB** | 10.30 μs / 1.44 KB | 12.23 μs / 3.88 KB | 22.80 μs / 11.70 KB |
| `EXCEPT` | **10.72 μs / 4.30 KB** | 11.22 μs / 1.43 KB | 15.59 μs / 5.01 KB | 26.55 μs / 14.92 KB |
| IN-list | 13.43 μs / 7.04 KB | **10.39 μs / 1.26 KB** | 18.55 μs / 17.28 KB | 29.03 μs / 22.20 KB |
| `INTERSECT` | **10.07 μs / 4.28 KB** | 10.36 μs / 1.28 KB | 14.61 μs / 4.96 KB | 25.82 μs / 13.99 KB |
| 4-table join | 16.44 μs / 10.05 KB | **15.73 μs / 2.02 KB** | 24.59 μs / 14.06 KB | 40.00 μs / 23.94 KB |
| `LEFT JOIN` | 13.27 μs / 5.64 KB | **12.63 μs / 2.21 KB** | 22.28 μs / 8.94 KB | 31.47 μs / 19.88 KB |
| Recursive CTE | 14.49 μs / 10.09 KB | **13.30 μs / 1.52 KB** | 19.94 μs / 8.45 KB | — |
| String functions | **8.58 μs / 2.81 KB** | 8.72 μs / 1.27 KB | 12.23 μs / 4.89 KB | 21.87 μs / 12.74 KB |
| UDF | **9.03 μs / 2.90 KB** | 9.09 μs / 1.27 KB | 15.92 μs / 6.63 KB | 23.31 μs / 12.74 KB |
| Window functions | **13.91 μs / 4.19 KB** | 14.43 μs / 1.54 KB | 18.98 μs / 5.79 KB | — |
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
Snapshot: **2026-11-17–2026-11-18** (measurements **2026-11-17 18:38:46–2026-11-18 02:56:28 UTC**, from the
run log). The six reports below contain 98 valid rows. The full run included 118 Tier-1 cases, including 20
additional CSV diagnostic cases not compared here. Machine: AMD Ryzen 7 5800HS with Radeon Graphics (8 logical
/ 4 physical cores), .NET SDK 11.0.100, runtime .NET 11.0.0, BenchmarkDotNet 0.15.8. 89 report rows used
`Job.Default` parameters (UnrollFactor=16); 9 raw-reader rows used the specialized InvocationCount=1 and
UnrollFactor=1. Every row used `InProcessEmitToolchain`. Source: HEAD
`9226f0415d96eb1c4364c81ac3c4acaa8f9db6a8`; built from 10 local NextORM project references and the local
source-generator analyzer; all 11 build/runtime hash rows matched those outputs. The source tree was clean at
measurement time.
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
> raw-reader runs produced 9 warnings for iteration times below the recommended 100 ms; streaming produced 5
> multimodal-distribution warnings. Failed cases: 0.
<!-- benchmarks:tier1-caveats:end -->

### Projection — `Select(x => new Dto { Id, Str })`

Reproduce: `NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkProjection*`.
[Report](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkProjection-report-github.md).

<!-- benchmarks:tier1-projection:start -->
| Arm | Category | Mean | Allocated |
| --- | --- | ---: | ---: |
| A_Nextorm_Prepared_ToListAsync | A | 4.433 ms | 1.3 MB |
| A_EFCore_Compiled_ToListAsync | A | 4.979 ms | 3.36 MB |
| A_Linq2Db_Compiled_ToList | A | 6.528 ms | 1.47 MB |
| A_Dapper_ToListAsync | A+B | 7.363 ms | 1.7 MB |
| B_Nextorm_Cached_ToListAsync | B | 4.401 ms | 1.3 MB |
| B_Linq2Db_ToListAsync | B | 6.069 ms | 1.48 MB |
| B_EFCore_ToListAsync | B | 9.262 ms | 3.62 MB |
<!-- benchmarks:tier1-projection:end -->

### Aggregates — SQL-side `Count` / `Sum` / `GroupBy → Count`

Reproduce: `NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkAggregates*`.
[Report](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkAggregates-report-github.md).

<!-- benchmarks:tier1-aggregates:start -->
| Operation | Arm | Category | Mean | Allocated |
| --- | --- | --- | ---: | ---: |
| Count | A_Nextorm_Prepared_Count | A | 15.27 μs | 400 B |
| Count | A_Linq2Db_Compiled_Count | A | 19.70 μs | 1,272 B |
| Count | A_EFCore_Compiled_Count | A | 39.61 μs | 7,296 B |
| Sum | A_Nextorm_Prepared_Sum | A | 282.65 μs | 403 B |
| Sum | A_Linq2Db_Compiled_Sum | A | 294.11 μs | 1,283 B |
| Sum | A_EFCore_Compiled_Sum | A | 314.81 μs | 7,323 B |
| GroupBy→Count | A_Nextorm_Prepared_GroupByCount | A | 1,338.19 μs | 4,698 B |
| GroupBy→Count | A_Linq2Db_Compiled_GroupByCount | A | 1,369.71 μs | 7,018 B |
| GroupBy→Count | A_EFCore_Compiled_GroupByCount | A | 1,401.89 μs | 32,642 B |
| Count | Dapper_Count | A+B | 18.55 μs | 936 B |
| Sum | Dapper_Sum | A+B | 295.30 μs | 947 B |
| GroupBy→Count | Dapper_GroupByCount | A+B | 1,486.63 μs | 12,322 B |
| Count | B_Nextorm_Cached_Count | B | 15.41 μs | 400 B |
| Count | B_Linq2Db_Count | B | 26.00 μs | 2,848 B |
| Count | B_EFCore_Count | B | 47.36 μs | 9,616 B |
| Sum | B_Nextorm_Cached_Sum | B | 279.90 μs | 963 B |
| Sum | B_Linq2Db_Sum | B | 306.10 μs | 3,475 B |
| Sum | B_EFCore_Sum | B | 325.55 μs | 10,515 B |
| GroupBy→Count | B_Nextorm_Cached_GroupByCount | B | 1,340.93 μs | 8,730 B |
| GroupBy→Count | B_Linq2Db_GroupByCount | B | 1,391.61 μs | 11,738 B |
| GroupBy→Count | B_EFCore_GroupByCount | B | 1,454.86 μs | 42,155 B |
<!-- benchmarks:tier1-aggregates:end -->

### Paging — `OrderBy(Id).Offset(5000).Limit(100)`

Reproduce: `NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkPaging*`.
[Report](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkPaging-report-github.md).

<!-- benchmarks:tier1-paging:start -->
| Arm | Category | Mean | Allocated |
| --- | --- | ---: | ---: |
| A_Nextorm_Prepared_PageAsync | A | 90.91 μs | 13.95 KB |
| A_Linq2Db_Compiled_Page | A | 124.58 μs | 17.6 KB |
| A_EFCore_Compiled_PageAsync | A | 131.03 μs | 44.37 KB |
| A_Dapper_PageAsync | A+B | 112.87 μs | 18.14 KB |
| B_Nextorm_Cached_PageAsync | B | 118.32 μs | 19.43 KB |
| B_Linq2Db_PageAsync | B | 149.24 μs | 22.6 KB |
| B_EFCore_PageAsync | B | 215.39 μs | 56.39 KB |
<!-- benchmarks:tier1-paging:end -->

### Streaming — buffered / unbuffered DTO and zero-materialization readers

Reproduce: `NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkStreaming*`.
[Report](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkStreaming-report-github.md).

Buffered DTO:

<!-- benchmarks:tier1-streaming-buffered:start -->
| Arm | Category | Mean | Allocated |
| --- | --- | ---: | ---: |
| A_Nextorm_Prepared_ToList_Dto | A | 5,217.99 μs | 1,360,688 B |
| Linq2Db_Compiled_ToList_Dto | A | 6,867.59 μs | 1,544,580 B |
| A_EFCore_Compiled_ToList_Dto | A | 10,303.59 μs | 3,790,111 B |
| Dapper_ToList_Dto | A+B | 7,831.00 μs | 1,784,116 B |
| B_Nextorm_Cached_ToList_Dto | B | 5,110.60 μs | 1,362,858 B |
| B_Linq2Db_ToList_Dto | B | 7,003.57 μs | 1,547,645 B |
| B_EFCore_ToList_Dto | B | 10,053.18 μs | 3,794,208 B |
<!-- benchmarks:tier1-streaming-buffered:end -->

Unbuffered DTO enumeration:

<!-- benchmarks:tier1-streaming-unbuffered:start -->
| Arm | Category | Mean | Allocated |
| --- | --- | ---: | ---: |
| A_Nextorm_Prepared_AsyncStream_Dto | A | 5,102.05 μs | 1,280,890 B |
| A_EFCore_Compiled_AsyncStream_Dto | A | 6,390.56 μs | 3,527,002 B |
| Dapper_AsyncStream_Dto | A+B | 5,615.85 μs | 1,521,474 B |
| B_Nextorm_Cached_AsyncStream_Dto | B | 5,489.86 μs | 1,284,226 B |
| B_Linq2Db_AsyncStream_Dto | B | 5,632.32 μs | 1,284,642 B |
| B_EFCore_AsyncStream_Dto | B | 6,309.38 μs | 3,531,154 B |
<!-- benchmarks:tier1-streaming-unbuffered:end -->

Zero-materialization raw readers (synchronous; shared non-retaining sink; EF Core excluded):

<!-- benchmarks:tier1-streaming-readers:start -->
| Arm | Category | Mean | Allocated |
| --- | --- | ---: | ---: |
| A_Nextorm_Prepared_ToDataReader | A | 5,302.54 μs | 949.29 KB |
| Linq2Db_ToDataReader | B | 4,204.47 μs | 943.65 KB |
| Provider_Direct_ToDataReader | B | 4,258.22 μs | 943.39 KB |
| Dapper_ToDataReader | B | 4,597.43 μs | 943.42 KB |
| B_Nextorm_ToDataReader | B | 5,008.46 μs | 951.91 KB |
<!-- benchmarks:tier1-streaming-readers:end -->

An empty `DbDataReader` measures setup and close overhead without reading rows:

<!-- benchmarks:tier1-streaming-empty-readers:start -->
| Arm | Category | Mean | Allocated |
| --- | --- | ---: | ---: |
| Provider_Direct_ToDataReader_Empty | B | 79.50 μs | 0.00 KB |
| Dapper_ToDataReader_Empty | B | 92.57 μs | 0.00 KB |
| Linq2Db_ToDataReader_Empty | B | 92.69 μs | 0.00 KB |
| B_Nextorm_ToDataReader_Empty | B | 220.89 μs | 16.80 KB |
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
| WriteJson_Array_Scalar | 5,721.1 μs | 0.92 | 10.31 KB |
| WriteJsonAsync_Array_Scalar | 4,948.7 μs | 0.80 | 9.97 KB |
| ToList_Dto | 6,215.8 μs | 1.00 | 783.76 KB |
| Nextorm_ToList_Dto_Json | 7,040.5 μs | 1.13 | 784.07 KB |
| WriteJson_Array_Dto | 7,424.2 μs | 1.20 | 403.13 KB |
| Linq2Db_ToList_Json | 8,423.1 μs | 1.36 | 963.75 KB |
| WriteJson_Array_ScalarPayload | 8,201.0 μs | 1.32 | 11493.13 KB |
| WriteJsonAsync_Array_ScalarPayload | 8,612.0 μs | 1.39 | 11493.34 KB |
| Dapper_ToList_Json | 8,835.7 μs | 1.42 | 1273.72 KB |
| EFCore_ToList_Json | 10,377.1 μs | 1.67 | 3158.27 KB |
| WriteJson_Array_WideDto | 10,384.0 μs | 1.67 | 11495.97 KB |
<!-- benchmarks:tier1-write-json:end -->

CSV (`large_table` slice, 10,000 rows; Ratio relative to `Nextorm_ToList`):

<!-- benchmarks:tier1-csv:start -->
| Method | Mean | Ratio | Allocated |
| --- | ---: | ---: | ---: |
| Nextorm_StringProjection_ToList | 2.617 ms | 0.34 | 1018.15 KB |
| Nextorm_StringProjection_WriteCsv | 3.302 ms | 0.42 | 949.13 KB |
| Nextorm_ToList | 7.805 ms | 1.00 | 2266.74 KB |
| Nextorm_ToList_ManualCsv | 9.237 ms | 1.18 | 4375.42 KB |
| Nextorm_WriteCsv | 9.480 ms | 1.21 | 1733.92 KB |
| Linq2Db_ToList_ManualCsv | 11.925 ms | 1.53 | 4087.51 KB |
| Dapper_ToList_ManualCsv | 12.323 ms | 1.58 | 5100.61 KB |
| EFCore_ToList_ManualCsv | 13.288 ms | 1.70 | 6750.96 KB |
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
