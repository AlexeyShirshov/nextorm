# Benchmarks: Nextorm vs Dapper, linq2db and EF Core

<!-- benchmarks:conditions:start -->
Benchmark conditions: provider SQLite (`Microsoft.Data.Sqlite`) for every participant; async queries; each
cell is the **fastest method** for that library in the linked BenchmarkDotNet report. Machine: AMD EPYC 9V74
(4 logical / 2 physical cores), .NET 10.0.14, BenchmarkDotNet 0.15.8 (`ShortRun` + `InProcessEmitToolchain`).
As of **2026-11-08** for the end-to-end, allocations and Category B tables (the Category A table and the
tier-1 section keep the date of their own run). In each row the leading cell is **bold** — the fastest in the
time tables and the smallest allocation in the allocations table.
<!-- benchmarks:conditions:end -->

> **Feature-level view:** a per-feature, tiered comparison — no `Prepare()` arms, one table per tier,
> competitors as columns — is in [Feature benchmarks](feature-benchmarks.md).

## End-to-end scenarios

<!-- benchmarks:e2e:start -->
| Method | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| [Data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkIteration-report-github.md) | **9.981 μs** | 13.313 μs | 13.744 μs | 26.878 μs |
| [Wide data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkLargeIteration-report-github.md) | **7.305 ms** | 7.999 ms | 8.100 ms | 8.562 ms |
| [Where](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWhere-report-github.md) | **868.8 μs** | 1,156.3 μs | 1,199.4 μs | 2,345.1 μs |
| [Simulate work](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSimulateWork-report-github.md) | **6.622 ms** | 19.412 ms | 225.819 ms | 77.219 ms |
| [Any](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkAny-report-github.md) | **877.4 μs** | 1,176.8 μs | 1,246.9 μs | 2,457.1 μs |
| [FirstOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFirst-report-github.md) | **87.67 μs** | 121.11 μs | 220.07 μs | 242.83 μs |
| [SingleOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkSingle-report-github.md) | **89.13 μs** | 119.99 μs | 220.66 μs | 243.46 μs |
<!-- benchmarks:e2e:end -->

### Allocations

Allocations for the same fastest method per library (BenchmarkDotNet `Allocated`):

<!-- benchmarks:allocations:start -->
| Method | Nextorm | Dapper | linq2db | EF Core |
|---|:---:|:---:|:---:|:---:|
| Data fetch | **976 B** | 1,904 B | 2,088 B | 10,224 B |
| Wide data fetch | **2.14 MB** | 2.59 MB | **2.14 MB** | 4.28 MB |
| Where | **103.36 KB** | 180.71 KB | 253.52 KB | 789.16 KB |
| Simulate work | **6.25 MB** | 8.90 MB | 121.57 MB | 13.68 MB |
| Any | **85.16 KB** | 139.07 KB | 233.60 KB | 807.05 KB |
| FirstOrDefault | **8.63 KB** | 16.48 KB | 187.58 KB | 80.40 KB |
| SingleOrDefault | **8.36 KB** | 16.40 KB | 187.58 KB | 80.40 KB |
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

<!-- benchmarks:category-a:start -->
As of **2026-11-08**.

| Feature | Nextorm (prepared) | Dapper | linq2db (compiled) | EF Core (compiled) |
|---|---|---|---|---|
| `CASE WHEN` | **8.82 μs / 672 B** | 12.20 μs / 1.34 KB | 12.60 μs / 1.59 KB | 24.30 μs / 7.90 KB |
| CTE | **9.17 μs / 720 B** | 16.60 μs / 1.57 KB | 16.64 μs / 1.77 KB | — |
| `SELECT DISTINCT` | **10.19 μs / 672 B** | 13.87 μs / 1.56 KB | 13.50 μs / 1.59 KB | 26.35 μs / 7.66 KB |
| `EXCEPT` | **10.24 μs / 688 B** | 14.54 μs / 1.44 KB | 16.16 μs / 1.68 KB | 29.38 μs / 8.81 KB |
| IN-list | **11.17 μs / 1.56 KB** | 14.00 μs / 1.27 KB | 24.80 μs / 15.23 KB | 38.16 μs / 14.77 KB |
| `INTERSECT` | **9.57 μs / 672 B** | 13.58 μs / 1.29 KB | 15.15 μs / 1.63 KB | 27.98 μs / 7.94 KB |
| 4-table join | **11.95 μs / 1,008 B** | 21.18 μs / 2.02 KB | 18.87 μs / 2.07 KB | 33.04 μs / 8.49 KB |
| `LEFT JOIN` | **11.52 μs / 1.13 KB** | 16.79 μs / 2.22 KB | 16.99 μs / 2.32 KB | 29.30 μs / 9.92 KB |
| Recursive CTE | **11.08 μs / 680 B** | 17.88 μs / 1.53 KB | 18.91 μs / 1.78 KB | — |
| String functions | **8.72 μs / 665 B** | 11.94 μs / 1.28 KB | 12.52 μs / 1.64 KB | 24.14 μs / 7.49 KB |
| UDF | **8.73 μs / 696 B** | 11.95 μs / 1.28 KB | 12.51 μs / 1.64 KB | 24.12 μs / 7.49 KB |
| Window functions | **12.46 μs / 776 B** | 20.43 μs / 1.55 KB | 21.13 μs / 1.74 KB | — |
<!-- benchmarks:category-a:end -->

Nextorm prepared is the fastest on **every** feature and has the smallest allocation on 11 of 12 — IN-list is
the exception, where Dapper's cached raw SQL allocates less. The exact ratios are in the table above; they move
with the runner, so treat deltas below ~20 % as noise.

### Category B — warm cached vs regular

<!-- benchmarks:category-b:start -->
| Feature | Nextorm (cached) | Dapper | linq2db | EF Core |
|---|---|---|---|---|
| `CASE WHEN` | **11.48 μs / 3.47 KB** | 12.36 μs / 1.34 KB | 15.83 μs / 3.80 KB | 34.16 μs / 11.95 KB |
| CTE | 21.42 μs / 11.65 KB | **17.07 μs / 1.57 KB** | 33.70 μs / 9.06 KB | — |
| `SELECT DISTINCT` | **12.67 μs / 4.16 KB** | 13.95 μs / 1.56 KB | 18.07 μs / 3.91 KB | 37.39 μs / 12.20 KB |
| `EXCEPT` | **14.15 μs / 6.40 KB** | 14.81 μs / 1.44 KB | 22.76 μs / 5.05 KB | 45.38 μs / 15.31 KB |
| IN-list | 19.87 μs / 7.96 KB | **14.22 μs / 1.27 KB** | 42.52 μs / 18.91 KB | 78.89 μs / 22.61 KB |
| `INTERSECT` | **13.58 μs / 6.38 KB** | 13.70 μs / 1.29 KB | 21.72 μs / 5.01 KB | 43.97 μs / 14.38 KB |
| 4-table join | 24.32 μs / 11.33 KB | **21.52 μs / 2.02 KB** | 40.64 μs / 14.02 KB | 83.27 μs / 24.63 KB |
| `LEFT JOIN` | 18.07 μs / 6.54 KB | **16.92 μs / 2.22 KB** | 40.12 μs / 8.60 KB | 64.00 μs / 20.20 KB |
| Recursive CTE | 22.73 μs / 13.71 KB | **18.31 μs / 1.53 KB** | 36.23 μs / 8.54 KB | — |
| String functions | 12.34 μs / 4.48 KB | **11.92 μs / 1.28 KB** | 18.36 μs / 4.78 KB | 40.56 μs / 14.01 KB |
| UDF | 12.46 μs / 4.56 KB | **11.90 μs / 1.28 KB** | 25.04 μs / 6.39 KB | 39.70 μs / 13.38 KB |
| Window functions | **20.51 μs / 5.38 KB** | 20.54 μs / 1.55 KB | 36.92 μs / 5.82 KB | — |
<!-- benchmarks:category-b:end -->

In the warm path Nextorm beats Dapper on `CASE WHEN` and `SELECT DISTINCT` and is level on `INTERSECT`;
it trails Dapper by about 1.06–1.20× on string functions, `EXCEPT`, UDF and window functions, by 1.38× on
`LEFT JOIN`, and by 1.56–1.82× on 4-table join, CTE, recursive CTE and IN-list — while staying well ahead
of regular linq2db and EF Core on time. Warm-path allocations are higher than the prepared path (the plan
key is rebuilt per call); they stay far below regular EF Core and below linq2db on most features, but
exceed linq2db on CTE, recursive CTE, `EXCEPT` and `INTERSECT`.

> **IN-list, Category B.** A captured collection disables the plan cache by design (each call re-renders the
> SQL), so the captured variants pay a full build; the inline variants hit the cache. The four Nextorm
> variants span 37.5–41.4 µs — this is the feature where Dapper's cached raw SQL is furthest ahead.

## Tier-1 query-shape scenarios

Added in #188 to cover query shapes the two tables above do not exercise: projection into a fresh DTO,
SQL-side aggregates, ordered paging, streaming (buffered/unbuffered DTO vs. zero-materialization raw
readers) and cross-library JSON/CSV output. Data size: `large_table` (10,000 rows) for
projection/aggregates/paging/streaming; the JSON class seeds its own `json_bench` table and the CSV
class slices `large_table` (10,000 rows). The fair-comparison fixture invariants stay `simple_entity`=10
and `complex_entity`=3 — the "10,000 rows" above is `large_table`, not those fixtures. As of **2026-11-07**.

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
