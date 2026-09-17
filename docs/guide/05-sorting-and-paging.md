# Sorting and paging

> Order rows with `OrderBy`/`OrderByDescending`, slice them with `Limit`/`Offset`/`Page`, and read one row or a boolean with the single-row terminals.

**Prerequisites:** [Querying and projections](01-querying-and-projections.md) · [Filtering (WHERE)](02-filtering-where.md)

## Overview

Ordering and paging are applied to the command before it executes, so they become part of the
generated statement rather than a client-side operation:

* `Entity<TEntity>` exposes `OrderBy(Expression<Func<TEntity, object?>>, OrderDirection)`,
  `OrderBy(expr)`, `OrderByDescending(expr)` and the ordinal overloads `OrderBy(int)`,
  `OrderBy(int, OrderDirection)`, `OrderByDescending(int)`.
* `Entity<TEntity>` also exposes `Limit(int)`, `Offset(int)` and `Page(int limit, int offset)`.
* After `Select`, the returned `QueryCommand<TResult>` has the ordinal overloads only:
  `OrderBy(int columnIndex, OrderDirection direction)`, `OrderBy(int)` and `OrderByDescending(int)`.

Each call appends to an immutable builder, so a second `OrderBy` adds a tie-break key and leaves the
first one in place. The terminal method (`ToListAsync`, `FirstAsync`, `AnyAsync`, ...) executes the
command.

## Ordering by expression

```csharp
var last = await dataContext.Create<SimpleEntity>()
    .OrderByDescending(it => it.Id)
    .Select(it => it.Id)
    .FirstAsync();
```

```sql
-- SQLite
select id from simple_entity order by id desc limit 1
```

Ascending is the default and is not written explicitly:

```csharp
var rows = await dataContext.Create<ComplexEntity>()
    .OrderBy(it => it.Int)
    .OrderByDescending(it => it.Id)
    .Select(it => new { it.Id })
    .ToListAsync();
```

```sql
select id from complex_entity order by nullableint, id desc
```

Keys are emitted in the order the calls were made and separated by commas.

## Ordering by projected column ordinal

After a projection, the sort key can be the ordinal of a select-list column (1-based). This is the
usual way to order by a computed column without repeating the expression:

```csharp
var last = await dataContext.Create<SimpleEntity>()
    .Select(it => it.Id)
    .OrderByDescending(1)
    .First();
```

```sql
-- SQLite
select id from simple_entity order by 1 desc limit 1
```

The entity builder accepts an ordinal with an explicit direction as well, for example
`.OrderBy(2, OrderDirection.Asc)`.

## NULL ordering

nextorm does not emit `NULLS FIRST` / `NULLS LAST`; null placement is whatever the provider does by
default:

| Provider | `ASC` | `DESC` |
|---|---|---|
| SQLite | nulls first | nulls last |
| SQL Server | nulls first | nulls last |
| PostgreSQL | nulls last | nulls first |
| In-memory | nulls first | nulls last |

```csharp
var rows = await dataContext.Create<ComplexEntity>()
    .OrderByDescending(it => it.Int)
    .Select(it => new { it.Id })
    .ToListAsync();
```

On PostgreSQL the row with a `null` `nullableint` (id `1`) is first; on SQL Server it is last. Do not
rely on a cross-provider null order in shared queries.

## Limit and Offset

`Limit(int)` caps the number of rows and `Offset(int)` skips rows before the result:

```csharp
var page = await dataContext.Create<SimpleEntity>()
    .Offset(1)
    .Select(it => it.Id)
    .FirstAsync();
```

```sql
-- SQLite
select id from simple_entity limit 1 offset 1
```

`FirstAsync` applies its single-row paging limit (`limit 1` on SQLite and PostgreSQL, `top(1)` on
SQL Server; see below). A plain `Limit(5).Select(it => it.Id)` produces
`select id from simple_entity limit 5` on SQLite and PostgreSQL, and
`select top(5) id from simple_entity` on SQL Server.

## Page

`Page(limit, offset)` sets both bounds in one call:

```csharp
var page = await dataContext.Create<SimpleEntity>()
    .Page(5, 10)
    .Select(it => it.Id)
    .ToListAsync();
```

| Provider | SQL for `Page(5, 10)` |
|---|---|
| SQLite | `select id from simple_entity limit 5 offset 10` |
| PostgreSQL | `select id from simple_entity limit 5 offset 10` |
| SQL Server | `select id from simple_entity order by (select null as anyorder) offset 10 rows fetch next 5 rows only` |

SQL Server rejects `OFFSET ... FETCH` without an `ORDER BY`, so the provider injects
`order by (select null as anyorder)` when paging without an explicit sort. When an `OrderBy` is
present it is used instead and nothing is injected.

Offset/limit rendering per provider:

| Provider | `Limit(5)` | `Offset(10)` | `Page(5, 10)` |
|---|---|---|---|
| SQLite | `limit 5` | `limit -1 offset 10` | `limit 5 offset 10` |
| SQL Server | `select top(5) ...` | `offset 10 rows` (plus injected `ORDER BY`) | `offset 10 rows fetch next 5 rows only` (plus injected `ORDER BY`) |
| PostgreSQL | `limit 5` | `offset 10` | `limit 5 offset 10` |

SQLite has no `OFFSET` without `LIMIT`, so an offset-only query emits the sentinel `limit -1`.

## First, FirstOrDefault, Single, SingleOrDefault

The single-row terminals are available in synchronous and asynchronous forms:

| Terminal | Result |
|---|---|
| `First()` / `FirstAsync()` | the first row; throws `InvalidOperationException` if the sequence is empty |
| `FirstOrDefault()` / `FirstOrDefaultAsync()` | the first row, or `default` if empty |
| `Single()` / `SingleAsync()` | exactly one row; throws if empty or more than one |
| `SingleOrDefault()` / `SingleOrDefaultAsync()` | the only row, or `default` if empty; throws if more than one |

```csharp
var first = await dataContext.Create<SimpleEntity>()
    .OrderBy(it => it.Id)
    .Select(it => it.Id)
    .FirstAsync();
```

```sql
-- SQLite
select id from simple_entity order by id limit 1
```

```csharp
var only = await dataContext.Create<SimpleEntity>()
    .Where(it => it.Id == 2)
    .Select(it => it.Id)
    .SingleAsync();
```

```sql
-- SQLite
select id from simple_entity where id = 2 limit 2
```

The limit is how a single row is enforced without a second round trip:

* `First` / `FirstOrDefault` set `Paging.Limit = 1` (and `SingleRow`) on the command.
* `Single` / `SingleOrDefault` set `Paging.Limit = 2`; if the provider returns two rows the terminal
  throws, so `Single` can never silently truncate a result set.

These limits are part of the command's shape and of the query-plan cache key, and they are applied
whether or not the query already had a `Limit`/`Offset`.

## Any

`Any()` and `AnyAsync()` are available on both `Entity<TEntity>` and `QueryCommand<TResult>`. They
emit an `exists(...)` predicate and read a single boolean:

```csharp
var exists = await dataContext.Create<SimpleEntity>()
    .Where(it => it.Id == 100)
    .AnyAsync();
```

```sql
-- SQLite
select exists(select * from simple_entity where id = 100)
```

SQL Server has no boolean scalar, so it renders the same predicate as
`select cast(case when exists(...) then 1 else 0 end as bit)`. Unlike `First`, `Any` does not need the
row data, so the projection is discarded and only existence is tested.

## Provider differences

| Provider | Behaviour |
|---|---|
| SQLite | `LIMIT` / `OFFSET`; offset without limit emits `limit -1 offset n`; nulls sort as the smallest value. |
| SQL Server | `TOP(n)` when there is no offset; `OFFSET n ROWS` / `FETCH NEXT n ROWS ONLY` otherwise, with an injected `ORDER BY`; nulls sort as the smallest value. |
| PostgreSQL | `LIMIT` / `OFFSET`; nulls sort as the largest value. |
| In-memory | `OrderBy` / `OrderByDescending` run through LINQ; nulls sort first ascending (the CLR default comparer). |

## See also

* [Querying and projections](01-querying-and-projections.md)
* [Filtering (WHERE)](02-filtering-where.md)
* [Query reuse: cache vs Prepare](15-query-reuse.md)
* [Provider overview](../providers/overview.md)

---

Source: `test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:479`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:489`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:499`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:510`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:521`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:532`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:540`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:551`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:562`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:573`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:592`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:602`,
`test/nextorm.integration.tests/CommonTestSuite.LinqExtensions.cs:8`,
`test/nextorm.integration.tests/PostgresSpecificTests.cs:24`,
`test/nextorm.integration.tests/SqlServerSpecificTests.cs:24`,
`test/nextorm.integration.tests/SqlServerSpecificTests.cs:43`,
`test/nextorm.integration.tests/SqliteSpecificTests.cs:44`;
generated SQL: `test/nextorm.sqlite.tests/SqlGenerationTests.cs:133`,
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:142`,
`test/nextorm.sqlserver.tests/SqlGenerationTests.cs:146`,
`test/nextorm.sqlserver.tests/SqlGenerationTests.cs:160`,
`test/nextorm.sqlserver.tests/SqlGenerationTests.cs:173`,
`test/nextorm.postgres.tests/SqlGenerationTests.cs:130`,
`test/nextorm.postgres.tests/SqlGenerationTests.cs:139`;
`src/nextorm.core/Query/QueryCommand.TResult.cs:147`,
`src/nextorm.core/Query/QueryCommand.TResult.cs:197`.
