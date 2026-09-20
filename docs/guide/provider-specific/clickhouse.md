# ClickHouse-specific SQL

> ClickHouse has the largest provider-exclusive surface in nextorm: native arrays and `ARRAY JOIN`, the
> `LIMIT n BY expr` and `GROUP BY ... WITH TOTALS` modifiers, the `FINAL`/`SAMPLE`/`PREWHERE`/`SETTINGS`
> query modifiers, join strictness and `GLOBAL JOIN`, the distributed `GLOBAL IN` predicate, its own
> aggregate, array, JSON and dictionary functions, and the `numbers`/`zeros` table functions.

**Prerequisites:** [Querying and projections](../01-querying-and-projections.md) · [ClickHouse provider](../../providers/clickhouse.md)

## Arrays and `ARRAY JOIN`

ClickHouse has a native `Array(T)` type. Array functions operate on array columns or nested array
expressions, and the array-join clause expands one row per element:

```csharp
var rows = dataContext.From<IArrayEntity>()
    .LeftArrayJoin(e => e.Tags)
    .Where(e => e.Id > 0)
    .Select(e => new { e.Id })
    .ToList();
```

```sql
select id from array_entity left array join tags where id > 0
```

The scalar `array_join` (one row per element, projectable) and the clause methods
`ArrayJoin`/`LeftArrayJoin`/`ArrayJoinElement`/`LeftArrayJoinElement` are covered in
[Arrays (ClickHouse)](../11-scalar-functions.md#arrays-clickhouse); the array functions are gated by
`SupportsArrayFunctions`, the clause by `SupportsArrayJoinClause`.

## `LIMIT n BY expr`

[`LimitBy`](xref:NextORM.Core.EntityBuilder`1) returns the first `n` rows **per distinct key**, emitted
after `ORDER BY` and before the final `LIMIT`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .OrderBy(x => x.Id)
    .LimitBy(2, x => x.Int)
    .Select(x => new { x.Id, x.Int })
    .ToList();
```

```sql
select id, nullableint from complex_entity order by id limit 2 by nullableint
```

See [Sorting and paging](../05-sorting-and-paging.md#limit-by-clickhouse)
([`SupportsLimitBy`](xref:NextORM.Core.ISqlDialect.SupportsLimitBy)).

## `GROUP BY ... WITH TOTALS`

[`WithTotals`](xref:NextORM.Core.EntityBuilder`1) appends the ClickHouse `with totals` modifier to a
grouping, adding a totals row for the whole result set. It is orthogonal to `ROLLUP`/`CUBE` and cannot
be combined with `GROUPING SETS`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupBy(x => x.Int)
    .Select(x => new { x.Key, Count = SqlFunctions.Sql.count() })
    .WithTotals()
    .ToList();
```

```sql
select nullableint, count(*) from complex_entity group by nullableint with totals
```

See [Grouping and aggregates](../04-grouping-and-aggregates.md#with-totals)
([`SupportsGroupByWithTotals`](xref:NextORM.Core.ISqlDialect.SupportsGroupByWithTotals)). The official
`ClickHouse.Driver` returns the totals block separately and does not surface it through `IDataReader`, so
only the group rows are materialised.

## Query modifiers: `FINAL`, `SAMPLE`, `PREWHERE`, `SETTINGS`

Four builder methods render ClickHouse query-level modifiers: `Final()`, `Sample(ratio[, offset])`,
`PreWhere(predicate)` and `Settings(("key", "value"), ...)`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Final()
    .PreWhere(c => c.Int > 0)
    .Settings(("max_threads", "2"))
    .Select(c => new { c.Id })
    .ToList();
```

```sql
select id from complex_entity final prewhere (nullableint > 0) settings max_threads = 2
```

See [Query hints](../17-query-hints.md#clickhouse-query-modifiers)
([`SupportsFinal`](xref:NextORM.Core.ISqlDialect.SupportsFinal)/[`SupportsSample`](xref:NextORM.Core.ISqlDialect.SupportsSample)/[`SupportsPreWhere`](xref:NextORM.Core.ISqlDialect.SupportsPreWhere)/[`SupportsSettings`](xref:NextORM.Core.ISqlDialect.SupportsSettings)).
`FINAL`/`PREWHERE` need a table engine that supports them — the `Memory` engine rejects both.

## Join strictness and `GLOBAL JOIN`

ClickHouse join modifiers are applied to the join that was just added:
`WithStrictness(JoinStrictness.Any | All | Asof)` and `Global()`:

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .LeftJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Global()
    .WithStrictness(JoinStrictness.Any)
    .Select(p => new { p.Item1.Id, p.Item2.String })
    .ToList();
```

```sql
select t1.id, t2.somestring from simple_entity as `t1` global left any join complex_entity as `t2` on cast(t1.id as bigint) = t2.id
```

See [Joins](../03-joins.md#provider-specific-join-modifiers-clickhouse)
([`SupportsJoinStrictness`](xref:NextORM.Core.ISqlDialect.SupportsJoinStrictness)/[`SupportsGlobalJoin`](xref:NextORM.Core.ISqlDialect.SupportsGlobalJoin)).
`ANY` keeps one right-hand row per left-hand row, `ALL` keeps every match and `ASOF` needs one equi-join
column plus a final inequality; `GLOBAL` broadcasts the right side for distributed queries.
`SEMI`/`ANTI`/`PASTE` joins are not supported.

## `GLOBAL IN`

[`SqlFunctions.ClickHouse.global_in`](xref:NextORM.Core.ClickHouseFunctions) renders the distributed
predicate over a subquery or a value list; negate with `!` for `GLOBAL NOT IN`:

```csharp
var values = new int[] { 1, 3 };
var ids = dataContext.From<ISimpleEntity>()
    .Where(x => SqlFunctions.ClickHouse.global_in(x.Id, values))
    .Select(x => new { x.Id })
    .ToList();
```

```sql
select id from simple_entity where global in (@p0, @p1)
```

See [Filtering](../02-filtering-where.md#global-in-clickhouse)
([`SupportsGlobalPredicates`](xref:NextORM.Core.ISqlDialect.SupportsGlobalPredicates)).

## Aggregates

ClickHouse-exclusive aggregates live on [`ClickHouseFunctions`](xref:NextORM.Core.ClickHouseFunctions)
and are rejected by every other dialect. Some return types the row reader cannot materialise (`UInt64`,
`Float32`), so the dialect wraps them in `toInt64(...)`/`toInt32(...)`/`toFloat64(...)` casts:

* distinct-count aggregates `uniq`/`uniq_exact`/`uniq_combined`/`uniq_hll12` (`toInt64`);
* parameterised `quantile(level)(value)`/`quantile_exact`/`quantile_timing` and `median` (`toFloat64`);
* the last-row arbitrary-value aggregate `any_last` (`anyLast`);
* the sequence/funnel aggregates `window_funnel`/`sequence_match`/`retention` (`windowFunnel`/`sequenceMatch` with `toInt32(...)`; `retention` returns an array, usable only nested);
* the `-If` combinators `count_if`/`sum_if`/`avg_if`/`min_if`/`max_if`;
* `arg_min`/`arg_max`.

Portable aggregates — including `count`, the arbitrary-value `any_agg` (rendered `ANY_VALUE` on MySQL and
MariaDB) and `corr`/`covar*` — are documented with the concept page, not here.

See [Grouping and aggregates](../04-grouping-and-aggregates.md).

## JSON, dictionaries and array functions

* string-JSON extractors `JSONExtractString`/`JSONExtractInt`/`JSONExtractFloat`/`JSONExtractBool`/
  `JSONExtractRaw`/`JSONHas`/`JSONType`, `json_length`, and the flat-JSON `visitParamExtract*`;
* JSONPath scalars `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS` (over string JSON);
* dictionary lookups `dict_get`/`dict_get_or_default`/`dict_has` (need a configured `CREATE DICTIONARY`);
* array functions `length`/`has`/`index_of`/`has_any`/`has_all`/`array_string_concat`/`split_by_char`/
  `array_sort`/`array_reverse`/`array_distinct`/`range`/`array_enumerate`/`array_cum_sum`/`array_slice`/
  `array_push_back`.

See [Scalar functions](../11-scalar-functions.md) and [JSON support](../18-json.md)
([`SupportsJsonExtract`](xref:NextORM.Core.ISqlDialect.SupportsJsonExtract)/[`SupportsDictionaries`](xref:NextORM.Core.ISqlDialect.SupportsDictionaries)).

## Table functions

`numbers`/`numbers_mt` and the row-count generators `zeros`/`zeros_mt` are available through
[`FromTableFunction`](xref:NextORM.Core.EntityBuilder`1). The `numbers` `UInt64` column is cast to
`Int64` inside a wrapping subquery so it materialises as a CLR `long`; `zeros` materialises directly as
`byte`:

```csharp
var rows = dataContext.FromTableFunction(() => SqlFunctions.ClickHouse.zeros(3))
    .Select(r => new { r.Value })
    .ToList();
```

See [Table-valued functions](../13-table-valued-functions.md).

## Not yet supported

`SEMI`/`ANTI`/`PASTE` joins, higher-order array functions (`arrayMap`/`arrayFilter`), array/tuple row
readers, the native `JSON` type, and distributed table functions (`remote`, `cluster`, `s3`, `file`)
are out of scope today. See [Limitations and out-of-scope features](../../advanced/limitations.md).

## See also

* [ClickHouse provider](../../providers/clickhouse.md)
* [Provider-specific SQL](overview.md)

---

Source: `src/nextorm.clickhouse/ClickHouseDialect.cs`, `src/nextorm.core/Query/SqlFunctions.ClickHouse.cs`,
`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs`.
