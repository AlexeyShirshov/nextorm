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
[Arrays (ClickHouse)](../../scalar-functions/06-arrays.md#arrays-clickhouse); the array functions are gated by
`SupportsArrayFunctions`, the clause by `ArrayJoinClause`.

## `LIMIT n BY expr`

[`LimitBy`](xref:NextORM.Core.EntityBuilder`1.LimitBy``1(System.Int32,System.Int32,System.Linq.Expressions.Expression{System.Func{`0,``0}})) returns the first `n` rows **per distinct key**, emitted
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
([`LimitBy`](xref:NextORM.Core.ISqlDialect.LimitBy)).

## `GROUP BY ... WITH TOTALS`

[`WithTotals`](xref:NextORM.Core.EntityBuilder`1.WithTotals) appends the ClickHouse `with totals` modifier to a
grouping, adding a totals row for the whole result set. It is orthogonal to `ROLLUP`/`CUBE` and cannot
be combined with `GROUPING SETS`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupBy(x => x.Int)
    .Select(x => new { x.Int, Count = SqlFunctions.Sql.count() })
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
`SEMI`/`ANTI`/`PASTE` have dedicated builders: `SemiJoin`/`AntiJoin` return only the left-hand columns
for left rows that do (respectively do not) have a match, and `PasteJoin` pairs the two sources by row
position with no `ON` ([`SupportsSemiAntiJoin`](xref:NextORM.Core.ISqlDialect.SupportsSemiAntiJoin)/`SupportsPasteJoin`).

## `GLOBAL IN`

[`SqlFunctions.ClickHouse.global_in`](xref:NextORM.Core.ClickHouseFunctions.global_in``1(``0,NextORM.Core.QueryCommand{``0})) renders the distributed
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
and are rejected by every other dialect. Their native result types (`UInt64`, `Float32`, the `-If`
integer types) do not always match the declared CLR return type, so the dialect wraps them in
`toInt64(...)`/`toInt32(...)`/`toFloat64(...)` casts that normalise the value to the type the method
declares:

* distinct-count aggregates `uniq`/`uniq_exact`/`uniq_combined`/`uniq_hll12` (`toInt64`);
* parameterised `quantile(level)(value)`/`quantile_exact`/`quantile_timing` and `median` (`toFloat64`), multi-level `quantiles(level1, level2, ...)(value)` (returns `Array(Float64)`, materialised as a CLR `double[]`; the levels must be an inline array);
* the most-frequent `topK(k)(value)`/`topKWeighted(k)(value, weight)` aggregates (return `Array(T)`, materialised as a CLR `T[]`);
* the last-row arbitrary-value aggregate `any_last` (`anyLast`);
* the array-returning aggregates `group_array`/`group_uniq_array` (`groupArray`/`groupUniqArray`, materialised as a CLR `T[]`; `groupArray` of an array column yields a nested `T[][]`);
* the sequence/funnel aggregates `window_funnel`/`sequence_match`/`retention` (`windowFunnel`/`sequenceMatch` with `toInt32(...)`; `retention` returns an array, projected directly);
* the shared filtered-aggregate API rendered as the `-If` combinators `countIf`/`sumIf`/`avgIf`/`minIf`/`maxIf`;
* `arg_min`/`arg_max`;
* the bitmap aggregates `group_bitmap`/`group_bitmap_and`/`group_bitmap_or`/`group_bitmap_xor`
  (`groupBitmap`/`groupBitmapAnd`/`groupBitmapOr`/`groupBitmapXor`; the opaque `UInt64` state) and the
  per-key `sum_map`/`sum_map_filtered` (`sumMap`/`sumMapFiltered(keys)(key, value)`, surfaced as a
  `Map`).

Portable aggregates — including `count`, the arbitrary-value `any_agg` (rendered `ANY_VALUE` on MySQL and
MariaDB) and `corr`/`covar*` — are documented with the concept page, not here. A plain `UInt64` column
(or any `ulong`/`ulong?` projection) materialises directly through the row reader's
`DbDataReader.GetFieldValue<ulong>` accessor, so only the function results above need the normalising
cast.

See [Grouping and aggregates](../04-grouping-and-aggregates.md).

## Native scalar functions, maps and hashes

Beyond the cross-provider scalar surface, `ClickHouseFunctions` exposes the native ClickHouse
functions that have no portable spelling. They render the exact camel-case name and are rejected by
every other dialect:

* UTF-8 case, trim and regexp/search strings: `lower_utf8`/`upper_utf8` (`lowerUTF8`/`upperUTF8`),
  `trim_left`/`trim_right`/`trim_both` (`trimLeft`/`trimRight`/`trimBoth`, with an optional character
  set), `replace_regexp_one`/`replace_regexp_all`, `match`, `extract`, `extract_all` (projects as
  `string[]`) and `split_by_string`/`split_by_regexp`/`split_by_whitespace`;
* date and time: `format_date_time` (`formatDateTime`, optional time zone),
  `parse_date_time`/`parse_date_time_best_effort` (`parseDateTime`/`parseDateTimeBestEffort`) and the
  current-date helpers `now`/`today`/`yesterday`;
* array set operations: `array_concat`, `array_flatten`, `array_uniq` (`arrayUniq`, wrapped in
  `toInt64`), `array_intersect`, `array_union`, `array_except`, `array_symmetric_difference`;
* the `Map` family: the `map` constructor (inline `Tuple.Create` pairs), `map_keys`/`map_values`,
  `map_contains_key`/`map_contains_value`, `map_add`/`map_concat`, the two-parameter lambdas
  `map_filter`/`map_apply`/`map_all`/`map_exists` and `map_sort`;
* hashes: `md5`/`sha1`/`sha256`/`sha512` (`MD5`/`SHA1`/`SHA256`/`SHA512`, fixed binary strings),
  `xx_hash32`/`xx_hash64`/`xxh3`, `city_hash64`, `sip_hash64`/`sip_hash128` and
  `murmur_hash2_32`/`murmur_hash2_64`/`murmur_hash3_32`/`murmur_hash3_64`/`murmur_hash3_128`;
* `generate_ulid` (`generateULID`).

A `Map` result is surfaced as `IDictionary<TKey, TValue>`; combine it with `map_keys`/`map_values` or
one of the contains predicates when it is projected, because the row mapper does not materialise a
bare `IDictionary` column.

```csharp
var rows = dataContext.From<Event>()
    .Select(e => new
    {
        HasMatch = SqlFunctions.ClickHouse.match(e.Name, "error"),
        Keys = SqlFunctions.ClickHouse.map_keys(SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L))),
        NewId = SqlFunctions.ClickHouse.generate_ulid()
    })
    .ToList();
```

See [Scalar functions](../../scalar-functions/index.md).

## JSON, dictionaries and array functions

* string-JSON extractors `JSONExtractString`/`JSONExtractInt`/`JSONExtractFloat`/`JSONExtractBool`/
  `JSONExtractRaw`/`JSONHas`/`JSONType`, `json_length`, and the flat-JSON `visitParamExtract*`;
* array-returning `JSONExtractKeys`/`JSONExtractArrayRaw` (`json_extract_keys`/`json_extract_array_raw`,
  projecting as `string[]`) and `JSONExtractKeysAndValues` (`json_extract_keys_and_values<T>`, projecting
  as `Tuple<string, T>[]`; `T` must be a non-nullable ClickHouse type);
* JSONPath scalars `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS` (over string JSON);
* native-JSON functions `json_all_paths`/`json_all_paths_with_types` (`JSONAllPaths`/
  `JSONAllPathsWithTypes`; the former projects as `string[]`, the latter's native `Map(String, String)`
  as `Dictionary<string, string>`; both take a native `JSON` value) and `to_json_string`
  (`toJSONString`);
* dictionary lookups `dict_get`/`dict_get_or_default`/`dict_has` and the hierarchy
  `dict_get_hierarchy`/`dict_get_children`/`dict_is_in` (need a configured `CREATE DICTIONARY`);
* array functions `length`/`has`/`index_of`/`has_any`/`has_all`/`starts_with`/`ends_with`/`has_substr`/
  `array_string_concat`/`split_by_char`/`array_sort`/`array_reverse`/`array_distinct`/`range`/
  `array_enumerate`/`array_cum_sum`/`array_slice`/`array_push_back`;
* tuple constructor and element access: `System.Tuple.Create(a, b)` renders `tuple(a, b)` and
  `System.Tuple<...>.ItemN` renders `tupleElement(t, n)` (a whole `Tuple(...)` projects as
  `System.Tuple<...>`); `untuple` is not supported.

See [Scalar functions](../../scalar-functions/index.md) and [JSON support](../18-json.md)
([`SupportsJsonExtract`](xref:NextORM.Core.ISqlDialect.SupportsJsonExtract)/[`SupportsDictionaries`](xref:NextORM.Core.ISqlDialect.SupportsDictionaries)).

## Table functions

`numbers`/`numbers_mt` and the row-count generators `zeros`/`zeros_mt` are available through
[`FromTableFunction`](xref:NextORM.Core.DataContextExtensions.FromTableFunction``1(NextORM.Core.IDataContext,System.Linq.Expressions.Expression{System.Func{System.Linq.IQueryable{``0}}})). The `numbers` `UInt64` column is cast to
`Int64` inside a wrapping subquery so it materialises as a CLR `long`; `zeros` materialises directly as
`byte`:

```csharp
var rows = dataContext.FromTableFunction(() => SqlFunctions.ClickHouse.zeros(3))
    .Select(r => new { r.Value })
    .ToList();
```

See [Table-valued functions](../13-table-valued-functions.md).

## Not yet supported

The native `JSON` column type (its reader/type-mapping) and distributed
table functions (`remote`, `cluster`, `s3`, `file`) are out of scope today. See
[Limitations and out-of-scope features](../../advanced/limitations.md).

## See also

* [ClickHouse provider](../../providers/clickhouse.md)
* [Provider-specific SQL](overview.md)

---

Source: `src/nextorm.clickhouse/ClickHouseDialect.cs`, `src/nextorm.core/Query/SqlFunctions.ClickHouse.cs`,
`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs`.
