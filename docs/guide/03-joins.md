# Joins

> Combine rows from two or more entities, derived queries or raw tables with [`Join`](xref:NextORM.Core.EntityBuilder`1.Join``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}})), [`LeftJoin`](xref:NextORM.Core.EntityBuilder`1.LeftJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}})), [`RightJoin`](xref:NextORM.Core.EntityBuilder`1.RightJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}})), [`FullJoin`](xref:NextORM.Core.EntityBuilder`1.FullJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}})), [`CrossJoin`](xref:NextORM.Core.EntityBuilder`1.CrossJoin``1(NextORM.Core.EntityBuilder{``0})), [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0})) and [`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0})).

**Prerequisites:** [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Querying and projections](01-querying-and-projections.md) · [Filtering (WHERE)](02-filtering-where.md)

## Overview

Every `EntityBuilder<T>` exposes seven join methods: [`Join`](xref:NextORM.Core.EntityBuilder`1.Join``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}})) (inner), [`LeftJoin`](xref:NextORM.Core.EntityBuilder`1.LeftJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}})), [`RightJoin`](xref:NextORM.Core.EntityBuilder`1.RightJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}})), [`FullJoin`](xref:NextORM.Core.EntityBuilder`1.FullJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}})),
[`CrossJoin`](xref:NextORM.Core.EntityBuilder`1.CrossJoin``1(NextORM.Core.EntityBuilder{``0})), [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0})) and [`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0})). A join condition is an expression over the two sides and is
emitted as the `ON` clause of the join, exactly where the builder can translate it. [`CrossJoin`](xref:NextORM.Core.EntityBuilder`1.CrossJoin``1(NextORM.Core.EntityBuilder{``0})),
[`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0})) and [`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0})) take no condition; the first emits `cross join`, the latter two emit the
provider's lateral/apply form (see [APPLY and LATERAL](#apply-and-lateral)).

The right-hand side can be:

* another typed entity, `EntityBuilder<TJoinEntity>`;
* a `QueryCommand<TJoinEntity>` — a subquery that is rendered as a derived table;
* a raw table, [`EntityBuilder`](xref:NextORM.Core.EntityBuilder) created through [`From`](xref:NextORM.Core.DataContext.From(System.String)), whose columns are read through
  the [`TableAlias`](xref:NextORM.Core.TableAlias) indexer (`t["id"]`).

Two things are important before looking at the examples:

1. **Join arity is capped at eight tables, at compile time.** The first join returns
   [`JoinedEntityBuilder<T1, T2>`](xref:NextORM.Core.JoinedEntityBuilder`2), the next [`JoinedEntityBuilder<T1, T2, T3>`](xref:NextORM.Core.JoinedEntityBuilder`3), and so on up to `JoinedEntityBuilder<T1..T8>`.
   `JoinedEntityBuilder` deliberately exposes no further [`Join`](xref:NextORM.Core.EntityBuilder`1.Join``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}}))/[`LeftJoin`](xref:NextORM.Core.EntityBuilder`1.LeftJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}}))/[`RightJoin`](xref:NextORM.Core.EntityBuilder`1.RightJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}}))/[`FullJoin`](xref:NextORM.Core.EntityBuilder`1.FullJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}}))/[`CrossJoin`](xref:NextORM.Core.EntityBuilder`1.CrossJoin``1(NextORM.Core.EntityBuilder{``0}))
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

A [`Where`](xref:NextORM.Core.EntityBuilder`1.Where(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}})) placed before the join filters the left side first; the two are equivalent for inner joins
but differ for outer joins:

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Where(it => it.Id > 2)
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Where(p => p.Item2.RequiredString == "34mfs")
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToListAsync();
```

### Addressing the projection

The accumulated projection is positional and tuple-style: `Item1` is the base (left-hand) source, `Item2`
the first joined source and `ItemN` the *N*-th source added, in join order. The names are not semantic —
nothing in `Item1`/`Item2` says which side is the "order" and which the "customer" — so give them meaning
by projecting into a named type right after the join and threading that type through the rest of the query:

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { Order = p.Item1.Id, Customer = p.Item2.RequiredString })
    .ToListAsync();
```

```sql
select t1.id as 'Order', t2.requiredstring as 'Customer' from simple_entity as 't1' join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
```

A projection member must be a column of one of the joined entities (`p.Item1.Id`); referring to an entity
as a whole (`Order = p.Item1`) is not a column and is rejected. When you need several columns from the same
side, list each of them explicitly.

## Outer joins

[`LeftJoin`](xref:NextORM.Core.EntityBuilder`1.LeftJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}})) keeps every row of the left side and fills the right side with `NULL` when there is no
match; [`RightJoin`](xref:NextORM.Core.EntityBuilder`1.RightJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}})) and [`FullJoin`](xref:NextORM.Core.EntityBuilder`1.FullJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}})) behave symmetrically.

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .LeftJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { LeftId = p.Item1.Id, RightString = p.Item2.RequiredString })
    .ToList();
```

```sql
select t1.id as 'LeftId', t2.requiredstring as 'RightString' from simple_entity as 't1' left join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
```

[`RightJoin`](xref:NextORM.Core.EntityBuilder`1.RightJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}})) and [`FullJoin`](xref:NextORM.Core.EntityBuilder`1.FullJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}})) are emitted with the same shape:

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

[`RightJoin`](xref:NextORM.Core.EntityBuilder`1.RightJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}})) and [`FullJoin`](xref:NextORM.Core.EntityBuilder`1.FullJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}})) are rejected with `NotSupportedException` only when a dialect reports
`SupportsRightFullJoin == false`; all providers nextorm ships declare support.

## Cross join

[`CrossJoin`](xref:NextORM.Core.EntityBuilder`1.CrossJoin``1(NextORM.Core.EntityBuilder{``0})) takes no condition and produces the Cartesian product:

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

[`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0})) and [`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0})) render the provider's lateral-source form. The right-hand side is the same
set of sources that a regular join accepts — a typed entity, a `QueryCommand<T>` derived table, a raw
table or a table-valued function — but there is no `ON` condition:

* [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0})) keeps only the left-hand rows for which the applied source returns at least one row
  (SQL Server `CROSS APPLY`, PostgreSQL/MySQL/MariaDB `CROSS JOIN LATERAL`);
* [`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0})) also keeps left-hand rows whose applied source is empty, filling the right side with
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

### Correlated APPLY / LATERAL

The applied source can reference columns of the left-hand row by building it inside a lambda that
receives that row. [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0}))/[`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0})) accept such a lambda in two forms: one returning a
`QueryCommand<T>` (a projected derived query) and one returning an `EntityBuilder<T>` (the whole
entity).

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .CrossApply(s => dataContext.From<IComplexEntity>()
        .Where(c => c.Id == s.Id)
        .Select(c => new { c.Id, c.RequiredString }))
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToListAsync();
```

```sql
-- SQL Server
... from simple_entity as [t1] cross apply (select ... from complex_entity as [t2] where t2.id = t1.id) as [t3]
-- PostgreSQL / MySQL / MariaDB
... from simple_entity as t1 cross join lateral (select ...) as t3
```

The lambda parameter behaves like the outer parameter of a correlated scalar subquery: a column of the
left-hand row (here `s.Id`) becomes an outer reference resolved to the left-hand table alias.
[`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0})) also keeps left-hand rows whose applied source is empty, projecting `NULL`s.

Correlation needs a lateral source: dialects with `SupportsApply == false` (SQLite, ClickHouse) reject a
correlated apply with `NotSupportedException`, as does the in-memory provider. A correlated apply cannot
reference a join projection; apply it to a single-entity source instead.

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

## Joining a derived query as the primary source

A `QueryCommand<T>` can also be the **primary** `FROM` source, with the joined table written second:

```csharp
var derived = dataContext.From<IComplexEntity>()
    .Where(c => c.Id > 0)
    .Select(c => new { c.Id, c.RequiredString });

var rows = await dataContext.From(derived)
    .Join(dataContext.From<ISimpleEntity>(), (d, s) => d.Id == s.Id)
    .Select(p => new { p.Item1.Id, SId = p.Item2.Id })
    .ToListAsync();
```

```sql
select t1.id, t2.id as 'SId' from (select id, requiredstring from complex_entity where (id > 0)) as 't1' join simple_entity as 't2' on cast(t1.id as bigint) = t2.id
```

A `Where` may be written before the join; it is applied to the derived table (`d => d.Id > 5` becomes a
filter on the derived source, `where t1.id > 5`). Any other modifier (`OrderBy`, `GroupBy`, `Distinct`,
paging, ...) must be applied **inside** the derived query, because moving it past the join would change
the query's meaning — the builder throws `NotSupportedException` instead of silently relocating it. The
join works on every SQL provider; the in-memory provider rejects it.

The derived query may itself contain joins: its `Select` projection becomes the derived table's columns,
and those members can be referenced by the follow-up `Where`/`Join`. Every source is aliased
positionally (`t1`, `t2`, ...), so the outer join never reuses an alias from inside the derived query:

```csharp
var derived = dataContext.From<ISimpleEntity>()
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { OrderId = p.Item1.Id, CustomerName = p.Item2.RequiredString });

var rows = await dataContext.From(derived)
    .Where(d => d.CustomerName != null)
    .Join(dataContext.From<IComplexEntity>(), (d, c2) => d.OrderId == c2.Id)
    .Select(p => new { p.Item1.OrderId, p.Item1.CustomerName, Third = p.Item2.RequiredString })
    .ToListAsync();
```

```sql
select t3.OrderId, t3.CustomerName, t4.requiredstring as 'Third' from (select t1.id as 'OrderId', t2.requiredstring as 'CustomerName' from simple_entity as 't1' join complex_entity as 't2' on cast(t1.id as bigint) = t2.id) as 't3' join complex_entity as 't4' on cast(t3.OrderId as bigint) = t4.id where t3.CustomerName is not null
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
added, with [`WithStrictness`](xref:NextORM.Core.EntityBuilder`1.WithStrictness(NextORM.Core.JoinStrictness)) and [`Global`](xref:NextORM.Core.EntityBuilder`1.Global):

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

[`JoinStrictness.Any`](xref:NextORM.Core.JoinStrictness.Any) renders `<type> any join` and keeps a single
right-hand row per left-hand row; [`JoinStrictness.All`](xref:NextORM.Core.JoinStrictness.All) keeps every
match; [`JoinStrictness.Asof`](xref:NextORM.Core.JoinStrictness.Asof) renders `asof join`, which requires one
equi-join column plus a final inequality. [`Global`](xref:NextORM.Core.EntityBuilder`1.Global) renders the
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
in-memory context reject them with `NotSupportedException`.

### SEMI / ANTI / PASTE joins

`SEMI`, `ANTI` and `PASTE` are join *kinds* rather than modifiers: they change which columns and rows
the join contributes, so they get dedicated builders instead of `WithStrictness`:

```csharp
var ids = dataContext.From<ISimpleEntity>()
    .SemiJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(s => s.Id)
    .ToList();
```

```sql
select t1.id from simple_entity as `t1` left semi join complex_entity as `t2` on cast(t1.id as bigint) = t2.id
```

- [`SemiJoin`](xref:NextORM.Core.EntityBuilder`1.SemiJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}})) keeps only the left-hand columns, once per left row that
  has at least one matching right row;
- [`AntiJoin`](xref:NextORM.Core.EntityBuilder`1.AntiJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}})) keeps only the left-hand columns for left rows with no
  match (the complement of `SemiJoin`);
- [`PasteJoin`](xref:NextORM.Core.EntityBuilder`1.PasteJoin``1(NextORM.Core.EntityBuilder{``0})) pairs the two sources by row position with no `ON`; the
  projection exposes both sides and the result has as many rows as the shorter side.

`SemiJoin`/`AntiJoin` return the same projection shape (the right columns are not accessible), whereas
`PasteJoin` extends it by one item. They are ClickHouse-only
([`SupportsSemiAntiJoin`](xref:NextORM.Core.ISqlDialect.SupportsSemiAntiJoin)/[`SupportsPasteJoin`](xref:NextORM.Core.ISqlDialect.SupportsPasteJoin));
every other provider and the in-memory context reject them with `NotSupportedException`. See
[Provider-specific SQL](provider-specific/overview.md) for the full catalogue.

## Provider differences

| Provider | Join aliases | Derived-table alias | Outer joins | APPLY / LATERAL |
|---|---|---|---|---|
| SQLite | `as 't1'` | optional | left/right/full supported | not supported (`NotSupportedException`) |
| SQL Server | `as [t1]` | required | left/right/full supported | `CROSS APPLY` / `OUTER APPLY` |
| PostgreSQL | `as "t1"` | required | left/right/full supported | `CROSS JOIN LATERAL` / `LEFT JOIN LATERAL ... ON true` |
| MySQL / MariaDB | `as \`t1\`` | required | left/right supported (no full) | `CROSS JOIN LATERAL` / `LEFT JOIN LATERAL ... ON true` |
| ClickHouse | `as \`t1\`` | required | left/right/full supported | not supported (`NotSupportedException`) |
| In-memory | not applicable (delegate execution) | not applicable | inner/left/right/full/cross supported; APPLY and table-valued function sources are not | not supported (correlated apply included) |

The in-memory provider compiles the join condition to a delegate and loops, so it does not emit SQL;
it supports [`Inner`](xref:NextORM.Core.JoinType.Inner), [`Left`](xref:NextORM.Core.JoinType.Left), [`Right`](xref:NextORM.Core.JoinType.Right), [`Full`](xref:NextORM.Core.JoinType.Full) and [`Cross`](xref:NextORM.Core.JoinType.Cross) joins (see
`tests/nextorm.core.tests/InMemoryJoinTests.cs`). [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0}))/[`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0})) are SQL-only and throw
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
`tests/nextorm.integration.tests/CommonTestSuite.Join.cs:226`,
`tests/nextorm.core.tests/InMemoryJoinTests.cs:14`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:320`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:2282`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:379`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:312`
(and the other provider `SqlGenerationTests.cs`).
