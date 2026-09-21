# nextorm.examples.mssql.adventureworks

Runnable nextorm port of the AdventureWorks 2022 OLTP database (`Sales`, `Production`, `Person`,
`Purchasing`). Provider: `nextorm.sqlserver`.

The point of this project is to show **how a real SQL query becomes nextorm code**. Every method in
[`AdventureWorksQueries.cs`](AdventureWorksQueries.cs) models exactly one query file in [`Sql/`](Sql) —
the method comment names it, and the tables below map each SQL construct to the nextorm construct that
replaces it. Read a `Sql/*.sql` file next to its method and you can translate your own queries.

## Layout

| Path | What it is |
|---|---|
| [`Sql/*.sql`](Sql) | Original T-SQL model queries — the reference the C# is verified against |
| [`AdventureWorksQueries.cs`](AdventureWorksQueries.cs) | nextorm implementation of the 5 demo queries + 6 course-style exercises |
| [`Entities.cs`](Entities.cs) | `[SqlTable]` / `[Column]` entity interfaces for the AdventureWorks schemas |
| [`DemoUdf.cs`](DemoUdf.cs) | Provider `[SqlFunction("format")]` UDF for T-SQL `FORMAT` |
| [`DemoDatabase.cs`](DemoDatabase.cs) | Testcontainers provisioning and `.bak` restore |
| [`Program.cs`](Program.cs) | Entry point; runs every query, catching per-query failures, and prints a summary |

## Running

```bash
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project examples/nextorm.examples.mssql.adventureworks -c Debug
```

Pass `--connection "<cs>"` (or set `ADVENTUREWORKS_CONNECTION`) to skip the container and restore. The
program runs every query, prints `[ OK ]`/`[FAIL]` per query, and ends with an `n/m succeeded` summary —
a `FAIL` is a documented engine gap (`NotSupportedException`/`QueryPreparationException`); any other error
still aborts the run. See [`../README.md`](../README.md) for details.

## SQL → nextorm: general rules

SQL is modelled as a chain of LINQ calls over table interfaces; the call order (roughly) matches the
SQL clause order: `From().Join().Where().GroupBy().Having().OrderBy().Limit().Select()`.

| SQL (T-SQL) | nextorm |
|---|---|
| `FROM t` | `ctx.From<IT>()`, where `IT` is an interface marked `[SqlTable("Schema.t")]` |
| `WITH x AS (SELECT …)` | declare it as a CTE: `ctx.With("x", query).From("x")`; a later CTE reads an earlier one by name (`ctx.From("x")`). CTE columns are read by name (`t.GetInt32("id")`) because the CTE source is untyped |
| `SELECT a AS x` | `.Select(t => new { x = t.A })` (anonymous type = result row) |
| `JOIN u ON a = b` / `LEFT JOIN` | `.Join(ctx.From<IU>(), (t, u) => …)` / `.LeftJoin(...)`; joined items are `p.Item1`, `p.Item2`, … |
| `WHERE c` | `.Where(t => c)` |
| `GROUP BY a, b` | `.GroupBy(t => new { t.A, t.B })` |
| `HAVING c` | `.Having(g => c)` (repeat the aggregate) or a derived query + `.Where(...)` |
| `ORDER BY a, b DESC` | `.OrderBy(t => t.A).OrderByDescending(t => t.B)` (chained calls = multiple keys) |
| `TOP n` / `LIMIT n` | `.Limit(n)` |
| `COUNT(*)`, `SUM(x)`, `AVG`, `MIN`, `MAX`, `STDEV`, `COUNT(DISTINCT x)` | `SqlFunctions.Sql.count()`, `.sum(x)`, `.avg(x)`, `.min(x)`, `.max(x)`, `.stdev(x)`, `.count_distinct(x)` |
| `CASE WHEN c THEN a ELSE b END` | ternary `c ? a : b` |
| `PIVOT (…)` | `EntityBuilder.Pivot(...)` emits native T-SQL `PIVOT` and accepts a derived query as the source (query 5 pivots a 5-table join with a computed `Margin`/`FOR` column) |
| `LAG(x) OVER (PARTITION BY p ORDER BY o)` | `SqlFunctions.Sql.lag(x).Over(partitionBy: () => p, orderBy: () => o)` |
| `MAX(x) OVER()` | `SqlFunctions.Sql.max_over(x).Over()` |
| `ROW_NUMBER()` / `RANK()` / `NTILE(n) OVER (…)` | `SqlFunctions.Sql.row_number()` / `.rank()` / `.ntile(n)` `.Over(…)` |
| `SUM(x) OVER (PARTITION BY p ORDER BY o ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)` | `SqlFunctions.Sql.sum_over(x).Over(partitionBy: () => p, orderBy: () => o, frame: WindowFrame.RowsUnboundedPrecedingToCurrentRow)` |
| `AVG(x) OVER (… ROWS BETWEEN 2 PRECEDING AND CURRENT ROW)` | `SqlFunctions.Sql.avg_over(x).Over(…, frame: WindowFrame.Rows(WindowFrameBound.Preceding(2), WindowFrameBound.CurrentRow))` |
| `DATEADD(month, DATEDIFF(month, 0, d), 0)` | `SqlFunctions.Sql.date_trunc("month", d)` (SQL Server 2022 `datetrunc`) |
| `DATEADD(quarter, DATEDIFF(quarter, 0, d), 0)` | `SqlFunctions.Sql.date_trunc("quarter", d)` |
| `DATEADD(day, n, d)` | `SqlFunctions.Sql.date_add("day", n, d)` |
| `DATEDIFF(day, a, b)` | `SqlFunctions.Sql.date_diff("day", a, b)` |
| `DATEPART(quarter, d)` | `SqlFunctions.Sql.date_diff("quarter", SqlFunctions.Sql.date_from_parts(y, 1, 1), d) + 1` |
| `AVG(CAST(qty AS FLOAT))` | `SqlFunctions.Sql.avg((double)qty)` |
| `FORMAT(d, 'yyyy-MM-dd')` | no portable built-in; declared as a provider UDF `DemoUdf.Format` with `[SqlFunction("format")]`  |
| `ROUND(x, n)` | `Math.Round(x, n)` (runs in C# after materialization) |

Two nextorm-specific rules that trip people up:

* **A computed `GROUP BY` key must be repeated verbatim in `Select`.** After `.GroupBy(...)` the
  `Select` lambda is still typed over the *source entity*, not over the key, so
  `GroupBy(p => new { M = date_trunc("month", p.OrderDate) })` must be followed by
  `Select(p => new { M = date_trunc("month", p.OrderDate), … })`.
* **Derived queries as a `FROM` source and as a join side** — `ctx.From(derived).Join(...)` works, and a
  `QueryCommand<T>` can be joined directly; a `Where` may precede the join (it is pushed onto the derived
  query), while other modifiers must stay inside the derived query. The reference queries declare their
  subqueries as CTEs, so the example uses `ctx.With(name, query)` / `ctx.From(name)` (see rule 1);
  queries 1, 3 and 4 are expressed this way and are **WORKING**.
* **Native `PIVOT` over a derived source** — `EntityBuilder.Pivot(...)` emits native T-SQL `PIVOT` and
  accepts a derived query as the source, so query 5 pivots the 5-table join with its computed `Margin`
  and `FOR` column directly (`FOR QuarterNum IN (1,2,3,4)` over the numeric `datepart(quarter, …)`; the
  string `'Q' + datepart(...)` form needs an explicit T-SQL `CAST`). Every method carries a `WORKING`
  comment saying what it is and, where relevant, which construct it exercises.

## Demo queries: SQL file → method

Markers: **OK** — expressed with the regular LINQ API; **≡** — same result via an equivalent construct;
**FAIL** — the faithful port hits a gap and fails (documented in the method comment).

| # | SQL | Method | Coverage | Notes |
|---|---|---|---|---|
| 1 | [`mssql_vip_churn.sql`](Sql/mssql_vip_churn.sql) | `AdventureWorksQueries.VipChurn` | OK | two CTEs (`CustomerOrders`, `CustomerMetrics` grouping the first); `MAX(OrderDate) OVER()` → `max_over(...).Over()`; `LAG(…) OVER(…)` → `lag(...).Over(...)`; `DATEDIFF(day,…)` → `date_diff("day",…)`; `FORMAT(…)` → `DemoUdf.Format`; manager `LEFT JOIN` on the CTE |
| 2 | [`mssql_rolling_kpi.sql`](Sql/mssql_rolling_kpi.sql) | `AdventureWorksQueries.RollingKpi` | OK | CTE `MonthlySales`; `DATEADD(month, DATEDIFF(month,0,…),0)` → `date_trunc("month",…)`; cumulative/3-month `AVG` frames → `sum_over`/`avg_over` with `WindowFrame`; `FORMAT(…)` → `DemoUdf.Format` |
| 3 | [`mssql_supply_chain.sql`](Sql/mssql_supply_chain.sql) | `AdventureWorksQueries.SupplyChain` | OK | two CTEs (`SupplierDelays`, `ProductionImpact`) joined by name; `DATEADD(day,5,…)` → `date_add("day",5,…)`; `DATEDIFF(day,…)` → `date_diff("day",…)` |
| 4 | [`mssql_product_abc_xyz.sql`](Sql/mssql_product_abc_xyz.sql) | `AdventureWorksQueries.ProductAbcXyz` | OK | three chained CTEs; `DATEADD(quarter,…)` → `date_trunc("quarter",…)`; `STDEV` → `stdev`; `AVG(CAST(qty AS FLOAT))` → `avg((double)qty)`; `SUM(...) OVER(ORDER BY … DESC)/SUM(...) OVER()` → `sum_over(...).Over(desc(...)) / sum_over(...).Over()` |
| 5 | [`mssql_quarterly_pivot.sql`](Sql/mssql_quarterly_pivot.sql) | `AdventureWorksQueries.QuarterlyPivot` | OK | native `Pivot` over a **derived query**: the join 5 tables and the computed `Margin`/`FOR` column live in the derived query, `PIVOT ... FOR QuarterNum IN (1,2,3,4)` over the numeric `datepart(quarter, …)` (string `'Q'+datepart` needs an explicit T-SQL `CAST`, so the numeric quarter is pivoted); pivot cells stay nullable |

## Course exercises (6–11)

No standalone `.sql` files: these are extra exercises over the same schema, described inline in
[`AdventureWorksQueries.cs`](AdventureWorksQueries.cs). All of them are **WORKING** (each method carries
the `WORKING` comment).

| Method | Technique |
|---|---|
| `TopProductsByCategory` | `ROW_NUMBER() OVER (PARTITION BY category ORDER BY revenue DESC)` top-N per group |
| `TerritoryYearOverYear` | `LAG` over a yearly aggregate |
| `CustomerRfm` | `NTILE(4) OVER (ORDER BY … DESC)` RFM segmentation |
| `QuotaAttainment` | target vs actual, computed in `Select` |
| `TerritoryGrowthMonthOverMonth` | `LAG` over a monthly aggregate |
| `CustomerPareto` | ABC / Pareto 80-20 concentration with running window shares |

See [`../README.md`](../README.md) for the shared run notes and the list of engine gaps these examples
exercise.
