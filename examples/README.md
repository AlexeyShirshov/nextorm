# nextorm examples

Three runnable console applications that model the demo databases used in
[`docs/specs/demodb/`](../docs/specs/demodb). Each one provisions its database with
[Testcontainers](https://dotnet.testcontainers.org/) on first run (downloading and caching the
official dataset), then executes the five demo queries and prints the result rows.

| Project | Provider | Dataset | Queries |
|---|---|---|---|
| [`nextorm.examples.postgres.aviasales`](nextorm.examples.postgres.aviasales) | `nextorm.postgres` | PostgreSQL Pro demo «Авиаперевозки» (schema `bookings`), 2025-09-01 | [postgres-demodb](../docs/specs/postgres-demodb) |
| [`nextorm.examples.mssql.adventureworks`](nextorm.examples.mssql.adventureworks) | `nextorm.sqlserver` | AdventureWorks 2022 | [mssql-demodb](../docs/specs/mssql-demodb) |
| [`nextorm.examples.clickhouse.analytics`](nextorm.examples.clickhouse.analytics) | `nextorm.clickhouse` | ClickHouse `datasets.hits_v1` | [clickhouse-demodb](../docs/specs/clickhouse-demodb) |

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

## Notes on the nextorm API

Building these examples surfaced and fixed six engine bugs (all covered by the existing test suites):

* computed `GROUP BY` keys were emitted with an `AS alias` (`group by date_trunc('day', d) as "x"`),
  which no supported dialect accepts;
* aggregate projections (`count`/`count_distinct`/`sum`/`avg`/`min`/`max`/`stdev`) were not aliased, so
  a derived table could not expose them under their property name;
* aggregate arguments were rendered with the alias provider unset, which crashed when the argument was
  a column of a joined entity (`sum(t4.price)`);
* `ORDER BY` expressions were rendered without table aliases, so a column name shared by two joined
  tables (`DueDate` in supply-chain) was ambiguous;
* `decimal` literals were silently dropped (`t.IsPrimitive` is false for `decimal`), so
  `x / 1000000.0m` or `pct <= 0.80m` produced invalid SQL.

Each query is written to mirror the **original SQL structurally** (CTEs become derived queries,
`WITH ... JOIN` becomes a derived-query join, window functions run over the aggregate) and does **not**
add a workaround. Where nextorm lacks a capability the query is still expressed naturally and fails;
the gap is tracked in the roadmap instead of being masked:

* **Derived query as the primary `FROM` source** — `ctx.From(derivedQuery).Join(...)` fails during
  preparation (`QueryPreparationException: Select must return new anonymous type`). Used by the
  aviasales delay-chain/occupancy queries and the adventureworks churn/supply-chain queries. Tracked in
  [`sql-capabilities-gap-analysis.md`](../docs/specs/roadmap/sql-capabilities-gap-analysis.md) (known
  gap 10); the structural alternative is the `With(name, query).From(name)` CTE API
  ([guide](../docs/guide/09-cte.md)).
* **`Math.Round` over `double` on PostgreSQL** — `round(double precision, int)` does not exist, so the
  natural call fails. Tracked in [`todo_postgres.md`](../docs/specs/roadmap/todo_postgres.md).
* **T-SQL `FORMAT`** — no built-in, so the demo projects the raw `DateTime`. Tracked in
  [`todo_mssql.md`](../docs/specs/roadmap/todo_mssql.md).
* **SQL Server `PIVOT`** — unsupported; the demo uses the conditional-aggregation equivalent. Tracked
  in [`todo_mssql.md`](../docs/specs/roadmap/todo_mssql.md).
* **ClickHouse** — the LINQ surface cannot express `arrayMap`/`arrayFilter`/array columns,
  `windowFunnel`, `uniqMerge` (`-Merge`/`-State`), `groupArray`, `multiIf`, `lagInFrame`/
  `runningAccumulate`, or materialize `UInt64`/array results; those queries run the original SQL
  verbatim through `WithSql`. Tracked in
  [`todo_clickhouse.md`](../docs/specs/roadmap/todo_clickhouse.md).

## Extra course exercises

On top of the five demo queries each project adds six course-style queries (6–11) over the same data:
top-N per group, conditional/FILTER aggregation, `WITHIN GROUP` percentiles, `string_agg`/`group_concat`,
`LAG`/`LEAD` growth, `NTILE` RFM segmentation, Pareto 80/20, rolling windows, and a device/session
breakdown for ClickHouse.

## Verified

Every project is built (`dotnet build`, 0 warnings) and the queries are run against the provisioned
datasets. Because the examples deliberately express the original SQL without workarounds, a full run
**stops at the first unimplemented feature** listed above (the earlier workarounds have been removed);
queries needing no missing capability still return rows. See
[`docs/specs/demodb/README.md`](../docs/specs/demodb/README.md) for the original coverage matrix.

The ClickHouse retention query in `docs/specs/clickhouse-demodb/clickhouse_retention.sql` was also
corrected: the original referenced `UserID` outside the subquery that exposes it and divided by the
cohort-week instead of the cohort size.
