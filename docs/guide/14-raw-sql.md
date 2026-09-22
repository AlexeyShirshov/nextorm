# Raw SQL

> Replace a query's generated SQL with hand-written text while keeping nextorm's row mapping.

**Prerequisites:** [Querying and projections](01-querying-and-projections.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Query reuse: cache vs Prepare](15-query-reuse.md)

## Overview

[`WithSql`](xref:NextORM.Core.EntityBuilder`1) and [`PrepareFromSql`](xref:NextORM.Core.EntityBuilder`1) let you keep a normal typed query as the **result shape** and swap in a
raw statement for execution. Everything else - the projection, the entity mapping, member-init
construction, nested DTOs - is taken from the query you built before the swap.

```csharp
// QueryCommand<TResult>
public static QueryCommand<TResult> WithSql<TResult>(this QueryCommand<TResult> queryCommand, string sql);
public static QueryCommand<TResult> WithSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params);

public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, CancellationToken cancellationToken = default);
public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params, CancellationToken cancellationToken = default);
public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, PrepareFromSqlMode mode, CancellationToken cancellationToken = default);
public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params, PrepareFromSqlMode mode, CancellationToken cancellationToken = default);

// EntityBuilder<TResult> convenience overloads
public static QueryCommand<TResult> WithSql<TResult>(this EntityBuilder<TResult> entity, string sql);
public static QueryCommand<TResult> WithSql<TResult>(this EntityBuilder<TResult> entity, string sql, object? @params);
public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this EntityBuilder<TResult> entity, string sql);
```

* [`WithSql`](xref:NextORM.Core.EntityBuilder`1) returns a [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) that you execute with the usual terminals
  ([`ToListAsync`](xref:NextORM.Core.EntityBuilder`1), [`FirstAsync`](xref:NextORM.Core.EntityBuilder`1), ...). It goes through the implicit plan cache like any other command.
* [`PrepareFromSql`](xref:NextORM.Core.EntityBuilder`1) returns an [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1); execute it with the context overloads
  (`dataContext.ToListAsync(prepared, ...)`, `dataContext.FirstAsync(prepared, ...)`, ...).
* `@params` is a plain object. Its **public instance properties** become named parameters, in property
  order, with the property name as the parameter name.
* `mode` is a `[Flags]` value: [`None`](xref:NextORM.Core.PrepareFromSqlMode.None) (the default) is for buffered/scalar execution
  (same as `nonStreamUsing: true` in `Prepare(...)`), [`Streaming`](xref:NextORM.Core.PrepareFromSqlMode.Streaming) is required for streaming/non-buffered
  consumption, and [`StoreInCache`](xref:NextORM.Core.PrepareFromSqlMode.StoreInCache) populates the plan cache. Every overload defaults to `None`, so raw SQL
  prepared this way does not populate the plan cache unless asked.

The raw statement is passed through verbatim, including comments. Parameter placeholders must match what
the underlying ADO.NET provider expects (`@name` for SQL Server/PostgreSQL; Microsoft.Data.Sqlite also
accepts `@name` even though nextorm's generated SQLite SQL uses `$name`).

## [`WithSql`](xref:NextORM.Core.EntityBuilder`1)

```csharp
var ids = await dataContext.From<ISimpleEntity>()
    .Select(it => it.Id)
    .WithSql("select id from simple_entity --this is custom sql")
    .ToListAsync();
```

```sql
-- executed as written
select id from simple_entity --this is custom sql
```

The `Output:` tables below show the rows returned by each example against the integration-test seed data (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Output:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

A raw statement with named parameters:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(it => new SimpleEntity { Id = it.Id })
    .WithSql("select id from simple_entity where id = @id", new { id = 1 })
    .ToListAsync();
```

```sql
select id from simple_entity where id = @id
-- @id is bound from the property `id` of the params object
```

Output:

| Id |
|----|
| 1 |

## [`PrepareFromSql`](xref:NextORM.Core.EntityBuilder`1)

Prepare a raw statement and execute it against the context. Runtime parameters are supplied at execution
time exactly as for `Prepare(...)`:

```csharp
var prepared = dataContext.From<ISimpleEntity>()
    .Select(it => it.Id)
    .PrepareFromSql("select id from simple_entity", cancellationToken);

var ids = await dataContext.ToListAsync(prepared);
```

Output:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

A parameter from the params object plus a runtime parameter (`@norm_p0`) passed to the terminal:

```csharp
var prepared = dataContext.From<SimpleEntity>()
    .Select(it => new SimpleEntity { Id = it.Id })
    .PrepareFromSql("select id from simple_entity where id = @id+@norm_p0", new { id = 1 }, cancellationToken);

var entity = await dataContext.FirstAsync(prepared, 1);
// id = 1 + 1 = 2
```

Output:

| Id |
|----|
| 2 |

## Mapping the result

The result type is defined by the query you build **before** swapping the SQL:

```csharp
// scalar
var ids = await dataContext.From<ISimpleEntity>()
    .Select(it => it.Id)
    .WithSql("select id from simple_entity")
    .ToListAsync();

// entity member-init
var entities = await dataContext.From<SimpleEntity>()
    .Select(it => new SimpleEntity { Id = it.Id })
    .WithSql("select id from simple_entity")
    .ToListAsync();

// DTO
var dtos = await dataContext.From<SimpleEntity>()
    .Select(it => new IdDto { Id = it.Id })
    .WithSql("select id from simple_entity")
    .ToListAsync();

public sealed class IdDto
{
    public int Id { get; set; }
}
```

Output:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

Column names in the raw `select` list are matched against that projection, so they must line up with the
mapped column names (or `[Column]` names) exactly.

## Compositing raw SQL as a `FROM` source

[`FromSql`](xref:NextORM.Core.DataContextExtensions.FromSql) uses a raw fragment as the query's **source**
instead of a mapped table, so it can be filtered, joined, grouped, projected and paged like any other
source. Columns are read through [`TableAlias`](xref:NextORM.Core.TableAlias) accessors
(`t["id"].AsInt`); the same params-object convention binds named parameters.

```csharp
var rows = dataContext
    .FromSql("select id, somestring from complex_entity where id > @min", new { min = 5 })
    .Select(t => new { Id = t["id"].AsInt })
    .ToList();
```

```sql
select t1.id from (select id, somestring from complex_entity where id > @min) as "t1"
```

The fragment can also be the **joined** side (rendered as an aliased derived table):

```csharp
var rows = dataContext
    .From<ISimpleEntity>()
    .Join(dataContext.FromSql("select id from complex_entity"), (s, r) => s.Id == r["id"].AsInt)
    .Select(p => new { p.Item1.Id, R = p.Item2["id"].AsInt })
    .ToList();
```

```sql
select t1.id, t2.id from simple_entity as "t1" join (select id from complex_entity) as "t2" on t1.id = t2.id
```

The fragment is emitted verbatim (only pass trusted SQL). A provider opts in through
[`SupportsRawSqlSource`](xref:NextORM.Core.ISqlDialect.SupportsRawSqlSource); every SQL provider does,
and SQLite omits the derived-table alias when the source is not joined.

## Provider differences

| Provider | Behaviour |
|---|---|
| SQLite | Statement passed through verbatim; parameters bound by name (`@name` works with `Microsoft.Data.Sqlite`; generated SQL normally uses `$name`). |
| SQL Server | Statement passed through verbatim; `@name` parameters. |
| PostgreSQL | Statement passed through verbatim; `@name` parameters. |
| MySQL | Statement passed through verbatim; `@name` parameters. |
| MariaDB | Statement passed through verbatim; `@name` parameters. |
| ClickHouse | Statement passed through verbatim; `@name` parameters (rewritten to `{name:Type}` by the driver). |
| In-memory | [`PrepareFromSql`](xref:NextORM.Core.EntityBuilder`1) and [`FromSql`](xref:NextORM.Core.DataContextExtensions.FromSql) are not supported ([`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) throws `NotSupportedException`); use a SQL provider for raw statements. |

## See also

* [Query reuse: cache vs Prepare](15-query-reuse.md) - the `nonStreamUsing` / `storeInCache` trade-offs.
* [Scalar functions](11-scalar-functions.md) - stay in LINQ instead of dropping to raw SQL.
* [Provider overview](../providers/overview.md) - parameter placeholder per provider.

---

Source: `src/nextorm.core/Query/QueryCommandExtensions.cs:7`, `src/nextorm.core/Builders/EntityExtensions.cs:5`, `src/nextorm.core/Query/RawSqlOverride.cs:3`, `src/nextorm.core/DataContext/InMemoryDataContext.cs:634`, `src/nextorm.core/DataContext/DataContextExtensions.cs` (`FromSql`), `src/nextorm.core/DataContext/SqlSourceRenderer.cs` (`MakeRawSqlSource`);
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:671`, `:698`, `:713`.
