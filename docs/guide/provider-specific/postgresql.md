# PostgreSQL-specific SQL

> PostgreSQL has the richest native type system, so its provider-exclusive surface is a set of functions
> and operators on native types rather than query keywords: array types, `json`/`jsonb`, ordered-set,
> regression and boolean aggregates, the extended scalar/regexp helpers, `DISTINCT ON` and the `unnest`
> table function.

**Prerequisites:** [Querying and projections](../01-querying-and-projections.md) · [PostgreSQL provider](../../providers/postgres.md)

## Arrays

PostgreSQL has native array types. An array operand is always passed as a **single parameter** (the
whole array), never expanded into a value list, so the SQL text does not depend on the number of
elements and the plan stays cacheable. The surface is gated by
[`SupportsArrays`](xref:NextORM.Core.ISqlDialect.SupportsArrays) and includes `cardinality`,
`array_length`, `array_position`, `array_append`, `array_cat`, `array_to_string`, `string_to_array` and
the operators `@>`, `<@`, `&&`, `||`, as well as `any`/`all` over an array:

```csharp
var ids = new long[] { 1, 2, 3 };

var rows = dataContext.From<IComplexEntity>()
    .Where(e => SqlFunctions.Postgres.any(e.Id, ids))
    .Select(e => new { e.Id })
    .ToList();
```

```sql
select id from complex_entity where (id = any(@p0))
```

See [Arrays (PostgreSQL)](../../scalar-functions/06-arrays.md#arrays-postgresql). Functions that return an array
can be used inside a query or projected directly: the row reader materialises an `Array(T)` result as
a CLR `T[]`.

## Range types

PostgreSQL has native range types (`int4range`, `int8range`, `numrange`, `tsrange`, `tstzrange`,
`daterange`). nextorm represents them with the provider-agnostic
[`Range<T>`](xref:NextORM.Core.Range`1) value type — `Lower`/`Upper`, `LowerInclusive`/`UpperInclusive`,
`LowerInfinite`/`UpperInfinite`, `IsEmpty` and [`Range<T>.Empty`](xref:NextORM.Core.Range`1.Empty) — which
the PostgreSQL provider binds through Npgsql, preserving unbounded sides and the `empty` range. The
surface is gated by [`SupportsRanges`](xref:NextORM.Core.ISqlDialect.SupportsRanges) and lives on
[`Postgres`](xref:NextORM.Core.SqlFunctions.Postgres). The operators are exposed as static methods named
after the SQL tokens, so no range member collides with a CLR operator or the full-text `Contains`:

| Member | SQL |
|---|---|
| `overlaps(a, b)` | `a && b` |
| `range_contains(range, value)` / `range_contains(outer, inner)` | `range @> value` / `outer @> inner` |
| `range_contained_by(inner, outer)` | `inner <@ outer` |
| `range_union(a, b)` / `range_intersection(a, b)` / `range_difference(a, b)` | `a + b` / `a * b` / `a - b` |
| `range_adjacent(a, b)` | `a -|- b` |
| `range_strictly_left_of(a, b)` / `range_strictly_right_of(a, b)` | `a << b` / `a >> b` |
| `range_not_extend_right_of(a, b)` / `range_not_extend_left_of(a, b)` | `a &< b` / `a &> b` |
| `lower(range)` / `upper(range)` / `isempty(range)` | `lower(range)` / `upper(range)` / `isempty(range)` |
| `lower_inc` / `upper_inc` / `lower_inf` / `upper_inf` | `lower_inc(range)` / … |
| `int4range` / `int8range` / `numrange` / `tsrange` / `tstzrange` / `daterange` | native constructor, optionally with a bounds literal (`'[)'`, `'[]'`, `'()'`, `'(]'`) |
| `empty_range<T>()` | `'empty'::<range type>` |

```csharp
var window = new Range<int>(15, 25);   // [15,25)

var rows = dataContext.From<IReservation>()
    .Where(e => SqlFunctions.Postgres.overlaps(e.During, window))
    .Select(e => new { e.Id })
    .ToList();
```

```sql
select id from reservation where (during && @p0)
```

`Range<T>` is a `readonly struct` and compares by its PostgreSQL characteristics, so `Range<int>.Empty`
and a `default(Range<int>)` value are both the empty range. The in-memory provider evaluates `overlaps`,
`range_contains`/`range_contained_by` and the inspection functions with the same semantics; the remaining
operators and the constructors require PostgreSQL. Timestamp ranges use `DateTime` for `tsrange` and
`DateTimeOffset` for `tstzrange`; `daterange` uses `DateOnly`.

## `json` and `jsonb`

PostgreSQL is the only provider with native `json`/`jsonb` types. JSON operands are mapped columns,
other JSON functions, or parameters whose runtime value is a `JsonDocument`/`JsonElement`/`JsonNode`
(which Npgsql binds as `jsonb`). The `jsonb_*` family covers construction, extraction, containment and
aggregation, for example `jsonb_build_object`, `jsonb_agg`, `json_get_text`, `json_cast`, `->`, `->>`.

```csharp
var json = dataContext.From<IComplexEntity>()
    .Select(e => SqlFunctions.Postgres.jsonb_agg(e.String))
    .First();
```

```sql
select jsonb_agg(somestring) from complex_entity
```

See [JSON and JSONB (PostgreSQL)](../../scalar-functions/index.md) and
[JSON support across providers](../18-json.md).

## Ordered-set, regression and boolean aggregates

* `percentile_cont`/`percentile_disc` are ordered-set aggregates rendered as
  `percentile_cont(f) within group (order by x)` ([`SupportsOrderedAggregates`](xref:NextORM.Core.ISqlDialect.SupportsOrderedAggregates),
  [`MakeWithinGroup`](xref:NextORM.Core.ISqlDialect.MakeWithinGroup(System.String,System.String,NextORM.Core.KeywordCase))); `mode()` is the ordered-set mode;
* `bool_and`/`bool_or`/`every`, the `regr_*` regression family and `bit_and`/`bit_or`/`bit_xor` are
  PostgreSQL-only and are rejected by every other dialect;
* `array_agg` and the string/array aggregate surface are gated by
  [`SupportsStringArrayAggregates`](xref:NextORM.Core.ISqlDialect.SupportsStringArrayAggregates).

See [Grouping and aggregates](../04-grouping-and-aggregates.md).

## Full-text search

The cross-provider boolean predicates `contains`/`freetext` render
`to_tsvector(col) @@ plainto_tsquery(search)` (or `websearch_to_tsquery` for `freetext`). PostgreSQL
additionally exposes the native text-search scalar surface, gated by
[`SupportsTextSearchFunctions`](xref:NextORM.Core.ISqlDialect.SupportsTextSearchFunctions):
`to_tsvector`, `to_tsquery` (plus `plainto_tsquery`/`phraseto_tsquery`/`websearch_to_tsquery`),
`ts_rank`, `ts_rank_cd` (cover-density ranking), `ts_headline` and the `@@` match operator.
`tsvector`/`tsquery` are represented as `string`
on the CLR side.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(e => SqlFunctions.Postgres.ts_match(
        SqlFunctions.Postgres.to_tsvector(e.String!),
        SqlFunctions.Postgres.websearch_to_tsquery("cat & dog")))
    .Select(e => new
    {
        e.Id,
        Rank = SqlFunctions.Postgres.ts_rank(
            SqlFunctions.Postgres.to_tsvector(e.String!),
            SqlFunctions.Postgres.to_tsquery("cat"))
    })
    .ToList();
```

```sql
select id, ts_rank(to_tsvector(somestring), to_tsquery('cat')) from complex_entity
 where (to_tsvector(somestring) @@ websearch_to_tsquery('cat & dog'))
```

Only PostgreSQL implements the native surface; every other provider rejects it at SQL build time.

## Extended scalar and regexp helpers

The [`Postgres`](xref:NextORM.Core.SqlFunctions.Postgres) surface adds `pg_typeof`, `num_nulls`/
`num_nonnulls`, `regexp_replace`/`regexp_like`/`regexp_split_to_array`/`regexp_count`/`regexp_instr`,
`split_part`/`strpos`/`left`/`right`/`lpad`/`rpad`/`translate`/`overlay`/`format`, the math helpers
(`pi`/`random`/`gcd`/`lcm`/`width_bucket`/...), the extended date/time helpers (`to_char`/`to_date`/
`to_number`/`to_timestamp`/`timezone`/`make_interval`/`justify_*`) and the array constructors, all gated
by `SupportsExtendedScalarFunctions`.

See [Scalar functions](../../scalar-functions/index.md).

## `DISTINCT ON`

[`DistinctOn`](xref:NextORM.Core.EntityBuilder`1.DistinctOn``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) renders `SELECT DISTINCT ON (expr, ...)`, keeping the
first row of each key ([`DistinctOn`](xref:NextORM.Core.ISqlDialect.DistinctOn));
mutually exclusive with `Distinct`.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .DistinctOn(e => e.String)
    .OrderBy(e => e.String)
    .Select(e => new { e.Id, e.String })
    .ToList();
```

```sql
select distinct on (somestring) id, somestring from complex_entity order by somestring
```

See [Distinct](../08-distinct.md).

## Set-returning functions

PostgreSQL's set-returning functions are exposed as table-valued functions through
[`FromTableFunction`](xref:NextORM.Core.DataContextExtensions.FromTableFunction``1(NextORM.Core.IDataContext,System.Linq.Expressions.Expression{System.Func{System.Linq.IQueryable{``0}}})):

| `SqlFunctions.Postgres.*` | SQL output columns | Row shape |
| --- | --- | --- |
| `generate_series(start, stop[, step])` | `generate_series` | `IGenerateSeriesRow` |
| `unnest(array)` | `unnest` | `IUnnestRow<T>` |
| `regexp_matches(source, pattern[, flags])` | `regexp_matches text[]` | `IRegexpMatchesRow` |
| `regexp_split_to_table(source, pattern[, flags])` | `regexp_split_to_table` | `IRegexpSplitToTableRow` |
| `jsonb_array_elements(json)` / `jsonb_array_elements_text(json)` | `value` | `IJsonArrayElementsRow` |
| `jsonb_each(json)` / `jsonb_each_text(json)` | `key`, `value` | `IJsonbEachRow` |
| `jsonb_object_keys(json)` | `jsonb_object_keys` | `IJsonObjectKeysRow` |
| `jsonb_path_query(json, jsonpath)` | `jsonb_path_query` | `IJsonPathQueryRow` |
| `ts_stat(query)` | `word`, `ndoc`, `nentry` | `ITsStatRow` |

A JSONPath operand is passed as text through `SqlFunctions.Postgres.jsonpath(path)`, which renders
`cast(path as jsonpath)`.

PostgreSQL replaces the only column name of a scalar-returning function with the alias nextorm always
adds to a derived source, so the dialect wraps those calls in a one-column subquery
(`from (select generate_series from generate_series(...)) as "t1"`); the functions with an explicit
output column (`value`, `key`/`value`, `word`/`ndoc`/`nentry`) are emitted unchanged.

See [Table-valued functions](../13-table-valued-functions.md).

## `TABLESAMPLE`

[`FromOptions.TableSample`](xref:NextORM.Core.FromOptions.TableSample(System.Double,NextORM.Core.TableSampleMethod,System.Nullable{System.Double})) appends a `TABLESAMPLE` modifier to the
query's primary table ([`TableSample`](xref:NextORM.Core.ISqlDialect.TableSample)).
PostgreSQL supports both sampling methods and an optional repeatable seed
(`TableSample`,
`ITableSampleMethods.Render`):

```csharp
var rows = dataContext.From<ISimpleEntity>(o => o.TableSample(10, TableSampleMethod.System, seed: 42))
    .Select(e => e.Id)
    .ToList();
```

```sql
select id from simple_entity tablesample system (10) repeatable (42)
```

[`TableSampleMethod.System`](xref:NextORM.Core.TableSampleMethod.System) renders
`tablesample system (10)` and [`TableSampleMethod.Bernoulli`](xref:NextORM.Core.TableSampleMethod.Bernoulli)
renders `tablesample bernoulli (5)`; other providers reject the modifier at SQL build time. See
[Table sampling](../01-querying-and-projections.md#table-sampling-tablesample).

## Row locking

[`ForUpdate`](xref:NextORM.Core.EntityBuilder`1.ForUpdate) and
[`ForShare`](xref:NextORM.Core.EntityBuilder`1.ForShare) emit a trailing row-locking clause, placed
after `WHERE` and `ORDER BY` ([`Lock`](xref:NextORM.Core.ISqlDialect.Lock),
`ILockRenderer.Render`):

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .Where(e => e.Id > 5)
    .ForUpdate()
    .ToList();
```

```sql
select id from simple_entity where (id > 5) for update
```

`LockMode.Update` renders `for update` and `LockMode.Share` renders `for share`. Both accept a
[`LockWaitMode`](xref:NextORM.Core.LockWaitMode): `ForUpdate(LockWaitMode.SkipLocked)` renders
`for update skip locked` and `ForShare(LockWaitMode.NoWait)` renders `for share nowait` (PostgreSQL 9.5+).
See [Row locking](../01-querying-and-projections.md#row-locking-for-update--for-share).

## Data-modifying CTEs

PostgreSQL is the only provider that accepts a data-modifying statement as a CTE body
(`WITH <name> AS (INSERT ... RETURNING ...)`), gated by
[`SupportsDataModifyingCtes`](xref:NextORM.Core.ISqlDialect.SupportsDataModifyingCtes). Start the scope with
the `With(name, insert)` overload: it returns a [`MutationCteQuery<TResult>`](xref:NextORM.Core.MutationCteQuery`1)
typed by the `RETURNING` projection, and `From(name)` reads those rows with the full operator set:

```csharp
var rows = dataContext
    .With("ins", dataContext.InsertInto<IOrder>()
        .Value(x => x.CustomerId, 7)
        .Returning(x => new { x.Id, x.Total }))
    .From("ins")
    .Where(r => r.Total > 0)
    .Select(r => new { r.Id })
    .ToList();
```

```sql
with ins as (insert into orders (customer_id) values (@p0) returning id, total) select id from ins as "t1"
 where (t1.total > 0)
```

The body may be a `VALUES` insert or an `INSERT ... SELECT`, and the mutation may read an earlier read CTE
(declare it first and use `CteQuery.With(name, insert)`) or feed a main `INSERT ... SELECT`. See
[Data modification (INSERT): Data-modifying CTE](../19-insert-statement.md#data-modifying-cte-postgresql)
for the full set of forms; general read CTEs are in
[Common table expressions](../09-cte.md). Every
other provider rejects `With(name, insert)` at build time with `NotSupportedException`.

## Dynamic record schema

`jsonb_to_record`/`jsonb_to_recordset` are exposed as table-valued functions whose result schema is
declared by the caller's row type and rendered as the alias column-definition list
(`AS x(a int, b text)`). See [Dynamic result schema](../13-table-valued-functions.md#dynamic-result-schema).
The `json_populate_record(set)` variants (which populate a caller-supplied base record instead of a
free-form column list) remain out of scope. See
[Limitations and out-of-scope features](../../advanced/limitations.md).

## See also

* [PostgreSQL provider](../../providers/postgres.md)
* [Provider-specific SQL](overview.md)

---

Source: `src/nextorm.postgres/PostgresDialect.cs`, `src/nextorm.core/Query/SqlFunctions.Postgres.cs`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs`.
