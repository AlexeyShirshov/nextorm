# Eager loading child collections (`LoadWith`)

> `LoadWith` fills a parent-side collection and stitches the children onto the already materialized parents in memory. Split-query is the default — one extra child query per key chunk, two round trips for at most 1000 parents, never N+1 — and passing `EagerLoadMode.SingleQuery` (or configuring the context-wide default) fetches the parents and every declared collection in one denormalized round trip instead. The strategy is explicit: split, single-query, a conservative `Auto`, or a context default; `Correlated` stays reserved and rejected, while a child query's own `Take`/`Skip` is applied per parent (see [Per-parent paging in eager loading](eager-loading-per-parent-paging.md)). A child query may itself declare `LoadWith`, so a finite graph nests to any depth (level 2 and deeper) and runs in a single, graph-wide mode.

**Prerequisites:** [Quickstart](../getting-started/02-quickstart.md) · [Joins](../guide/02-joins.md) · [Correlated queries](../guide/05-subqueries.md)

## Overview

nextorm does not infer relationships, so a graph is not loaded from a mapper convention. `LoadWith` declares one edge of eager loading explicitly: a parent collection member, a factory that builds the child query, and the two key selectors that pair children with parents — it does not consume relationship metadata. The child query is an ordinary `EntityBuilder<TChild>` and can carry its own `LoadWith`, so the declarations form a finite graph, level 2 and deeper included. The association lives on the query, not in the entity metadata; for the declared-metadata, single-round-trip alternative see [`JoinInto`](relationships.md).

This is the nextorm equivalent of linq2db `LoadWith` and EF Core `Include`. Split-query is the default; the opt-in `EagerLoadMode.SingleQuery` mode issues one denormalized round trip instead, and a nested graph is supported in both modes where the provider can express it (see [Nested graphs](#nested-graphs-level-2-and-deeper)).

## Signature

```csharp
public EntityBuilder<TEntity> LoadWith<TChild, TKey>(
    Expression<Func<TEntity, ICollection<TChild>>> collection,
    Func<IDataContext, EntityBuilder<TChild>> childQuery,
    Expression<Func<TEntity, TKey>> parentKey,
    Expression<Func<TChild, TKey>> childKey,
    EagerLoadMode mode = EagerLoadMode.Default)
    where TKey : notnull;
```

- `collection` — the parent-side [`ICollection<TChild>`](xref:System.Collections.Generic.ICollection`1) member to fill.
- `childQuery` — builds the child query from the data context that owns the parent query.
- `parentKey` / `childKey` — the selectors paired to decide which children belong to which parent.
- `mode` — the whole-builder eager-load mode (see [Mode selection](#mode-selection)); `EagerLoadMode.Default` inherits the builder's current choice, which falls back to the context-wide default (`DataContextBuilder.UseEagerLoadMode`, `SplitQuery` unless configured).
- `TKey` must be a non-nullable type (`where TKey : notnull`), for example `int` or `string`. A null key value observed at runtime is skipped and never matches.

`LoadWith` returns a **copy** of the builder carrying the declaration, so the original builder is unchanged and several loads can be chained:

```csharp
var orders = ctx.From<Order>()
    .Where(o => o.CustomerId == customerId)
    .LoadWith(o => o.Items, c => c.From<OrderItem>(), o => o.Id, i => i.OrderId)
    .OrderBy(o => o.Id)
    .ToList();
```

Like every builder modifier, `LoadWith` needs the generic key type to be known at the call site; when a selector is implicitly convertible, spell the key type out or make both selector bodies produce the same type.

## Mode selection

The `mode` argument selects how the declared collections are loaded, using the public [`EagerLoadMode`](xref:NextORM.Core.EagerLoadMode) enum. The numeric values are fixed and migration-compatible: `Default=0`, `SplitQuery=1` and `SingleQuery=2` keep their historical meaning for callers that persist or compare the enum, and `Correlated=3`/`Auto=4` are appended:

| Member | Value | Meaning |
|---|---|---|
| `EagerLoadMode.Default` | `0` | No explicit choice: inherit the mode already active on the builder, falling back to the context default below. |
| `EagerLoadMode.SplitQuery` | `1` | Force split loading for the whole builder. |
| `EagerLoadMode.SingleQuery` | `2` | Force single-query loading for the whole builder. |
| `EagerLoadMode.Correlated` | `3` | Correlated per-parent loading. Recognized but **not supported** in this release; a query that resolves to it is rejected. |
| `EagerLoadMode.Auto` | `4` | Automatic selection, resolved once by the conservative rule below. |

The mode belongs to the **whole builder**, not to an individual declaration, and one resolved mode covers the whole nested graph (every level declared by its child queries):

- An explicit mode applies to every collection declared on the builder, including one declared earlier while the mode was still `Default`.
- A later `LoadWith(..., EagerLoadMode.Default)` (or an omitted argument) inherits the current mode; it never resets it.
- Repeating the same explicit mode is allowed.
- Mixing two different explicit modes — `SingleQuery` then `SplitQuery`, or the reverse — throws [`NotSupportedException`](xref:System.NotSupportedException): a single SQL command cannot mix a denormalized join for one collection with a separate chunked statement for another.
- A value other than the five defined enum members throws [`ArgumentOutOfRangeException`](xref:System.ArgumentOutOfRangeException) with `ParamName == "mode"`, and the original builder is left unchanged.

The mode is resolved when `LoadWith` runs; the load specification is otherwise unchanged, so switching modes never changes the [assignment rule](#assignment-rule) or the [ordering guarantees](#ordering-and-deduplication).

### Context-wide default

[`DataContextBuilder.UseEagerLoadMode(EagerLoadMode)`](xref:NextORM.Core.DataContextBuilder.UseEagerLoadMode(NextORM.Core.EagerLoadMode)) configures a context-wide default for `LoadWith` collections whose query declares no explicit mode. It defaults to `EagerLoadMode.SplitQuery`, so an unconfigured context keeps the historical behavior:

```csharp
var builder = new DataContextBuilder()
    .UseSqlite(connection)
    .UseEagerLoadMode(EagerLoadMode.SingleQuery);
using var dataContext = builder.CreateDataContext();
```

- The default must be one of the four concrete strategies — `SplitQuery`, `SingleQuery`, `Correlated` or `Auto`. `EagerLoadMode.Default` is rejected with [`ArgumentOutOfRangeException`](xref:System.ArgumentOutOfRangeException): it is an inheritance sentinel, not a context setting, and it must not be applied recursively.
- Configuring a context never changes a static/global value; each context carries its own setting, exposed read-only as `EagerLoadMode` on [`IDataContext`](xref:NextORM.Core.IDataContext) / [`DataContext`](xref:NextORM.Core.DataContext).
- The default is resolved once, before any SQL is generated, and the resolved value is folded into the plan-cache key, so two queries that resolve to different strategies never share a cached plan.

### Precedence

The effective strategy is `query override ?? context default`: an explicit `LoadWith(..., mode)` wins over the context default, and when the query makes no explicit choice the context default applies. Within a builder, `EagerLoadMode.Default` keeps inheriting the mode already chosen by an earlier `LoadWith`.

### Conservative `Auto`

`Auto` is a **conservative dispatcher**, not a cost-based optimizer. It never guesses provider capabilities and never retries a failed execution with another strategy:

| Requested operation | Behavior in this release |
|---|---|
| Ordinary supported `LoadWith`, `Auto` | Resolves to `SplitQuery`. |
| Ordinary `LoadWith`, explicit `SplitQuery`/`SingleQuery` | Existing split/single implementation and guards. |
| `LoadWith`, explicit `Correlated` | Rejected before any SQL is generated and before a plan is cached. |
| A request needing per-parent top-N (a child `Limit`/`Offset`/`Page`) | Honored per parent through the ranked child source, batched like any split load; `Auto` never reinterprets it as a global child limit or an unrestricted load (see [Per-parent paging in eager loading](eager-loading-per-parent-paging.md)). |
| `JoinInto` | Fixed single-query; unaffected by the context default (see [Relationship metadata and `JoinInto`](#relationship-metadata-and-joininto)). |
| No eager loads | No eager-loading dispatch or eager-load-specific command mutation. |

`Auto` selects `SingleQuery` only when it is asked explicitly; automatic single selection, arbitrary `APPLY`/`LATERAL` construction and provider capability guessing are not introduced.

### `Correlated` and per-parent paging

`EagerLoadMode.Correlated` has no implemented execution path in this release. It is recognized and rejected with [`NotSupportedException`](xref:System.NotSupportedException) **during preparation — before any SQL is generated and before a plan entry is inserted**, so a rejected query neither runs the parent statement nor poisons the plan cache. The enum value is reserved for the correlated work tracked separately. The rejection is terminal-agnostic: it also holds on the **non-list** terminals below, which evaluate the parent only. Such a terminal first validates the `LoadWith` set, so an explicit `Correlated` is rejected before its parent command is built instead of being silently dropped; the non-list terminal still does not materialize the collection.

A child query's own `Limit`/`Offset`/`Page` is **not** rejected: it is applied independently to each parent through a partitioned `row_number()` child source in both `SplitQuery` and `SingleQuery`, with child filtering and ordering before paging, root bounds kept independent and empty parents preserved. `Auto` honors the paged load through its split dispatch, and a paged load is never reinterpreted as a global child limit or an unrestricted load. Per-parent top-N is a `LoadWith` capability: `JoinInto` rejects it before execution, and the in-memory provider rejects a paged eager child fail-closed (it has no SQL window) while unpaged eager loading is unchanged. See [Per-parent paging in eager loading](eager-loading-per-parent-paging.md).

## Split-query semantics

The declaration is **not** part of the parent SQL. After the parent query is materialized:

1. The distinct, non-null parent keys are collected **in parent order** and split into chunks of at most **1000** keys.
2. One child query per chunk runs `WHERE childKey IN (<chunk keys>)` — a separate statement, no `JOIN` and no per-parent query.
3. The children are grouped by `childKey` and assigned to the parents whose `parentKey` matches.

A query with at most 1000 distinct parent keys therefore issues exactly **two round trips**: one parent statement and one child statement. Larger key sets add one child statement per additional chunk; the parent statement still runs once. There is no N+1: the number of child statements depends on the size of the key set, not on the number of parents.

```sql
-- parent statement
select id, name from order

-- child statement (one per chunk of at most 1000 distinct parent keys)
select id, order_id, name from order_item where order_id in (@k0, @k1, @k2)
```

The chunk predicate is an ordinary `IN` list, so it participates in the plan cache and provider parameter binding exactly like a captured-collection `Contains`.

When `ToListAsync`/`ToArrayAsync` is cancelled, the token is checked between key chunks and between multiple `LoadWith` declarations, so a cancelled call stops issuing further child statements.

## Single-query mode (`EagerLoadMode.SingleQuery`)

By default `LoadWith` uses the split shape above — two or more round trips, one per key chunk. Pass `EagerLoadMode.SingleQuery` as the last argument to `LoadWith` (the mode applies to the whole builder), or configure the context-wide default with `UseEagerLoadMode(EagerLoadMode.SingleQuery)`, to fetch the parents and every declared collection with **one** denormalized command instead:

```csharp
var orders = ctx.From<Order>()
    .Where(o => o.CustomerId == customerId)
    .LoadWith(o => o.Items, c => c.From<OrderItem>(), o => o.Id, i => i.OrderId,
        EagerLoadMode.SingleQuery)
    .ToList();
```

The builder then emits one denormalized command instead of the two statements above:

```sql
select t1.id, t1.name, t2.id, t2.order_id, t2.name
from order as t1
left join order_item as t2 on t1.id = t2.order_id
```

A child `Where` is folded into the join `ON`, so it filters the children without dropping childless parents:

```sql
select t1.id, t1.name, t2.id, t2.order_id, t2.name
from order as t1
left join order_item as t2 on t1.id = t2.order_id and t2.active
```

- **One round trip, no key chunking.** The parent source and each declared child are joined in a single `LEFT JOIN` command. There is no `IN` list and no chunk size, so any number of parents — including more than 1000 distinct keys — is fetched by the same command, and there is no silent fallback to split.
- **Only the child query's own `Where` is folded in.** A child condition, for example `c => c.From<OrderItem>().Where(i => i.Active)`, is merged into the join `ON` predicate, so it filters the children without dropping childless parents.
- **Paging is folded into a ranked source; other child-query shapes are rejected.** A child query carrying `Limit`/`Offset`/`Page` (with or without its own `OrderBy`) is folded into a partitioned `row_number()` derived source and paged per parent (see [Per-parent paging in eager loading](eager-loading-per-parent-paging.md)). A **bare** child `OrderBy` that is not accompanied by paging is still a per-parent output modifier the single denormalized join cannot express, and a single denormalized join cannot apply joins or table-level clauses either, so a child query carrying a bare `OrderBy`, `Distinct`, `GroupBy`, `Having`, `DistinctOn`, `LimitBy`, a join (`Join`/`LeftJoin`/`SemiJoin`/`AntiJoin`/…), `Final`, `PreWhere`, `ArrayJoin`, `TableSample`, `Sample`, `ForSystemTime`, `ForUpdate`/`ForShare`, `Window`, `Settings`, a CTE, a table/index hint or a derived (`As`) source throws [`NotSupportedException`](xref:System.NotSupportedException) as soon as the single-query command is materialized. Split mode honors every one of them; use split (omit `EagerLoadMode.SingleQuery` or pass `EagerLoadMode.SplitQuery`) when the child query needs a shape beyond `Where`. The ClickHouse-only shapes in that list (`LimitBy`, `Final`, `PreWhere`, `ArrayJoin`, `Sample`, `Settings`) are extension methods from `NextORM.ClickHouse` (`using NextORM.ClickHouse;`).
- **Parent and child filter scopes combine by union, identically in split and single-query mode.** The child's effective scope is always the union of its own `IgnoreFilters` scope and the parent's. A parent `IgnoreFilters()` disables the declared children's filters too (the all-or-nothing scope absorbs the child's own scope), and a selective parent `IgnoreFilters(...)` reaches the children as well. Conversely, an `IgnoreFilters(...)` applied inside the child query disables only that child's filters while the parent's still apply. The same rule holds whether you load with the default split shape or with `EagerLoadMode.SingleQuery`.
- **The parent type must declare a mapped key.** Denormalized rows are deduplicated by that key, so a keyless parent type throws [`NotSupportedException`](xref:System.NotSupportedException) with guidance to configure a key or use split mode, which does not require one. Rows that share the same mapped key collapse into one parent; split mode keeps them separate.
- **The key type must support value equality.** The join predicate is built with the equality operator, so a key type without one — for example a struct with no `==` overload, or a reference type whose `==` would degrade to reference equality — throws [`NotSupportedException`](xref:System.NotSupportedException). Split mode compares keys with the default equality comparer and works for such types.
- **Nested graphs are joined per level.** A child query that itself declares `LoadWith` is planned as a nested graph: the builder emits one `LEFT JOIN` per node and assembles every level from the one result set (see [Nested graphs](#nested-graphs-level-2-and-deeper)). Only the child's own `Where` is foldable at each level, and a node that both declares children and pages per parent is rejected.
- **Assignment and ordering are unchanged.** The same stitcher deduplicates the parents and fills each collection, so the [assignment rule](#assignment-rule) and [ordering guarantees](#ordering-and-deduplication) below hold in both modes.
- **One plain entity source only.** `EagerLoadMode.SingleQuery` cannot be combined with other joins on the same builder; declaring it on a query that already carries a regular (non-`JoinInto`) join throws [`NotSupportedException`](xref:System.NotSupportedException).
- **Cannot be composed further.** The single-query mode of `LoadWith` cannot be followed by `Select` or `As` (a projection command has no loader) or by `Join`/`Apply` (a joined builder carries neither the load specifications nor the mode). Each of those compositions throws [`NotSupportedException`](xref:System.NotSupportedException) instead of silently leaving the collections empty; materialize with a stitching terminal first.

Split stays the default: prefer it when the chunked child result is smaller than the denormalized join product, and opt into `EagerLoadMode.SingleQuery` when a single round trip matters more.

## Nested graphs (level 2 and deeper)

A child query is an ordinary `EntityBuilder<TChild>`, so it can declare its own `LoadWith` and the loader recurses: `Root → Mids → Leaves → Tips …` is loaded level by level, to any finite depth. Nothing about the declaration changes — each `LoadWith` still names one parent collection, one child factory and two key selectors — and the same assignment rule, ordering guarantee and null-key rule apply at every level.

```csharp
var roots = ctx.From<Root>()
    .LoadWith(
        r => r.Mids,
        c => c.From<Mid>()
            .OrderBy(m => m.SortKey)
            .LoadWith(m => m.Leaves, l => l.From<Leaf>().OrderByDescending(x => x.SortKey), m => m.Id, l => l.MidId),
        r => r.Id,
        m => m.RootId)
    .ToList();
```

The following contract holds for the whole graph:

- **One mode for the whole graph.** The strategy resolved for the root (`SplitQuery`, `SingleQuery`, a context default or the conservative `Auto`) applies to every level. A nested declaration may not request a different explicit mode: `LoadWith(..., EagerLoadMode.X)` on a child whose resolved mode differs from the graph-wide mode throws [`NotSupportedException`](xref:System.NotSupportedException) (`... mixes modes ...`). `EagerLoadMode.Default` on a child simply inherits the graph-wide mode.
- **Per-level order/top-N.** Each level's child query keeps its own effective `OrderBy`, and its own per-parent `Take`/`Skip` is applied within that level's parents (split mode, SQL providers). Level-1 ordering is unaffected, and two consecutive levels can order independently.
- **Ancestor-scoped identity.** Descendants are grouped by **ancestor-composite identity** — the level-1 key plus the level-2 key plus … — so the same child key under two different ancestors assembles under each ancestor independently. A null key at any level never matches and never crashes, and an empty intermediate result simply loads no descendants.
- **Split honors every child shape at every level** exactly as it does at level one; **single-query** folds only each node's own child `Where` into its `LEFT JOIN`.

### Provider and frontend matrix

| Nested graph / shape | Split (`SplitQuery`, `Auto`) | Single-query (`EagerLoadMode.SingleQuery`) |
|---|---|---|
| Nested `LoadWith` (depth 2+), child `Where` only | yes — every level, all SQL providers and in-memory | yes — one `LEFT JOIN` per node, all SQL providers and in-memory |
| Nested child `OrderBy` / `Distinct` / `GroupBy` / joins / other non-`Where` shapes | yes — every level | rejected fail-closed (only the child's own `Where` is foldable at each level) |
| Per-level per-parent `Take`/`Skip` (`Limit`/`Offset`/`Page`) | yes per level on SQL providers; the in-memory provider rejects a paged eager child (no SQL window) | rejected when the paged node also declares children — the ranked child has no per-level rendering |

The SQL providers (SQLite, PostgreSQL, SQL Server, MySQL/MariaDB, ClickHouse) execute nested split and nested single-query; the in-memory provider executes nested graphs for unpaged children and keeps its existing fail-closed rejection for a paged child. Frontends agree with this contract: a [`JoinInto`](relationships.md) level composes with a nested `LoadWith` graph — the joined collection and the deeper nested levels both stitch — and an entity-materializing implicit-navigation projection (`Select(n => new { n.Id, Parent = n.Parent })`) combined with a nested `LoadWith` graph is a tested rejection: it throws [`NotSupportedException`](xref:System.NotSupportedException) before execution rather than silently dropping the nested graph.

### Fail-closed forms

Every unexpressible nested graph is rejected before any SQL is generated and before a plan-cache entry is inserted, rather than silently dropping a descendant or falling back to another strategy:

- **unsupported child shape at any level** (single-query): a nested child that is not just `Where` — `OrderBy`, `Distinct`, `GroupBy`, `Having`, `DistinctOn`, a join, a table-level clause or a derived (`As`) source — throws [`NotSupportedException`](xref:System.NotSupportedException) naming the shape (see [Single-query mode](#single-query-mode-eagerloadmodesinglequery));
- **nested per-parent paging** (single-query): a node that both declares children and pages per parent throws `NotSupportedException` (`Limit/Offset/Page`); split supports per-level paging on SQL providers;
- **projection-arity overflow**: flattening the graph into a single denormalized command for more than seven child collections (the [`Projection<T1..T8>`](relationships.md#one-round-trip) arity cap) throws `NotSupportedException`; declare a shallower graph or use split mode;
- **nested mode conflict**: a child requesting an explicit mode different from the graph-wide mode throws `NotSupportedException` (`... mixes modes ...`);
- **cycles**: a declaration whose ancestor-composite identity repeats — a self-referential entity, or a child factory that recursively returns itself — throws `NotSupportedException` deterministically: a finite acyclic graph is required, not a depth limit.
- **in-memory paged eager child**: the existing #211 fail-closed rejection (no SQL window) is unchanged; unpaged nested loading works there.

## Assignment rule

For every parent the loader decides the target collection:

| Current value of `collection` | Member settable | Behaviour |
|---|---|---|
| non-null | either | cleared and refilled |
| `null` | yes | a fresh list is assigned |
| `null` | no (read-only) | [`NotSupportedException`](xref:System.NotSupportedException) |

A read-only collection is therefore supported as long as it is initialized with an empty collection and never reassigned:

```csharp
public sealed class Order
{
    public int Id { get; set; }
    public ICollection<OrderItem> Items { get; } = new List<OrderItem>();
}
```

A read-only member is not mapped as a database column, so initializing it does not affect the parent `SELECT`.

## Ordering and deduplication

- **Parent order is preserved.** Children are assigned to the parents in the order the parent query returned them; the loader never reorders the parents.
- **Child order follows the child query.** Children keep the order the child statement returned them in, so add an `OrderBy` to `childQuery` when the order matters.
- **Duplicate parent keys share the data (split).** Two parents with the same key each receive a collection with the same children (each parent has its own collection instance). In single-query mode the mapped parent key is the deduplication identity, so duplicate-key rows collapse into one parent — use split mode when the duplicate rows must stay distinct.
- **Unmatched children are ignored.** A child whose key matches no parent is never assigned, and a parent whose key has no children gets an empty collection.
- **Null keys never match.** A parent or child key that is `null` at runtime is skipped; it is never looked up and never crashes the grouping.

## Duplicate declarations

A collection member can be declared **only once** per query. Calling `LoadWith` twice for the same member throws [`InvalidOperationException`](xref:System.InvalidOperationException), because the two declarations would execute two child queries for the same collection and the later assignment would discard the earlier load. Declare each member at most once and use distinct members to load several collections:

```csharp
ctx.From<Order>()
    .LoadWith(o => o.Items, c => c.From<OrderItem>(), o => o.Id, i => i.OrderId)
    .LoadWith(o => o.Payments, c => c.From<Payment>(), o => o.Id, p => p.OrderId); // a different member
```

## Terminal boundary

The load specification is honoured **only** by the four stitching terminals:

- [`ToList()`](xref:NextORM.Core.EntityBuilderExtensions.ToList``1(NextORM.Core.EntityBuilder{``0},System.ReadOnlySpan{System.Object})) and [`ToListAsync()`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])).
- `ToArray()` and `ToArrayAsync()`.

These run the parent query — plus the split child queries, or the single denormalized command with `EagerLoadMode.SingleQuery` — and stitch the materialized rows into the collections.

Every other terminal evaluates the parent query **only** and does not run eager loading; it issues no child or load query, so the collections keep whatever value the entity was materialized with (usually empty):

- `ToHashSet` / `ToHashSetAsync` and `ToDictionary` / `ToDictionaryAsync`.
- `First` / `FirstAsync` and `FirstOrDefault` / `FirstOrDefaultAsync`.
- `Single` / `SingleAsync` and `SingleOrDefault` / `SingleOrDefaultAsync`.
- `ToEnumerable` and `ToAsyncEnumerable`.
- `ToCommand` and any `QueryCommand<T>` obtained from it.

`Any` / `AnyAsync` and `Count` / `CountAsync` are **scalar** terminals: they return a single value and never run eager loading either. `ToCommand()` ignoring the specification is deliberate — it is what lets the child query run without recursing into the parent's loader. Materialize the parents with one of the four stitching terminals when the collections must be filled. A non-list terminal still validates the `LoadWith` set before building its parent-only command: an explicit `Correlated` or a per-parent top-N child is rejected with [`NotSupportedException`](xref:System.NotSupportedException) (no SQL and no plan-cache entry) rather than silently ignoring the unsupported request. The same preparation-time validation covers the declared nested graph, so a cycle or a conflicting nested mode is rejected before the parent-only command is built. The child-query factory must be pure and idempotent; it is invoked at most once per eager-loading specification and context, during the first terminal validation, and the memoized child query is reused during execution.

## Relationship metadata and `JoinInto`

`LoadWith` does not create or consume navigation metadata; the association is expressed by the two key selectors at each call site. nextorm also has a metadata model for **declared** relationships (`HasMany`/`HasOne` or [`[Relationship]`](xref:NextORM.Core.RelationshipAttribute)) and a single-query loader, [`JoinInto`](relationships.md), which fills one child collection from one `LEFT JOIN` (or `INNER JOIN`) and stitches the denormalized rows back onto the deduplicated parents. Use `JoinInto` when the association is declared as relationship metadata, and `LoadWith` when it is expressed ad hoc by key selectors at the call site; `LoadWith` with `EagerLoadMode.SingleQuery` also reduces to one denormalized query, so the two share the same single-query materialization, assignment contract and ordering guarantees. `JoinInto` is a **fixed** single-query operation: unlike `LoadWith`, it never consults the context default, so a context configured with `UseEagerLoadMode(EagerLoadMode.SplitQuery)`, `Correlated` or `Auto` leaves `JoinInto` on the single-query path. Relationships are still never inferred from conventions or foreign keys. A graph is loaded explicitly: each `LoadWith` call declares one edge, and a nested `LoadWith` inside the child query declares the next level.
