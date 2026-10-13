# How the engine works

> A high-level tour of what sits between a fluent `From<T>()...Select(...)` call and the rows you get back: the query command, the pipeline that turns it into SQL, the plan cache, execution and row materialization. For step-by-step usage see the [Guide](../guide/01-filtering-where.md); for the caching rules see [Plan cache and reuse](03-plan-cache-and-reuse.md).

**Prerequisites:** [Quickstart](../getting-started/02-quickstart.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md).

## The pipeline at a glance

Every query follows the same shape:

- `ctx.From<TEntity>()` returns an [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1); the fluent operators (`Where`, `Join`, `Select`, ...) accumulate into a [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1).
- A terminal (`ToListAsync`, `FirstAsync`, `AnyAsync`, ...) drives **preparation**: the command is turned into a structural `QueryPlan` key and looked up in the plan cache.
- On a cache **miss** the query is prepared, translated to SQL text plus parameters, and paired with a compiled row mapper and a `DbCommand`; the plan is stored back in the cache.
- The command is executed against the database, and the `DbDataReader` is turned into result rows by the row mapper or streamed straight through.

| Piece | Responsibility |
|---|---|
| [`IDataContext`](xref:NextORM.Core.IDataContext) / provider `DataContext` | Connection, metadata, dialect, command creation, and the entry point for builders. |
| [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1) | Fluent LINQ surface (`From<T>()`, `Where`, `Join`, `Select`, ...). |
| [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) | The built query — source, projection, filters, joins, sorting, hints — and its terminals. |
| `QueryPreparer` | Turns the expression tree into prepared clause pieces and plan hashes (via expression visitors). |
| `QueryPlan` / `QueryPlanStore` | The structural plan key and the per-thread implicit plan cache. |
| `SqlBuilder` + `ISqlDialect` | Renders the dialect-specific SQL text and the parameter list. |
| `RowMapperFactory` / `RowMaterializer` | Compiles the row mapper (`IDataRecord` → entity / DTO). |
| `DbCommand` (via `DataContext.CreateCommand`) | The provider command carrying the SQL text and the bound parameters. |
| `QueryExecutor` | Runs the command and owns the ADO.NET reader. |
| `DbDataReader` | The ADO.NET reader over the result set. |
| Terminals | Buffered, streamed, JSON/CSV, or raw-reader outputs. |

## Command execution path

The diagram traces the full path — from the fluent builder through the plan cache and SQL generation to execution and the row mapper:

![Command execution path: EntityBuilder → QueryCommand → QueryPlan → plan-cache lookup → QueryExecutor → DbDataReader → RowMapperFactory → buffered/streamed/scalar/serializing/raw terminals; on a cache miss QueryPreparer → SqlBuilder generate the SQL, and QueryPlanStore backs the lookup.](../assets/diagrams/nextorm-command-execution.svg)

## Explicit `Prepare()`

The second diagram shows the explicit reuse path, where the query is compiled once into an [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1) and executed any number of times — the plan cache is not involved:

![Explicit Prepare: EntityBuilder → QueryCommand.Prepare() → GetPreparedQueryCommand → QueryPreparer → SqlBuilder, which forks into RowMapperFactory and CreateCommand, both merged into IPreparedQueryCommand<TResult>; the compiled command is then executed N times through prepared.ToList(db)/First(db) → QueryExecutor → DbDataReader → result rows, while QueryPlanStore is bypassed.](../assets/diagrams/nextorm-command-execution-prepared.svg)

## In this section

* [Query pipeline: fluent to SQL](01-query-pipeline.md) — the command, preparation, the plan key and SQL generation.
* [Execution and rows](02-execution-and-rows.md) — running the command and shaping the result.
* [Plan cache and reuse: `Prepare`](03-plan-cache-and-reuse.md) — the implicit cache and explicit prepared commands.

## See also

* [Filtering (WHERE)](../guide/01-filtering-where.md)
* [Connections](../infrastructure/01-connections.md)
* [Provider overview](../connecting-providers/overview.md)
* [In-memory provider](../connecting-providers/in-memory.md)
