# String and array aggregates

`SqlFunctions.Sql.string_agg` is available on PostgreSQL, SQL Server 2017+, ClickHouse, MySQL/MariaDB and SQLite
([`SupportsStringAgg`](xref:NextORM.Core.ISqlDialect.SupportsStringAgg), which defaults to the umbrella [`SupportsStringArrayAggregates`](xref:NextORM.Core.ISqlDialect.SupportsStringArrayAggregates));
ClickHouse renders it as `arrayStringConcat(groupArray(x), delimiter)`, MySQL/MariaDB as
`group_concat(x separator delimiter)` and SQLite as `group_concat(x, delimiter)`. `SqlFunctions.Postgres.array_agg`
([`SupportsArrayAgg`](xref:NextORM.Core.ISqlDialect.SupportsArrayAgg)) requires an array type and is therefore PostgreSQL-only. An `array_agg`
result is an array column:

```csharp
var names = dataContext.From<IComplexEntity>()
    .Select(e => SqlFunctions.Sql.string_agg(e.String, ","))
    .First();
```

```sql
-- PostgreSQL / SQL Server
select string_agg(somestring, ',')
 from complex_entity
-- ClickHouse
select arrayStringConcat(groupArray(somestring), ',')
 from complex_entity
-- MySQL/MariaDB
select group_concat(somestring separator ',')
 from complex_entity
-- SQLite
select group_concat(somestring, ',')
 from complex_entity
```

| C# | SQL | Providers |
|---|---|---|
| `SqlFunctions.Sql.string_agg(x, delimiter)` | `string_agg(x, delimiter)` / `arrayStringConcat(groupArray(x), delimiter)` / `group_concat(x separator delimiter)` / `group_concat(x, delimiter)` | PostgreSQL, SQL Server, ClickHouse, MySQL/MariaDB, SQLite |
| `SqlFunctions.Postgres.array_agg(x)` | `array_agg(x)` | PostgreSQL |

## SQL Server 2025: `product`

`SqlFunctions.SqlServer.product(value)` renders the SQL Server 2025 `PRODUCT` aggregate in an aggregate
context. The result keeps the input type (`int?` for an `int` column), NULL rows are ignored, and a
group with no non-null value has no product (SQL `NULL`):

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Select(e => new { e.Int, Product = SqlFunctions.SqlServer.product(e.Id) })
    .ToList();
```

```sql
select nullableint, product(id) as [Product]
 from complex_entity
 group by nullableint
```

The aggregate is **SQL Server 2025 (major version 17) only** and follows the version-only
[`SupportsProductAggregate`](xref:NextORM.Core.ISqlDialect.SupportsProductAggregate) flag (which in turn
follows [`SupportsSqlServer2025Functions`](xref:NextORM.Core.ISqlDialect.SupportsSqlServer2025Functions)).
It is offered in the aggregate context only — no window/`OVER` expansion. A context declared for 16, an
unversioned context or an edition-only declaration refuses the call before SQL with `The PRODUCT function
requires SQL Server major version 17 or later; declared major version: 16.` (or `… unspecified.`). An
edition never substitutes for the declared major version.

## SQL Server: `ANY_VALUE` is refused

`SqlFunctions.SqlServer.any_value(value)` exists as a **deterministic refusal**, not as an aggregate.
On SQL Server build `17.0.5005.3` the engine rejected `ANY_VALUE` with `Msg 195` in both the aggregate
and the `OVER ()` forms, at compatibility levels 160 and 170 and with `PREVIEW_FEATURES = ON`, so no
supported SQL Server execution contract is established. Every invocation — direct or inside a query —
throws `NotSupportedException` with
`SqlFunctions.SqlServer.any_value (ANY_VALUE) is refused: SQL Server build 17.0.5005.3 rejected this
function with Msg 195, including with preview features enabled; no supported SQL Server execution
contract is established.` The refusal is raised before SQL is rendered and before any command is
created, and a sibling aggregate of the same query still executes.

This is an **observed engine rejection**, not a CLR/scope limitation and not a version gap a later
nextorm release can close. The portable `CommonFunctions.any_agg` is a different member: it stays gated
off on SQL Server (`SupportsAnyValueAggregate = false`) and is **not** replaced by this refusal; the
providers that enable it (MySQL/MariaDB via `ANY_VALUE`, ClickHouse via `any`) keep their dispatch
unchanged.

## SQL Server aggregate family (2019 and 2022)

Five [#282](https://github.com/AlexeyShirshov/nextorm/issues/282) members close the residual SQL Server
aggregate names. They are aggregate-context members: the two approximate percentiles are ordered-set
aggregates spelled `WITHIN GROUP (ORDER BY …)` with no window form, `approx_count_distinct` has no
`OVER` form in the engine at all, and `checksum_agg` — whose native `OVER` form the engine does accept —
is offered in the aggregate form only here. `grouping_id` is the analytic member of the family and needs
a grouped command.

| Member | SQL | CLR result | Empty group / all-NULL group | Requires |
|---|---|---|---|---|
| `approx_count_distinct(value)` | `approx_count_distinct(value)` | `long` (`bigint`) | `0` / `0` | major version 15 (SQL Server 2019) |
| `checksum_agg(value)` | `checksum_agg(value)` | `int?` (`int`) | `NULL` / `NULL` | every declaration (no version floor) |
| `grouping_id(expressions...)` | `grouping_id(a, b, …)` | `int` | `0` under a plain `GROUP BY`; the subtotal mask otherwise | every declaration; grouped command only |
| `approx_percentile_cont(fraction, value)` | `approx_percentile_cont(f) within group (order by value)` | `double?` (`float(53)`) | `NULL` / `NULL` | major version 16 (SQL Server 2022) |
| `approx_percentile_disc(fraction, value)` | `approx_percentile_disc(f) within group (order by value)` | `T?` — the sort input type (`int?` for an `int` column) | `NULL` / `NULL` | major version 16 (SQL Server 2022) |

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupBy(e => new { e.Int, e.Boolean })
    .Select(e => new
    {
        e.Int,
        e.Boolean,
        Distinct = SqlFunctions.SqlServer.approx_count_distinct(e.Int),
        Checksum = SqlFunctions.SqlServer.checksum_agg(e.Int),
        Group = SqlFunctions.SqlServer.grouping_id(e.Int, e.Boolean),
        Median = SqlFunctions.SqlServer.approx_percentile_cont(0.5, e.Int),
        MedianValue = SqlFunctions.SqlServer.approx_percentile_disc(0.5, e.Int)
    })
    .ToList();
```

```sql
select nullableint, b,
       approx_count_distinct(nullableint) as [Distinct],
       checksum_agg(nullableint) as [Checksum],
       grouping_id(nullableint, b) as [Group],
       approx_percentile_cont(0.5) within group (order by nullableint) as [Median],
       approx_percentile_disc(0.5) within group (order by nullableint) as [MedianValue]
 from complex_entity
 group by nullableint, b
```

`approx_count_distinct`, `approx_percentile_cont` and `approx_percentile_disc` follow the same
version-only contract as [`product`](#sql-server-2025-product): a lower declared major version, an
unversioned context or an edition-only declaration refuses the call **before SQL**, and an edition never
substitutes for a version.

```text
The APPROX_COUNT_DISTINCT function requires SQL Server major version 15 or later; declared major version: 14.
The APPROX_PERCENTILE_CONT function requires SQL Server major version 16 or later; declared major version: 15.
```

### `approx_count_distinct`

`approx_count_distinct(value)` returns `long` (`bigint`). NULL rows are not counted, and an empty group
and an all-NULL group both yield `0` — never SQL NULL. It is an estimate, not the exact
`count(distinct …)`: keep `SqlFunctions.Sql.count_distinct` when exactness matters. A constant NULL
operand renders as `CAST(NULL AS <type>)`, because a bare `NULL` operand is Msg 8117
(`Operand data type NULL is invalid for approx_count_distinct operator.`). The engine has no `OVER`
form (Msg 4113), and this surface offers the aggregate context only.

### `checksum_agg`

`checksum_agg(value)` returns `int?` (`int`). NULL rows are skipped, and an empty or all-NULL group has
no checksum (`NULL`). The value is order-insensitive but is **not** a stable identity and not a hash for
equality: identical values cancel (`checksum_agg` over `(5, 5)` is `0`, over `(5)` is `5`, over
`(5, 6)` is `3`), so it detects change rather than content. There is no version floor: every declared
SQL Server major version renders it, including an unversioned declaration. A constant NULL operand
renders as `CAST(NULL AS int)` (a bare `NULL` is Msg 8117, `Operand data type NULL is invalid for
checksum_agg operator.`). The aggregate form is the only one offered, although the engine itself would
accept `checksum_agg(...) over (...)`.

### `grouping_id`

`grouping_id(expressions...)` renders `grouping_id(a, b, …)` and returns `int`. The `params object?[]`
arguments are flattened into one SQL argument per grouping expression; a captured array is refused. Two
obligations are **enforced before SQL**, with zero statements executed, instead of being left to the
server's Msg 8161:

* every argument must match a `GROUP BY` expression, otherwise
  `The GROUPING_ID function requires every argument to match a GROUP BY expression.` (expressions are
  compared with the query's expression-plan comparer, so an equivalent expression matches);
* the call is legal only in the `SELECT` list, `HAVING` and `ORDER BY` of a command with a `GROUP BY`; a
  missing grouping context — or a call in `WHERE`, a join `ON` or the `GROUP BY` itself — is refused with
  `The GROUPING_ID function requires a GROUP BY context.`

A plain `GROUP BY` reports `0` for every row. `ROLLUP` and `GROUPING SETS` report the subtotal mask: a
set bit per expression aggregated away, with the **last** argument as the least significant bit, so
`grouping_id(grp, value)` over `rollup(grp, value)` is `0` for a detail row, `1` for the `grp` subtotal and
`3` for the grand total. There is no version floor.

### `approx_percentile_cont` and `approx_percentile_disc`

`approx_percentile_cont(fraction, value)` returns `double?` (`float(53)`) and interpolates;
`approx_percentile_disc(fraction, value)` returns the sort input type (`T?`, for example `int?` for an
`int` column) and returns an input value. NULL rows are skipped, and an empty or all-NULL group yields
`NULL` for both. The spelling is `WITHIN GROUP (ORDER BY value)` and the endpoints `0` and `1` are exact.

The fraction is a `double` in `[0, 1]`, never a 0–100 percentage, and it must be query-independent: a
literal or a captured scalar. A fraction that depends on a row (`(double)e.Int`) is refused when the SQL
is generated with `The fraction for approx_percentile_disc on SQL Server must be a constant or a captured
scalar.`, and a literal or captured value that is `NaN`, infinite or outside `[0, 1]` is refused with
`The fraction for approx_percentile_cont on SQL Server must be finite and between 0 and 1 inclusive.`
A runtime query-parameter fraction renders as a parameter; the engine then enforces the same range
(Msg 8727).

Neither member has an `OVER` overload: they are ordered-set aggregates, `WITHIN GROUP` only, so an
`OVER` form cannot be written through the public surface. If an `OVER` shape is constructed by other
means, the engine rejects it with Msg 4113 (`The function 'approx_percentile_cont' is not a valid
windowing function, and cannot be used with the OVER clause.`).

The `ORDER BY` key must be a column, a parameter or a computed expression. SQL Server answers Msg 5309
(`Windowed functions, aggregates and NEXT VALUE FOR functions do not support constants as ORDER BY
clause expressions.`) for a literal or a NULL key — a typed `CAST(NULL AS int)` is still a constant — so
such a call is refused when the SQL is generated, while a captured scalar becomes a parameter and
executes.

### Approximate results and their caveat

`approx_count_distinct` and both approximate percentiles are statistical estimates: the vendor documents
a maximum rank error of about **1.33 % at 99 % confidence** for the percentiles, and the distinct count is
likewise an estimate. That is a vendor statistical bound, **not** a deterministic tolerance this library
promises — consecutive runs may return different values. Tests therefore assert the CLR type, a value
inside the input bounds and the NULL/empty behaviour, never equality with one expected number.
`checksum_agg` and `grouping_id` are exact.

## Aggregate FILTER

`count`/`count_big`/`min`/`max`/`avg`/`sum` and the string/array aggregates accept an extra
`Expression<Func<bool>>` argument that filters the rows the aggregate sees. The filter predicate is a
full query predicate and may reference columns and parameters. The spelling is selected by
[`AggregateFilterStyle`](xref:NextORM.Core.ISqlDialect.AggregateFilterStyle) — PostgreSQL and SQLite
render the ANSI `filter (where ...)` clause, ClickHouse renders its `-If` combinator
(`countIf`/`sumIf`/...) and MySQL/MariaDB and SQL Server reject the call. On PostgreSQL the ANSI clause
requires server version 9.4 or later; a dialect configured with an older `Version` rejects the call with
`NotSupportedException`, while an unset version assumes 9.4+ (see
[PostgreSQL provider](../connecting-providers/postgres.md#server-version)):

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Select(e => new
    {
        e.Int,
        Big = SqlFunctions.Sql.count(() => e.Id > 10L),
        Total = SqlFunctions.Sql.sum(e.Id, () => e.Boolean == true)
    })
    .ToList();
```

```sql
select nullableint, count(*) filter (where (id > 10)) as "Big", sum(id) filter (where (b = true)) as "Total"
 from complex_entity group by nullableint
```

On ClickHouse the same query renders `toInt32(countIf((id > 10)))` and `sumIf(id, (b = true))`.

MariaDB explicitly refuses `GROUPING` in this catalogue; see the
[MariaDB catalogue](08-reference.md#mariadb-catalogue).

## Median and percentiles

The four names are not portable and are deliberately not interchangeable: `percentile_cont` interpolates,
`percentile_disc` returns an input value, `median` is the fixed `0.5` case, and the generic `percentile`
member is refused everywhere instead of aliasing the continuous form.

| C# | SQL | Providers |
|---|---|---|
| `SqlFunctions.Postgres.percentile_cont(fraction, () => x)` | `percentile_cont(f) within group (order by x)` (`double precision`) | PostgreSQL: ordered-set aggregate, one row per group; floating-point sort only |
| `SqlFunctions.Postgres.percentile_disc(fraction, () => x)` | `percentile_disc(f) within group (order by x)` (keeps the sort expression's type) | PostgreSQL: ordered-set aggregate |
| `SqlFunctions.Postgres.median(() => x)` | `percentile_cont(0.5) within group (order by x)` | PostgreSQL: aggregate, one row per group |
| `SqlFunctions.Sql.percentile_cont(fraction, x).Over(...)`, `percentile_disc(fraction, x).Over(...)` | `percentile_cont(f) within group (order by x) over (...)` | SQL Server (`float(53)`), MariaDB (`double(18,10)`): analytic, one row per input row, partition-only `OVER` |
| `SqlFunctions.SqlServer.median(x).Over(...)`, `SqlFunctions.MySql.median(x).Over(...)` | `percentile_cont(0.5) within group (order by x) over (...)` | SQL Server, MariaDB: analytic, one row per input row, partition-only `OVER` |
| `SqlFunctions.ClickHouse.median(x)` | `toFloat64(median(x))` | ClickHouse: aggregate over the `quantile` family (approximate) |
| generic `percentile(fraction, value)` | `NotSupportedException` | every provider |

A fraction is a `double` in `[0, 1]`, never a 0–100 percentage; it must be finite and constant (a literal or
a captured variable), and a row-dependent fraction is refused when the SQL is generated. SQLite refuses
`median` and every percentile (the shipped build has no `median` aggregate and reports
`SQLITE_ENABLE_PERCENTILE=0`, and nextorm provides no emulation), MySQL refuses all four (there is no
`MEDIAN(…)` and no `PERCENTILE_*`), and ClickHouse refuses the percentile names: its `median` is the native
`quantile`-family aggregate, and no `quantile*` alias is created for the portable spellings. The analytic
forms must be completed with `Over(...)`, and a window `ORDER BY` or a frame is refused.

## Set-returning helpers (PostgreSQL)

`SqlFunctions.Postgres.generate_series` and `SqlFunctions.Postgres.unnest` are pre-declared
[`TvfEntityBuilder<T>`](../guide/13-table-valued-functions.md) runtime factories, so no custom factory is needed:

```csharp
var numbers = dataContext
    .From(() => SqlFunctions.Postgres.generate_series(1L, 3L))
    .Select(r => r.Value)
    .ToList();

var elements = dataContext
    .From(() => SqlFunctions.Postgres.unnest(SqlFunctions.Parameter<long[]>(0)))
    .Select(r => r.Value)
    .ToList(new long[] { 1, 2, 3 });
```

`generate_series` selects [`Value`](xref:NextORM.Core.SqlFunctions.IGenerateSeriesRow.Value) and `unnest` selects
[`Value`](xref:NextORM.Core.SqlFunctions.IUnnestRow`1.Value); both map to the single column the function returns.

## ClickHouse aggregate catalogue (#286, T286b)

The residual ClickHouse aggregate catalogue is typed on `ClickHouseFunctions` and dispatched by
`AdvancedAggregateTranslator` over the ClickHouse-only `IClickHouseAggregateRenderer` capability (null on
every other provider, so a call fails closed before SQL). Parametric aggregates use the vendor
two-parenthesis grammar — the accuracy/level/limit parameters are the leading CLR arguments and the value
arguments form the second pair — and the result is normalised to the declared CLR shape (`toInt64`,
`toInt32`, `toFloat64`, or a composite cast for `Array`/`Tuple` results).

| Group | Members |
|---|---|
| Statistical | `entropy`, `simple_linear_regression`, `delta_sum`, `delta_sum_timestamp`, `gini`, `skew_pop`, `skew_samp`, `kurt_pop`, `kurt_samp`, `rank_corr`, `contingency`, `cramers_v`, `cramers_v_bias_corrected`, `theils_u`, `welch_t_test`, `mann_whitney_u_test`, `kolmogorov_smirnov_test`, `mean_z_test`, `analysis_of_variance`, `student_t_test`, `student_t_test_one_sample` |
| Quantiles | `quantile_tdigest`, `quantile_gk`, `quantile_dd`, `quantile_bfloat16`, `quantile_prometheus_histogram`, `quantiles`, `quantiles_exact`, `quantiles_exact_low`, `quantiles_exact_high`, `quantiles_exact_weighted`, `quantiles_timing`, `quantiles_tdigest`, `quantiles_bfloat16`, `quantiles_dd`, `quantiles_gk`, `quantiles_prometheus_histogram`, `uniq_up_to` |
| Top-K and maps | `approx_top_k`, `approx_top_sum`, `any_heavy`, `max_map`, `min_map` |
| Group-array family | `group_array`, `group_array_moving_avg`, `group_array_moving_sum`, `group_array_sample`, `group_array_sorted`, `group_array_last`, `group_array_insert_at`, `group_array_intersect`, `group_concat` |
| Counting and sums | `sum_count`, `sum_kahan`, `histogram`, `sparkbar`, `group_format`, `max_intersections` (+`max_intersections_position`) |
| Row picking | `first_value`, `last_value` (aggregate forms; the window forms stay on `CommonFunctions`) |
| Sequences | `sequence_count`, `sequence_match_events`, `sequence_next_node` |
| Time series | `time_series_group_sum`, `time_series_group_rate_sum`, `time_series_instant_rate_to_grid`, `time_series_rate_to_grid`, `time_series_resample_to_grid_with_staleness`; window-only `exponential_time_decayed_sum`/`_max`/`_count`/`_avg` (complete them with `Over(...)`) |

Version facts are pinned, not hidden: `gini`, `student_t_test_one_sample`, `group_format` and
`quantile_prometheus_histogram` were introduced after 25.8 and `time_series_group_sum`/
`time_series_group_rate_sum` were removed, so their arms render the vendor name and the live test pins
`UNKNOWN_FUNCTION`. `sequence_next_node` needs `allow_experimental_funnel_functions` and the time-series
grids need `allow_experimental_time_series_aggregate_functions`. Three members stay refused
(`stochastic_linear_regression`, `stochastic_logistic_regression`, `exponential_moving_average`: opaque
`AggregateFunction` states) — see
[ClickHouse-specific SQL › Not yet supported](../guide/provider-specific-sql/clickhouse.md#not-yet-supported).
