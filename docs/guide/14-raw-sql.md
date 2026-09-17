# Raw SQL

> Replace a query's generated SQL with hand-written text while keeping nextorm's row mapping.

**Prerequisites:** [Querying and projections](01-querying-and-projections.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Query reuse: cache vs Prepare](15-query-reuse.md)

## Overview

`WithSql` and `PrepareFromSql` let you keep a normal typed query as the **result shape** and swap in a
raw statement for execution. Everything else - the projection, the entity mapping, member-init
construction, nested DTOs - is taken from the query you built before the swap.

```csharp
// QueryCommand<TResult>
public static QueryCommand<TResult> WithSql<TResult>(this QueryCommand<TResult> queryCommand, string sql);
public static QueryCommand<TResult> WithSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params);

public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, CancellationToken cancellationToken = default);
public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params, CancellationToken cancellationToken = default);
public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, bool nonStreamUsing, CancellationToken cancellationToken = default);
public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params, bool nonStreamUsing, CancellationToken cancellationToken = default);
public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params, bool nonStreamUsing, bool storeInCache, CancellationToken cancellationToken = default);

// Entity<TResult> convenience overloads
public static QueryCommand<TResult> WithSql<TResult>(this Entity<TResult> entity, string sql);
public static QueryCommand<TResult> WithSql<TResult>(this Entity<TResult> entity, string sql, object? @params);
public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this Entity<TResult> entity, string sql);
```

* `WithSql` returns a `QueryCommand<TResult>` that you execute with the usual terminals
  (`ToListAsync`, `FirstAsync`, ...). It goes through the implicit plan cache like any other command.
* `PrepareFromSql` returns an `IPreparedQueryCommand<TResult>`; execute it with the context overloads
  (`dataContext.ToListAsync(prepared, ...)`, `dataContext.FirstAsync(prepared, ...)`, ...).
* `@params` is a plain object. Its **public instance properties** become named parameters, in property
  order, with the property name as the parameter name.
* `nonStreamUsing` has the same meaning as in `Prepare(...)`: `true` (the default) is for buffered/scalar
  terminals, `false` is required for streaming. `storeInCache` is **false** for every `PrepareFromSql`
  overload except the five-argument one, so raw SQL prepared this way does not populate the plan cache by
  default.

The raw statement is passed through verbatim, including comments. Parameter placeholders must match what
the underlying ADO.NET provider expects (`@name` for SQL Server/PostgreSQL; Microsoft.Data.Sqlite also
accepts `@name` even though nextorm's generated SQLite SQL uses `$name`).

## `WithSql`

```csharp
var ids = await dataContext.Create<ISimpleEntity>()
    .Select(it => it.Id)
    .WithSql("select id from simple_entity --this is custom sql")
    .ToListAsync();
```

```sql
-- executed as written
select id from simple_entity --this is custom sql
```

A raw statement with named parameters:

```csharp
var rows = await dataContext.Create<SimpleEntity>()
    .Select(it => new SimpleEntity { Id = it.Id })
    .WithSql("select id from simple_entity where id = @id", new { id = 1 })
    .ToListAsync();
```

```sql
select id from simple_entity where id = @id
-- @id is bound from the property `id` of the params object
```

## `PrepareFromSql`

Prepare a raw statement and execute it against the context. Runtime parameters are supplied at execution
time exactly as for `Prepare(...)`:

```csharp
var prepared = dataContext.Create<ISimpleEntity>()
    .Select(it => it.Id)
    .PrepareFromSql("select id from simple_entity", cancellationToken);

var ids = await dataContext.ToListAsync(prepared);
```

A parameter from the params object plus a runtime parameter (`@norm_p0`) passed to the terminal:

```csharp
var prepared = dataContext.Create<SimpleEntity>()
    .Select(it => new SimpleEntity { Id = it.Id })
    .PrepareFromSql("select id from simple_entity where id = @id+@norm_p0", new { id = 1 }, cancellationToken);

var entity = await dataContext.FirstAsync(prepared, 1);
// id = 1 + 1 = 2
```

## Mapping the result

The result type is defined by the query you build **before** swapping the SQL:

```csharp
// scalar
var ids = await dataContext.Create<ISimpleEntity>()
    .Select(it => it.Id)
    .WithSql("select id from simple_entity")
    .ToListAsync();

// entity member-init
var entities = await dataContext.Create<SimpleEntity>()
    .Select(it => new SimpleEntity { Id = it.Id })
    .WithSql("select id from simple_entity")
    .ToListAsync();

// DTO
var dtos = await dataContext.Create<SimpleEntity>()
    .Select(it => new IdDto { Id = it.Id })
    .WithSql("select id from simple_entity")
    .ToListAsync();

public sealed class IdDto
{
    public int Id { get; set; }
}
```

Column names in the raw `select` list are matched against that projection, so they must line up with the
mapped column names (or `[Column]` names) exactly.

## Provider differences

| Provider | Behaviour |
|---|---|
| SQLite | Statement passed through verbatim; parameters bound by name (`@name` works with `Microsoft.Data.Sqlite`; generated SQL normally uses `$name`). |
| SQL Server | Statement passed through verbatim; `@name` parameters. |
| PostgreSQL | Statement passed through verbatim; `@name` parameters. |
| In-memory | `PrepareFromSql` is not implemented (`InMemoryContext` throws `NotImplementedException`); use a SQL provider for raw statements. |

## See also

* [Query reuse: cache vs Prepare](15-query-reuse.md) - the `nonStreamUsing` / `storeInCache` trade-offs.
* [Scalar functions](11-scalar-functions.md) - stay in LINQ instead of dropping to raw SQL.
* [Provider overview](../providers/overview.md) - parameter placeholder per provider.

---

Source: `src/nextorm.core/Query/QueryCommandExtensions.cs:7`, `src/nextorm.core/Builders/EntityExtensions.cs:5`, `src/nextorm.core/Query/DbQueryCommandExtension.cs:3`, `src/nextorm.core/DataContext/InMemoryDataContext.cs:634`;
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:671`, `:698`, `:713`.
