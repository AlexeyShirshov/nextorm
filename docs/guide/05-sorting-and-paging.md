# Sorting and paging

> Order rows with [`OrderBy`](xref:NextORM.Core.EntityBuilder`1.OrderBy(System.Int32))/[`OrderByDescending`](xref:NextORM.Core.EntityBuilder`1.OrderByDescending(System.Int32)), slice them with [`Limit`](xref:NextORM.Core.Paging.Limit)/[`Offset`](xref:NextORM.Core.Paging.Offset)/[`Page`](xref:NextORM.Core.EntityBuilder`1.Page(System.Int32,System.Int32)), and read one row or a boolean with the single-row terminals.

**Prerequisites:** [Querying and projections](01-querying-and-projections.md) · [Filtering (WHERE)](02-filtering-where.md)

## Overview

Ordering and paging are applied to the command before it executes, so they become part of the
generated statement rather than a client-side operation:

* [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1) exposes `OrderBy(Expression<Func<TEntity, object?>>, OrderDirection)`,
  `OrderBy(expr)`, `OrderByDescending(expr)` and the ordinal overloads `OrderBy(int)`,
  `OrderBy(int, OrderDirection)`, `OrderByDescending(int)`.
* [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1) also exposes `Limit(int)`, `Offset(int)` and `Page(int limit, int offset)`.
* After [`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})), the returned [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) mirrors the builder surface: the expression
  overloads `OrderBy(expr)`, `OrderBy(expr, OrderDirection)` and `OrderByDescending(expr)`, the ordinal
  overloads `OrderBy(int columnIndex, OrderDirection direction)`, `OrderBy(int)` and `OrderByDescending(int)`,
  and `Limit(int)`, `Offset(int)` and `Page(int limit, int offset)` (see [Ordering and paging a projected command](#ordering-and-paging-a-projected-command)).

Each call appends to an immutable builder, so a second [`OrderBy`](xref:NextORM.Core.EntityBuilder`1.OrderBy(System.Int32)) adds a tie-break key and leaves the
first one in place. The terminal method ([`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), [`FirstAsync`](xref:NextORM.Core.EntityBuilderExtensions.FirstAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), [`AnyAsync`](xref:NextORM.Core.EntityBuilderExtensions.AnyAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), ...) executes the
command.

## Ordering by expression

```csharp
var last = await dataContext.From<SimpleEntity>()
    .OrderByDescending(it => it.Id)
    .Select(it => it.Id)
    .FirstAsync();
```

```sql
-- SQLite
select id from simple_entity order by id desc limit 1
```

The `Output:` tables below show the rows returned by each example against the integration-test seed data (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Output:

| Id |
|----|
| 10 |

Ascending is the default and is not written explicitly:

```csharp
var rows = await dataContext.From<ComplexEntity>()
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
var last = await dataContext.From<SimpleEntity>()
    .Select(it => it.Id)
    .OrderByDescending(1)
    .First();
```

```sql
-- SQLite
select id from simple_entity order by 1 desc limit 1
```

Output:

| Id |
|----|
| 10 |

The entity builder accepts an ordinal with an explicit direction as well, for example
`.OrderBy(2, OrderDirection.Asc)`.

## Ordering and paging a projected command

A projected [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) supports ordering by an expression over
`TResult` and paging, so a grouped/aggregated query does not have to repeat the aggregate in an
outer query. Each member of the sort expression is resolved to the projection expression that produced
the corresponding output column, so the `ORDER BY` re-emits the underlying expression rather than the
output alias:

```csharp
var rows = await dataContext.From<ComplexEntity>()
    .GroupBy(it => it.Int)
    .Select(it => new { it.Int, Count = SqlFunctions.Sql.count() })
    .OrderByDescending(it => it.Count)
    .Page(10, 0)
    .ToListAsync();
```

```sql
-- SQLite
select nullableint as 'Int', count(*) as 'Count' from complex_entity
 group by nullableint
 order by count(*) desc
limit 10 offset 0
```

`Limit(int)` sets only the page size and `Offset(int)` only the leading rows skipped; both accept the
same bounds as the builder methods (non-negative). `OrderBy(int)`/`OrderByDescending(int)` still take the
1-based ordinal of an output column, so `.Select(it => it.Id).OrderByDescending(1)` continues to work. A
sort member that is not part of the projection is rejected while the query is prepared.

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
var rows = await dataContext.From<ComplexEntity>()
    .OrderByDescending(it => it.Int)
    .Select(it => new { it.Id })
    .ToListAsync();
```

On PostgreSQL the row with a `null` `nullableint` (id `1`) is first; on SQL Server it is last. Do not
rely on a cross-provider null order in shared queries.

## Limit and Offset

`Limit(int)` caps the number of rows and `Offset(int)` skips rows before the result:

```csharp
var page = await dataContext.From<SimpleEntity>()
    .Offset(1)
    .Select(it => it.Id)
    .FirstAsync();
```

```sql
-- SQLite
select id from simple_entity limit 1 offset 1
```

Output:

| Id |
|----|
| 2 |

[`FirstAsync`](xref:NextORM.Core.EntityBuilderExtensions.FirstAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) applies its single-row paging limit (`limit 1` on SQLite and PostgreSQL, `top(1)` on
SQL Server; see below). A plain `Limit(5).Select(it => it.Id)` produces
`select id from simple_entity limit 5` on SQLite and PostgreSQL, and
`select top(5) id from simple_entity` on SQL Server.

## Page

`Page(limit, offset)` sets both bounds in one call:

```csharp
var page = await dataContext.From<SimpleEntity>()
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
`order by (select null as anyorder)` when paging without an explicit sort. When an [`OrderBy`](xref:NextORM.Core.EntityBuilder`1.OrderBy(System.Int32)) is
present it is used instead and nothing is injected.

Offset/limit rendering per provider:

| Provider | `Limit(5)` | `Offset(10)` | `Page(5, 10)` |
|---|---|---|---|
| SQLite | `limit 5` | `limit -1 offset 10` | `limit 5 offset 10` |
| SQL Server | `select top(5) ...` | `offset 10 rows` (plus injected `ORDER BY`) | `offset 10 rows fetch next 5 rows only` (plus injected `ORDER BY`) |
| PostgreSQL | `limit 5` | `offset 10` | `limit 5 offset 10` |

SQLite has no `OFFSET` without `LIMIT`, so an offset-only query emits the sentinel `limit -1`.

## Limit By (ClickHouse)

`LimitBy(limit, expr)` renders ClickHouse [`LIMIT n BY expr`](xref:NextORM.Core.ISqlDialect.LimitBy):
at most `limit` rows per distinct value of the key. The key may be a single column or an anonymous type
to key on several columns, and an overload takes a per-key `offset` (`LIMIT offset, n BY expr`). The
clause is emitted after `ORDER BY` and before the final `LIMIT`:

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .OrderBy(x => x.Id)
    .LimitBy(2, x => x.Int)
    .Select(x => new { x.Id, x.Int })
    .ToListAsync();
```

```sql
select id, nullableint from complex_entity order by id limit 2 by nullableint
```

Only ClickHouse supports it; every other SQL provider and the in-memory context throw
`NotSupportedException`.

## With Ties (PostgreSQL, SQL Server)

[`WithTies`](xref:NextORM.Core.EntityBuilder`1.WithTies) turns a page request into a `WITH TIES`
request: the result keeps every row tied with the last row of the page according to the `ORDER BY`. It
requires a positive page limit and an `ORDER BY`:

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .OrderBy(x => x.Int)
    .Limit(3)
    .WithTies()
    .Select(x => new { x.Id, x.Int })
    .ToListAsync();
```

```sql
-- PostgreSQL
select id, nullableint from complex_entity order by nullableint fetch first 3 rows with ties
```

```sql
-- SQL Server (no offset): TOP(n) WITH TIES
select top(3) with ties id, nullableint from complex_entity order by nullableint
```

Only PostgreSQL and SQL Server support `WITH TIES`; every other SQL provider and the in-memory context
throw `NotSupportedException`. Combining it with `DISTINCT`/`DISTINCT ON` is rejected.

## First, FirstOrDefault, Single, SingleOrDefault

The single-row terminals are available in synchronous and asynchronous forms:

| Terminal | Result |
|---|---|
| [`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})) / [`FirstAsync`](xref:NextORM.Core.EntityBuilderExtensions.FirstAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) | the first row; throws `InvalidOperationException` if the sequence is empty |
| [`FirstOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.FirstOrDefault``1(NextORM.Core.EntityBuilder{``0})) / [`FirstOrDefaultAsync`](xref:NextORM.Core.EntityBuilderExtensions.FirstOrDefaultAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) | the first row, or `default` if empty |
| [`Single`](xref:NextORM.Core.EntityBuilderExtensions.Single``1(NextORM.Core.EntityBuilder{``0})) / [`SingleAsync`](xref:NextORM.Core.EntityBuilderExtensions.SingleAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) | exactly one row; throws if empty or more than one |
| [`SingleOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.SingleOrDefault``1(NextORM.Core.EntityBuilder{``0})) / [`SingleOrDefaultAsync`](xref:NextORM.Core.EntityBuilderExtensions.SingleOrDefaultAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) | the only row, or `default` if empty; throws if more than one |

```csharp
var first = await dataContext.From<SimpleEntity>()
    .OrderBy(it => it.Id)
    .Select(it => it.Id)
    .FirstAsync();
```

```sql
-- SQLite
select id from simple_entity order by id limit 1
```

Output:

| Id |
|----|
| 1 |

```csharp
var only = await dataContext.From<SimpleEntity>()
    .Where(it => it.Id == 2)
    .Select(it => it.Id)
    .SingleAsync();
```

```sql
-- SQLite
select id from simple_entity where id = 2 limit 2
```

Output:

| Id |
|----|
| 2 |

The limit is how a single row is enforced without a second round trip:

* [`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})) / [`FirstOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.FirstOrDefault``1(NextORM.Core.EntityBuilder{``0})) set `Paging.Limit = 1` (and [`SingleRow`](xref:NextORM.Core.QueryCommand.SingleRow)) on the command.
* [`Single`](xref:NextORM.Core.EntityBuilderExtensions.Single``1(NextORM.Core.EntityBuilder{``0})) / [`SingleOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.SingleOrDefault``1(NextORM.Core.EntityBuilder{``0})) set `Paging.Limit = 2`; if the provider returns two rows the terminal
  throws, so [`Single`](xref:NextORM.Core.EntityBuilderExtensions.Single``1(NextORM.Core.EntityBuilder{``0})) can never silently truncate a result set.

These limits are part of the command's shape and of the query-plan cache key, and they are applied
whether or not the query already had a [`Limit`](xref:NextORM.Core.Paging.Limit)/``

## Any

[`Any`](xref:NextORM.Core.EntityBuilderExtensions.Any``1(NextORM.Core.EntityBuilder{``0})) and [`AnyAsync`](xref:NextORM.Core.EntityBuilderExtensions.AnyAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) are available on both [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1) and [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1). They
emit an `exists(...)` predicate and read a single boolean:

```csharp
var exists = await dataContext.From<SimpleEntity>()
    .Where(it => it.Id == 100)
    .AnyAsync();
```

```sql
-- SQLite
select exists(select * from simple_entity where id = 100)
```

SQL Server has no boolean scalar, so it renders the same predicate as
`select cast(case when exists(...) then 1 else 0 end as bit)`. Unlike [`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})), [`Any`](xref:NextORM.Core.EntityBuilderExtensions.Any``1(NextORM.Core.EntityBuilder{``0})) does not need the
row data, so the projection is discarded and only existence is tested.

## Provider differences

| Provider | Behaviour |
|---|---|
| SQLite | `LIMIT` / `OFFSET`; offset without limit emits `limit -1 offset n`; nulls sort as the smallest value. |
| SQL Server | `TOP(n)` when there is no offset; `OFFSET n ROWS` / `FETCH NEXT n ROWS ONLY` otherwise, with an injected `ORDER BY`; nulls sort as the smallest value. |
| PostgreSQL | `LIMIT` / `OFFSET`; nulls sort as the largest value. |
| MySQL | `LIMIT` / `OFFSET`; offset without limit emits `limit 18446744073709551615 offset n`; nulls sort as the smallest value (first ascending). |
| MariaDB | Same as MySQL. |
| ClickHouse | `LIMIT` / `OFFSET`; offset without limit emits `limit 18446744073709551615 offset n`. |
| In-memory | [`OrderBy`](xref:NextORM.Core.EntityBuilder`1.OrderBy(System.Int32)) / [`OrderByDescending`](xref:NextORM.Core.EntityBuilder`1.OrderByDescending(System.Int32)) run through LINQ; nulls sort first ascending (the CLR default comparer). |

## See also

* [Querying and projections](01-querying-and-projections.md)
* [Filtering (WHERE)](02-filtering-where.md)
* [Query reuse: cache vs Prepare](15-query-reuse.md)
* [Provider overview](../providers/overview.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:479`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:489`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:499`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:510`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:521`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:532`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:540`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:551`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:562`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:573`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:592`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:602`,
`tests/nextorm.integration.tests/CommonTestSuite.LinqExtensions.cs:8`,
`tests/nextorm.integration.tests/PostgresSpecificTests.cs:24`,
`tests/nextorm.integration.tests/SqlServerSpecificTests.cs:24`,
`tests/nextorm.integration.tests/SqlServerSpecificTests.cs:43`,
`tests/nextorm.integration.tests/SqliteSpecificTests.cs:44`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:133`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:142`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:146`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:160`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:173`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:130`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:139`;
`src/nextorm.core/Query/QueryCommand.TResult.cs:147`,
`src/nextorm.core/Query/QueryCommand.TResult.cs:197`.
