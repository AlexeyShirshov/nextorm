# Materializing a query into a table (`CREATE TABLE ... AS SELECT`)

> Use a temporary table when a later query on the same connection needs the rows
> from a query. `ToTempTable(name)` creates it immediately, while `AsTempTable()`
> creates a fresh table when a later query reads the source. Use `ToTable(name)`
> for a persistent table; provider-specific temporary-table forms are covered
> later. Unlike a
> [common table expression](10-cte.md), a materialized table is available to
> later queries on the connection. nextorm does not track changes or provide
> `SaveChanges`; each terminal issues one command or batch.

**Prerequisites:** [Common table expressions](10-cte.md) · [Data modification (INSERT)](17-insert-statement.md) · [Raw SQL](14-raw-sql.md) · [Provider overview](../connecting-providers/overview.md)

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

The target name is required and is used verbatim (only identifier quoting applies); the naming convention is **not** applied, because a raw name is not an entity. When you do not want to invent one, use the [lazy `AsTempTable`](#lazy-temporary-tables-astemptable) form instead: it generates a `__nextorm_temp_xxxxxxxx` name for you and materialises on every read. Values captured in the query become parameters and travel with the body, exactly as in a standalone query.

A temporary table is session-scoped. Create and read it on the **same context** (the context keeps one connection open), then read it back through `From("name")` with the [`TableAlias`](xref:NextORM.Core.TableAlias) accessors. A connection pooler that reassigns the backend per transaction needs one transaction around both steps — see [Session affinity and connection poolers](#session-affinity-and-connection-poolers):

```csharp
var orders = ctx.From("recent_orders")
    .Select(t => new { Id = t.GetInt32("id"), Total = t.GetDecimal("total") })
    .ToList();
```

`ToTempTableSql(name)` renders the statement without opening a connection; `ToTempTable` / `ToTempTableAsync` execute it.

## Creating an empty temporary table with a primary key

Use `CreateEmptyTempTable` on a SQLite or PostgreSQL `DataContext` when you need a table with an explicit schema instead of rows copied from a query:

```csharp
await db.CreateEmptyTempTable("keys_pick", [("id", typeof(string))], "id",
    withoutRowId: true); // SQLite only; pass false on PostgreSQL.

await db.CreateBulkInsertBuilder<KeyRow>(options => options
        .Table("keys_pick").TargetKind(BulkInsertTargetKind.LocalTemporary))
    .Values(keys)
    .BulkInsertAsync(cancellationToken);
```

Map `KeyRow` to the declared column names and use the same context and database session for both operations. The primary key must name one declared column exactly. Supported CLR types are `string`, `int`, `long`, `double`, `decimal`, `bool`, and `byte[]`, including nullable forms of the value types. PostgreSQL rejects `withoutRowId: true`. This API does not change `ToTempTableAsync` or `BatchBuilder.CreateTempTable`, which materialize queries.

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

## Creating and reading in one batch

A materialisation and the query that reads it can be sent as one batch — one round trip on one server session, which keeps a session-scoped table visible under a connection-level pooler. Use the general [`BatchBuilder`](xref:NextORM.Core.BatchBuilder); it is covered in [Executing statements in one batch](25-sql-batch.md).

```csharp
var orders = ctx.CreateBatchBuilder()
    .CreateTempTable("recent_orders", ctx.From<IOrder>()
        .Where(x => x.Total > minTotal)
        .Select(x => new { x.Id, x.Total }))
    .Query(ctx.From("recent_orders")
        .Select(t => new { Id = t.GetInt32("id") }))
    .ToList();
```

The materialisation and the read go to the database as one batch; `.ToSql()` returns the `;`-joined text it sends:

```sql
create temporary table recent_orders as select id, total
 from orders
 where (total > @b0_minTotal); select id
 from recent_orders
```

The captured `minTotal` becomes a parameter, and its name is prefixed per statement (`b0_minTotal`) so it cannot collide with a same-named variable in another statement of the batch.

## Lazy temporary tables (`AsTempTable`)

`AsTempTable()` turns a query into a **lazy** source: nothing is executed. The table is created (and read) only when a query reads the source through `From(...)`, and every read does so as one batch — `DROP TABLE IF EXISTS`, `CREATE TEMPORARY TABLE ... AS SELECT` and the read — on a single server session. The table is therefore always freshly materialised and the read never depends on connection pinning:

```csharp
var source = ctx.From<IOrder>()
    .Where(x => x.Total > minTotal)
    .Select(x => new { x.Id, x.Total })
    .AsTempTable();   // nothing executed

var orders = ctx.From(source)   // materialises and reads in one batch
    .Select(t => new { Id = t.GetInt32("id"), Total = t.GetDecimal("total") })
    .ToList();
```

`source.Name` is the generated `__nextorm_temp_xxxxxxxx` name; the naming convention is not applied. Reading the same source twice re-materialises it each time (drop and re-create), so it never returns stale rows and never fails with `table already exists`. Because the materialisation is batched with the read, this form needs no `BeginTransaction` around the two steps even behind a transaction-level pooler.

`AsTempTable()` is available on a built query (after `Select`) and on a builder (every column of the source); add [`CreateTableOptions`](xref:NextORM.Core.CreateTableOptions) to control the statement. It is supported on the same providers as `ToTempTable` (PostgreSQL, SQLite, MySQL, MariaDB); SQL Server, ClickHouse and the in-memory context reject it when the query renders.

`ToBatchSql()` renders the whole batch without executing it:

```csharp
var sql = ctx.From(source)
    .Select(t => new { Id = t.GetInt32("id") })
    .ToBatchSql();
// drop table if exists __nextorm_temp_xxxxxxxx;
// create temporary table __nextorm_temp_xxxxxxxx as select ...;
// select id
// from __nextorm_temp_xxxxxxxx
```

Scalar terminals (`Count`, `Any`, …) and all materialisation terminals (`ToList`, `First`, `Single`, `ToAsyncEnumerable`, …) run the batch too.

## Creating a persistent table

`ToTable(name)` uses the same query but materialises into a persistent table (on SQL Server it renders `SELECT ... INTO <name>`):

```csharp
ctx.From<IOrder>().Select(x => new { x.Id, x.Total }).ToTable("order_archive");
```

Unlike a temporary table, a persistent table outlives the session (and is not tied to one connection).

## Options

Pass [`CreateTableOptions`](xref:NextORM.Core.CreateTableOptions) to control the statement:

```csharp
ctx.From<IOrder>()
    .Select(x => new { x.Id })
    .ToTempTable("recent_orders", new CreateTableOptions
    {
        IfNotExists = true,
        Columns = ["order_id"],
        OnCommit = TempTableOnCommit.Drop,
        WithData = false,
    });
```

Every terminal also accepts a [`CreateTableOptionsBuilder`](xref:NextORM.Core.CreateTableOptionsBuilder) callback, which configures the same options fluently; the terminal builds the options from it:

```csharp
ctx.From<IOrder>()
    .Select(x => new { x.Id })
    .ToTempTable("recent_orders", o => o
        .IfNotExists()
        .Columns("order_id")
        .OnCommit(TempTableOnCommit.Drop)
        .WithData(false));
```

| Option | Effect | Providers |
|---|---|---|
| `IfNotExists` | Adds `IF NOT EXISTS`, so repeating the statement is a no-op. | PostgreSQL, SQLite, MySQL, MariaDB, ClickHouse (SQL Server's `SELECT ... INTO` rejects it) |
| `DropExisting` | Renders `DROP TABLE IF EXISTS <name>` before the create, so re-running replaces the table with the current query's rows. Persistent `ToTable` only; mutually exclusive with `IfNotExists`. | All providers |
| `Columns` | Declares the target column names instead of deriving them from the query. | PostgreSQL, MySQL, MariaDB (SQLite derives every column; SQL Server takes the names from the select list; ClickHouse needs `name type` pairs) |
| `OnCommit` | `ON COMMIT { PRESERVE ROWS \| DELETE ROWS \| DROP }` of a temporary table; only valid with `ToTempTable`. | PostgreSQL |
| `WithData` | `false` renders `WITH NO DATA` (the table is created empty). | PostgreSQL |

An option a provider cannot express is rejected with `NotSupportedException` when the SQL is built, never silently ignored.

To re-create a persistent table from scratch, set `DropExisting` instead of dropping it yourself:

```csharp
ctx.From<IOrder>().Select(x => new { x.Id, x.Total })
    .ToTable("order_archive", o => o.DropExisting());
```

This runs `DROP TABLE IF EXISTS order_archive` followed by the create, so the table always reflects the current query. It is only valid for a persistent `ToTable` (a temporary table is session-scoped) and cannot be combined with `IfNotExists`; a contradictory or provider-unsupported combination is rejected before the drop runs, so the target is never left missing.

## Placement and routing

The target name is literal, so a materialisation is routed by the entities its **source query** reads, not
by the row type it projects: `ToTable`/`ToTempTable` on a routed entity creates the table on that entity's
topology host, and on a bucket-scoped query it creates it on that bucket's shard, rendered under the same
scope as the query. With `DropExisting` the `DROP` follows the create to the same host, so a
materialisation never drops a table on one server and creates it on another. The lazy `AsTempTable` read is
one batch (drop, create, read) and obeys the same rule — see
[One batch, one topology](34-routing-data-to-topologies.md#one-batch-one-topology).

## Provider support

| Provider | `CREATE TABLE ... AS SELECT` |
|---|---|
| PostgreSQL | yes — `CREATE [TEMPORARY] TABLE ... AS SELECT` (`TEMPORARY`/`TEMP`, column list, `ON COMMIT`, `WITH [NO] DATA`) |
| SQLite | yes — `CREATE [TEMPORARY] TABLE ... AS SELECT` (`TEMP`/`TEMPORARY`; no column list, no `ON COMMIT`, no `WITH NO DATA`) |
| MySQL / MariaDB | yes — `CREATE [TEMPORARY] TABLE ... AS SELECT` (`TEMPORARY`, column list) |
| SQL Server | `ToTable` yes — `SELECT ... INTO <table>`; `ToTempTable` no (a session-scoped table is `ToTable("#name")`; no `IF NOT EXISTS`, no column list) |
| ClickHouse | `ToTable` yes — `CREATE TABLE ... ENGINE = MergeTree ORDER BY tuple() AS SELECT`; `ToTempTable` no (a temporary table takes explicit columns and no `AS SELECT`) |
| In-memory | no (read-only context) |

On a provider without the requested form, and on the in-memory context, the terminal throws `NotSupportedException`. The lazy `AsTempTable` form follows `ToTempTable` (temporary column): it is supported exactly where `ToTempTable` is.

## Notes and limitations

* The terminal returns `void` (not a row count): `CREATE TABLE AS SELECT` reports no meaningful affected rows.
* On SQL Server a session-scoped table is created by naming it with the `#` prefix and using `ToTable` (T-SQL has no `CREATE TEMPORARY TABLE ... AS SELECT`); read it back with `From("#name")`:
  ```csharp
  ctx.From<IOrder>().Select(x => new { x.Id, x.Total }).ToTable("#recent_orders");
  var rows = ctx.From("#recent_orders").Select(t => t.GetInt32("id")).ToList();
  ```
* ClickHouse materialises into `MergeTree` with an empty sorting key (`ENGINE = MergeTree ORDER BY tuple()`, an explicit engine is required); a custom engine/sorting key is not part of this API.
* Only a raw `From("t")` read-back is supported; mapping the created table onto an entity is out of scope.
* Dropping a table that is not being re-created (`DROP TABLE`), and adding indexes to it, are not part of this API; use the connection's `ExecuteNonQuery` for those (see [Raw SQL](14-raw-sql.md)). For the common "materialise again" case, use `CreateTableOptions.DropExisting` above.

## See also

- [Common table expressions](10-cte.md)
- [Data modification (INSERT)](17-insert-statement.md)
- [Executing statements in one batch](25-sql-batch.md)
- [Transactions](23-transactions.md)
- [Provider overview](../connecting-providers/overview.md)

## Temporary views on SQLite and PostgreSQL

Use `await query.ToTempView("message")` to define a named, session-local view from a NextORM query. Unlike `ToTempTableAsync`, this operation does not copy rows. For example, if a temporary key table already exists, join it to a schema-qualified permanent table and project the columns that the view should expose:

```csharp
// SQLite: use "main" for the permanent table and "temp" for the key table.
// PostgreSQL: use "public" and "pg_temp" for these example schemas.
var source = db.From<Message>().WithSchema("main");
var keys = db.From<SelectedKey>().WithSchema("temp");
var selected = db.From(source.ToCommand())
    .Join(keys.ToCommand(), (message, key) => message.SessionId == key.Id)
    .Select(row => row.Item1);
await selected.ToTempView("message");
```

Keep the same database session open while reading the view. If `Message` maps to `message` without an explicit schema, `db.From<Message>()` resolves to the view on that session (with PostgreSQL's standard `search_path`). An explicitly qualified query such as `source` still reads the permanent table, and other sessions cannot see the view. Changing the key rows changes subsequent reads without recreating the view.

Pass a raw, unqualified view name: NextORM quotes it as one identifier. The defining query must not contain bound SQL parameters, including captured values rendered as parameters; NextORM rejects them before sending DDL. Use a join to the temporary key table instead of passing the keys as parameters.
