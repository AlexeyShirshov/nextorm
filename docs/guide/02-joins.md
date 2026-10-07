# Joins

> Combine rows from two or more entities, derived queries or raw tables with [`Join`](xref:NextORM.Core.EntityBuilder`1.Join``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})), [`LeftJoin`](xref:NextORM.Core.EntityBuilder`1.LeftJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})), [`RightJoin`](xref:NextORM.Core.EntityBuilder`1.RightJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})), [`FullJoin`](xref:NextORM.Core.EntityBuilder`1.FullJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})), [`CrossJoin`](xref:NextORM.Core.EntityBuilder`1.CrossJoin``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})), [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) and [`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})).

**Prerequisites:** [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Querying and projections](../querying/index.md) · [Filtering (WHERE)](01-filtering-where.md)

## Overview

Every `EntityBuilder<T>` exposes seven join methods: [`Join`](xref:NextORM.Core.EntityBuilder`1.Join``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})) (inner), [`LeftJoin`](xref:NextORM.Core.EntityBuilder`1.LeftJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})), [`RightJoin`](xref:NextORM.Core.EntityBuilder`1.RightJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})), [`FullJoin`](xref:NextORM.Core.EntityBuilder`1.FullJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})),
[`CrossJoin`](xref:NextORM.Core.EntityBuilder`1.CrossJoin``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})), [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) and [`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})). A join condition is an expression over the two sides and is
emitted as the `ON` clause of the join, exactly where the builder can translate it. [`CrossJoin`](xref:NextORM.Core.EntityBuilder`1.CrossJoin``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})),
[`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) and [`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) take no condition; the first emits `cross join`, the latter two emit the
provider's lateral/apply form (see [APPLY and LATERAL](#apply-and-lateral)).

The right-hand side can be:

* another typed entity, `EntityBuilder<TJoinEntity>`;
* a `QueryCommand<TJoinEntity>` — a subquery that is rendered as a derived table;
* a raw table, [`EntityBuilder`](xref:NextORM.Core.EntityBuilder) created through [`From`](xref:NextORM.Core.DataContext.From(System.String)), whose columns are read through
  the [`TableAlias`](xref:NextORM.Core.TableAlias) indexer (`t["id"]`).

Two things are important before looking at the examples:

1. **Join arity is capped at eight tables, at compile time.** The first join returns
   [`JoinedEntityBuilder<T1, T2>`](xref:NextORM.Core.JoinedEntityBuilder`2), the next [`JoinedEntityBuilder<T1, T2, T3>`](xref:NextORM.Core.JoinedEntityBuilder`3), and so on up to `JoinedEntityBuilder<T1..T8>`.
   `JoinedEntityBuilder` deliberately exposes no further [`Join`](xref:NextORM.Core.EntityBuilder`1.Join``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions}))/[`LeftJoin`](xref:NextORM.Core.EntityBuilder`1.LeftJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions}))/[`RightJoin`](xref:NextORM.Core.EntityBuilder`1.RightJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions}))/[`FullJoin`](xref:NextORM.Core.EntityBuilder`1.FullJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions}))/[`CrossJoin`](xref:NextORM.Core.EntityBuilder`1.CrossJoin``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions}))
   methods, and `Projection<T1..T8>` does not implement [`IExtendableProjection`](xref:NextORM.Core.IExtendableProjection), so a ninth join
   does not compile. The cap can be lifted by naming the accumulated projection with
   [`As`](xref:NextORM.Core.EntityBuilder`1.As``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) (see [Naming an intermediate projection](#naming-an-intermediate-projection-as)).
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

## Named join aliases

The positional addressing above is always available for purely positional queries. When two joined
sources share the same CLR type — for example a buyer and an approver are both `Person` — positional
members cannot tell them apart. Named aliases add a name to a slot **without removing the positional
one**, and the two styles may be **freely mixed in one chain**: an alias is optional at every step, and
`p.ItemK` and the alias of slot K are always the **same** slot. Every join operator accepts an optional
trailing `Alias.<Name>` argument that names the new slot; a step without it is positional, so
positional → alias, alias → positional and alternating chains are all valid (within the eight-slot cap,
see [Chained joins and arity 2..8](#chained-joins-and-arity-28)). The generated projection exposes the
alias name as a typed property:

```csharp
using NextORM.Generated.MyAssembly; // Alias lives in NextORM.Generated.<your assembly name>

var people = dataContext.From<Person>(b => b.Table("person"));

var rows = await dataContext.From<Order>(b => b.Table("orders"))
    .Join<Person>(people, (o, buyer) => o.BuyerId == buyer.Id, Alias.Buyer)
    .Join<Person>(people, (o, approver) => o.Item1.ApproverId == approver.Id, Alias.Approver)
    .Select(p => new { Buyer = p.Buyer.Id, Approver = p.Approver.Id })
    .ToListAsync();
```

```sql
select t2.id as 'Buyer', t3.id as 'Approver' from orders as 't1' join person as 't2' on t1.buyerid = t2.id join person as 't3' on t1.approverid = t3.id
```

An aliased step can be followed by a positional one (or the reverse) in the same chain; the slots keep
their order and the SQL aliases still match it:

```csharp
var rows = await dataContext.From<Order>(b => b.Table("orders"))
    .Join<Person>(people, (o, buyer) => o.BuyerId == buyer.Id, Alias.Buyer)  // slot 2, alias
    .Join(people, (p, other) => p.Item2.Id == other.Id)                      // slot 3, positional
    .Select(p => new { Buyer = p.Buyer.Id, Third = p.Item3.Id })
    .ToListAsync();
```

```sql
select t2.id as 'Buyer', t3.id as 'Third' from orders as 't1' join person as 't2' on t1.buyerid = t2.id join person as 't3' on t2.id = t3.id
```

`Alias.<Name>` is a compile-time marker. The source generator that ships inside the `nextorm` package
as a Roslyn analyzer (no additional package or reference is needed) emits, for every used chain, a
`public` projection type `AliasProjection_<suffix>` with one property per alias and a `public` builder
type `AliasJoin_<suffix>` that mirrors the join operators. The suffix encodes **every** slot in order —
`P{slot}` for a positional step and `A{slot}_{name}` for an aliased one — so the same alias names in a
different positional placement get different generated types (for example
`AliasProjection_P1_A2_Buyer_A3_Approver<Order, Person, Person>` and its builder
`AliasJoin_P1_A2_Buyer_A3_Approver<Order, Person, Person>`). All of it lands in the reserved namespace
`NextORM.Generated.<assembly name>`, so no projection has to be pre-declared. The generated properties
carry [`JoinSlot(n)`](xref:NextORM.Core.JoinSlotAttribute), the 1-based entity position in the chain
(the first alias is slot `2`, the next is `3`, and so on), so two aliases of the same CLR type resolve
to different tables. The generated type name always carries the slot-encoded suffix; no name without
a `P{slot}`/`A{slot}_{name}` suffix is produced.

The generated alias members are **expression-only**: they exist so that a `Select`/`Where`
expression tree can name the joined table of the slot, and the translator rewrites them to that
table. Reading one outside an expression tree — for example materialising `p.Buyer` directly —
throws `NotSupportedException` by design instead of returning a defaulted value; project the column
you need (`Select(p => p.Buyer.Id)`) instead. The retained positional `ItemN` members remain ordinary
properties.

All seven projection join operators accept an alias; `JoinInto` is not one of them and has no aliased
surface, so the single-query relationship loader never takes an `Alias.<Name>` argument. The
conditionless three take only the source and the marker:

```csharp
var rows = await dataContext.From<Order>(b => b.Table("orders"))
    .CrossJoin(people, Alias.Person)
    .Select(p => p.Person.Id)
    .ToListAsync();
```

Within one chain the generated projection retains the positional `ItemN` members alongside the alias
names: after `Alias.Buyer`, `p.Buyer` and `p.Item2` are the same slot, so a later step may address the
same table either way and mixing is free in both directions. An alias must be a valid C# identifier
without `_` (reserved for composing the generated type names); a reserved keyword is emitted escaped
(`@class`). The marker class is the only approved alias argument form — `Alias.Buyer<int>` or
`Alias.Buyer.Approver` is rejected. The chain is capped at eight slots by `Projection<T1..T8>`; a ninth
step is a compile-time error, and the cap can be lifted with
[`As`](#naming-an-intermediate-projection-as) exactly as for positional joins.

The generated alias overloads also accept a typed CTE descriptor directly — a [`Cte<T>`](xref:NextORM.Core.Cte`1) in place of
the [`EntityBuilder<T>`](xref:NextORM.Core.EntityBuilder`1) source. The joined type is inferred from the descriptor, so the explicit
`<T>` type argument and the conversion (`dataContext.From(cte)`) can both be omitted:

```csharp
var peopleCte = dataContext.From<Person>(b => b.Table("person"))
    .ToCommand()
    .AsCte("people_cte");

var rows = await dataContext.From<Order>(b => b.Table("orders"))
    .Join(peopleCte, (o, p) => o.BuyerId == p.Id, Alias.Buyer)
    .Select(p => new { OrderId = p.Item1.Id, BuyerId = p.Buyer.Id, BuyerName = p.Buyer.Name })
    .ToListAsync();
```

```sql
-- SQLite: the CTE is joined by name, never wrapped in a derived (select ...) table
... join people_cte as 't2' on t1.buyerid = t2.Id
```

The named path is SQL-provider only: the in-memory provider throws `NotSupportedException`. A query may
cross a method boundary, but the method must name the generated builder type in its signature, because
that type is generated and cannot be inferred from a hand-written name:

```csharp
private static AliasJoin_P1_A2_Buyer_A3_Approver<Order, Person, Person> AddApprover(
    AliasJoin_P1_A2_Buyer<Order, Person> builder,
    EntityBuilder<Person> people)
    => builder.Join<Person>(people, (o, a) => o.Item1.ApproverId == a.Id, Alias.Approver);
```

The generator reports `NORMGEN001`–`NORMGEN008` (a duplicate alias; an alias colliding with a
generated member; an invalid identifier; an arity over eight; an argument that is not in the
`Alias.<Name>` form; an assembly name that cannot be normalised to a namespace; and a `.WithAlias` that
is not applied to a plain root source). See [Limitations](../advanced/limitations.md).

### Naming the root source: `WithAlias`

The **root** source can claim a lexical name for slot 1 with the chained `.WithAlias(Alias.<Name>)`
call. It is root-only: it must be the first alias step on a plain root source — `From<T>`,
`From("table")`/`CreateQueryBuilder`, `FromSql`, `From(Cte<T>)`, a temp-table source,
`FromTableFunction<T>`, `From(QueryCommand<T>)`, `From(builder)` and the `CreateQueryBuilder*`
factories — and never after a join or a second `WithAlias` (a compile-time `NORMGEN008` error, with the
same root-only guard kept at runtime as a failsafe). The named root member is the same slot 1 as the
retained `p.Item1`:

```csharp
var rooted = dataContext.From<Order>(b => b.Table("orders")).WithAlias(Alias.Root);
var people = dataContext.From<Person>(b => b.Table("person"));

var rows = await rooted
    .Join<Person>(people, (o, p) => o.Root.BuyerId == p.Id, Alias.Buyer)
    .Select(p => new { p.Root.Id, p.Item1.Id, BuyerId = p.Buyer.Id })
    .ToListAsync();
```

```sql
select t1.Id, t1.Id, t2.Id as 'BuyerId' from orders as 't1' join person as 't2' on t1.BuyerId = t2.Id
```

`p.Root` and `p.Item1` are the same slot, and after the join the chain may continue positionally or
with another alias. A derived root source is **preserved** when it is aliased: `FromSql`,
`From(builder)` and `From(QueryCommand<T>)` stay a derived table (`(select …) as 't1'`) instead of
being flattened to the physical table. As with join aliases, the named path is SQL-provider only: the
in-memory provider throws `NotSupportedException` at the alias step, while purely positional chains in
memory are unchanged, and the eight-slot cap is lifted by
[`As`](#naming-an-intermediate-projection-as).

## Outer joins

[`LeftJoin`](xref:NextORM.Core.EntityBuilder`1.LeftJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})) keeps every row of the left side and fills the right side with `NULL` when there is no
match; [`RightJoin`](xref:NextORM.Core.EntityBuilder`1.RightJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})) and [`FullJoin`](xref:NextORM.Core.EntityBuilder`1.FullJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})) behave symmetrically.

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .LeftJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { LeftId = p.Item1.Id, RightString = p.Item2.RequiredString })
    .ToList();
```

```sql
select t1.id as 'LeftId', t2.requiredstring as 'RightString' from simple_entity as 't1' left join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
```

[`RightJoin`](xref:NextORM.Core.EntityBuilder`1.RightJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})) and [`FullJoin`](xref:NextORM.Core.EntityBuilder`1.FullJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})) are emitted with the same shape:

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

[`RightJoin`](xref:NextORM.Core.EntityBuilder`1.RightJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})) and [`FullJoin`](xref:NextORM.Core.EntityBuilder`1.FullJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})) are rejected with `NotSupportedException` only when a dialect reports
`SupportsRightFullJoin == false`; all providers nextorm ships declare support.

## Cross join

[`CrossJoin`](xref:NextORM.Core.EntityBuilder`1.CrossJoin``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) takes no condition and produces the Cartesian product:

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

[`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) and [`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) render the provider's lateral-source form. The right-hand side is the same
set of sources that a regular join accepts — a typed entity, a `QueryCommand<T>` derived table, a raw
table or a table-valued function — but there is no `ON` condition:

* [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) keeps only the left-hand rows for which the applied source returns at least one row
  (SQL Server `CROSS APPLY`; PostgreSQL/MySQL/MariaDB `CROSS JOIN LATERAL`, or a plain `CROSS JOIN`
  when the applied source is a plain table — `LATERAL` is only valid before a subquery or function);
* [`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) also keeps left-hand rows whose applied source is empty, filling the right side with
  `NULL` (SQL Server `OUTER APPLY`; PostgreSQL/MySQL/MariaDB `LEFT JOIN LATERAL ... ON true`, or a
  plain `LEFT JOIN ... ON true` for a plain table).

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
select t1.id, t2.somestring from simple_entity as t1 cross join complex_entity as t2
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
receives that row. [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions}))/[`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) accept such a lambda in two forms: one returning a
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
[`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) also keeps left-hand rows whose applied source is empty, projecting `NULL`s.

Correlation needs a lateral source: dialects with `SupportsApply == false` (SQLite, ClickHouse) reject a
correlated apply with `NotSupportedException`, as does the in-memory provider. A correlated apply cannot
reference a join projection; apply it to a single-entity source instead.

### APPLY over a table-valued function

The applied source can be a table-valued function (see [Table-valued functions](11-table-valued-functions.md)):
[`FromTableFunction`](xref:NextORM.Core.DataContextExtensions.FromTableFunction``1(NextORM.Core.IDataContext,System.Linq.Expressions.Expression{System.Func{System.Linq.IQueryable{``0}}})) returns an `EntityBuilder<T>`, so it is passed exactly like an entity. An uncorrelated function is applied directly:

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .CrossApply(dataContext.FromTableFunction(() => Tvf.AllRows()))
    .Select(p => new { p.Item1.Id, p.Item2.Value })
    .ToList();
```

```sql
-- SQL Server
select t1.id, t2.value from simple_entity as [t1] cross apply all_rows() as [t2]
-- PostgreSQL / MySQL / MariaDB
select t1.id, t2.value from simple_entity as t1 cross join lateral all_rows() as t2
```

Correlating the function with the left-hand row passes that row's column as a function argument; the call is then wrapped in a lateral derived table:

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .CrossApply(s => dataContext.FromTableFunction(() => Tvf.ById(s.Id)))
    .Select(p => new { p.Item1.Id, p.Item2.Value })
    .ToList();
```

```sql
-- SQL Server
select t1.id, t3.value from simple_entity as [t1] cross apply (select t2.id, t2.value from rows_by_id(cast(t1.id as bigint)) as [t2]) as [t3]
-- PostgreSQL / MySQL / MariaDB
select t1.id, t3.value from simple_entity as t1 cross join lateral (select t2.id, t2.value from rows_by_id(cast(t1.id as bigint)) as t2) as t3
```

Like every correlated apply, the correlated function form needs a lateral source and is rejected with
`NotSupportedException` on SQLite/ClickHouse and in the in-memory provider; the uncorrelated form follows
the provider's source rules (`CROSS APPLY` on SQL Server, `CROSS JOIN LATERAL` on PostgreSQL/MySQL/MariaDB).

## Joining a subquery

A `QueryCommand<T>` can be joined directly. It is wrapped in parentheses and aliased as a derived
table; the alias is always emitted (even on SQLite, where it is optional) so every reference to the
derived source — the `ON` condition, `WHERE`, `ORDER BY` and the projection — resolves through the same
alias identity. A member that the derived projection renames (`c.String` mapped to `requiredstring`)
is exposed under the projected name (`requiredstring as 'String'`) and is referenced outside by that
exposed name (`t1.'String'`), not by the physical column name.

```csharp
var subQuery = dataContext.From<IComplexEntity>()
    .Where(it => it.Id == 3)
    .Select(it => new { it.Id, it.RequiredString, it.Boolean });

var rows = await dataContext.From<ISimpleEntity>()
    .Join(subQuery, (s, c) => s.Id == c.Id)
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToListAsync();
```

```sql
select t1.id, t2.requiredstring from simple_entity as 't1' join (select id, requiredstring, b as 'Boolean' from complex_entity where id = 3) as 't2' on cast(t1.id as bigint) = t2.id
```

A typed CTE is **not** a subquery: pass a [`Cte<T>`](xref:NextORM.Core.Cte`1) straight to the same seven operators and it
is joined by name — no derived `(select ...)` wrapper — exactly as if you had converted it with
`dataContext.From(cte)` first. See
[Joining a typed CTE directly](08-cte.md#joining-a-typed-cte-directly).

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

A raw-table (`TableAlias`) join now honours an explicit source on the joined builder. When the joined
side is a raw SQL source (`FromSql`), a table override (`b => b.Table("...")` / `WithTableName`), a
derived query (`From(query)`), a table-valued function (`FromTableFunction`), or a typed CTE
(`Cte<T>`), that source is used as the join's `FROM` instead of the entity metadata. A plain mapped
entity with no explicit source keeps the metadata fallback. `SemiJoin`/`AntiJoin` are unaffected: the
joined type still resolves through its entity metadata.

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

The direct [`Cte<T>`](xref:NextORM.Core.Cte`1) overloads are also available at every continuation: each
[`JoinedEntityBuilder<T1..Tn>`](xref:NextORM.Core.JoinedEntityBuilder`2) (receiver arities 2–7) takes a typed CTE in place of
the `EntityBuilder<T>` source for all seven operators, so a CTE can be joined at any step. The
projection still stops at eight slots — arity eight exposes no further join, and a `Cte<T>` does not
lift that cap.

## Naming an intermediate projection: `As`

[`As`](xref:NextORM.Core.EntityBuilder`1.As``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) projects each joined row into a named type and exposes the result as a derived
table, so a later `Join` starts from the named members instead of `p.Item1`, `p.Item2`, …:

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .As(p => new { OrderId = p.Item1.Id, CustomerName = p.Item2.String })
    .Join(dataContext.From<IComplexEntity>(), (d, c2) => d.OrderId == c2.Id)
    .Select(p => new { p.Item1.OrderId, p.Item1.CustomerName, Third = p.Item2.Id })
    .ToList();
```

```sql
select t3.OrderId, t3.CustomerName, t4.id as 'Third' from (select t1.id as 'OrderId', t2.somestring as 'CustomerName' from simple_entity as 't1' join complex_entity as 't2' on cast(t1.id as bigint) = t2.id) as 't3' join complex_entity as 't4' on cast(t3.OrderId as bigint) = t4.id
```

`As` is declared once on `EntityBuilder<T>` and inherited by every `JoinedEntityBuilder<T1..Tn>`, so
there is no per-arity overload. Because the result is a derived table, it also lifts the eight-table
compile-time cap: `.As(...)` resets the arity counter, and a further `Join` returns a fresh
`JoinedEntityBuilder<TResult, …>`.

Only the projected members remain visible after `As`, and the projection becomes a materialization
boundary, so the columns of earlier sources cannot be referenced by later joins. The in-memory
provider compiles joins to delegates and cannot join a derived source, so joining after `As` throws
`NotSupportedException`.

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
(`ANY`/`ALL`/`ASOF`) and the distributed `GLOBAL` prefix. Both are passed to the join through its
trailing `Action<JoinOptions>` lambda, with [`JoinOptions.WithStrictness`](xref:NextORM.ClickHouse.ClickHouseEntityBuilderExtensions.WithStrictness(NextORM.Core.JoinOptions,NextORM.Core.JoinStrictness)) and [`JoinOptions.Global`](xref:NextORM.ClickHouse.ClickHouseEntityBuilderExtensions.Global(NextORM.Core.JoinOptions)):

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .LeftJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id,
        j => j.WithStrictness(JoinStrictness.Any))
    .Select(p => new { p.Item1.Id, p.Item2.String })
    .ToList();
```

```sql
select t1.id, t2.somestring from simple_entity as `t1` left any join complex_entity as `t2` on cast(t1.id as bigint) = t2.id
```

[`JoinStrictness.Any`](xref:NextORM.Core.JoinStrictness.Any) renders `<type> any join` and keeps a single
right-hand row per left-hand row; [`JoinStrictness.All`](xref:NextORM.Core.JoinStrictness.All) keeps every
match; [`JoinStrictness.Asof`](xref:NextORM.Core.JoinStrictness.Asof) renders `asof join`, which requires one
equi-join column plus a final inequality. [`JoinOptions.Global`](xref:NextORM.ClickHouse.ClickHouseEntityBuilderExtensions.Global(NextORM.Core.JoinOptions)) renders the
`GLOBAL` prefix used by distributed queries and composes with the strictness modifier in either order
(`global left any join`):

```csharp
var global = dataContext.From<ISimpleEntity>()
    .LeftJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id,
        j => j.Global().WithStrictness(JoinStrictness.Any));
```

```sql
... from simple_entity as `t1` global left any join complex_entity as `t2` on ...
```

The options are copied into the join they are declared on, so they stay scoped to that join and never
leak into another query or a later join. A modifier is only accepted on
`INNER`/`LEFT`/`RIGHT`/`FULL` joins (`CROSS`/`APPLY` throw `NotSupportedException`). The modifiers are
ClickHouse-only ([`SupportsJoinStrictness`](xref:NextORM.Core.ISqlDialect.SupportsJoinStrictness),
[`SupportsGlobalJoin`](xref:NextORM.Core.ISqlDialect.SupportsGlobalJoin)); every other provider and the
in-memory context reject them with `NotSupportedException`.

### SEMI / ANTI / PASTE joins

`SEMI`, `ANTI` and `PASTE` are join *kinds* rather than modifiers: they change which columns and rows
the join contributes, so they get dedicated builders instead of the `j => j.WithStrictness(...)` option:

```csharp
var ids = dataContext.From<ISimpleEntity>()
    .SemiJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(s => s.Id)
    .ToList();
```

```sql
select t1.id from simple_entity as `t1` left semi join complex_entity as `t2` on cast(t1.id as bigint) = t2.id
```

- [`SemiJoin`](xref:NextORM.ClickHouse.ClickHouseEntityBuilderExtensions.SemiJoin``2(NextORM.Core.EntityBuilder{``0},NextORM.Core.EntityBuilder{``1},System.Linq.Expressions.Expression{System.Func{``0,``1,System.Boolean}},System.Action{NextORM.Core.JoinOptions})) keeps only the left-hand columns, once per left row that
  has at least one matching right row;
- [`AntiJoin`](xref:NextORM.ClickHouse.ClickHouseEntityBuilderExtensions.AntiJoin``2(NextORM.Core.EntityBuilder{``0},NextORM.Core.EntityBuilder{``1},System.Linq.Expressions.Expression{System.Func{``0,``1,System.Boolean}},System.Action{NextORM.Core.JoinOptions})) keeps only the left-hand columns for left rows with no
  match (the complement of `SemiJoin`);
- [`PasteJoin`](xref:NextORM.ClickHouse.ClickHouseEntityBuilderExtensions.PasteJoin``2(NextORM.Core.EntityBuilder{``0},NextORM.Core.EntityBuilder{``1},System.Action{NextORM.Core.JoinOptions})) pairs the two sources by row position with no `ON`; the
  projection exposes both sides and the result has as many rows as the shorter side.

`SemiJoin`/`AntiJoin` return the same projection shape (the right columns are not accessible), whereas
`PasteJoin` extends it by one item. They are ClickHouse-only
([`SupportsSemiAntiJoin`](xref:NextORM.Core.ISqlDialect.SupportsSemiAntiJoin)/[`SupportsPasteJoin`](xref:NextORM.Core.ISqlDialect.SupportsPasteJoin));
every other provider and the in-memory context reject them with `NotSupportedException`. See
[Provider-specific SQL](provider-specific/overview.md) for the full catalogue.

## Provider differences

| Provider | Positional SQL table aliases | Derived-table alias | Outer joins | APPLY / LATERAL |
|---|---|---|---|---|
| SQLite | `as 't1'` | always emitted (optional in SQLite itself) | left/right/full supported | not supported (`NotSupportedException`) |
| SQL Server | `as [t1]` | required | left/right/full supported | `CROSS APPLY` / `OUTER APPLY` |
| PostgreSQL | `as "t1"` | required | left/right/full supported | `CROSS JOIN LATERAL` / `LEFT JOIN LATERAL ... ON true` (plain tables: `CROSS JOIN` / `LEFT JOIN ... ON true`) |
| MySQL / MariaDB | `as \`t1\`` | required | left/right supported (no full) | `CROSS JOIN LATERAL` / `LEFT JOIN LATERAL ... ON true` (plain tables: `CROSS JOIN` / `LEFT JOIN ... ON true`) |
| ClickHouse | `as \`t1\`` | required | left/right/full supported | not supported (`NotSupportedException`) |
| In-memory | not applicable (delegate execution) | not applicable | inner/left/right/full/cross supported; APPLY and table-valued function sources are not | not supported (correlated apply included) |

The in-memory provider compiles the join condition to a delegate and loops, so it does not emit SQL;
it supports [`Inner`](xref:NextORM.Core.JoinType.Inner), [`Left`](xref:NextORM.Core.JoinType.Left), [`Right`](xref:NextORM.Core.JoinType.Right), [`Full`](xref:NextORM.Core.JoinType.Full) and [`Cross`](xref:NextORM.Core.JoinType.Cross) joins (see
`tests/nextorm.core.tests/InMemoryJoinTests.cs`). [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions}))/[`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) are SQL-only and throw
`NotSupportedException` on the in-memory provider, like the other unsupported join types. Joins through
`JoinedEntityBuilder<T1..T8>` are resolved at query build time on every provider.

## See also

- [Subqueries](05-subqueries.md) - a joined `QueryCommand<T>` is a derived table.
- [Grouping and aggregates](03-grouping-and-aggregates.md) - aggregate over a join.
- [Query hints](13-query-hints.md) - statement-level hints such as SQL Server `OPTION (RECOMPILE)`.
- [Relationships and single-query loading (`JoinInto`)](../advanced/relationships.md) - declared relationship metadata and the single-query child-collection loader.
- [Provider-specific SQL](provider-specific/overview.md) - the full catalogue of provider-only constructs.
- [Querying and projections](../querying/index.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite.Join.cs:9`,
`tests/nextorm.integration.tests/CommonTestSuite.Join.cs:226`,
`tests/nextorm.core.tests/InMemoryJoinTests.cs:14`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:320`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:2282`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:379`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:312`
(and the other provider `SqlGenerationTests.cs`).
