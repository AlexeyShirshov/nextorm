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

See [Arrays (PostgreSQL)](../11-scalar-functions.md#arrays-postgresql). Functions that return an array
are meant to be used inside a query; the row reader cannot materialise an array column yet, so
projecting one directly throws at preparation time.

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

See [JSON and JSONB (PostgreSQL)](../11-scalar-functions.md) and
[JSON support across providers](../18-json.md).

## Ordered-set, regression and boolean aggregates

* `percentile_cont`/`percentile_disc` are ordered-set aggregates rendered as
  `percentile_cont(f) within group (order by x)` ([`SupportsOrderedAggregates`](xref:NextORM.Core.ISqlDialect.SupportsOrderedAggregates),
  [`MakeWithinGroup`](xref:NextORM.Core.ISqlDialect)); `mode()` is the ordered-set mode;
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
`ts_rank`, `ts_headline` and the `@@` match operator. `tsvector`/`tsquery` are represented as `string`
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

See [Scalar functions](../11-scalar-functions.md).

## `DISTINCT ON`

[`DistinctOn`](xref:NextORM.Core.EntityBuilder`1) renders `SELECT DISTINCT ON (expr, ...)`, keeping the
first row of each key ([`SupportsDistinctOn`](xref:NextORM.Core.ISqlDialect.SupportsDistinctOn));
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
[`FromTableFunction`](xref:NextORM.Core.EntityBuilder`1):

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

## Not yet supported

`jsonb_to_record`/`json_populate_record` need a dynamic record schema and are not part of the current
surface. See [Limitations and out-of-scope features](../../advanced/limitations.md).

## See also

* [PostgreSQL provider](../../providers/postgres.md)
* [Provider-specific SQL](overview.md)

---

Source: `src/nextorm.postgres/PostgresDialect.cs`, `src/nextorm.core/Query/SqlFunctions.Postgres.cs`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs`.
