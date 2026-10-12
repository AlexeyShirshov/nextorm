# Per-parent paging in eager loading (`LoadWith`)

> `LoadWith` can apply a child query's own `Limit`/`Offset`/`Page` **independently to each parent**. Child filtering and ordering run first, so the page a parent receives is the `filter → order → Skip → Take` slice of its own children; a child query that limits globally never starves later parents. Root paging stays independent, and a parent with no qualifying children is kept with an empty collection. The rewrite is a partitioned `row_number()` child source, batched exactly like ordinary eager loading and supported in both `EagerLoadMode.SplitQuery` and `EagerLoadMode.SingleQuery`.

**Prerequisites:** [Eager loading child collections (`LoadWith`)](eager-loading.md) · [Joins](../guide/02-joins.md) · [Relationships and single-query loading (`JoinInto`)](relationships.md)

## Overview

Ordinary `LoadWith` loads **every** matching child for each parent and leaves navigation order to the child query. When the child query carries its own paging (`Take`/`Skip`, exposed as `Limit`/`Offset`/`Page`), nextorm treats that paging as **per parent** instead of as one global limit over the whole child result:

```csharp
var parents = ctx.From<Order>()
    .LoadWith(o => o.Items,
        c => c.From<OrderItem>().OrderBy(i => i.CreatedAt).Offset(1).Limit(2), // the 2nd and 3rd item of each order
        o => o.Id, i => i.OrderId)
    .OrderBy(o => o.Id)
    .ToList();
```

Every order receives its own slice: `Offset`/`Limit` are resolved against that order's children, not against the concatenation of all children. It is therefore safe to page a child query whose parents are many; no child of a later parent is starved by the first parent's rows.

This is different from applying `Skip`/`Take` **after** materialization: the page is computed by the database inside each parent's partition, and it is applied before the children are attached.

## Evaluation order per parent

For every parent the effective pipeline is:

1. **Filter** — the child query's own `Where` (and its effective query-filter scope) selects the candidate children.
2. **Order** — the child query's effective `OrderBy` orders each parent's candidates. Direction and provider null-ordering are preserved, and multiple keys apply in order.
3. **Skip** — the normalized `Offset`/`Skip` drops that many leading candidates.
4. **Take** — the normalized `Limit`/`Take` keeps that many of the remaining candidates.

Paging normalization follows the existing [`Paging`](../guide/04-sorting-and-paging.md) contract, not a second implementation: `Take(0)` means "no limit" and does **not** activate paging, so a parent keeps every child; a `Skip` past the end of a parent's children and a parent with no matching children produce an empty collection. An actual page needs a child `OrderBy` (see below). Bounds are overflow-safe exactly as ordinary paging.

## Root paging stays independent

Parent paging and child paging are separate bounds on separate sources. `.Offset(...)`/`.Limit(...)` applied to the outer builder still select the parent rows; the child page is resolved per parent afterwards and never changes the parent set:

```csharp
var parents = ctx.From<Order>()
    .LoadWith(o => o.Items, c => c.From<OrderItem>().OrderBy(i => i.CreatedAt).Limit(2), o => o.Id, i => i.OrderId)
    .OrderBy(o => o.Id)
    .Offset(10)   // root: parents 11..20
    .Limit(10)
    .ToList();    // each selected parent still gets its own 2 children
```

## Empty parents are preserved

A parent whose children are all filtered out, whose `Skip` drops them all or that has no children at all is still returned, with an **empty** collection. The rank filter lives inside the child source, never as a predicate that could remove childless parents from the outer result. In `SingleQuery` mode this follows from the `LEFT JOIN`; in `SplitQuery` mode the parent statement is untouched and the stitcher leaves unmatched parents empty.

## Both eager modes

Per-parent paging is available in both strategies; the mode still selects only how the rows are transported.

### Split-query (default)

The parent statement is unchanged. One batched child statement per chunk of at most 1000 distinct parent keys carries a **partitioned ranking** derived source, and the page bound filters each partition:

```sql
-- parent statement (unchanged by the child page)
select id, name from "order" as 't1' order by t1.id

-- one batched child statement per key chunk (representative shape)
select t1.id, t1.order_id, t1.created_at
from (
    select *, row_number() over (partition by order_id order by created_at) as __nextorm_rn
    from order_item
) as t1
where t1.order_id in (@k0, @k1, @k2)
  and t1.__nextorm_rn > 1 and t1.__nextorm_rn <= 3
```

The partition key is the complete parent-link key, and the window ordering is the child query's effective ordering. Composite parent-link keys (a `ValueTuple` of key components) are supported on this split path on SQLite, PostgreSQL, MySQL, MariaDB and ClickHouse; SQL Server rejects the composite form before execution (see below). The rank column is internal: it is used only to filter the derived source and never reaches the outer projection or the materialized entity. Batching is unchanged — two round trips for up to 1000 parents, no N+1.

### Single-query (`EagerLoadMode.SingleQuery`)

The ranked child source is joined into the single denormalized command instead of being a separate statement, so per-parent paging stays **one** round trip:

```sql
-- representative shape: the ranked child is a derived source joined per parent
select t1.id, t1.name, t3.id, t3.order_id, t3.created_at
from "order" as t1
left join (
    select *, row_number() over (partition by order_id order by created_at) as __nextorm_rn
    from order_item
) as t3 on t1.id = t3.order_id and t3.__nextorm_rn > 1 and t3.__nextorm_rn <= 3
order by t1.id, t3.created_at
```

Because the ranked child is a **nested** derived source, its statement-level `ORDER BY` is not emitted inside the derived table (some providers reject an `ORDER BY` there); the ordering that pairs children back onto parents is carried by the outer command instead. The rank column is dropped from the projection as in split mode.

## Supported and unsupported child shapes

Supported by the ranked child source:

- a child `Where` predicate, applied **before** ranking;
- a child `OrderBy` (single or multiple keys, ascending/descending) that accompanies paging;
- `Limit`/`Offset`/`Page` (skip-only, take-only, skip+take), including a skip past the end; `Take(0)` is "no limit" and does not activate paging, so it is an ordinary unpaged load;
- the same parent-link key shapes and projections that ordinary `LoadWith` supports.

Rejected **before any SQL is generated**:

- a **bare child `OrderBy`** in `SingleQuery` mode that is not accompanied by paging — the denormalized join still cannot express an output-only child ordering (split mode does not have this restriction);
- a child shape the ranked source cannot fold in — `Distinct`, `GroupBy`, `Having` and the other aggregate/grouped/set shapes — rejected in **both modes** (split and single-query), because the ranked lowering renders only the child's own `Where` and `OrderBy`;
- per-parent paging combined with `WITH TIES` (`WithTies()`): the ranked source keeps a fixed number of rows per partition and cannot express the tied-with-the-last-row semantics, so the combination is rejected before any SQL (`NotSupportedException`);
- a paged child with **no `OrderBy`** in either mode — a per-parent page needs a window ordering, and a `row_number()` window with no `ORDER BY` is invalid on providers such as SQL Server;
- `EagerLoadMode.Correlated`, which remains reserved and rejected in this release;
- per-parent paging through **`JoinInto`**, which keeps its own fail-closed guard;
- per-parent paging on the **in-memory** provider, which has no SQL window and rejects a paged eager child with `NotSupportedException` (unpaged eager loading is unchanged there);
- composite / `ValueTuple` parent-link keys on the **single-query** path and on **SQL Server** (both paths) — the pre-existing tuple-`IN` and key-equality guards reject them before any SQL runs, whereas the split path supports them on SQLite, PostgreSQL, MySQL, MariaDB and ClickHouse; scalar (single-column) parent-link keys are unaffected.

The rejection is terminal-agnostic: it happens during preparation, before the parent statement runs and before a plan entry is inserted. There is no client-side relational fallback — a page is never re-expressed as a global child `LIMIT`.

## Ordering ties and unordered pages

The ranking gives each parent a deterministic sequence only for a **total** ordering. When the child ordering ties, the exact members that fall inside the page are not promised: the same row set is guaranteed only up to count and membership. To make the per-parent page deterministic, order by a unique key. A paged child with no `ORDER BY` at all is rejected before execution: the window would have no valid order (see above).

## Provider notes

| Provider | Per-parent paging |
|---|---|
| SQLite | yes — window functions and a derived source |
| PostgreSQL | yes — window functions and a derived source |
| SQL Server | yes — the rank filter is applied inside a derived source; the nested source carries no statement-level `ORDER BY` |
| MySQL / MariaDB | yes, where the server supports window functions (see the server prerequisite below) |
| ClickHouse | yes, where the server supports window functions and derived sources |
| In-memory | rejected fail-closed (`NotSupportedException`) — no SQL window; client-side relational paging is never substituted |
| `JoinInto` | rejected fail-closed — per-parent top-N is a `LoadWith` capability |

Window-function support is a **server compatibility prerequisite**, not an auto-negotiated capability: nextorm emits the partitioned `row_number()` lowering against the provider you configure, and you target an engine that supports window functions and a derived source. No `ISqlDialect` capability is negotiated for this rewrite and no minimum version is detected automatically. The in-memory provider is rejected fail-closed because it has no SQL window; there is no relational client fallback.

## See also

- [Eager loading child collections (`LoadWith`)](eager-loading.md) — the loader, its modes, assignment rule and ordering guarantees.
- [Relationships and single-query loading (`JoinInto`)](relationships.md) — the declared-metadata single-query loader.
- [Joins](../guide/02-joins.md) — the explicit join surface.
- [Sorting and paging](../guide/04-sorting-and-paging.md) — the paging contract reused per parent.
- [Implicit navigation queries](../guide/29-implicit-navigation.md) — declared relationships as query constructs.

---

Source: `tests/nextorm.sqlite.tests/EagerLoadingSqlGenerationTests.cs:457` (split), `:624` (single-query),
`tests/nextorm.sqlite.tests/EagerLoadPerParentPagingTests.cs:53`,
`tests/nextorm.core.tests/EagerLoadPerParentPagingTests.cs:48`.
