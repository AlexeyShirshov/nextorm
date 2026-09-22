# SELECT DISTINCT

> Remove duplicate rows with [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) on an entity builder or on a projected query, including over joins, paging and set operations.

**Prerequisites:** [Querying and projections](01-querying-and-projections.md) · [Sorting and paging](05-sorting-and-paging.md) · [Set operations](07-set-operations.md)

## Overview

nextorm exposes two entry points for `SELECT DISTINCT`:

* [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) — sets the flag on the builder, before [`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})):

  ```csharp
  dataContext.From<IComplexEntity>().Distinct().Select(x => new { x.Int })
  ```

* [`Distinct`](xref:NextORM.Core.QueryCommand`1.Distinct) — sets the flag on an already projected command:

  ```csharp
  dataContext.From<IComplexEntity>().Select(x => new { x.Int }).Distinct()
  ```

Both produce the same SQL. `DISTINCT` applies to the whole select list, so the duplicates that are
removed are duplicates of the projected tuple, not of a single column.

## Basic distinct

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
select distinct nullableint as 'Int' from complex_entity
```

The `Output:` tables below show the rows returned by each example against the integration-test seed data (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Output:

| Int |
|-----|
| null |
| 1 |

## Distinct over a join

A projected join can contain duplicate rows; [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) collapses them:

```csharp
// complex_entity booleans are true, false, false, so the projected join has duplicate rows.
var rows = dataContext.From<ISimpleEntity>()
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { p.Item2.Boolean })
    .Distinct()
    .ToList();
```

```sql
select distinct t2.b as 'Boolean' from simple_entity as 't1' join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
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
select distinct t2.id from simple_entity as 't1' cross join complex_entity as 't2'
```

## Distinct and paging

Paging is applied to the distinct result. The [`Limit`](xref:NextORM.Core.Paging.Limit)/[`Page`](xref:NextORM.Core.EntityBuilder`1.Page(System.Int32,System.Int32)) call is placed before [`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})), and the
double-checked shape ([`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) then [`Limit`](xref:NextORM.Core.Paging.Limit)) is governed by the provider's keyword order:

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
select distinct b as 'Boolean' from complex_entity limit 1
```

```sql
-- SQL Server: DISTINCT must precede TOP
select distinct top(1) b as 'Boolean' from complex_entity
```

On SQL Server, `select top(1) distinct ...` is invalid T-SQL, so the dialect emits `distinct` before
`top(n)` (the SQL-generation test pins the order with `e.Distinct().Limit(5).Select(x => x.Id)` →
`select distinct top(5) id from simple_entity`).

## Distinct and set operations

[`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) is a property of its own command, not of the whole chain, so it only deduplicates the
branch it is attached to:

```csharp
var cmd = dataContext.From<IComplexEntity>().Select(it => it.Int)
    .Distinct()
    .Union(dataContext.From<IComplexEntity>().Select(it => it.Int));

var count = dataContext.From(cmd).Count(); // 2
```

```sql
-- The left branch carries DISTINCT; UNION removes the remaining duplicates.
select distinct nullableint from complex_entity
 union 
select nullableint from complex_entity
```

With [`UnionAll`](xref:NextORM.Core.QueryCommand`1.UnionAll``1(NextORM.Core.QueryCommand{``0})) the branches are concatenated without an extra deduplication, so the left branch is
distinct but the right one is not:

```csharp
var cmd = dataContext.From<IComplexEntity>().Select(it => it.Int)
    .Distinct()
    .UnionAll(dataContext.From<IComplexEntity>().Select(it => it.Int));

var count = dataContext.From(cmd).Count();
// Left branch is distinct ({null, 1}); UNION ALL keeps the right branch ({null, 1, 1}) => 5 rows.
```

```sql
select distinct nullableint from complex_entity
 union all 
select nullableint from complex_entity
```

## `DISTINCT ON` (PostgreSQL)

PostgreSQL also supports `DISTINCT ON (expr, ...)`, which keeps the first row of each distinct key
according to the `ORDER BY` (the leading sort expressions must match the key). Use
[`DistinctOn`](xref:NextORM.Core.EntityBuilder`1.DistinctOn``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) instead of `Distinct`; combining the two
throws, because PostgreSQL treats them as mutually exclusive.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .DistinctOn(e => e.String)
    .OrderBy(e => e.String)
    .Select(e => new { e.Id, e.String })
    .ToList();
```

```sql
select distinct on (somestring) id, somestring from complex_entity order by somestring
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

- [Set operations](07-set-operations.md) - [`Union`](xref:NextORM.Core.QueryCommand`1.Union``1(NextORM.Core.QueryCommand{``0})) already removes duplicates; [`UnionAll`](xref:NextORM.Core.QueryCommand`1.UnionAll``1(NextORM.Core.QueryCommand{``0})) does not.
- [Sorting and paging](05-sorting-and-paging.md) - [`Limit`](xref:NextORM.Core.Paging.Limit), [`Offset`](xref:NextORM.Core.Paging.Offset) and [`Page`](xref:NextORM.Core.EntityBuilder`1.Page(System.Int32,System.Int32)).
- [Querying and projections](01-querying-and-projections.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite.Distinct.cs:9`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:26`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:26,35`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:25`.
