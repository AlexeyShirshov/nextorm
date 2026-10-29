# Arrays (PostgreSQL)

PostgreSQL has native array types. An array operand is always passed as a **single parameter** (the
whole array), never expanded into a value list, so the SQL text does not depend on the number of
elements and the plan stays cacheable. An array can be a runtime parameter ([`Parameter`](xref:NextORM.Core.SqlFunctions.Parameter``1(System.Int32))), a
captured local/field or an inline `new[]`. Only a dialect that opts in with [`SupportsArrays`](xref:NextORM.Core.ISqlDialect.SupportsArrays)
(PostgreSQL) can render the array surface; every other provider throws `NotSupportedException`.

`SqlFunctions.Postgres.any` / `SqlFunctions.Postgres.all` accept an array, either as a complete predicate (`column = any(@array)`)
or as the right-hand side of a comparison:

```csharp
var ids = new long[] { 1, 2, 3 };

var rows = dataContext.From<IComplexEntity>()
    .Where(e => SqlFunctions.Postgres.any(e.Id, ids))     // (id = any(@p0))
    .Select(e => new { e.Id })
    .ToList();

var same = dataContext.From<IComplexEntity>()
    .Where(e => e.Id == SqlFunctions.Postgres.any(ids))   // id = any(@p0)
    .Select(e => new { e.Id })
    .ToList();
```

```sql
select id
 from complex_entity where (id = any(@p0))
```

A runtime array parameter uses the same [`Parameter`](xref:NextORM.Core.SqlFunctions.Parameter``1(System.Int32)) mechanism, so the array never has to be known when
the query is prepared:

```csharp
var prepared = dataContext.From<IComplexEntity>()
    .Where(e => e.Id == SqlFunctions.Postgres.any(SqlFunctions.Parameter<long[]>(0)))
    .Select(e => new { e.Id })
    .Prepare();

var rows = prepared.ToList(new long[] { 1, 2, 3 });
```

```sql
select id
 from complex_entity where id = any(@norm_p0)
```

The array functions and operators map to their PostgreSQL names:

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.cardinality(a)` | `cardinality(a)` |
| `SqlFunctions.Postgres.array_length(a, dim)` | `array_length(a, dim)` |
| `SqlFunctions.Postgres.array_ndims(a)` | `array_ndims(a)` |
| `SqlFunctions.Postgres.array_lower(a, dim)` | `array_lower(a, dim)` |
| `SqlFunctions.Postgres.array_upper(a, dim)` | `array_upper(a, dim)` |
| `SqlFunctions.Postgres.array_position(a, element)` | `array_position(a, element)` |
| `SqlFunctions.Postgres.array_contains(a, b)` | `a @> b` |
| `SqlFunctions.Postgres.array_contained_by(a, b)` | `a <@ b` |
| `SqlFunctions.Postgres.array_overlaps(a, b)` | `a && b` |
| `SqlFunctions.Postgres.array_concat(a, b)` | `a \|\| b` |
| `SqlFunctions.Postgres.array_cat(a, b)` | `array_cat(a, b)` |
| `SqlFunctions.Postgres.array_append(a, element)` | `array_append(a, element)` |
| `SqlFunctions.Postgres.array_prepend(element, a)` | `array_prepend(element, a)` |
| `SqlFunctions.Postgres.array_remove(a, element)` | `array_remove(a, element)` |
| `SqlFunctions.Postgres.array_replace(a, from, to)` | `array_replace(a, from, to)` |
| `SqlFunctions.Postgres.array_fill(value, dims)` | `array_fill(value, dims)` |
| `SqlFunctions.Postgres.array_dims(a)` | `array_dims(a)` |
| `SqlFunctions.Postgres.array_positions(a, element)` | `array_positions(a, element)` |
| `SqlFunctions.Postgres.array_reverse(a)` | — (no PostgreSQL release provides `array_reverse`; refused at preparation) |
| `SqlFunctions.Postgres.array_sort(a)` | `array_sort(a)` (PostgreSQL 18+, explicit version required) |
| `SqlFunctions.Postgres.array_shuffle(a)` | `array_shuffle(a)` (PostgreSQL 16+) |
| `SqlFunctions.Postgres.array_sample(a, n)` | `array_sample(a, n)` (PostgreSQL 16+) |
| `SqlFunctions.Postgres.array_to_string(a, delimiter)` | `array_to_string(a, delimiter)` |
| `SqlFunctions.Postgres.string_to_array(s, delimiter)` | `string_to_array(s, delimiter)` |

> `array_reverse` and `array_sort` are gated per name by
> [`SupportsArrayFunction`](xref:NextORM.Core.ISqlDialect.SupportsArrayFunction(System.String)): the
> PostgreSQL provider refuses both at preparation, before any SQL is sent, with a plain
> `NotSupportedException`. `array_reverse` is refused on **every** PostgreSQL version — no release
> provides it — with `The PostgreSQL function 'array_reverse' is not supported by this provider.`;
> `array_sort` is refused below PostgreSQL 18 **and when the server version is unset**, with
> `The PostgreSQL function 'array_sort' is not supported by this provider.` The refusal is never a
> `NULL`, an unchanged array or a driver `42883`.

> The functions that return an array (`array_append`, `array_cat`, `string_to_array`,
> ...) can be used inside a query (a predicate, `having` or a nested expression) or projected directly:
> the row reader materialises an `Array(T)` result as a CLR `T[]`.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(e => SqlFunctions.Postgres.array_length(SqlFunctions.Parameter<long[]>(0), 1) == 3)
    .Select(e => new { N = SqlFunctions.Postgres.cardinality(SqlFunctions.Parameter<long[]>(1)) })
    .ToList();
```

```sql
select cardinality(@norm_p1) as "N"
 from complex_entity where array_length(@norm_p0, 1) = 3
```

### Shaping and expanding arrays (#281)

`trim_array` returns a shortened copy of a one-dimensional array, and the multi-array `unnest`, the
`WITH ORDINALITY` and the `generate_subscripts` forms expand arrays into rows. The six set-returning
members are `FROM` sources: call them inside `ctx.From(() => ...)` and project the members of their row
interface. The single-array `unnest(array)` source (`IUnnestRow<T>`, the `unnest` column) is described in
[Table-valued functions](../guide/13-table-valued-functions.md#built-in-table-functions).

The scalar member:

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.trim_array(array, n)` | `trim_array(@p0, n)` |

The set-returning members — the `FROM` fragment each one renders:

| C# | SQL | Row interface | Columns (C# ← SQL: CLR) |
|---|---|---|---|
| `SqlFunctions.Postgres.unnest(first, second)` | `rows from (unnest(@p0), unnest(@p1)) as u(value1, value2)` | `IUnnestMultiRow<T1, T2>` | `Value1 T1 ← value1`; `Value2 T2 ← value2` |
| `SqlFunctions.Postgres.unnest(first, second, third)` | `rows from (unnest(@p0), unnest(@p1), unnest(@p2)) as u(value1, value2, value3)` | `IUnnestMultiRow<T1, T2, T3>` | `Value1 T1 ← value1`; `Value2 T2 ← value2`; `Value3 T3 ← value3` |
| `SqlFunctions.Postgres.unnest_with_ordinality(array)` | `rows from (unnest(@p0)) with ordinality as u(value, ordinality)` | `IUnnestOrdinalityRow<T>` | `Value T ← value`; `Ordinality long ← ordinality` |
| `SqlFunctions.Postgres.unnest_with_ordinality(first, second)` | `rows from (unnest(@p0), unnest(@p1)) with ordinality as u(value1, value2, ordinality)` | `IUnnestOrdinalityMultiRow<T1, T2>` | `Value1 T1 ← value1`; `Value2 T2 ← value2`; `Ordinality long ← ordinality` |
| `SqlFunctions.Postgres.generate_subscripts(array, dimension)` | `generate_subscripts(@p0, 1)` | `IGenerateSubscriptsRow` | `Value int ← generate_subscripts` |
| `SqlFunctions.Postgres.generate_subscripts(array, dimension, reverse)` | `generate_subscripts(@p0, 1, true)` | `IGenerateSubscriptsRow` | `Value int ← generate_subscripts` |

Measured behaviour on PostgreSQL 17.11:

* **Unequal arrays are padded with `NULL`.** One row is produced per position of the longest array, and
  every shorter array contributes `NULL` from that position on: `unnest({1,2}, {a})` yields
  `(1, 'a')`, `(2, NULL)`. Two empty arrays yield no rows.
* **The ordinality column is `bigint`, so it is `long` on the CLR side.** It is one-based and shared by
  every admitted array of the call, so `unnest_with_ordinality({10,20})` yields `(10, 1)`, `(20, 2)`, and
  the two-array form returns the shared position alongside both arrays.
* **The admitted multi-array arity is one, two or three arrays.** Four or more arrays are refused while
  the source is composed — `NotSupportedException`, no SQL emitted — with
  `The unnest function is refused for four or more arrays: multi-array unnest is FROM-clause grammar with no variadic arity, and this library admits exactly one, two and three arrays as typed overloads.`
  PostgreSQL's multi-array `unnest` is `FROM`-clause grammar rather than a `pg_proc` function, so it has
  no variadic arity; the provider renders the admitted arities through `ROWS FROM (...)`.
* `generate_subscripts` numbers dimensions from one and yields one row per valid subscript as an `int`;
  `reverse: true` generates them in descending order, and an empty (or SQL-NULL) array yields no rows.
* `trim_array` removes the last `n` elements and keeps the element type; `n` equal to the length yields an
  empty array, `n = 0` returns the array unchanged, and `n` greater than the length raises the server's
  error. Only one-dimensional arrays are accepted.
* An array operand travels as **one** parameter of its own type — `SqlFunctions.Parameter<T[]>(idx)` or a
  captured array — including the array of a `FROM` source:
  `generate_subscripts(SqlFunctions.Parameter<long[]>(0), 1)`.

```csharp
var rows = dataContext
    .From(() => SqlFunctions.Postgres.unnest_with_ordinality(SqlFunctions.Parameter<long[]>(0)))
    .Select(r => new { r.Value, r.Ordinality })
    .ToList(new long[] { 10, 20 });
```

```sql
select value, ordinality
 from (select value, ordinality
 from rows from (unnest(@norm_p0)) with ordinality as u(value, ordinality)) as "t1"
```

## Arrays (ClickHouse)

ClickHouse has a native `Array(T)` type. The array functions operate on array **columns** (or on nested
array expressions) and are gated by `ISqlDialect.SupportsArrayFunctions`; `arrayJoin` additionally
requires `SupportsArrayJoin`. `arrayJoin(array)` expands the array into one row per element, so its
result can be projected like a scalar column.

| Function | SQL |
|---|---|
| `SqlFunctions.ClickHouse.length(a)` | `length(a)` |
| `SqlFunctions.ClickHouse.has(a, element)` | `has(a, element)` |
| `SqlFunctions.ClickHouse.index_of(a, element)` | `indexOf(a, element)` |
| `SqlFunctions.ClickHouse.has_any(a, b)` | `hasAny(a, b)` |
| `SqlFunctions.ClickHouse.has_all(a, b)` | `hasAll(a, b)` |
| `SqlFunctions.ClickHouse.starts_with(a, prefix)` | `startsWith(a, prefix)` |
| `SqlFunctions.ClickHouse.ends_with(a, suffix)` | `endsWith(a, suffix)` |
| `SqlFunctions.ClickHouse.has_substr(a, other)` | `hasSubstr(a, other)` |
| `SqlFunctions.ClickHouse.array_string_concat(a, delimiter)` | `arrayStringConcat(a, delimiter)` |
| `SqlFunctions.ClickHouse.split_by_char(separator, s)` | `splitByChar(separator, s)` |
| `SqlFunctions.ClickHouse.array_sort(a)` | `arraySort(a)` |
| `SqlFunctions.ClickHouse.array_reverse(a)` | `arrayReverse(a)` |
| `SqlFunctions.ClickHouse.array_distinct(a)` | `arrayDistinct(a)` |
| `SqlFunctions.ClickHouse.range(start, end)` | `range(start, end)` |
| `SqlFunctions.ClickHouse.array_enumerate(a)` | `arrayEnumerate(a)` |
| `SqlFunctions.ClickHouse.array_cum_sum(a)` | `arrayCumSum(a)` |
| `SqlFunctions.ClickHouse.array_slice(a, offset, length)` | `arraySlice(a, offset, length)` |
| `SqlFunctions.ClickHouse.array_push_back(a, element)` | `arrayPushBack(a, element)` |
| `SqlFunctions.ClickHouse.array_join(a)` | `arrayJoin(a)` |
| `SqlFunctions.ClickHouse.group_array(a)` | `groupArray(a)` |
| `SqlFunctions.ClickHouse.group_uniq_array(a)` | `groupUniqArray(a)` |
| `SqlFunctions.ClickHouse.array_map(f, a)` | `arrayMap(f, a)` |
| `SqlFunctions.ClickHouse.array_filter(f, a)` | `arrayFilter(f, a)` |
| `SqlFunctions.ClickHouse.array_exists(f, a)` | `arrayExists(f, a)` |
| `SqlFunctions.ClickHouse.array_all(f, a)` | `arrayAll(f, a)` |
| `SqlFunctions.ClickHouse.array_count(f, a)` | `arrayCount(f, a)` |
| `SqlFunctions.ClickHouse.array_first(f, a)` | `arrayFirst(f, a)` |
| `SqlFunctions.ClickHouse.array_first_index(f, a)` | `arrayFirstIndex(f, a)` |
| `SqlFunctions.ClickHouse.array_last(f, a)` | `arrayLast(f, a)` |
| `SqlFunctions.ClickHouse.array_last_index(f, a)` | `arrayLastIndex(f, a)` |

### Delivered array, tuple and map functions (#286, T286b)

The members below complete the array catalogue on the pinned 25.8.33.6 server. Result shapes are
declared explicitly: element-wise predicates return `bool`, aggregations return the scalar/array CLR
type they declare, and the sort/split/fill families return `T[]`.

| Function | SQL |
|---|---|
| `SqlFunctions.ClickHouse.array_element(a, i)` | `arrayElement(a, i)` |
| `SqlFunctions.ClickHouse.array_enumerate_dense(a)` | `arrayEnumerateDense(a)` |
| `SqlFunctions.ClickHouse.array_zip(a, b)` | `arrayZip(a, b)` |
| `SqlFunctions.ClickHouse.array_difference(a)` | `arrayDifference(a)` |
| `SqlFunctions.ClickHouse.array_avg(a)` | `arrayAvg(a)` |
| `SqlFunctions.ClickHouse.array_sum(a)` | `arraySum(a)` |
| `SqlFunctions.ClickHouse.array_min(a)` | `arrayMin(a)` |
| `SqlFunctions.ClickHouse.array_max(a)` | `arrayMax(a)` |
| `SqlFunctions.ClickHouse.array_product(a)` | `arrayProduct(a)` |
| `SqlFunctions.ClickHouse.array_top_k(a, k)` | `arrayTopK(a, k)` (version-gated: 26.6) |
| `SqlFunctions.ClickHouse.array_bottom_k(a, k)` | `arrayBottomK(a, k)` (version-gated: 26.6) |
| `SqlFunctions.ClickHouse.array_random_sample(a, n)` | `arrayRandomSample(a, n)` |
| `SqlFunctions.ClickHouse.array_compact(a)` | `arrayCompact(a)` |
| `SqlFunctions.ClickHouse.array_pop_back(a)` | `arrayPopBack(a)` |
| `SqlFunctions.ClickHouse.array_pop_front(a)` | `arrayPopFront(a)` |
| `SqlFunctions.ClickHouse.array_push_front(a, element)` | `arrayPushFront(a, element)` |
| `SqlFunctions.ClickHouse.array_resize(a, size[, extender])` | `arrayResize(a, size[, extender])` |
| `SqlFunctions.ClickHouse.array_remove(a, f)` | `arrayRemove(a, f)` (version-gated: 25.11) |
| `SqlFunctions.ClickHouse.array_rotate_left(a, n)` / `array_rotate_right(a, n)` | `arrayRotateLeft` / `arrayRotateRight` |
| `SqlFunctions.ClickHouse.array_shift_left(a, n)` / `array_shift_right(a, n)` | `arrayShiftLeft` / `arrayShiftRight` |
| `SqlFunctions.ClickHouse.array_fold(f, a, init)` | `arrayFold(f, a, init)` |
| `SqlFunctions.ClickHouse.array_fill(f, a)` / `array_reverse_fill(f, a)` | `arrayFill` / `arrayReverseFill` |
| `SqlFunctions.ClickHouse.array_split(f, a)` / `array_reverse_split(f, a)` | `arraySplit` / `arrayReverseSplit` |
| `SqlFunctions.ClickHouse.array_reverse_sort(a)` / `array_partial_sort(a, k)` | `arrayReverseSort` / `arrayPartialSort` |
| `SqlFunctions.ClickHouse.array_sort(a)` | `arraySort(a)` |
| `SqlFunctions.ClickHouse.array_shingles(a, n)` | `arrayShingles(a, n)` |
| `SqlFunctions.ClickHouse.array_transpose(a)` | `arrayTranspose(a)` (version-gated: 26.4) |
| `SqlFunctions.ClickHouse.array_cum_sum_non_negative(a)` | `arrayCumSumNonNegative(a)` |
| `SqlFunctions.ClickHouse.count_equal(a, b)` | `countEqual(a, b)` |
| `SqlFunctions.ClickHouse.array_with_constant(n, value)` | `arrayWithConstant(n, value)` |
| `SqlFunctions.ClickHouse.array_jaccard_index(a, b)` / `array_similarity(a, b)` | `arrayJaccardIndex` / `arraySimilarity` |
| `SqlFunctions.ClickHouse.tuple_concat(a, b)` | `tupleConcat(a, b)` |
| `SqlFunctions.ClickHouse.tuple_names(t)` | `tupleNames(t)` |
| `SqlFunctions.ClickHouse.flatten_tuple(t)` | `flattenTuple(t)` |
| `SqlFunctions.ClickHouse.tuple_to_name_value_pairs(t)` | `tupleToNameValuePairs(t)` |
| `SqlFunctions.ClickHouse.tuple_plus` / `tuple_minus` / `tuple_multiply` / `tuple_divide` | `tuplePlus` / `tupleMinus` / `tupleMultiply` / `tupleDivide` |
| `SqlFunctions.ClickHouse.tuple_hamming_distance(a, b)` | `tupleHammingDistance(a, b)` |
| `SqlFunctions.ClickHouse.dot_product(a, b)` | `dotProduct(a, b)` |
| `SqlFunctions.ClickHouse.map_subtract(a, b)` | `mapSubtract(a, b)` |
| `SqlFunctions.ClickHouse.map_update(a, b)` | `mapUpdate(a, b)` |
| `SqlFunctions.ClickHouse.map_reverse_sort(m[, f])` / `map_partial_sort(m, k[, f])` | `mapReverseSort` / `mapPartialSort` |
| `SqlFunctions.ClickHouse.map_from_arrays(keys, values)` | `mapFromArrays(keys, values)` |
| `SqlFunctions.ClickHouse.map_populate_series(m, start, end[, step])` | `mapPopulateSeries(...)` |
| `SqlFunctions.ClickHouse.extract_key_value_pairs(s)` | `extractKeyValuePairs(s)` |

`arrayReduce`/`arrayReduceInRanges` stay refused: the aggregate is named by a runtime SQL string, so no
typed contract can close it (see
[ClickHouse-specific SQL › Not yet supported](../guide/provider-specific-sql/clickhouse.md#not-yet-supported)).
`untuple` is refused for the same structural reason: one projection expression maps to exactly one
result column.

> `length`/`indexOf` return `UInt64` natively, so the dialect casts them with `toInt64(...)`. Functions
> that return an array (`split_by_char`, `array_sort`, `array_reverse`, `array_distinct`, `range`,
> `array_enumerate`, `array_cum_sum`, `array_slice`, `array_push_back`, `group_array`,
> `group_uniq_array`) can be projected directly — the row reader materialises an `Array(T)` result as a
> CLR `T[]` — or used as the operand of another array function (for example `length(...)` or
> `array_string_concat(...)`). The facades whose declared CLR array type can differ from the element type
> ClickHouse infers (`top_k`, `top_k_weighted`, `group_array`, `group_uniq_array`, `retention`,
> `array_map`, and the three `range` overloads) are normalised **server-side**: the dialect wraps the
> completed call in exactly one `cast(... as Array(...))` built from the closed declared CLR type
> (`int[]` → `Array(Int32)`, `int?[]` → `Array(Nullable(Int32))`, `long[]` → `Array(Int64)`,
> `string[]`/`string?[]` → `Array(Nullable(String))`). A reference-type element is always declared
> `Nullable(...)`, because its CLR nullable annotation is erased at runtime, so no caller-side cast is
> required and `retention` materialises as
> `int[]`, not `byte[]`. The same reader materialises a native `Tuple(...)` column (or a
> `Tuple(...)`-returning expression) as a `System.Tuple<...>` of arity 1–7.

> `SqlFunctions.ClickHouse.array_except` is retained for API compatibility. An undeclared context —
> the pinned 25.8 baseline — has no native `arrayExcept` (it exists only from ClickHouse 25.9), so the
> call is refused with `NotSupportedException` at translation time instead of being emitted as SQL.
> A context that declares 25.9 or later through the versioned `ClickHouseDataContext` constructor
> renders the native `arrayExcept(...)` for the two-array form only (the native function takes exactly
> two arrays; any other arity is refused before SQL); a declared older version still refuses, naming the required
> `25.9` and the declared version. See
> [Declaring the server version](../connecting-providers/clickhouse.md#declaring-the-server-version).

> The array relation predicates return `bool`: `starts_with(array, prefix)`/`ends_with(array, suffix)`
> test a prefix/suffix and `has_substr(array, other)` tests that `other` occurs in `array` contiguously
> and in order (an empty `other` is always contained). They require a provider that supports the array
> functions.

> The row-value (tuple) surface is cross-provider and built from `System.Tuple.Create` /
> `new Tuple<...>` / `System.Tuple<...>.ItemN`: a constructor renders through the dialect's row
> constructor — `tuple(a, b)` on ClickHouse, `ROW(a, b)` on PostgreSQL, and the flat ANSI `(a, b)` on
> MySQL/MariaDB/SQLite — and access to an element of a *server-side* row renders through the dialect's
> positional access (`tupleElement(pair, 1)` on ClickHouse, `(pair).f1` on PostgreSQL). MySQL/MariaDB/SQLite
> have no server-side element access, so `System.Tuple<...>.ItemN` on a server row throws
> `NotSupportedException` there. Access to an element of an *inline* constructor
> (`Tuple.Create(a, b).Item1`, `new ValueTuple<...>(a, b).Item2`) folds to the argument and works on every
> dialect that can express the constructor. `System.Tuple<,> ==` (reference equality in C#) is
> reinterpreted as a SQL row-value comparison (`Tuple.Create(x.A, x.B) == Tuple.Create(1, 'a')` →
> `ROW(a, b) = ROW(1, 'a')`); `ValueTuple` `==` cannot appear in an expression tree. On PostgreSQL and
> ClickHouse the surface is the native row type ([`ITupleRenderer`](xref:NextORM.Core.ITupleRenderer) /
> [`ISqlDialect.Tuple`](xref:NextORM.Core.ISqlDialect.Tuple)); on MySQL/MariaDB/SQLite the flat `(a, b)`
> constructor is accepted only as a **direct comparison operand** in `WHERE`/`HAVING`/`JOIN ON` — in
> `Select`/`ORDER BY`/`GROUP BY` or as a function argument it throws `NotSupportedException` at
> preparation. SQL Server and the in-memory provider reject that **row-constructor/row-comparison**
> surface; the in-memory provider still evaluates a tuple `IN`/`Contains` value list in process (it is
> not rejected). Tuple `IN`/`Contains` over a
> **value list** of flat `System.Tuple`/`System.ValueTuple` entries of arity 1..7 is shipped
> ([#193](https://github.com/AlexeyShirshov/nextorm/issues/193)); the exact provider forms and semantics
> are documented in the tuple `IN` block below.
> Materialising a raw `ROW(...)` is shipped on PostgreSQL
> ([#194](https://github.com/AlexeyShirshov/nextorm/issues/194)): a single `ROW(...)` (arity 1..7) or a
> caller-registered named composite result column materialises into the matching
> `System.Tuple<...>`/named type (see
> [Raw SQL](../guide/14-raw-sql.md#postgresql-raw-rows-and-composites)); `ValueTuple`, arity ≥8, a
> nested/empty `ROW`, several or mixed record columns, an unregistered named composite and a composite
> declared as `System.Tuple` remain guarded with `NotSupportedException`. `untuple` is not supported
> because it changes the result column set rather than producing a scalar.

> Tuple `IN`/`Contains` translates a value list of flat `System.Tuple<...>`/`System.ValueTuple<...>`
> entries of arity **1..7** (`SqlFunctions.Sql.@in`, `SqlFunctions.ClickHouse.global_in` or
> `Enumerable.Contains`) into a row membership test built from the dialect's row constructor, with the
> ordered components as separate parameters (never one opaque tuple parameter). The exact provider forms:
>
> | Provider | Tuple `IN` SQL |
> |---|---|
> | SQLite | `(a, b) IN (VALUES (@p0, @p1), (@p2, @p3))` |
> | MySQL / MariaDB | `(a, b) IN ((@p0, @p1), (@p2, @p3))` |
> | PostgreSQL | `ROW(a, b) IN (ROW(@p0, @p1), ROW(@p2, @p3))` |
> | ClickHouse | `tuple(a, b) IN (tuple(@p0, @p1), tuple(@p2, @p3))` (`global_in` renders `GLOBAL IN`) |
> | SQL Server | not supported — **every** tuple form throws `NotSupportedException` with `SQL Server does not support tuple IN/Contains translation.`, raised before the collection is evaluated (a null collection, a null reference-tuple entry, arity ≥8 or a nested tuple all surface this message, never an evaluation error) |
> | In-memory | evaluated in process (no SQL is rendered; `Enumerable.Contains` runs against the in-memory rows) |
>
> An empty (non-null) collection renders `1 = 0`. On the providers that support the surface, a null
> collection throws `ArgumentNullException`, a `default` value tuple is an ordinary row, and a
> **nullable tuple element** (`List<(int, int)?>`) throws `NotSupportedException`. Membership is
> two-valued, so a null component matches another null component: rows whose cells are all non-null use
> the provider's native row list, guarded with `IS NOT NULL` on every nullable left-hand component, while
> a row that contains a null is rendered as an explicit OR-of-AND arm (`c1 IS NULL` /
> `c1 IS NOT NULL AND c1 = @p`), because a plain row `IN` can never match SQL `NULL`. A null
> reference-tuple entry (`null` inside the collection) throws `NotSupportedException` rather than
> degrading into an all-null row, and arity ≥8, the `Rest` form and nested tuple components throw
> `NotSupportedException`; on SQL Server all of these surface the provider rejection above instead.
> Deferred in the current milestone: a tuple-typed `QueryCommand` (subquery) right-hand side and
> `Rest`/nested tuples are **not supported yet**; no future milestone is promised.

> The higher-order (lambda) functions take an inline C# lambda whose parameter is the array element,
> for example `array_map(v => -v, e.Nums)` renders `arrayMap(v -> -(v), nums)`. `array_exists`/`array_all`
> return `bool`; `array_count`/`array_first_index`/`array_last_index` return `long` (the dialect casts
> the native `UInt32` with `toInt64(...)`); `array_first`/`array_last` return the element or its default
> value when nothing matches. They require a provider that supports the higher-order array functions
> (see [`SupportsHigherOrderArrayFunctions`](xref:NextORM.Core.ISqlDialect.SupportsHigherOrderArrayFunctions);
> ClickHouse). ClickHouse promotes the arithmetic result type independently of C# (an `Int32` element
> multiplied by an integer literal becomes `Array(Int64)`); the dialect normalises the completed
> `arrayMap` result to the closed declared CLR element type, so the lambda operand stays exactly as
> written and no `(long)v`-style cast inside the lambda is needed (`arrayFilter` already returns its
> input element type).

> **Nullable elements in a bound array parameter:** a rank-1 CLR array whose **declared** element type is
> nullable (`Nullable<T>` or `string`) is bound as `Array(Nullable(T))` derived from that declared CLR type
> (`int?[]` → `Array(Nullable(Int32))`, `double?[]` → `Array(Nullable(Float64))`, `DateTime?[]` →
> `Array(Nullable(DateTime))`, `decimal?[]` → `Array(Nullable(Decimal(38,10)))`, `string?[]` →
> `Array(Nullable(String))`), so `null` elements and their positions reach the server —
> `array_map(v => v, new int?[] { 1, null, 3 })` yields `{ 1, null, 3 }`. Empty and all-`null` arrays carry
> the same typed element, and the caller's array is not mutated. Non-nullable element arrays (`int[]`,
> `double[]`, `DateTime[]`, `decimal[]`, `byte[]`) are unchanged; nested/jagged and multidimensional arrays
> and a `null` array object are **not supported** and keep the previous (driver-inferred) behaviour.
> Array **results** follow the same element rule: a **reference-type** element result is declared
> `Nullable(...)` unconditionally (a `string[]`/`string?[]` result renders `Array(Nullable(String))`, a
> jagged one `Array(Array(Nullable(String)))`) and still materialises as a CLR `string[]` — only the
> rendered SQL text changes, not the data, the CLR type or the public API, while value-type elements
> (`int[]`, `int?[]`, `byte[]`) are unaffected. Decimal array **results** are materialised in both
> shapes since [#337](https://github.com/AlexeyShirshov/nextorm/issues/337):
> `Array(Nullable(Decimal(38,10)))` → `decimal?[]` and `Array(Decimal(38,10))` → `decimal[]`, through
> the direct, member-bound, multi-column and raw read routes, with every representable value preserved
> exactly (no rounding, truncation or culture conversion). A `Decimal(38,10)` element whose scaled
> mantissa exceeds 96 bits — for example `1e19`, whose mantissa at scale 10 is `1e29` while CLR
> `decimal`'s mantissa is `2^96 − 1` — is **not** representable in CLR `decimal`: the materialisation
> fails closed with the driver's `OverflowException` (`Value cannot be represented as System.Decimal`),
> and because the pinned driver does not trim trailing zeros a mathematically smaller representation is
> not substituted. The remaining array restrictions are listed under
> [Limitations](../advanced/limitations.md#limitations).

The CLR `string.Split` is rendered as `splitByChar(separator, value)` (gated by
[`StringSplit`](xref:NextORM.Core.ISqlDialect.StringSplit)); only a single-character
separator is supported (the multi-character `splitByString` is not exposed), the result is a `string[]`
that can be projected directly or used inside another array function, and the `count` overload, multiple separators and
`StringSplitOptions` other than `None` throw `NotSupportedException`:

```csharp
var parts = dataContext.From<IComplexEntity>()
    .Select(e => SqlFunctions.ClickHouse.length(e.String!.Split(',')))
    .First();
```

```csharp
var tags = dataContext.From<IArrayEntity>()
    .Where(e => e.Id == 1)
    .Select(e => new { e.Id, Tag = SqlFunctions.ClickHouse.array_join(e.Tags) })
    .ToList();
```

```sql
select id, arrayJoin(tags) as `Tag`
 from array_entity where id = 1
```

`EntityBuilder.ArrayJoin`/`LeftArrayJoin` render the `[LEFT] ARRAY JOIN` clause, which expands the rows
before `WHERE`/`GROUP BY`; `LEFT ARRAY JOIN` keeps a row whose array is empty. The expanded element is
not bound to a CLR member, so use the scalar `array_join` above when the value must be projected or
filtered.

```csharp
var ids = dataContext.From<IArrayEntity>()
    .LeftArrayJoin(e => e.Tags)
    .Select(e => e.Id)
    .ToList();
```

```sql
select id
 from array_entity
 left array join tags
```

`EntityBuilder.ArrayJoinElement`/`LeftArrayJoinElement` add the same clause but return
`EntityBuilder<ArrayJoinProjection<TEntity, TElement>>`, so both the original entity (`p.Item1`) and the
expanded element (`p.Element`) can be referenced. The clause expression is aliased and `p.Element`
translates to that alias:

```csharp
var rows = dataContext.From<IArrayEntity>()
    .ArrayJoinElement(e => e.Tags)
    .Where(p => p.Element == "b")
    .Select(p => new { p.Item1.Id, Tag = p.Element })
    .ToList();
```

```sql
select id, __nextorm_aj_element as `Tag`
 from array_entity
 array join tags as __nextorm_aj_element
where __nextorm_aj_element = 'b'
```

A bound array join is only supported on a single, un-joined source; `Where`/`Having` must be applied
after it (their parameter type is the array-join projection). Use `ArrayJoin`/`LeftArrayJoin` with the
scalar `array_join` when you need multiple arrays or a joined query.
