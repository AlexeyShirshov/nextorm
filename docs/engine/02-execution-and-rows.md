# Execution and rows

> After SQL generation, a terminal determines how the result is consumed. `QueryExecutor` runs the `DbCommand`, the `DbDataReader` exposes rows, and the terminal returns them as objects or collections, a single row or aggregate, a stream, JSON/CSV, or a raw reader or stream. See the [engine overview](index.md) for the whole route.

## Executing the command

`DataContext.CreateCommand` creates the provider's `DbCommand` and binds the prepared SQL text and parameters in order. `QueryExecutor` runs it through ADO.NET `ExecuteReader`, which returns a `DbDataReader` over the rows. A `CommandReaderOwner` owns the command and reader together, so they are disposed as one unit.

## Materialization

When the projection needs objects, `RowMapperFactory` compiles a row mapper that reads each row from the `DbDataReader` and builds an entity, DTO, record, tuple, or anonymous type. See [Projections](../guide/02-projections.md) for those shapes. NextORM compiles the mapper once per plan shape and reuses it for every row.

A nullable aggregate scalar is materialized as `null`. The four variability aggregates (`Stdev`/`Stdevp`/`Var`/`Varp`) are the first aggregates that return `T?`. For a nullable value-type projection, the mapper checks `IsDBNull` before calling the value getter, so SQL `NULL` reaches the caller as `null` instead of causing an exception.

The terminal's declared `T?` and the nullable projection are one decision. `AggregateTerminalRewriter` projects the aggregate as `Nullable<T>` whenever the aggregate's parameter is `T?`.

## Terminals and their output shapes

Choose a terminal by the result shape you need:

| Terminal | Output |
|---|---|
| `ToList` / `ToArray` / `ToHashSet` / `ToDictionary` | **Buffered collection**: every row is materialized into a list, array, set or dictionary. |
| `First` / `FirstOrDefault`, `Single` / `SingleOrDefault`, `Last` / `LastOrDefault` | **Single row**: one materialized row (or `default`). |
| `Count` / `LongCount`, `Sum` / `Avg` / `Min` / `Max`, `Stdev` / `Stdevp` / `Var` / `Varp` | **Aggregate**: the query is rewritten to a scalar `SELECT` (`COUNT(*)`, `SUM(...)`, ...) and one value comes back; nothing is materialized per row. |
| `Any` / `ExecuteScalar` | **Scalar check or value**: a `bool`, or the value of the single projected column. |
| `ToEnumerable` / `ToAsyncEnumerable` / `Pipeline` | **Streamed**: rows are materialized lazily as you iterate. |
| `WriteJson` / `WriteCsv` | **Serialized**: rows are written straight from the reader to JSON or CSV with **no materialization**. |
| `ToDataReader` / `ToStream` / `ToTextReader` | **Raw**: the underlying `DbDataReader`, `Stream` or `TextReader` is handed to the caller, who owns and disposes it. |
| `PrepareBuffered` / `PrepareStreaming` | **Compile without executing**: return separate interfaces for buffered and streaming reads (see [plan cache and reuse](03-plan-cache-and-reuse.md)). |

All terminals have synchronous and asynchronous forms (`ToList`/`ToListAsync`, `ToEnumerable`/`ToAsyncEnumerable`, `WriteJson`/`WriteJsonAsync`, ...), except `PrepareBuffered` and `PrepareStreaming`, which have no asynchronous twins. All terminals except `PrepareBuffered` take query parameters positionally.

Buffered and streamed terminals reuse the same plan; they differ in when rows are materialized. Serializing terminals bypass the row mapper entirely.

Aggregate terminals are declared on the entity builder (`ctx.From<T>()`) and take the entity selector; `Count()` and `LongCount()` take no selector. `ToDataReader`, `ToStream`, `ToTextReader`, and `Pipeline` are declared on the `QueryCommand<TResult>` returned by `Select`.

Mutation builders (`CreateInsertBuilder`/`CreateUpdateBuilder`/`CreateDeleteBuilder`/`CreateMergeBuilder`/`CreateBulkInsertBuilder`) follow the same distinction from the other side. `ToSql()` renders a statement without executing it; `Insert()`/`Update()`/`Delete()`/`Merge()`/`BulkInsert()` execute it. See the Guide pages linked from [builder entry points](index.md#builder-entry-points).

## In-memory

The [in-memory provider](../connecting-providers/in-memory.md) produces the same output shapes without a `DbDataReader`: the compiled query enumerates the in-memory data set and the materializer builds the objects directly.

## See also

* [Engine overview](index.md)
* [Query pipeline](01-query-pipeline.md)
* [Plan cache and reuse](03-plan-cache-and-reuse.md)
* [Projections](../guide/02-projections.md)
* [Grouping and aggregates](../guide/05-grouping-and-aggregates.md) — aggregate selection and the `Count`/`Sum`/`Avg`/... terminals.
* [Streaming large objects](../guide/28-large-objects.md)
