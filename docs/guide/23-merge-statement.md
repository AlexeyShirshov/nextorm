# Data merging (`MERGE` / upsert)

> nextorm builds a `MERGE` (upsert) through [`MergeInto<TEntity>()`](xref:NextORM.Core.DataContextExtensions.MergeInto``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}})): one entry point covers both the portable **key upsert** (`INSERT ... ON CONFLICT` / `ON DUPLICATE KEY` / `MERGE`) and the general, multi-branch **full `MERGE`** with `WHEN MATCHED`/`WHEN NOT MATCHED` branches. There is no change tracking and no `SaveChanges`: every terminal issues exactly one command.

**Prerequisites:** [Data modification (INSERT)](19-insert-statement.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Provider overview](../providers/overview.md)

## Overview

`MergeInto<TEntity>()` writes a source row set into the target table and lets the database decide, row by row, what to do. The source is a mapped entity, a batch, or a server-side query; the match is either the declared key (`OnKeys()`) or an arbitrary condition (`On(...)`); and the actions are attached as branches. `Merge()`/`MergeAsync()` execute and return the affected-row count, while `ToSql()` renders the statement without a connection.

Two forms share the same builder:

* **key upsert** — `OnKeys()` + `WhenMatchedUpdate()` + `WhenNotMatchedInsert()`. Every SQL provider expresses it in its native form, and it is the only form the in-memory provider applies (to the registered sequence).
* **full `MERGE`** — `WhenMatched()`/`WhenNotMatched()`/`WhenNotMatchedBySource()`, each finished with `ThenUpdate`/`ThenInsert`/`ThenDelete`/`ThenDoNothing`. Native on SQL Server and PostgreSQL 15+; the other providers reject it.

## Upsert (key merge)

[`MergeInto<TEntity>()`](xref:NextORM.Core.DataContextExtensions.MergeInto``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}})) writes a source row set into the table and lets the database decide, per declared key, whether to update the existing row or insert a new one. The source is a single mapped entity or a batch, the match key is resolved from the entity mapping with `OnKeys()`, and both branches — `WhenMatchedUpdate()` (set every non-key writable column from the source) and `WhenNotMatchedInsert()` — are required:

```csharp
ctx.MergeInto<ISimpleEntity>()
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

`Merge()`/`MergeAsync()` return the number of affected rows. The key must be declared (`[Key]`/`.Key()`) and must not be database-generated; an entity with only key columns is rejected because there is nothing to update. This is the *key upsert* form; for arbitrary branches (including `DELETE`) see [Full MERGE](#full-merge).

## Full MERGE

[`WhenMatched()`](xref:NextORM.Core.MergeBuilder`1.WhenMatched) / [`WhenNotMatched()`](xref:NextORM.Core.MergeBuilder`1.WhenNotMatched) / [`WhenNotMatchedBySource()`](xref:NextORM.Core.MergeBuilder`1.WhenNotMatchedBySource) extend the same builder into a general, multi-branch `MERGE` on the providers that render it natively (SQL Server, PostgreSQL 15+). Each branch is finished with an action, and the branches run in the order they are declared:

```csharp
ctx.MergeInto<IDest>()
    .Using(source)                            // entity, batch, or a query: Using(ctx.From<IDest>().Where(...))
    .OnKeys()                                 // or .On((t, s) => t.Id == s.Id && s.Age > 0)
    .WhenMatched().ThenUpdate()               // or .WhenMatched((t, s) => t.Name != s.Name).ThenUpdate()
    .WhenNotMatched().ThenInsert()            // or .WhenNotMatched((t, s) => s.Age > 0).ThenInsert()
    .WhenNotMatchedBySource().ThenDelete()    // SQL Server only
    .Merge();
```

`ThenUpdate()`/`ThenInsert()` without a selector write every eligible column; an explicit selector (`x => new { x.Name }`) restricts the written columns, and the source values are the same-named columns of the source row. A selector may not name a database-generated column, and a matched branch may not rewrite the match key. `ThenDelete()` on a matched branch and `WhenNotMatchedBySource().ThenDelete()` let the merge remove rows — the difference from the key upsert. `ThenDoNothing()` leaves the candidate row unchanged; it is available on PostgreSQL only, because SQL Server has no `DO NOTHING` action.

`On(condition)` replaces the key match with an arbitrary search condition (`ON <condition>`). A branch builder also accepts a condition (`WhenMatched(condition)`, `WhenNotMatched(condition)`, `WhenNotMatchedBySource(condition)`), rendered as `WHEN ... AND <condition>`; a branch fires only when its condition holds. Conditions use the two-parameter form `(target, source)` — reference the existing row as the first lambda parameter and the incoming source row as the second, for example `On((t, s) => t.Id == s.Id && s.Age > 0)`. On SQL Server a `WHEN NOT MATCHED BY SOURCE` condition may only reference the target row.

| Provider | Full `MERGE` | `THEN DELETE` | `THEN DO NOTHING` | `ON` / `WHEN ... AND` | `WHEN NOT MATCHED BY SOURCE` |
|---|---|---|---|---|---|
| SQL Server | yes | yes | — | yes | yes |
| PostgreSQL | yes (15+) | yes | yes | yes | — |
| SQLite, MySQL, MariaDB | — (key upsert only) | — | — | — | — |
| In-memory | — (key upsert only) | — | — | — | — |
| ClickHouse | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` |

`Using(ctx.From<T>().Where(...))` or `Using(QueryCommand<T>)` supplies the rows from a server-side query (`USING (<select>) AS source`) instead of a `VALUES` batch; a query source is accepted by the full-`MERGE` form only.

### Returning the merged rows

[`Returning()`](xref:NextORM.Core.MergeBuilder`1.Returning) and [`Returning(x => new { ... })`](xref:NextORM.Core.MergeBuilder`1.Returning``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) materialise the merged rows through the provider's output clause. Read them with `Single()`/`SingleAsync()`/`ToList()`/`ToListAsync()`:

```csharp
var rows = ctx.MergeInto<IDest>()
    .Using(source)
    .OnKeys()
    .WhenMatched().ThenUpdate()
    .WhenNotMatched().ThenInsert()
    .Returning(x => new { x.Id, x.Name })
    .ToList();
```

Returning is available on **both** forms. SQL Server renders the portable key upsert as a
`MERGE ... OUTPUT inserted.<col>`, while PostgreSQL and SQLite append `RETURNING` to their
`ON CONFLICT ... DO UPDATE` form; the full `MERGE` returns through SQL Server `OUTPUT inserted.<col>`
and PostgreSQL 17+ `RETURNING target.<col>`. MySQL/MariaDB have no row-returning key-upsert form, so
`.Returning()` there throws `NotSupportedException`; SQLite and ClickHouse still reject the full
`MERGE` (and PostgreSQL 15+ only gained the statement itself).

## Inspecting the SQL

[`ToSql()`](xref:NextORM.Core.MergeBuilder`1.ToSql) renders the parameterised SQL a `Merge()` would execute, without opening a connection:

```csharp
var sql = ctx.MergeInto<ISimpleEntity>()
    .Using(new SimpleEntity { Id = 1, Name = "a" })
    .OnKeys()
    .WhenMatchedUpdate()
    .WhenNotMatchedInsert()
    .ToSql();
```

## Provider and capability summary

| Provider | Key upsert | Full `MERGE` | `RETURNING`/`OUTPUT` | Notes |
|---|---|---|---|---|
| SQL Server | `MERGE ... USING (VALUES ...)` | yes | `OUTPUT inserted.<col>` (key upsert and full) | every branch, including `WHEN NOT MATCHED BY SOURCE` |
| PostgreSQL | `ON CONFLICT ... DO UPDATE` | yes (15+) | `RETURNING` (key upsert 9.5+, full 17+) | `DO NOTHING`; no `BY SOURCE` |
| SQLite | `ON CONFLICT ... DO UPDATE` | — | `RETURNING` (key upsert, 3.35+) | key upsert only |
| MySQL | `ON DUPLICATE KEY UPDATE` | — | — | key upsert only; no `RETURNING` |
| MariaDB | `ON DUPLICATE KEY UPDATE` | — | — | key upsert only; `RETURNING` is not used for `ON DUPLICATE KEY` |
| In-memory | applied to the registered sequence | — | — | key upsert only, no SQL |
| ClickHouse | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` | no engine-level upsert |

## Notes and limits

* **Values are parameters.** A source value is never inlined; constants become named parameters (`@p0`, `$p0`, ...) and travel with the `VALUES` rows, exactly like an `INSERT`.
* **Branches run in declaration order.** SQL Server and PostgreSQL evaluate the `WHEN` clauses top to bottom; the first matching clause for a row applies.
* **A `WHEN NOT MATCHED BY SOURCE` condition is target-only on SQL Server.** SQL Server's `MERGE` does not expose a source alias in that clause, so the condition may only reference the target row.
* **Server-side query source.** `Using(ctx.From<T>().Where(...))` / `Using(QueryCommand<T>)` is accepted by the full-`MERGE` form only; the key-upsert providers fall back to a `VALUES` batch.
* **Mutations are not prepared or plan-cached.** Optimisation in nextorm targets read-only queries only (`Prepare`, the implicit plan cache, benchmarks); a merge always renders and executes one command per call.
* **Affected-row count.** `Merge()`/`MergeAsync()` return the number of rows the provider reports.
* The in-memory provider is query-only: the full `MERGE` throws `NotSupportedException`; only the key-upsert merge is applied to the registered sequence in the context.

## See also

- [Data modification (INSERT)](19-insert-statement.md)
- [Data modification (DELETE)](20-delete-statement.md)
- [Data modification (UPDATE)](21-update-statement.md)
- [Optimistic concurrency and change tracking](29-optimistic-concurrency.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)
- [Provider overview](../providers/overview.md)
- [API reference](../advanced/api-reference.md)
