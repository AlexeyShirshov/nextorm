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

A temporary table is session-scoped. Create and read it on the **same context** (the context keeps one connection open), then read it back through `From("name")` with the [`TableAlias`](xref:NextORM.Core.TableAlias) accessors. A connection pooler that reassigns the backend per transaction needs one transaction around both steps — see [Session affinity and connection poolers](#session-affinity-and-connection-poolers):

```csharp
var orders = ctx.From("recent_orders")
    .Select(t => new { Id = t.GetInt32("id"), Total = t.GetDecimal("total") })
    .ToList();
```

`ToTempTableSql(name)` renders the statement without opening a connection; `ToTempTable` / `ToTempTableAsync` execute it.

## Session affinity and connection poolers

A temporary table lives in the database **session** (the server backend), not merely on the client connection. When PostgreSQL sits behind a connection pooler that reassigns the backend per transaction — PgBouncer in `transaction` mode, or any connection-level load balancer — two commands on the same client connection can be routed to different backends, and the one that reads the table fails with `relation "recent_orders" does not exist`.

One open client connection is therefore not enough. Wrap the `CREATE TEMPORARY TABLE ... AS SELECT` and **every** query that reads the table in one explicit transaction; PgBouncer pins a server for the whole `BEGIN … COMMIT`, so both run on the same backend:

```csharp
var transactions = (ITransactionManager)ctx;
await using var tx = await transactions.BeginTransactionAsync();

ctx.From<IOrder>()
    .Where(x => x.Total > minTotal)
    .Select(x => new { x.Id, x.Total })
    .ToTempTable("recent_orders");

var orders = ctx.From("recent_orders")
    .Select(t => new { Id = t.GetInt32("id") })
    .ToList();

await tx.CommitAsync();
```

* Keep `OnCommit` at the default `PreserveRows`: `Drop` and `DeleteRows` empty or drop the table at commit.
* The table does **not** survive the transaction. After `COMMIT` the pooler may hand the next transaction to a different backend, where the table does not exist; do not rely on it across transactions.
* With PgBouncer in `session` mode the backend is pinned for the whole client session, so no transaction is needed; in `statement` mode a transaction is not enough.
* Prepared statements are a separate concern of `transaction` mode: a statement prepared on one backend cannot be executed on another, so prepared statements may need `max_prepared_statements=0` on PgBouncer or provider-side prepared statements disabled.

## Creating a persistent table

`ToTable(name)` uses the same query but materialises into a persistent table (on SQL Server it renders `SELECT ... INTO <name>`):

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
| `IfNotExists` | Adds `IF NOT EXISTS`, so repeating the statement is a no-op. | PostgreSQL, SQLite, MySQL, MariaDB, ClickHouse (SQL Server's `SELECT ... INTO` rejects it) |
| `Columns` | Declares the target column names instead of deriving them from the query. | PostgreSQL, MySQL, MariaDB (SQLite derives every column; SQL Server takes the names from the select list; ClickHouse needs `name type` pairs) |
| `OnCommit` | `ON COMMIT { PRESERVE ROWS \| DELETE ROWS \| DROP }` of a temporary table; only valid with `ToTempTable`. | PostgreSQL |
| `WithData` | `false` renders `WITH NO DATA` (the table is created empty). | PostgreSQL |

An option a provider cannot express is rejected with `NotSupportedException` when the SQL is built, never silently ignored.

## Provider support

| Provider | `CREATE TABLE ... AS SELECT` |
|---|---|
| PostgreSQL | yes — `CREATE [TEMPORARY] TABLE ... AS SELECT` (`TEMPORARY`/`TEMP`, column list, `ON COMMIT`, `WITH [NO] DATA`) |
| SQLite | yes — `CREATE [TEMPORARY] TABLE ... AS SELECT` (`TEMP`/`TEMPORARY`; no column list, no `ON COMMIT`, no `WITH NO DATA`) |
| MySQL / MariaDB | yes — `CREATE [TEMPORARY] TABLE ... AS SELECT` (`TEMPORARY`, column list) |
| SQL Server | `ToTable` yes — `SELECT ... INTO <table>`; `ToTempTable` no (a session-scoped table is `ToTable("#name")`; no `IF NOT EXISTS`, no column list) |
| ClickHouse | `ToTable` yes — `CREATE TABLE ... ENGINE = MergeTree ORDER BY tuple() AS SELECT`; `ToTempTable` no (a temporary table takes explicit columns and no `AS SELECT`) |
| In-memory | no (read-only context) |

On a provider without the requested form, and on the in-memory context, the terminal throws `NotSupportedException`.

## Notes and phase-1 limits

* The terminal returns `void` (not a row count): `CREATE TABLE AS SELECT` reports no meaningful affected rows.
* On SQL Server a session-scoped table is created by naming it with the `#` prefix and using `ToTable` (T-SQL has no `CREATE TEMPORARY TABLE ... AS SELECT`); read it back with `From("#name")`:
  ```csharp
  ctx.From<IOrder>().Select(x => new { x.Id, x.Total }).ToTable("#recent_orders");
  var rows = ctx.From("#recent_orders").Select(t => t.GetInt32("id")).ToList();
  ```
* ClickHouse materialises into `MergeTree` with an empty sorting key (`ENGINE = MergeTree ORDER BY tuple()`, an explicit engine is required); a custom engine/sorting key is not part of this API.
* Only a raw `From("t")` read-back is supported; mapping the created table onto an entity is out of scope.
* Dropping the table (`DROP TABLE`) and adding indexes to it are not part of this API; use the connection's `ExecuteNonQuery` for those (see [Raw SQL](14-raw-sql.md)).

## See also

- [Common table expressions](09-cte.md)
- [Data modification (INSERT)](19-insert-statement.md)
- [Transactions](25-transactions.md)
- [Provider overview](../providers/overview.md)
