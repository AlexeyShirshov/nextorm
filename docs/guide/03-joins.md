# Joins

> Combine rows from two or more entities, derived queries or raw tables with [`Join`](xref:NextORM.Core.EntityBuilder`1), [`LeftJoin`](xref:NextORM.Core.EntityBuilder`1), [`RightJoin`](xref:NextORM.Core.EntityBuilder`1), [`FullJoin`](xref:NextORM.Core.EntityBuilder`1), [`CrossJoin`](xref:NextORM.Core.EntityBuilder`1), [`CrossApply`](xref:NextORM.Core.EntityBuilder`1) and [`OuterApply`](xref:NextORM.Core.EntityBuilder`1).

**Prerequisites:** [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Querying and projections](01-querying-and-projections.md) · [Filtering (WHERE)](02-filtering-where.md)

## Overview

Every `EntityBuilder<T>` exposes seven join methods: [`Join`](xref:NextORM.Core.EntityBuilder`1) (inner), [`LeftJoin`](xref:NextORM.Core.EntityBuilder`1), [`RightJoin`](xref:NextORM.Core.EntityBuilder`1), [`FullJoin`](xref:NextORM.Core.EntityBuilder`1),
[`CrossJoin`](xref:NextORM.Core.EntityBuilder`1), [`CrossApply`](xref:NextORM.Core.EntityBuilder`1) and [`OuterApply`](xref:NextORM.Core.EntityBuilder`1). A join condition is an expression over the two sides and is
emitted as the `ON` clause of the join, exactly where the builder can translate it. [`CrossJoin`](xref:NextORM.Core.EntityBuilder`1),
[`CrossApply`](xref:NextORM.Core.EntityBuilder`1) and [`OuterApply`](xref:NextORM.Core.EntityBuilder`1) take no condition; the first emits `cross join`, the latter two emit the
provider's lateral/apply form (see [APPLY and LATERAL](#apply-and-lateral)).

The right-hand side can be:

* another typed entity, `EntityBuilder<TJoinEntity>`;
* a `QueryCommand<TJoinEntity>` — a subquery that is rendered as a derived table;
* a raw table, [`EntityBuilder`](xref:NextORM.Core.EntityBuilder) created through [`From`](xref:NextORM.Core.DataContext), whose columns are read through
  the [`TableAlias`](xref:NextORM.Core.TableAlias) indexer (`t["id"]`).

Two things are important before looking at the examples:

1. **Join arity is capped at eight tables, at compile time.** The first join returns
   [`JoinedEntityBuilder<T1, T2>`](xref:NextORM.Core.JoinedEntityBuilder`2), the next [`JoinedEntityBuilder<T1, T2, T3>`](xref:NextORM.Core.JoinedEntityBuilder`3), and so on up to `JoinedEntityBuilder<T1..T8>`.
   `JoinedEntityBuilder` deliberately exposes no further [`Join`](xref:NextORM.Core.EntityBuilder`1)/[`LeftJoin`](xref:NextORM.Core.EntityBuilder`1)/[`RightJoin`](xref:NextORM.Core.EntityBuilder`1)/[`FullJoin`](xref:NextORM.Core.EntityBuilder`1)/[`CrossJoin`](xref:NextORM.Core.EntityBuilder`1)
   methods, and `Projection<T1..T8>` does not implement [`IExtendableProjection`](xref:NextORM.Core.IExtendableProjection), so a ninth join
   does not compile.
2. **The accumulated projection is addressed as `p.Item1`, `p.Item2`, … `p.Item8`.** After the first join the
   condition receives that projection instead of the plain entity, so a chained join references the
   tables already joined through `p.tN`.

The generated SQL references each table under a positional alias: `t1` for the base table and `t2`,
`t3`, … for the joined tables, in the order they were added. Aliases are quoted per provider
(`'t1'` on SQLite, `[t1]` on SQL Server, `"t1"` on PostgreSQL).

## Inner join

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToListAsync();
```

```sql
select t1.id, t2.requiredstring from simple_entity as 't1' join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
```

The `Output:` tables below show the rows returned by each example against the integration-test seed data (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Output:

| Id | RequiredString |
|----|----------------|
| 1 | sdf |
| 2 | asdfgoi |
| 3 | 34mfs |

`SimpleEntity.Id` is `int` while `ComplexEntity.Id` is `long`, so the narrower side is widened with
`cast(t1.id as bigint)`.

A `WHERE` after the join is applied to the accumulated projection and can reference either side:

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Where(p => p.Item2.Boolean ?? false)
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToListAsync();
```

A [`Where`](xref:NextORM.Core.EntityBuilder`1) placed before the join filters the left side first; the two are equivalent for inner joins
but differ for outer joins:

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Where(it => it.Id > 2)
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Where(p => p.Item2.RequiredString == "34mfs")
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToListAsync();
```

## Outer joins

[`LeftJoin`](xref:NextORM.Core.EntityBuilder`1) keeps every row of the left side and fills the right side with `NULL` when there is no
match; [`RightJoin`](xref:NextORM.Core.EntityBuilder`1) and [`FullJoin`](xref:NextORM.Core.EntityBuilder`1) behave symmetrically.

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .LeftJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { LeftId = p.Item1.Id, RightString = p.Item2.RequiredString })
    .ToList();
```

```sql
select t1.id as 'LeftId', t2.requiredstring as 'RightString' from simple_entity as 't1' left join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
```

[`RightJoin`](xref:NextORM.Core.EntityBuilder`1) and [`FullJoin`](xref:NextORM.Core.EntityBuilder`1) are emitted with the same shape:

```csharp
var right = dataContext.From<IComplexEntity>()
    .RightJoin(dataContext.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
    .Select(p => new { LeftString = p.Item1.RequiredString, RightId = p.Item2.Id })
    .ToList();

var full = dataContext.From<ISimpleEntity>()
    .FullJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { LeftId = p.Item1.Id, RightString = p.Item2.RequiredString })
    .ToList();
```

[`RightJoin`](xref:NextORM.Core.EntityBuilder`1) and [`FullJoin`](xref:NextORM.Core.EntityBuilder`1) are rejected with `NotSupportedException` only when a dialect reports
`SupportsRightFullJoin == false`; all providers nextorm ships declare support.

## Cross join

[`CrossJoin`](xref:NextORM.Core.EntityBuilder`1) takes no condition and produces the Cartesian product:

```csharp
var count = dataContext.From<ISimpleEntity>().CrossJoin(dataContext.From<IComplexEntity>()).Count();
```

```sql
select count(*) from simple_entity as 't1' cross join complex_entity as 't2'
```

Output:

| Count |
|-------|
| 30 |

## APPLY and LATERAL

[`CrossApply`](xref:NextORM.Core.EntityBuilder`1) and [`OuterApply`](xref:NextORM.Core.EntityBuilder`1) render the provider's lateral-source form. The right-hand side is the same
set of sources that a regular join accepts — a typed entity, a `QueryCommand<T>` derived table, a raw
table or a table-valued function — but there is no `ON` condition:

* [`CrossApply`](xref:NextORM.Core.EntityBuilder`1) keeps only the left-hand rows for which the applied source returns at least one row
  (SQL Server `CROSS APPLY`, PostgreSQL/MySQL/MariaDB `CROSS JOIN LATERAL`);
* [`OuterApply`](xref:NextORM.Core.EntityBuilder`1) also keeps left-hand rows whose applied source is empty, filling the right side with
  `NULL` (SQL Server `OUTER APPLY`, PostgreSQL/MySQL/MariaDB `LEFT JOIN LATERAL ... ON true`).

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .CrossApply(dataContext.From<IComplexEntity>())
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToListAsync();
```

```sql
-- SQL Server
select t1.id, t2.somestring from simple_entity as [t1] cross apply complex_entity as [t2]
-- PostgreSQL / MySQL / MariaDB
select t1.id, t2.somestring from simple_entity as t1 cross join lateral complex_entity as t2
```

An `OUTER APPLY` over a derived table:

```csharp
var subQuery = dataContext.From<IComplexEntity>()
    .Where(c => c.Id > 1)
    .Select(c => new { c.Id, c.RequiredString });

var rows = dataContext.From<ISimpleEntity>()
    .OuterApply(subQuery)
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToList();
```

```sql
-- SQL Server
... from simple_entity as [t1] outer apply (select id, somestring from complex_entity where ...) as [t2]
-- PostgreSQL / MySQL / MariaDB
... from simple_entity as t1 left join lateral (select ...) as t2 on true
```

> **Correlation is not expressible yet.** The applied source cannot reference columns of the left-hand
> row, because there is no public API to author an outer-row reference inside a `FROM` subquery. Until
> that lands, [`CrossApply`](xref:NextORM.Core.EntityBuilder`1)/[`OuterApply`](xref:NextORM.Core.EntityBuilder`1) are equivalent to a `CROSS JOIN`/`LEFT JOIN` over a non-correlated
> source and are rejected by dialects without a lateral source (`SupportsApply == false`).

## Joining a subquery

A `QueryCommand<T>` can be joined directly. It is wrapped in parentheses and aliased as a derived
table (the alias is optional on SQLite and required on SQL Server and PostgreSQL):

```csharp
var subQuery = dataContext.From<IComplexEntity>()
    .Where(it => it.Id == 3)
    .Select(it => new { it.Id, it.RequiredString, it.Boolean });

var rows = await dataContext.From<ISimpleEntity>()
    .Join(subQuery, (s, c) => s.Id == c.Id)
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToListAsync();
```

## Joining a raw table

`From("table")` starts a non-generic [`EntityBuilder`](xref:NextORM.Core.EntityBuilder) whose columns are accessed through [`TableAlias`](xref:NextORM.Core.TableAlias). The
left and right sides can be mixed freely with typed entities:

```csharp
var rows = dataContext
    .From("simple_entity")
    .Join(dataContext.From("complex_entity"), (s, c) => s["id"] == c["id"])
    .Select(p => new { Id = p.Item1["id"].AsInt, Str = p.Item2["someString"].AsString })
    .ToList();

var mixed = dataContext
    .From("simple_entity")
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s["id"].AsInt == c.Id)
    .Select(p => new { Id = p.Item1["id"].AsInt, Str = p.Item2.String })
    .ToList();
```

## Chained joins and arity 2..8

Each chained call adds one table. The condition receives the projection accumulated so far and the
new entity:

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)          // JoinedEntityBuilder<SimpleEntity, ComplexEntity>
    .Join(dataContext.From<ISimpleEntity>(), (p, s) => p.Item2.Id == s.Id)        // JoinedEntityBuilder<...>
    .Join(dataContext.From<IComplexEntity>(), (p, c) => p.Item3.Id == c.Id)       // JoinedEntityBuilder<...>
    .Select(p => new { A = p.Item1.Id, B = p.Item2.RequiredString, C = p.Item3.Id, D = p.Item4.RequiredString })
    .ToList();
```

```sql
select t1.id as 'A', t2.requiredstring as 'B', t3.id as 'C', t4.requiredstring as 'D' from simple_entity as 't1' join complex_entity as 't2' on cast(t1.id as bigint) = t2.id join simple_entity as 't3' on t2.id = cast(t3.id as bigint) join complex_entity as 't4' on cast(t3.id as bigint) = t4.id
```

The same pattern extends the arity up to `JoinedEntityBuilder<T1..T8>` (eight tables). At eight tables
the projection exposes `Item1`..[`Item8`](xref:NextORM.Core.Projection`8.Item8):

```csharp
var e = new[]
{
    dataContext.From<ISimpleEntity>(), dataContext.From<ISimpleEntity>(),
    dataContext.From<ISimpleEntity>(), dataContext.From<ISimpleEntity>(),
    dataContext.From<ISimpleEntity>(), dataContext.From<ISimpleEntity>(),
    dataContext.From<ISimpleEntity>(), dataContext.From<ISimpleEntity>(),
};

var sql = e[0]
    .Join(e[1], (a, b) => a.Id == b.Id)
    .Join(e[2], (p, c) => p.Item2.Id == c.Id)
    .Join(e[3], (p, c) => p.Item3.Id == c.Id)
    .Join(e[4], (p, c) => p.Item4.Id == c.Id)
    .Join(e[5], (p, c) => p.Item5.Id == c.Id)
    .Join(e[6], (p, c) => p.Item6.Id == c.Id)
    .Join(e[7], (p, c) => p.Item7.Id == c.Id)
    .Select(p => new { A = p.Item1.Id, B = p.Item2.Id, C = p.Item3.Id, D = p.Item4.Id,
                       E = p.Item5.Id, F = p.Item6.Id, G = p.Item7.Id, H = p.Item8.Id });
```

## Captured parameters in a join

A captured local in the join condition becomes a parameter and is re-extracted on every execution,
including on an implicit plan-cache hit:

```csharp
for (var i = 1; i <= 3; i++)
{
    var id = i;
    var rows = dataContext.From<ISimpleEntity>()
        .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
        .Where(p => p.Item2.Id == id)
        .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
        .ToList();
}
```

## Provider-specific join modifiers (ClickHouse)

ClickHouse adds two join modifiers that the other dialects do not have: a **strictness** modifier
(`ANY`/`ALL`/`ASOF`) and the distributed `GLOBAL` prefix. Both are applied to the join that was just
added, with [`WithStrictness`](xref:NextORM.Core.EntityBuilder`1) and [`Global`](xref:NextORM.Core.EntityBuilder`1):

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .LeftJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .WithStrictness(JoinStrictness.Any)
    .Select(p => new { p.Item1.Id, p.Item2.String })
    .ToList();
```

```sql
select t1.id, t2.somestring from simple_entity as `t1` left any join complex_entity as `t2` on cast(t1.id as bigint) = t2.id
```

[`JoinStrictness.Any`](xref:NextORM.Core.JoinStrictness) renders `<type> any join` and keeps a single
right-hand row per left-hand row; [`JoinStrictness.All`](xref:NextORM.Core.JoinStrictness) keeps every
match; [`JoinStrictness.Asof`](xref:NextORM.Core.JoinStrictness) renders `asof join`, which requires one
equi-join column plus a final inequality. [`Global`](xref:NextORM.Core.EntityBuilder`1) renders the
`GLOBAL` prefix used by distributed queries and composes with the strictness modifier in either order
(`global left any join`):

```csharp
var global = dataContext.From<ISimpleEntity>()
    .LeftJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Global()
    .WithStrictness(JoinStrictness.Any);
```

```sql
... from simple_entity as `t1` global left any join complex_entity as `t2` on ...
```

Both methods copy the builder and replace only its last join, so the source builder and earlier chains
are not mutated; a modifier set before a later join stays on the join it was applied to. They throw
`InvalidOperationException` when no join precedes them, and a modifier is only accepted on
`INNER`/`LEFT`/`RIGHT`/`FULL` joins (`CROSS`/`APPLY` throw `NotSupportedException`). The modifiers are
ClickHouse-only ([`SupportsJoinStrictness`](xref:NextORM.Core.ISqlDialect.SupportsJoinStrictness),
[`SupportsGlobalJoin`](xref:NextORM.Core.ISqlDialect.SupportsGlobalJoin)); every other provider and the
in-memory context reject them with `NotSupportedException`. `SEMI`/`ANTI`/`PASTE` joins are not
supported. See [Provider-specific SQL](provider-specific/overview.md) for the full catalogue.

## Provider differences

| Provider | Join aliases | Derived-table alias | Outer joins | APPLY / LATERAL |
|---|---|---|---|---|
| SQLite | `as 't1'` | optional | left/right/full supported | not supported (`NotSupportedException`) |
| SQL Server | `as [t1]` | required | left/right/full supported | `CROSS APPLY` / `OUTER APPLY` |
| PostgreSQL | `as "t1"` | required | left/right/full supported | `CROSS JOIN LATERAL` / `LEFT JOIN LATERAL ... ON true` |
| MySQL / MariaDB | `as \`t1\`` | required | left/right supported (no full) | `CROSS JOIN LATERAL` / `LEFT JOIN LATERAL ... ON true` |
| ClickHouse | `as \`t1\`` | required | left/right/full supported | not supported (`NotSupportedException`) |
| In-memory | not applicable (delegate execution) | not applicable | inner/left/right/full/cross supported; APPLY and table-valued function sources are not | not supported |

The in-memory provider compiles the join condition to a delegate and loops, so it does not emit SQL;
it supports [`Inner`](xref:NextORM.Core.JoinType.Inner), [`Left`](xref:NextORM.Core.JoinType.Left), [`Right`](xref:NextORM.Core.JoinType.Right), [`Full`](xref:NextORM.Core.JoinType.Full) and [`Cross`](xref:NextORM.Core.JoinType.Cross) joins (see
`tests/nextorm.core.tests/InMemoryJoinTests.cs`). [`CrossApply`](xref:NextORM.Core.EntityBuilder`1)/[`OuterApply`](xref:NextORM.Core.EntityBuilder`1) are SQL-only and throw
`NotSupportedException` on the in-memory provider, like the other unsupported join types. Joins through
`JoinedEntityBuilder<T1..T8>` are resolved at query build time on every provider.

## See also

- [Subqueries](06-subqueries.md) - a joined `QueryCommand<T>` is a derived table.
- [Grouping and aggregates](04-grouping-and-aggregates.md) - aggregate over a join.
- [Query hints](17-query-hints.md) - statement-level hints such as SQL Server `OPTION (RECOMPILE)`.
- [Provider-specific SQL](provider-specific/overview.md) - the full catalogue of provider-only constructs.
- [Querying and projections](01-querying-and-projections.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite.Join.cs:9`,
`tests/nextorm.core.tests/InMemoryJoinTests.cs:14`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:320`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:379`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:312`
(and the other provider `SqlGenerationTests.cs`).
