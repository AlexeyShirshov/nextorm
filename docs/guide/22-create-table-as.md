# Materializing a query into a table (`CREATE TABLE ... AS SELECT`)

> nextorm can run a normal query and materialize its rows into a table in one command with [`ToTempTable<TResult>()`](xref:NextORM.Core.TempTableExtensions) / `ToTable<TResult>()`. Unlike a [common table expression](09-cte.md), which is reusable only inside one statement, the materialized table is reusable by later queries on the same connection. There is no change tracking and no `SaveChanges`: the terminal issues exactly one command.

**Prerequisites:** [Common table expressions](09-cte.md) · [Data modification (INSERT)](19-insert-statement.md) · [Raw SQL](14-raw-sql.md) · [Provider overview](../providers/overview.md)

## Creating a temporary table

`ToTempTable(name)` builds a `CREATE TEMPORARY TABLE <name> AS <select>` from the query it is called on and executes it:

```csharp
ctx.From<IOrder>()
    .Where(x => x.Total > minTotal)
    .Select(x => new { x.Id, x.Total })
    .ToTempTable("recent_orders");

await ctx.From<IOrder>()
    .Where(x => x.Total > minTotal)
    .Select(x => new { x.Id, x.Total })
    .ToTempTableAsync("recent_orders", cancellationToken: cancellationToken);
```

The target name is used verbatim (only identifier quoting applies); the naming convention is **not** applied, because a raw name is not an entity. Values captured in the query become parameters and travel with the body, exactly as in a standalone query.

A temporary table is session-scoped. Create and read it on the **same context** (the context keeps one connection open), then read it back through `From("name")` with the [`TableAlias`](xref:NextORM.Core.TableAlias) accessors:

```csharp
var orders = ctx.From("recent_orders")
    .Select(t => new { Id = t.GetInt32("id"), Total = t.GetDecimal("total") })
    .ToList();
```

`ToTempTableSql(name)` renders the statement without opening a connection; `ToTempTable` / `ToTempTableAsync` execute it.

## Creating a persistent table

`ToTable(name)` uses the same query but renders `CREATE TABLE` without the `TEMPORARY` keyword:

```csharp
ctx.From<IOrder>().Select(x => new { x.Id, x.Total }).ToTable("order_archive");
```

Unlike a temporary table, a persistent table outlives the session (and is not tied to one connection).

## Options

Pass [`CreateTableAsOptions`](xref:NextORM.Core.CreateTableAsOptions) to control the statement:

```csharp
ctx.From<IOrder>()
    .Select(x => new { x.Id })
    .ToTempTable("recent_orders", new CreateTableAsOptions
    {
        IfNotExists = true,
        Columns = ["order_id"],
        OnCommit = TempTableOnCommit.Drop,
        WithData = false,
    });
```

| Option | Effect | Providers |
|---|---|---|
| `IfNotExists` | Adds `IF NOT EXISTS`, so repeating the statement is a no-op. | PostgreSQL, SQLite, MySQL, MariaDB |
| `Columns` | Declares the target column names instead of deriving them from the query. | PostgreSQL, MySQL, MariaDB (SQLite derives every column) |
| `OnCommit` | `ON COMMIT { PRESERVE ROWS \| DELETE ROWS \| DROP }` of a temporary table; only valid with `ToTempTable`. | PostgreSQL |
| `WithData` | `false` renders `WITH NO DATA` (the table is created empty). | PostgreSQL |

An option a provider cannot express is rejected with `NotSupportedException` when the SQL is built, never silently ignored.

## Provider support

| Provider | `CREATE TABLE ... AS SELECT` |
|---|---|
| PostgreSQL | yes (`TEMPORARY`/`TEMP`, column list, `ON COMMIT`, `WITH [NO] DATA`) |
| SQLite | yes (`TEMP`/`TEMPORARY`; no column list, no `ON COMMIT`, no `WITH NO DATA`) |
| MySQL / MariaDB | yes (`TEMPORARY`, column list) |
| SQL Server | no (its `SELECT ... INTO #t` form is not wired yet) |
| ClickHouse | no (a temporary table allows no `AS SELECT`) |
| In-memory | no (read-only context) |

On a provider without the form, and on the in-memory context, the terminal throws `NotSupportedException`.

## Notes and phase-1 limits

* The terminal returns `void` (not a row count): `CREATE TABLE AS SELECT` reports no meaningful affected rows.
* Only a raw `From("t")` read-back is supported; mapping the created table onto an entity is out of scope.
* Dropping the table (`DROP TABLE`) and adding indexes to it are not part of this API; use `ExecuteNonQuery` for those (see [Raw SQL](14-raw-sql.md)).

## See also

- [Common table expressions](09-cte.md)
- [Data modification (INSERT)](19-insert-statement.md)
- [Provider overview](../providers/overview.md)
