# Data modification (UPDATE)

> nextorm changes rows with the same explicit-command model as `INSERT` and `DELETE`:
> [`Update<TEntity>()`](xref:NextORM.Core.DataContextExtensions.Update``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}}))
> builds a parameterised `UPDATE <table> SET ... [WHERE ...]`, and the context extension
> [`Update<TEntity>(entity)`](xref:NextORM.Core.DataContextExtensions.Update``1(NextORM.Core.IDataContext,``0))
> updates by the entity's declared key. There is no change tracking and no `SaveChanges`: every terminal
> issues exactly one command.

**Prerequisites:** [Data modification (INSERT)](19-insert-statement.md) · [Data modification (DELETE)](20-delete-statement.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Provider overview](../providers/overview.md)

## Updating rows by predicate

[`Update<TEntity>()`](xref:NextORM.Core.DataContextExtensions.Update``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}}))
returns a [`UpdateBuilder<TEntity>`](xref:NextORM.Core.UpdateBuilder`1). Add one `Set` per written column, then
`Where`, and finish with `Update()`/`UpdateAsync()` to get the affected-row count:

```csharp
var updated = ctx.Update<ISimpleEntity>()
    .Set(x => x.Name, "renamed")
    .Set(x => x.Age, 42)
    .Where(x => x.Id == 1)
    .Update();

await ctx.Update<ISimpleEntity>()
    .Set(x => x.Name, "renamed")
    .Where(x => x.Age > 10)
    .UpdateAsync(cancellationToken);
```

* `Set(column, value)` always binds the value as a parameter, never inlines it.
* `Where` accepts the same predicate expressions as a query (`From<T>().Where(...)`), and repeating it
  combines the predicates with `and`. Captured variables become parameters, inline literals are emitted
  verbatim, exactly as in a query `WHERE`.
* `Update()`/`UpdateAsync()` return the affected-row count (`0` when nothing matched). Omitting `Where`
  updates **every** row of the table.
* `ToSql()` renders the statement without opening a connection.

## Assignments

The right-hand side of `Set` is a value, a mapped column, or an expression:

```csharp
// value (parameter)
.Set(x => x.Name, "renamed")

// another mapped column of the same row
.Set(x => x.UpdatedAt, x => x.CreatedAt)

// arbitrary expression: other columns and captured values
.Set(x => x.Counter, x => x.Counter + 1)
.Set(x => x.Total, x => x.Price * quantity)
```

An expression is rendered by the same translator as a `SELECT`/`WHERE` expression, so it may only use
mapped columns, captured values and the supported scalar functions. `Set(entity)` writes every mapped,
non-key, non-identity, non-computed column of the entity at once:

```csharp
ctx.Update<ISimpleEntity>()
    .Set(entity)
    .Where(x => x.Id == entity.Id)
    .Update();
```

Assigning a computed column throws `NotSupportedException`. A repeated `Set` for the same column replaces
the earlier assignment.

## Updating every row

Unlike `DELETE`, which requires the explicit `All()` marker, `UPDATE` without `Where` updates the whole
table:

```csharp
ctx.Update<ISimpleEntity>().Set(x => x.Archived, true).Update();   // update simple_entity set archived = @p0
```

## Updating by key

The key form updates exactly the row identified by the entity's declared key (`[Key]`/`.Key()`), writing
every non-key, non-identity, non-computed column and rendering the key equality as a parameter:

```csharp
ctx.Update(new SimpleEntity { Id = 1, Name = "renamed" });   // update simple_entity set name = @p0 where id = @p1
await ctx.UpdateAsync(new SimpleEntity { Id = 1, Name = "renamed" });
```

The entity type must declare a key; otherwise the call throws `InvalidOperationException`.

## Returning the updated rows

`Returning()` / `Returning(projection)` materialise the updated rows through the provider's
`RETURNING`/`OUTPUT` form, read through `Single()`/`ToList()`:

```csharp
var updated = ctx.Update<ISimpleEntity>()
    .Set(x => x.Archived, true)
    .Where(x => x.Age > 10)
    .Returning(x => new { x.Id, x.Name })
    .ToList();

var one = ctx.Update<ISimpleEntity>()
    .Set(x => x.Archived, true)
    .Where(x => x.Id == 1)
    .Returning()
    .Single();
```

`Returning()` returns the whole mapped entity (a concrete `TEntity`); `Returning(x => ...)` returns the
projected shape (a single member, an anonymous type or a member-init). `Single()` throws when the update
touched no row or more than one. `Returning` is only available on the predicate form: the key form
(`Update(entity)`) does not expose it.

## Updating from a join

[`UpdateJoin()`](xref:NextORM.Core.DataContextExtensions.UpdateJoin``2(NextORM.Core.JoinedEntityBuilder{``0,``1}))
on a joined query builds a multi-table `UPDATE` whose target is the **first** table of the join and whose
`SET` values may read any joined table:

```csharp
var updated = ctx.From<IOrder>()
    .Join(ctx.From<ICustomer>(), (o, c) => o.CustomerId == c.Id)
    .UpdateJoin()
    .Set(p => p.Item1.Status, "priority")
    .Set(p => p.Item1.Total, p => p.Item1.Total + p.Item2.Credit)
    .Where(p => p.Item2.Tier == "gold")
    .Update();
```

[`Set`](xref:NextORM.Core.UpdateJoinBuilder`1.Set``1(System.Linq.Expressions.Expression{System.Func{`0,``0}},``0))
selects a column of the first (`Item1`) table; the value may be a constant or an expression that
references any joined table (`p.Item2...`). `Where` filters on the whole projection, and the terminal is
`Update()`/`UpdateAsync()` (affected-row count) or `ToSql()`.

Only INNER `Join` joins are supported: the join conditions are folded into the filter (or kept as the
join's `ON`), so an outer join would silently change which rows are updated — `LeftJoin`/`RightJoin`/
`FullJoin`/`CrossJoin` throw `NotSupportedException`. PostgreSQL and SQLite render `UPDATE ... FROM`,
SQL Server renders `UPDATE <alias> ... FROM ... JOIN`, and MySQL/MariaDB render `UPDATE ... JOIN ... SET`;
ClickHouse and the in-memory provider throw.

The joined side may be a CTE: its declaration is hoisted before the `UPDATE` while the target stays the
first (physical) table.

```csharp
var recent = ctx.With("recent", ctx.From<IOrder>().Where(o => o.Id > 1000).Select(o => new { o.Id }));

var updated = ctx.From<IOrder>()
    .Join(recent.From("recent"), (o, r) => o.Id == r.GetInt64("id"))
    .UpdateJoin()
    .Set(p => p.Item1.Status, "priority")
    .Update();
```

```sql
-- PostgreSQL
with recent as (select id from orders where (id > 1000)) update orders as "t1" set status = @p0 from recent as "t2" where t1.id = t2.id
```

This works on every provider that supports `UPDATE ... FROM`/`JOIN`, including when the CTE sits on the
second or a later join, and for recursive CTEs (SQL Server appends `OPTION (MAXRECURSION n)`). Two
distinct CTE declarations sharing a name on the two sides of a join are rejected with an
`InvalidOperationException`, because a single `WITH` cannot bind one name to two definitions. See
[Common table expressions](09-cte.md).

## Provider support at a glance

| Provider | `UPDATE ... SET ... WHERE` | `RETURNING` / `OUTPUT` | `UPDATE ... FROM` |
|---|---|---|---|
| PostgreSQL | yes | `UPDATE ... RETURNING <cols>` | `UPDATE <target> AS t1 SET ... FROM ... WHERE ...` |
| SQL Server | yes | `UPDATE ... OUTPUT inserted.<col> ...` (between `SET` and `WHERE`) | `UPDATE <alias> SET ... FROM <target> JOIN ...` |
| SQLite | yes | `UPDATE ... RETURNING <cols>` | `UPDATE <target> AS t1 SET ... FROM ... WHERE ...` (3.33+) |
| MySQL / MariaDB | yes | `NotSupportedException` | `UPDATE <target> JOIN ... SET <alias>.<col> = ...` |
| ClickHouse | `ALTER TABLE ... UPDATE ... SETTINGS mutations_sync = 1` (reports no affected-row count) | `NotSupportedException` | `NotSupportedException` |
| In-memory | `NotSupportedException` (query-only) | `NotSupportedException` | `NotSupportedException` |

## Notes and out-of-scope

* `UPDATE` writes only the columns named in `Set` — there is no change tracking, so "the modified
  properties" cannot be inferred.
* ClickHouse updates through `ALTER TABLE ... UPDATE`; the mutation is applied synchronously
  (`SETTINGS mutations_sync = 1`) but reports no affected-row count, so `Update()`/`UpdateAsync()` return
  `0`. A predicate is required by the engine, so an update without `Where` renders `WHERE 1`.
* Optimistic concurrency (`rowversion`) and global query filters are **not** part of this surface.
* The in-memory context is query-only: `Update`/`UpdateAsync` (like every other write) throw
  `NotSupportedException`; query your own collections instead.
* `UPDATE` is not prepared or plan-cached — optimisation in nextorm targets read-only queries only
  (`Prepare`, the implicit plan cache, benchmarks); a mutation always renders and executes one command
  per call.

## See also

- [Data modification (INSERT)](19-insert-statement.md)
- [Data modification (DELETE)](20-delete-statement.md)
- [Data merging (MERGE / upsert)](23-merge-statement.md)
- [Filtering (WHERE)](02-filtering-where.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)
- [Provider overview](../providers/overview.md)
- [API reference](../advanced/api-reference.md)
