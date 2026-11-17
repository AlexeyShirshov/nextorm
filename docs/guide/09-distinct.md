# SELECT DISTINCT

> Remove duplicate projected rows with [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct). `DISTINCT` compares the complete projected row, not just one column.

**Prerequisites:** [Querying and projections](../engine/index.md) · [Sorting and paging](06-sorting-and-paging.md) · [Set operations](08-set-operations.md)

## Overview

`DISTINCT` removes duplicate projected tuples: two rows are duplicates only when all projected values
match. nextorm exposes two entry points for `SELECT DISTINCT`:

* [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) — sets the flag on the builder, before [`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})):

  ```csharp
  dataContext.From<IComplexEntity>().Distinct().Select(x => new { x.Int })
  ```

* [`Distinct`](xref:NextORM.Core.QueryCommand`1.Distinct) — sets the flag on an already projected command:

  ```csharp
  dataContext.From<IComplexEntity>().Select(x => new { x.Int }).Distinct()
  ```

Both forms produce the same SQL. Choose either form based on whether you call `Distinct` before or
after `Select`.

## Basic distinct

This example projects one column and removes repeated values from that result:

```csharp
// complex_entity stores nullableint values null, 1, 1 so DISTINCT must collapse the two 1s.
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new { e.Int })
    .Distinct()
    .ToList();

var same = dataContext.From<IComplexEntity>()
    .Distinct()
    .Select(e => new { e.Int })
    .ToList();
```

```sql
select distinct nullableint as 'Int'
 from complex_entity
```

The `Output:` tables below show the rows returned by each example against the integration-test seed data (`tests/nextorm.integration.tests/Providers/TestProviders/SqliteTestProvider.cs`).

Output:

| Int |
|-----|
| null |
| 1 |

## Distinct over a join

A join can return repeated values in its projection. [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) removes those repeated projected rows:

```csharp
// complex_entity booleans are true, false, false, so the projected join has duplicate rows.
var rows = dataContext.From<ISimpleEntity>()
    .Join<IComplexEntity>((s, c) => s.Id == c.Id)
    .Select(p => new { p.Item2.Boolean })
    .Distinct()
    .ToList();
```

```sql
select distinct t2.b as 'Boolean'
 from simple_entity as 't1'
 join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
```

A cross join produces the full Cartesian product, which [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) then reduces to the distinct
values of the projected column:

```csharp
var all = dataContext.From<ISimpleEntity>()
    .CrossJoin(dataContext.From<IComplexEntity>())
    .Select(p => new { p.Item2.Id })
    .ToList(); // 30 rows

var distinct = dataContext.From<ISimpleEntity>()
    .CrossJoin(dataContext.From<IComplexEntity>())
    .Select(p => new { p.Item2.Id })
    .Distinct()
    .ToList(); // 3 rows
```

```sql
select distinct t2.id
 from simple_entity as 't1'
 cross join complex_entity as 't2'
```

## Distinct and paging

Apply paging to the distinct result: first remove duplicate projected rows, then take the requested
page. In the builder chain, call [`Limit`](xref:NextORM.Core.Paging.Limit) or
[`Page`](xref:NextORM.Core.EntityBuilder`1.Page(System.Int32,System.Int32)) before
[`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})).
The provider determines the SQL keyword order:

```csharp
var distinct = dataContext.From<IComplexEntity>()
    .Select(e => new { e.Boolean })
    .Distinct()
    .ToList(); // 2 rows

var firstPage = dataContext.From<IComplexEntity>()
    .Limit(1)
    .Select(e => new { e.Boolean })
    .Distinct()
    .ToList(); // 1 row
```

```sql
-- SQLite / PostgreSQL: the distinct result is paged
select distinct b as 'Boolean'
 from complex_entity limit 1
```

```sql
-- SQL Server: DISTINCT must precede TOP
select distinct top(1) b as 'Boolean'
 from complex_entity
```

On SQL Server, `select top(1) distinct ...` is invalid T-SQL. The dialect emits `distinct` before
`top(n)`. The SQL-generation test pins the order with
`e.Distinct().Limit(5).Select(x => x.Id)` → `select distinct top(5) id from simple_entity`.

## Distinct and set operations

[`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) applies to its own command, not to the whole
set-operation chain. It removes duplicates only from the branch where you call it:

```csharp
var cmd = dataContext.From<IComplexEntity>().Select(it => it.Int)
    .Distinct()
    .Union(dataContext.From<IComplexEntity>().Select(it => it.Int));

var count = dataContext.From(cmd).Count(); // 2
```

```sql
-- The left branch carries DISTINCT; UNION removes the remaining duplicates.
select distinct nullableint
 from complex_entity
 union 
select nullableint
 from complex_entity
```

With [`UnionAll`](xref:NextORM.Core.QueryCommand`1.UnionAll``1(NextORM.Core.QueryCommand{``0})), the
branches are concatenated without another deduplication step. The left branch is distinct, but the
right branch is not:

```csharp
var cmd = dataContext.From<IComplexEntity>().Select(it => it.Int)
    .Distinct()
    .UnionAll(dataContext.From<IComplexEntity>().Select(it => it.Int));

var count = dataContext.From(cmd).Count();
// Left branch is distinct ({null, 1}); UNION ALL keeps the right branch ({null, 1, 1}) => 5 rows.
```

```sql
select distinct nullableint
 from complex_entity
 union all 
select nullableint
 from complex_entity
```

## `DISTINCT ON` (PostgreSQL)

PostgreSQL also supports `DISTINCT ON (expr, ...)`. It keeps the first row for each key according to
`ORDER BY`, whose leading sort expressions must match that key. Use
[`DistinctOn`](xref:NextORM.Postgres.PostgresEntityBuilderExtensions.DistinctOn``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}}))
instead of `Distinct`. PostgreSQL treats the two forms as mutually exclusive, so combining them throws.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .DistinctOn(e => e.String)
    .OrderBy(e => e.String)
    .Select(e => new { e.Id, e.String })
    .ToList();
```

```sql
select distinct on (somestring) id, somestring
 from complex_entity order by somestring
```

The key may be an anonymous type to key on several columns. Only PostgreSQL implements `DISTINCT ON`;
every other provider rejects it at SQL build time.

## Provider differences

| Provider | `DISTINCT` + limit | `DISTINCT` over a join |
|---|---|---|
| SQLite | `select distinct ... limit N` | supported |
| SQL Server | `select distinct top(N) ...` (DISTINCT before TOP) | supported |
| PostgreSQL | `select distinct ... limit N` | supported |
| MySQL | `select distinct ... limit N` | supported |
| MariaDB | `select distinct ... limit N` | supported |
| ClickHouse | `select distinct ... limit N` | supported |
| In-memory | duplicates removed by the in-memory enumerator ([`IsDistinct`](xref:NextORM.Core.QueryCommand.IsDistinct) is honoured) | not covered by the in-memory test suite |

## See also

- [Set operations](08-set-operations.md) - [`Union`](xref:NextORM.Core.QueryCommand`1.Union``1(NextORM.Core.QueryCommand{``0})) already removes duplicates; [`UnionAll`](xref:NextORM.Core.QueryCommand`1.UnionAll``1(NextORM.Core.QueryCommand{``0})) does not.
- [Sorting and paging](06-sorting-and-paging.md) - [`Limit`](xref:NextORM.Core.Paging.Limit), [`Offset`](xref:NextORM.Core.Paging.Offset) and [`Page`](xref:NextORM.Core.EntityBuilder`1.Page(System.Int32,System.Int32)).
- [Grouping and aggregates](05-grouping-and-aggregates.md) - keeping the extreme row with `SelectWhereMax` / `SelectWhereMin`.
- [Querying and projections](../engine/index.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite/Queries/Rows/CommonTestSuite.Distinct.cs:9`,
`tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:26`,
`tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs:26,35`,
`tests/nextorm.postgres.tests/SqlGeneration/SqlGenerationTests.cs:25`.
