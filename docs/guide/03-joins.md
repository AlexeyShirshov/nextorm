# Joins

> Combine rows from two or more entities, derived queries or raw tables with `Join`, `LeftJoin`, `RightJoin`, `FullJoin` and `CrossJoin`.

**Prerequisites:** [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Querying and projections](01-querying-and-projections.md) · [Filtering (WHERE)](02-filtering-where.md)

## Overview

Every `Entity<T>` exposes five join methods: `Join` (inner), `LeftJoin`, `RightJoin`, `FullJoin` and
`CrossJoin`. A join condition is an expression over the two sides and is emitted as the `ON` clause
of the join, exactly where the builder can translate it. `CrossJoin` takes no condition and emits
`cross join`.

The right-hand side can be:

* another typed entity, `Entity<TJoinEntity>`;
* a `QueryCommand<TJoinEntity>` — a subquery that is rendered as a derived table;
* a raw table, `Entity` created through `DataContext.From("table")`, whose columns are read through
  the `TableAlias` indexer (`t["id"]`).

Two things are important before looking at the examples:

1. **Join arity is capped at eight tables, at compile time.** The first join returns
   `EntityP2<T1, T2>`, the next `EntityP3<T1, T2, T3>`, and so on up to `EntityP8<T1..T8>`.
   `EntityP8` deliberately exposes no further `Join`/`LeftJoin`/`RightJoin`/`FullJoin`/`CrossJoin`
   methods, and `Projection<T1..T8>` does not implement `IExtendableProjection`, so a ninth join
   does not compile.
2. **The accumulated projection is addressed as `p.t1`, `p.t2`, … `p.t8`.** After the first join the
   condition receives that projection instead of the plain entity, so a chained join references the
   tables already joined through `p.tN`.

The generated SQL references each table under a positional alias: `t1` for the base table and `t2`,
`t3`, … for the joined tables, in the order they were added. Aliases are quoted per provider
(`'t1'` on SQLite, `[t1]` on SQL Server, `"t1"` on PostgreSQL).

## Inner join

```csharp
var rows = await dataContext.Create<ISimpleEntity>()
    .Join(dataContext.Create<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { p.t1.Id, p.t2.RequiredString })
    .ToListAsync();
```

```sql
select t1.id, t2.requiredstring from simple_entity as 't1' join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
```

`SimpleEntity.Id` is `int` while `ComplexEntity.Id` is `long`, so the narrower side is widened with
`cast(t1.id as bigint)`.

A `WHERE` after the join is applied to the accumulated projection and can reference either side:

```csharp
var rows = await dataContext.Create<ISimpleEntity>()
    .Join(dataContext.Create<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Where(p => p.t2.Boolean ?? false)
    .Select(p => new { p.t1.Id, p.t2.RequiredString })
    .ToListAsync();
```

A `Where` placed before the join filters the left side first; the two are equivalent for inner joins
but differ for outer joins:

```csharp
var rows = await dataContext.Create<ISimpleEntity>()
    .Where(it => it.Id > 2)
    .Join(dataContext.Create<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Where(p => p.t2.RequiredString == "34mfs")
    .Select(p => new { p.t1.Id, p.t2.RequiredString })
    .ToListAsync();
```

## Outer joins

`LeftJoin` keeps every row of the left side and fills the right side with `NULL` when there is no
match; `RightJoin` and `FullJoin` behave symmetrically.

```csharp
var rows = dataContext.Create<ISimpleEntity>()
    .LeftJoin(dataContext.Create<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { LeftId = p.t1.Id, RightString = p.t2.RequiredString })
    .ToList();
```

```sql
select t1.id as 'LeftId', t2.requiredstring as 'RightString' from simple_entity as 't1' left join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
```

`RightJoin` and `FullJoin` are emitted with the same shape:

```csharp
var right = dataContext.Create<IComplexEntity>()
    .RightJoin(dataContext.Create<ISimpleEntity>(), (c, s) => c.Id == s.Id)
    .Select(p => new { LeftString = p.t1.RequiredString, RightId = p.t2.Id })
    .ToList();

var full = dataContext.Create<ISimpleEntity>()
    .FullJoin(dataContext.Create<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { LeftId = p.t1.Id, RightString = p.t2.RequiredString })
    .ToList();
```

`RightJoin` and `FullJoin` are rejected with `NotSupportedException` only when a dialect reports
`SupportsRightFullJoin == false`; all providers nextorm ships declare support.

## Cross join

`CrossJoin` takes no condition and produces the Cartesian product:

```csharp
var count = dataContext.Create<ISimpleEntity>().CrossJoin(dataContext.Create<IComplexEntity>()).Count();
```

```sql
select count(*) from simple_entity as 't1' cross join complex_entity as 't2'
```

## Joining a subquery

A `QueryCommand<T>` can be joined directly. It is wrapped in parentheses and aliased as a derived
table (the alias is optional on SQLite and required on SQL Server and PostgreSQL):

```csharp
var subQuery = dataContext.Create<IComplexEntity>()
    .Where(it => it.Id == 3)
    .Select(it => new { it.Id, it.RequiredString, it.Boolean });

var rows = await dataContext.Create<ISimpleEntity>()
    .Join(subQuery, (s, c) => s.Id == c.Id)
    .Select(p => new { p.t1.Id, p.t2.RequiredString })
    .ToListAsync();
```

## Joining a raw table

`From("table")` starts a non-generic `Entity` whose columns are accessed through `TableAlias`. The
left and right sides can be mixed freely with typed entities:

```csharp
var rows = dataContext
    .From("simple_entity")
    .Join(dataContext.From("complex_entity"), (s, c) => s["id"] == c["id"])
    .Select(p => new { Id = p.t1["id"].AsInt, Str = p.t2["someString"].AsString })
    .ToList();

var mixed = dataContext
    .From("simple_entity")
    .Join(dataContext.Create<IComplexEntity>(), (s, c) => s["id"].AsInt == c.Id)
    .Select(p => new { Id = p.t1["id"].AsInt, Str = p.t2.String })
    .ToList();
```

## Chained joins and arity 2..8

Each chained call adds one table. The condition receives the projection accumulated so far and the
new entity:

```csharp
var rows = dataContext.Create<ISimpleEntity>()
    .Join(dataContext.Create<IComplexEntity>(), (s, c) => s.Id == c.Id)          // EntityP2<SimpleEntity, ComplexEntity>
    .Join(dataContext.Create<ISimpleEntity>(), (p, s) => p.t2.Id == s.Id)        // EntityP3<...>
    .Join(dataContext.Create<IComplexEntity>(), (p, c) => p.t3.Id == c.Id)       // EntityP4<...>
    .Select(p => new { A = p.t1.Id, B = p.t2.RequiredString, C = p.t3.Id, D = p.t4.RequiredString })
    .ToList();
```

```sql
select t1.id as 'A', t2.requiredstring as 'B', t3.id as 'C', t4.requiredstring as 'D' from simple_entity as 't1' join complex_entity as 't2' on cast(t1.id as bigint) = t2.id join simple_entity as 't3' on t2.id = cast(t3.id as bigint) join complex_entity as 't4' on cast(t3.id as bigint) = t4.id
```

The same pattern reaches `EntityP5` through `EntityP8`. At eight tables the projection exposes
`t1`..`t8`:

```csharp
var e = new[]
{
    dataContext.Create<ISimpleEntity>(), dataContext.Create<ISimpleEntity>(),
    dataContext.Create<ISimpleEntity>(), dataContext.Create<ISimpleEntity>(),
    dataContext.Create<ISimpleEntity>(), dataContext.Create<ISimpleEntity>(),
    dataContext.Create<ISimpleEntity>(), dataContext.Create<ISimpleEntity>(),
};

var sql = e[0]
    .Join(e[1], (a, b) => a.Id == b.Id)
    .Join(e[2], (p, c) => p.t2.Id == c.Id)
    .Join(e[3], (p, c) => p.t3.Id == c.Id)
    .Join(e[4], (p, c) => p.t4.Id == c.Id)
    .Join(e[5], (p, c) => p.t5.Id == c.Id)
    .Join(e[6], (p, c) => p.t6.Id == c.Id)
    .Join(e[7], (p, c) => p.t7.Id == c.Id)
    .Select(p => new { A = p.t1.Id, B = p.t2.Id, C = p.t3.Id, D = p.t4.Id,
                       E = p.t5.Id, F = p.t6.Id, G = p.t7.Id, H = p.t8.Id });
```

## Captured parameters in a join

A captured local in the join condition becomes a parameter and is re-extracted on every execution,
including on an implicit plan-cache hit:

```csharp
for (var i = 1; i <= 3; i++)
{
    var id = i;
    var rows = dataContext.Create<ISimpleEntity>()
        .Join(dataContext.Create<IComplexEntity>(), (s, c) => s.Id == c.Id)
        .Where(p => p.t2.Id == id)
        .Select(p => new { p.t1.Id, p.t2.RequiredString })
        .ToList();
}
```

## Provider differences

| Provider | Join aliases | Derived-table alias | Outer joins |
|---|---|---|---|
| SQLite | `as 't1'` | optional | left/right/full supported |
| SQL Server | `as [t1]` | required | left/right/full supported |
| PostgreSQL | `as "t1"` | required | left/right/full supported |
| In-memory | not applicable (delegate execution) | not applicable | inner/left/right/full/cross supported; table-valued function sources are not |

The in-memory provider compiles the join condition to a delegate and loops, so it does not emit SQL;
it supports `Inner`, `Left`, `Right`, `Full` and `Cross` joins (see
`test/nextorm.core.tests/InMemoryJoinTests.cs`). Joins through `EntityP2..P8` are resolved at query
build time on every provider.

## See also

- [Subqueries](06-subqueries.md) - a joined `QueryCommand<T>` is a derived table.
- [Grouping and aggregates](04-grouping-and-aggregates.md) - aggregate over a join.
- [Querying and projections](01-querying-and-projections.md)

---

Source: `test/nextorm.integration.tests/CommonTestSuite.Join.cs:9`,
`test/nextorm.core.tests/InMemoryJoinTests.cs:14`,
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:268` (and the other provider `SqlGenerationTests.cs`).
