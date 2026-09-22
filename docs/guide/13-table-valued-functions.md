# Table-valued functions

> Query a database table-valued function as a `FROM` source with [`FromTableFunction`](xref:NextORM.Core.DataContextExtensions.FromTableFunction``1(NextORM.Core.IDataContext,System.Linq.Expressions.Expression{System.Func{System.Linq.IQueryable{``0}}})) and map its rows
> like any other entity.

**Prerequisites:** [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Joins](03-joins.md) · [Grouping and aggregates](04-grouping-and-aggregates.md)

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

The returned `EntityBuilder<T>` is an ordinary query source, so [`Where`](xref:NextORM.Core.EntityBuilder`1.Where(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}})), [`OrderBy`](xref:NextORM.Core.EntityBuilder`1.OrderBy(System.Int32)), [`GroupBy`](xref:NextORM.Core.EntityBuilder`1.GroupBy``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})), [`Join`](xref:NextORM.Core.EntityBuilder`1.Join``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}})),
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

A TVF is a normal source, so it can be joined to a table and read through the `t1`/`t2` projection:

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
`jsonb_object_keys`, `jsonb_path_query` and `ts_stat`; SQL Server enables
`string_split`/`openjson`, ClickHouse enables `numbers`/`numbers_mt`, `zeros`/`zeros_mt`,
`generateRandom` and the server/cluster functions `url`/`s3`/`file`/`remote`/`remoteSecure`/`cluster`/
`clusterAllReplicas`, and any other provider rejects them with `NotSupportedException` (a user-defined
`[SqlTableFunction]` is never gated).

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

* [Joins](03-joins.md) - joining a TVF to a table or another TVF.
* [User-defined functions](12-user-defined-functions.md) - the scalar equivalent.
* [Provider overview](../providers/overview.md) - the TVF alias requirement per provider.

---

Source: `src/nextorm.core/SqlTableFunctionAttribute.cs:17`, `src/nextorm.core/DataContext/DataContextExtensions.cs:117`, `src/nextorm.core/DataContext/InMemoryDataContext.cs:121`;
`tests/nextorm.integration.tests/CommonTestSuite.Tvf.cs:34`, `:47`, `:61`, `:76`;
`tests/nextorm.core.tests/SqlTableFunctionAttributeTests.cs:8`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:1453`, `:1462`, `:1475`, `:1489`, `:1503`;
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:1044`, `:1066`, `:1080`, `:1094`;
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:976`, `:998`, `:1012`, `:1026`, `:1477`;
`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:1015`, `:1031`, `:1047`, `:1063`, `:1079`, `:1095`, `:1111`.
