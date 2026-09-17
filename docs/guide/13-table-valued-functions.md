# Table-valued functions

> Query a database table-valued function as a `FROM` source with `FromTableFunction` and map its rows
> like any other entity.

**Prerequisites:** [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Joins](03-joins.md) · [Grouping and aggregates](04-grouping-and-aggregates.md)

## Overview

`SqlTableFunctionAttribute` maps a placeholder static method to a database table-valued function.
The method must return `IQueryable<T>` (where `T` describes the row shape) and is only referenced inside
the expression passed to `FromTableFunction`:

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class SqlTableFunctionAttribute : Attribute
{
    public SqlTableFunctionAttribute();
    public SqlTableFunctionAttribute(string name);
    public string? Name { get; set; }     // defaults to the CLR method name
    public string? Schema { get; set; }   // optional schema/owner prefix
}
```

```csharp
public static Entity<T> FromTableFunction<T>(this IDataContext dataContext,
    Expression<Func<IQueryable<T>>> call);
```

`call` must be a call to a method annotated with `[SqlTableFunction]` (or declared in an annotated type);
anything else throws `ArgumentException`. The call is translated to `[schema.]name(arg1, arg2, ...)`,
with the arguments rendered through the regular expression visitor, so **captured values become
parameters**. nextorm only emits the call - the function must already exist in the target database.

The returned `Entity<T>` is an ordinary query source, so `Where`, `OrderBy`, `GroupBy`, `Join`,
`Select`, paging and terminals all work over it.

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
    .Join(dataContext.Create<IComplexEntity>(), (r, c) => r.Id == c.Id)
    .Select(p => new { p.t1.Value, p.t2.String })
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
    .Select(g => new { g.Value, Cnt = NORM.SQL.count() })
    .ToList();
// (1, 2), (2, 1)
```

## Provider differences

| Provider | Behaviour |
|---|---|
| SQLite | Function call emitted without an alias for a bare source; an alias is still emitted when the source is joined. |
| SQL Server | The derived source gets an alias (`as [t1]`). |
| PostgreSQL | An alias is required and always emitted (`as "t1"`). |
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
`test/nextorm.integration.tests/CommonTestSuite.Tvf.cs:34`, `:47`, `:61`, `:76`;
`test/nextorm.core.tests/SqlTableFunctionAttributeTests.cs:8`;
generated SQL: `test/nextorm.sqlite.tests/SqlGenerationTests.cs:1453`, `:1462`, `:1475`, `:1489`, `:1503`;
`test/nextorm.sqlserver.tests/SqlGenerationTests.cs:1044`, `:1066`, `:1080`, `:1094`;
`test/nextorm.postgres.tests/SqlGenerationTests.cs:976`, `:998`, `:1012`, `:1026`.
