# Implicit navigation queries

> A **declared** relationship turns a navigation member into a query construct: a reference member can be
> used as a scalar chain or a presence check, and a collection member exposes exactly four direct
> terminals (`Any`, `Count`, `LongCount` and the `Count` property). nextorm expands the navigation on the
> server as a correlated subquery or a `LEFT JOIN`; it never reads the CLR object graph and never loads a
> collection lazily. `Count`/`Count()` materialise as a checked `int` (a 64-bit count narrowed only at
> materialisation), while `LongCount()` stays 64-bit end to end.

**Prerequisites:** [Relationships and single-query loading (`JoinInto`)](../advanced/relationships.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Joins](02-joins.md)

## Overview

A navigation query is available only when the relationship is **declared** in the mapping metadata:
`HasMany`/`HasOne`/`HasOneToOne`/`HasManyThrough` or the [`[Relationship]`](../advanced/relationships.md)
attribute. Conventions over foreign-key names are **not** used and a member that is not part of a declared
relationship is never treated as a navigation.

With the relationship declared, two navigation shapes are supported:

* a **reference** navigation (`c.Parent`) — scalar member chains, presence checks and whole-reference
  projection;
* a **collection** navigation (`p.Children`) — the four direct terminals below, plus the
  `AsEntityBuilder<T>` adapter.

Everything is metadata-driven: on the in-memory provider the registered datasets and relationship
metadata are authoritative, so the result matches the SQL providers even when the CLR object graph is
empty or inconsistent.

## Reference navigation

### Scalar chains

A member chain through a reference navigation is translated to the related source. nextorm emits a
`LEFT JOIN` for the path and reads the member from the joined alias:

```csharp
var rows = ctx.From<Child>()
    .Where(c => c.Id == id)
    .Select(c => new { c.Id, ParentName = c.Parent!.Name })
    .ToList();

// select c.Id, p.Name from child as c left join parent as p on c.ParentId = p.Id
```

The reference expansion is always a **`LEFT JOIN`** — a relationship carries no mandatory flag, and an
`INNER` join would silently drop a child whose foreign key dangles. A dangling foreign key therefore
produces `NULL` and `c.Parent!.Name` materialises as `null`.

A chain may cross more than one reference hop (`c.Parent!.Parent!.Name`): nextorm emits one `LEFT JOIN`
per hop and the absence propagates at every hop, so a missing intermediate parent stays absent instead of
re-appearing through a default key. Self-references (a self-referencing entity's `n.Parent!.Name`) and two
reference paths that happen to share one CLR type (`o.Buyer`, `o.Seller`) are supported too: each declared
path (root + member path + lexical scope) binds its own alias and paths are never merged by CLR type.
Reusing the same path in `Where`, `Select` and `OrderBy` still joins it once.

### Presence checks

`c.Parent == null` / `c.Parent != null` test whether the related row actually exists, using the
non-nullable identifying principal key of the relationship — not merely the foreign-key value. A child
whose foreign key points at a missing parent is treated as **absent**:

```csharp
var withParent = ctx.From<Child>().Where(c => c.Parent != null).ToList();
var withoutParent = ctx.From<Child>().Where(c => c.Parent == null).ToList();
```

A presence check may span more than one reference hop (`c.Parent!.Parent != null`,
`c.Parent!.Parent == null`): an absent row at the **first**, an intermediate or the final hop makes the
whole chain read as absent, and `==`/`!=` stay mutually consistent. The same holds on the in-memory
provider, where the chain is walked over the registered datasets rather than the CLR graph.

### Whole-reference projection

Projecting the reference itself yields `null` for an absent row and a fully materialised entity for a
present one on every provider, including in-memory:

```csharp
var rows = ctx.From<Child>()
    .Select(c => new { c.Id, Parent = c.Parent })
    .ToList();

var present = rows[0].Parent;   // the principal entity, or
var absent = rows[1].Parent;    // null when the foreign key dangles
```

The in-memory provider resolves the entity (or `null`) from the mapped foreign/principal keys against the
registered dataset — it never reads the receiver's populated CLR navigation graph, so an inconsistent graph
still yields the FK-correct entity. Projecting a collection reached through an absent reference yields an
empty collection, not a failure.

### Null contract

A nullable scalar derived from a reference follows the SQL null semantics of the `LEFT JOIN`:

```csharp
var rows = ctx.From<Child>()
    .Select(c => new
    {
        c.Id,
        Age = (int?)c.Parent!.Age,          // SQL NULL when the parent is absent
        AgeOrDefault = (int?)c.Parent!.Age ?? 0,
    })
    .ToList();
```

An **unlifted** non-nullable scalar (`c.Parent!.Age` typed as `int`) whose parent can be absent is
rejected during preparation with [`QueryPreparationException`](xref:NextORM.Core.QueryPreparationException),
naming the navigation path and the result type, instead of silently reading `default(int)`. Lift the member
to a nullable type or supply an explicit fallback (`?? 0`).

Null compensation is consistent across predicates and bool/value projections, including the inverted forms:
`c.Parent!.Name != "x"`, its negation `!(c.Parent!.Name == "x")`, a nullable operand such as
`c.Parent!.Score == null`, and a disjunction that also admits rows without a parent
(`c.IsActive || c.Parent!.Name == "x"`). A child whose foreign key dangles takes part with SQL `NULL`
semantics — it is **included** in `!=`/negated/`OR` comparisons — rather than being silently dropped or
compared against a default value.

## Collection navigation

A declared collection supports exactly **four** parameterless terminals. They lower to a correlated
subquery over the related source — never to an `OUTER` join that would multiply the parent rows:

| Form | Result | Lowering |
|---|---|---|
| `p.Children.Any()` | `bool` | correlated `EXISTS` |
| `p.Children.Count()` | `int` | correlated 64-bit `COUNT_BIG`; checked narrowing to `int` only at materialisation |
| `p.Children.LongCount()` | `long` | correlated 64-bit `COUNT_BIG` end to end (no narrowing) |
| `p.Children.Count` (property) | `int` | same as `Count()` |

```csharp
var rows = ctx.From<Parent>()
    .Select(p => new
    {
        p.Id,
        HasChildren = p.Children.Any(),
        ChildCount = p.Children.Count(),
        ChildCount64 = p.Children.LongCount(),
        ChildCountProperty = p.Children.Count,
    })
    .ToList();
```

An empty collection yields `false`/`0`/`0`/`0`. `Count()` and the `Count` property are evaluated at
**64-bit inside the database**: SQL emits the wide `COUNT_BIG` count with no narrowing cast, so a
**predicate**, a **boolean projection** or **arithmetic** over the count compares/combines the true wide
value and is always exact — it never truncates or wraps. The checked narrowing to `int` happens **only
where an `int` is materialised** — a scalar projection or an `int`-typed projection member — and throws an
un-wrapped `OverflowException` on **every** provider when the count
exceeds `Int32.MaxValue`. `LongCount()` returns a true 64-bit count end to end (no narrowing). The
in-memory provider follows the same boundary: `Count()` and the `Count` property match `Enumerable.Count`
and throw `OverflowException` for every consumer, while `LongCount()` stays 64-bit.

### Cardinality and duplicates

The direct terminals report the cardinality of the **declared relation**:

* a one-to-many collection counts the matching child rows;
* a many-to-many collection counts the matching **junction rows whose mapped child exists** — a junction
  row whose child is missing (**dangling link**) is excluded, so it is not counted and does not make the
  collection non-empty;
* duplicate junction rows that reference the same existing child each count — they are not deduplicated
  and no implicit `DISTINCT` is applied;
* a many-to-many collection whose junction has only dangling links yields `false`/`0`/`0`/`0`.

## Adapter: `AsEntityBuilder<T>`

The **collection** navigation can be adapted to a full [`EntityBuilder<T>`](xref:NextORM.Core.EntityBuilder`1)
with `AsEntityBuilder<T>`, then used with the same direct terminals:

```csharp
ctx.From<Parent>()
    .Select(p => new
    {
        p.Id,
        Has = p.Children.AsEntityBuilder().Any(),          // T inferred from ICollection<Child>
        Count = p.Children.AsEntityBuilder<Child>().Count(),
    });
```

The adapter is a marker: it must appear **inside a query expression tree**, where the translator resolves
the receiver to the declared navigation path. Calling it as an ordinary method — outside an expression
tree, including on a captured collection — always throws
[`NotSupportedException`](xref:System.NotSupportedException); it never returns a null or default marker.
A captured `IEnumerable` is an ordinary in-memory collection, not a navigation.

The public surface also provides a **reference** overload, `AsEntityBuilder<T>(object?)`, which must be
called with an explicit generic argument so that it never steals collection inference
(`c.Parent!.AsEntityBuilder<Parent>()`). It supplies reference semantics on SQL and in-memory:
`Any()` tests presence, and `Count()`/`LongCount()` return `1` when the reference is present and `0` when
it is absent — exactly like `c.Parent != null`. Projecting the adapted **marker itself** (rather than one
of those terminals) and composing LINQ operators after the adapter remain unsupported and fail closed.

## Providers

| Provider | Reference chains / presence | Whole-reference | Collection terminals | In-memory |
|---|---|---|---|---|
| SQLite | yes | yes | yes | yes |
| PostgreSQL | yes | yes | yes | yes |
| SQL Server | yes | yes | yes | yes |
| MySQL / MariaDB | yes | yes | yes | yes |
| ClickHouse | yes | yes | yes | yes |
| In-memory | yes | yes | yes | — |

On ClickHouse a reference-navigation query needs real SQL `NULL`s on the unmatched side of the outer
join. nextorm injects `join_use_nulls = 1` **query-locally** for a reference-expansion query only — never
as a session or global setting — and rejects an explicit conflicting `join_use_nulls = 0` before
execution.

A collection reached **through a reference** (`child.Parent.Children`) is not available on ClickHouse.
This is an **accepted provider limitation** (approved), not a defect: the engine cannot correlate a
subquery on a joined source, so nextorm rejects the shape with an explicit
[`NotSupportedException`](xref:System.NotSupportedException) (*"cannot correlate a subquery on a joined
source"*) instead of emitting SQL the engine fails on. Query the collection from its own source or use an
explicit join.

On the in-memory provider the registered datasets and the declared metadata are authoritative: a populated
navigation property is never read, and the result matches the SQL providers even for an empty or
inconsistent CLR graph. If a declared navigation's related type has no registered dataset/mapping,
preparation throws [`BuildSqlCommandException`](xref:NextORM.Core.BuildSqlCommandException) naming the table
(`Table name is not registered`) — the ordinary missing-source diagnostic, never a silent empty/`null`
result.

The generated SQL, the plan key and the returned rows are identical across a cold plan, a warm cache hit
and an uncached (prepared) run. A navigation query does not disable the shared `.Any()`/`.Count()` plan
cache.

## Limitations and rejected forms

Implicit navigation is a deliberately narrow surface. The following are rejected with
[`NotSupportedException`](xref:System.NotSupportedException) (or, for the unlifted scalar, a
[`QueryPreparationException`](xref:NextORM.Core.QueryPreparationException)) and never fall back to
client-side evaluation:

* **Predicate overloads** of `Any`/`Count`/`LongCount` (`p.Children.Any(c => ...)`,
  `p.Children.Count(c => ...)`, `p.Children.LongCount(c => ...)`).
* **Direct LINQ composition** on a collection navigation — `Where`, `OrderBy`/`OrderByDescending`,
  `Select`, `Skip`, `Take`, `Distinct`, `GroupBy`, `GroupJoin`, `SelectMany`, joins, set operations
  (`Union`/`Except`/`Intersect`) and materialisation (`ToArray`/`ToList`).
* **Extra terminals** — `First`/`First(predicate)`, `Single`/`SingleOrDefault`, `Last`/`LastOrDefault`,
  `Contains`, `Sum`, `Average` and other aggregates.
* **Captured or outside markers** — a captured enumerable is not a navigation, and `AsEntityBuilder`
  outside a query expression always throws.
* **Undeclared navigations** — a reference- or collection-typed member with no declared relationship is
  not inferred from a convention.
* **Composite relationship keys** — only single-column foreign/principal/junction keys are supported.
* **Navigation in grouping/paging modifier keys** — `GROUP BY`, `DISTINCT ON`, `LIMIT BY` and
  extreme-row keys do not evaluate navigation (use explicit joins there).
* **Temp-table / table-valued (TVP) navigation** — navigation over a temp-table/TVP source is
  **deferred**: a lazy temp-table read source (`ctx.From(tempTableSource)`) is an untyped `TableAlias`
  with no navigation surface, so a navigation member cannot be read over it. The trigger is a navigation
  source combined with a temp-table/TVP path. Navigation declared **before** the temp-table boundary is
  still expanded into the materialising `CREATE TEMPORARY TABLE ... AS SELECT`; build a navigation over
  the temp-table rows from an explicit join or from the mapped source instead.
* **Adapter LINQ composition** — chaining query operators after `AsEntityBuilder<T>` is not delivered;
  only the direct terminals are recognised.
* **Bare reference-adapter projection** — projecting the reference marker itself
  (`Select(c => new { X = c.Parent!.AsEntityBuilder<Parent>() })`) is not a column and is rejected; use
  the `Any`/`Count`/`LongCount` terminals instead.

For the explicit, fully-featured relationship surface use [`JoinInto`](../advanced/relationships.md) and
the explicit join operators; see [Limitations](../advanced/limitations.md) for the general boundaries.

## See also

- [Relationships and single-query loading (`JoinInto`)](../advanced/relationships.md) — declaring the metadata this page builds on.
- [Joins](02-joins.md) — the explicit join surface.
- [Subqueries](05-subqueries.md) — the correlated forms nextorm renders.
- [Entities and metadata](../getting-started/03-entities-and-metadata.md) — keys and column mapping.
- [Limitations and out-of-scope features](../advanced/limitations.md).
