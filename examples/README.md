# nextorm examples

Three runnable console applications that model realistic demo databases. Each one provisions its
database with [Testcontainers](https://dotnet.testcontainers.org/) on first run (downloading and
caching the official dataset), then executes the five demo queries and prints the result rows.

The model SQL lives next to the code, in each project's `Sql/` folder, and every C# method names the
file it models. Each project README maps every SQL construct to the nextorm construct that replaces
it — start there to understand how to translate a query.

| Project | Provider | Dataset | Model SQL |
|---|---|---|---|
| [`nextorm.examples.postgres.aviasales`](nextorm.examples.postgres.aviasales/README.md) | `nextorm.postgres` | PostgreSQL Pro demo «Авиаперевозки» (schema `bookings`), 2025-09-01 | [Sql/](nextorm.examples.postgres.aviasales/Sql) |
| [`nextorm.examples.mssql.adventureworks`](nextorm.examples.mssql.adventureworks/README.md) | `nextorm.sqlserver` | AdventureWorks 2022 | [Sql/](nextorm.examples.mssql.adventureworks/Sql) |
| [`nextorm.examples.clickhouse.analytics`](nextorm.examples.clickhouse.analytics/README.md) | `nextorm.clickhouse` | ClickHouse `datasets.hits_v1` | [Sql/](nextorm.examples.clickhouse.analytics/Sql) |

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

All queries run to completion: the program reports `[ OK ]`/`[FAIL]` per query and ends with an
`n/m succeeded` summary. Only documented engine gaps (`NotSupportedException`/`QueryPreparationException`)
are recorded as `FAIL`; any other error still aborts the run.

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

* **Derived query as the primary `FROM` source** — `ctx.From(derivedQuery).Join(...)` still fails during
  preparation (`QueryPreparationException: Select must return new anonymous type`). The reference
  queries declare those subqueries as `WITH`, and the examples express them with the CTE API
  `With(name, query).From(name)` — which supports `Join`, `GroupBy`, `Having`, `OrderBy` and `Limit` —
  so none of the demo queries hit this gap. Tracked in
  [`sql-capabilities-gap-analysis.md`](../docs/specs/roadmap/sql-capabilities-gap-analysis.md) (known
  gap 10); see the [CTE guide](../docs/guide/09-cte.md).
* **T-SQL `FORMAT`** — no portable built-in, so the example declares a provider-specific
  `[SqlFunction("format")]` UDF (`DemoUdf.Format`) instead of projecting the raw `DateTime`.
* **SQL Server `PIVOT`** — the native `EntityBuilder.Pivot` now exists, but it accepts only a plain
  table/entity source; query 5 pivots a join-derived `WITH OrderMargins` CTE with a computed aggregate
  and `FOR` column, so it throws instead of being rewritten as conditional aggregation.
* **ClickHouse** — `windowFunnel`, `lagInFrame` → `lag_in_frame`, `multiIf` → `multi_if` and
  `countIf`/`uniq` are now expressed natively in LINQ (`runningAccumulate` is still approximated by a
  framed `sum_over`); `arrayMap`/`arrayFilter`/array columns, `uniqMerge` (`-Merge`/`-State`) and
  `groupArray`/tuple results still have no LINQ surface, so those queries throw instead of running the
  original SQL through `WithSql`.

## Extra course exercises

On top of the five demo queries each project adds six course-style queries (6–11) over the same data:
top-N per group, conditional/FILTER aggregation, `WITHIN GROUP` percentiles, `string_agg`/`group_concat`,
`LAG`/`LEAD` growth, `NTILE` RFM segmentation, Pareto 80/20, rolling windows, and a device/session
breakdown for ClickHouse. All of them are `WORKING` except where noted in the method comment.

## Verified

Every project is built (`dotnet build`, 0 warnings) and the queries are run against the provisioned
datasets. The examples deliberately express the original SQL without workarounds, so a run executes
**every query to the end**: each `Program` catches the documented engine-gap exceptions, prints
`[ OK ]`/`[FAIL]`, and finishes with a `n/m succeeded` summary. A `FAIL` means the query hit a
documented gap and threw instead of falling back to raw SQL; any other error still aborts.

Last run against Testcontainers (Podman):

| Project | Result | Remaining `FAIL`s (documented gaps) |
|---|---|---|
| postgres aviasales | 11/11 | — |
| mssql adventureworks | 11/11 | — |
| clickhouse analytics | 8/11 | `ArrayAnalytics`, `Incremental`, `Retention` (arrays / `-Merge` / `groupArray` have no LINQ surface) |

The detailed, dated verification log (per-query failures and the roadmap item that tracks each) lives in
[`VERIFICATION.md`](VERIFICATION.md) — update it on every re-run.

The ClickHouse retention query in `nextorm.examples.clickhouse.analytics/Sql/clickhouse_retention.sql`
was also corrected: the original referenced `UserID` outside the subquery that exposes it and divided
by the cohort-week instead of the cohort size.
