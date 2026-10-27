# PostgreSQL-specific SQL

> PostgreSQL has the richest native type system, so its provider-exclusive surface is a set of functions
> and operators on native types rather than query keywords: array types, `json`/`jsonb`, ordered-set,
> regression and boolean aggregates, the extended scalar/regexp helpers, `DISTINCT ON` and the `unnest`
> table function.

**Prerequisites:** [Querying and projections](../../engine/index.md) · [PostgreSQL provider](../../connecting-providers/postgres.md)

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
select id
 from complex_entity where (id = any(@p0))
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

On a database without a native range type, map a `Range<T>` property to a pair of scalar columns with [`RangeColumns`](../27-range-columns.md); the same operators are then translated over the pair.

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
select id
 from reservation where (during && @p0)
```

`Range<T>` is a `readonly struct` and compares by its PostgreSQL characteristics, so `Range<int>.Empty`
and a `default(Range<int>)` value are both the empty range. The in-memory provider evaluates the whole
surface above — `overlaps`, `range_contains`/`range_contained_by`, `range_union`/`range_intersection`/
`range_difference`, the positional and adjacency operators, the inspection functions and the
constructors — with the same semantics as PostgreSQL (including the `result of range union/difference
would not be contiguous` failure). Timestamp ranges use `DateTime` for `tsrange` and
`DateTimeOffset` for `tstzrange`; `daterange` uses `DateOnly`.

### Multiranges

PostgreSQL multirange types (`int4multirange`, `int8multirange`, `nummultirange`, `tsmultirange`,
`tstzmultirange`, `datemultirange`) are represented as a `Range<T>[]` — bound and read as a
`NpgsqlRange<T>[]`. The array is canonical: sorted by the lower bound with overlapping and adjacent
ranges merged. Map a property or parameter directly to `Range<T>[]`; the operator names above are
overloaded for multiranges (`overlaps`, `range_contains`, `range_contained_by`, `range_union`,
`range_intersection`, `range_difference`, `range_adjacent` and the positional operators), alongside
`multirange(range)`, `range_merge`, the inspection functions and the `range_agg` /
`range_intersect_agg` aggregates:

```csharp
var window = new[] { new Range<int>(15, 25), new Range<int>(40, 50) };

var rows = dataContext.From<IReservation>()
    .Where(e => SqlFunctions.Postgres.overlaps(e.During, window))
    .Select(e => new
    {
        All = SqlFunctions.Postgres.range_agg(e.During),
        Common = SqlFunctions.Postgres.range_intersect_agg(e.During)
    })
    .ToList();
```

## `json` and `jsonb`

PostgreSQL is the only provider with native `json`/`jsonb` types. JSON operands are native JSON columns
(a `[JsonColumn]` property maps a CLR object to `jsonb` on read and write), other JSON functions, or
parameters whose runtime value is a `JsonDocument`/`JsonElement`/`JsonNode` (which Npgsql binds as
`jsonb`). A bare `JsonNode`/`JsonNode?` property (declared exactly `JsonNode`, not `JsonObject`/`JsonArray`)
reads a native `json`/`jsonb` column for object, array and scalar roots, in scalar and composite
projections; SQL `NULL` and a JSON literal `null` both become CLR `null`. Declared `JsonObject`/`JsonArray`
properties and a bare `JsonDocument`/`JsonElement` *scalar* projection remain outside this support. The
`jsonb_*` family covers construction, extraction, containment and
aggregation, for example `jsonb_build_object`, `jsonb_agg`, `json_get_text`, `json_cast`, `->`, `->>`.

The four accessors `json_get`, `json_get_text`, `json_get_path` and `json_get_path_text` (string-key and
integer-index overloads) apply **contextual SQL typing**: a raw client document value (literal, captured
value or explicit parameter) is parsed as `jsonb` in the generated SQL — `json_get("{\"a\":1}", "a")`
renders `cast('{"a":1}' as jsonb) -> cast('a' as text)` — while a native `json`/`jsonb` column or a
server-side JSON expression is emitted unchanged (`doc -> cast('a' as text)`), so its operand and result
types are preserved. The key/index/path operand is typed per overload as `text`/`integer`/`text[]`.
Provider parameter binding is not changed: an ordinary `string` parameter still binds as `text`, and
`JSON_EXISTS`/`jsonpath` and the sibling JSON functions keep their own behavior.

```csharp
var json = dataContext.From<IComplexEntity>()
    .Select(e => SqlFunctions.Postgres.jsonb_agg(e.String))
    .First();
```

```sql
select jsonb_agg(somestring)
 from complex_entity
```

See [JSON and JSONB (PostgreSQL)](../../scalar-functions/index.md) and
[JSON support across providers](../16-json.md).

## Ordered-set, regression and boolean aggregates

* `percentile_cont`/`percentile_disc` are ordered-set aggregates rendered as
  `percentile_cont(f) within group (order by x)` ([`SupportsOrderedAggregates`](xref:NextORM.Core.ISqlDialect.SupportsOrderedAggregates),
  [`MakeWithinGroup`](xref:NextORM.Core.ISqlDialect.MakeWithinGroup(System.String,System.String,NextORM.Core.KeywordCase))); `mode()` is the ordered-set mode;
* `median(() => x)` is the same surface with the fraction fixed at `0.5`
  (`percentile_cont(0.5) within group (order by x)`, [`SupportsOrderedPercentiles`](xref:NextORM.Core.ISqlDialect.SupportsOrderedPercentiles)):
  PostgreSQL has no `median` function, and this form is an **aggregate** (one row per group), not a window
  function, so it never takes `OVER`;
* the continuous forms interpolate and return `double precision`, so `percentile_cont`/`median` require a
  floating-point sort expression (an integer/numeric key is refused); `percentile_disc` keeps the sort type.
  Fractions are `double` in `[0, 1]`, finite and constant/captured, and are validated before rendering; the
  generic `percentile` is refused;
* `bool_and`/`bool_or`/`every`, the `regr_*` regression family and `bit_and`/`bit_or`/`bit_xor` are
  PostgreSQL-only and are rejected by every other dialect;
* `array_agg` and the string/array aggregate surface are gated by
  [`SupportsStringArrayAggregates`](xref:NextORM.Core.ISqlDialect.SupportsStringArrayAggregates).

See [Grouping and aggregates](../05-grouping-and-aggregates.md).

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
select id, ts_rank(to_tsvector(somestring), to_tsquery('cat'))
 from complex_entity
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

The residual string catalogue of #281 sits on the same `Postgres` surface, one member per server
overload: `btrim` (one-argument, character-set and `bytea` forms), the two-argument and `bytea` forms
of `ltrim`/`rtrim`, `starts_with`, `parse_ident`, the `text` overloads of
`quote_ident`/`quote_literal`/`quote_nullable`, `to_ascii(value, encoding)` (encoding named as text or
as its PostgreSQL code),
`to_hex`/`to_oct`/`to_bin` (integer and `bigint`), `convert_from`/`convert_to`, `encode`/`decode`,
`normalize`, `unistr`, `regexp_match` (two- and three-argument) and the set-returning
`string_to_table`. Each of these names is gated **individually** by
[`IScalarFunctions.Supports`](xref:NextORM.Core.IScalarFunctions.Supports(System.String)) rather than
by the family flag: a provider that does not implement the interface fails closed with
`NotSupportedException`, and PostgreSQL SQL never reaches it. `normalize` takes its form as a bare
grammar keyword (`NFC`/`NFD`/`NFKC`/`NFKD`), `regexp_match` returns NULL when nothing matches, and
`to_hex` spells a negative `int` in two's complement (`-1` → `ffffffff`). Members that cannot run on
the pinned PostgreSQL 17.11 — `casefold`, the one-argument `to_ascii` and the `anyelement` `quote_*`
overloads — are refused while the query is prepared; the tested reasons are in the refusal table of
[String functions](../../scalar-functions/01-string-functions.md#tested-refusals).

The residual date/time family of #281 follows the same rule: `date_subtract` (the two-argument
`interval` form and the zoned three-argument form, both returning `timestamp with time zone`),
`justify_interval`, `make_timestamptz` (with and without a zone), the clocks
`clock_timestamp`/`statement_timestamp`/`transaction_timestamp`, `timeofday` and `isfinite` over
`date`, `timestamp` and `interval`. `clock_timestamp` is evaluated per row while
`statement_timestamp`/`transaction_timestamp` stay fixed for the statement/transaction, and
`justify_interval` returns an `interval`, so it follows the provider's interval handling described
below. Each name is gated **individually** by `IScalarFunctions.Supports` and fails closed with
`NotSupportedException` on a provider without the interface; `pg_sleep`/`pg_sleep_for`/`pg_sleep_until`
are refused because each returns `void` and no faithful expression result exists — the refusal rows are
in [Date and time functions](../../scalar-functions/03-date-and-time.md#postgresql-extended-date-and-time).

UUID extraction is PostgreSQL-only as well: `uuid_extract_timestamp` yields a `timestamptz` (NULL for a
version-4 UUID or the nil UUID) and `uuid_extract_version` yields a `smallint` (NULL for the nil UUID),
both gated per name by `IScalarFunctions.Supports` with the same fail-closed refusal — see
[Sequences and UUID generators](../../scalar-functions/10-sequences.md#uuid-generators).

Next to `encode`/`decode`, the binary-string family adds `get_bit`/`set_bit` (bit indexes counted from
the least significant bit of the first byte), `get_byte`/`set_byte` (zero-based byte indexes) and
`bit_count` (set bits as `bigint`), each gated individually by `IScalarFunctions.Supports` and failing
closed the same way. The `bit`-typed spellings have no CLR mapping and are refused, and
`crc32`/`crc32c` are PostgreSQL 18 functions refused on the pinned 17.11; both reasons are in the
refusal table of [String functions](../../scalar-functions/01-string-functions.md#tested-refusals).

On the mapped-column path the duration rule follows the **returned** interval, never the function name:
`make_interval`, a `justify_*` normalization and `age` all reject a result carrying a non-zero month
component (either sign) with `InvalidCastException` naming calendar months, and convert a zero-month
result exactly.

See [Scalar functions](../../scalar-functions/index.md).

## Numeric scale functions

`SqlFunctions.Postgres.scale`/`min_scale`/`trim_scale` describe and normalize the scale of a `numeric`
value. Each name is gated **individually** by
[`IScalarFunctions.Supports`](xref:NextORM.Core.IScalarFunctions.Supports(System.String)) — not by a
family-wide flag — and every other provider rejects the call with `NotSupportedException`, so
PostgreSQL SQL never reaches a server that cannot run it.

| C# | SQL | Returns |
|---|---|---|
| `SqlFunctions.Postgres.scale(decimal? value)` | `scale(value)` | `int?` — fractional digits, trailing zeroes included |
| `SqlFunctions.Postgres.min_scale(decimal? value)` | `min_scale(value)` | `int?` — fractional digits without the trailing zeroes |
| `SqlFunctions.Postgres.trim_scale(decimal? value)` | `trim_scale(value)` | `decimal?` — the value with its trailing zeroes removed |

`scale` exists on every supported PostgreSQL version; `min_scale`/`trim_scale` need PostgreSQL 13 or
newer. A NULL argument yields NULL for all three. `trim_scale` removes trailing zeroes only: unlike
`round`/`trunc` it never rounds and never changes a significant digit, so `trim_scale(12.3400)` is
`12.34` (decimal scale 2) while `trim_scale(12.3456)` is unchanged at scale 4. The value travels
through Npgsql's numeric reader, so the returned decimal keeps the server-side scale.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Scale = SqlFunctions.Postgres.scale(e.Numeric),
        MinScale = SqlFunctions.Postgres.min_scale(e.Numeric),
        Trimmed = SqlFunctions.Postgres.trim_scale(e.Numeric)
    })
    .ToList();
```

Next to the scale helpers, `SqlFunctions.Postgres` exposes the residual numeric family: `div`
(truncating `numeric` division, so `div(7, 2)` is `3` and `div(-7, 2)` is `-3`), `erf`/`erfc`, the
two-argument `random_normal(mean, stddev)` and the degree-based trigonometry
`acosd`/`asind`/`atand`/`atan2d`/`cosd`/`cotd`/`sind`/`tand`, which take and return degrees. Every one
of these names — like the three scale helpers — is gated individually by
`IScalarFunctions.Supports`, so any other provider rejects the call with `NotSupportedException`.
`gamma`/`tgamma`/`lgamma` are refused because they do not exist on the pinned PostgreSQL 17.11; the
tested reasons for that and for the refused string members are listed in the refusal tables of the
scalar-functions articles.

See [Math functions](../../scalar-functions/02-math-functions.md#postgresql-numeric-scale).

## Null-safe comparison

PostgreSQL has the ANSI operators natively, so both helpers map one-to-one and the result is a plain
boolean: `SqlFunctions.Sql.is_not_distinct_from(a, b)` renders `(a is not distinct from b)` and
`is_distinct_from(a, b)` renders `(a is distinct from b)`. Both are two-valued, so no scalar
legalisation is needed; ordinary `==`/`!=` keep their SQL NULL semantics. See
[Null-safe comparison](../../scalar-functions/04-conditionals-and-conversion.md#null-safe-comparison-is-not-distinct-from).

## `DISTINCT ON`

[`DistinctOn`](xref:NextORM.Postgres.PostgresEntityBuilderExtensions.DistinctOn``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}})) renders `SELECT DISTINCT ON (expr, ...)`, keeping the
first row of each key ([`DistinctOn`](xref:NextORM.Core.ISqlDialect.DistinctOn));
mutually exclusive with `Distinct`. It is an extension method in the `NextORM.Postgres` namespace; add
`using NextORM.Postgres;` to use it.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .DistinctOn(e => e.String)
    .OrderBy(e => e.String)
    .Select(e => new { e.Id, e.String })
    .ToList();
```

```sql
select distinct on (somestring) id, somestring
 from complex_entity order by somestring
```

See [Distinct](../09-distinct.md).

## Set-returning functions

PostgreSQL's set-returning functions are exposed as table-valued functions through
[`From`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,System.Linq.Expressions.Expression{System.Func{NextORM.Core.EntityBuilder{``0}}})) with a runtime-factory call:

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
select id
 from simple_entity tablesample system (10) repeatable (42)
```

[`TableSampleMethod.System`](xref:NextORM.Core.TableSampleMethod.System) renders
`tablesample system (10)` and [`TableSampleMethod.Bernoulli`](xref:NextORM.Core.TableSampleMethod.Bernoulli)
renders `tablesample bernoulli (5)`; other providers reject the modifier at SQL build time. See
[Table sampling](../../guide/01-query-sources.md#table-sampling-tablesample).

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
select id
 from simple_entity where (id > 5) for update
```

`LockMode.Update` renders `for update` and `LockMode.Share` renders `for share`. Both accept a
[`LockWaitMode`](xref:NextORM.Core.LockWaitMode): `ForUpdate(LockWaitMode.SkipLocked)` renders
`for update skip locked` and `ForShare(LockWaitMode.NoWait)` renders `for share nowait` (PostgreSQL 9.5+).
See [Row locking](../../guide/31-provider-specific-features.md#row-locking-for-update--for-share).

For optimistic concurrency, PostgreSQL's `xmin` system column is available too: map it to a `uint`
property marked computed and compare it in `Where` (equality only) — see
[Optimistic concurrency and change tracking](../26-optimistic-concurrency.md#postgresql-xmin).

The same `uint` mapping reads PostgreSQL's `oid` and other `uint`-backed system types (for example
`cid`) into a CLR `uint` property: Npgsql surfaces them as `uint`, so materialising one works. Only
comparison and write parameters are limited: nextorm binds a `uint` parameter as `xid`, and `oid = xid`
(or `cid = xid`) has no operator, so against a non-`xid` target it fails with SQLSTATE 42883. Compare
through a `bigint` cast — `Where(e => (long)e.Oid == (long)value)` — or run raw SQL with a `bigint`
parameter and an explicit `::oid` cast.

## Data-modifying CTEs

PostgreSQL is the only provider that accepts a data-modifying statement as a CTE body
(`WITH <name> AS (INSERT ... RETURNING ...)`), gated by
[`SupportsDataModifyingCtes`](xref:NextORM.Core.ISqlDialect.SupportsDataModifyingCtes). Start the scope with
the `With(name, insert)` overload: it returns a [`MutationCteQuery<TResult>`](xref:NextORM.Core.MutationCteQuery`1)
typed by the `RETURNING` projection, and `From(name)` reads those rows with the full operator set:

```csharp
var rows = dataContext
    .With("ins", dataContext.CreateInsertBuilder<IOrder>()
        .Value(x => x.CustomerId, 7)
        .Returning(x => new { x.Id, x.Total }))
    .From("ins")
    .Where(r => r.Total > 0)
    .Select(r => new { r.Id })
    .ToList();
```

```sql
with ins as (insert into orders (customer_id) values (@p0) returning id, total) select id
 from ins as "t1"
 where (t1.total > 0)
```

The body may be a `VALUES` insert or an `INSERT ... SELECT`, and the mutation may read an earlier read CTE
(declare it first and use `CteQuery.With(name, insert)`) or feed a main `INSERT ... SELECT`. See
[Data modification (INSERT): Data-modifying CTE](../17-insert-statement.md#data-modifying-cte-postgresql)
for the full set of forms; general read CTEs are in
[Common table expressions](../10-cte.md). Every
other provider rejects `With(name, insert)` at build time with `NotSupportedException`.

## Raw rows and composites

A raw statement that projects a single PostgreSQL record column — an anonymous `ROW(...)` or a
caller-registered named composite — can be materialised into the matching `System.Tuple<...>` or the named
CLR type. This is the PostgreSQL counterpart of the ClickHouse `tuple(...)` result; see
[Raw SQL](../14-raw-sql.md#postgresql-raw-rows-and-composites) for the full shape matrix, the
configuration and the error contract.

```csharp
using var result = dataContext.ExecuteRaw(
    "select row(id, somestring) as r from complex_entity order by id");

var rows = result.Read<System.Tuple<int, string?>>();
// rows[0].Item1 == 1, rows[0].Item2 == "..."
```

An anonymous `ROW(...)` needs no registration; a named composite must be registered on the caller-owned
`NpgsqlDataSource` (`MapComposite<EntityRow>()`) and the context created from `dataSource.CreateConnection()`.
Only a single record column is supported. Unsupported shapes — a `ValueTuple`, arity ≥8, a nested or empty
`ROW`, several or mixed record columns, an unregistered composite and a composite declared as
`System.Tuple` — throw `NotSupportedException`; a `NULL` in a field declared as a non-nullable value type
throws `InvalidOperationException` instead of substituting `default`. See
[Raw SQL](../14-raw-sql.md#postgresql-raw-rows-and-composites).

Composite classification is authoritative: a column is treated as a named composite only when the loaded Npgsql type catalog says so, never from a dotted data type name or an exception shape. On a clean catalog an unmapped non-composite type (for example `hstore` or `ltree`) now yields the ordinary "None of the result-set columns (...) matches a mapped property" error rather than the named-composite diagnostic; a genuine composite whose type is not in the loaded catalog degrades to the same ordinary error until authoritative type metadata is available (a deliberate trade-off — a cold catalog gives no composite verdict instead of a misleading one). A composite registered through `MapComposite<T>` still materialises, and a warm-catalog unregistered composite still reports the composite-specific guard (it must be registered before it can be read). nextorm contexts share a process-wide implicit Npgsql data source keyed by connection string; a type reload must target the source associated with the same connection string used by the context.

Provider authors control composite classification through the protected `DataContext.IsGenuineCompositeColumn(DbDataReader, int)` seam — the single predicate the raw-row mapper consults on both the single- and multi-column paths. The base implementation returns `false` (no authoritative composite verdict), mirroring `SupportsRawRowColumns`; `PostgresDataContext` overrides it with `NpgsqlDataReader.GetPostgresType(ordinal) is PostgresCompositeType`.

## Dynamic record schema

`jsonb_to_record`/`jsonb_to_recordset` are exposed as table-valued functions whose result schema is
declared by the caller's row type and rendered as the alias column-definition list. PostgreSQL
**requires** that column definition list, for example `AS x(a int, b text)`:

```sql
select a, b
 from jsonb_to_recordset(@json) as "t1"(a integer, b text)
```

A free/partial column list **without** a caller-declared `TRow` is not supported for these two
functions. Schema-less scenarios are covered by the existing paths instead:

* a `[DynamicColumns]` store (`Dictionary<string, object?>`) captures the row's unmapped columns —
  see [Dynamic columns](../29-dynamic-columns.md);
* the raw reader path — [`ResultSet`](../14-raw-sql.md#multiple-result-sets) columns are read by
  `FieldCount`/`ColumnNames` without materialisation, and [`ToDataReader`](../28-large-objects.md)
  streams them as a `DbDataReader`;
* `jsonb_each` / `jsonb_each_text` / `jsonb_object_keys` expand a JSON object into key/value rows —
  see [Result schema and provider restrictions](../13-tvf-result-schemas.md#dynamic-result-schema).

The `json_populate_record(set)` variants (which populate a caller-supplied base record instead of a
free-form column list) remain out of scope. See
[Limitations and out-of-scope features](../../advanced/limitations.md).

## See also

* [PostgreSQL provider](../../connecting-providers/postgres.md)
* [Provider-specific SQL](overview.md)

---

Source: `src/nextorm.postgres/PostgresDialect.cs`, `src/nextorm.core/Query/SqlFunctions.Postgres.cs`,
`tests/nextorm.postgres.tests/SqlGeneration/SqlGenerationTests.cs`.
