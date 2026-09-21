# nextorm.examples.clickhouse.analytics

Runnable nextorm port of ClickHouse's official «anonymized web analytics» sample
(`datasets.hits_v1`) plus a stand-specific `AggregatingMergeTree` materialized view. Provider:
`nextorm.clickhouse`.

The point of this project is to show **how a real SQL query becomes nextorm code** — and where the
portable LINQ surface stops. Every method in [`ClickHouseQueries.cs`](ClickHouseQueries.cs) models
exactly one query file in [`Sql/`](Sql); the method comment names it, states whether it is `WORKING`,
and — when it is `NOT WORKING` — why (with the roadmap document that tracks the gap). Missing capabilities
are **not** worked around: the affected method throws `NotSupportedException` instead of falling back to
raw SQL. The tables below map each SQL construct to the nextorm construct that replaces it.

## Layout

| Path | What it is |
|---|---|
| [`Sql/*.sql`](Sql) | Original model queries — the reference the C# is verified against |
| [`ClickHouseQueries.cs`](ClickHouseQueries.cs) | nextorm implementation of the 5 demo queries + 6 course-style exercises |
| [`Entities.cs`](Entities.cs) | `[SqlTable]` / `[Column]` entity interfaces for `hits_v1` and the materialized view |
| [`DemoDatabase.cs`](DemoDatabase.cs) | Testcontainers provisioning and `hits_v1` load |
| [`Program.cs`](Program.cs) | Entry point; runs every query, catching per-query failures, and prints a summary |

## Running

```bash
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project examples/nextorm.examples.clickhouse.analytics -c Debug
```

Pass `--connection "<cs>"` (or set `CLICKHOUSE_ANALYTICS_CONNECTION`) to skip the container and load.
The first run pulls `clickhouse/clickhouse-server:25.8-alpine` and several GB of data. The program runs
every query, prints `[ OK ]`/`[FAIL]` per query, and ends with an `n/m succeeded` summary — a `FAIL` is a
documented engine gap (`NotSupportedException`/`QueryPreparationException`); any other error still
aborts the run. See [`../README.md`](../README.md) for details.

## SQL → nextorm: general rules

A query is built by chaining LINQ calls over table interfaces; the order (roughly) matches the SQL
clause order: `From().Where().GroupBy().Having().OrderBy().Limit().Select()`.

| SQL (ClickHouse) | nextorm |
|---|---|
| `FROM datasets.hits_v1` | `ctx.From<IHit>()` with `[SqlTable("datasets.hits_v1")]` |
| `WITH x AS (SELECT …)` | declare it as a CTE: `ctx.With("x", query).From("x")`; a later CTE reads an earlier one by name (`ctx.From("x")`). CTE columns are read by name (`t.GetInt64("UserID")`) because the CTE source is untyped |
| `SELECT a AS x` | `.Select(h => new { x = h.A })` |
| `JOIN u ON a = b` | `.Join(ctx.From<IU>(), (h, u) => …)`; joined items are `p.Item1`, `p.Item2`, … |
| `WHERE c` | `.Where(h => c)` |
| `GROUP BY a` | `.GroupBy(h => new { h.A })` |
| `HAVING c` | `.Having(g => c)` or a derived query + `.Where(...)` |
| `ORDER BY a, b DESC` | `.OrderBy(h => h.A).OrderByDescending(h => h.B)` |
| `LIMIT n` | `.Limit(n)` |
| `count()` | `SqlFunctions.Sql.count()` |
| `countIf(c)` | `SqlFunctions.ClickHouse.count_if(() => c)` |
| `uniqExact(x)` | `SqlFunctions.ClickHouse.uniq_exact(x)` (bound by the ClickHouse dialect) |
| `uniq(x)` | `SqlFunctions.ClickHouse.uniq(x)` |
| `quantile(0.99)(x)` | `SqlFunctions.ClickHouse.quantile(0.99, x)` |
| `lagInFrame(x) OVER (PARTITION BY p ORDER BY o)` | `SqlFunctions.ClickHouse.lag_in_frame(x).Over(partitionBy: () => p, orderBy: () => o)` |
| `runningAccumulate(if(c, 1, 0)) OVER (PARTITION BY p ORDER BY o)` | `SqlFunctions.Sql.sum_over(c ? 1 : 0).Over(partitionBy: () => p, orderBy: () => o, frame: WindowFrame.RowsUnboundedPrecedingToCurrentRow)` |
| `SUM(x) OVER (ORDER BY o ROWS BETWEEN 6 PRECEDING AND CURRENT ROW)` | `SqlFunctions.Sql.sum_over(x).Over(SqlFunctions.Sql.asc(() => o), WindowFrame.Rows(WindowFrameBound.Preceding(6), WindowFrameBound.CurrentRow))` |
| `Date - Date` (difference) | `SqlFunctions.Sql.date_diff("second", a, b)` |
| `windowFunnel(1800)(ts, c1, c2, …)` | `SqlFunctions.ClickHouse.window_funnel(1800, ts, c1, c2, …)` |
| `multiIf(c1, r1, c2, r2, …)` | `SqlFunctions.ClickHouse.multi_if(when(c1, r1), …, otherwise(rLast))` |
| `arrayMap`/`arrayFilter` (higher-order lambdas), `uniqMerge` (`-Merge`/`-State`), `groupArray`/tuple results | no LINQ surface yet ; the query is **NOT WORKING** and throws instead of falling back to raw SQL |

> **Unsigned types.** `hits_v1` stores `UserID`/`WatchID` as `UInt64`, and ClickHouse returns `UInt64`
> from `count()`/`uniq()`/`sum()` over unsigned inputs. The dialect already casts `count`, `uniq`,
> `windowFunnel` and friends to a signed type; a plain `UInt64` column, however, has no CLR reader
> getter, so the query casts it in SQL — `UserID = (long)h.UserId` renders `cast(UserID as Int64)` —
> before reading it back with `GetInt64(...)`. `Funnel`/`DailyTraffic` also rely on a compile-time
> `new DateTime(...)` literal being bound as a parameter rather than inlined.

## Demo queries: SQL file → method

Markers: **OK** — expressed with the regular LINQ API; **FAIL** — the faithful port hits a gap and
throws (documented in the method comment).

| # | SQL | Method | Coverage | Notes |
|---|---|---|---|---|
| 1 | [`clickhouse_array_analytics.sql`](Sql/clickhouse_array_analytics.sql) | `ClickHouseQueries.ArrayAnalytics` | FAIL | `arrayMap`/`arrayFilter` (higher-order lambdas) and grouping by an array have no LINQ surface (`split_by_char` and `length` over arrays exist, but the higher-order functions do not); throws instead of falling back to `WithSql` |
| 2 | [`clickhouse_funnel.sql`](Sql/clickhouse_funnel.sql) | `ClickHouseQueries.Funnel` | OK | `windowFunnel` → `window_funnel(...)`; inner `GROUP BY UserID` + outer `GROUP BY level` → two derived queries |
| 3 | [`clickhouse_incremental.sql`](Sql/clickhouse_incremental.sql) | `ClickHouseQueries.Incremental` | FAIL | `uniqMerge` over `AggregatingMergeTree` has no LINQ surface; throws instead of falling back to `WithSql` |
| 4 | [`clickhouse_retention.sql`](Sql/clickhouse_retention.sql) | `ClickHouseQueries.Retention` | FAIL | the reference has `WITH first_visits, cohort_sizes` CTEs, but `groupArray((…))` returns an array/tuple (no LINQ surface); throws instead of falling back to `WithSql` |
| 5 | [`clickhouse_sessions.sql`](Sql/clickhouse_sessions.sql) | `ClickHouseQueries.Sessions` | OK | three chained CTEs (`sessions`, `session_flags`, `session_counts`); `lagInFrame` → `lag_in_frame(...).Over(...)`; `runningAccumulate(if(…))` → cumulative `sum_over(c ? 1 : 0)`; `quantile(0.99)(…)` → `SqlFunctions.ClickHouse.quantile(0.99, …)` |

> The retention query in `Sql/clickhouse_retention.sql` was corrected on porting: the original
> referenced `UserID` outside the subquery that exposes it and divided by the cohort-week instead of the
> cohort size.

## Course exercises (6–11)

No standalone `.sql` files: these are extra exercises over the same schema, described inline in
[`ClickHouseQueries.cs`](ClickHouseQueries.cs). All of them are **WORKING** and fully LINQ.

| Method | Technique | API |
|---|---|---|
| `DailyTraffic` | `GROUP BY EventDate` with `count()` + `uniqExact` | LINQ |
| `TopLandingPages` | `URL` grouping + `uniq` | LINQ |
| `DeviceSplit` | conditional aggregates via `countIf` + `uniq` + ternary device key | LINQ |
| `TopReferrers` | `RefererDomain` grouping + `uniq` | LINQ |
| `SessionDepth` | sessionization + histogram buckets via `multi_if` | LINQ |
| `RollingActivity` | 7-day rolling window + cumulative window over an aggregate | LINQ |

See [`../README.md`](../README.md) for the shared run notes and the list of engine gaps these examples
exercise.
