# nextorm.examples.postgres.aviasales

Runnable nextorm port of the PostgreSQL Pro demo database «Авиаперевозки» (schema `bookings`,
dump `demo-20250901-3m`). Provider: `nextorm.postgres`.

The point of this project is to show **how a real SQL query becomes nextorm code**. Every method in
[`AviasalesQueries.cs`](AviasalesQueries.cs) models exactly one query file in [`Sql/`](Sql) — the
method comment names it, and the tables below map each SQL construct to the nextorm construct that
replaces it. Read a `Sql/*.sql` file next to its method and you can translate your own queries.

## Layout

| Path | What it is |
|---|---|
| [`Sql/*.sql`](Sql) | Original model queries — the reference the C# is verified against |
| [`AviasalesQueries.cs`](AviasalesQueries.cs) | nextorm implementation of the 5 demo queries + 6 course-style exercises |
| [`Entities.cs`](Entities.cs) | `[SqlTable]` / `[Column]` entity interfaces for the `bookings` schema |
| [`DemoDatabase.cs`](DemoDatabase.cs) | Testcontainers provisioning and dataset download |
| [`Program.cs`](Program.cs) | Entry point; runs every query, catching per-query failures, and prints a summary |

## Running

```bash
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project examples/nextorm.examples.postgres.aviasales -c Debug
```

Pass `--connection "<cs>"` (or set `AVIASALES_CONNECTION`) to skip the container and dataset download.
Add `--log-sql` to print the generated SQL. The program runs every query, prints `[ OK ]`/`[FAIL]` per
query, and ends with an `n/m succeeded` summary — a `FAIL` is a documented engine gap
(`NotSupportedException`/`QueryPreparationException`); any other error still aborts the run. See
[`../README.md`](../README.md) for details.

## SQL → nextorm: general rules

SQL is modelled as a chain of LINQ calls over table interfaces; the call order (roughly) matches the
SQL clause order: `From().Join().Where().GroupBy().Having().OrderBy().Limit().Select()`.

| SQL | nextorm |
|---|---|
| `FROM t` | `ctx.From<IT>()`, where `IT` is an interface marked `[SqlTable("schema.t")]` |
| `WITH x AS (SELECT …)` | declare it as a CTE: `ctx.With("x", query).From("x")`; a later CTE reads an earlier one by name (`ctx.From("x")`). CTE columns are read by name (`t.GetInt32("id")`) because the CTE source is untyped |
| `SELECT a AS x` | `.Select(t => new { x = t.A })` (anonymous type = result row) |
| `JOIN u ON a = b` / `LEFT JOIN` | `.Join(ctx.From<IU>(), (t, u) => …)` / `.LeftJoin(...)`; joined items are `p.Item1`, `p.Item2`, … |
| `WHERE c` | `.Where(t => c)` |
| `GROUP BY a, b` | `.GroupBy(t => new { t.A, t.B })` |
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
* **A derived query cannot yet be the primary `FROM` source while joined** — `ctx.From(derived).Join(...)`
  fails during preparation (known gap 10 in
  [`sql-capabilities-gap-analysis.md`](../../docs/specs/roadmap/sql-capabilities-gap-analysis.md)).
  The reference queries declare their subqueries as CTEs, so the example uses `ctx.With(name, query)` /
  `ctx.From(name)` (see rule 1); queries 1–5 are expressed this way and are all **WORKING**.
  Every method carries a `WORKING` / `NOT WORKING` comment saying which it is and, when it fails, why.

## Demo queries: SQL file → method

Markers: **OK** — expressed with the regular LINQ API; **≡** — same result via an equivalent construct;
**FAIL** — the faithful port hits a gap and fails during preparation (documented in the method comment).

| # | SQL | Method | Coverage | Notes |
|---|---|---|---|---|
| 1 | [`aircraft_delay_chains.sql`](Sql/aircraft_delay_chains.sql) | `AviasalesQueries.AircraftDelayChains` | OK | two CTEs (`flight_delays`, `delay_chains` reading the first); `LAG(...) OVER` → `lag(...).Over(...)`; `->> 'ru'` → `json_get_text`; `EXTRACT(EPOCH)/60` → `date_diff("second",…)/60.0`; final `JOIN airplanes_data` on the CTE |
| 2 | [`business_occupancy_matrix.sql`](Sql/business_occupancy_matrix.sql) | `AviasalesQueries.BusinessOccupancyMatrix` | OK | two CTEs joined to each other and to `airplanes_data`; `EXTRACT(ISODOW…)` → `date_diff("day", date_trunc("week",…),…)+1`; `AVG(CASE WHEN day=n THEN ratio END)` → conditional `avg(day == n ? ratio : null)`; `NULLIF` → `nullif` |
| 3 | [`passenger_noshow_analysis.sql`](Sql/passenger_noshow_analysis.sql) | `AviasalesQueries.PassengerNoShowAnalysis` | OK | `passenger_stats` CTE groups `passenger_flight_history` by name; `CASE WHEN bp.seat_no IS NULL THEN 1 ELSE 0 END` → `x == null ? 1 : 0`; `total_booked = total_noshows` in the outer `Where` |
| 4 | [`rolling_revenue_metrics.sql`](Sql/rolling_revenue_metrics.sql) | `AviasalesQueries.RollingRevenueMetrics` | OK | CTE `daily_revenue`; `DATE_TRUNC('day')` → `date_trunc("day",…)`; `SUM OVER (ROWS UNBOUNDED PRECEDING)` → `sum_over(...).Over(..., RowsUnboundedPrecedingToCurrentRow)`; `AVG OVER (6 PRECEDING)` → `avg_over(...).Over(..., Rows(Preceding(6), CurrentRow))` |
| 5 | [`route_network_abc_xyz.sql`](Sql/route_network_abc_xyz.sql) | `AviasalesQueries.RouteNetworkAbcXyz` | OK | three chained CTEs (`route_monthly_revenue`, `route_aggregates`, `abc_analys`); `STDDEV` → `stdev`; `SUM(...) OVER() / SUM(...) OVER()` → `sum_over(...).Over()`; `city ->> 'ru' \|\| ' -> '` → `json_get_text(...) + " -> " + …`; ABC/XYZ `CASE` → ternary chain |

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
