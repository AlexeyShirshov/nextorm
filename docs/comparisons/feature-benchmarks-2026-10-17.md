# Feature benchmarks — 2026-10-17 (1.0.9-rc2)

**Report date:** 2026-10-17 · **Release:** 1.0.9-rc2 (branch `1.0.9-rc3`, HEAD `881bf497`) · **Provider:** SQLite

This is a dated, warm-path-only feature comparison. It is additive: the [benchmarks](benchmarks.md) page keeps
its own dated tables; this page is a new 2026-10-17 snapshot with a different layout and scope.

- **No prepared path.** Every Nextorm number comes from the implicit plan cache (`B_Nextorm_Cached_*`) or a
  direct terminal (`WriteJson`/`WriteCsv`). No `Prepare()` / `A_*` Nextorm arm is used.
- **One table per feature group.** Columns are the libraries; rows are scenarios. There is no Category A/B
  split inside a table.
- **Per query.** Each cell is `mean time / allocated`. Category B methods execute 10 queries per invocation,
  so their BenchmarkDotNet `Mean`/`Allocated` are divided by 10; Tier-1 methods execute one query per
  invocation. Tier-5 cells are one query plus serialization of 10,000 rows.
- **Marks.** `**time**` is the fastest time in the row; `**alloc**` is the smallest allocation; a fully bold
  cell is both.
- **Noise.** `ShortRun` + `InProcessEmitToolchain` is exploratory: treat differences below ~20 % as noise
  and read the full reports before drawing conclusions.

## Method

- **Machine:** AMD Ryzen 7 5800HS with Radeon Graphics (8 logical / 4 physical cores), .NET 10.0.12,
  BenchmarkDotNet 0.15.8 (`ShortRun` + `InProcessEmitToolchain`), SQLite through `Microsoft.Data.Sqlite`;
  the database lives on tmpfs.
- **Nextorm:** implicit plan cache for the Category B–derived groups, and the cached Tier-1 arms
  (`B_Nextorm_Cached_*`, `B_Nextorm_ToDataReader`). No `Prepare()` and no compiled-query arm.
- **Dapper:** raw SQL with its cached IL. Dapper has no prepared or compiled form, so its raw-SQL arm is
  the only Dapper path and is used unchanged.
- **linq2db and EF Core:** regular, non-compiled APIs (`B_Linq2Db_*`, `B_EFCore_*`). No
  `CompiledQuery`/`Compile()` arms.
- **Missing arms:** EF Core has no equivalent in this suite for CTE, recursive CTE, window functions or the
  synchronous raw reader; those cells are `—`.
- **Raw reader:** synchronous, shared non-retaining sink, zero materialization; EF Core is excluded by
  design because it has no LINQ raw-reader counterpart.
- **JSON/CSV:** the competitor arms are the closest equivalent — materialize then serialize manually —
  whereas Nextorm writes directly. These two rows are useful, but they are not a shared-terminal comparison.

Source reports (BenchmarkDotNet output committed under `benchmarks/BenchmarkDotNet.Artifacts/results/`):

- [Features, warm cached](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFairCached-report-github.md)
- [Projection](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkProjection-report-github.md)
- [Aggregates](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkAggregates-report-github.md)
- [Paging](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkPaging-report-github.md)
- [Streaming](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkStreaming-report-github.md)
- [JSON](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkWriteJson-report-github.md) ·
  [CSV](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkCsv-report-github.md)

## Tier-1 — query-shape scenarios

Projection, SQL-side aggregates, ordered paging, DTO streaming and a zero-materialization raw reader.
Nextorm is fastest on 6 of 8 scenarios and allocates least on 5; Dapper wins `Count`, and the raw reader is
1.19× behind linq2db (1.17× behind Dapper).

| Scenario | Nextorm | Dapper | linq2db | EF Core |
|---|---:|---:|---:|---:|
| Projection → DTO | **4.047 ms / 1.30 MB** | 7.286 ms / 1.70 MB | 5.958 ms / 1.48 MB | 9.881 ms / 3.62 MB |
| Count | 18.47 μs / 3.73 KB | **17.07 μs / 944 B** | 24.37 μs / 2.80 KB | 47.10 μs / 9.84 KB |
| Sum | **286.23 μs** / 4.13 KB | 297.01 μs / **954 B** | 305.35 μs / 3.41 KB | 345.24 μs / 10.72 KB |
| GroupBy → Count | **1.276 ms / 10.33 KB** | 1.290 ms / 12.04 KB | 1.416 ms / 11.27 KB | 1.397 ms / 41.94 KB |
| Paging — offset 5,000 / limit 100 | **101.31 μs** / 22.62 KB | 103.67 μs / **18.15 KB** | 140.91 μs / 22.65 KB | 185.84 μs / 56.90 KB |
| Streaming — buffered DTO | **4.044 ms / 1.30 MB** | 7.102 ms / 1.70 MB | 6.053 ms / 1.48 MB | 8.838 ms / 3.62 MB |
| Streaming — unbuffered DTO | **4.059 ms / 1.22 MB** | 4.886 ms / 1.45 MB | 4.522 ms / 1.23 MB | 5.166 ms / 3.37 MB |
| Raw reader — synchronous | 4.677 ms / 947.98 KB | 3.996 ms / **942.77 KB** | **3.942 ms** / 943.00 KB | — |

**What this says.** Nextorm's wins are the wide projection and DTO streaming (0.55–0.57× Dapper), `Sum`
(0.96×), `GroupBy` (0.99×) and paging (0.98×). `Count` is 1.08× behind Dapper. On the raw reader all three
libraries are within 1.19× on time and within 0.6 % on allocation.

## Tier-2 — relational shapes

Dapper is fastest on 5 of 6 and allocates least on all 6; Nextorm wins only `SELECT DISTINCT`
(15.53 vs 18.41 μs, 0.84×). Nextorm trails Dapper by 1.16–1.75× on the rest.

| Scenario | Nextorm | Dapper | linq2db | EF Core |
|---|---:|---:|---:|---:|
| CTE | 32.72 μs / 10.89 KB | **19.83 μs / 1.57 KB** | 56.89 μs / 9.06 KB | — |
| Recursive CTE | 35.33 μs / 13.31 KB | **24.44 μs / 1.53 KB** | 60.41 μs / 8.39 KB | — |
| 4-table join | 32.96 μs / 10.67 KB | **25.03 μs / 2.02 KB** | 65.38 μs / 14.10 KB | 114.20 μs / 24.69 KB |
| LEFT JOIN | 23.03 μs / 6.16 KB | **19.83 μs / 2.22 KB** | 69.12 μs / 8.60 KB | 94.80 μs / 20.27 KB |
| SELECT DISTINCT | **15.53 μs** / 3.80 KB | 18.41 μs / **1.56 KB** | 30.44 μs / 3.91 KB | 64.19 μs / 12.20 KB |
| IN-list — inline | 31.65 μs / 7.76 KB | **18.12 μs / 1.27 KB** | 69.44 μs / 18.91 KB | 112.63 μs / 22.61 KB |

The captured IN-list variants add 24.2 μs and 12.6 KB per query to the inline arm; that is a Nextorm
cache-lookup property, so the table keeps the inline arm for the cross-library row.

## Tier-3 — set operations

Dapper wins both by a small time margin and by 4.4–4.9× on allocation.

| Scenario | Nextorm | Dapper | linq2db | EF Core |
|---|---:|---:|---:|---:|
| EXCEPT | 17.15 μs / 6.38 KB | **16.96 μs / 1.44 KB** | 38.10 μs / 5.05 KB | 72.82 μs / 15.31 KB |
| INTERSECT | 16.79 μs / 6.36 KB | **15.60 μs / 1.29 KB** | 36.77 μs / 5.01 KB | 71.94 μs / 14.38 KB |

EXCEPT is 1.01× and INTERSECT is 1.08× behind Dapper; both stay 2.2–2.4× ahead of linq2db on time.

## Tier-4 — scalar and expression features

Nextorm wins `CASE WHEN` (0.93×) and `String.Contains` (0.82×); Dapper wins `ToUpper` (1.10×), UDF (1.09×)
and both window scenarios (1.04× and 1.11×), and allocates least on all six. Splitting `Contains`/`ToUpper`
and `ROW_NUMBER`/`SUM OVER` avoids mixing different operations in one “string” or “window” row.

| Scenario | Nextorm | Dapper | linq2db | EF Core |
|---|---:|---:|---:|---:|
| CASE WHEN | **13.04 μs** / 3.22 KB | 14.00 μs / **1.34 KB** | 25.05 μs / 3.80 KB | 56.78 μs / 11.95 KB |
| String.Contains | **14.97 μs** / 4.20 KB | 18.18 μs / **1.72 KB** | 30.95 μs / 4.78 KB | 69.55 μs / 14.01 KB |
| ToUpper | 15.29 μs / 4.20 KB | **13.93 μs / 1.28 KB** | 47.75 μs / 6.50 KB | 66.26 μs / 13.27 KB |
| UDF | 15.53 μs / 4.29 KB | **14.29 μs / 1.28 KB** | 48.46 μs / 6.50 KB | 66.25 μs / 13.27 KB |
| Window — ROW_NUMBER | 31.55 μs / 5.03 KB | **30.46 μs / 1.54 KB** | 62.11 μs / 5.99 KB | — |
| Window — SUM OVER | 33.03 μs / 5.46 KB | **29.75 μs / 1.55 KB** | 62.28 μs / 5.82 KB | — |

## Tier-5 — serialization output

Nextorm allocates least on JSON but is 1.10–1.13× behind Dapper/linq2db on time; CSV is fastest and
allocates least. The competitor arms materialize then serialize manually, so these are closest-equivalent
rows, not a shared terminal.

| Scenario | Nextorm | Dapper | linq2db | EF Core |
|---|---:|---:|---:|---:|
| JSON array — DTO (10,000 rows) | 10.352 ms / **435.64 KB** | 9.416 ms / 1.24 MB | **9.150 ms** / 963.56 KB | 11.303 ms / 3.08 MB |
| CSV (10,000 rows) | **11.076 ms / 1.70 MB** | 13.291 ms / 4.75 MB | 13.385 ms / 4.22 MB | 14.364 ms / 6.36 MB |

The JSON row uses `WriteJson_Array_Dto`, the same DTO payload as the competitor arms (the narrower
`WriteJson_Array_Scalar` is faster but writes only the key). A specialized Nextorm string-projection CSV
path is faster still (4.029 ms / 949.67 KB), but it covers a narrower shape and is not the general row
above.

## Verdict

Across the 22 like-for-like query rows, Nextorm is fastest on 9, Dapper on 12 and linq2db on 1; allocations
lead on 5, 17 and 0 respectively. Dapper remains the warm-path baseline to beat. Nextorm's wins are
concentrated in projection/streaming/aggregates, `SELECT DISTINCT`, `CASE WHEN` and `String.Contains`; the
relational shapes, set operations and most scalar/expression rows still trail. On the two serialization
rows, CSV is a Nextorm win on both metrics; JSON allocates least but is 1.10–1.13× behind on time. Both
serialization rows compare a direct writer with materialize-then-serialize arms, so read them with that
caveat.

This report intentionally says nothing about the prepared path. For that comparison, including Category A,
see the dated [benchmarks](benchmarks.md) page.

## Optimization follow-ups

The cases where this snapshot is behind linq2db are tracked in the rc3 milestone:
[#243](https://github.com/AlexeyShirshov/nextorm/issues/243) (Count/Sum allocations),
[#244](https://github.com/AlexeyShirshov/nextorm/issues/244) (CTE/recursive CTE allocations),
[#245](https://github.com/AlexeyShirshov/nextorm/issues/245) (EXCEPT/INTERSECT allocations),
[#246](https://github.com/AlexeyShirshov/nextorm/issues/246) (raw reader) and
[#247](https://github.com/AlexeyShirshov/nextorm/issues/247) (JSON streaming).

## Reproducing

```bash
# No prepared arms are used by this report.
for filter in "*SqliteBenchmarkFeaturesFairCached*" "*SqliteBenchmarkProjection*" \
              "*SqliteBenchmarkAggregates*" "*SqliteBenchmarkPaging*" "*SqliteBenchmarkStreaming*" \
              "*SqliteBenchmarkWriteJson*" "*SqliteBenchmarkCsv*"; do
  dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter "$filter"
done
```

Each linked report is the BenchmarkDotNet output committed under
`benchmarks/BenchmarkDotNet.Artifacts/results/`. Set `NEXTORM_BENCH_FULL=1` to run the full out-of-process
job instead of `ShortRun`.
