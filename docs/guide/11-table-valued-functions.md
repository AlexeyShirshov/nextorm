# Table-valued functions

> Query a database table-valued function as a `FROM` source with [`FromTableFunction`](xref:NextORM.Core.DataContextExtensions.FromTableFunction``1(NextORM.Core.IDataContext,System.Linq.Expressions.Expression{System.Func{System.Linq.IQueryable{``0}}})) and map its rows
> like any other entity.

**Prerequisites:** [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Joins](02-joins.md) · [Grouping and aggregates](03-grouping-and-aggregates.md)

## Overview

[`SqlTableFunctionAttribute`](xref:NextORM.Core.SqlTableFunctionAttribute) maps a placeholder static method to a database table-valued function.
The method must return `IQueryable<T>` (where `T` describes the row shape) and is only referenced inside
the expression passed to [`FromTableFunction`](xref:NextORM.Core.DataContextExtensions.FromTableFunction``1(NextORM.Core.IDataContext,System.Linq.Expressions.Expression{System.Func{System.Linq.IQueryable{``0}}})):

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class SqlTableFunctionAttribute : Attribute
{
    public SqlTableFunctionAttribute();
    public SqlTableFunctionAttribute(string name);
    public string? Name { get; set; }       // defaults to the CLR method name
    public string? Schema { get; set; }     // optional schema/owner prefix
    public string? WithClause { get; set; } // optional trailing WITH (...) body
    public string? CallClause { get; set; } // optional verbatim SQL inside the call parentheses
    public int[]? VerbatimArguments { get; set; } // argument indices emitted as raw identifiers
    public TableFunctionSchema ResultSchema { get; set; } // render the row type's schema (None by default)
}
```

```csharp
public static EntityBuilder<T> FromTableFunction<T>(this IDataContext dataContext,
    Expression<Func<IQueryable<T>>> call);
```

`call` must be a call to a method annotated with `[SqlTableFunction]` (or declared in an annotated type);
anything else throws `ArgumentException`. The call is translated to `[schema.]name(arg1, arg2, ...)`,
with the arguments rendered through the regular expression visitor, so **captured values become
parameters**. nextorm only emits the call - the function must already exist in the target database.

The returned `EntityBuilder<T>` is an ordinary query source, so [`Where`](xref:NextORM.Core.EntityBuilder`1.Where(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}})), [`OrderBy`](xref:NextORM.Core.EntityBuilder`1.OrderBy(System.Int32)), [`GroupBy`](xref:NextORM.Core.EntityBuilder`1.GroupBy``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})), [`Join`](xref:NextORM.Core.EntityBuilder`1.Join``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})),
[`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})), paging and terminals all work over it.

## Declaring the mapping

The row shape is a normal entity, and the placeholder methods carry the mapping:

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

private static class Tvf
{
    [SqlTableFunction("all_rows")]
    public static IQueryable<ITvfRow> AllRows() => throw new NotSupportedException();

    [SqlTableFunction("rows_by_id")]
    public static IQueryable<ITvfRow> ById(long id) => throw new NotSupportedException();

    [SqlTableFunction("rows_between", Schema = "app")]
    public static IQueryable<ITvfRow> Between(long lo, long hi) => throw new NotSupportedException();

    [SqlTableFunction("json_each")]
    public static IQueryable<IJsonEachRow> JsonEach(string json) => throw new NotSupportedException();
}
```

## Basic example

```csharp
var values = dataContext
    .FromTableFunction(() => Tvf.JsonEach("[1,2,3]"))
    .Select(r => r.Value)
    .ToList();
// 1, 2, 3
```

```sql
-- SQLite
select value from json_each('[1,2,3]')
-- SQL Server / PostgreSQL append an alias
select value from json_each('[1,2,3]') as [t1]
```

The `Output:` tables below show the rows returned by each example against the integration-test seed data (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Output:

| Value |
|-------|
| 1     |
| 2     |
| 3     |

## Arguments become parameters

```csharp
var id = 5L;

var prepared = dataContext
    .FromTableFunction(() => Tvf.ById(id))
    .Select(r => new { r.Id })
    .Prepare();
```

```sql
-- SQLite placeholder; SQL Server/PostgreSQL use @id
select id from rows_by_id($id)
```

A schema-qualified name with two arguments renders as `app.rows_between(...)`:

```csharp
var lo = 1L;
var hi = 3L;

var prepared = dataContext
    .FromTableFunction(() => Tvf.Between(lo, hi))
    .Select(r => new { r.Id })
    .Prepare();
```

```sql
select id from app.rows_between($lo, $hi)
```

## Join, filter and sort a TVF

A TVF is a normal source, so it can sit on either side of a join. The two sources are exposed as
`p.Item1`/`p.Item2` in the order they are declared: a TVF on the left is read through `p.Item1`, one
on the right through `p.Item2`.

A TVF as the left-hand source, joined to a table:

```csharp
var rows = dataContext
    .FromTableFunction(() => Tvf.AllRows())
    .Join(dataContext.From<IComplexEntity>(), (r, c) => r.Id == c.Id)
    .Select(p => new { p.Item1.Value, p.Item2.String })
    .ToList();
```

```sql
-- SQLite
select t1.value, t2.somestring as 'String' from all_rows() as 't1' join complex_entity as 't2' on t1.id = t2.id
```

The reverse — a table on the left, the TVF on the right — behaves the same, and is the shape used once
the function takes a column of the outer row (see [Applying a TVF](#applying-a-tvf)):

```csharp
var rows = dataContext
    .From<ISimpleEntity>()
    .Join(dataContext.FromTableFunction(() => Tvf.AllRows()), (s, r) => r.Id == s.Id)
    .Select(p => new { p.Item1.Id, p.Item2.Value })
    .ToList();
```

```sql
-- SQLite
select t1.id, t2.value from simple_entity as 't1' join all_rows() as 't2' on t2.id = cast(t1.id as bigint)
-- SQL Server / PostgreSQL differ only in identifier quoting ([t2] / "t2")
```

The `cast` is the regular join-time type unification: `Tvf.AllRows().Id` is `long` while
`ISimpleEntity.Id` is `int`.

### Applying a TVF

[`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) and [`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) accept a TVF as the applied source too (see [APPLY and LATERAL](02-joins.md#apply-and-lateral)). Without a lambda the function is applied once:

```csharp
var rows = dataContext
    .From<ISimpleEntity>()
    .CrossApply(dataContext.FromTableFunction(() => Tvf.AllRows()))
    .Select(p => new { p.Item1.Id, p.Item2.Value })
    .ToList();
```

```sql
-- SQL Server
select t1.id, t2.value from simple_entity as [t1] cross apply all_rows() as [t2]
-- PostgreSQL / MySQL / MariaDB
select t1.id, t2.value from simple_entity as "t1" cross join lateral all_rows() as "t2"
```

Building the function inside the lambda correlates it with the outer row: the captured column becomes
an outer reference and the function is called once per left-hand row.

```csharp
var rows = dataContext
    .From<ISimpleEntity>()
    .CrossApply(s => dataContext.FromTableFunction(() => Tvf.ById(s.Id)))
    .Select(p => new { p.Item1.Id, p.Item2.Value })
    .ToList();
```

```sql
-- SQL Server
select t1.id, t3.value from simple_entity as [t1] cross apply (select t2.id, t2.value from rows_by_id(cast(t1.id as bigint)) as [t2]) as [t3]
-- PostgreSQL
select t1.id, t3.value from simple_entity as "t1" cross join lateral (select t2.id, t2.value from rows_by_id(cast(t1.id as bigint)) as "t2") as "t3"
```

`OuterApply` is the left-preserving twin: it keeps left-hand rows for which the function returns no
rows, projecting `null`s. Applying any source needs a lateral-capable provider — SQLite, ClickHouse
and the in-memory provider reject it ([`SupportsApply`](xref:NextORM.Core.ISqlDialect.SupportsApply)).

Filtering, sorting and grouping behave as usual:

```csharp
var values = dataContext
    .FromTableFunction(() => Tvf.JsonEach("[3,1,2]"))
    .Where(r => r.Value > 1)
    .OrderByDescending(r => r.Value)
    .Select(r => r.Value)
    .ToList();
// 3, 2

var rows = dataContext
    .FromTableFunction(() => Tvf.JsonEach("[1,1,2]"))
    .GroupBy(r => new { r.Value })
    .Select(g => new { g.Value, Cnt = SqlFunctions.Sql.count() })
    .ToList();
// (1, 2), (2, 1)
```

Output:

| Value |
|-------|
| 3     |
| 2     |

Output:

| Value | Cnt |
|-------|-----|
| 1     | 2   |
| 2     | 1   |

## Built-in table functions

A few common table-valued functions are pre-declared with `[SqlTableFunction]`, so no user-defined
wrapper is needed.

`SqlFunctions.Postgres.generate_series` and `SqlFunctions.Postgres.unnest` are PostgreSQL (they return [`SqlFunctions.IGenerateSeriesRow`](xref:NextORM.Core.SqlFunctions.IGenerateSeriesRow)
with the `generate_series` column and [`SqlFunctions.IUnnestRow<T>`](xref:NextORM.Core.SqlFunctions.IUnnestRow`1) with the `unnest` column):

```csharp
var numbers = dataContext
    .FromTableFunction(() => SqlFunctions.Postgres.generate_series(1L, 3L))
    .Select(r => r.Value)
    .ToList();

var elements = dataContext
    .FromTableFunction(() => SqlFunctions.Postgres.unnest(SqlFunctions.Parameter<long[]>(0)))
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
| `jsonb_to_record(json)` / `jsonb_to_recordset(json)` | declared by `TRow` | `TRow` (see [Dynamic result schema](#dynamic-result-schema)) |

```csharp
var json = JsonDocument.Parse("""{"a":1,"b":2}""");

var entries = dataContext
    .FromTableFunction(() => SqlFunctions.Postgres.jsonb_each_text(json))
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
    .FromTableFunction(() => SqlFunctions.Postgres.jsonb_path_query(json, SqlFunctions.Postgres.jsonpath(path)))
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
    .FromTableFunction(() => SqlFunctions.SqlServer.string_split(csv, separator))
    .Select(r => r.Value)
    .ToList();
```

```sql
select value from string_split(@csv, @separator) as [t1]
```

`SqlFunctions.SqlServer.openjson` is SQL Server 2016+ and returns [`SqlFunctions.IOpenJsonRow`](xref:NextORM.Core.SqlFunctions.IOpenJsonRow) (`key`/`value`/`type`). The
default schema yields the properties of a JSON object or the elements of a JSON array:

```csharp
var json = """{"a":1,"b":2}""";

var entries = dataContext
    .FromTableFunction(() => SqlFunctions.SqlServer.openjson(json))
    .Select(r => new { r.Key, r.Value, r.Type })
    .ToList();
```

```sql
select [key] as [Key], value, type from openjson(@json) as [t1]
```

For a **typed schema** (`OPENJSON ... WITH (...)`) declare your own wrapper whose row shape matches the
schema and set [`WithClause`](xref:NextORM.Core.SqlTableFunctionAttribute.WithClause); the clause body is emitted verbatim after the call:

```csharp
public interface IOpenJsonTypedRow
{
    [Column("name")]
    string? Name { get; set; }
    [Column("age")]
    int Age { get; set; }
}

private static class OpenJsonTvf
{
    [SqlTableFunction("openjson", WithClause = "name nvarchar(50) '$.name', age int '$.age'")]
    public static IQueryable<IOpenJsonTypedRow> Typed(string json) => throw new NotSupportedException();
}

var person = dataContext
    .FromTableFunction(() => OpenJsonTvf.Typed("""{"name":"Ada","age":36}"""))
    .Select(r => new { r.Name, r.Age })
    .First();
```

```sql
select name, age from openjson(@json) with (name nvarchar(50) '$.name', age int '$.age') as [t1]
```

SQL Server also ships the full-text table functions `SqlFunctions.SqlServer.containstable` and
`freetexttable`, which expose the matched row's full-text key and relevance score through
[`SqlFunctions.IKeyRankRow<TKey>`](xref:NextORM.Core.SqlFunctions.IKeyRankRow`1) (`.Key`, `.Rank`). Join the
function back to the indexed table on `Key` and order by `Rank`:

```csharp
var search = "stuffed bear";

var ranked = dataContext
    .FromTableFunction(() => SqlFunctions.SqlServer.containstable<int>("documents", "title", search))
    .Join(dataContext.From<IDocument>(), (k, d) => k.Key == d.Id)
    .OrderByDescending(p => p.Item1.Rank)
    .Select(p => new { p.Item2.Id, p.Item1.Rank })
    .ToList();
```

`containstable` uses `CONTAINSTABLE` (boolean/prefix/phrase syntax) and `freetexttable` the
natural-language `FREETEXTTABLE`; both are SQL Server-only
([`SupportsTableFunction`](xref:NextORM.Core.ISqlDialect.SupportsTableFunction(System.String))), and other providers throw
`NotSupportedException`. The `table` and `column` arguments are emitted **verbatim** as identifiers (see
`VerbatimArguments`), so pass the table name or alias exactly as it appears in the generated query, and
only pass trusted values.

For a table function whose schema lives **inside** the call parentheses rather than in a trailing `WITH`
clause, set [`CallClause`](xref:NextORM.Core.SqlTableFunctionAttribute.CallClause) to append verbatim SQL
after the arguments (include your own leading separator). MySQL `JSON_TABLE` is the typical case:

```csharp
public interface IJsonTableRow
{
    [Column("id")]
    int Id { get; set; }
    [Column("name")]
    string? Name { get; set; }
}

private static class JsonTableTvf
{
    [SqlTableFunction("json_table", CallClause = ", '$[*]' columns(id int path '$.id', name varchar(50) path '$.name')")]
    public static IQueryable<IJsonTableRow> JsonTable(string doc) => throw new NotSupportedException();
}
```

```sql
select id, name from json_table(@doc, '$[*]' columns(id int path '$.id', name varchar(50) path '$.name')) as `t1`
```

`VerbatimArguments` is the related hook for functions that take a raw identifier (a table or column name):
each listed argument must be a constant string and is rendered unquoted.

`SqlFunctions.ClickHouse.numbers`/`numbers_mt` are ClickHouse table functions returning
[`SqlFunctions.INumbersRow`](xref:NextORM.Core.SqlFunctions.INumbersRow) (the single `number` column). `numbers(count)` yields
consecutive integers from zero, `numbers(start, stop[, step])` an arbitrary range:

```csharp
var rows = dataContext
    .FromTableFunction(() => SqlFunctions.ClickHouse.numbers(3))
    .Select(r => new { r.Value })
    .ToList();
```

```sql
select number as `Value` from (select toInt64(number) as number from numbers(@count)) as `t1`
```

The `number` column is `UInt64`, which the row reader cannot materialise, so the dialect wraps the call
in a casting subquery (`toInt64(number) as number`).

`SqlFunctions.ClickHouse.zeros`/`zeros_mt` are the ClickHouse row-count table functions returning
[`SqlFunctions.IZerosRow`](xref:NextORM.Core.SqlFunctions.IZerosRow) (the single `zero UInt8` column):

```csharp
var rows = dataContext
    .FromTableFunction(() => SqlFunctions.ClickHouse.zeros(3))
    .Select(r => new { r.Value })
    .ToList();
```

```sql
select zero as `Value` from zeros(@count) as `t1`
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
    .FromTableFunction(() => SqlFunctions.ClickHouse.generate_random())
    .Page(3, 0)
    .Select(r => new { r.Id, r.Value, r.Name })
    .ToList();
```

```sql
select id as `Id`, value as `Value`, name as `Name`
from (select toInt64(id) as id, value, name from generateRandom('id UInt64, value Float64, name String')) as `t1`
limit 3
```

ClickHouse also ships the server/cluster table functions, pre-declared as generic wrappers whose row
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
    .FromTableFunction(() => SqlFunctions.ClickHouse.url<IHitsRow>(
        "http://127.0.0.1:12345/", "CSV", "id UInt64, name String"))
    .Select(r => new { r.Id, r.Name })
    .ToList();
```

```sql
select id as `Id`, name as `Name` from url(@url, @format, @structure) as `t1`
```

The functions are ClickHouse-only ([`SupportsTableFunction`](xref:NextORM.Core.ISqlDialect.SupportsTableFunction(System.String)))
and require the matching server permissions; URL/S3/remote authentication is the server's responsibility,
so prefer named collections or `<remote_servers>` to keep secrets out of the query and its plan. The
`format`/`merge`/`input` table functions are intentionally **not** pre-declared — `format`'s schema may be
inferred from the data, `merge` derives it from the underlying tables and `input` is INSERT-only — so use
the generic `[SqlTableFunction]` wrapper declared above or [`FromSql`](xref:NextORM.Core.DataContextExtensions.FromSql(NextORM.Core.IDataContext,System.String,System.Object))
for those.

The mapped function must exist in the database — nextorm only emits the call, it does not create the
function — so use the helper only on the provider that defines it. The built-in helpers are gated by
[`SupportsTableFunction`](xref:NextORM.Core.ISqlDialect.SupportsTableFunction(System.String)): PostgreSQL enables `generate_series`, `unnest`,
`regexp_matches`, `regexp_split_to_table`, `jsonb_array_elements(_text)`, `jsonb_each(_text)`,
`jsonb_object_keys`, `jsonb_path_query`, `ts_stat` and the record functions `jsonb_to_record`/`jsonb_to_recordset`; SQL
Server enables `string_split`/`openjson`, ClickHouse enables `numbers`/`numbers_mt`,
`zeros`/`zeros_mt`, `generateRandom`, `values` and the server/cluster functions
`url`/`s3`/`file`/`remote`/`remoteSecure`/`cluster`/`clusterAllReplicas`, and any other provider
rejects them with `NotSupportedException` (a user-defined `[SqlTableFunction]` is never gated, but a
caller-declared `ResultSchema` is gated by
[`SupportsResultSchema(TableFunctionSchema)`](xref:NextORM.Core.ISqlDialect.SupportsResultSchema(NextORM.Core.TableFunctionSchema))).

## Dynamic result schema

Some table functions do not declare their output columns in the call: the schema is supplied by the caller. Declare it once on the mapped row type (the `T` of the placeholder method's `IQueryable<T>` return type) and set [`ResultSchema`](xref:NextORM.Core.SqlTableFunctionAttribute.ResultSchema) on `[SqlTableFunction]`; nextorm renders the matching native form from that type's `[Column]` metadata and CLR property types. The rows still materialize into the same `[Column]`-annotated DTO as any other table function — there is no separate dynamic reader — and the declared schema is part of the query plan key, so two row shapes never share a cached plan.

[`TableFunctionSchema`](xref:NextORM.Core.TableFunctionSchema) selects where the schema is rendered:

| `ResultSchema` | Rendered form | Provider |
|---|---|---|
| `LeadingArgument` | a quoted structure string as the first call argument: `values('a UInt8, b String', (1,'x'))` | ClickHouse |
| `AliasColumnList` | an alias column-definition list: `jsonb_to_record(@json) as "t1"(a integer, b text)` | PostgreSQL |

ClickHouse `values` reads a literal table from a tuple list:

```csharp
public interface IPairRow
{
    [Column("a")]
    byte A { get; set; }
    [Column("b")]
    string? B { get; set; }
}

var rows = dataContext
    .FromTableFunction(() => SqlFunctions.ClickHouse.values<IPairRow>("(1, 'x'), (2, 'y')"))
    .Select(r => new { r.A, r.B })
    .ToList();
```

```sql
select a, b from values('a UInt8, b String', (1, 'x'), (2, 'y')) as `t1`
```

The `tuples` argument is emitted verbatim (see `VerbatimArguments`), so it is developer-authored SQL only — never build it from user input.

PostgreSQL's `jsonb_to_record`/`jsonb_to_recordset` expand a JSON object/array into a record whose columns are declared by `TRow`:

```csharp
public interface IPairRow
{
    [Column("a")]
    int A { get; set; }
    [Column("b")]
    string? B { get; set; }
}

using var json = JsonDocument.Parse("""[{"a":1,"b":"x"},{"a":2,"b":"y"}]""");

var rows = dataContext
    .FromTableFunction(() => SqlFunctions.Postgres.jsonb_to_recordset<IPairRow>(json))
    .Select(r => new { r.A, r.B })
    .ToList();
```

```sql
select a, b from jsonb_to_recordset(@json) as "t1"(a integer, b text)
```

The column types come from the provider's CLR-to-SQL type mapping (`byte`→`UInt8`, `int`→`integer`, `string`→`text`/`String`); a nullable value type becomes `Nullable(T)` on ClickHouse. Only `LeadingArgument` and `AliasColumnList` are recognized, and each provider opts into the specific form it renders ([`SupportsResultSchema(TableFunctionSchema)`](xref:NextORM.Core.ISqlDialect.SupportsResultSchema(NextORM.Core.TableFunctionSchema))): ClickHouse renders only `LeadingArgument` and PostgreSQL only `AliasColumnList`. A declared form the provider does not implement is rejected with `NotSupportedException` — it is never emitted as SQL without the column list — and every other provider throws as well.

## Provider differences

| Provider | Behaviour |
|---|---|
| SQLite | Function call emitted without an alias for a bare source; an alias is still emitted when the source is joined. |
| SQL Server | The derived source gets an alias (`as [t1]`). |
| PostgreSQL | An alias is required and always emitted (`as "t1"`). |
| MySQL | The derived source gets an alias (`` as `t1` ``). |
| MariaDB | The derived source gets an alias (`` as `t1` ``). |
| ClickHouse | The derived source gets an alias (`` as `t1` ``). |
| In-memory | Table-valued function sources are **not supported** (`NotSupportedException`: "Table-valued function sources are not supported by the in-memory provider."). |

> The integration tests use SQLite's bundled `json_each`, so the shared test suite runs the TVF
> behavioural tests only on SQLite; SQL Server and PostgreSQL are skipped via
> `ITestProvider.SupportsTableValuedFunctions` because the function has to be created in the database
> first. SQL generation for all three providers is still covered by the `TableFunction_*` tests.

## See also

* [Joins](02-joins.md) - joining a TVF to a table or another TVF, and applying it with `CrossApply`/`OuterApply` ([APPLY and LATERAL](02-joins.md#apply-and-lateral)).
* [User-defined functions](10-user-defined-functions.md) - the scalar equivalent.
* [Insert statement](15-insert-statement.md) - a TVF (`unnest`, `generate_series`) as an `INSERT ... SELECT` source.
* [Provider overview](../providers/overview.md) - the TVF alias requirement per provider.

---

Source: `src/nextorm.core/SqlTableFunctionAttribute.cs:39`, `src/nextorm.core/DataContext/DataContextExtensions.cs:1127`, `src/nextorm.core/DataContext/InMemoryQueryBuilder.cs:132`;
`tests/nextorm.integration.tests/CommonTestSuite.Tvf.cs:34`, `:47`, `:61`, `:76`;
`tests/nextorm.core.tests/SqlTableFunctionAttributeTests.cs:5`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:2399`, `:2421`, `:2434`, `:2448`, `:2462`;
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:1740`, `:1749`, `:1762`, `:1776`, `:1790`, `:1803`, `:1816`;
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:1638`, `:1748`, `:2801`, `:2810`, `:2823`, `:2837`, `:2851`, `:2864`, `:2877`;
`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:1474`, `:1501`, `:1527`, `:1556`, `:1668`, `:1694`.
