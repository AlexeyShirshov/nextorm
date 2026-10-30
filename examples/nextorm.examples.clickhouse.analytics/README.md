# nextorm.examples.clickhouse.analytics

Comparison project for ClickHouse's official «anonymized web analytics» sample (`datasets.hits_v1`) plus
a stand-specific `AggregatingMergeTree` materialized view. Every query is implemented **twice** — with
nextorm and with [linq2db](https://github.com/linq2db/linq2db) — then both arms are executed against the
same dataset and benchmarked.

The project shows **how a real SQL query becomes nextorm code — and where the portable LINQ surface
stops**. Every method pair models exactly one query file in [`Sql/`](Sql); the method comment names it.
Missing capabilities are **not** worked around on the nextorm side: the affected method throws
`NotSupportedException` instead of falling back to raw SQL. Only `Incremental` (`uniqMerge` over an
`AggregateFunction` state) still throws — its linq2db counterpart works through `FromSql`, which makes
the gap visible side by side. The tables below map each SQL construct to the nextorm construct that
replaces it.

## Layout

| Path | What it is |
|---|---|
| [`Sql/*.sql`](Sql) | Original model queries — the reference both arms are verified against |
| [`ClickHouseQueries.cs`](ClickHouseQueries.cs) | nextorm implementation of the 5 demo queries + 6 course-style exercises |
| [`Entities.cs`](Entities.cs) | nextorm `[SqlTable]` / `[Column]` entity interfaces |
| [`Linq2DbQueries.cs`](Linq2DbQueries.cs) | linq2db implementation of the same eleven queries |
| [`Linq2DbEntities.cs`](Linq2DbEntities.cs) | linq2db `[Table]` / `[Column]` POCOs for `hits_v1` and the materialized view |
| [`Comparison.cs`](Comparison.cs) | Runs both arms, prints `[ OK ]`/`[FAIL]`/`[DIFF]`, compares row counts |
| [`Benchmarks.cs`](Benchmarks.cs) | BenchmarkDotNet class: one `[Benchmark]` per arm and query |
| [`DemoDatabase.cs`](DemoDatabase.cs) | Testcontainers provisioning and `hits_v1` load |
| [`Program.cs`](Program.cs) | Entry point: comparison run, or `--benchmark` |

## Running

```bash
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project examples/nextorm.examples.clickhouse.analytics -c Debug
```

Pass `--connection "<cs>"` (or set `CLICKHOUSE_ANALYTICS_CONNECTION`) to skip the container and load.
The first run pulls `clickhouse/clickhouse-server:25.8-alpine` and several GB of data. Add
`--only Name1,Name2` to run a subset and `--benchmark` to run the BenchmarkDotNet comparison.

The plain run executes both arms per query, prints the first rows of each and a per-query verdict, then
an `n/m queries matched in both arms` summary. `[ OK ]` means both arms returned the same number of
rows; `[FAIL]` means one arm hit a documented engine gap; `[DIFF]` means the row counts differ. Any
other exception still aborts the run. See [`../README.md`](../README.md) for the shared run notes.

### Benchmarks

`--benchmark` reuses the loaded dataset and runs BenchmarkDotNet in-process (`Job.ShortRun` +
`InProcessEmitToolchain`, `[MemoryDiagnoser]`). Every query has a `Nextorm_*` baseline and a
`Linq2Db_*` arm (`Incremental` is linq2db-only); a filtered run:

```bash
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project examples/nextorm.examples.clickhouse.analytics -c Release -- \
  --benchmark --filter '*DailyTraffic*'
```

Set `NEXTORM_EXAMPLES_BENCH_FULL=1` for the full out-of-process BenchmarkDotNet job.

### The linq2db arm

`windowFunnel`, `groupArray`/tuples, `uniqMerge` and the `quantile` threshold are delivered through
linq2db's raw terminals (`FromSql` / `Sql.Expr`) where its LINQ surface has no raw-aggregate API; the
rest is regular linq2db LINQ (`Sql.Window`, `Count(DISTINCT)` in place of the approximate `uniq`,
`Sql.Expr` for `multiIf`/array functions). ClickHouse calls the schema a *database*, so the tables are
attached with `DatabaseName("datasets")`. Every deviation is annotated in
[`Linq2DbQueries.cs`](Linq2DbQueries.cs).

Two representation differences are intentional: `uniq` is **approximate** by design, so the linq2db arm
uses `COUNT(DISTINCT)` (exact) and the `Users` column can differ by a small amount (for example
`DeviceSplit` desktop: 86 944 vs 86 317 on the current dataset); and the retention matrix is projected
with `toString(groupArray(...))` on the linq2db arm because CLR has no tuple-array type for it, while
nextorm materialises `Tuple<…>[]` and formats it in C#.

## SQL → nextorm: general rules

A query is built by chaining LINQ calls over table interfaces; the order (roughly) matches the SQL
clause order: `From().Where().GroupBy().Having().OrderBy().Limit().Select()`.

| SQL (ClickHouse) | nextorm |
|---|---|
| `FROM datasets.hits_v1` | `ctx.From<IHit>()` with `[SqlTable("datasets.hits_v1")]` |
| `WITH x AS (SELECT …)` | declare it as a CTE: the typed `query.AsCte("x")` + `ctx.From(cte)` (member access, type inference) or the string `ctx.With("x", query).From("x")` (columns read by name, `t.GetInt64("UserID")`, because the name carries no type); a later CTE reads an earlier one the same way |
| `SELECT a AS x` | `.Select(h => new { x = h.A })` |
| `JOIN u ON a = b` | `.Join<IU>((h, u) => …)`; joined items are `p.Item1`, `p.Item2`, … |
| `JOIN u ON a = b` with slot names | `.Join<IU>((h, u) => …, Alias.U)` plus `.WithAlias(Alias.Root)` on the chain's root: the slots are then read as `p.Root`/`p.U`. A joined **CTE** passes its descriptor instead of a type parameter (`.Join(cte, …, Alias.X)`). The chain's projection must be a **named** type (the alias generator renders the chain's concrete type and reports `NORMGEN009` on an anonymous one, #229). Every query in this example names its joins. Query 4 is currently blocked by an unrelated packaging defect in the tuple-array cast ([#359](https://github.com/AlexeyShirshov/nextorm/issues/359)), not by the alias surface |
| `WHERE c` | `.Where(h => c)` |
| `GROUP BY a` | `.GroupBy(h => new { h.A })`, or `.SelectGroup(h => new { h.A, n = SqlFunctions.Sql.count() })` to infer the keys from the projection (most demo queries). `Retention.first_visits` and `Retention.weekly` keep `GroupBy`: the first would group by the `UserId` cast, the second must stay a `Select` producer because the shipped alias generator recovers a consumed chain only through `Select` |
| `HAVING c` | `.Having(g => c)` or a derived query + `.Where(...)` |
| `ORDER BY a, b DESC` | `.OrderBy(h => h.A).OrderByDescending(h => h.B)` |
| `LIMIT n` | `.Limit(n)` |
| `count()` | `SqlFunctions.Sql.count()` |
| `countIf(c)` | `SqlFunctions.Sql.count(() => c)` (the shared filter API renders `countIf`) |
| `uniqExact(x)` | `SqlFunctions.ClickHouse.uniq_exact(x)` (bound by the ClickHouse dialect) |
| `uniq(x)` | `SqlFunctions.ClickHouse.uniq(x)` |
| `quantile(0.99)(x)` | `SqlFunctions.ClickHouse.quantile(0.99, x)` |
| `lagInFrame(x) OVER (PARTITION BY p ORDER BY o)` | `SqlFunctions.ClickHouse.lag_in_frame(x).Over(partitionBy: () => p, orderBy: () => o)` |
| `runningAccumulate(if(c, 1, 0)) OVER (PARTITION BY p ORDER BY o)` | `SqlFunctions.Sql.sum_over(c ? 1 : 0).Over(partitionBy: () => p, orderBy: () => o, frame: WindowFrame.RowsUnboundedPrecedingToCurrentRow)` |
| `SUM(x) OVER (ORDER BY o ROWS BETWEEN 6 PRECEDING AND CURRENT ROW)` | `SqlFunctions.Sql.sum_over(x).Over(SqlFunctions.Sql.asc(() => o), WindowFrame.Rows(WindowFrameBound.Preceding(6), WindowFrameBound.CurrentRow))` |
| `Date - Date` (difference) | `SqlFunctions.Sql.date_diff("second", a, b)` |
| `windowFunnel(1800)(ts, c1, c2, …)` | `SqlFunctions.ClickHouse.window_funnel(1800, ts, c1, c2, …)` |
| `multiIf(c1, r1, c2, r2, …)` | `SqlFunctions.ClickHouse.multi_if(when(c1, r1), …, otherwise(rLast))` |
| `splitByChar(sep, s)` | `SqlFunctions.ClickHouse.split_by_char(sep, s)` |
| `arrayMap(x -> lower(x), a)` | `SqlFunctions.ClickHouse.array_map(x => x.ToLower(), a)` |
| `arrayFilter(x -> length(x) > 3, a)` | `SqlFunctions.ClickHouse.array_filter(x => x.ToLower().Length > 3, a)` |
| `length(s)` (string) | `x.ToLower().Length` inside the lambda — member access on the lambda parameter itself (`x.Length`) is rejected by the translator, so `length` is reached through the `lower()` call |
| `groupArray((a, b))` | `SqlFunctions.ClickHouse.group_array(Tuple.Create(a, b))`, materialised as a CLR `Tuple<…>[]` |
| `uniqMerge(users_state)` (`-Merge`/`-State`) | no LINQ surface yet on the released packages; the query is **NOT WORKING** and throws instead of falling back to raw SQL |

> **Unsigned types.** `hits_v1` stores `UserID`/`WatchID` as `UInt64`, and ClickHouse returns `UInt64`
> from `count()`/`uniq()`/`sum()` over unsigned inputs. The dialect casts the aggregate/function results
> that declare a signed CLR type (`count`, `uniq`, `windowFunnel`, ...) to that type; a plain `UInt64`
> column now materialises directly as `ulong` (`UserID = h.UserId` reads `GetFieldValue<ulong>`, with no
> SQL cast). These demo queries still cast to `long` (`UserID = (long)h.UserId` renders `cast(UserID as
> Int64)`) because the rest of the query works in signed 64-bit. `Funnel`/`DailyTraffic` also rely on a
> compile-time `new DateTime(...)` literal being bound as a parameter rather than inlined.

## Demo queries: SQL file → method

Markers: **OK** — expressed with the regular LINQ API; **FAIL** — the faithful port hits a gap and
throws (documented in the method comment); **BLOCKED** — the port is complete but a packaged engine
defect stops it at execution (tracked in the linked issue).

| # | SQL | Method | Coverage | Notes |
|---|---|---|---|---|
| 1 | [`clickhouse_array_analytics.sql`](Sql/clickhouse_array_analytics.sql) | `ClickHouseQueries.ArrayAnalytics` | OK | `splitByChar` → `split_by_char`; `arrayMap`/`arrayFilter` → `array_map`/`array_filter`; the result is grouped by the array expression |
| 2 | [`clickhouse_funnel.sql`](Sql/clickhouse_funnel.sql) | `ClickHouseQueries.Funnel` | OK | `windowFunnel` → `window_funnel(...)`; inner `GROUP BY UserID` + outer `GROUP BY level` → two derived queries |
| 3 | [`clickhouse_incremental.sql`](Sql/clickhouse_incremental.sql) | `ClickHouseQueries.Incremental` | FAIL | `uniqMerge` (`-Merge` over an `AggregateFunction` state) has no LINQ surface on the released packages; throws instead of falling back to `WithSql` |
| 4 | [`clickhouse_retention.sql`](Sql/clickhouse_retention.sql) | `ClickHouseQueries.Retention` | **BLOCKED** ([#359](https://github.com/AlexeyShirshov/nextorm/issues/359)) | the `WITH first_visits, cohort_sizes, weekly` CTEs → typed `AsCte`; `cohort_sizes` and `weekly` are projected into named rows (`CohortSizeRow`, `RetentionWeekRow`) and the outer chain names both slots (`WithAlias(Alias.Week)` + `Alias.Cohort`); the inner join + `groupArray((…))` → `group_array(Tuple.Create(…))`. The aliased SQL is correct, but the `1.0.9-rc4` package wraps the tuple array in ``cast(… as Array(Nullable(Tuple`2)))`` — a CLR type name, not a ClickHouse type — so the server rejects the statement (`Code: 62`) |
| 5 | [`clickhouse_sessions.sql`](Sql/clickhouse_sessions.sql) | `ClickHouseQueries.Sessions` | OK | three chained **typed** CTEs (`AsCte`: `sessions`, `session_flags`, `session_counts`); `lagInFrame` → `lag_in_frame(...).Over(...)`; `runningAccumulate(if(…))` → cumulative `sum_over(c ? 1 : 0)`; `quantile(0.99)(…)` → `SqlFunctions.ClickHouse.quantile(0.99, …)` |

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
