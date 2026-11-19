# Common table expressions (CTE)

> Declare one or more named `with` queries and use them as the `from` source of a query, including
> recursive CTEs for series and hierarchies.

**Prerequisites:** [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Querying and projections](../engine/index.md) · [Set operations](08-set-operations.md)

## Overview

A common table expression (CTE) gives a query a name so another query can use its result as a source.
Use `With(name, query)` for a non-recursive CTE, or `WithRecursive(name, query, maxRecursion)` when
the query refers to itself. Both methods extend [`IDataContext`](xref:NextORM.Core.IDataContext)
through [`DataContextExtensions`](xref:NextORM.Core.DataContextExtensions):

```csharp
public static CteQuery With(this IDataContext dataContext, string name, QueryCommand query);

public static CteQuery WithRecursive(this IDataContext dataContext, string name, QueryCommand query,
    int? maxRecursion = null);
```

Each method returns a [`CteQuery`](xref:NextORM.Core.CteQuery) scope. The scope collects the
declarations in [`Ctes`](xref:NextORM.Core.CteQuery.Ctes). Declarations are immutable: every
[`With`](xref:NextORM.Core.DataContextExtensions.With(NextORM.Core.IDataContext,System.String,NextORM.Core.QueryCommand)) or
[`WithRecursive`](xref:NextORM.Core.DataContextExtensions.WithRecursive(NextORM.Core.IDataContext,System.String,NextORM.Core.QueryCommand,System.Nullable{System.Int32}))
call returns a **new** scope and appends a [`CteDefinition`](xref:NextORM.Core.CteDefinition) to the
previous declarations. Each definition records its name, the
[`QueryCommand`](xref:NextORM.Core.QueryCommand) that produces its rows, and whether its body may refer
to its own name.

Call [`From`](xref:NextORM.Core.CteQuery.From(NextORM.Core.CteDefinition)) with a definition to start a
query over that CTE. The new command carries the declarations with it. `From` returns the regular
[`EntityBuilder<T>`](xref:NextORM.Core.EntityBuilder`1) in entity-free
[`TableAlias`](xref:NextORM.Core.TableAlias) mode, so you can use `Where`, `Join`, `GroupBy`, `Having`,
`OrderBy`, `Limit` and `Select`.

With the string API, read CTE columns by name (`t["id"].AsInt` or `t.GetInt64("id")`). A recursive
body refers to itself the same way, with `dataContext.From("nums")` inside the step query.

SQL syntax varies by dialect. SQLite and PostgreSQL emit `with recursive` when any definition is
recursive. SQL Server uses `with` alone and appends the depth option after the statement.

## Non-recursive CTE

This example names a filtered query `recent`, then reads its rows from the outer query:

```csharp
var recent = dataContext.From<IComplexEntity>()
    .Where(c => c.Id > 1)
    .Select(c => new { c.Id });

var rows = dataContext
    .With("recent", recent)
    .From("recent")
    .Select(t => new { Id = t["id"].AsInt })
    .ToList();
```

```sql
-- SQLite (all providers produce the same shape)
with recent as (select id
 from complex_entity where (id > 1)) select id
 from recent
```

The `Output:` tables below show the rows returned by each example against the integration-test seed data (`tests/nextorm.integration.tests/Providers/TestProviders/SqliteTestProvider.cs`).

Output:

| Id |
|----|
| 2 |
| 3 |

## Typed CTE

The string-based `With` API exposes CTE columns through
[`TableAlias`](xref:NextORM.Core.TableAlias), so you address each column by name. If you still have the
query that defines the CTE, call [`AsCte`](xref:NextORM.Core.QueryCommand`1.AsCte(System.String)) to
create a typed descriptor. `From(Cte<T>)` then reads the CTE through members and infers their types:

```csharp
var recent = dataContext.From<IComplexEntity>()
    .Where(c => c.Id > 1)
    .Select(c => new { c.Id })
    .AsCte("recent");

var rows = dataContext
    .From(recent)
    .Select(r => new { r.Id })
    .ToList();
```

```sql
-- SQLite
with recent as (select id
 from complex_entity where (id > 1)) select id
 from recent as 't1'
```

[`QueryCommand<T>.AsCte`](xref:NextORM.Core.QueryCommand`1.AsCte(System.String)) returns an immutable
[`Cte<TResult>`](xref:NextORM.Core.Cte`1). Its `TResult` is exactly the `Select` projection.
`From(Cte<T>)` returns the regular [`EntityBuilder<TResult>`](xref:NextORM.Core.EntityBuilder`1), so
the full operator set (`Where`, `Join`, `GroupBy`, `OrderBy`, `Limit` and `Select`) remains available.
Use members such as `r.Id` instead of `t["id"]` or `t.GetInt64("id")`.

A CTE is a **typed projection source, not a mapped entity**. Even if `TResult` is a whole entity, the
outer source is not a table. Members resolve to the defining projection's output aliases and carry its
aliases and configured converters/provider types; nextorm doesn't remap the DTO.

The string API remains available alongside the typed API: use `With`, `WithRecursive`, `From(string)` or
`From(CteDefinition)` to declare and read CTEs by name. For a typed recursive CTE, use
`AsRecursiveCte` as described below.

Typed declaration reuses the same machinery, so heterogeneous descriptors and a self-join compose normally.
The same descriptor instance used twice is declared once; different `TResult`s are independent:

```csharp
var recent = dataContext.From<IComplexEntity>()
    .Select(x => new { x.Id, x.String })
    .AsCte("recent");
var other = dataContext.From<ISimpleEntity>()
    .Select(y => new { y.Id })
    .AsCte("other");

var joined = dataContext.From(recent)
    .Join(dataContext.From(other), (r, o) => r.Id == o.Id)
    .Select(p => new { Id = p.Item1.Id, Name = p.Item1.String, Other = p.Item2.Id })
    .ToList();

// self-join: one declaration, two outer aliases
var pairs = dataContext.From(recent)
    .Join(dataContext.From(recent), (a, b) => a.Id == b.Id)
    .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id })
    .ToList();
```

```sql
-- SQLite: heterogeneous descriptors
with recent as (select id as 'Id', somestring as 'String'
 from complex_entity), other as (select id as 'Id'
 from simple_entity) select t1.Id, t1.String, t2.Id
 from recent as 't1'
 join other as 't2' on t1.Id = cast(t2.Id as bigint)

-- SQLite: self-
join of one descriptor -> one declaration, two aliases
with recent as (select id as 'Id', somestring as 'String'
 from complex_entity) select t1.Id, t2.Id
 from recent as 't1'
 join recent as 't2' on t1.Id = t2.Id
```

`From(Cte<T>)` also collects the descriptor's dependencies automatically. It includes every declaration
referenced by the body, directly or through nested CTE bodies, joins, derived subqueries or set-operation
branches. Dependencies come before the CTE that uses them. Unreferenced declarations are omitted.

Using one descriptor more than once adds one declaration. Two different declarations with the same name
are still rejected, as they are with the string API.

You can also pass the descriptor directly to a join when the receiving builder already carries the
context. This avoids converting it with `dataContext.From(cte)` first; see
[Joining a typed CTE directly](#joining-a-typed-cte-directly). The direct form works with every join
operator, including `AsCte` descriptors that project an anonymous type.

Entity filters are **not** injected on the typed source — neither on the main `From(Cte<T>)` source nor on a
joined one; the defining query's own filters stay inside the CTE body.

## Typed recursive CTE

A typed recursive CTE starts with an **anchor** query and uses `AsRecursiveCte` to define its **step**.
The method takes the CTE name and a callback that builds the step from a typed self-reference. A second
overload also takes a required recursion-depth limit:

```csharp
public Cte<TResult> AsRecursiveCte(string name,
    Func<CteReference<TResult>, QueryCommand<TResult>> step);

public Cte<TResult> AsRecursiveCte(string name,
    Func<CteReference<TResult>, QueryCommand<TResult>> step, int maxRecursion);
```

The callback runs **exactly once**, during the `AsRecursiveCte` call. Preparation, SQL generation and
execution don't call it again. Inside the callback,
[`dataContext.From(reference)`](xref:NextORM.Core.CteReference`1) returns the regular
[`EntityBuilder<TResult>`](xref:NextORM.Core.EntityBuilder`1) for reading the CTE. Member access follows
the anchor's projection shape.

nextorm combines the anchor and step as `anchor UNION ALL step` **automatically**. You don't need to
call `UnionAll` or declare the CTE a second time.

The self-reference belongs to the callback that receives it. A
[`CteReference<TResult>`](xref:NextORM.Core.CteReference`1) is valid only while that callback runs.
Using it outside the callback—including a captured or foreign reference with the same name—or using a
self-reference in the anchor throws `InvalidOperationException` **before nextorm builds a database
command**.

Recursive CTEs can't reference one another. Each recursive CTE can refer only to its own self-reference.

```csharp
// Anchor: id 1. Step: add 1 while the value is below 5 -> 1, 2, 3, 4, 5.
var numbers = dataContext.From<ISimpleEntity>()
    .Where(s => s.Id == 1)
    .Select(s => s.Id)
    .AsRecursiveCte("nums", self => dataContext.From(self)
        .Where(n => n < 5)
        .Select(n => n + 1));

var rows = dataContext.From(numbers).ToList();
```

```sql
-- SQLite
with recursive nums as (select id as 'Id'
 from simple_entity
 where id = 1
 union all
 select (Id + 1) as 'c0'
 from nums as 't1'
 where (t1.Id < 5)) select id
 from nums as 't1'
```

The same surface covers an anonymous projection with several slots:

```csharp
var numbers = dataContext.From<ISimpleEntity>()
    .Where(s => s.Id == 1)
    .Select(s => new { s.Id, Next = s.Id + 1 })
    .AsRecursiveCte("nums", self => dataContext.From(self)
        .Where(n => n.Id < 4)
        .Select(n => new { Id = n.Id + 1, Next = n.Next + 1 }));

var rows = dataContext.From(numbers)
    .Select(n => new { n.Id, n.Next })
    .ToList();
```

```sql
-- SQLite
with recursive nums as (select id as 'Id', (id + 1) as 'Next'
 from simple_entity
 where id = 1
 union all
 select (id + 1) as 'Id', (Next + 1) as 'Next'
 from nums as 't1'
 where (t1.Id < 4)) select Id, Next
 from nums as 't1'
```

... and a DTO projection, for example the member-init `CteNumberRow` used by the string API below:

```csharp
public sealed class CteNumberRow
{
    public int n { get; set; }
}

var numbers = dataContext.From<ISimpleEntity>()
    .Where(s => s.Id == 1)
    .Select(s => new CteNumberRow { n = s.Id })
    .AsRecursiveCte("nums", self => dataContext.From(self)
        .Where(n => n.n < 5)
        .Select(n => new CteNumberRow { n = n.n + 1 }));

var rows = dataContext.From(numbers)
    .Select(n => new CteNumberRow { n = n.n })
    .ToList();
```

```sql
-- SQLite
with recursive nums as (select id as 'n'
 from simple_entity
 where id = 1
 union all
 select (n + 1) as 'n'
 from nums as 't1'
 where (t1.n < 5)) select n
 from nums as 't1'
```

### The anchor defines the shape

The **anchor's** projection defines the recursive CTE's readable columns, their order and aliases. The
step must reproduce the same shape slot by slot; its aliases don't rename the CTE's columns.

Before sending SQL to the database, each preparation validates every leaf in the step against the
anchor. It throws `InvalidOperationException` if any of these differ:

* the column **count**;
* the self-reference **member slot** at that position (a bare member read must name the anchor's column);
* the declared **CLR type**;
* the bound **provider type** (the converter-bound type, or the prepared provider type);
* the **nullability** (whether the CLR type is a `Nullable<T>`).

The message names the CTE and the step position, for example:

```text
The recursive common table expression 'reordered' is invalid at step position 0: the step reads the
self-reference member 'Total' at position 0, but the anchor names that column 'Id'.
```

nextorm repeats this validation on every preparation, including a plan-cache hit. A cached plan can't
bypass the anchor/step contract.

### Recursion depth (`maxRecursion`)

The two-argument overload emits **no** depth hint. SQL Server then uses its default (`100`). The
three-argument overload requires an `int` and emits `option (maxrecursion n)` only on SQL Server. A
value of `0` means "no limit"; a positive value sets the maximum recursion depth. Other providers
ignore the hint and use their own defaults, as they do with the string API.

When nextorm hoists a recursive CTE into another statement—`INSERT … SELECT`, `MERGE`, `CTAS` /
`SELECT … INTO`, or a batch step—the explicit limit travels with it. SQL Server appends
`option (maxrecursion n)` at the end of the whole statement, before the `MERGE` terminator. This
preserves an explicit `maxRecursion` instead of falling back to the server default. Providers without
this option are unaffected and emit byte-identical SQL.

```csharp
var numbers = dataContext.From<ISimpleEntity>()
    .Where(s => s.Id == 1)
    .Select(s => s.Id)
    .AsRecursiveCte("nums", self => dataContext.From(self)
        .Where(n => n < 5)
        .Select(n => n + 1), 100);

var rows = dataContext.From(numbers).ToList();
```

```sql
-- SQL Server: no `recursive` keyword, depth option appended
with nums as (select id as [Id]
 from simple_entity
 where id = 1
 union all
 select (Id + 1) as [c0]
 from nums as [t1]
 where (t1.Id < 5)) select id
 from nums as [t1] option (maxrecursion 100)
```

### Provider differences (typed recursion)

| Provider | Typed recursive CTE |
|---|---|
| SQLite | Supported; declares `with recursive`; `maxRecursion` ignored. |
| PostgreSQL | Supported; declares `with recursive`; `maxRecursion` ignored. |
| SQL Server | Supported; declares `with` (no `recursive` keyword); two-argument overload emits no hint (server default), three-argument overload emits `option (maxrecursion n)` (`0` = no limit, positive = depth). |
| MySQL | Supported; declares `with recursive`; `maxRecursion` ignored. |
| MariaDB | Supported; declares `with recursive`; `maxRecursion` ignored. |
| ClickHouse | **Excluded**: a typed recursive CTE throws `NotSupportedException` before any SQL is emitted. An ordinary (non-recursive) typed CTE still works on ClickHouse. |
| In-memory | Not applicable: recursion is rendered by the SQL dialects. |

The string `WithRecursive` is **not** gated by this capability check and works on every provider, with one
SQL Server restriction described below.

### SQL Server: recursive CTE must use `UNION ALL`

A `WithRecursive` declaration whose body is a distinct `UNION` (`anchor.Union(step)`) is not a valid
SQL Server recursive CTE — the server answers *"does not contain a top-level UNION ALL operator"*. nextorm
rejects it locally while the query is prepared, **before** it is executed and before it is placed in the plan
cache, with a `NotSupportedException` naming SQL Server and both supported forms:

```csharp
// Rejected on SQL Server: distinct UNION in a recursive body.
var body = anchor.Union(step);

// Supported alternatives:
var body = anchor.UnionAll(step);                        // string API, explicit UNION ALL
var nums = anchor.AsRecursiveCte("nums", self => ...);   // typed API, always UNION ALL
```

PostgreSQL, SQLite, MySQL and MariaDB keep accepting a distinct `UNION` in a recursive CTE and emit it
byte-for-byte.

Every example above is bounded by a terminating predicate in the step; never build an unbounded recursive
CTE.

## Chained declarations

Each [`With`](xref:NextORM.Core.DataContextExtensions.With(NextORM.Core.IDataContext,System.String,NextORM.Core.QueryCommand))
adds a declaration to the previous scope. This lets a later CTE use an earlier one. nextorm renders
the declarations in declaration order:

```csharp
var first = dataContext.From<IComplexEntity>()
    .Where(x => x.Id > 1)
    .Select(x => new { x.Id });

var second = dataContext.From("first")
    .Select(t => new { id = t["id"].AsInt });

var rows = dataContext
    .With("first", first)
    .With("second", second)
    .From("second")
    .Select(t => new { id = t["id"].AsInt })
    .ToList();
```

```sql
with first as (select id
 from complex_entity where (id > 1)), second as (select id
 from first) select id
 from second
```

> **Nested CTEs are hoisted automatically.** If a CTE body carries its own `WITH`—for example, a query
> built with `dataContext.With(...).From(...)` and passed to another `With`—nextorm flattens both into a
> single top-level `with` clause.
>
> The hoister places each dependency before the CTE that uses it, even if the dependency is a later
> sibling declaration. It preserves the relative order of declarations that don't depend on each
> other. Reusing the same `CteDefinition` instance adds it only once. Two different instances with the
> same name cause `InvalidOperationException`, because one `WITH` clause can't bind one name to two
> definitions.
>
> Every provider then sees the flat, portable form. In particular, SQL Server always emits a plain
> `with name as (...)` list — never `with recursive`; T-SQL declares a recursive CTE with `with` alone.
>
> A declaration that cannot be moved to the top-level `WITH` — one nested inside a derived-table subquery,
> a correlated reference or a set-operation branch — fails fast with an `InvalidOperationException` before
> any SQL is rendered, rather than emitting the non-portable nested form.

`From(CteDefinition)` is equivalent to `From(definition.Name)` and is convenient when you kept the scope
instead of the name:

```csharp
var cte = dataContext.With("recent", recent);
var rows = cte.From(cte.Ctes[0])
    .Select(t => new { id = t["id"].AsInt })
    .ToList();
```

## Composing over a CTE

The CTE source uses the regular generic builder, so you can filter, aggregate, sort, page and join it
as you would a table. A later CTE can also aggregate an earlier one by name:

```csharp
var first = dataContext.From<IComplexEntity>()
    .Select(x => new { x.Id, name = x.String });

var second = dataContext.From("first")
    .GroupBy(t => new { name = t.GetString("name") })
    .Select(t => new { name = t.GetString("name"), count = SqlFunctions.Sql.count() });

var rows = dataContext
    .With("first", first)
    .With("second", second)
    .From("second")
    .OrderByDescending(t => t.GetInt32("count"))
    .Select(t => new { name = t.GetString("name"), count = t.GetInt32("count") })
    .ToList();
```

```sql
-- SQLite
with first as (select id, somestring as 'name'
 from complex_entity),
     second as (select name, count(*) as 'count'
 from first group by name)
select name, count
 from second order by count desc
```

You can join a CTE to another CTE or to a table. With the string API, address both sides by name.
nextorm qualifies each `TableAlias` column with its table alias, so a column shared by both CTEs stays
unambiguous:

```csharp
var left = dataContext.From<IComplexEntity>().Select(x => new { x.Id });
var right = dataContext.From<ISimpleEntity>().Select(x => new { x.Id });

var rows = dataContext
    .With("l", left)
    .With("r", right)
    .From("l")
    .Join(dataContext.From("r"), (l, r) => l.GetInt64("id") == r.GetInt64("id"))
    .Select(p => new { Id = p.Item1.GetInt64("id"), Other = p.Item2.GetInt64("id") })
    .ToList();
```

```sql
-- SQLite
with l as (select id
 from complex_entity), r as (select id
 from simple_entity) select t1.id, t2.id
 from l as 't1'
 join r as 't2' on t1.id = t2.id
```

> The CTE source has no mapped entity, so its columns are read by name (`t.GetInt64("id")` /
> `t["id"].AsInt`) and the names must match the CTE body's output aliases. Name the projection members
> after the SQL aliases (lower-case `snake_case`) so the outer references stay exact.

### Joining a typed CTE directly

Once you have a descriptor, pass it directly to any join operator:
`Join`, `LeftJoin`, `RightJoin`, `FullJoin`, `CrossJoin`, `CrossApply` or `OuterApply`. The operator
converts the [`Cte<T>`](xref:NextORM.Core.Cte`1) with the receiving builder's context, then uses the
existing [`EntityBuilder<T>`](xref:NextORM.Core.EntityBuilder`1) overload. This produces the same SQL,
parameters and materialized shape as converting the descriptor first:

```csharp
var left = dataContext.From<IComplexEntity>()
    .Where(c => c.Id > 1)
    .Select(c => new { c.Id })
    .AsCte("l");
var right = dataContext.From<ISimpleEntity>()
    .Select(s => new { s.Id })
    .AsCte("r");

// direct: the descriptor is converted with this context
var rows = dataContext.From(left)
    .Join(right, (l, r) => l.Id == r.Id)
    .Select(p => new { Id = p.Item1.Id, Other = p.Item2.Id })
    .ToList();

// equivalent explicit form
var converted = dataContext.From(left)
    .Join(dataContext.From(right), (l, r) => l.Id == r.Id)
    .Select(p => new { Id = p.Item1.Id, Other = p.Item2.Id })
    .ToList();
```

```sql
-- SQLite: both forms produce this
with l as (select id as 'Id'
 from complex_entity where (id > 1)), r as (select id as 'Id'
 from simple_entity) select t1.Id, t2.Id
 from l as 't1'
 join r as 't2' on t1.Id = cast(t2.Id as bigint)
```

#### Choose a join overload

The three conditionless operators (`CrossJoin`, `CrossApply` and `OuterApply`) take only the descriptor.
The other four take a predicate and may also take a trailing `Action<JoinOptions>`. The options
overloads have **no** default value, so omit the options argument to select the shorter overload:

```csharp
var rows = dataContext.From(left)
    .Join(right, (l, r) => l.Id == r.Id, o => o.SuppressCartesianWarning());
```

A direct descriptor behaves like a converted one in other respects too. Dependencies are hoisted in
dependency-before-consumer order; repeated use of one instance adds one declaration; a different
descriptor with a duplicate name is still rejected. Entity filters stay in the defining body, and
repeated calls don't change the shared command or its cache flag.

On [`JoinedEntityBuilder<T1..Tn>`](xref:NextORM.Core.JoinedEntityBuilder`2), these overloads exist for
receiver arities 2–7, so you can join a CTE at any continuation. The flat typed projection still stops
at eight slots: no further join case is declared at arity eight. The inherited generic-base joins
remain available.

#### Use a CTE in a joined mutation

A CTE scope can also drive a multi-table `UPDATE` or `DELETE`. Put the CTE on the **joined** side and
keep the physical table as the target. nextorm hoists the declaration before the mutation:

```csharp
var recent = dataContext
    .With("recent", dataContext.From<IOrder>().Where(o => o.Id > 1000).Select(o => new { o.Id }));

dataContext.From<IOrder>()
    .Join(recent.From("recent"), (o, r) => o.Id == r.GetInt64("id"))
    .CreateUpdateJoinBuilder()
    .Set(p => p.Item1.Status, "archived")
    .Update();
```

```sql
-- PostgreSQL
with recent as (select id
 from orders where (id > 1000)) update orders as "t1" set status = @p0 from recent as "t2" where t1.id = t2.id
```

This works on providers that support `UPDATE ... FROM`/`JOIN` or `DELETE ... USING`/join, at any join
position, and with recursive CTEs. See
[Data modification (UPDATE)](19-update-statement.md#updating-from-a-join) and
[Data modification (DELETE)](18-delete-statement.md#delete-based-on-a-join).

## Recursive CTE: a number series

A recursive CTE builds a result by combining an **anchor** (a non-recursive query) with a **step**.
The step reads the CTE by name and continues until its predicate no longer matches. Combine the two
queries with `UnionAll`, then pass that body to
[`WithRecursive`](xref:NextORM.Core.DataContextExtensions.WithRecursive(NextORM.Core.IDataContext,System.String,NextORM.Core.QueryCommand,System.Nullable{System.Int32})):

```csharp
public sealed class CteNumberRow
{
    public int n { get; set; }
}

var anchor = dataContext.From<ISimpleEntity>()
    .Where(s => s.Id == 1)
    .Select(s => new CteNumberRow { n = s.Id });

var step = dataContext.From("nums")
    .Where(t => t["n"].AsInt < 5)
    .Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });

var body = anchor.UnionAll(step);

var numbers = dataContext
    .WithRecursive("nums", body)
    .From("nums")
    .Select(t => new CteNumberRow { n = t["n"].AsInt })
    .ToList();

// numbers -> 1, 2, 3, 4, 5
```

```sql
-- SQLite: `with recursive` prefix
with recursive nums as (select id as 'n'
 from simple_entity where id = 1 union all select (n + 1) as 'n'
 from nums where (n < 5)) select n
 from nums
```

Output:

| n |
|---|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |

`maxRecursion` optionally sets a recursion-depth limit. SQL Server is the only provider in this
example with a statement-level option; its dialect appends `option (maxrecursion n)` to the statement.
SQLite and PostgreSQL ignore the option and use their own defaults:

```csharp
var numbers = dataContext
    .WithRecursive("nums", body, maxRecursion: 100)
    .From("nums")
    .Select(t => new CteNumberRow { n = t["n"].AsInt })
    .ToList();
```

```sql
-- SQL Server: no `recursive` keyword, depth option appended
with nums as (select id as [n]
 from simple_entity where id = 1 union all select (n + 1) as [n]
 from nums where (n < 5)) select n
 from nums option (maxrecursion 100)
```

## CTE plus a captured parameter

A captured value in the CTE body becomes a parameter exactly like anywhere else, and the
parameter-extraction pass walks the `with` clause, not only the outer statement:

```csharp
var threshold = 1L;

var prepared = dataContext
    .With("recent", dataContext.From<IComplexEntity>()
        .Where(x => x.Id > threshold)
        .Select(x => new { x.Id }))
    .From("recent")
    .Select(t => new { id = t["id"].AsInt })
    .Prepare();

var ids = prepared.ToList(dataContext);
```

```sql
-- SQLite parameter placeholder; SQL Server/PostgreSQL use @threshold
with recent as (select id
 from complex_entity where (id > $threshold)) select id
 from recent
```

## Plan-cache reuse

The plan-cache key includes CTE definitions. Two queries with different CTE bodies do **not** share a
cached plan. Freshly built chains with the same structure do reuse a plan, including a recursive CTE
whose body is a `union all` of two fresh commands:

* a cached CTE plan re-extracts the CTE's captured parameter on a cache hit
  (`PlanCacheTests.Cte_WithCapturedParam_RepeatedExecution_ShouldRefreshParam`);
* two different CTE bodies produce two plans
  (`PlanCacheTests.Cte_DifferentDefinitions_ShouldNotSharePlan`);
* an equivalent recursive CTE built again reuses the cached plan
  (`PlanCacheTests.RecursiveCte_FreshCommand_ShouldReuseCachedPlan`).

See [Query reuse: cache vs Prepare](../engine/03-plan-cache-and-reuse.md) for the lifetime and invalidation rules of the
plan cache.

## Data-modifying CTE (PostgreSQL)

PostgreSQL is the only supported provider that accepts a data-modifying statement as a CTE body:
`WITH <name> AS (<INSERT|UPDATE|DELETE> ... RETURNING ...)`. This capability is exposed through
[`SupportsDataModifyingCtes`](xref:NextORM.Core.ISqlDialect.SupportsDataModifyingCtes).

Use the `With(name, mutation)` overloads on `IDataContext` or `CteQuery` to pass a row-returning
`INSERT`, `UPDATE` or `DELETE`. They return a
[`MutationCteQuery<TResult>`](xref:NextORM.Core.MutationCteQuery`1) typed from the `RETURNING`
projection. A `RETURNING` projection is required; a body that only has side effects isn't supported.

PostgreSQL accepts single-table mutations and joined mutations using INNER joins for arities 2–8. Build
a multi-table `UPDATE ... FROM` through
[`UpdateJoinBuilder<TProjection>.Returning()`](xref:NextORM.Core.UpdateJoinBuilder`1.Returning) or
[`Returning(projection)`](xref:NextORM.Core.UpdateJoinBuilder`1.Returning``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})),
and build a multi-table `DELETE ... USING` through the joined
builder's [`CreateDeleteJoinBuilder()`](xref:NextORM.Core.DataContextExtensions.CreateDeleteJoinBuilder``2(NextORM.Core.JoinedEntityBuilder{``0,``1})),
which returns a [`DeleteJoinBuilder<TProjection>`](xref:NextORM.Core.DeleteJoinBuilder`1) whose
[`Returning()`](xref:NextORM.Core.DeleteJoinBuilder`1.Returning)/[`Returning(projection)`](xref:NextORM.Core.DeleteJoinBuilder`1.Returning``1(System.Linq.Expressions.Expression{System.Func{`0,``0}}))
to return rows. `Returning()` is equivalent to `Returning(p => p)` and returns the whole joined
projection; the explicit projection form works as before.

Every other provider rejects `With(name, mutation)` with `NotSupportedException` because its CTE body
must be a `SELECT`. Unlike a read CTE, a statement with a data-modifying CTE is never stored in the
plan cache because it has side effects. nextorm plans it again on every call.

```csharp
// Single-table UPDATE body: update, then read the updated rows typed.
var updated = dataContext
    .With("upd", dataContext.CreateUpdateBuilder<IOrder>()
        .Set(x => x.Total, 0)
        .Where(x => x.CustomerId == 7)
        .Returning(x => new { x.Id, x.Total }))
    .From("upd")
    .Select(r => new { r.Id, r.Total })
    .ToList();
```

```sql
-- PostgreSQL
with upd as (update orders set total = @p0 where customer_id = 7 returning id, total) select id, total
 from upd as "t1"
```

```csharp
// Join/multi-table DELETE body: delete the target rows matched by the join, returning columns from both sides.
var doomed = dataContext
    .From<IOrder>()
    .Join<ICustomer>((o, c) => o.CustomerId == c.Id)
    .CreateDeleteJoinBuilder()
    .Returning(p => new { OrderId = p.Item1.Id, CustomerName = p.Item2.Name });

var removed = dataContext
    .With("del", doomed)
    .From("del")
    .Select(r => new { r.OrderId, r.CustomerName })
    .ToList();
```

```sql
-- PostgreSQL
with del as (delete from orders as "t1" using customers as "t2" where t1.customer_id = t2.id returning t1.id as "OrderId", t2.name as "CustomerName") select "OrderId", "CustomerName"
 from del as "t1"
```

```csharp
// Whole-projection body: .Returning() returns every slot, and the read side addresses them by ItemN.
var doomed = dataContext
    .From<IOrder>()
    .Join<ICustomer>((o, c) => o.CustomerId == c.Id)
    .CreateDeleteJoinBuilder()
    .Returning();   // equivalent to .Returning(p => p)

var removed = dataContext
    .With("del", doomed)
    .From("del")
    .Select(r => new { OrderId = r.Item1.Id, CustomerName = r.Item2.Name })
    .ToList();
```

The whole-projection body emits **every** returnable column from both slots under deterministic
`__sN_*` aliases (`__s1_*` for the target and `__s2_*` for the joined side). The read side must use the
stored alias rather than the CLR member name:

```sql
-- PostgreSQL: read side of the whole-projection body above
select "__s1_id", "__s2_name"
 from del as "t1"
```

The write CTE is documented together with the write surface it belongs to — typed read-back via
`From`/`FromTable`, a `VALUES` or `INSERT ... SELECT` body, reading an earlier read CTE, and feeding a main
`INSERT ... SELECT` — in
[Data modification (INSERT): Data-modifying CTE](17-insert-statement.md#data-modifying-cte-postgresql).
For the general `UPDATE` surface see [Data modification (UPDATE)](19-update-statement.md), and for `DELETE`
[Data modification (DELETE)](18-delete-statement.md).

## Provider differences

| Provider | Behaviour |
|---|---|
| SQLite | `with` for non-recursive, `with recursive` for recursive; no depth option. |
| SQL Server | Recursive CTEs are declared with `with` alone (no `recursive` keyword); `maxRecursion` renders `option (maxrecursion n)` at the end of the statement. |
| PostgreSQL | `with` / `with recursive`; no depth option. |
| MySQL | `with` for non-recursive, `with recursive` for recursive; no depth option. |
| MariaDB | `with` / `with recursive`; no depth option. |
| ClickHouse | Every CTE is declared with plain `with`; recursive CTEs are not supported. |
| In-memory | Not applicable: CTEs are rendered by the SQL dialects and are not part of the in-memory provider. |

## See also

* [Set operations](08-set-operations.md) - [`UnionAll`](xref:NextORM.Core.QueryCommand`1.UnionAll``1(NextORM.Core.QueryCommand{``0})) and friends, used to build a recursive body.
* [Joins](04-joins.md) - joining a CTE to a table, as in `CommonTestSuite.Cte.cs`.
* [Raw SQL](14-raw-sql.md) - when the whole statement is hand-written.
* [Query reuse: cache vs Prepare](../engine/03-plan-cache-and-reuse.md) - how CTE plans are cached.

---

Source: `src/nextorm.core/Builders/Cte/CteQuery.cs:7`, `src/nextorm.core/DataContext/DataContextExtensions.cs:9`;
`tests/nextorm.integration.tests/CommonTestSuite/Cte/CommonTestSuite.Cte.cs:14`, `tests/nextorm.integration.tests/CommonTestSuite/Cte/CommonTestSuite.Cte.cs:33`;
`tests/nextorm.core.tests/Cte/CteQueryTests.cs:8`;
typed recursive CTE: `src/nextorm.core/Cte.cs:15`, `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs:1100`,
`src/nextorm.core/DataContext/Sql/SqlSourceRenderer.cs:35`, `src/nextorm.core/Query/Commands/QueryCommand.QueryPreparer.cs:981`;
`tests/nextorm.core.tests/Cte/TypedCteTests.cs:1057`, `tests/nextorm.sqlite.tests/Cte/TypedCteTests.cs:718`;
`tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs:190`, `tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs:343`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:1188`, `:1202`, `:1216`, `:1231`, `:1248`;
`tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs:827`, `:856`;
`tests/nextorm.postgres.tests/SqlGeneration/SqlGenerationTests.cs:759`, `:788`.
