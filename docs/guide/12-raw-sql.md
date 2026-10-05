# Raw SQL

> Replace a query's generated SQL with hand-written text while keeping nextorm's row mapping.

**Prerequisites:** [Querying and projections](../querying/index.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Query reuse: cache vs Prepare](../infrastructure/01-query-reuse-and-caching.md)

## Overview

[`WithSql`](xref:NextORM.Core.EntityExtensions.WithSql``1(NextORM.Core.EntityBuilder{``0},System.String)) and [`PrepareFromSql`](xref:NextORM.Core.EntityExtensions.PrepareFromSql``1(NextORM.Core.EntityBuilder{``0},System.String)) let you keep a normal typed query as the **result shape** and swap in a
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

* [`WithSql`](xref:NextORM.Core.EntityExtensions.WithSql``1(NextORM.Core.EntityBuilder{``0},System.String)) returns a [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) that you execute with the usual terminals
  ([`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), [`FirstAsync`](xref:NextORM.Core.EntityBuilderExtensions.FirstAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), ...). It goes through the implicit plan cache like any other command.
* [`PrepareFromSql`](xref:NextORM.Core.EntityExtensions.PrepareFromSql``1(NextORM.Core.EntityBuilder{``0},System.String)) returns an [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1); execute it with the context overloads
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

> **Raw row values are not re-materialised.** A `ROW(...)` (PostgreSQL) or `tuple(...)` (ClickHouse) value
> selected by a hand-written statement is read like any other server value, but nextorm cannot convert it
> back into a `System.Tuple<...>` — raw row materialisation is tracked in
> [#194](https://github.com/AlexeyShirshov/nextorm/issues/194). The flat `(a, b)` constructor is a
> builder-side surface for direct `==`/`!=` predicate operands (see
> [Row values](../scalar-functions/06-arrays.md)), not part of raw SQL.

## [`WithSql`](xref:NextORM.Core.EntityExtensions.WithSql``1(NextORM.Core.EntityBuilder{``0},System.String))

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

## [`PrepareFromSql`](xref:NextORM.Core.EntityExtensions.PrepareFromSql``1(NextORM.Core.EntityBuilder{``0},System.String))

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

[`FromSql`](xref:NextORM.Core.DataContextExtensions.FromSql(NextORM.Core.IDataContext,System.String,System.Object)) uses a raw fragment as the query's **source**
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

### Binding a raw source to entity metadata

By default a raw `FROM` source is passed through as written and no [global query filter](../advanced/query-filters.md)
is applied — nextorm does not know which columns the fragment exposes. Call
[`BindEntity<TEntity>`](xref:NextORM.Core.EntityBuilderExtensions.BindEntity``1(NextORM.Core.EntityBuilder{NextORM.Core.TableAlias},System.Collections.Generic.IReadOnlyCollection{System.String}))
as the **first** operation on the source to declare the entity type and the output columns the fragment
returns; each active filter whose columns are all declared is then applied best-effort, and a filter whose
columns are missing is skipped with a `RawSourceFilterSkipped` warning on the `NextORM.QueryFilters` logger
category (level `Warning`, reason `MissingColumns`, carrying the **physical mapped column names** the filter
reads). With an empty declared-column list, a filter with a proven zero-column dependency is applied, while
a column-dependent or undetermined filter is skipped with reason `UndeterminedColumns`. None of this throws:

```csharp
public static EntityBuilder<TEntity> BindEntity<TEntity>(
    this EntityBuilder<TableAlias> source,
    IReadOnlyCollection<string> availableColumns)
```

```csharp
var rows = dataContext
    .FromSql("select id, tenant_id from complex_entity where id > @min", new { min = 5 })
    .BindEntity<ComplexEntity>(["id", "tenant_id"])
    .Where(t => t.Id > 10)
    .ToList();
```

```sql
-- SQLite; other providers qualify the derived table as "t1" and the columns with their own quoting
select id, tenant_id as 'TenantId' from (select id, tenant_id from complex_entity where id > @min)
 where (id > 10)
```

- Column names are the **output/SQL names** of the raw select list (a configured column mapping wins over
  the auto name), compared case-insensitively.
- `BindEntity` must be the first call after `FromSql`/`From(string)`: a later call (after
  `Where`/`Select`/`Join`/projection) throws `InvalidOperationException`, and another source shape, or
  binding to `TableAlias`, throws `NotSupportedException`.
- The list is a caller declaration, not a schema probe: nextorm does not parse the SQL, does not add or
  rename columns and does not verify that the columns actually exist.
- Binding applies best-effort filters only and is **not** a security guarantee: a filter whose columns you
  omit is silently skipped, so keep enforced row scoping in the SQL itself.
- The binding does not apply to `WithSql`/`PrepareFromSql`/`ExecuteRaw`, and the in-memory provider still
  rejects `FromSql` with `NotSupportedException`.

Binding is per source: a joined raw source uses its own `BindEntity<TEntity>` binding and declared columns
(never the main source's or another occurrence's), and its compatible filters are merged into that join's
`ON` condition. The main source's filters are always evaluated against its own binding; in a joined command
they are re-rooted onto the projection's main alias `Item1` and placed in `WHERE`. `SourceOrdinal` names the
skipped source — `0` for the main source and `j + 1` for join index `j`, counting all joins, bound or not. A
`CROSS`/`CROSS APPLY` join has no `ON` clause, so a bound source on such a join has its compatible filters
placed in `WHERE` and nextorm never fabricates an `ON`.

## Executing raw commands (`ExecuteRaw`)

[`WithSql`](xref:NextORM.Core.EntityExtensions.WithSql``1(NextORM.Core.EntityBuilder{``0},System.String)) and [`FromSql`](xref:NextORM.Core.DataContextExtensions.FromSql(NextORM.Core.IDataContext,System.String,System.Object)) keep a typed query and swap part of it. When the statement is not a mapped query at all - a DDL/DML command, a stored procedure, or a command that returns several result sets - use `ExecuteRaw` (arbitrary command text) or `ExecuteProcedure` (a stored procedure by name), which run the command and hand back a [`ProcedureResult`](xref:NextORM.Core.ProcedureResult):

```csharp
// DataContext, and the IRawCommandExecutor role on IDataContext
public ProcedureResult ExecuteRaw(string sql, params IReadOnlyList<ProcedureParameter> parameters);

// async: the expanded form has no token; pass a CancellationToken with the collection form
public Task<ProcedureResult> ExecuteRawAsync(string sql, params IReadOnlyList<ProcedureParameter> parameters);
public Task<ProcedureResult> ExecuteRawAsync(string sql, IReadOnlyList<ProcedureParameter> parameters, CancellationToken cancellationToken = default);

// parameterless async convenience overload for IDataContext
public static Task<ProcedureResult> ExecuteRawAsync(this IDataContext dataContext, string sql, CancellationToken cancellationToken = default);
```

Because `parameters` is a `params` collection, an inline argument is accepted in two equivalent shapes: the expanded form `ExecuteRaw(sql, new ProcedureParameter("min", 0))` and the collection form `ExecuteRaw(sql, [new ProcedureParameter("min", 0)])`. `ExecuteProcedure(name, new ProcedureParameter("a", 1))` does the same. A `params` parameter must be last (CS0231), so on the async twins the token-less expanded call `ExecuteRawAsync(sql, new ProcedureParameter(...))` works, while passing a `CancellationToken` requires the collection form `ExecuteRawAsync(sql, [p1, p2], cancellationToken)` — the two cannot be combined. `ExecuteProcedureAsync` behaves identically.

The statement text is passed through verbatim and is **not** put through the query planner, so it never reuses the plan cache; the result mapper is cached by **result shape** (the reader's ordered column names and the result type), not by SQL text, so arbitrary statements do not grow the mapper cache.

> **SQL injection.** `sql` is executed verbatim; the planner never parameterises it. Never concatenate untrusted input into the text — pass values through `ProcedureParameter` and reference them with placeholders.

> **Why `ProcedureParameter` and not a params object?** `WithSql`, `PrepareFromSql` and `FromSql` go through the query planner, so they also accept the planner's params-object convention: a plain object whose public instance properties become named parameters, in property order (`new { id = 1 }`). `ExecuteRaw` deliberately bypasses the planner - the text is sent verbatim and nothing enumerates an object's properties - so a parameter must be declared explicitly, by name. `ProcedureParameter` is that explicit descriptor, and it is a superset of a bare input value: besides `Name` and `Value` it carries the ADO.NET `Direction`, `DbType`, `Size` and `TypeName`, and the table-valued rows of `Table<T>`, so the same shape also serves output and return parameters and `ExecuteProcedure`. An anonymous object could only ever express inputs.

`ProcedureResult` holds the command and its reader open until disposed. On SQL Server **without MARS**, an open reader blocks every other command on the same connection, so dispose the result before issuing another command on the context.

### Disposal

`ProcedureResult` owns the ADO.NET command and reader until it is disposed; the connection stays owned by the context. Always dispose it (`using`/`await using`) so the command and reader are released and the connection becomes idle again. `Dispose`/`DisposeAsync` are idempotent.

```csharp
using var result = dataContext.ExecuteRaw("delete from simple_entity where id = @id", [new ProcedureParameter("id", 7)]);
// no Read call: the statement returns no result set
```

### DML and DDL

A command without a result set needs no read: execute it and dispose. `Read<T>()` on such a result throws `InvalidOperationException` (there are no result sets), so use `ExecuteRaw` purely for its side effect.

```csharp
using (dataContext.ExecuteRaw("create table raw_log (id integer, message text)"))
{
}

using (dataContext.ExecuteRaw(
    "insert into raw_log (id, message) values (@id, @message)",
    [
        new ProcedureParameter("id", 1),
        new ProcedureParameter("message", "hello"),
    ]))
{
}
```

### Scalar results

`Read<T>()` advances to the next result set and materialises it. When `T` is a scalar, column 0 of every row is read. The first `Read` skips leading result sets without columns; when there are no more sets, `Read<T>()` throws `InvalidOperationException`. A SQL `NULL` read as a non-nullable scalar returns `default` (for example `0` for `int`); read through a nullable `T` (`int?`) to observe the `NULL`.

```csharp
using var result = dataContext.ExecuteRaw("select count(*) as total from simple_entity");

IReadOnlyList<int> totals = result.Read<int>();
var total = totals[0];
```

### Entity results

When `T` is a mapped entity, every column of the current result set is matched to a property by **reader column name**, case-insensitively, in two passes: first against the mapped column name, then against the CLR property name for properties still unmatched. Each reader column binds at most one property, and each property at most once; a duplicated reader column name binds its first occurrence and later ones are ignored. Columns with no matching property are ignored, and properties with no matching column are left at their default (the mapping does not have to cover the whole entity). A `Range<T>` property stored as two columns is reordered so the lower bound immediately precedes its upper bound; an incomplete pair in the reader is dropped. `T` must have a parameterless constructor. A type that was never queried through LINQ is mapped **on demand** with its default mapping (attributes and auto-derived names, with the context's naming convention applied to auto names).

```csharp
[SqlTable("raw_orders")]
public sealed class RawOrder
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

// expanded params form; the collection form [new ProcedureParameter("min", 0)] is equivalent
using var result = dataContext.ExecuteRaw(
    "select name, id from raw_orders where id > @min order by id",
    new ProcedureParameter("min", 0));

IReadOnlyList<RawOrder> orders = result.Read<RawOrder>();
```

Anything that is neither a scalar nor a parameterless-constructor mapped entity throws `NotSupportedException` (see [Mapping, parameters and caching](#mapping-parameters-and-caching)).

### Multiple result sets

Each `Read<T>()` call advances to the next result set and materialises all of its rows. Already-read sets are not revisited. When the command has no further result set, `Read<T>()` throws `InvalidOperationException`. To read sets whose element types differ, enumerate the result object instead (see [Result-set cursors](#result-set-cursors)).

```csharp
using var result = dataContext.ExecuteRaw("select 1 as a; select 2 as b");

IReadOnlyList<int> first = result.Read<int>();   // [1]
IReadOnlyList<int> second = result.Read<int>();  // [2]
// result.Read<int>(); now throws InvalidOperationException
```

#### Result-set cursors

Positional `Read<T>()` always reads the *next* set as the same `T`, so it cannot handle a command whose sets need different element types. Enumerating the result object instead yields the column-bearing result sets as [`ResultSet`](xref:NextORM.Core.ResultSet) cursors; every cursor reads **its own** set with a per-set `T`, either eagerly with `Read<T>()` or lazily with `ReadAsync<T>(ct)`. The async twin is `await foreach (var set in result)`. The same cursor model, and the same traversal rules below, apply to [`BatchResult`](23-sql-batch.md#multiple-result-sets).

```csharp
using var result = dataContext.ExecuteRaw("select 1 as id; select 'two' as label");

foreach (var set in result)
{
    if (set.Index == 0)
    {
        IReadOnlyList<int> ids = set.Read<int>();            // [1]
    }
    else
    {
        IReadOnlyList<string> labels = set.Read<string>();   // ["two"]
    }

    // ...or, asynchronously:
    // await foreach (var value in set.ReadAsync<int>(cancellationToken)) { }
}
```

A cursor exposes:

- `Index` — its 0-based position among the **column-bearing** sets only.
- `FieldCount` — the number of columns in the set.
- `ColumnNames` — a snapshot taken when the cursor was produced; it stays valid after the outer traversal advances.
- `Read<T>()` / `ReadAsync<T>(ct)` — the one-shot read of this set (eager or lazy).

Traversal rules:

- **One-shot and forward-only.** Enumerating the result a second time, or mixing enumeration with positional `Read<T>()`/`ReadAsync<T>()`, throws `InvalidOperationException`. Each set can be read once; a second read, or a cursor used after the outer traversal advanced or ended, throws `InvalidOperationException`. A cursor whose owning result was disposed throws `ObjectDisposedException`.
- **Column-less sets are skipped.** Leading, intermediate and trailing sets without columns (DDL/DML) are skipped and do not count toward `Index`; a set with columns but no rows is still yielded. Rows of the current set that were not read are dropped automatically when the traversal advances, so the next set is read intact.
- **Cancellation.** `await foreach (var set in result.WithCancellation(ct))` cancels the outer traversal; `set.ReadAsync<T>(ct)` honors its own token. Cancellation throws `OperationCanceledException` and ends the traversal for good.
- **Outputs.** `OutputParameters`/`ReturnValue` remain available once the sets are exhausted, but reading them first closes the reader, so a later enumeration throws `InvalidOperationException`; accessing outputs while a traversal is active is rejected.
- **Ownership.** Disposing the outer enumerator (for example `break` in a `foreach`) invalidates its cursors but does not dispose the `ProcedureResult`; `using`/`await using` still releases the reader and command.

A buffering `ReadAllAsync` that materialises every set at once is intentionally not provided: the traversal is forward-only and consumes one set at a time.

Unlike the streaming `ProcedureResult`, [`BatchResult`](23-sql-batch.md#multiple-result-sets) enumerates an eager in-memory buffer through the identical cursor surface and traversal rules; it owns no reader and is not disposable.

### Async

`ExecuteRawAsync` opens the reader asynchronously; `ReadAsync<T>()` returns an `IAsyncEnumerable<T>` over the rows of the current set. `await using` disposes the result asynchronously. A `params` parameter must be last, so a `CancellationToken` cannot be combined with the expanded form: pass the token with the collection form `ExecuteRawAsync(sql, [p1, p2], cancellationToken)`, or use the token-less expanded form `ExecuteRawAsync(sql, new ProcedureParameter(...))`.

```csharp
await using var result = await dataContext.ExecuteRawAsync(
    "select id from simple_entity order by id",
    Array.Empty<ProcedureParameter>(),
    cancellationToken);

var ids = new List<int>();
await foreach (var id in result.ReadAsync<int>(cancellationToken))
    ids.Add(id);
```

### Output parameters and return values

A [`ProcedureParameter`](xref:NextORM.Core.ProcedureParameter) describes the name, value and the ADO.NET options `Direction`, `DbType`, `Size` and `TypeName`:

```csharp
public readonly record struct ProcedureParameter(
    string Name,
    object? Value,
    ParameterDirection Direction = ParameterDirection.Input,
    DbType? DbType = null,
    int? Size = null,
    string? TypeName = null);
```

Declare output and return-value parameters by direction. ADO.NET only populates them once the reader is closed, so `OutputParameters` and `ReturnValue` close the reader **on first access** and discard any result set not yet read. The example below targets SQL Server (`exec` with output parameters); the underlying ADO.NET provider supplies the support.

```csharp
using var result = dataContext.ExecuteRaw(
    "exec @result = dbo.usp_Add @a = @a, @b = @b, @sum = @sum output",
    [
        new ProcedureParameter("result", null, ParameterDirection.ReturnValue),
        new ProcedureParameter("a", 2),
        new ProcedureParameter("b", 3),
        new ProcedureParameter("sum", null, ParameterDirection.Output, DbType.Int32),
    ]);

var sum = result.OutputParameters[0].Value;   // closes the reader and returns 5
var returnValue = result.ReturnValue;         // same snapshot
```

`OutputParameters` is an `IReadOnlyList<ProcedureOutputParameter>` (`Name`, `Value`, `Direction`); `DBNull` is normalised to `null`. `ReturnValue` is the value of the `ParameterDirection.ReturnValue` parameter, or `null` when none was declared. Both properties return a snapshot, so the live `DbParameter` is never exposed.

`ReturnValue` is populated only for a **stored-procedure command type** (see [`ExecuteProcedure`](#stored-procedures-executeprocedure)), and only where the provider has a return status (SQL Server). A text command such as `EXEC` is provider-dependent and typically leaves it unset — capture the value with a `ParameterDirection.Output` parameter instead.

`TypeName` (the provider type name of a structured / table-valued parameter) is supported **only on SQL Server**, where it also marks the parameter `SqlDbType.Structured`. It remains usable with a legacy `DataTable`/`IEnumerable<SqlDataRecord>` value, but the recommended entry point is [`ProcedureParameter.Table<T>`](#table-valued-parameters). PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse reject `TypeName` with `ArgumentException` (they emulate a table parameter with a bound array or a JSON document and have no named type); only the in-memory context has no table parameters at all.

### Stored procedures (`ExecuteProcedure`)

A stored procedure is invoked by name with the dedicated API, which sends the command with `CommandType.StoredProcedure`:

```csharp
// DataContext, and the IRawCommandExecutor role on IDataContext
public ProcedureResult ExecuteProcedure(string name, params IReadOnlyList<ProcedureParameter> parameters);

// async: the expanded form has no token; pass a CancellationToken with the collection form
public Task<ProcedureResult> ExecuteProcedureAsync(string name, params IReadOnlyList<ProcedureParameter> parameters);
public Task<ProcedureResult> ExecuteProcedureAsync(string name, IReadOnlyList<ProcedureParameter> parameters, CancellationToken cancellationToken = default);

// parameterless async convenience overload for IDataContext
public static Task<ProcedureResult> ExecuteProcedureAsync(this IDataContext dataContext, string name, CancellationToken cancellationToken = default);
```

> **SQL injection.** `name` is **not** quoted, escaped or parameterised — it is set as the command's text. Never pass untrusted input as the procedure name. Parameter names are supplied without the provider prefix (for example `@` on SQL Server).

The result is the same [`ProcedureResult`](xref:NextORM.Core.ProcedureResult) as for `ExecuteRaw`: `Read<T>()`/`ReadAsync<T>()` consume the procedure's result sets in order, and `OutputParameters`/`ReturnValue` close the reader on first access. Support is advertised by [`ISqlDialect.SupportsStoredProcedures`](xref:NextORM.Core.ISqlDialect.SupportsStoredProcedures): **SQL Server, PostgreSQL and MySQL/MariaDB** execute procedures; **SQLite, ClickHouse and the in-memory** context throw `NotSupportedException` before opening a connection.

```csharp
// SQL Server: a procedure's own return status comes back through a ReturnValue parameter.
using var result = dataContext.ExecuteProcedure(
    "dbo.usp_Add",
    [
        new ProcedureParameter("result", null, ParameterDirection.ReturnValue, DbType.Int32),
        new ProcedureParameter("a", 2, DbType.Int32),
        new ProcedureParameter("b", 3, DbType.Int32),
        new ProcedureParameter("sum", null, ParameterDirection.Output, DbType.Int32),
    ]);

var outputs = result.OutputParameters;   // "sum" -> 5
var returnValue = result.ReturnValue;    // the procedure's return status
```

| Provider | Generated call | Notes |
|---|---|---|
| SQL Server | `exec name ...` | `Output`/`InputOutput` parameters, and a `ReturnValue` parameter for the procedure's own return status. |
| PostgreSQL | `CALL name(...)` | Invokes **procedures** (PostgreSQL 11+) only. An `INOUT`/`OUT` parameter's value comes back as a result-row column, and Npgsql copies it into `OutputParameters` when the reader is closed — so `OutputParameters` is populated for an `INOUT` procedure. Call a **function** through `ExecuteRaw` instead. |
| MySQL / MariaDB | `CALL name(...)` | `IN`, `OUT` and `INOUT` parameters (`OutputParameters`), plus result sets through `Read<T>()`. No return value. |
| SQLite / ClickHouse / in-memory | — | `NotSupportedException` (the capability is `false`). |

A PostgreSQL function is invoked through `ExecuteRaw`:

```csharp
using var result = dataContext.ExecuteRaw("select f(@a) as value", [new ProcedureParameter("a", 21)]);
var value = result.Read<int>()[0];
```

### Table-valued parameters

A table can be passed as a **parameter** (not as a `FROM` source) through the `ProcedureParameter.Table<T>` factories, which produce an input parameter carrying an `IEnumerable<T>` row set:

```csharp
public static ProcedureParameter Table<T>(string name, IEnumerable<T> rows);
public static ProcedureParameter Table<T>(string name, string typeName, IEnumerable<T> rows);
```

For example, a scalar set binds a single column and an entity set one column per mapped property:

```csharp
var ids = ProcedureParameter.Table("ids", new[] { 1, 2, 3 });   // a single int column

var employees = ProcedureParameter.Table("rows", new[]          // one column per mapped property
{
    new TvpRow { Id = 1, Name = "alpha" },
    new TvpRow { Id = 2, Name = "beta" },
});
```

The parameter is then passed to `ExecuteRaw`/`ExecuteProcedure` like any other input parameter; the SQL that consumes it is provider-specific (see the table and the per-provider examples below).

* **Row type.** A **scalar** row type (a primitive, `string`, `decimal`, `Guid`, `DateTime`/`DateTimeOffset`/`DateOnly`/`TimeOnly`, `TimeSpan`, `byte[]`, an enum or a nullable of these) binds as a single column. Any other type is treated as a **mapped entity**: the columns are its non-computed mapped properties, in metadata order, **including identity columns** (the same mapping as a bulk insert). A `Range<T>` property maps to two columns and is not supported.
* **Capability.** [`ISqlDialect.SupportsTableValuedParameters`](xref:NextORM.Core.ISqlDialect.SupportsTableValuedParameters) gates the feature: **SQL Server** binds natively, **PostgreSQL, MySQL/MariaDB and SQLite** emulate with a typed array or a JSON document, **ClickHouse** emulates with a bound `Array(T)`/`Array(Tuple(...))` expanded server-side with `arrayJoin(@p)`, and only the **in-memory context** throws `NotSupportedException`.
* **`TypeName`** is **SQL Server only** (the user-defined table type). PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse reject it with `ArgumentException`; call `Table(name, rows)` there.
* **Decimal precision/scale.** A mapped-entity `decimal` column can declare its precision/scale with [`DecimalPrecision`](xref:NextORM.Core.DecimalPrecisionAttribute) (`[DecimalPrecision(12, 4)]`) or fluently with `Property(x => x.Amount).DecimalPrecision(12, 4)`. The pair is provider-neutral and validated against the type actually bound for the column, as precision `1..38` and `0 <= scale <= precision` (SQL Server's maximum). A bound provider type of `decimal` is required: a non-decimal model mapped through a value converter to `decimal` is accepted, while a `decimal` model mapped to a non-decimal representation (a converter to `string` or a `JsonColumn` mapping) is rejected. An out-of-range pair throws `ArgumentOutOfRangeException` (the parameter name is `precision` or `scale`); a non-decimal bound type, or a pair with only one of precision/scale declared, throws `InvalidOperationException`. SQL Server then binds the column as `decimal(precision, scale)` and ClickHouse as `Decimal(precision, scale)`; without a declaration the provider default applies (SQL Server `decimal(38,18)`, ClickHouse `Decimal(38, 10)`). A scalar `decimal` row type has no property to annotate and keeps the provider default.
* **Fluent mapping and attributes.** Declaring even one property fluently (`Property(x => ...)`) maps **only** the properties declared on the builder for that entity; the attributes ([`Column`](xref:System.ComponentModel.DataAnnotations.Schema.ColumnAttribute), [`DecimalPrecision`](xref:NextORM.Core.DecimalPrecisionAttribute), and the rest) on the properties that were not declared are not auto-built and are ignored. Declare every property you need on the builder, or rely entirely on attributes and declare no property fluently.
* **Value converters to `TimeSpan`.** The general write seam lets a value converter own its provider representation — a converted `TimeSpan` is bound as-is, not reduced to the integer duration storage. A table parameter has no native duration binding on the providers without a duration type (SQL Server, SQLite, ClickHouse), so a mapped-entity column whose converter targets `TimeSpan` is rejected with `NotSupportedException` when the columns are built: bind the converted value as its integer storage instead. PostgreSQL `interval` and MySQL/MariaDB `TIME` are native and keep the converted value.
* **Empty set.** An empty sequence is valid and binds an empty table.
* `name`/`rows` are validated (`ArgumentException` for a blank name, `ArgumentNullException` for a null sequence), and a table parameter is input-only.

The SQL that consumes the parameter is provider-specific:

| Provider | How `T` is bound | Sample SQL using `@p` |
|---|---|---|
| SQL Server | `SqlDbType.Structured` + `TypeName` (user-defined table type), streamed as `SqlDataRecord` | `select sum(value) as total from @p` |
| PostgreSQL | scalar → typed array; entity → `jsonb` document | `select sum(x) as total from unnest(@p) as x` / `select x."Id", x."Name" from jsonb_to_recordset(@p) as x("Id" int, "Name" text)` |
| MySQL / MariaDB | JSON document | `select t.Id, t.Name from JSON_TABLE(@p, '$[*]' COLUMNS(Id INT PATH '$.Id', Name varchar(100) PATH '$.Name')) as t` |
| SQLite | JSON document | `select value from json_each(@p)` / `select json_extract(value, '$.Id') as Id from json_each(@p)` |
| ClickHouse | bound `Array(T)` (scalar) / `Array(Tuple(...))` (entity) expanded with `arrayJoin` | `select arrayJoin(@p) as value` / `select t.1, t.2 from (select arrayJoin(@p) as t)` |
| In-memory | — | `NotSupportedException` |

SQL Server requires a user-defined table type:

```sql
create type dbo.IdList as table (value int not null);
```

```csharp
using var result = dataContext.ExecuteRaw(
    "select sum(value) as total from @p",
    [ProcedureParameter.Table("p", "dbo.IdList", new[] { 1, 2, 3 })]);

var total = result.Read<int>()[0];   // 6
```

On SQL Server the CLR type of each column picks a fixed T-SQL type (a supplied user-defined table type must match it, by ordinal):

| CLR | T-SQL |
|---|---|
| `bool` | `bit` |
| `char` | `nchar(1)` |
| `sbyte`, `short` | `smallint` |
| `byte` | `tinyint` |
| `ushort`, `int` | `int` |
| `uint`, `long` | `bigint` |
| `ulong` | `decimal(20,0)` |
| `float` | `real` |
| `double` | `float` |
| `decimal` | `decimal(38,18)` (or the declared `decimal(p,s)`) |
| `string` | `nvarchar(max)` |
| `Guid` | `uniqueidentifier` |
| `DateTime` | `datetime2` |
| `DateTimeOffset` | `datetimeoffset` |
| `DateOnly` | `date` |
| `TimeOnly` | `time` |
| `TimeSpan` | `bigint` (the mapped duration unit, or ticks) |
| `byte[]` | `varbinary(max)` |

A `decimal` column uses the precision/scale declared with `[DecimalPrecision]` or the fluent mapping, defaulting to `decimal(38,18)`. For a different precision/scale on any other type or a non-`max` string length, declare the user-defined table type explicitly and pass a legacy `DataTable` with `ProcedureParameter.TypeName` instead of the `Table<T>` factory.

The emulating providers (PostgreSQL, MySQL/MariaDB, SQLite) serialize the rows as JSON with these conventions: `null` → JSON `null`, `DateTime`/`DateTimeOffset` → ISO-8601 strings, `DateOnly` → `yyyy-MM-dd`, `TimeOnly` → `HH:mm:ss.fffffff`, `TimeSpan` → its invariant `c` form when the provider has a native duration type (PostgreSQL `interval`, MySQL/MariaDB `TIME`), otherwise its stored integer (the mapped duration unit, or ticks — for example SQLite), `Guid` → its `D` form, `byte[]` → base64, enums → their underlying number, and `NaN`/`±∞` → `NotSupportedException` (no JSON representation).

On PostgreSQL an entity set is a `jsonb` document whose object keys are the mapped column names:

```csharp
using var result = dataContext.ExecuteRaw(
    "select x.\"Id\", x.\"Name\" from jsonb_to_recordset(@rows) as x(\"Id\" int, \"Name\" text)",
    [ProcedureParameter.Table("rows", employees)]);
```

On ClickHouse a scalar set is a native `Array(T)` and an entity set an `Array(Tuple(...))`; the server expands either with `arrayJoin`:

```csharp
public sealed class TvpRow
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

// scalar set -> select arrayJoin(@ids)
using (var result = dataContext.ExecuteRaw(
    "select arrayJoin(@ids) as value order by value",
    [ProcedureParameter.Table("ids", new[] { 3, 1, 2 })]))
{
    var values = result.Read<int>();   // [1, 2, 3]
}

// entity set -> select t.1, t.2 from (select arrayJoin(@rows) as t)
using (var result = dataContext.ExecuteRaw(
    "select t.1 as Id, t.2 as Name from (select arrayJoin(@rows) as t) order by Id",
    [ProcedureParameter.Table("rows", new[]
    {
        new TvpRow { Id = 2, Name = "beta" },
        new TvpRow { Id = 1, Name = null },
    })]))
{
    var rows = result.Read<TvpRow>();
}
```

ClickHouse has no stored procedures, so a table parameter is consumed by `ExecuteRaw`/`ExecuteRawAsync` SQL calling `arrayJoin` — `ExecuteProcedure` throws `NotSupportedException` there ([`SupportsStoredProcedures`](xref:NextORM.Core.ISqlDialect.SupportsStoredProcedures)). The provider sets the driver's explicit `ClickHouseType`, wrapping a nullable column in `Nullable(...)`, so a null element and an empty array round-trip; an empty set binds an empty array, and null elements, nullable columns and empty sets are covered by the provider tests. Only CLR types with a ClickHouse mapping are supported (the scalar column set: the integer types, `float`/`double`, `decimal` as `Decimal(38, 10)`, `bool`, `string`/`char`, `Guid`, `DateTime`/`DateTimeOffset`/`DateOnly`, an enum as its underlying number; `TimeOnly` binds as `String` formatted invariantly as `HH:mm:ss.fffffff`, `TimeSpan` as `Int64` in its mapped duration unit (ticks by default), and `byte[]` as `String`); any other column type throws `NotSupportedException`. A `decimal` column binds as `Decimal(38, 10)` by default or as the declared `Decimal(precision, scale)`. A scalar `sbyte`/`ushort`/`uint`/`ulong` binds as the native `Int8`/`UInt16`/`UInt32`/`UInt64` and `char` as `String`, the same ClickHouse type as the matching entity column. A `byte[]` column is bound as `String`, so the row reader materialises it as a `string`, not a `byte[]` — do not rely on a `byte[]` round-trip. The whole set is bound as one array parameter — this is not a streaming or binary bulk path, so keep very large sets on the provider's bulk-insert API.

### Mapping, parameters and caching

* **Mapper cache is shape-keyed, not SQL-keyed.** The result mapper is cached by the reader's ordered column names and the result type, so two statements that return the same shape share a mapper and arbitrary SQL text does not grow the cache. Parameter values are still best passed as [`ProcedureParameter`](xref:NextORM.Core.ProcedureParameter) rather than inlined, both for injection safety and to keep the SQL text stable.
* **Mapping.** `T` is either a scalar (column 0) or a mapped entity with a parameterless constructor. Any other type throws `NotSupportedException`.
* **Context services.** Raw commands run through the same executor as the rest of the context, so they honour the current transaction, the command timeout, interceptors and logging.
* **Provider support.** The in-memory context does not support raw commands and throws `NotSupportedException`. Stored procedures require `ISqlDialect.SupportsStoredProcedures` (SQL Server, PostgreSQL, MySQL/MariaDB); SQLite and ClickHouse throw `NotSupportedException` before opening a connection. Table-valued parameters require `ISqlDialect.SupportsTableValuedParameters` (SQL Server, PostgreSQL, MySQL/MariaDB, SQLite, ClickHouse); the in-memory context throws `NotSupportedException`.

## Provider differences

| Provider | Behaviour |
|---|---|
| SQLite | Statement passed through verbatim; parameters bound by name (`@name` works with `Microsoft.Data.Sqlite`; generated SQL normally uses `$name`). |
| SQL Server | Statement passed through verbatim; `@name` parameters. |
| PostgreSQL | Statement passed through verbatim; `@name` parameters. |
| MySQL | Statement passed through verbatim; `@name` parameters. |
| MariaDB | Statement passed through verbatim; `@name` parameters. |
| ClickHouse | Statement passed through verbatim; `@name` parameters (rewritten to `{name:Type}` by the driver). |
| In-memory | [`PrepareFromSql`](xref:NextORM.Core.EntityExtensions.PrepareFromSql``1(NextORM.Core.EntityBuilder{``0},System.String)) and [`FromSql`](xref:NextORM.Core.DataContextExtensions.FromSql(NextORM.Core.IDataContext,System.String,System.Object)) are not supported ([`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) throws `NotSupportedException`); use a SQL provider for raw statements. |

## See also

* [Query reuse: cache vs Prepare](../infrastructure/01-query-reuse-and-caching.md) - the `nonStreamUsing` / `storeInCache` trade-offs.
* [Scalar functions](../scalar-functions/index.md) - stay in LINQ instead of dropping to raw SQL.
* [Provider overview](../providers/overview.md) - parameter placeholder per provider.

---

Source: `src/nextorm.core/Query/QueryCommandExtensions.cs:7`, `src/nextorm.core/Builders/EntityExtensions.cs:5`, `src/nextorm.core/Query/RawSqlOverride.cs:3`, `src/nextorm.core/DataContext/InMemoryDataContext.cs:634`, `src/nextorm.core/DataContext/DataContextExtensions.cs` (`FromSql`), `src/nextorm.core/DataContext/SqlSourceRenderer.cs` (`MakeRawSqlSource`);
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:671`, `:698`, `:713`;
`src/nextorm.core/DataContext/ProcedureParameter.cs`, `src/nextorm.core/DataContext/ProcedureResult.cs`, `src/nextorm.core/DataContext/Roles/IRawCommandExecutor.cs` (`ExecuteRaw`/`ExecuteRawAsync`/`ExecuteProcedure`/`ExecuteProcedureAsync`, including the expanded `params` overloads), `src/nextorm.core/DataContext/DataContext.cs` (concrete implementations of the same raw-command overloads), `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs` (`SupportsStoredProcedures`, `SupportsTableValuedParameters`), `tests/nextorm.sqlite.tests/RawCommandTests.cs`, `tests/nextorm.integration.tests/CommonTestSuite.Raw.cs`, `tests/nextorm.integration.tests/CommonTestSuite.StoredProcedures.cs`, `src/nextorm.core/DataContext/TableParameterValue.cs`, `src/nextorm.core/DataContext/TableParameterBinder.cs`, `src/nextorm.core/DataContext/ProcedureParameter.cs` (`Table<T>`), `src/nextorm.sqlserver/SqlServerDataContext.cs`, `src/nextorm.postgres/PostgresDataContext.cs`, `src/nextorm.mysql/MySqlDataContext.cs`, `src/nextorm.sqlite/SqliteDataContext.cs`, `src/nextorm.clickhouse/ClickHouseDataContext.cs`, `tests/nextorm.clickhouse.tests/TableValuedParameterTests.cs`, `tests/nextorm.integration.tests/ClickHouseTableValuedParameterTests.cs`.
