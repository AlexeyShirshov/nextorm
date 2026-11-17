# Benchmarks: Nextorm vs Dapper, linq2db and EF Core

Use these results to compare the listed query shapes under the stated SQLite setup. They are
evidence for these scenarios, not a prediction for every provider, workload, or machine.

<!-- benchmarks:conditions:start -->
Benchmark conditions: provider SQLite (`Microsoft.Data.Sqlite`) for every participant; async queries; each
cell is the **fastest method** for that library in the linked BenchmarkDotNet report. Machine: AMD Ryzen 7
5800HS with Radeon Graphics (8 logical / 4 physical cores), .NET 11.0.0, BenchmarkDotNet 0.15.8 (`Job.Default`
+ `InProcessEmitToolchain`). As of **2026-11-16** for the end-to-end, allocations and Category B tables (the
Category A table and the tier-1 section keep the date of their own run). In each row the leading cell is
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
`complex_entity`=3; the 10,000 rows refer to `large_table`, not those fixtures. Snapshot date:
**2026-11-07**.

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

> **Reading these numbers.** `ShortRun` + `InProcessEmitToolchain` is exploratory: treat deltas below ~20%
> as noise and read the full report (error, standard deviation, allocations) before drawing conclusions.
> The tables report BenchmarkDotNet `Mean`/`Allocated` as-is; they are not a universal-win claim.

### Projection — `Select(x => new Dto { Id, Str })`

Reproduce: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkProjection*`.
[Report](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkProjection-report-github.md).

| Arm | Category | Mean | Allocated |
|---|---|---:|---:|
| A_Nextorm_Prepared_ToListAsync | A | 4.284 ms | 1.3 MB |
| A_EFCore_Compiled_ToListAsync | A | 5.531 ms | 3.36 MB |
| A_Linq2Db_Compiled_ToList | A | 6.327 ms | 1.47 MB |
| A_Dapper_ToListAsync | A+B | 7.435 ms | 1.7 MB |
| B_Nextorm_Cached_ToListAsync | B | 4.370 ms | 1.3 MB |
| B_Linq2Db_ToListAsync | B | 6.597 ms | 1.48 MB |
| B_EFCore_ToListAsync | B | 9.956 ms | 3.62 MB |

### Aggregates — SQL-side `Count` / `Sum` / `GroupBy → Count`

Reproduce: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkAggregates*`.
[Report](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkAggregates-report-github.md).

| Operation | Arm | Category | Mean | Allocated |
|---|---|---:|---:|---:|
| Count | A_Nextorm_Prepared_Count | A | 14.65 μs | 408 B |
| Count | A_Linq2Db_Compiled_Count | A | 19.26 μs | 1,280 B |
| Count | A_EFCore_Compiled_Count | A | 40.92 μs | 7,744 B |
| Count | Dapper_Count | A+B | 18.23 μs | 944 B |
| Count | B_Nextorm_Cached_Count | B | 15.39 μs | 408 B |
| Count | B_Linq2Db_Count | B | 29.52 μs | 3,008 B |
| Count | B_EFCore_Count | B | 64.48 μs | 10,080 B |
| Sum | A_Nextorm_Prepared_Sum | A | 298.17 μs | 410 B |
| Sum | A_Linq2Db_Compiled_Sum | A | 316.45 μs | 1,290 B |
| Sum | A_EFCore_Compiled_Sum | A | 334.54 μs | 7,770 B |
| Sum | Dapper_Sum | A+B | 317.79 μs | 954 B |
| Sum | B_Nextorm_Cached_Sum | B | 314.20 μs | 970 B |
| Sum | B_Linq2Db_Sum | B | 367.16 μs | 3,490 B |
| Sum | B_EFCore_Sum | B | 371.48 μs | 10,978 B |
| GroupBy→Count | A_Nextorm_Prepared_GroupByCount | A | 1,468.98 μs | 4,666 B |
| GroupBy→Count | A_EFCore_Compiled_GroupByCount | A | 1,501.91 μs | 33,090 B |
| GroupBy→Count | A_Linq2Db_Compiled_GroupByCount | A | 1,511.01 μs | 7,026 B |
| GroupBy→Count | Dapper_GroupByCount | A+B | 1,477.65 μs | 12,330 B |
| GroupBy→Count | B_Nextorm_Cached_GroupByCount | B | 1,554.85 μs | 10,146 B |
| GroupBy→Count | B_Linq2Db_GroupByCount | B | 1,675.00 μs | 11,554 B |
| GroupBy→Count | B_EFCore_GroupByCount | B | 1,886.06 μs | 42,754 B |

### Paging — `OrderBy(Id).Offset(5000).Limit(100)`

Reproduce: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkPaging*`.
[Report](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkPaging-report-github.md).

| Arm | Category | Mean | Allocated |
|---|---|---:|---:|
| A_Nextorm_Prepared_PageAsync | A | 88.54 μs | 13.92 KB |
| A_Linq2Db_Compiled_Page | A | 112.67 μs | 17.61 KB |
| A_EFCore_Compiled_PageAsync | A | 133.15 μs | 44.81 KB |
| A_Dapper_PageAsync | A+B | 106.51 μs | 18.15 KB |
| B_Nextorm_Cached_PageAsync | B | 108.62 μs | 22.33 KB |
| B_Linq2Db_PageAsync | B | 139.58 μs | 22.65 KB |
| B_EFCore_PageAsync | B | 187.81 μs | 56.9 KB |

### Streaming — buffered / unbuffered DTO and zero-materialization readers

Reproduce: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkStreaming*`.
[Report](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkStreaming-report-github.md).

Buffered DTO:

| Arm | Category | Mean | Allocated |
|---|---|---:|---:|
| A_Nextorm_Prepared_ToList_Dto | A | 3,956.46 μs | 1,328.77 KB |
| Linq2Db_Compiled_ToList_Dto | A | 5,960.94 μs | 1,508.22 KB |
| A_EFCore_Compiled_ToList_Dto | A | 10,629.30 μs | 3,701.53 KB |
| Dapper_ToList_Dto | A+B | 8,223.40 μs | 1,742.27 KB |
| B_Nextorm_Cached_ToList_Dto | B | 3,954.38 μs | 1,332.18 KB |
| B_Linq2Db_ToList_Dto | B | 6,128.13 μs | 1,511.21 KB |
| B_EFCore_ToList_Dto | B | 9,005.49 μs | 3,705.72 KB |

Unbuffered DTO enumeration:

| Arm | Category | Mean | Allocated |
|---|---|---:|---:|
| A_Nextorm_Prepared_AsyncStream_Dto | A | 4,585.96 μs | 1,250.84 KB |
| A_EFCore_Compiled_AsyncStream_Dto | A | 5,292.02 μs | 3,444.77 KB |
| Dapper_AsyncStream_Dto | A+B | 4,785.98 μs | 1,485.82 KB |
| B_Nextorm_Cached_AsyncStream_Dto | B | 4,255.35 μs | 1,254.42 KB |
| B_Linq2Db_AsyncStream_Dto | B | 4,982.80 μs | 1,254.55 KB |
| B_EFCore_AsyncStream_Dto | B | 5,295.63 μs | 3,448.89 KB |

Zero-materialization raw readers (synchronous; shared non-retaining sink; EF Core excluded):

| Arm | Category | Mean | Allocated |
|---|---|---:|---:|
| A_Nextorm_Prepared_ToDataReader | A | 4,453.12 μs | 948.45 KB |
| Dapper_ToDataReader | B | 3,954.36 μs | 943.1 KB |
| Linq2Db_ToDataReader | B | 4,006.42 μs | 943.33 KB |

### JSON / CSV cross-library

Reproduce JSON: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkWriteJson*`.
[Report](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWriteJson-report-github.md).
Reproduce CSV: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkCsv*`.
[Report](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkCsv-report-github.md).

The JSON report covers `RowCount` 1,000 / 10,000 / 100,000; the 10,000-row block is shown (Ratio is
relative to `ToList_Dto` in the same block). Competitor arms are materialize → manual serializer.

| Method (10,000 rows) | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| WriteJsonAsync_Array_Scalar | 5,452.9 μs | 0.88 | 9.98 KB |
| ToList_Dto | 6,196.4 μs | 1.00 | 785.02 KB |
| WriteJson_Array_Scalar | 6,294.1 μs | 1.02 | 9.83 KB |
| Nextorm_ToList_Dto_Json | 7,261.4 μs | 1.17 | 785.33 KB |
| WriteJson_Array_Dto | 8,302.0 μs | 1.34 | 403.24 KB |
| Linq2Db_ToList_Json | 8,793.1 μs | 1.42 | 963.73 KB |
| Dapper_ToList_Json | 9,459.1 μs | 1.53 | 1,273.77 KB |
| WriteJsonAsync_Array_ScalarPayload | 9,720.8 μs | 1.57 | 11,493.34 KB |
| WriteJson_Array_ScalarPayload | 10,431.7 μs | 1.68 | 11,493.19 KB |
| EFCore_ToList_Json | 10,952.7 μs | 1.77 | 3,158.63 KB |
| WriteJson_Array_WideDto | 12,280.9 μs | 1.98 | 11,496.04 KB |

CSV (`large_table` slice, 10,000 rows; Ratio relative to `Nextorm_ToList`):

| Method | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| Nextorm_StringProjection_ToList | 2.799 ms | 0.34 | 1,019.05 KB |
| Nextorm_StringProjection_WriteCsv | 3.423 ms | 0.41 | 948.99 KB |
| Nextorm_ToList | 8.278 ms | 1.00 | 2,267.52 KB |
| Nextorm_ToList_ManualCsv | 9.806 ms | 1.18 | 4,141.82 KB |
| Nextorm_WriteCsv | 10.241 ms | 1.24 | 1,733.64 KB |
| Dapper_ToList_ManualCsv | 14.115 ms | 1.71 | 4,866.08 KB |
| Linq2Db_ToList_ManualCsv | 14.635 ms | 1.77 | 4,321.92 KB |
| EFCore_ToList_ManualCsv | 16.190 ms | 1.96 | 6,516.91 KB |

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
- `ShortRun` + `InProcessEmitToolchain` is an exploratory mode. Treat small differences as noise and
  read the full report—including mean, error, standard deviation, and allocations—before drawing
  conclusions.
- These results cover one machine, SQLite, and a fixed set of scenarios. They do not establish
  performance for every workload; reproduce them with your own data and hardware.

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

The end-to-end, allocations, Category A and Category B tables on this page are **generated** from those
reports — do not edit them by hand. After a run:

```bash
python3 tools/benchmarks/update_benchmark_tables.py          # rewrite readme.md and both article mirrors
python3 tools/benchmarks/update_benchmark_tables.py --check  # CI mode: fail when a table is stale
```

The same refresh runs from `.github/workflows/benchmarks.yml` — automatically after every merge into `main`
that touches the engine (`src/**`) or the benchmark project, and on demand from the Actions tab. It opens a
pull request with the new reports and tables.

## See also

- [Capabilities](capabilities.md) — what each library supports.
- [Query reuse: cache vs Prepare](../engine/03-plan-cache-and-reuse.md).
