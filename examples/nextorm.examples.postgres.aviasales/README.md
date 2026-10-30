# nextorm.examples.postgres.aviasales

Comparison project for the PostgreSQL Pro demo database «Авиаперевозки» (schema `bookings`, dump
`demo-20250901-3m`): every query is implemented **twice** — with nextorm and with
[linq2db](https://github.com/linq2db/linq2db) — and both arms are executed against the same dataset,
then benchmarked.

The project shows **how a real SQL query becomes nextorm code, and how it looks in another LINQ ORM**.
Every method pair models exactly one query file in [`Sql/`](Sql): the method comment names it, and the
tables below map each SQL construct to the nextorm construct that replaces it. Read a `Sql/*.sql` file
next to its two implementations and you can translate your own queries in either direction.

## Layout

| Path | What it is |
|---|---|
| [`Sql/*.sql`](Sql) | Original model queries — the reference both arms are verified against |
| [`AviasalesQueries.cs`](AviasalesQueries.cs) | nextorm implementation of the 5 demo queries + 6 course-style exercises |
| [`Entities.cs`](Entities.cs) | nextorm `[SqlTable]` / `[Column]` entity interfaces for the `bookings` schema |
| [`Linq2DbQueries.cs`](Linq2DbQueries.cs) | linq2db implementation of the same eleven queries |
| [`Linq2DbEntities.cs`](Linq2DbEntities.cs) | linq2db `[Table]` / `[Column]` POCOs for the same tables |
| [`Comparison.cs`](Comparison.cs) | Runs both arms, prints `[ OK ]`/`[FAIL]`/`[DIFF]`, compares row counts |
| [`Benchmarks.cs`](Benchmarks.cs) | BenchmarkDotNet class: one `[Benchmark]` per arm and query |
| [`DemoDatabase.cs`](DemoDatabase.cs) | Testcontainers provisioning and dataset download |
| [`Program.cs`](Program.cs) | Entry point: comparison run, or `--benchmark` |

## Running

```bash
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project examples/nextorm.examples.postgres.aviasales -c Debug
```

Pass `--connection "<cs>"` (or set `AVIASALES_CONNECTION`) to skip the container and dataset download.
Add `--log-sql` to print the nextorm SQL, `--only Name1,Name2` to run a subset, and `--benchmark` to
run the BenchmarkDotNet comparison instead of the plain run (see below).

The plain run executes both arms per query, prints the first rows of each and a per-query verdict, then
an `n/m queries matched in both arms` summary. `[ OK ]` means both arms returned the same number of
rows; `[FAIL]` means one arm hit a documented engine gap (`NotSupportedException`,
`QueryPreparationException` or `LinqToDBException`); `[DIFF]` means the row counts differ. Any other
exception still aborts the run. See [`../README.md`](../README.md) for the shared run notes.

### Benchmarks

`--benchmark` provisions the database once (the same named volume as the plain run) and then runs
BenchmarkDotNet in-process (`Job.ShortRun` + `InProcessEmitToolchain`, `[MemoryDiagnoser]`). Every
query has a `Nextorm_*` baseline and a `Linq2Db_*` arm; the measured code is only "build + execute +
materialise" (no printing, no JSON). A full filtered example:

```bash
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project examples/nextorm.examples.postgres.aviasales -c Release -- \
  --benchmark --filter '*AircraftDelayChains*'
```

Set `NEXTORM_EXAMPLES_BENCH_FULL=1` for the full out-of-process BenchmarkDotNet job. Reports land in
`BenchmarkDotNet.Artifacts/` under the current directory.

### The linq2db arm

The linq2db queries mirror the SQL structurally as well: typed CTEs use `AsCte`/`From`, window
functions use `Sql.Window.*`, `STDDEV` uses `AnalyticFunctions.StdDevSamp`, and `string_agg` uses
`Sql.StringAggregate(...).ToValue()`. Two constructs have no linq2db LINQ surface and are issued
through `FromSql` (still inside linq2db, not a separate driver): the ordered-set
`percentile_disc(...) WITHIN GROUP (...)` in query 8, and nothing else — the `jsonb ->>` operator uses
`Sql.Expr`. Both are annotated in [`Linq2DbQueries.cs`](Linq2DbQueries.cs).

## SQL → nextorm: general rules

SQL is modelled as a chain of LINQ calls over table interfaces; the call order (roughly) matches the
SQL clause order: `From().Join().Where().GroupBy().Having().OrderBy().Limit().Select()`.

| SQL | nextorm |
|---|---|
| `FROM t` | `ctx.From<IT>()`, where `IT` is an interface marked `[SqlTable("schema.t")]` |
| `WITH x AS (SELECT …)` | declare it as a CTE: the typed `query.AsCte("x")` + `ctx.From(cte)` (member access, type inference) or the string `ctx.With("x", query).From("x")` (columns read by name, `t.GetInt32("id")`, because the name carries no type); a later CTE reads an earlier one the same way |
| `SELECT a AS x` | `.Select(t => new { x = t.A })` (anonymous type = result row) |
| `JOIN u ON a = b` / `LEFT JOIN` | `.Join<IU>((t, u) => …)` / `.LeftJoin<IU>((t, u) => …)`; joined items are `p.Item1`, `p.Item2`, … |
| `WHERE c` | `.Where(t => c)` |
| `GROUP BY a, b` | `.GroupBy(t => new { t.A, t.B })`, or `.SelectGroup(t => new { t.A, t.B, total = SqlFunctions.Sql.sum(t.X) })` to infer the keys from the projection (query 5) |
| `HAVING c` | `.Having(g => c)` (repeat the aggregate) or a derived query + `.Where(...)` |
| `ORDER BY a, b DESC` | `.OrderBy(t => t.A).OrderByDescending(t => t.B)` (chained calls = multiple keys) |
| `LIMIT n` | `.Limit(n)` |
| `COUNT(*)`, `SUM(x)`, `AVG`, `MIN`, `MAX`, `STDDEV`, `COUNT(DISTINCT x)` | `SqlFunctions.Sql.count()`, `.sum(x)`, `.avg(x)`, `.min(x)`, `.max(x)`, `.stdev(x)`, `.count_distinct(x)` |
| `CASE WHEN c THEN a ELSE b END` | ternary `c ? a : b` |
| `AVG(CASE WHEN c THEN x END)` | filtered aggregate `SqlFunctions.Sql.avg(x, () => c)` |
| `SUM(CASE WHEN c THEN 1 ELSE 0 END)` | `SqlFunctions.Sql.sum(c ? 1 : 0)` (or `SqlFunctions.Sql.count(() => c)`) |
| `LAG(x) OVER (PARTITION BY p ORDER BY o)` | `SqlFunctions.Sql.lag(x).Over(partitionBy: () => p, orderBy: () => o)` |
| `RANK()` / `ROW_NUMBER()` / `NTILE(n) OVER (…)` | `SqlFunctions.Sql.rank()` / `.row_number()` / `.ntile(n)` `.Over(…)` |
| `SUM(x) OVER (ORDER BY o ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)` | `SqlFunctions.Sql.sum_over(x).Over(SqlFunctions.Sql.asc(() => o), WindowFrame.RowsUnboundedPrecedingToCurrentRow)` |
| `AVG(x) OVER (ORDER BY o ROWS BETWEEN 6 PRECEDING AND CURRENT ROW)` | `SqlFunctions.Sql.avg_over(x).Over(SqlFunctions.Sql.asc(() => o), WindowFrame.Rows(WindowFrameBound.Preceding(6), WindowFrameBound.CurrentRow))` |
| `col ->> 'ru'` on `jsonb` | `SqlFunctions.Postgres.json_get_text(col, "ru")` |
| `EXTRACT(EPOCH FROM a - b) / 60` | `SqlFunctions.Sql.date_diff("second", a, b) / 60.0` |
| `EXTRACT(ISODOW FROM d)` | `SqlFunctions.Sql.date_diff("day", SqlFunctions.Sql.date_trunc("week", d), d) + 1` |
| `DATE_TRUNC('day' \| 'week' \| 'month', d)` | `SqlFunctions.Sql.date_trunc("day" \| "week" \| "month", d)` |
| `NULLIF(x, 0)` | `SqlFunctions.Sql.nullif(x, 0)` |
| `to_char(d, 'YYYY-MM')` | `SqlFunctions.Postgres.to_char(d, "YYYY-MM")` |
| `string_agg(x, ', ')` | `SqlFunctions.Sql.string_agg(x, ", ")` |
| `percentile_disc(0.5) WITHIN GROUP (ORDER BY x)` | `SqlFunctions.Postgres.percentile_disc(0.5, () => x)` |
| `ROUND(x, n)` | `Math.Round(x, n)` (runs in C# after materialization) |

Two nextorm-specific rules that trip people up:

* **A computed `GROUP BY` key must be repeated verbatim in `Select`.** After `.GroupBy(...)` the
  `Select` lambda is still typed over the *source entity*, not over the key, so
  `GroupBy(b => new { D = date_trunc("day", b.BookDate) })` must be followed by
  `Select(b => new { D = date_trunc("day", b.BookDate), … })`.
* **A derived query can be the primary `FROM` source while joined** — `ctx.From(derived).Join(...)` is
  supported (a `Where` before the join is pushed onto the derived query). The reference queries declare
  their subqueries as CTEs, so the example uses the typed `query.AsCte(name)` / `ctx.From(cte)` API
  (see rule 1); queries 1–5 are expressed this way and are all **WORKING**.
  Every method carries a `WORKING` / `NOT WORKING` comment saying which it is and, when it fails, why.
* **Named joins are opt-in and need a named projection.** A chain names its slots with
  `ctx.From(...).WithAlias(Alias.Root)` plus a trailing `Alias.<Name>` on each join, and reads them as
  `p.Root`/`p.Name` instead of `p.ItemN`. Two constraints come with it: a joined **entity** uses the
  source-less form (`.Join<IEntity>((l, r) => …, Alias.X)`), while a joined **CTE** passes its
  descriptor (`.Join(cte, …, Alias.X)`), and the chain's **whole projection must be a named type**,
  because the alias generator renders that concrete type and reports `NORMGEN009` for an anonymous one
  (#229). Every query in this example names its joins; the `route_monthly_revenue` chain of query 5 needs
  no DTO because its root is the mapped `ITimetable` table.

## Demo queries: SQL file → method

Markers: **OK** — expressed with the regular LINQ API; **≡** — same result via an equivalent construct;
**FAIL** — the faithful port hits a gap and fails during preparation (documented in the method comment).

| # | SQL | Method | Coverage | Notes |
|---|---|---|---|---|
| 1 | [`aircraft_delay_chains.sql`](Sql/aircraft_delay_chains.sql) | `AviasalesQueries.AircraftDelayChains` | OK | two **typed** CTEs (`AsCte`: `flight_delays`, `delay_chains` reading the first and projected into the named `DelayChainRow`); the outer chain is aliased — `WithAlias(Alias.DelayChain)` + `Alias.Airplane` — and read as `p.DelayChain`/`p.Airplane`; `LAG(...) OVER` → `lag(...).Over(...)`; `->> 'ru'` → `json_get_text`; `EXTRACT(EPOCH)/60` → `date_diff("second",…)/60.0` |
| 2 | [`business_occupancy_matrix.sql`](Sql/business_occupancy_matrix.sql) | `AviasalesQueries.BusinessOccupancyMatrix` | OK | two CTEs joined to each other and to `airplanes_data`, both projected into named rows (`FlightOccupancyRow`, `BusinessCapacityRow`) and all three slots named — `WithAlias(Alias.Occupancy)` + `Alias.Capacity`/`Alias.Airplane`; `EXTRACT(ISODOW…)` → `date_diff("day", date_trunc("week",…),…)+1`; `AVG(CASE WHEN day=n THEN ratio END)` → conditional `avg(day == n ? ratio : null)`; `NULLIF` → `nullif` |
| 3 | [`passenger_noshow_analysis.sql`](Sql/passenger_noshow_analysis.sql) | `AviasalesQueries.PassengerNoShowAnalysis` | OK | `passenger_stats` typed CTE groups `passenger_flight_history` with member access; `CASE WHEN bp.seat_no IS NULL THEN 1 ELSE 0 END` → `x == null ? 1 : 0`; `total_booked = total_noshows` in the outer `Where` |
| 4 | [`rolling_revenue_metrics.sql`](Sql/rolling_revenue_metrics.sql) | `AviasalesQueries.RollingRevenueMetrics` | OK | CTE `daily_revenue`; `DATE_TRUNC('day')` → `date_trunc("day",…)`; `SUM OVER (ROWS UNBOUNDED PRECEDING)` → `sum_over(...).Over(..., RowsUnboundedPrecedingToCurrentRow)`; `AVG OVER (6 PRECEDING)` → `avg_over(...).Over(..., Rows(Preceding(6), CurrentRow))` |
| 5 | [`route_network_abc_xyz.sql`](Sql/route_network_abc_xyz.sql) | `AviasalesQueries.RouteNetworkAbcXyz` | OK | three chained **typed** CTEs (`AsCte`: `route_monthly_revenue`, `route_aggregates`, `abc_analys`); the two grouped CTEs use `SelectGroup`, so the keys (`route` + `flight_month`, then `route`) are inferred from the projection instead of a separate `GroupBy`; the four-table join of `route_monthly_revenue` names every slot — `WithAlias(Alias.Timetable)` + `Alias.Departure`/`Arrival`/`Segment`/`Ticket` — and needs no DTO because its root is a mapped table; `STDDEV` → `stdev` (projected `(decimal?)`, so the one-month-per-route case stays distinct from zero); `SUM(...) OVER() / SUM(...) OVER()` → `sum_over(...).Over()`; `city ->> 'ru' \|\| ' -> '` → `json_get_text(...) + " -> " + …`; ABC/XYZ `CASE` → ternary chain |

## Course exercises (6–11)

No standalone `.sql` files: these are extra exercises over the same schema, described inline in
[`AviasalesQueries.cs`](AviasalesQueries.cs). All of them are **WORKING** (each method carries the
`WORKING` comment).

| Method | Technique |
|---|---|
| `TopRoutesByCity` | `RANK() OVER (PARTITION BY city ORDER BY revenue DESC)` top-N per group |
| `AirportOnTimePerformance` | conditional aggregation (`SUM(CASE…)`) plus `HAVING`-equivalent derived filter |
| `DelayPercentilesByModel` | `percentile_disc(0.5\|0.9) WITHIN GROUP (ORDER BY …)` |
| `FrequentFlyers` | `string_agg` + `HAVING count() >= 5` + `LIMIT` |
| `PassengerGrowth` | `LAG` over a monthly aggregate |
| `CancellationByRoute` | conditional `COUNT` + rate, top-N |

See [`../README.md`](../README.md) for the shared run notes and the list of engine gaps these examples
exercise.
