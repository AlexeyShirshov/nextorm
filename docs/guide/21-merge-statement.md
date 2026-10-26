# Data merging (`MERGE` / upsert)

> nextorm builds a `MERGE` (upsert) through [`CreateMergeBuilder<TEntity>()`](xref:NextORM.Core.DataContextExtensions.CreateMergeBuilder``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}})): one entry point covers both the portable **key upsert** (`INSERT ... ON CONFLICT` / `ON DUPLICATE KEY` / `MERGE`) and the general, multi-branch **full `MERGE`** with `WHEN MATCHED`/`WHEN NOT MATCHED` branches. There is no change tracking and no `SaveChanges`: every terminal issues exactly one command.

**Prerequisites:** [Data modification (INSERT)](17-insert-statement.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Provider overview](../connecting-providers/overview.md)

## Overview

`CreateMergeBuilder<TEntity>()` writes a source row set into the target table and lets the database decide, row by row, what to do. The source is a mapped entity, a batch, or a server-side query; the match is either the declared key (`OnKeys()`) or an arbitrary condition (`On(...)`); and the actions are attached as branches. `Merge()`/`MergeAsync()` execute and return the affected-row count, while `ToSql()` renders the statement without a connection.

Two forms share the same builder:

* **key upsert** — `OnKeys()` + `WhenMatchedUpdate()` + `WhenNotMatchedInsert()`. Every SQL provider expresses it in its native form, and it is the only form the in-memory provider applies (to the registered sequence).
* **full `MERGE`** — `WhenMatched()`/`WhenNotMatched()`/`WhenNotMatchedBySource()`, each finished with `ThenUpdate`/`ThenInsert`/`ThenDelete`/`ThenDoNothing`. Native on SQL Server and PostgreSQL (an execution-time server prerequisite: the server must be 15+); the other providers reject it.

## Upsert (key merge)

[`CreateMergeBuilder<TEntity>()`](xref:NextORM.Core.DataContextExtensions.CreateMergeBuilder``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}})) writes a source row set into the table and lets the database decide, per declared key, whether to update the existing row or insert a new one. The source is a single mapped entity or a batch, the match key is resolved from the entity mapping with `OnKeys()`, and both branches — `WhenMatchedUpdate()` (set every non-key writable column from the source) and `WhenNotMatchedInsert()` — are required:

```csharp
ctx.CreateMergeBuilder<ISimpleEntity>()
    .Using(new SimpleEntity { Id = 1, Name = "a" })   // or Using(new[] { e1, e2 })
    .OnKeys()
    .WhenMatchedUpdate()
    .WhenNotMatchedInsert()
    .Merge();
```

The builder renders the provider's native form; `ToSql()` inspects it without a connection:

| Provider | Rendered form |
|---|---|
| PostgreSQL, SQLite | `INSERT ... ON CONFLICT (<keys>) DO UPDATE SET <col> = excluded.<col>` |
| MySQL, MariaDB | `INSERT ... ON DUPLICATE KEY UPDATE <col> = VALUES(<col>)` |
| SQL Server | `MERGE ... USING (VALUES ...) AS source (...) ON ... WHEN MATCHED THEN UPDATE SET ... WHEN NOT MATCHED THEN INSERT ...;` |
| In-memory | applied to the registered sequence in the context (no SQL) |
| ClickHouse | `NotSupportedException` (no engine-level DML upsert) |

```sql
-- PostgreSQL
insert into simple_entity (id, name) values (@p0, @p1) on conflict (id) do update set name = excluded.name

-- SQLite
insert into simple_entity (id, name) values ($p0, $p1) on conflict (id) do update set name = excluded.name

-- MySQL / MariaDB
insert into simple_entity (id, name) values (@p0, @p1) on duplicate key update name = values(name)

-- SQL Server
merge into simple_entity as target using (values (@p0, @p1)) as source (id, name) on target.id = source.id when matched then update set target.name = source.name when not matched then insert (id, name) values (source.id, source.name);
```

`Merge()`/`MergeAsync()` return the number of affected rows. The key must be declared (`[Key]`/`.Key()`) and must not be database-generated; an entity with only key columns is rejected because there is nothing to update. This is the *key upsert* form; for arbitrary branches (including `DELETE`) see [Full MERGE](#full-merge).

## Full MERGE

[`WhenMatched()`](xref:NextORM.Core.MergeBuilder`1.WhenMatched) / [`WhenNotMatched()`](xref:NextORM.Core.MergeBuilder`1.WhenNotMatched) / [`WhenNotMatchedBySource()`](xref:NextORM.Core.MergeBuilder`1.WhenNotMatchedBySource) extend the same builder into a general, multi-branch `MERGE` on the providers that render it natively (SQL Server, PostgreSQL; server 15+). Each branch is finished with an action, and the branches run in the order they are declared:

```csharp
ctx.CreateMergeBuilder<IDest>()
    .Using(source)                            // entity, batch, or a query: Using(ctx.From<IDest>().Where(...))
    .OnKeys()                                 // or .On((t, s) => t.Id == s.Id && s.Age > 0)
    .WhenMatched().ThenUpdate()               // or .WhenMatched((t, s) => t.Name != s.Name).ThenUpdate()
    .WhenNotMatched().ThenInsert()            // or .WhenNotMatched((t, s) => s.Age > 0).ThenInsert()
    .WhenNotMatchedBySource().ThenDelete()    // SQL Server only
    .Merge();
```

```sql
-- SQL Server
merge into dest as target using (values (@p0, @p1)) as source (id, name) on target.id = source.id when matched then update set target.name = source.name when not matched then insert (id, name) values (source.id, source.name) when not matched by source then delete;
```

`ThenUpdate()`/`ThenInsert()` without a selector write every eligible column; an explicit assignment set restricts the written columns — see [Column assignments](#column-assignments-expressions). A selector may not name a database-generated column, and a matched branch may not rewrite the match key. `ThenDelete()` on a matched branch and `WhenNotMatchedBySource().ThenDelete()` let the merge remove rows — the difference from the key upsert. `ThenDoNothing()` leaves the candidate row unchanged; it is available on PostgreSQL only, because SQL Server has no `DO NOTHING` action.

`On(condition)` replaces the key match with an arbitrary search condition (`ON <condition>`). A branch builder also accepts a condition (`WhenMatched(condition)`, `WhenNotMatched(condition)`, `WhenNotMatchedBySource(condition)`), rendered as `WHEN ... AND <condition>`; a branch fires only when its condition holds. Conditions use the two-parameter form `(target, source)` — reference the existing row as the first lambda parameter and the incoming source row as the second, for example `On((t, s) => t.Id == s.Id && s.Age > 0)`. On SQL Server a `WHEN NOT MATCHED BY SOURCE` condition may only reference the target row.

**VALUES-derived source restriction.** With a `VALUES` source — an entity or batch passed to `Using(IEnumerable<TEntity>)` — the `On(...)` match condition and every branch condition (`WhenMatched(...)`, `WhenNotMatched(...)`, `WhenNotMatchedBySource(...)`, whatever action ends it: `ThenUpdate`, `ThenDelete` or `ThenDoNothing`) may not reference a mapped source column that is **not part of the derived `VALUES` source**. That source declares the writable mapped columns only, so a database-generated column (identity or computed) has no derived column to name and `ToSql()`/`Merge()` throws `NotSupportedException`:

```csharp
// unsupported: Total is computed, so the VALUES source declares no Total column
ctx.CreateMergeBuilder<IDest>()
    .Using(new Dest { Name = "a" })
    .On((t, s) => t.Name == s.Name && s.Total > 0)
    .WhenMatched().ThenUpdate()
    .WhenNotMatched().ThenInsert()
    .ToSql();   // NotSupportedException

// supported: a query source projects the whole row, including Total
ctx.CreateMergeBuilder<IDest>()
    .Using(ctx.From<IDest>())
    .On((t, s) => t.Name == s.Name && s.Total > 0)
    .WhenMatched().ThenUpdate()
    .WhenNotMatched().ThenInsert()
    .ToSql();
```

Otherwise reference only columns the `VALUES` source already carries.

| Provider | Full `MERGE` | `THEN DELETE` | `THEN DO NOTHING` | `ON` / `WHEN ... AND` | `WHEN NOT MATCHED BY SOURCE` |
|---|---|---|---|---|---|
| SQL Server | yes | yes | — | yes | yes |
| PostgreSQL | yes (server 15+) | yes | yes | yes | — |
| SQLite, MySQL, MariaDB | — (key upsert only) | — | — | — | — |
| In-memory | — (key upsert only) | — | — | — | — |
| ClickHouse | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` |

`Using(ctx.From<T>().Where(...))` or `Using(QueryCommand<T>)` supplies the rows from a server-side query (`USING (<select>) AS source`) instead of a `VALUES` batch; a query source is accepted by the full-`MERGE` form only.

Under an active [global query filter](../advanced/query-filters.md), the full `MERGE` injects the target predicate into the `MERGE ... ON` condition and into every `WHEN NOT MATCHED BY SOURCE` arm (SQL Server), so a target row hidden by the filter is never matched, updated or deleted. The form must be a real multi-branch `MERGE` (actual `WhenMatched()`/`WhenNotMatched()` branches); `.On(...)` does not turn the key-upsert shortcut into that form. A key upsert on a provider that cannot express the predicate (`ON CONFLICT` / `ON DUPLICATE KEY` / in-memory) **refuses** with `NotSupportedException` before any mutation, and the predicate is never silently dropped; `IgnoreFilters()` restores the native upsert. A capability/form refusal happens before any read, while a query-sourced merge may first run its source pre-check read before a translation/render refusal. On the supported full-`MERGE` form every branch — including update-only and delete-only — validates its incoming source rows against the target filters and throws `QueryFilterException` before the mutation (see [Write-target isolation](../advanced/query-filters.md#write-target-isolation) and [INSERT and MERGE (validation)](../advanced/query-filters.md#insert-and-merge-validation)).

### Returning the merged rows

[`Returning()`](xref:NextORM.Core.MergeBuilder`1.Returning) and [`Returning(x => new { ... })`](xref:NextORM.Core.MergeBuilder`1.Returning``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) materialise the merged rows through the provider's output clause. Read them with `Single()`/`SingleAsync()`/`ToList()`/`ToListAsync()`:

```csharp
var rows = ctx.CreateMergeBuilder<IDest>()
    .Using(source)
    .OnKeys()
    .WhenMatched().ThenUpdate()
    .WhenNotMatched().ThenInsert()
    .Returning(x => new { x.Id, x.Name })
    .ToList();
```

```sql
-- SQL Server
merge into dest as target using (values (@p0, @p1)) as source (id, name) on target.id = source.id when matched then update set target.name = source.name when not matched then insert (id, name) values (source.id, source.name) output inserted.id, inserted.name;

-- PostgreSQL 17+
merge into dest as target using (values (@p0, @p1)) as source (id, name) on target.id = source.id when matched then update set name = source.name when not matched then insert (id, name) values (source.id, source.name) returning target.id, target.name;
```

Returning is available on **both** forms. SQL Server renders the portable key upsert as a
`MERGE ... OUTPUT inserted.<col>`, while PostgreSQL and SQLite append `RETURNING` to their
`ON CONFLICT ... DO UPDATE` form; the full `MERGE` returns through SQL Server `OUTPUT inserted.<col>`
and PostgreSQL 17+ `RETURNING target.<col>`. MySQL/MariaDB have no row-returning key-upsert form, so
`.Returning()` there throws `NotSupportedException`; SQLite and ClickHouse still reject the full
`MERGE` (and the server must be PostgreSQL 15+ for the general `MERGE` statement itself).

## Column assignments (expressions)

Both forms accept an explicit assignment set instead of the default "every non-key column, from the same-named source column" rule. A selector must be an anonymous initializer or a typed initializer assignable to the entity; a bare member, an empty initializer or a non-initializer body is rejected with `ArgumentException` (parameter `assignments`) before any SQL is built.

The overloads differ by lambda arity — three update forms, and one insert form next to the no-argument default:

```csharp
// key upsert, on MergeBuilder<TEntity>
public MergeBuilder<TEntity> WhenMatchedUpdate();
public MergeBuilder<TEntity> WhenMatchedUpdate(Expression<Func<TEntity, TEntity, object?>> assignments);         // target, source
public MergeBuilder<TEntity> WhenMatchedUpdate(Expression<Func<TEntity, TEntity, TEntity, object?>> assignments); // target, source, excluded
public MergeBuilder<TEntity> WhenNotMatchedInsert();
public MergeBuilder<TEntity> WhenNotMatchedInsert(Expression<Func<TEntity, object?>> assignments);                 // source only

// full MERGE, on the branch builders
public MergeBuilder<TEntity> ThenUpdate();
public MergeBuilder<TEntity> ThenUpdate(Expression<Func<TEntity, TEntity, object?>> assignments);
public MergeBuilder<TEntity> ThenUpdate(Expression<Func<TEntity, TEntity, TEntity, object?>> assignments);
public MergeBuilder<TEntity> ThenInsert(Expression<Func<TEntity, object?>> assignments);                          // source only
```

The initializer binds **by member name**: every initializer member is a mapped destination CLR property (`Name`, never the database column name), and the right-hand side is the value written to that destination. Only the members present are written — an omitted column keeps its database default on insert and its current value on update, there is no implicit "copy every other column from the source". A right-hand side is bound to the role of the **lambda parameter it is written on**, never inferred from a matching member name: in `new { Name = source.Name }` the read is the source role even though the destination is also called `Name`.

```csharp
// key upsert
ctx.CreateMergeBuilder<IDest>()
    .Using(new Dest { Id = 1, Name = "a", Age = 5 })
    .OnKeys()
    .WhenMatchedUpdate((target, source) => new { Name = source.Name, Age = target.Age + 1 })
    .WhenNotMatchedInsert(source => new { Id = source.Id, Name = source.Name })
    .Merge();

// full MERGE: the same overloads on the branch actions
ctx.CreateMergeBuilder<IDest>()
    .Using(source)
    .OnKeys()
    .WhenMatched().ThenUpdate((target, source) => new { Name = source.Name, Age = target.Age + 1 })
    .WhenNotMatched().ThenInsert(source => new { Id = source.Id, Name = source.Name })
    .Merge();
```

The left-hand side is always a mapped destination column; only the assigned destinations are written. Identity, computed and match-key destinations are refused with `NotSupportedException`, and so are unknown or duplicate destinations.

### Roles

The lambda parameters are roles, resolved by position — never by name:

| Role | Position | Meaning | Rendered reference |
|---|---|---|---|
| target | 1st | the target row as it exists before the update | the target alias (full `MERGE`) or the quoted table reference (native key upsert) |
| source | 2nd | the original input row handed to `Using` | `source.<col>` (full `MERGE`); a bound statement parameter read from the single materialized input row (native key upsert) |
| excluded | 3rd | the row the provider proposes for insertion | `excluded.<col>` (PostgreSQL, SQLite) or `VALUES(<col>)` (MySQL, MariaDB) |

* The third parameter is optional. Declaring it without reading it adds no role and is accepted.
* A used `excluded` role is refused on a form without a proposed row (the full `MERGE`, SQL Server's `MERGE` key upsert) with `NotSupportedException` naming the role.
* On a native key upsert, reading the **source** role — in the update arm or in an expression INSERT — requires exactly one materialized input row; a batch or a query source using that role is refused before execution (`... requires one materialized source row ...`). A declared-but-unused source role does not trigger the guard.
* An expression **INSERT** on a key upsert must assign every conflict key directly from its same-named original source property (`Id = source.Id`). A constant, a transformed expression or a different property is refused with `ArgumentException` and names the key. The full `MERGE` insert form is exempt from this key rule.
* An **insert** assignment sees the source row only. `ThenInsert`/`WhenNotMatchedInsert` take a single-parameter lambda, so a right-hand side that names a second role is a compile error rather than a silent substitution.

`excluded` is not a synonym for `source`, and the difference is observable. On a native key upsert, take a target row with `Count = 10`, an input row with `Count = 3`, and an insert assignment `Count = source.Count * 2` (so the provider proposes `Count = 6`):

| Update assignment | Reads | Stored value |
|---|---|---|
| `new { Count = target.Count + source.Count }` | pre-update target + original input | `13` (`10 + 3`) |
| `new { Count = target.Count + excluded.Count }` | pre-update target + proposed row | `16` (`10 + 6`) |

The proposed row is the one the engine itself would insert: it carries the insert-transformed values and, where the engine defines them, native defaults and trigger effects — which is exactly why it differs from the original input. nextorm never synthesises, recomputes or projects that row, and there is no cross-engine blanket guarantee about generated columns or triggers: what `excluded` contains is whatever the native form exposes (see the provider table below).

### Parameters and unsupported shapes

* Every constant and closure in an assignment right-hand side becomes a **named statement parameter** (`@p0`, `$p0`, ...), never an inlined literal; a CLR `null` stays a null parameter value. Supported scalar expressions stay SQL (`target.Age + 1`), and a parameter-free server function is not evaluated on the client.
* One statement-wide parameter sequence covers the conditions, the assignment right-hand sides and the `VALUES` rows; repeated execution binds the current closure/source values again.
* A right-hand side that is not a single-row scalar — a nested query, an aggregate, a lambda, a reference-typed object graph — is refused with `NotSupportedException`: `MERGE assignment for '<property>' cannot translate expression '<nodeKind>'.`
* Providers with no assignment rendering at all (ClickHouse, the in-memory context) fail closed with `Provider '<provider>' does not support MERGE assignment expressions.` The no-argument key upsert keeps working in memory.
* Under an active target filter, an expression `INSERT` is refused before execution on **every** supported assignment form and provider — the planned row is built from expressions and cannot be validated against the filter before the statement runs. `IgnoreFilters()` opts out explicitly (see [Write-target isolation](../advanced/query-filters.md#write-target-isolation)).

### Migrating from the removed column selector

The old one-parameter update selector (`ThenUpdate(x => new { x.Name })`, `WhenMatchedUpdate(x => new { x.Name })`, `WhenMatched().ThenUpdate(x => new { x.Name })`) is **removed**; this is an intentional breaking change. The replacement reads the source role explicitly:

```csharp
// before (does not compile)
.WhenMatched().ThenUpdate(x => new { x.Name })

// after
.WhenMatched().ThenUpdate((target, source) => new { Name = source.Name })
```

`ThenInsert(x => new { x.Name })` still compiles — the same signature is now read as the assignment `Name = source.Name`. A bare-member body (`ThenInsert(x => x.Name)`), an empty initializer or a non-initializer body is rejected at runtime with `ArgumentException` (parameter `assignments`): an insert assignment set must be an initializer.

## Inspecting the SQL

[`ToSql()`](xref:NextORM.Core.MergeBuilder`1.ToSql) renders the parameterised SQL a `Merge()` would execute, without opening a connection:

```csharp
var sql = ctx.CreateMergeBuilder<ISimpleEntity>()
    .Using(new SimpleEntity { Id = 1, Name = "a" })
    .OnKeys()
    .WhenMatchedUpdate()
    .WhenNotMatchedInsert()
    .ToSql();
```

```sql
-- PostgreSQL
insert into simple_entity (id, name) values (@p0, @p1) on conflict (id) do update set name = excluded.name
```

## Provider and capability summary

| Provider | Key upsert | Full `MERGE` | Assignments | `RETURNING`/`OUTPUT` | Notes |
|---|---|---|---|---|---|
| SQL Server | `MERGE ... USING (VALUES ...)` | yes | target + source (no proposed row) | `OUTPUT inserted.<col>` (key upsert and full) | every branch, including `WHEN NOT MATCHED BY SOURCE` |
| PostgreSQL | `ON CONFLICT ... DO UPDATE` | yes (server 15+) | target + source + `excluded` | `RETURNING` (key upsert 9.5+, full 17+) | `DO NOTHING`; no `BY SOURCE` |
| SQLite | `ON CONFLICT ... DO UPDATE` | — | target + source + `excluded` | `RETURNING` (key upsert, 3.35+) | key upsert only |
| MySQL | `ON DUPLICATE KEY UPDATE` | — | target + source + `VALUES(<col>)` | — | key upsert only; no `RETURNING` |
| MariaDB | `ON DUPLICATE KEY UPDATE` | — | target + source + `VALUES(<col>)` | — | key upsert only; `RETURNING` is not used for `ON DUPLICATE KEY` |
| In-memory | applied to the registered sequence | — | — (`NotSupportedException`; the no-argument upsert still applies) | — | key upsert only, no SQL |
| ClickHouse | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` | no engine-level upsert |

## Notes and limits

* **Values are parameters.** A source value is never inlined; constants become named parameters (`@p0`, `$p0`, ...) and travel with the `VALUES` rows, exactly like an `INSERT`. An assignment right-hand side follows the same rule: its constants and closures are bound parameters for the whole statement, not per affected row.
* **Branches run in declaration order.** SQL Server and PostgreSQL evaluate the `WHEN` clauses top to bottom; the first matching clause for a row applies.
* **A `WHEN NOT MATCHED BY SOURCE` condition is target-only on SQL Server.** SQL Server's `MERGE` does not expose a source alias in that clause, so the condition may only reference the target row.
* **Server-side query source.** `Using(ctx.From<T>().Where(...))` / `Using(QueryCommand<T>)` is accepted by the full-`MERGE` form only; the key-upsert providers fall back to a `VALUES` batch.
* **PostgreSQL 15+ is a server prerequisite.** nextorm does not discover or configure the server version and performs no version read; it renders the general `MERGE` from the dialect's capability flags alone, and an older server rejects the statement itself. There is no client-side "PostgreSQL < 15 refuses" guard.
* **Mutations are not prepared or plan-cached.** Optimisation in nextorm targets read-only queries only (`Prepare`, the implicit plan cache, benchmarks); a merge always renders and executes one command per call.
* **Affected-row count.** `Merge()`/`MergeAsync()` return the number of rows the provider reports.
* **Global query filters and the merge target.** A filter declared for the entity type is not injected
  through a source `FROM`; the incoming source rows of every branch of the supported full `MERGE`
  (insert, update-only and delete-only) are validated against the target's active filters before
  execution, and a violation raises `QueryFilterException`. Under an active filter
  the full `MERGE` additionally isolates the target in the statement (see
  [Write-target isolation](../advanced/query-filters.md#write-target-isolation)), while an unfilterable
  key upsert (`ON CONFLICT` / `ON DUPLICATE KEY` / in-memory) **refuses** with `NotSupportedException`;
  `IgnoreFilters` disables the filter from validation and restores the native upsert. See
  [INSERT and MERGE (validation)](../advanced/query-filters.md#insert-and-merge-validation).
* The in-memory provider is query-only: the full `MERGE` throws `NotSupportedException`; only the key-upsert merge is applied to the registered sequence in the context.

## See also

- [Data modification (INSERT)](17-insert-statement.md)
- [Data modification (DELETE)](18-delete-statement.md)
- [Data modification (UPDATE)](19-update-statement.md)
- [Optimistic concurrency and change tracking](26-optimistic-concurrency.md)
- [Global query filters](../advanced/query-filters.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)
- [Provider overview](../connecting-providers/overview.md)
- [API reference](../advanced/api-reference.md)
