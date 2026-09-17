# SELECT DISTINCT

> Remove duplicate rows with `Distinct()` on an entity builder or on a projected query, including over joins, paging and set operations.

**Prerequisites:** [Querying and projections](01-querying-and-projections.md) · [Sorting and paging](05-sorting-and-paging.md) · [Set operations](07-set-operations.md)

## Overview

nextorm exposes two entry points for `SELECT DISTINCT`:

* `Entity<TEntity>.Distinct()` — sets the flag on the builder, before `Select`:

  ```csharp
  dataContext.Create<IComplexEntity>().Distinct().Select(x => new { x.Int })
  ```

* `QueryCommand<TResult>.Distinct()` — sets the flag on an already projected command:

  ```csharp
  dataContext.Create<IComplexEntity>().Select(x => new { x.Int }).Distinct()
  ```

Both produce the same SQL. `DISTINCT` applies to the whole select list, so the duplicates that are
removed are duplicates of the projected tuple, not of a single column.

## Basic distinct

```csharp
// complex_entity stores nullableint values null, 1, 1 so DISTINCT must collapse the two 1s.
var rows = dataContext.Create<IComplexEntity>()
    .Select(e => new { e.Int })
    .Distinct()
    .ToList();

var same = dataContext.Create<IComplexEntity>()
    .Distinct()
    .Select(e => new { e.Int })
    .ToList();
```

```sql
select distinct nullableint as 'Int' from complex_entity
```

## Distinct over a join

A projected join can contain duplicate rows; `Distinct` collapses them:

```csharp
// complex_entity booleans are true, false, false, so the projected join has duplicate rows.
var rows = dataContext.Create<ISimpleEntity>()
    .Join(dataContext.Create<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { p.t2.Boolean })
    .Distinct()
    .ToList();
```

```sql
select distinct t2.b as 'Boolean' from simple_entity as 't1' join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
```

A cross join produces the full Cartesian product, which `Distinct` then reduces to the distinct
values of the projected column:

```csharp
var all = dataContext.Create<ISimpleEntity>()
    .CrossJoin(dataContext.Create<IComplexEntity>())
    .Select(p => new { p.t2.Id })
    .ToList(); // 30 rows

var distinct = dataContext.Create<ISimpleEntity>()
    .CrossJoin(dataContext.Create<IComplexEntity>())
    .Select(p => new { p.t2.Id })
    .Distinct()
    .ToList(); // 3 rows
```

```sql
select distinct t2.id from simple_entity as 't1' cross join complex_entity as 't2'
```

## Distinct and paging

Paging is applied to the distinct result. The `Limit`/`Page` call is placed before `Select`, and the
double-checked shape (`Distinct` then `Limit`) is governed by the provider's keyword order:

```csharp
var distinct = dataContext.Create<IComplexEntity>()
    .Select(e => new { e.Boolean })
    .Distinct()
    .ToList(); // 2 rows

var firstPage = dataContext.Create<IComplexEntity>()
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

`Distinct` is a property of its own command, not of the whole chain, so it only deduplicates the
branch it is attached to:

```csharp
var cmd = dataContext.Create<IComplexEntity>().Select(it => it.Int)
    .Distinct()
    .Union(dataContext.Create<IComplexEntity>().Select(it => it.Int));

var count = dataContext.From(cmd).Count(); // 2
```

```sql
-- The left branch carries DISTINCT; UNION removes the remaining duplicates.
select distinct nullableint from complex_entity
 union 
select nullableint from complex_entity
```

With `UnionAll` the branches are concatenated without an extra deduplication, so the left branch is
distinct but the right one is not:

```csharp
var cmd = dataContext.Create<IComplexEntity>().Select(it => it.Int)
    .Distinct()
    .UnionAll(dataContext.Create<IComplexEntity>().Select(it => it.Int));

var count = dataContext.From(cmd).Count();
// Left branch is distinct ({null, 1}); UNION ALL keeps the right branch ({null, 1, 1}) => 5 rows.
```

```sql
select distinct nullableint from complex_entity
 union all 
select nullableint from complex_entity
```

## Provider differences

| Provider | `DISTINCT` + limit | `DISTINCT` over a join |
|---|---|---|
| SQLite | `select distinct ... limit N` | supported |
| SQL Server | `select distinct top(N) ...` (DISTINCT before TOP) | supported |
| PostgreSQL | `select distinct ... limit N` | supported |
| In-memory | duplicates removed by the in-memory enumerator (`IsDistinct` is honoured) | not covered by the in-memory test suite |

## See also

- [Set operations](07-set-operations.md) - `Union` already removes duplicates; `UnionAll` does not.
- [Sorting and paging](05-sorting-and-paging.md) - `Limit`, `Offset` and `Page`.
- [Querying and projections](01-querying-and-projections.md)

---

Source: `test/nextorm.integration.tests/CommonTestSuite.Distinct.cs:9`,
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:26`,
`test/nextorm.sqlserver.tests/SqlGenerationTests.cs:26,35`,
`test/nextorm.postgres.tests/SqlGenerationTests.cs:25`.
