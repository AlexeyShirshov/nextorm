# Table-valued functions

> Declare a database table-valued function as a runtime descriptor and query it as a `FROM`, join or
> APPLY source with `ctx.From(() => Db.MyTvf(...))`, mapping its rows like any other entity.

**Prerequisites:** [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Joins](04-joins.md) · [Grouping and aggregates](05-grouping-and-aggregates.md)

## Overview

A table-valued function (TVF) source is an ordinary static factory method that returns a
[`TvfEntityBuilder<T>`](xref:NextORM.Core.TvfEntityBuilder`1) descriptor:

```csharp
public static TvfEntityBuilder<ITvfRow> AllRows() => new("all_rows");
public static TvfEntityBuilder<ITvfRow> ById(long id) => new("rows_by_id");
public static TvfEntityBuilder<ITvfRow> Between(long lo, long hi) => new("rows_between", "app");
```

The scalar parameters of the factory declare only the **argument positions** of the SQL call; the name,
schema, clauses, verbatim indices and result schema live in the descriptor. The source is always
supplied as an **expression**, so the call node — and with it the arguments — reaches the SQL builder:

```csharp
var values = dataContext
    .From(() => Tvf.JsonEach("[1,2,3]"))
    .Select(r => r.Value)
    .ToList();
// 1, 2, 3
```

```sql
-- SQLite
select value
 from json_each('[1,2,3]')
-- SQL Server / PostgreSQL append an alias
select value
 from json_each('[1,2,3]') as [t1]
```

The descriptor is context-independent: it carries no `DataContext`, composes without one and is bound to
a context only when it is consumed by `From`, a join or an APPLY. nextorm only emits the call — the
function must already exist in the target database.

The `Output:` tables below show the rows returned by each example against the integration-test seed data (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Output:

| Value |
|-------|
| 1     |
| 2     |
| 3     |

### Breaking change in 1.0.9-rc4

The previous static surface was removed **without a compatibility shim**:

* the `[SqlTableFunction]` attribute and the `IQueryable<T>`-returning stub methods it annotated;
* the table-function `From`/`CreateQueryBuilder…From` entry points that took
  `Expression<Func<IQueryable<T>>>`.

Every table-valued function is now a runtime factory as described below. Migrating an existing
declaration replaces the attribute and the stub body with the descriptor, and the call site with a lambda
source:

```csharp
// 1.0.9-rc4
public static TvfEntityBuilder<ITvfRow> ById(long id) => new("rows_by_id");

var rows = dataContext
    .From(() => Tvf.ById(id))
    .Select(r => new { r.Id })
    .ToList();
```

The `[SqlTableFunction]` metadata (name, schema, `WithClause`, `CallClause`, verbatim indices, result
schema) moves into the descriptor constructor unchanged; see [Declaring a runtime factory](#declaring-a-runtime-factory).

## Declaring a runtime factory

The descriptor has three constructors: `TvfEntityBuilder(string name)`, `TvfEntityBuilder(string name,
string? schema)` and the full metadata constructor:

```csharp
public TvfEntityBuilder(
    string name,
    string? schema,
    string? withClause,
    string? callClause,
    IReadOnlyList<int> verbatimArguments,
    TableFunctionSchema resultSchema);
```

| Member | Rendered as |
|---|---|
| `Name` | the SQL function name |
| `Schema` | optional `schema.name` prefix |
| `WithClause` | a trailing `WITH (...)` body appended after the call |
| `CallClause` | verbatim SQL appended inside the call parentheses, after the arguments |
| `VerbatimArguments` | argument indices emitted unquoted as SQL identifiers instead of parameters |
| `ResultSchema` | where the caller-declared result schema is rendered (see [Result schema and provider restrictions](13-tvf-result-schemas.md)) |

`name` is required and must not be blank; `verbatimArguments` is never `null` ("no verbatim argument" is
an empty list) and must contain distinct, non-negative indices — a blank name, a negative or a duplicate
index throws `ArgumentException`, a `null` name or list throws `ArgumentNullException`. The list is
defensively copied and the metadata is constructor-only, so a later change to the caller's collection
cannot mutate a descriptor or an already-built query.

The row shape is a normal entity, mapped with `[Column]` like any other DTO:

```csharp
public interface ITvfRow
{
    [Column("id")]
    long Id { get; set; }
    [Column("value")]
    string? Value { get; set; }
}

public interface IJsonEachRow
{
    [Column("key")]
    long Key { get; set; }
    [Column("value")]
    long Value { get; set; }
}
```

The remaining options mirror the provider forms they emit:

```csharp
public static class Tvf
{
    // schema-qualified: app.rows_between(a, b)
    public static TvfEntityBuilder<ITvfRow> Between(long lo, long hi) => new("rows_between", "app");

    // SQL Server OPENJSON ... WITH (...)
    public static TvfEntityBuilder<IOpenJsonTypedRow> OpenJsonTyped(string json) =>
        new("openjson", null, "name nvarchar(50) '$.name', age int '$.age'", null, [], TableFunctionSchema.None);

    // MySQL/MariaDB JSON_TABLE: the schema sits inside the parentheses
    public static TvfEntityBuilder<IJsonTableRow> JsonTable(string doc) =>
        new("json_table", null, null,
            ", '$[*]' columns(id int path '$.id', name varchar(50) path '$.name')", [], TableFunctionSchema.None);

    // SQL Server CONTAINSTABLE: arguments 0 and 1 are identifiers, not values
    public static TvfEntityBuilder<SqlFunctions.IKeyRankRow<int>> ContainsTable(string table, string column, string search) =>
        new("containstable", null, null, null, [0, 1], TableFunctionSchema.None);
}
```

A verbatim argument that is not a constant string, or an index that does not address an existing
argument of the factory call, fails explicitly — it is never emitted as a value.

## Metadata and factory purity

The descriptor is materialised from the factory **call node**, not from a value:

* the factory is invoked once with a typed `default` in every argument position, only to read its
  metadata — the caller's argument expressions are never evaluated for this;
* the metadata (name, schema, clauses, verbatim indices, result schema) therefore **must not depend on
  the argument values**; a factory is a declaration, not an expression-language evaluator;
* captured configuration outside the SQL arguments (a static name, a schema constant) is allowed and is
  read when the source is lowered, so it participates in the source identity;
* the number of factory invocations is not guaranteed, so a factory must be free of side effects;
* the original call keeps its arguments: no argument is replaced by the metadata-projection default.

```csharp
// valid: the argument only names a position; the metadata is constant
public static TvfEntityBuilder<IRow> ById(long id) => new("rows_by_id");

// not valid: the metadata depends on an argument value
public static TvfEntityBuilder<IRow> ById(long id) => new(id > 0 ? "rows_pos" : "rows_all");
```

## Arguments stay SQL expressions

Because the source is a lambda, every argument is lowered by the regular expression visitor: a literal is
inlined as a SQL literal, a capture becomes a bound parameter, `null`/`default` and nested SQL
expressions are preserved, and an outer-row member stays an outer-column reference.

```csharp
var id = 5L;

var prepared = dataContext
    .From(() => Tvf.ById(id))
    .Select(r => new { r.Id })
    .Prepare();
```

```sql
-- SQLite placeholder; SQL Server/PostgreSQL use @id
select id
 from rows_by_id($id)
```

A schema-qualified name with two arguments renders as `app.rows_between(...)`:

```csharp
var lo = 1L;
var hi = 3L;

var prepared = dataContext
    .From(() => Tvf.Between(lo, hi))
    .Select(r => new { r.Id })
    .Prepare();
```

```sql
select id
 from app.rows_between($lo, $hi)
```

The argument **shape** is part of the SQL identity and of the plan key, the argument **value** is not: a
changed capture reuses the cached plan, re-renders nothing and refreshes only its parameter slot, while a
literal is part of the statement text and therefore a different literal is a different plan.

```csharp
var id = 1L;
QueryCommand<long> Build() => dataContext.From(() => Tvf.ById(id)).Select(r => r.Id);

var first = Build().Prepare();   // id = 1
id = 2;
var second = Build().Prepare();  // same cached plan, parameter refreshed to 2
```

Multiple sources refresh independently — with a TVF in `FROM` and another as the join source, only the
changed capture is rebound and the other slot keeps its value. A shared `QueryCommand` on the context is
never mutated to achieve this (no sticky `Cache = false`), so the plan cache stays enabled for every
later query on the same context.

## Querying a function

`ctx.From(() => ...)` is the only entry point: the lambda carries the factory call, and the descriptor
returned by a direct call (`ctx.From(Tvf.ById(5))`) has lost that call — see
[Unbound chains and captured descriptors](#unbound-chains-and-captured-descriptors).

An ordinary builder chain composes inside the lambda and reaches the same statement. Any public
builder-returning operation with preserved lambda or constant arguments is re-applied in its original
order; the factory call is never mistaken for the outermost operation:

```csharp
var rows = dataContext
    .From(() => Tvf.JsonEach("[3,1,2]").Where(r => r.Value > 1).OrderByDescending(r => r.Value))
    .Select(r => r.Value)
    .ToList();
// 3, 2
```

```sql
-- SQLite
select value
 from json_each('[3,1,2]')
where (value > 1) order by value desc
```

Grouping and aggregation work the same way:

```csharp
var rows = dataContext
    .From(() => Tvf.JsonEach("[1,1,2]"))
    .GroupBy(r => new { r.Value })
    .Select(g => new { g.Value, Cnt = SqlFunctions.Sql.count() })
    .ToList();
// (1, 2), (2, 1)
```

Output:

| Value | Cnt |
|-------|-----|
| 1     | 2   |
| 2     | 1   |

## Joins

`Join`, `LeftJoin`, `RightJoin`, `FullJoin` and `CrossJoin` accept a runtime source as an
`Expression<Func<EntityBuilder<T>>>` through the
[`TvfJoinSourceExtensions`](xref:NextORM.Core.TvfJoinSourceExtensions) extension methods. The family
covers the generic, non-generic and joined receivers at arities 2–8, with and without a
`JoinOptions` callback:

```csharp
var rows = dataContext
    .From<ISimpleEntity>()
    .Join(() => Tvf.AllRows(), (s, r) => r.Id == s.Id)
    .Select(p => new { p.Item1.Id, p.Item2.Value })
    .ToList();
```

```sql
-- SQLite
select t1.id, t2.value
 from simple_entity as 't1'
 join all_rows() as 't2' on t2.id = cast(t1.id as bigint)
-- SQL Server / PostgreSQL differ only in identifier quoting ([t2] / "t2")
```

The `cast` is the regular join-time type unification: `Tvf.AllRows().Id` is `long` while
`ISimpleEntity.Id` is `int`. Because these are extension methods, a call that passes an entity, a CTE or
a derived-query builder keeps binding to the existing instance overloads unchanged.

## Applying a function

[`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) and [`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) accept a correlated lambda whose body is the runtime factory call. The outer parameter is never evaluated in CLR: its member reads become SQL column references, and the call is applied once per left-hand row.

```csharp
var rows = dataContext
    .From<ISimpleEntity>()
    .CrossApply(s => Tvf.ById(s.Id))
    .Select(p => new { p.Item1.Id, p.Item2.Value })
    .ToList();
```

```sql
-- SQL Server
select t1.id, t3.value
 from simple_entity as [t1]
 cross apply (select t2.id, t2.value
 from rows_by_id(cast(t1.id as bigint)) as [t2]) as [t3]
-- PostgreSQL
select t1.id, t3.value
 from simple_entity as "t1"
 cross join lateral (select t2.id, t2.value
 from rows_by_id(cast(t1.id as bigint)) as "t2") as "t3"
```

`OuterApply` is the left-preserving twin: it keeps left-hand rows for which the function returns no
rows, projecting `null`s. Applying any source needs a lateral-capable provider — SQLite, ClickHouse,
MariaDB 11.4 (except the direct `JSON_TABLE` form below) and the in-memory provider reject it
([`SupportsApply`](xref:NextORM.Core.ISqlDialect.SupportsApply)).

A **correlated `JSON_TABLE`** is special-cased on MySQL and MariaDB: the function correlates implicitly
in `FROM`, so it is rendered directly as the join source — without a derived wrapper and without a
`LATERAL` keyword (MySQL rejects an outer reference inside a table function under `LATERAL`, and MariaDB
has no `LATERAL` at all):

```sql
-- MySQL / MariaDB: CROSS APPLY / OUTER APPLY
select t1.id, jt.child_id
 from parent as `t1`
 join json_table(t1.children_json, '$[*]' columns(child_id int path '$.child_id')) as jt
-- OUTER APPLY is `left join json_table(...) as jt on true`
```

An uncorrelated function is passed with an empty parameter list and applied once:

```csharp
var rows = dataContext
    .From<ISimpleEntity>()
    .CrossApply(() => Tvf.AllRows())
    .Select(p => new { p.Item1.Id, p.Item2.Value })
    .ToList();
```

```sql
-- SQL Server
select t1.id, t2.value
 from simple_entity as [t1]
 cross apply all_rows() as [t2]
-- PostgreSQL / MySQL 8.0.14+
select t1.id, t2.value
 from simple_entity as "t1"
 cross join lateral all_rows() as "t2"
```

An uncorrelated source, another function, or a source whose derived query filters, groups, orders, pages
or renames columns keeps the derived rendering (`cross join lateral (select ... from fn(...) as t2) as t3`)
on those providers.

Applying any source needs a lateral-capable provider — SQLite, ClickHouse, MariaDB 11.4 (except the
direct `JSON_TABLE` form above) and the in-memory provider reject a correlated APPLY with
`NotSupportedException`
([`SupportsApply`](xref:NextORM.Core.ISqlDialect.SupportsApply)). Applying an uncorrelated function
directly (`.CrossApply(() => Tvf.AllRows())`) is an ordinary join on such a provider and is only rejected
where the provider rejects the join form itself.

## Composing, cloning and reuse

A descriptor is a builder: `Where`, `OrderBy`, paging, joins and a type-changing `Select`/`As` compose on
it without a context, and the same chain works inside the `From` lambda (see
[Querying a function](#querying-a-function)). The factory row type and the composed result type are
tracked separately, so a projection to another DTO keeps the source and its arguments:

```csharp
var builder = dataContext.From(() => Tvf.ById(id).As(row => new RowDto { Id = row.Id }));
```

The same applies to a typed reference, a clone and an upcast to `EntityBuilder<T>`: the TVF semantics
follow the builder, not the static type of the variable. A descriptor may be shared between queries and
contexts; each consumption binds it independently and no value leaks between contexts.

### Unbound chains and captured descriptors

A descriptor and its chain are unbound on purpose — no placeholder context is created and none is ever
written into the descriptor:

```csharp
var descriptor = Tvf.ById(5);
descriptor.Where(r => r.Id > 0).ToCommand();
// InvalidOperationException: The builder composes an unbound table-valued-function source and carries
// no data context; consume it with ctx.From(...) before any context-dependent operation.
```

A **captured descriptor** — a variable holding the result of a factory call — is rejected by name,
because the original call node and therefore the argument provenance are gone:

```csharp
var descriptor = Tvf.ById(5);
var act = () => dataContext.From(() => descriptor);
// NotSupportedException: The source expression must be a direct call to a runtime
// table-valued-function factory, ... A captured descriptor loses the original argument provenance
// and cannot be lowered.
```

Any other shape is rejected explicitly instead of guessed: a method that does not return
`TvfEntityBuilder<T>` ("does not return TvfEntityBuilder<T>"), a builder operation that is not part of
the supported composition spine, or a factory returning `null`.

## Built-in table functions

A few common table-valued functions are pre-declared as `TvfEntityBuilder<T>` factories on
`SqlFunctions.*`, so no custom factory is needed. Their support is gated per provider
([`SupportsTableFunction`](xref:NextORM.Core.ISqlDialect.SupportsTableFunction(System.String))), unlike a
custom factory, which is emitted verbatim.

`SqlFunctions.Postgres.generate_series` and `SqlFunctions.Postgres.unnest` are PostgreSQL (they return [`SqlFunctions.IGenerateSeriesRow`](xref:NextORM.Core.SqlFunctions.IGenerateSeriesRow)
with the `generate_series` column and [`SqlFunctions.IUnnestRow<T>`](xref:NextORM.Core.SqlFunctions.IUnnestRow`1) with the `unnest` column):

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

PostgreSQL also ships the regexp, JSON and text-search set-returning functions:

| `SqlFunctions.Postgres.*` | SQL output columns | Row shape |
| --- | --- | --- |
| `regexp_matches(source, pattern[, flags])` | `regexp_matches text[]` | `IRegexpMatchesRow` (`.Matches`) |
| `regexp_split_to_table(source, pattern[, flags])` | `regexp_split_to_table` | `IRegexpSplitToTableRow` (`.Value`) |
| `jsonb_array_elements(json)` / `jsonb_array_elements_text(json)` | `value` | `IJsonArrayElementsRow` (`.Value`) |
| `jsonb_each(json)` / `jsonb_each_text(json)` | `key`, `value` | `IJsonbEachRow` (`.Key`, `.Value`) |
| `jsonb_object_keys(json)` | `jsonb_object_keys` | `IJsonObjectKeysRow` (`.Key`) |
| `jsonb_path_query(json, jsonpath)` | `jsonb_path_query` | `IJsonPathQueryRow` (`.Value`) |
| `ts_stat(query)` | `word`, `ndoc`, `nentry` | `ITsStatRow` (`.Word`, `.Ndoc`, `.Nentry`) |
| `jsonb_to_record(json)` / `jsonb_to_recordset(json)` | declared by `TRow` | `TRow` (see [Dynamic result schema](13-tvf-result-schemas.md#dynamic-result-schema)) |

```csharp
var json = JsonDocument.Parse("""{"a":1,"b":2}""");

var entries = dataContext
    .From(() => SqlFunctions.Postgres.jsonb_each_text(json))
    .Select(r => new { r.Key, r.Value })
    .ToList();
// ("a", "1"), ("b", "2")
```

A JSONPath operand is passed as text through `SqlFunctions.Postgres.jsonpath(path)`, which renders
`cast(path as jsonpath)`:

```csharp
var json = JsonDocument.Parse("""{"a":[1,2]}""");
var path = "$.a[*]";

var values = dataContext
    .From(() => SqlFunctions.Postgres.jsonb_path_query(json, SqlFunctions.Postgres.jsonpath(path)))
    .Select(r => r.Value)
    .ToList();
// "1", "2"
```

PostgreSQL renames the only column of a scalar set-returning function (`generate_series`, `unnest`,
`regexp_matches`, `regexp_split_to_table`, `jsonb_object_keys`, `jsonb_path_query`) to the alias nextorm
adds to a derived source, so the dialect wraps those calls in a one-column subquery
(`select generate_series from generate_series(...)`); the functions with an explicit output column
(`value`, `key`/`value`, `word`/`ndoc`/`nentry`) are emitted unchanged.

`SqlFunctions.SqlServer.string_split` is SQL Server 2016+ and returns [`SqlFunctions.IStringSplitRow`](xref:NextORM.Core.SqlFunctions.IStringSplitRow) (the single `value`
column). The fragments are not guaranteed to be ordered, so add an `order by` when the input order
matters:

```csharp
var csv = "a,b,c";
var separator = ",";

var fragments = dataContext
    .From(() => SqlFunctions.SqlServer.string_split(csv, separator))
    .Select(r => r.Value)
    .ToList();
```

```sql
select value
 from string_split(@csv, @separator) as [t1]
```

`SqlFunctions.SqlServer.openjson` is SQL Server 2016+ and returns [`SqlFunctions.IOpenJsonRow`](xref:NextORM.Core.SqlFunctions.IOpenJsonRow) (`key`/`value`/`type`). The
default schema yields the properties of a JSON object or the elements of a JSON array:

```csharp
var json = """{"a":1,"b":2}""";

var entries = dataContext
    .From(() => SqlFunctions.SqlServer.openjson(json))
    .Select(r => new { r.Key, r.Value, r.Type })
    .ToList();
```

```sql
select [key] as [Key], value, type
 from openjson(@json) as [t1]
```

For a **typed schema** (`OPENJSON ... WITH (...)`) declare your own factory whose row shape matches the
schema and pass the clause body as `withClause`; the body is emitted verbatim after the call:

```csharp
public interface IOpenJsonTypedRow
{
    [Column("name")]
    string? Name { get; set; }
    [Column("age")]
    int Age { get; set; }
}

public static class OpenJsonTvf
{
    public static TvfEntityBuilder<IOpenJsonTypedRow> Typed(string json) =>
        new("openjson", null, "name nvarchar(50) '$.name', age int '$.age'");
}

var person = dataContext
    .From(() => OpenJsonTvf.Typed("""{"name":"Ada","age":36}"""))
    .Select(r => new { r.Name, r.Age })
    .First();
```

```sql
select name, age
 from openjson(@json) with (name nvarchar(50) '$.name', age int '$.age') as [t1]
```

SQL Server also ships the full-text table functions `SqlFunctions.SqlServer.containstable` and
`freetexttable`, which expose the matched row's full-text key and relevance score through
[`SqlFunctions.IKeyRankRow<TKey>`](xref:NextORM.Core.SqlFunctions.IKeyRankRow`1) (`.Key`, `.Rank`). Join the
function back to the indexed table on `Key` and order by `Rank`:

```csharp
var search = "stuffed bear";

var ranked = dataContext
    .From(() => SqlFunctions.SqlServer.containstable<int>("documents", "title", search))
    .Join<IDocument>((k, d) => k.Key == d.Id)
    .OrderByDescending(p => p.Item1.Rank)
    .Select(p => new { p.Item2.Id, p.Item1.Rank })
    .ToList();
```

`containstable` uses `CONTAINSTABLE` (boolean/prefix/phrase syntax) and `freetexttable` the
natural-language `FREETEXTTABLE`; both are SQL Server-only
([`SupportsTableFunction`](xref:NextORM.Core.ISqlDialect.SupportsTableFunction(System.String))), and other providers throw
`NotSupportedException`. The `table` and `column` arguments are emitted **verbatim** as identifiers
(declared through `VerbatimArguments`), so pass the table name or alias exactly as it appears in the
generated query, and only pass trusted values.

For a table function whose schema lives **inside** the call parentheses rather than in a trailing `WITH`
clause, pass `callClause` to append verbatim SQL after the arguments (include your own leading
separator). MySQL `JSON_TABLE` is the typical case:

```csharp
public interface IJsonTableRow
{
    [Column("id")]
    int Id { get; set; }
    [Column("name")]
    string? Name { get; set; }
}

public static class JsonTableTvf
{
    public static TvfEntityBuilder<IJsonTableRow> JsonTable(string doc) =>
        new("json_table", null, null,
            ", '$[*]' columns(id int path '$.id', name varchar(50) path '$.name')");
}
```

```sql
select id, name
 from json_table(@doc, '$[*]' columns(id int path '$.id', name varchar(50) path '$.name')) as `t1`
```

`VerbatimArguments` is the related hook for functions that take a raw identifier (a table or column name):
each listed argument must be a constant string and is rendered unquoted.

`SqlFunctions.Sqlite.json_each`/`json_tree` are the SQLite JSON table functions returning
[`SqlFunctions.IJsonEachRow`](xref:NextORM.Core.SqlFunctions.IJsonEachRow)/[`SqlFunctions.IJsonTreeRow`](xref:NextORM.Core.SqlFunctions.IJsonTreeRow) (`key`, `value`, `type`, `fullkey`, `path`,
plus `id`/`parent` for `json_tree`); the FTS5 search source is `SqlFunctions.Sqlite.MatchTable<TEntity>`.
See [SQLite](provider-specific-sql/sqlite.md#json_each--json_tree-table-functions).

`SqlFunctions.ClickHouse.numbers`/`numbers_mt` are ClickHouse table functions returning
[`SqlFunctions.INumbersRow`](xref:NextORM.Core.SqlFunctions.INumbersRow) (the single `number` column). `numbers(count)` yields
consecutive integers from zero, `numbers(start, stop[, step])` an arbitrary range:

```csharp
var rows = dataContext
    .From(() => SqlFunctions.ClickHouse.numbers(3))
    .Select(r => new { r.Value })
    .ToList();
```

```sql
select number as `Value`
 from (select toInt64(number) as number
 from numbers(@count)) as `t1`
```

The `number` column is `UInt64`, which the row reader cannot materialise, so the dialect wraps the call
in a casting subquery (`toInt64(number) as number`).

`SqlFunctions.ClickHouse.zeros`/`zeros_mt` are the ClickHouse row-count table functions returning
[`SqlFunctions.IZerosRow`](xref:NextORM.Core.SqlFunctions.IZerosRow) (the single `zero UInt8` column):

```csharp
var rows = dataContext
    .From(() => SqlFunctions.ClickHouse.zeros(3))
    .Select(r => new { r.Value })
    .ToList();
```

```sql
select zero as `Value`
 from zeros(@count) as `t1`
```

Unlike `numbers`, the `zero` column is `UInt8`, which the row reader materialises directly as `byte`,
so no cast wrapper is needed.

`SqlFunctions.ClickHouse.generate_random`/`generate_random(seed)` are the ClickHouse test-data table
functions returning [`SqlFunctions.IGenerateRandomRow`](xref:NextORM.Core.SqlFunctions.IGenerateRandomRow)
(`id UInt64`, `value Float64`, `name String`). ClickHouse's own schema is a string argument and its
no-argument form has a random schema, so the built-in helper fixes the structure and its `id` column is
cast to `Int64` through the same wrapping subquery as `numbers`; the stream is unbounded, so chain a
page/limit:

```csharp
var rows = dataContext
    .From(() => SqlFunctions.ClickHouse.generate_random())
    .Page(3, 0)
    .Select(r => new { r.Id, r.Value, r.Name })
    .ToList();
```

```sql
select id as `Id`, value as `Value`, name as `Name`
 from (select toInt64(id) as id, value, name
 from generateRandom('id UInt64, value Float64, name String')) as `t1`
limit 3
```

ClickHouse also ships the server/cluster table functions, pre-declared as generic factories whose row
shape is declared by the caller. The `TRow` interface's `[Column]` names must match the `structure`
argument (`url`/`s3`/`file`) or the target table (`remote`/`remoteSecure`/`cluster`/`clusterAllReplicas`):

| `SqlFunctions.ClickHouse.*` | SQL output |
| --- | --- |
| `url<TRow>(url, format, structure)` | `url(url, format, structure)` |
| `s3<TRow>(url, format, structure)` | `s3(url, format, structure)` |
| `file<TRow>(path, format, structure)` | `file(path, format, structure)` |
| `remote<TRow>(addresses, database, table)` | `remote(addresses, database, table)` |
| `remote_secure<TRow>(addresses, database, table)` | `remoteSecure(addresses, database, table)` |
| `cluster<TRow>(cluster, database, table)` | `cluster(cluster, database, table)` |
| `cluster_all_replicas<TRow>(cluster, database, table)` | `clusterAllReplicas(cluster, database, table)` |
| `values<TRow>(tuples)` | `values('<structure from TRow>', tuples...)` |

```csharp
public interface IHitsRow
{
    [Column("id")]
    long Id { get; set; }
    [Column("name")]
    string? Name { get; set; }
}

var hits = dataContext
    .From(() => SqlFunctions.ClickHouse.url<IHitsRow>(
        "http://127.0.0.1:12345/", "CSV", "id UInt64, name String"))
    .Select(r => new { r.Id, r.Name })
    .ToList();
```

```sql
select id as `Id`, name as `Name`
 from url(@url, @format, @structure) as `t1`
```

The functions are ClickHouse-only ([`SupportsTableFunction`](xref:NextORM.Core.ISqlDialect.SupportsTableFunction(System.String)))
and require the matching server permissions; URL/S3/remote authentication is the server's responsibility,
so prefer named collections or `<remote_servers>` to keep secrets out of the query and its plan. The
`format`/`merge`/`input` table functions are intentionally **not** pre-declared — `format`'s schema may be
inferred from the data, `merge` derives it from the underlying tables and `input` is INSERT-only — so
declare a custom factory or use [`FromSql`](xref:NextORM.Core.DataContextExtensions.FromSql(NextORM.Core.IDataContext,System.String,System.Object))
for those.

The mapped function must exist in the database — nextorm only emits the call, it does not create the
function — so use the helper only on the provider that defines it. The built-in helpers are gated by
[`SupportsTableFunction`](xref:NextORM.Core.ISqlDialect.SupportsTableFunction(System.String)): PostgreSQL enables `generate_series`, `unnest`,
`regexp_matches`, `regexp_split_to_table`, `jsonb_array_elements(_text)`, `jsonb_each(_text)`,
`jsonb_object_keys`, `jsonb_path_query`, `ts_stat` and the record functions `jsonb_to_record`/`jsonb_to_recordset`; SQL
Server enables `string_split`/`openjson`, ClickHouse enables `numbers`/`numbers_mt`,
`zeros`/`zeros_mt`, `generateRandom`, `values` and the server/cluster functions
`url`/`s3`/`file`/`remote`/`remoteSecure`/`cluster`/`clusterAllReplicas`, SQLite enables
`json_each`/`json_tree`/`fts5`, and any other provider rejects them with `NotSupportedException`. A
custom factory is never gated, but a caller-declared `ResultSchema` is gated by
[`SupportsResultSchema(TableFunctionSchema)`](xref:NextORM.Core.ISqlDialect.SupportsResultSchema(NextORM.Core.TableFunctionSchema)).

## Provider differences

| Provider | Behaviour |
|---|---|
| SQLite | Function call emitted without an alias for a bare source; an alias is still emitted when the source is joined. Correlated APPLY is rejected. |
| SQL Server | The derived source gets an alias (`as [t1]`); correlated APPLY renders `cross apply`/`outer apply`. |
| PostgreSQL | An alias is required and always emitted (`as "t1"`); correlated APPLY renders `cross join lateral`/`left join lateral`. |
| MySQL | The derived source gets an alias (`` as `t1` ``); a correlated `JSON_TABLE` is the join source itself, without `LATERAL`. |
| MariaDB | Same as MySQL; the dialect has no `LATERAL` keyword at all. |
| ClickHouse | The derived source gets an alias (`` as `t1` ``); correlated APPLY is rejected. |
| In-memory | Table-valued function sources are **not supported** (`NotSupportedException`: "Table-valued function sources are not supported by the in-memory provider."). |

The result-schema forms each provider can render are listed in
[Result schema and provider restrictions](13-tvf-result-schemas.md#provider-restrictions).

> The integration tests use SQLite's bundled `json_each`, so the shared test suite runs the TVF
> behavioural tests only on SQLite; SQL Server and PostgreSQL are skipped via
> `ITestProvider.SupportsTableValuedFunctions` because the function has to be created in the database
> first. SQL generation for all providers is still covered by the `TableFunction_*` and `T320` tests.

## See also

* [Joins](04-joins.md) - joining a TVF to a table or another TVF, and applying it with `CrossApply`/`OuterApply` ([APPLY and LATERAL](04-joins.md#apply-and-lateral)).
* [Result schema and provider restrictions](13-tvf-result-schemas.md) - caller-declared result columns and the per-provider forms.
* [User-defined functions](12-user-defined-functions.md) - the scalar equivalent.
* [Insert statement](17-insert-statement.md) - a TVF (`unnest`, `generate_series`) as an `INSERT ... SELECT` source.
* [Provider overview](../connecting-providers/overview.md) - the TVF alias requirement per provider.

---

Source: `src/nextorm.core/Builders/TvfEntityBuilder.cs`, `src/nextorm.core/Builders/SourceExpressionLowerer.cs`, `src/nextorm.core/Builders/TvfJoinSourceExtensions.cs`, `src/nextorm.core/DataContext/SqlSourceRenderer.cs`;
`tests/nextorm.core.tests/Builders/T320RuntimeTvfTests.cs`, `tests/nextorm.core.tests/Builders/T320RuntimeTvfIdentityTests.cs`;
`tests/nextorm.integration.tests/T320/T320MariaDbTvfTests.cs`;
generated SQL: `tests/nextorm.sqlite.tests/T320/T320RuntimeTvfSqlTests.cs`, `tests/nextorm.sqlserver.tests/T320/T320RuntimeTvfSqlTests.cs`, `tests/nextorm.postgres.tests/T320/T320RuntimeTvfSqlTests.cs`, `tests/nextorm.mysql.tests/T320/T320RuntimeTvfSqlTests.cs`, `tests/nextorm.clickhouse.tests/T320/T320RuntimeTvfSqlTests.cs`.
