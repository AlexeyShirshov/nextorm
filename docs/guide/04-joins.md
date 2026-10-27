# Joins

> Combine rows from two or more entities, derived queries or raw tables with [`Join`](xref:NextORM.Core.EntityBuilder`1.Join``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})), [`LeftJoin`](xref:NextORM.Core.EntityBuilder`1.LeftJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})), [`RightJoin`](xref:NextORM.Core.EntityBuilder`1.RightJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})), [`FullJoin`](xref:NextORM.Core.EntityBuilder`1.FullJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})), [`CrossJoin`](xref:NextORM.Core.EntityBuilder`1.CrossJoin``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})), [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) and [`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})).

**Prerequisites:** [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Querying and projections](../engine/index.md) · [Filtering (WHERE)](03-filtering-where.md)

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
* a mapped entity named by its type, without passing a source builder — see [Source-less (type-only) joins](#source-less-type-only-joins);

Two things are important before looking at the examples:

1. **Join arity has a flat arity-chain scope bound of eight tables.** The first join returns
   [`JoinedEntityBuilder<T1, T2>`](xref:NextORM.Core.JoinedEntityBuilder`2), the next [`JoinedEntityBuilder<T1, T2, T3>`](xref:NextORM.Core.JoinedEntityBuilder`3), and so on up to `JoinedEntityBuilder<T1..T8>`.
   Beyond this point no further [`Join`](xref:NextORM.Core.EntityBuilder`1.Join``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions}))/[`LeftJoin`](xref:NextORM.Core.EntityBuilder`1.LeftJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions}))/[`RightJoin`](xref:NextORM.Core.EntityBuilder`1.RightJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions}))/[`FullJoin`](xref:NextORM.Core.EntityBuilder`1.FullJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions}))/[`CrossJoin`](xref:NextORM.Core.EntityBuilder`1.CrossJoin``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions}))
   methods, and `Projection<T1..T8>` does not implement [`IExtendableProjection`](xref:NextORM.Core.IExtendableProjection), so the flat typed chain
   stops there. That is a scope bound, **not** a compile-time rejection: `JoinedEntityBuilder<T1..T8>` still
   inherits the generic-base `EntityBuilder<Projection<T1..T8>>` members, so a further source-less (and
   source-explicit) join does compile, wrapping the accumulated eight-table projection as `Item1`. Name the
   projection with
   [`As`](xref:NextORM.Core.EntityBuilder`1.As``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) (see [Naming an intermediate projection](#naming-an-intermediate-projection-as)) to keep the shape clean.
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
select t1.id, t2.requiredstring
 from simple_entity as 't1'
 join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
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

```sql
select t1.id, t2.requiredstring
 from (select id
 from simple_entity
 where (id > 2)) as 't1'
 join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
 where t2.requiredstring = '34mfs'
```

The pre-join filter is pushed into the left source as a derived table (`select id from simple_entity where
(id > 2)`). That is what makes the placement observable on an outer join: before a [`RightJoin`](xref:NextORM.Core.EntityBuilder`1.RightJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})) it stays on the
left source, so every row of the joined table survives,

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Where(it => it.Id > 2)
    .RightJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToListAsync();
```

```sql
select t1.id, t2.requiredstring
 from (select id
 from simple_entity
 where (id > 2)) as 't1'
 right join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
```

whereas after the join the same predicate is emitted in the outer `where` and drops the rows the `right join`
had preserved:

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .RightJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Where(p => p.Item1.Id > 2)
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToListAsync();
```

```sql
select t1.id, t2.requiredstring
 from simple_entity as 't1'
 right join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
 where (t1.id > 2)
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
select t1.id as 'Order', t2.requiredstring as 'Customer'
 from simple_entity as 't1'
 join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
```

A projection member is usually a column of one of the joined entities (`p.Item1.Id`), but the whole item can
also be selected directly: `Select(p => p.Item1)` / `Select(p => p.Item2)` selects only that entity's mapped
columns and materializes the entity — or `null` on the missing outer-join side. When you need selected columns
from the same side without materializing the entity, list each of them explicitly.

```csharp
var children = await dataContext.From<ISimpleEntity>()
    .LeftJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => p.Item2)
    .ToListAsync();
```

```sql
select t2.id, t2.nullableint, t2.somestring, ...
 from simple_entity as 't1'
 left join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
```

A wildcard `SELECT *` is never generated — the mapped columns are listed explicitly — and comparing whole
entities in a predicate or casting an item to an unrelated result type is not supported.

## Named join aliases

The positional addressing above is unchanged and always available. A join may additionally carry an
optional trailing `Alias.<Name>` argument that names the new slot, and positional and named steps can
be **freely mixed** in one chain and in any order — an alias is optional at every step. A named slot
and the positional `ItemK` of the same slot are the same table: after `Alias.Buyer`, `p.Buyer` and
`p.Item2` resolve to slot 2, which still maps to the SQL alias `t2`. Named aliases matter when two
joined sources share the same CLR type — for example a buyer and an approver are both `Person` —
because positional members cannot tell them apart. The generated projection exposes each name as a
typed property:

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
select t2.id as 'Buyer', t3.id as 'Approver'
 from orders as 't1'
 join person as 't2' on t1.buyerid = t2.id
 join person as 't3' on t1.approverid = t3.id
```

`Alias.<Name>` is a compile-time marker. The source generator that ships inside the `nextorm` package
as a Roslyn analyzer (no additional package or reference is needed) emits, for every discovered slot
chain, a `public` projection type `AliasProjection_<suffix>` with one property per named slot and a
`public` builder type `AliasJoin_<suffix>` that mirrors the join operators. The suffix encodes the
whole chain slot by slot — `P{slot}` for a positional slot and `A{slot}_{name}` for a named one — so
`From<Order>().Join<Person>(people, …, Alias.Buyer)` yields `AliasJoin_P1_A2_Buyer<Order, Person>`.
All of it lands in the reserved namespace `NextORM.Generated.<assembly name>`, so no projection has to
be pre-declared. The generated properties carry [`JoinSlot(n)`](xref:NextORM.Core.JoinSlotAttribute),
the 1-based entity position in the chain: the first joined source is slot `2`, the next is `3`, and so
on. Because the slot is explicit, two aliases of the same CLR type resolve to different tables, and
because the suffix kind letter disambiguates the tokens (an alias name may not contain `_`), a name
ending in a digit (`Buyer2`) is a name, never a slot number.

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
var people = dataContext.From<Person>(b => b.Table("person"));

var rows = await dataContext.From<Order>(b => b.Table("orders"))
    .CrossJoin(people, Alias.Person)
    .Select(p => p.Person.Id)
    .ToListAsync();
```

```sql
select t2.id
 from orders as 't1'
 cross join person as 't2'
```

Within one chain the generated projection retains the positional `ItemN` members alongside the named
ones, and the two address the same slots; mixing is free in both directions and may alternate:

```csharp
var chained = dataContext.From<Order>(b => b.Table("orders"))
    .Join<Person>(people, (o, p) => o.BuyerId == p.Id, Alias.Buyer)      // slot 2, named
    .Join(people, (p, x) => p.Item2.Id == x.Id)                          // slot 3, positional
    .Join<Person>(people, (p, a) => p.Item3.Id == a.Id, Alias.Approver); // slot 4, named
```

```sql
...
 join person as 't2' on t1.buyerid = t2.id
 join person as 't3' on t2.id = t3.id
 join person as 't4' on t3.id = t4.id
```

An alias must be a valid C# identifier without `_` (reserved for composing the generated type names);
a reserved keyword is emitted escaped (`@class`). The marker class is the only approved alias argument
form — `Alias.Buyer<int>` or `Alias.Buyer.Approver` is rejected. The flat typed chain the alias surface
extends stops at eight slots (`Projection<T1..T8>`); like the positional chain, that is a scope bound,
not a compile-time rejection.

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
...
 join people_cte as 't2' on t1.buyerid = t2.Id
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

The generator reports `NORMGEN001`–`NORMGEN009` (a duplicate alias; an alias colliding with a generated
member; an invalid identifier; an arity over eight; an argument that is not in the `Alias.<Name>` form;
an assembly name that cannot be normalised to a namespace; a join-alias extension signature collision;
`.WithAlias` applied to a non-root source; and an alias applied to a chain whose projected type is
anonymous). See [Limitations](../advanced/limitations.md).

`NORMGEN007` guards against alias-join overloads that would share a signature but have different bodies.
On the current generator that situation is **structurally unreachable**, so the diagnostic is
**defensive**: the emitted method body is a pure function of the emitted signature, and the last step's
joined type is always rendered as the type parameter `TJoin`. Two alias joins that share `Alias.X` but
project different last-step types are **both supported**: they share one generic `Join<TJoin>` overload
rather than producing two conflicting overloads — only separate typed builders are not produced. Give
each slot a distinct alias name (as with `Alias.Buyer` and `Alias.Approver` above) to get a separate
typed builder per chain; that is a builder-granularity choice only, not a verified `NORMGEN007`
remediation.

### Source-less aliases

The four predicate-based operators accept the marker on their source-less form as well. The joined
source is created from the receiver's own data context, so the call is a condition plus the marker —
with an optional join-options callback after the marker:

```csharp
var rows = await dataContext.From<Order>(b => b.Table("orders"))
    .Join<Person>((o, p) => o.BuyerId == p.Id, Alias.Buyer)            // slot 2, options omitted
    .Join<Person>((p, a) => p.Item2.Id == a.Id, Alias.Approver)        // slot 3, options omitted
    .Select(p => new { Buyer = p.Buyer.Id, Approver = p.Approver.Id })
    .ToListAsync();
```

```sql
select t2.id as 'Buyer', t3.id as 'Approver'
 from orders as 't1'
 join person as 't2' on t1.buyerid = t2.id
 join person as 't3' on t2.id = t3.id
```

`Join<TJoin>((left, right) => ..., Alias.X[, options])`, and the same shape for `LeftJoin`, `RightJoin`
and `FullJoin`, is the complete source-less alias surface. It is also the **only join** alias surface that
takes options: `(condition, marker, Action<JoinOptions> options)` accepts an omitted argument, an
explicit `null` or a callback — the first two mean "no options", and a supplied callback runs exactly
once per call with the same hint, lock and relationship semantics as the positional join overloads.
The marker means what it means everywhere else: it names the new slot (`Alias.Buyer` at slot 2 is also
`p.Item2`), the chain continues on the generated `AliasJoin_<suffix>` receiver, and source-less,
source-explicit and positional steps mix freely in one chain.

The receivers are the ones the marker form already has: the root `EntityBuilder<T>` and the
`JoinedEntityBuilder<T1..Tn>` arities 2–7, i.e. the alias join lands on slot 2 through slot 8. The two
boundaries of the source-less form itself are unchanged: `CrossJoin`, `CrossApply` and `OuterApply`
take no condition, so they have no source-less form (their aliased forms keep taking the explicit
source), and a raw-table source is not resolvable by type. The provider boundaries are unchanged too:
the in-memory provider throws `NotSupportedException` on any alias, and MySQL/MariaDB refuse `FullJoin`
in **every** form — source-less, source-explicit and positional — with `NotSupportedException` naming
the unsupported `Full join`, before any SQL is rendered, so an unsupported operator is never silently
skipped.

### Naming the root source (`.WithAlias`)

The root source occupies slot 1, so it can be named with a chained `.WithAlias(Alias.X)` applied to
the plain source builder before any join. The alias names the existing slot 1 — it adds no source and
no slot — and it works for every root-source family: `From<T>` (a mapped entity), `From("table")` and
`CreateQueryBuilder("table")` (a [`TableAlias`](xref:NextORM.Core.TableAlias) source), `FromSql`, a
typed [`Cte<T>`](xref:NextORM.Core.Cte`1), a lazy temp table, the runtime table-function source
`From(() => ...)`, `From(QueryCommand<T>)` and the `CreateQueryBuilder*` forwarders. The physical source is preserved: a
derived root (`FromSql`, `From(query)`, `From(builder)`) stays a derived table aliased to `t1`, and a
mapped entity keeps reading its physical table. `Where`, `OrderBy`, `GroupBy`, `Having` and paging
applied before `.WithAlias` are carried onto the root projection.

```csharp
var rooted = dataContext.From<Order>(b => b.Table("orders")).WithAlias(Alias.Order); // slot 1

var rows = await rooted
    .Join<Person>(people, (o, p) => o.Order.BuyerId == p.Id, Alias.Buyer)
    .Select(p => new { OrderId = p.Order.Id, Buyer = p.Buyer.Id })
    .ToListAsync();
```

```sql
select t1.id as 'OrderId', t2.id as 'Buyer'
 from orders as 't1'
 join person as 't2' on t1.buyerid = t2.id
```

`p.Order` is the same slot 1 as `p.Item1`. A positional join may follow a root alias (the generated
positional transition binds to the root), so naming the root does not force the rest of the chain to be
named. `.WithAlias` is root-only: applying it after a join or a second root alias is a compile-time
error (`NORMGEN008`) and the runtime seam fails closed. Like every named alias, it is expression-only
and the in-memory provider throws `NotSupportedException`.

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
select t1.id as 'LeftId', t2.requiredstring as 'RightString'
 from simple_entity as 't1'
 left join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
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

## Source-less (type-only) joins

The four conditional operators also have a source-less form that resolves the joined entity through the
same data context. Instead of passing `dataContext.From<TJoinEntity>()`, name the joined type as the
generic argument and pass only the condition:

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .Join<IComplexEntity>((s, c) => s.Id == c.Id)
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToList();
```

`Join<TJoinEntity>`, `LeftJoin<TJoinEntity>`, `RightJoin<TJoinEntity>` and `FullJoin<TJoinEntity>` take
the same `predicate` and optional `options` as their source-explicit counterparts and forward them
unchanged (the `options` callback is invoked once per call). For a **non-null** predicate the
type-only call is equivalent to the explicit `dataContext.From<TJoinEntity>()` form: the joined source is
resolved in the same context (configured mapping first, then convention/metadata) and produces the same
SQL, AST and rows. A `null` predicate is the one deliberate difference (see below).

The source-less form is available on every receiver arity, so a chain can be built entirely by type:

```csharp
var sql = dataContext.From<ISimpleEntity>()
    .Join<IComplexEntity>((s, c) => s.Id == c.Id)
    .Join<ISimpleEntity>((p, s) => p.Item2.Id == s.Id)
    .Join<IComplexEntity>((p, c) => p.Item3.Id == c.Id)
    // … up to JoinedEntityBuilder<T1..T8> (eight tables)
    .Select(p => new { p.Item1.Id, p.Item4.RequiredString });
```

### Inference and the invalid-null caveat

* **Typed predicate, explicit type argument.** `Join<TJoinEntity>((left, right) => …)` is the documented
  call: the generic argument fixes the joined type, so an explicit generic argument is the reliable form.
  When the predicate is typed (both lambda parameters carry types), the joined type can also be inferred.
* **Untyped lambda without an inferable type is a compile error.** `Join((a, b) => …)` cannot infer
  `TJoinEntity` from its parameters and does not fall through to another overload; pass the type argument
  or type the lambda.
* **`null` condition.** A `null` predicate passed to a source-less overload is rejected with
  `ArgumentNullException` before any source resolution, on every receiver and operator. This is a
  deliberate difference from the source-explicit form, **not** parity:
  `Join(dataContext.From<TJoinEntity>(), null)` is accepted (`JoinCore` takes a nullable `LambdaExpression`),
  so a null-condition join is expressible only through the explicit-source form. The equivalence statement
  above therefore covers non-null predicates only.
* **Invalid all-null pair.** The untyped all-`null` pair `joined.Join<T>(null, null)` compiles: overload
  resolution binds it to the pre-existing two-argument
  `Join<TJoinEntity>(Cte<TJoinEntity>, Expression<…>)` overload, which then rejects the null CTE with
  `ArgumentNullException` (`Parameter 'cte'`). This is not a new ambiguity introduced by the source-less
  overloads; use a typed argument to state the intent.

### Scope

The source-less form is limited to the four predicate-based operators. `CrossJoin`, `CrossApply` and
`OuterApply` have no predicate and are **not** provided source-less; a source-less `EntityBuilder` over a
raw table (`From("table")`) is likewise **not** provided. The form is available on the `EntityBuilder<T>`
receiver (arity 2) and on `JoinedEntityBuilder<T1..Tn>` receivers with arity 2–7 (resulting arity 3–8).
**No new** source-less overload is *declared* on the terminal `JoinedEntityBuilder<T1..T8>` — that is the
flat arity-chain scope bound, which stops at arity 8. It is **not** a compile-time rejection: the terminal
builder still inherits the generic-base `EntityBuilder<Projection<T1..T8>>` members, so a further
source-less join (like a further source-explicit join) *does* compile, treating the accumulated eight-table
projection as `Item1`. Use `As`/`From(query)` to name the projection when the flat chain is no longer the
right shape. The same four operators also accept an `Alias.<Name>` marker — with the optional join
options after it — on every one of those receivers; see [Source-less aliases](#source-less-aliases).

## Cross join

[`CrossJoin`](xref:NextORM.Core.EntityBuilder`1.CrossJoin``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) takes no condition and produces the Cartesian product:

```csharp
var count = dataContext.From<ISimpleEntity>().CrossJoin(dataContext.From<IComplexEntity>()).Count();
```

```sql
select count(*)
 from simple_entity as 't1'
 cross join complex_entity as 't2'
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
  (SQL Server `CROSS APPLY`; PostgreSQL/MySQL 8.0.14+ `CROSS JOIN LATERAL`, or a plain `CROSS JOIN`
  when the applied source is a plain table — `LATERAL` is only valid before a subquery or function);
* [`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) also keeps left-hand rows whose applied source is empty, filling the right side with
  `NULL` (SQL Server `OUTER APPLY`; PostgreSQL/MySQL 8.0.14+ `LEFT JOIN LATERAL ... ON true`, or a
  plain `LEFT JOIN ... ON true` for a plain table).

**MariaDB 11.4 has no lateral derived table**, so `MariaDbDialect` reports `SupportsApply == false` and
**both** [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) and [`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) are refused with `NotSupportedException`
while the command is prepared — before any SQL reaches the server (see [Limitations](../advanced/limitations.md)).
SQLite and ClickHouse report the same capability as `false`, and the in-memory provider does not support
`APPLY` at all.

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .CrossApply(dataContext.From<IComplexEntity>())
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToListAsync();
```

```sql
-- SQL Server
select t1.id, t2.somestring
 from simple_entity as [t1]
 cross apply complex_entity as [t2]
-- PostgreSQL / MySQL 8.0.14+
select t1.id, t2.somestring
 from simple_entity as t1
 cross join complex_entity as t2
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
...
 from simple_entity as [t1]
 outer apply (select id, somestring
 from complex_entity where ...) as [t2]
-- PostgreSQL / MySQL 8.0.14+
...
 from simple_entity as t1
 left join lateral (select ...) as t2 on true
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
...
 from simple_entity as [t1]
 cross apply (select ...
 from complex_entity as [t2] where t2.id = t1.id) as [t3]
-- PostgreSQL / MySQL 8.0.14+
...
 from simple_entity as t1
 cross join lateral (select ...) as t3
```

The lambda parameter behaves like the outer parameter of a correlated scalar subquery: a column of the
left-hand row (here `s.Id`) becomes an outer reference resolved to the left-hand table alias.
[`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) also keeps left-hand rows whose applied source is empty, projecting `NULL`s.

Correlation needs a lateral source: dialects with `SupportsApply == false` — SQLite, ClickHouse and
MariaDB 11.4 — reject every `CrossApply`/`OuterApply` with `NotSupportedException` while the command is
prepared, before SQL is rendered for execution; the in-memory provider rejects them too. A correlated
apply can also follow a join — see [Correlated APPLY after a join](#correlated-apply-after-a-join).

### Correlated APPLY after a join

`CrossApply`/`OuterApply` accept a correlated source on a receiver that already carries joined sources. The
form is available on the flat `JoinedEntityBuilder<T1..Tn>` receivers (a result of **3–8 total sources**,
i.e. 2–7 preceding sources plus the applied one) and on the generated alias receivers, with both lambda
returns — `EntityBuilder<T>` (the whole entity) and `QueryCommand<T>` (a projected derived query). The
hand-written overloads take an optional `Action<JoinOptions>` after the lambda; on an alias receiver the
marker form adds `Alias.<Name>` as its last argument and takes no options, while the positional transition
adds an unnamed `ItemN` and takes the optional options callback instead.

The applied source is the **next slot** of the same flat projection — never nested inside the previous
item — so with two joined sources the result reads `p.Item1`, `p.Item2`, `p.Item3`:

```csharp
var rows = await dataContext.From<Order>(b => b.Table("orders"))
    .Join(dataContext.From<Person>(b => b.Table("person")), (o, buyer) => o.BuyerId == buyer.Id)
    .CrossApply((Projection<Order, Person> p) => dataContext.From<OrderLine>(b => b.Table("order_lines"))
        .Where(l => l.OrderId == p.Item1.Id && l.CreatedBy == p.Item2.Id)
        .Select(l => new { l.Id, l.Total }))
    .Select(p => new { OrderId = p.Item1.Id, Buyer = p.Item2.Name, Total = p.Item3.Total })
    .ToListAsync();
```

```sql
-- SQL Server
select t1.id as 'OrderId', t2.name as 'Buyer', t4.total as 'Total'
 from orders as [t1]
 join person as [t2] on t1.buyerid = t2.id
 cross apply (select t3.id, t3.total
 from order_lines as [t3]
 where (t3.orderid = t1.id and t3.createdby = t2.id)) as [t4]
-- PostgreSQL / MySQL 8.0.14+
select t1.id as "OrderId", t2.name as "Buyer", t4.total as "Total"
 from orders as t1
 join person as t2 on t1.buyerid = t2.id
 cross join lateral (select t3.id, t3.total
 from order_lines as t3
 where (t3.orderid = t1.id and t3.createdby = t2.id)) as t4
```

`CrossApply` keeps a parent row only when the applied source returns at least one row, so an order with
no matching line disappears. [`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) keeps it: the parent row survives, the applied columns
come back as SQL `NULL`, and a whole-entity applied projection materialises as `null`:

```csharp
var rows = await dataContext.From<Order>(b => b.Table("orders"))
    .Join(dataContext.From<Person>(b => b.Table("person")), (o, buyer) => o.BuyerId == buyer.Id)
    .OuterApply((Projection<Order, Person> p) => dataContext.From<OrderLine>(b => b.Table("order_lines"))
        .Where(l => l.OrderId == p.Item1.Id))
    .Select(p => new { OrderId = p.Item1.Id, Line = p.Item3 })
    .ToListAsync(); // an order without lines is kept, with Line == null
```

```sql
-- SQL Server
select t1.id as 'OrderId', t4.id, t4.order_id, t4.created_by, t4.total
 from orders as [t1]
 join person as [t2] on t1.buyerid = t2.id
 outer apply (select t3.id, t3.order_id, t3.created_by, t3.total
 from order_lines as [t3]
 where t3.orderid = t1.id) as [t4]
-- PostgreSQL / MySQL 8.0.14+
...
 left join lateral (select ...) as t4 on true
```

Correlation binds to the **exact preceding source occurrence**, not to the CLR type: after a self-join, or
when the applied body uses the same CLR type as an earlier source, `p.Item2` still addresses the second
occurrence and `p.Item1` the base one. A named slot addresses the same occurrence (`p.Buyer` is
`p.Item2`), and the generated alias APPLY adds one flat slot that keeps both identities:

```csharp
var people = dataContext.From<Person>(b => b.Table("person"));

var rows = await dataContext.From<Order>(b => b.Table("orders"))
    .WithAlias(Alias.Order)
    .Join<Person>(people, (o, buyer) => o.Order.BuyerId == buyer.Id, Alias.Buyer)
    .OuterApply(p => people.Where(x => x.Id == p.Order.ApproverId), Alias.Approver)
    .Select(p => new { OrderId = p.Order.Id, Approver = p.Approver.Name })
    .ToListAsync();
```

The applied source can also be a runtime table-valued-function body, with the same slot addressing: the
factory call is the applied source, an outer member in its argument stays a column reference to the slot it
names (`p.Item1` is this command's `FROM`), and the applied row becomes the next flat slot:

```csharp
var rows = await dataContext.From<Order>(b => b.Table("orders"))
    .Join(dataContext.From<Person>(b => b.Table("person")), (o, buyer) => o.BuyerId == buyer.Id)
    .CrossApply((Projection<Order, Person> p) => Tvf.ById(p.Item1.Id))
    .Select(p => new { OrderId = p.Item1.Id, Buyer = p.Item2.Name, Line = p.Item3.Id })
    .ToListAsync();
// SQL Server: cross apply (select t3.id from rows_by_id(t1.id) as [t3]) as [t4]
```

A **composed** function body — the factory call followed by builder operators such as `.Where(...)` — is
not supported as an applied source: it is refused with `NotSupportedException` while the query is built,
before anything is compiled or executed, and the message names the composition and both supported forms. Use
a derived query for that shape:

```csharp
// refused: the applied source would be a composed function chain
joined.CrossApply((Projection<Order, Person> p) => Tvf.ById(p.Item1.Id).Where(r => r.Id > 0));

// supported: a derived query carrying the correlation and the extra operators
joined.CrossApply((Projection<Order, Person> p) => dataContext.From<OrderLine>(b => b.Table("order_lines"))
    .Where(l => l.OrderId == p.Item1.Id && l.Total > 0)
    .Select(l => new { l.Id, l.Total }));
```

The flat chain is capped at the usual **eight total sources** (`Projection<T1..T8>`). A ninth correlated
source is refused with `NotSupportedException` while the query is prepared — before the applied source is
evaluated — instead of nesting the projection. Project the accumulated join into a named type with `As`
(or start from `From(query)`) when the chain no longer fits.

### APPLY over a table-valued function

The applied source can be a runtime table-valued-function factory (see
[Table-valued functions](13-table-valued-functions.md)): the correlated lambda body is the factory call, so
the function's arguments keep their expressions and an outer-row member stays a column reference.

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .CrossApply(s => Tvf.ById(s.Id))
    .Select(p => new { p.Item1.Id, p.Item2.Value })
    .ToList();
```

```sql
-- SQL Server
select t1.id, t3.value
 from simple_entity as [t1]
 cross apply (select t2.id, t2.value
 from rows_by_id(cast(t1.id as bigint)) as [t2]) as [t3]
-- PostgreSQL / MySQL 8.0.14+
select t1.id, t3.value
 from simple_entity as "t1"
 cross join lateral (select t2.id, t2.value
 from rows_by_id(cast(t1.id as bigint)) as "t2") as "t3"
```

An uncorrelated function is passed with an empty parameter list and applied once:

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .CrossApply(() => Tvf.AllRows())
    .Select(p => new { p.Item1.Id, p.Item2.Value })
    .ToList();
```

```sql
-- SQL Server
select t1.id, t2.value
 from simple_entity as [t1]
 cross apply all_rows() as [t2]
-- PostgreSQL / MySQL 8.0.14+
select t1.id, t2.value
 from simple_entity as t1
 cross join lateral all_rows() as t2
```

A correlated `JSON_TABLE` on MySQL/MariaDB is rendered as the join source itself — no derived wrapper and
no `LATERAL` keyword (MariaDB has none, and MySQL rejects an outer reference inside a table function under
`LATERAL`): `join json_table(t1.children_json, '$[*]' columns(...)) as jt`, or
`left join json_table(...) as jt on true` for `OuterApply`.

Like every correlated apply, the correlated function form needs a lateral source and is rejected with
`NotSupportedException` on SQLite, ClickHouse, MariaDB 11.4 and in the in-memory provider; the uncorrelated
form follows the provider's source rules (`CROSS APPLY` on SQL Server, `CROSS JOIN LATERAL` on
PostgreSQL/MySQL 8.0.14+).

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
select t1.id, t2.requiredstring
 from simple_entity as 't1'
 join (select id, requiredstring, b as 'Boolean'
 from complex_entity where id = 3) as 't2' on cast(t1.id as bigint) = t2.id
```

A typed CTE is **not** a subquery: pass a [`Cte<T>`](xref:NextORM.Core.Cte`1) straight to the same seven operators and it
is joined by name — no derived `(select ...)` wrapper — exactly as if you had converted it with
`dataContext.From(cte)` first. See
[Joining a typed CTE directly](10-cte.md#joining-a-typed-cte-directly).

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
select t1.id, t2.id as 'SId'
 from (select id, requiredstring
 from complex_entity where (id > 0)) as 't1'
 join simple_entity as 't2' on cast(t1.id as bigint) = t2.id
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
select t3.OrderId, t3.CustomerName, t4.requiredstring as 'Third'
 from (select t1.id as 'OrderId', t2.requiredstring as 'CustomerName'
 from simple_entity as 't1'
 join complex_entity as 't2' on cast(t1.id as bigint) = t2.id) as 't3'
 join complex_entity as 't4' on cast(t3.OrderId as bigint) = t4.id where t3.CustomerName is not null
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
derived query (`From(query)`), a table-valued function (`From(() => ...)`), or a typed CTE
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
select t1.id as 'A', t2.requiredstring as 'B', t3.id as 'C', t4.requiredstring as 'D'
 from simple_entity as 't1'
 join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
 join simple_entity as 't3' on t2.id = cast(t3.id as bigint)
 join complex_entity as 't4' on cast(t3.id as bigint) = t4.id
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
flat typed chain still stops at eight slots — no further join case is *declared* at arity eight — and a
`Cte<T>` does not change that scope bound; the inherited generic-base joins remain reachable (see
[Overview](#overview)).

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
select t3.OrderId, t3.CustomerName, t4.id as 'Third'
 from (select t1.id as 'OrderId', t2.somestring as 'CustomerName'
 from simple_entity as 't1'
 join complex_entity as 't2' on cast(t1.id as bigint) = t2.id) as 't3'
 join complex_entity as 't4' on cast(t3.OrderId as bigint) = t4.id
```

`As` is declared once on `EntityBuilder<T>` and inherited by every `JoinedEntityBuilder<T1..Tn>`, so
there is no per-arity overload. Because the result is a derived table, `.As(...)` resets the arity
counter, so a further `Join` returns a fresh `JoinedEntityBuilder<TResult, …>` and the flat chain stays
readable — a readability reset, not an escape from a compile-time cap.

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
select t1.id, t2.somestring
 from simple_entity as `t1`
 left any join complex_entity as `t2` on cast(t1.id as bigint) = t2.id
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
...
 from simple_entity as `t1`
 global left any join complex_entity as `t2` on ...
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
select t1.id
 from simple_entity as `t1`
 left semi join complex_entity as `t2` on cast(t1.id as bigint) = t2.id
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
[Provider-specific SQL](provider-specific-sql/overview.md) for the full catalogue.

## Provider differences

| Provider | Positional SQL table aliases | Derived-table alias | Outer joins | APPLY / LATERAL |
|---|---|---|---|---|
| SQLite | `as 't1'` | always emitted (optional in SQLite itself) | left/right/full supported | not supported (`NotSupportedException`) |
| SQL Server | `as [t1]` | required | left/right/full supported | `CROSS APPLY` / `OUTER APPLY` |
| PostgreSQL | `as "t1"` | required | left/right/full supported | `CROSS JOIN LATERAL` / `LEFT JOIN LATERAL ... ON true` (plain tables: `CROSS JOIN` / `LEFT JOIN ... ON true`) |
| MySQL 8.0.14+ | `as \`t1\`` | required | left/right supported (no full) | `CROSS JOIN LATERAL` / `LEFT JOIN LATERAL ... ON true` (plain tables: `CROSS JOIN` / `LEFT JOIN ... ON true`) |
| MariaDB 11.4 | `as \`t1\`` | required | left/right supported (no full) | not supported (`NotSupportedException`, both kinds refused before SQL execution) |
| ClickHouse | `as \`t1\`` | required | left/right/full supported | not supported (`NotSupportedException`) |
| In-memory | not applicable (delegate execution) | not applicable | inner/left/right/full/cross supported; APPLY and table-valued function sources are not | not supported (correlated apply included) |

The in-memory provider compiles the join condition to a delegate and loops, so it does not emit SQL;
it supports [`Inner`](xref:NextORM.Core.JoinType.Inner), [`Left`](xref:NextORM.Core.JoinType.Left), [`Right`](xref:NextORM.Core.JoinType.Right), [`Full`](xref:NextORM.Core.JoinType.Full) and [`Cross`](xref:NextORM.Core.JoinType.Cross) joins (see
`tests/nextorm.core.tests/InMemory/InMemoryJoinTests.cs`). [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions}))/[`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) are SQL-only and throw
`NotSupportedException` on the in-memory provider, like the other unsupported join types. Joins through
`JoinedEntityBuilder<T1..T8>` are resolved at query build time on every provider.

## See also

- [Subqueries](07-subqueries.md) - a joined `QueryCommand<T>` is a derived table.
- [Grouping and aggregates](05-grouping-and-aggregates.md) - aggregate over a join.
- [Query hints](15-query-hints.md) - statement-level hints such as SQL Server `OPTION (RECOMPILE)`.
- [Relationships and single-query loading (`JoinInto`)](../relationships/relationships.md) - declared relationship metadata and the single-query child-collection loader.
- [Per-parent paging in eager loading](../relationships/eager-loading-per-parent-paging.md) - applying a child query's own `Take`/`Skip` independently to each parent.
- [Provider-specific SQL](provider-specific-sql/overview.md) - the full catalogue of provider-only constructs.
- [Querying and projections](../engine/index.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite/Joins/CommonTestSuite.Join.cs:9`,
`tests/nextorm.integration.tests/CommonTestSuite/Joins/CommonTestSuite.Join.cs:226`,
`tests/nextorm.core.tests/InMemory/InMemoryJoinTests.cs:14`,
`tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:320`,
`tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:2282`,
`tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs:379`,
`tests/nextorm.postgres.tests/SqlGeneration/SqlGenerationTests.cs:312`
(and the other provider `SqlGenerationTests.cs`).
