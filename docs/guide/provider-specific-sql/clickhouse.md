# ClickHouse-specific SQL

> ClickHouse has the largest provider-exclusive surface in nextorm: native arrays and `ARRAY JOIN`, the
> `LIMIT n BY expr` and `GROUP BY ... WITH TOTALS` modifiers, the `FINAL`/`SAMPLE`/`PREWHERE`/`SETTINGS`
> query modifiers, join strictness and `GLOBAL JOIN`, the distributed `GLOBAL IN` predicate, its own
> aggregate, array, JSON and dictionary functions, and the `numbers`/`zeros` table functions.

**Prerequisites:** [Querying and projections](../../engine/index.md) · [ClickHouse provider](../../connecting-providers/clickhouse.md)

> **Extension methods.** Every ClickHouse-only member on this page (`ArrayJoin`/`LeftArrayJoin`/
> `ArrayJoinElement`/`LeftArrayJoinElement`, `LimitBy`, `WithTotals`, `Final`, `PreWhere`, `Settings`,
> `Sample`, `SemiJoin`/`AntiJoin`/`PasteJoin` and `j => j.Global()`) is an extension method from the
> `nextorm.clickhouse` package, not part of the common `nextorm` API. Add `using NextORM.ClickHouse;`
> and the examples compile unchanged.

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
select id
 from array_entity
 left array join tags where id > 0
```

The scalar `array_join` (one row per element, projectable) and the clause methods
`ArrayJoin`/`LeftArrayJoin`/`ArrayJoinElement`/`LeftArrayJoinElement` are covered in
[Arrays (ClickHouse)](../../scalar-functions/06-arrays.md#arrays-clickhouse); the array functions are gated by
`SupportsArrayFunctions`, the clause by `ArrayJoinClause`.

## `LIMIT n BY expr`

[`LimitBy`](xref:NextORM.ClickHouse.ClickHouseEntityBuilderExtensions.LimitBy``2(NextORM.Core.EntityBuilder{``0},System.Int32,System.Int32,System.Linq.Expressions.Expression{System.Func{``0,``1}})) returns the first `n` rows **per distinct key**, emitted
after `ORDER BY` and before the final `LIMIT`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .OrderBy(x => x.Id)
    .LimitBy(2, x => x.Int)
    .Select(x => new { x.Id, x.Int })
    .ToList();
```

```sql
select id, nullableint
 from complex_entity order by id limit 2 by nullableint
```

See [Sorting and paging](../06-sorting-and-paging.md#limit-by-clickhouse)
([`LimitBy`](xref:NextORM.Core.ISqlDialect.LimitBy)).

## `GROUP BY ... WITH TOTALS`

[`WithTotals`](xref:NextORM.ClickHouse.ClickHouseEntityBuilderExtensions.WithTotals``1(NextORM.Core.EntityBuilder{``0})) appends the ClickHouse `with totals` modifier to a
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
select nullableint, count(*)
 from complex_entity group by nullableint with totals
```

See [Grouping and aggregates](../05-grouping-and-aggregates.md#with-totals)
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
select id
 from complex_entity final prewhere (nullableint > 0) settings max_threads = 2
```

See [Query hints](../15-query-hints.md#clickhouse-query-modifiers)
([`SupportsFinal`](xref:NextORM.Core.ISqlDialect.SupportsFinal)/[`SupportsSample`](xref:NextORM.Core.ISqlDialect.SupportsSample)/[`SupportsPreWhere`](xref:NextORM.Core.ISqlDialect.SupportsPreWhere)/[`SupportsSettings`](xref:NextORM.Core.ISqlDialect.SupportsSettings)).
`FINAL`/`PREWHERE` need a table engine that supports them — the `Memory` engine rejects both.

## Join strictness and `GLOBAL JOIN`

ClickHouse join modifiers are passed to the join through its trailing lambda:
`j => j.WithStrictness(JoinStrictness.Any | All | Asof)` and `j => j.Global()`:

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .LeftJoin<IComplexEntity>((s, c) => s.Id == c.Id,
        j => j.Global().WithStrictness(JoinStrictness.Any))
    .Select(p => new { p.Item1.Id, p.Item2.String })
    .ToList();
```

```sql
select t1.id, t2.somestring
 from simple_entity as `t1`
 global left any join complex_entity as `t2` on cast(t1.id as bigint) = t2.id
```

See [Joins](../04-joins.md#provider-specific-join-modifiers-clickhouse)
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
select id
 from simple_entity where global in (@p0, @p1)
```

See [Filtering](../03-filtering-where.md#global-in-clickhouse)
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
* the sequence/funnel aggregates `window_funnel`/`sequence_match`/`retention` (`windowFunnel`/`sequenceMatch` with `toInt32(...)`; `retention` is normalised to the declared `int[]`);
* the shared filtered-aggregate API rendered as the `-If` combinators `countIf`/`sumIf`/`avgIf`/`minIf`/`maxIf`;
* `arg_min`/`arg_max`;
* the bitmap aggregates `group_bitmap`/`group_bitmap_and`/`group_bitmap_or`/`group_bitmap_xor`
  (`groupBitmap`/`groupBitmapAnd`/`groupBitmapOr`/`groupBitmapXor`; the opaque `UInt64` state) and the
  per-key `sum_map`/`sum_map_filtered` (`sumMap`/`sumMapFiltered(keys)(key, value)`, surfaced as a
  `Map`).

Array-returning facades declare a closed CLR array type and the dialect normalises the server result to it
with exactly one outer `cast(... as Array(...))`: `top_k`, `top_k_weighted`, `group_array`,
`group_uniq_array` (a nullable input declares a nullable element type, `int?[]` →
`Array(Nullable(Int32))`), `retention` (`int[]` → `Array(Int32)`, never `byte[]`), `array_map` and all
three `range` overloads (`long[]` → `Array(Int64)`). This is a type correction of the rendered call only:
it changes neither the lambda translation nor the operand rendering, so the higher-order lambda
limitations described below still apply.

Portable aggregates — including `count`, the arbitrary-value `any_agg` (rendered `ANY_VALUE` on MySQL and
MariaDB) and `corr`/`covar*` — are documented with the concept page, not here. A plain `UInt64` column
(or any `ulong`/`ulong?` projection) materialises directly through the row reader's
`DbDataReader.GetFieldValue<ulong>` accessor, so only the function results above need the normalising
cast.

See [Grouping and aggregates](../05-grouping-and-aggregates.md).

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
  `toInt64`), `array_intersect`, `array_union`, `array_symmetric_difference`;
* the `Map` family: the `map` constructor (inline `Tuple.Create` pairs), `map_keys`/`map_values`,
  `map_contains_key`/`map_contains_value`, `map_add`/`map_concat`, and the public higher-order
  helpers `map_filter`/`map_apply`/`map_sort` (tuple and array results, lowered to array forms) plus
  `map_all`/`map_exists` (booleans, native predicates);
* hashes: the six string-returning digests `md5`/`sha1`/`sha256`/`sha512`/`sip_hash128`/
  `murmur_hash3_128` (`MD5`/`SHA1`/`SHA256`/`SHA512`/`sipHash128`/`murmurHash3_128`, lowercase
  hexadecimal) plus the numeric `xx_hash32`/`xx_hash64`/`xxh3`, `city_hash64`, `sip_hash64` and
  `murmur_hash2_32`/`murmur_hash2_64`/`murmur_hash3_32`/`murmur_hash3_64`;
* math: `log2`, `cbrt`, `exp2` and `exp10` (rendered `log2(x)`/`cbrt(x)`/`exp2(x)`/`exp10(x)`), each
  with a `double` and a `double?` overload returning `Float64`/`Nullable(Float64)` (a `NULL` argument
  stays `NULL`); the other providers and the in-memory provider reject them;
* `generate_ulid` (`generateULID`).

The six string-returning digests are encoded server-side as lowercase hexadecimal, so a `string?`
projection receives the digest text rather than the raw `FixedString` bytes (which the driver would
decode lossily as UTF-8):

| Function | Rendered SQL | Characters |
|---|---|---:|
| `md5` | `lower(hex(MD5(value)))` | 32 |
| `sha1` | `lower(hex(SHA1(value)))` | 40 |
| `sha256` | `lower(hex(SHA256(value)))` | 64 |
| `sha512` | `lower(hex(SHA512(value)))` | 128 |
| `sip_hash128` | `lower(hex(sipHash128(value)))` | 32 |
| `murmur_hash3_128` | `lower(hex(murmurHash3_128(value)))` | 32 |

Every character is in `[0-9a-f]`. A `NULL` argument propagates as `NULL` — it is never replaced by an
empty string or a default digest — and the value survives ordinary string materialisation unchanged
through an anonymous projection, a typed projection and `Scalar<string>` (a plain `string`, not a byte
array).

`array_except` keeps its place in `SqlFunctions.ClickHouse` for API compatibility but is **not
supported** on the pinned 25.8 baseline: the native `arrayExcept` was introduced in ClickHouse 25.9,
so the call is refused with `NotSupportedException` at translation time and never reaches the server.
The other array set operations are unaffected.

A native `Map` result (the `map` constructor, `map_add`, `map_concat`) is surfaced as
`IDictionary<TKey, TValue>`; combine it with `map_keys`/`map_values` or one of the contains
predicates when it is projected, because the row mapper does not materialise a bare `IDictionary`
column.

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

The five higher-order helpers are public LINQ helpers whose declared CLR result type is part of the
contract; only `map_all` and `map_exists` keep a native server function:

* `map_filter` — the entries matching the predicate, as `Tuple<TKey, TValue>[]`. Lowered to
  `arrayFilter(_t -> predicate, arrayZip(mapKeys(map), mapValues(map)))`, with the lambda's key and
  value rebound to the pair fields `_t.1`/`_t.2`;
* `map_apply` — one lambda result per entry, in map order, as `TResult[]`. Lowered to
  `arrayMap((k, v) -> expression, mapKeys(map), mapValues(map))`;
* `map_sort` — the entries ordered by key, as `Tuple<TKey, TValue>[]`. Lowered to
  `arraySort(_t -> _t.1, arrayZip(mapKeys(map), mapValues(map)))`;
* `map_all`/`map_exists` — `bool`. Kept native and rendered as
  `mapAll((k, v) -> predicate, map)` / `mapExists((k, v) -> predicate, map)`, lambda first.

The lowered filter, apply and sort expression is wrapped in a cast of the **declared** result type, not
of the type the server would infer from the operand — `Tuple<string, int>[]` becomes
`cast(... as Array(Tuple(String, Int32)))`, a `long` apply result becomes
`cast(... as Array(Int64))` — so the server reports exactly the element type the declaration promises
instead of a narrower one. Two rules decide that element:

* a **value-type** apply result stays bare — `int` → `Array(Int32)`;
* a **reference-type** apply result is spelled `Nullable(...)` — `string` → `Array(Nullable(String))` —
  because the declared element admits `null` (the `default(string)` branch of a projection is a real
  `NULL`) while ClickHouse `String` does not; the materialised CLR type stays `string[]`.

The key/value tuple components of `map_filter`/`map_sort` stay bare unless the CLR component itself is
`Nullable<T>`, because the map keys and values are non-nullable.

```sql
-- map_filter: arrayFilter over the zipped pairs, then the declared-type cast
cast(arrayFilter(_t -> (_t.2 > 0),
                arrayZip(mapKeys(map('a', 1, 'b', -1)), mapValues(map('a', 1, 'b', -1))))
    as Array(Tuple(String, Int64)))

-- map_sort: arraySort by key over the same pairs, then the declared-type cast
cast(arraySort(_t -> _t.1,
              arrayZip(mapKeys(map('b', 2, 'a', 1)), mapValues(map('b', 2, 'a', 1))))
    as Array(Tuple(String, Int64)))

-- map_apply: one result per entry, cast as the declared TResult — bare for a value type,
-- Nullable(...) for a reference type that admits null
cast(arrayMap((k, v) -> (v * 2),
              mapKeys(map('a', 1, 'b', -1)), mapValues(map('a', 1, 'b', -1)))
    as Array(Int64))

cast(arrayMap((k, v) -> k,
              mapKeys(map('a', 1, 'b', -1)), mapValues(map('a', 1, 'b', -1)))
    as Array(Nullable(String)))
```

**Breaking change in 1.0.9-rc4.** `map_filter` used to declare `IDictionary<TKey, TValue>`; it now
returns `Tuple<TKey, TValue>[]`, because a map cannot be materialised from a projected expression.
`map_sort` (already a tuple array) and `map_apply` (already `TResult[]`) keep their shapes.

No public path turns a filtered map back into a server-side `Map`: `mapFilter` is only reached through
the `map_*` lowering, `map_keys`/`map_values` require an `IDictionary` and therefore reject the tuple
array at compile time, and the higher-order array lambdas refuse member access on their parameter, so
`arrayMap(t => t.Item2, ...)` cannot be expressed. Migrate by reading the returned tuples — plain LINQ
over the array once it is materialised: `filtered.Select(t => t.Item2)` for the values, or
`filtered.ToDictionary(t => t.Item1, t => t.Item2)` for a dictionary on the client.

The native `Map` surface — `map_keys`/`map_values`, `map_contains_key`/`map_contains_value`,
`map_add`/`map_concat` and the `map` constructor — stays `IDictionary<TKey, TValue>`-based and is
separate from these five helpers, which take a map operand and return arrays or booleans.

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

See [Scalar functions](../../scalar-functions/index.md) and [JSON support](../16-json.md)
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

## Native JSON columns

ClickHouse's native `JSON` column is **object-rooted** (arrays and scalar roots are rejected). A
`[JsonColumn]` property (`Auto` or `Native` storage) maps an object-root POCO, `JsonObject`,
`JsonDocument` or `JsonElement` to it for both read and write; `Text` keeps a `String` column, and a
model that does not serialize to an object fails closed with `NotSupportedException`. Bare object-root
`JsonObject`/`JsonDocument`/`JsonElement` values also map for projection and parameter (the driver
surfaces `JSON` as `JsonObject`, and `JsonDocument`/`JsonElement` are adapted from it). Project through
a named shape (`Select(x => new { x.Doc })` or a DTO): a bare top-level scalar `Select(x => x.Doc)` is
not handled by the core projection classifier. An empty `{}` stays a non-null value, while SQL `NULL`
in a `Nullable(JSON)` column stays distinct from it (`null`, or `default`/`Undefined` for the value-type
`JsonElement`). A bare `string` read over a `JSON` column and the legacy `Object('json')` alias are not
supported; the alias can be created only with a connection-level `allow_experimental_object_type`, which
nextorm never sets, and even then reads materialize as `Tuple<SByte,String>` rather than through the
native `JsonObject` transport. See
[JSON support](../16-json.md#map-a-clr-object-to-a-native-json-column).

## Not yet supported

The server/cluster table functions (`url`, `s3`, `file`, `remote`, `remote_secure`, `cluster`,
`cluster_all_replicas`) **are** pre-declared on `SqlFunctions.ClickHouse` and render their SQL, but
exercising them end-to-end against a real cluster is not covered and stays out of scope. See
[Limitations and out-of-scope features](../../advanced/limitations.md).

## See also

* [ClickHouse provider](../../connecting-providers/clickhouse.md)
* [Provider-specific SQL](overview.md)

---

Source: `src/nextorm.clickhouse/ClickHouseDialect.cs`, `src/nextorm.core/Query/SqlFunctions.ClickHouse.cs`,
`tests/nextorm.clickhouse.tests/SqlGeneration/SqlGenerationTests.cs`.
