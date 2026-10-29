# Sorting and paging

> Order rows with [`OrderBy`](xref:NextORM.Core.EntityBuilder`1.OrderBy(System.Int32))/[`OrderByDescending`](xref:NextORM.Core.EntityBuilder`1.OrderByDescending(System.Int32)), slice them with [`Limit`](xref:NextORM.Core.Paging.Limit)/[`Offset`](xref:NextORM.Core.Paging.Offset)/[`Page`](xref:NextORM.Core.EntityBuilder`1.Page(System.Int32,System.Int32)), and read one row or a boolean with the single-row terminals.

**Prerequisites:** [Querying and projections](../engine/index.md) · [Filtering (WHERE)](03-filtering-where.md)

## Overview

Ordering and paging are applied to the command before it executes, so they become part of the
generated statement rather than a client-side operation:

* [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1) exposes `OrderBy(Expression<Func<TEntity, object?>>, OrderDirection)`,
  `OrderBy(expr)`, `OrderByDescending(expr)` and the ordinal overloads `OrderBy(int)`,
  `OrderBy(int, OrderDirection)`, `OrderByDescending(int)`, plus the explicit NULL-placement overloads
  `OrderBy(expr, NullsPosition)` / `OrderBy(expr, OrderDirection, NullsPosition)` /
  `OrderByDescending(expr, NullsPosition)` and the `ThenBy`/`ThenByDescending` family (see
  [NULL ordering and placement](#null-ordering-and-placement)).
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
select id
 from simple_entity order by id desc limit 1
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
select id
 from complex_entity order by nullableint, id desc
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
select id
 from simple_entity order by 1 desc limit 1
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
select nullableint as 'Int', count(*) as 'Count'
 from complex_entity
 group by nullableint
 order by count(*) desc
limit 10 offset 0
```

`Limit(int)` sets only the page size and `Offset(int)` only the leading rows skipped; both accept the
same bounds as the builder methods (non-negative). `OrderBy(int)`/`OrderByDescending(int)` still take the
1-based ordinal of an output column, so `.Select(it => it.Id).OrderByDescending(1)` continues to work. A
sort member that is not part of the projection is rejected while the query is prepared.

## NULL ordering and placement

Every ordering key accepts an explicit NULL placement through [`NullsPosition`](xref:NextORM.Core.NullsPosition):

| Member | Value | Meaning |
|---|---|---|
| [`Default`](xref:NextORM.Core.NullsPosition.Default) | `0` | emit no clause and keep the provider default (the value of every call that passes no placement) |
| [`First`](xref:NextORM.Core.NullsPosition.First) | `1` | `NULL` values sort before every non-null value, whatever the direction |
| [`Last`](xref:NextORM.Core.NullsPosition.Last) | `2` | `NULL` values sort after every non-null value, whatever the direction |

Placement is **independent of direction**: `First` keeps the `NULL` group first in an ascending and in a
descending key, and the direction only orders the non-null values.

### Overloads

Each form below exists on [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1) and, after
[`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})), on [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1):

* `OrderBy(expr, NullsPosition)`, `OrderBy(expr, OrderDirection, NullsPosition)` and
  `OrderByDescending(expr, NullsPosition)`;
* the secondary-key family `ThenBy(expr)`, `ThenBy(expr, OrderDirection)`,
  `ThenBy(expr, NullsPosition)`, `ThenBy(expr, OrderDirection, NullsPosition)`,
  `ThenByDescending(expr)` and `ThenByDescending(expr, NullsPosition)`;
* the same families over a 1-based output column index: `OrderBy(int, NullsPosition)`,
  `OrderBy(int, OrderDirection, NullsPosition)`, `OrderByDescending(int, NullsPosition)`, `ThenBy(int)`,
  `ThenBy(int, OrderDirection)`, `ThenBy(int, NullsPosition)`, `ThenBy(int, OrderDirection, NullsPosition)`,
  `ThenByDescending(int)` and `ThenByDescending(int, NullsPosition)`.

`ThenBy` appends a key exactly like a second `OrderBy`; the name only states the intent. Each key carries
its own direction and placement, so mixed secondary keys are legal:

```csharp
var rows = await dataContext.From<ComplexEntity>()
    .OrderBy(it => it.Int, NullsPosition.Last)
    .ThenBy(it => it.Id, OrderDirection.Desc, NullsPosition.First)
    .Select(it => new { it.Id })
    .ToListAsync();
```

```sql
-- SQLite
select id
 from complex_entity order by nullableint NULLS LAST, id desc NULLS FIRST
```

```sql
-- SQL Server
select id
 from complex_entity order by CASE WHEN nullableint IS NULL THEN 1 ELSE 0 END ASC, nullableint, CASE WHEN id IS NULL THEN 0 ELSE 1 END ASC, id desc
```

An ordinal key is resolved to the expression of the selected column before the placement is applied, so the
emulated form tests the real key (`CASE WHEN nullableint IS NULL …`) and never the literal ordinal
(`CASE WHEN 1 IS NULL …`); an index that does not resolve to a selected column fails closed.

### Per-provider rendering

SQLite, PostgreSQL and ClickHouse understand the native modifier
([`SupportsNullsOrdering`](xref:NextORM.Core.ISqlDialect.SupportsNullsOrdering) is `true`). SQL Server, MySQL
and MariaDB have no `NULLS FIRST`/`NULLS LAST` syntax and get the portable emulation, whose rank prefix is
**always ascending** — even for a descending key — with only the key carrying the requested direction:

| Provider | `ASC` / `First` | `DESC` / `Last` |
|---|---|---|
| SQLite, PostgreSQL, ClickHouse | `k NULLS FIRST` | `k desc NULLS LAST` |
| SQL Server, MySQL, MariaDB | `CASE WHEN k IS NULL THEN 0 ELSE 1 END ASC, k` | `CASE WHEN k IS NULL THEN 1 ELSE 0 END ASC, k desc` |

`k` is the qualified, quoted key SQL. On the emulating providers a parameterized key occurs **twice** in the
statement (`CASE WHEN (k * @factor) IS NULL … END ASC, (k * @factor)`) and both occurrences bind to the one
parameter the command carries; the native form emits the key once.

### Provider defaults

`NullsPosition.Default` emits no clause, so the placement is the provider's own — and it differs per provider
and per direction:

| Provider | `ASC` | `DESC` |
|---|---|---|
| SQLite | nulls first | nulls last |
| SQL Server | nulls first | nulls last |
| MySQL | nulls first | nulls last |
| MariaDB | nulls first | nulls last |
| PostgreSQL | nulls last | nulls first |
| ClickHouse | **nulls last** | **nulls last** |
| In-memory | nulls first | nulls last |

ClickHouse (25.8) sorts `NULL` last in **both** directions, so unlike every other SQL provider a `DESC` key
does not move the `NULL` group to the front. Do not rely on a cross-provider null order in shared queries —
request `NullsPosition.First`/`Last` when the placement is part of the result.

```csharp
var rows = await dataContext.From<ComplexEntity>()
    .OrderByDescending(it => it.Int)
    .Select(it => new { it.Id })
    .ToListAsync();
```

With this default the row holding a `null` `nullableint` (id `1`) is first on PostgreSQL and last on SQL
Server.

### Fail-closed shapes

The `CASE` emulation repeats the ordering key, so on SQL Server, MySQL and MariaDB an explicit placement is
rejected with `BuildSqlCommandException` — before any statement is sent — where the statement cannot legally
carry the repeated key: a `DISTINCT`/`DISTINCT ON` query (every `ORDER BY` expression must be in the select
list) and a ClickHouse-style `LIMIT BY`. The placement is never silently dropped. The native providers
(SQLite, PostgreSQL, ClickHouse) repeat nothing and are not restricted: `DISTINCT`, `DISTINCT ON` and
`LIMIT BY` keep the native modifier. See [Limitations](../advanced/limitations.md).

### In-memory

The in-memory provider honours the same explicit placement with the same semantics and matches the SQL
result oracle cell by cell. It compares with the CLR default comparer, so strings follow the CLR comparison
(`Comparer<string>.Default`), **not** the database collation.

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
select id
 from simple_entity limit 1 offset 1
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
select id, nullableint
 from complex_entity order by id limit 2 by nullableint
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
select id, nullableint
 from complex_entity order by nullableint fetch first 3 rows with ties
```

```sql
-- SQL Server (no offset): TOP(n) WITH TIES
select top(3) with ties id, nullableint
 from complex_entity order by nullableint
```

Only PostgreSQL and SQL Server support `WITH TIES`; every other SQL provider and the in-memory context
throw `NotSupportedException`. Combining it with `DISTINCT`/`DISTINCT ON` is rejected during SQL
generation with `BuildSqlCommandException`; the `WithTies()` fluent call itself does not validate the
combination.

On SQL Server the modifier is supported only in the no-offset `TOP(n) WITH TIES` form. A positive
`OFFSET` together with `WithTies()` — `Page(5, 3).WithTies()` or `Offset(3).Limit(5).WithTies()` —
throws `NotSupportedException` while the query is prepared, before any SQL is rendered, sent or cached:
T-SQL accepts `WITH TIES` only in `TOP`, not in `OFFSET … FETCH … ONLY`. Use `WithTies()` without an
`OFFSET`, or ordinary paging without ties.

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
select id
 from simple_entity order by id limit 1
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
select id
 from simple_entity where id = 2 limit 2
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
select exists(select *
 from simple_entity where id = 100)
```

SQL Server has no boolean scalar, so it renders the same predicate as
`select cast(case when exists(...) then 1 else 0 end as bit)`. Unlike [`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})), [`Any`](xref:NextORM.Core.EntityBuilderExtensions.Any``1(NextORM.Core.EntityBuilder{``0})) does not need the
row data, so the projection is discarded and only existence is tested.

## Provider differences

| Provider | Behaviour |
|---|---|
| SQLite | `LIMIT` / `OFFSET`; offset without limit emits `limit -1 offset n`; nulls sort as the smallest value by default, explicit placement is native (`NULLS FIRST`/`NULLS LAST`). |
| SQL Server | `TOP(n)` when there is no offset; `OFFSET n ROWS` / `FETCH NEXT n ROWS ONLY` otherwise, with an injected `ORDER BY`; nulls sort as the smallest value by default, explicit placement is emulated with a `CASE` prefix. |
| PostgreSQL | `LIMIT` / `OFFSET`; nulls sort as the largest value by default, explicit placement is native (`NULLS FIRST`/`NULLS LAST`). |
| MySQL | `LIMIT` / `OFFSET`; offset without limit emits `limit 18446744073709551615 offset n`; nulls sort as the smallest value (first ascending) by default, explicit placement is emulated with a `CASE` prefix. |
| MariaDB | Same as MySQL. |
| ClickHouse | `LIMIT` / `OFFSET`; offset without limit emits `limit 18446744073709551615 offset n`; nulls sort last in **both** directions by default, explicit placement is native (`NULLS FIRST`/`NULLS LAST`). |
| In-memory | [`OrderBy`](xref:NextORM.Core.EntityBuilder`1.OrderBy(System.Int32)) / [`OrderByDescending`](xref:NextORM.Core.EntityBuilder`1.OrderByDescending(System.Int32)) run through LINQ; nulls sort first ascending (the CLR default comparer), and an explicit placement is honoured in process. |

## See also

* [Querying and projections](../engine/index.md)
* [Filtering (WHERE)](03-filtering-where.md)
* [Query reuse: cache vs Prepare](../engine/03-plan-cache-and-reuse.md)
* [Provider overview](../connecting-providers/overview.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:479`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:489`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:499`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:510`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:521`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:532`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:540`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:551`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:562`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:573`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:592`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:602`,
`tests/nextorm.integration.tests/CommonTestSuite/Queries/CommonTestSuite.LinqExtensions.cs:8`,
`tests/nextorm.integration.tests/Postgres/PostgresSpecificTests.cs:24`,
`tests/nextorm.integration.tests/SqlServer/SqlServerSpecificTests.cs:24`,
`tests/nextorm.integration.tests/SqlServer/SqlServerSpecificTests.cs:43`,
`tests/nextorm.integration.tests/Sqlite/SqliteSpecificTests.cs:44`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:133`,
`tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:142`,
`tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs:146`,
`tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs:160`,
`tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs:173`,
`tests/nextorm.postgres.tests/SqlGeneration/SqlGenerationTests.cs:130`,
`tests/nextorm.postgres.tests/SqlGeneration/SqlGenerationTests.cs:139`;
`src/nextorm.core/Query/QueryCommand.TResult.cs:147`,
`src/nextorm.core/Query/QueryCommand.TResult.cs:197`.
