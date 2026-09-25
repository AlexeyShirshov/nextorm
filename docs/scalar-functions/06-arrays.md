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
select id from complex_entity where (id = any(@p0))
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
select id from complex_entity where id = any(@norm_p0)
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
| `SqlFunctions.Postgres.array_reverse(a)` | `array_reverse(a)` (PostgreSQL 18+) |
| `SqlFunctions.Postgres.array_sort(a)` | `array_sort(a)` (PostgreSQL 18+) |
| `SqlFunctions.Postgres.array_shuffle(a)` | `array_shuffle(a)` (PostgreSQL 16+) |
| `SqlFunctions.Postgres.array_sample(a, n)` | `array_sample(a, n)` (PostgreSQL 16+) |
| `SqlFunctions.Postgres.array_to_string(a, delimiter)` | `array_to_string(a, delimiter)` |
| `SqlFunctions.Postgres.string_to_array(s, delimiter)` | `string_to_array(s, delimiter)` |

> The functions that return an array (`array_append`, `array_cat`, `array_reverse`, `string_to_array`,
> ...) can be used inside a query (a predicate, `having` or a nested expression) or projected directly:
> the row reader materialises an `Array(T)` result as a CLR `T[]`.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(e => SqlFunctions.Postgres.array_length(SqlFunctions.Parameter<long[]>(0), 1) == 3)
    .Select(e => new { N = SqlFunctions.Postgres.cardinality(SqlFunctions.Parameter<long[]>(1)) })
    .ToList();
```

```sql
select cardinality(@norm_p1) as "N" from complex_entity where array_length(@norm_p0, 1) = 3
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

> `length`/`indexOf` return `UInt64` natively, so the dialect casts them with `toInt64(...)`. Functions
> that return an array (`split_by_char`, `array_sort`, `array_reverse`, `array_distinct`, `range`,
> `array_enumerate`, `array_cum_sum`, `array_slice`, `array_push_back`, `group_array`,
> `group_uniq_array`) can be projected directly — the row reader materialises an `Array(T)` result as a
> CLR `T[]` — or used as the operand of another array function (for example `length(...)` or
> `array_string_concat(...)`). The same reader materialises a native `Tuple(...)` column (or a
> `Tuple(...)`-returning expression) as a `System.Tuple<...>` of arity 1–7.

> The array relation predicates return `bool`: `starts_with(array, prefix)`/`ends_with(array, suffix)`
> test a prefix/suffix and `has_substr(array, other)` tests that `other` occurs in `array` contiguously
> and in order (an empty `other` is always contained). They require a provider that supports the array
> functions.

> The row-value (tuple) surface is cross-provider and built from `System.Tuple.Create` /
> `new Tuple<...>` / `System.Tuple<...>.ItemN`: a constructor renders through the dialect's row
> constructor — `tuple(a, b)` on ClickHouse, `ROW(a, b)` on PostgreSQL — and access to an element of a
> *server-side* row renders through the dialect's positional access (`tupleElement(pair, 1)` on
> ClickHouse, `(pair).f1` on PostgreSQL). Access to an element of an *inline* constructor
> (`Tuple.Create(a, b).Item1`, `new ValueTuple<...>(a, b).Item2`) folds to the argument, so it works on
> every dialect that can express the constructor. `System.Tuple<,> ==` (reference equality in C#) is
> reinterpreted as a SQL row-value comparison (`Tuple.Create(x.A, x.B) == Tuple.Create(1, 'a')` →
> `ROW(a, b) = ROW(1, 'a')`); `ValueTuple` `==` cannot appear in an expression tree. Requires a provider
> with a native row type (see [`ITupleRenderer`](xref:NextORM.Core.ITupleRenderer) /
> [`ISqlDialect.Tuple`](xref:NextORM.Core.ISqlDialect.Tuple); PostgreSQL and ClickHouse); SQL Server,
> MySQL, MariaDB, SQLite and the in-memory provider reject the surface, and tuple `IN`/`Contains` over a
> value list is not translated yet. `untuple` is not supported because it changes the result column set
> rather than producing a scalar.

> The higher-order (lambda) functions take an inline C# lambda whose parameter is the array element,
> for example `array_map(v => -v, e.Nums)` renders `arrayMap(v -> -(v), nums)`. `array_exists`/`array_all`
> return `bool`; `array_count`/`array_first_index`/`array_last_index` return `long` (the dialect casts
> the native `UInt32` with `toInt64(...)`); `array_first`/`array_last` return the element or its default
> value when nothing matches. They require a provider that supports the higher-order array functions
> (see [`SupportsHigherOrderArrayFunctions`](xref:NextORM.Core.ISqlDialect.SupportsHigherOrderArrayFunctions);
> ClickHouse). ClickHouse promotes the arithmetic result type independently of C# (an `Int32` element
> multiplied by an integer literal becomes `Array(Int64)`), so cast inside the lambda
> (`v => (long)v * 2`) when the element type must match the projected `T[]`.

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
select id, arrayJoin(tags) as `Tag` from array_entity where id = 1
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
select id from array_entity left array join tags
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
select id, __nextorm_aj_element as `Tag` from array_entity
array join tags as __nextorm_aj_element
where __nextorm_aj_element = 'b'
```

A bound array join is only supported on a single, un-joined source; `Where`/`Having` must be applied
after it (their parameter type is the array-join projection). Use `ArrayJoin`/`LeftArrayJoin` with the
scalar `array_join` when you need multiple arrays or a joined query.
