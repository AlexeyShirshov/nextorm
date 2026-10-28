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

## PostgreSQL aggregate additions (#281)

The members below extend the PostgreSQL aggregate surface. They are ordinary `pg_proc` aggregates — or,
for the SQL/JSON aggregates and `grouping`, parser-level constructs with no `pg_proc` row — gated per
name, and every other provider, including the in-memory provider, throws `NotSupportedException` at
translation.
`json_agg`, `jsonb_agg`, `json_object_agg` and `jsonb_object_agg` remain documented with the JSON
functions in [JSON and JSONB](07-json-and-xml.md).

### `any_value`

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.any_value(x)` | `any_value(x)` |

`any_value` returns an arbitrary value of the group (`anyelement → anyelement`, PostgreSQL 16+) and both
the argument and the result keep the argument's own type (`integer` stays `integer`). It is the
PostgreSQL `pg_proc` aggregate, not the portable `Sql.any_agg`. Because the result is polymorphic,
nextorm requires a single-column CLR shape — a primitive, string, date/time, decimal, `Guid` or byte
array — and refuses any other `T` before SQL is emitted; project the value with an explicit conversion
when needed. A group whose values are all NULL yields NULL, and empty input still produces one NULL
row.

### Regression sums (`regr_sxx`, `regr_sxy`, `regr_syy`)

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.regr_sxx(y, x)` | `regr_sxx(y, x)` |
| `SqlFunctions.Postgres.regr_sxy(y, x)` | `regr_sxy(y, x)` |
| `SqlFunctions.Postgres.regr_syy(y, x)` | `regr_syy(y, x)` |

Both operands are generic (`TY?`/`TX?`); PostgreSQL's aggregate overloads take `double precision` and
the result is `double precision` (`double?`). `regr_sxx` is
`sum(X^2) - sum(X)^2/N`, `regr_syy` is the same over `Y`, and `regr_sxy` is
`sum(X*Y) - sum(X)*sum(Y)/N`. A pair with a NULL operand is skipped; a group with a single non-NULL
pair yields `0` (measured), and an all-NULL group or empty input yields NULL — empty input still
produces one row.

### `xmlagg`

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.xmlagg(x)` | `xmlagg(cast(x as xml))` |

PostgreSQL has a single `xmlagg(xml) → xml` aggregate: `xmlagg(text)` and `xmlagg(integer)` are both
`42883` "function … does not exist" (measured), so the operand is cast to `xml` and must hold XML
markup the `xml` type accepts. The result is the concatenation of the group's documents with no
separator, and Npgsql materialises the `xml` result as `string` (measured), which is what the member
declares. NULL operands are skipped, a group whose operands are all NULL yields NULL, and empty input
still produces one NULL row.

### Strict and unique JSON aggregates

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.json_agg_strict(x)` | `json_agg_strict(x)` |
| `SqlFunctions.Postgres.jsonb_agg_strict(x)` | `jsonb_agg_strict(x)` |
| `SqlFunctions.Postgres.json_object_agg_unique(k, v)` | `json_object_agg_unique(k, v)` |
| `SqlFunctions.Postgres.json_object_agg_unique_strict(k, v)` | `json_object_agg_unique_strict(k, v)` |

PostgreSQL 16 aggregates. The two array forms ignore NULL values: a group whose values are all NULL
yields `[]` (not an array of nulls, measured), empty input still produces one NULL row, and the
`json`/`jsonb` result is read back as a string. `json_object_agg_unique` keeps a NULL value as JSON
`null`, while `json_object_agg_unique_strict` drops a NULL-valued pair; both raise `22030` on a
duplicate key and `22004` on a NULL key (measured on 17.11), and empty input yields one NULL row.

### SQL/JSON aggregates (`json_arrayagg`, `json_objectagg`)

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.json_arrayagg(x)` | `json_arrayagg(x)` |
| `SqlFunctions.Postgres.jsonb_arrayagg(x)` | `json_arrayagg(x returning jsonb)` |
| `SqlFunctions.Postgres.json_objectagg(k, v)` | `json_objectagg(k value v)` |
| `SqlFunctions.Postgres.json_objectagg_unique(k, v)` | `json_objectagg(k value v with unique keys)` |
| `SqlFunctions.Postgres.jsonb_objectagg(k, v)` | `json_objectagg(k value v returning jsonb)` |

PostgreSQL 17 parser-level constructs with no `pg_proc` row. `json_arrayagg` defaults to
`ABSENT ON NULL`: NULL values are dropped, an all-NULL group yields `[]`, and empty input yields NULL.
`json_objectagg` admits exactly one pair per call (`key value value`; the comma-separated two-pair
form is `42601`) and defaults to `NULL ON NULL` with duplicate keys kept, while `with unique keys`
raises `22030`; a NULL key raises `22004`, and empty input yields NULL. The key is not cast — the
server coerces it to text (measured with an integer key). `jsonb_arrayagg` and `jsonb_objectagg` are
member names for the `RETURNING jsonb` clauses, since PostgreSQL has no functions of those names. The
builder has no clause slot for an aggregate's internal argument list, so `ORDER BY …`,
`ABSENT|NULL ON NULL` and `DISTINCT` are not exposed; the `DISTINCT` spelling is a syntax error on
17.11 (`42601`, measured). Every result is `json`/`jsonb` read back as a string.

### `grouping`

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.grouping(column)` | `grouping(column)` |

One bit of the super-aggregate identity of the current row: `0` when the row groups the column, `1`
when the row is a super-aggregate that aggregated it away. The argument must be an expression of the
query's own grouping list — anything else raises `42803` — and the function is only useful inside a
`GROUP BY ROLLUP`/`CUBE`/`GROUPING SETS` grouping; in a plain `GROUP BY` it is legal and always `0`.
Nextorm's `GroupByRollup(...)` emits that grouping (as do `GroupByCube` and `GroupByGroupingSets`), so
`grouping` can be projected next to the aggregate columns. The zero-argument `grouping()` is a syntax
error (`42601`), and the result is `integer` (`int?`). Measured on 17.11. It returns `0` or `1` for
every row; there is no NULL or empty-input case.

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
