# nextorm examples

Three runnable **comparison** console applications over realistic demo databases. Each query in a
project is implemented twice — with nextorm and with [linq2db](https://github.com/linq2db/linq2db) —
and both arms are executed against the same provisioned database, then benchmarked with
BenchmarkDotNet. Each example provisions its database with
[Testcontainers](https://dotnet.testcontainers.org/) on first run (downloading and caching the official
dataset into a named Docker volume); the examples only read, so the loaded database is reused on later
runs.

The model SQL lives next to the code, in each project's `Sql/` folder, and every C# method pair names
the file it models. Each project README maps every SQL construct to the nextorm construct that replaces
it — start there to understand how to translate a query in either direction.

| Project | Provider | Dataset | Model SQL |
|---|---|---|---|
| [`nextorm.examples.postgres.aviasales`](nextorm.examples.postgres.aviasales/README.md) | `nextorm.postgres` | PostgreSQL Pro demo «Авиаперевозки» (schema `bookings`), 2025-09-01 | [Sql/](nextorm.examples.postgres.aviasales/Sql) |
| [`nextorm.examples.mssql.adventureworks`](nextorm.examples.mssql.adventureworks/README.md) | `nextorm.sqlserver` | AdventureWorks 2022 | [Sql/](nextorm.examples.mssql.adventureworks/Sql) |
| [`nextorm.examples.clickhouse.analytics`](nextorm.examples.clickhouse.analytics/README.md) | `nextorm.clickhouse` | ClickHouse `datasets.hits_v1` | [Sql/](nextorm.examples.clickhouse.analytics/Sql) |

> The postgres and clickhouse projects consume the **published `nextorm.*` NuGet packages** (version
> pinned by `NextOrmVersion` in [`Directory.Packages.props`](../Directory.Packages.props)) instead of
> referencing `src/` directly, so they double as a real consumer of what ships on nuget.org.
> **`nextorm.examples.mssql.adventureworks` is an explicit exception** (#232): it references
> `nextorm.core`/`nextorm.sqlserver` from `src/` via `ProjectReference` so it can demonstrate the
> current-checkout typed `Pivot`/`Unpivot` surface, which is not in a published package yet. Its run
> and build commands are the same as the other examples.

## Running

The container runtime is discovered the standard Testcontainers way (the `DOCKER_HOST` environment
variable or the default socket). With Podman on Windows run from WSL:

```bash
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project examples/nextorm.examples.postgres.aviasales -c Debug
```

Datasets are large (the PostgreSQL dump is ~133 MB compressed / ~1.3 GB loaded, AdventureWorks is a
~200 MB backup, `hits_v1` is several GB); they are downloaded once into `~/.cache/nextorm/<name>/`
and reused. To run against an already provisioned server instead, pass a connection string — either
`--connection "<cs>"` or the matching environment variable:

| Project | Argument | Environment variable |
|---|---|---|
| aviasales | `--connection` | `AVIASALES_CONNECTION` (fallback `NEXTORM_DEMODB_POSTGRES_CONNECTION`) |
| adventureworks | `--connection` | `ADVENTUREWORKS_CONNECTION` (fallback `NEXTORM_DEMODB_MSSQL_CONNECTION`) |
| analytics | `--connection` | `CLICKHOUSE_ANALYTICS_CONNECTION` (fallback `NEXTORM_DEMODB_CLICKHOUSE_CONNECTION`) |

When a connection string is supplied, no container is started and no dataset is downloaded.

The provisioned database is cached in a named volume (`nextorm-examples-{aviasales,adventureworks,clickhouse}-data`),
so a warm run skips the dataset copy and the load and only starts a container against the existing
volume. Set `NEXTORM_EXAMPLES_RELOAD=true` to delete the volume and rebuild from scratch.

All queries run to completion: each arm prints its rows, the runner reports
`[ OK ]`/`[FAIL]`/`[DIFF]` per query and the program ends with an `n/m queries matched in both arms`
summary. Only documented engine gaps (`NotSupportedException`, `QueryPreparationException`,
`LinqToDBException`) are recorded as `FAIL`; any other error still aborts the run. Use
`--only Name1,Name2` to run a subset.

## Comparison arms

Each project contains the nextorm implementation (`*Queries.cs` + `Entities.cs`), its linq2db
counterpart (`Linq2DbQueries.cs` + `Linq2DbEntities.cs`), the side-by-side runner (`Comparison.cs`) and
a BenchmarkDotNet class (`Benchmarks.cs`). `--benchmark` runs the comparison instead of the plain pass:

```bash
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project examples/nextorm.examples.postgres.aviasales -c Release -- \
  --benchmark --filter '*AircraftDelayChains*'
```

The benchmark uses `Job.ShortRun` + `InProcessEmitToolchain` (warmup 3 / iterations 3) and
`[MemoryDiagnoser]`; set `NEXTORM_EXAMPLES_BENCH_FULL=1` for the full out-of-process job. The measured
code is only "build + execute + materialise" — no printing or JSON formatting. Every query has a
`Nextorm_*` baseline and a `Linq2Db_*` arm; `Incremental` on ClickHouse is linq2db-only because the
nextorm arm throws the documented gap.

## Notes on the nextorm API

Building these examples surfaced and fixed eight engine bugs (all covered by the existing test suites):

* computed `GROUP BY` keys were emitted with an `AS alias` (`group by date_trunc('day', d) as "x"`),
  which no supported dialect accepts;
* aggregate projections (`count`/`count_distinct`/`sum`/`avg`/`min`/`max`/`stdev`) were not aliased, so
  a derived table could not expose them under their property name;
* aggregate arguments were rendered with the alias provider unset, which crashed when the argument was
  a column of a joined entity (`sum(t4.price)`);
* `ORDER BY` expressions were rendered without table aliases, so a column name shared by two joined
  tables (`DueDate` in supply-chain) was ambiguous;
* `decimal` literals were silently dropped (`t.IsPrimitive` is false for `decimal`), so
  `x / 1000000.0m` or `pct <= 0.80m` produced invalid SQL;
* a compile-time `new DateTime(2014, 3, 20)` literal was rendered by visiting the constructor arguments
  and concatenating their literals (`2014320`) instead of being folded into a bound parameter;
* numeric casts from unsigned CLR types (`(long)someUInt64`) were silently dropped, so a ClickHouse
  `UInt64` column (for example `hits_v1.UserID`) could not be materialised at all;
* a CTE/source built over a raw table name (`From(string)`) only exposed `Where`/`Join`/`Select`, so the
  reference `WITH` queries could not use `GroupBy`/`Having`/`OrderBy`/`Limit`.

Each query is written to mirror the **original SQL structurally** (CTEs become derived queries,
`WITH ... JOIN` becomes a derived-query join, window functions run over the aggregate) and does **not**
add a workaround. Every method carries a `WORKING` / `NOT WORKING` comment stating which it is and, when
it fails, why. Where nextorm lacks a capability the query is expressed naturally and fails (or, when the
construct has no LINQ surface at all, throws `NotSupportedException`); the gap is tracked in the roadmap
instead of being masked:

* **Derived query as the primary `FROM` source** — `ctx.From(derivedQuery).Join(...)` is supported (a
  `Where` before the join is pushed onto the derived query; other modifiers stay inside it). The
  reference queries declare those subqueries as `WITH`, and the examples express them with the typed CTE
  API `query.AsCte(name)` / `ctx.From(cte)` — which supports `Join`, `GroupBy`, `Having`, `OrderBy` and
  `Limit` (the string `With(name, query).From(name)` API remains available); see the
  [CTE guide](../docs/guide/10-cte.md).
* **T-SQL `FORMAT`** — no portable built-in, so the example declares a provider-specific
  `[SqlFunction("format")]` UDF (`DemoUdf.Format`) instead of projecting the raw `DateTime`.
* **SQL Server `PIVOT`** — the native `EntityBuilder.Pivot(...)` is a SQL Server provider extension (the
  example adds `using NextORM.SqlServer;`) and accepts a derived source, so query 5 pivots the
  join-derived `WITH OrderMargins` CTE with its computed aggregate and `FOR` column directly instead of
  being rewritten as conditional aggregation.
* **ClickHouse** — `windowFunnel`, `lagInFrame` → `lag_in_frame`, `multiIf` → `multi_if`,
  `countIf`/`uniq` are expressed natively in LINQ (`runningAccumulate` is still approximated by a framed
  `sum_over`), and the array / higher-order-lambda surface (`splitByChar`, `arrayMap`, `arrayFilter`,
  grouping by an array) plus `groupArray((…))`/tuple results are expressed through
  `split_by_char`/`array_map`/`array_filter`/`group_array` + `Tuple.Create`; only `uniqMerge`
  (`-Merge`/`-State`) still has no LINQ surface, so that query throws instead of running the original SQL
  through `WithSql`.

## Extra course exercises

On top of the five demo queries each project adds six course-style queries (6–11) over the same data:
top-N per group, conditional/FILTER aggregation, `WITHIN GROUP` percentiles, `string_agg`/`group_concat`,
`LAG`/`LEAD` growth, `NTILE` RFM segmentation, Pareto 80/20, rolling windows, and a device/session
breakdown for ClickHouse. All of them are `WORKING` except where noted in the method comment.

## Verified

Every project is built (`dotnet build`, 0 warnings) and both arms are run against the provisioned
datasets. The examples deliberately express the original SQL without workarounds, so a run executes
**every query to the end**: each `Program` catches the documented engine-gap exceptions, prints
`[ OK ]`/`[FAIL]`/`[DIFF]`, and finishes with an `n/m queries matched in both arms` summary. `FAIL`
means one arm hit a documented gap; `DIFF` means the two arms returned different row counts; any other
error still aborts.

Last run against Testcontainers (Podman), 2026-10-30 (`NextOrmVersion = 1.0.9-rc4`):

| Project | Matched in both arms | Remaining `FAIL`s (documented gaps) |
|---|---|---|
| postgres aviasales | 11/11 | — (query 8 uses linq2db `FromSql` for `percentile_disc`) |
| mssql adventureworks | 11/11 | — (query 5 uses conditional aggregation instead of `PIVOT` on the linq2db arm) |
| clickhouse analytics | 9/11 (10 executed) | `Incremental` — nextorm has no `uniqMerge` surface; `Retention` is excluded because the rc4 package emits an invalid tuple-array cast ([#359](https://github.com/AlexeyShirshov/nextorm/issues/359)) |

The detailed, dated verification log (per-query failures and the roadmap item that tracks each) lives in
[`VERIFICATION.md`](VERIFICATION.md) — update it on every re-run.

The ClickHouse retention query in `nextorm.examples.clickhouse.analytics/Sql/clickhouse_retention.sql`
was also corrected: the original referenced `UserID` outside the subquery that exposes it and divided
by the cohort-week instead of the cohort size.
