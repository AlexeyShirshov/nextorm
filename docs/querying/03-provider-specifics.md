# Provider-specific behaviour

## JSON output (SQL Server)

[`ForJson`](xref:NextORM.Core.QueryCommand`1.ForJson(NextORM.Core.ForJsonMode,System.String,System.Boolean,System.Object[])) is a **terminal operator**: it executes the query and returns the
whole result set as one JSON document ([`SupportsForJson`](xref:NextORM.Core.ISqlDialect.SupportsForJson)). Because it is
terminal it applies no implicit `TOP 1`, so the document covers every row; the query element type is
irrelevant since the database returns a single document column:

```csharp
string? json = dataContext.From<IComplexEntity>()
    .Select(e => new { e.Id, e.String })
    .ForJson(ForJsonMode.Path, root: "items", includeNullValues: true);
```

```sql
select id, somestring from complex_entity for json path, root('items'), include_null_values
```

[`Path`](xref:NextORM.Core.ForJsonMode.Path) shapes the document from the projection aliases, [`Auto`](xref:NextORM.Core.ForJsonMode.Auto) from the table
structure. `ForJson` returns `null` when the query produces no rows (SQL Server returns SQL NULL for an
empty `FOR JSON` result). The clause is placed after `ORDER BY` and before a trailing `OPTION (...)`;
other providers throw `NotSupportedException`. Use
[`WithForJson`](xref:NextORM.Core.QueryCommand`1.WithForJson(NextORM.Core.ForJsonMode,System.String,System.Boolean)) to only *attach* the clause and keep the command composable (for further
hints or SQL inspection).

## XML output (SQL Server)

[`ForXml`](xref:NextORM.Core.QueryCommand`1.ForXml(NextORM.Core.ForXmlMode,System.String,System.String,System.Boolean,System.Object[])) is the XML counterpart and terminal ([`SupportsForXml`](xref:NextORM.Core.ISqlDialect.SupportsForXml)); it supports
`RAW`, `AUTO`, `EXPLICIT` and `PATH`, with an optional row element name, a `ROOT('...')` wrapper and the
`ELEMENTS` flag:

```csharp
string? xml = dataContext.From<IComplexEntity>()
    .Select(e => new { e.Id })
    .ForXml(ForXmlMode.Raw, elementName: "row", root: "items", elements: true);
```

```sql
select id from complex_entity for xml raw('row'), root('items'), elements
```

Like `ForJson`, `ForXml` returns `null` for an empty result set, and
[`WithForXml`](xref:NextORM.Core.QueryCommand`1.WithForXml(NextORM.Core.ForXmlMode,System.String,System.String,System.Boolean)) attaches the clause without executing. `FOR JSON` and `FOR XML` are mutually
exclusive; combining them throws `NotSupportedException`.

## Row locking (`FOR UPDATE` / `FOR SHARE`)

[`ForUpdate`](xref:NextORM.Core.EntityBuilder`1.ForUpdate) and
[`ForShare`](xref:NextORM.Core.EntityBuilder`1.ForShare) lock the selected rows until the surrounding
transaction ends. PostgreSQL, MySQL and MariaDB emit a trailing clause placed last, after `WHERE`,
`ORDER BY` and a page request; SQL Server attaches a `WITH (updlock)`/`WITH (holdlock)` table hint to
the primary source instead:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Where(x => x.Id > 5)
    .ForUpdate()
    .ToListAsync();
```

```sql
-- PostgreSQL / MySQL / MariaDB
select id from simple_entity where (id > 5) for update

-- SQL Server
select id from simple_entity with (updlock) where (id > 5)
```

`ForUpdate()` locks rows exclusively; [`ForShare`](xref:NextORM.Core.EntityBuilder`1.ForShare) takes a
shared lock — PostgreSQL renders `for share`, MySQL/MariaDB render `lock in share mode`, and SQL Server
renders `holdlock` (shared) versus `updlock` for `ForUpdate`. The clause is implemented by PostgreSQL,
MySQL, MariaDB and SQL Server ([`Lock`](xref:NextORM.Core.ISqlDialect.Lock); SQL Server uses
[`ILockRenderer.UsesTableHints`](xref:NextORM.Core.ILockRenderer.UsesTableHints));
every other provider throws `NotSupportedException` when the SQL is built.

Pass a [`LockWaitMode`](xref:NextORM.Core.LockWaitMode) to control what happens when another transaction
already holds the row: [`NoWait`](xref:NextORM.Core.LockWaitMode.NoWait) fails immediately instead of
waiting, and [`SkipLocked`](xref:NextORM.Core.LockWaitMode.SkipLocked) leaves the locked rows out of the
result — the standard way to build a queue or worker pool:

```csharp
var claimed = await dataContext.From<Job>()
    .Where(x => x.State == "pending")
    .ForUpdate(LockWaitMode.SkipLocked)
    .ToListAsync();
```

```sql
-- PostgreSQL / MySQL / MariaDB
select id from job where (state = 'pending') for update skip locked

-- SQL Server (READPAST approximates SKIP LOCKED)
select id from job with (updlock, readpast) where (state = 'pending')
```

PostgreSQL and MySQL append the trailing `nowait`/`skip locked` (`FOR UPDATE`/`FOR SHARE [NOWAIT | SKIP
LOCKED]`); a shared lock with a wait mode switches MySQL from `lock in share mode` to `for share`, because
`LOCK IN SHARE MODE` takes no lock option. MariaDB appends the mode to both `for update` and `lock in
share mode` (`NOWAIT` on 10.3+, `SKIP LOCKED` on 10.6+). SQL Server adds `nowait` or `readpast` to the
same table hint (`with (updlock, nowait)` / `with (updlock, readpast)`); `readpast` skips any locked row,
not only a row locked by another writer, so it approximates rather than exactly matches `SKIP LOCKED`.
[`Wait`](xref:NextORM.Core.LockWaitMode.Wait) is the default and keeps the blocking behaviour.

## Provider differences

| Provider | Behaviour |
|---|---|
| SQLite | Column aliases are single-quoted (`as 'Calc'`); derived tables do not require an alias. |
| SQL Server | Column aliases are bracket-quoted (`as [Calc]`); every derived table must be aliased (`as [t1]`). String concatenation uses `+`. |
| PostgreSQL | Column aliases are double-quoted (`as "Calc"`); derived tables must be aliased (`as "t1"`). |
| MySQL | Column aliases are backtick-quoted (`` as `Calc` ``); every derived table must be aliased (`` as `t1` ``). String concatenation uses `concat(a, b)`. |
| MariaDB | Same as MySQL: backtick-quoted aliases, aliased derived tables and `concat(a, b)` concatenation. |
| ClickHouse | Column aliases are backtick-quoted (`` as `Calc` ``); every derived table must be aliased (`` as `t1` ``). String concatenation uses `concat(a, b)`. |
| In-memory | No SQL is generated; the projection delegates are compiled and run over in-memory objects. |

