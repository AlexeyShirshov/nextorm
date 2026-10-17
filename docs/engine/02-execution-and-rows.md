# Execution and rows

> What happens after SQL exists: the `DbCommand` is executed, the `DbDataReader` is read, and the result is shaped — materialized into objects or collections, collapsed to a single row or an aggregate, streamed, serialized to JSON/CSV, or handed back raw. See the [engine overview](index.md) for the whole picture.

## Executing the command

The prepared SQL text is bound to a provider `DbCommand` (created by `DataContext.CreateCommand`, with the parameters added in order) and run by `QueryExecutor` through ADO.NET `ExecuteReader`. The command and the resulting `DbDataReader` are owned together (a `CommandReaderOwner`), so the reader and its command are disposed as one unit.

## Materialization

When the projection needs objects, a row mapper compiled by `RowMapperFactory` reads each row from the `DbDataReader` and builds the entity, DTO, record, tuple or anonymous type — the shapes are covered under [Projections](../guide/02-projections.md). The mapper is compiled once per plan shape and reused for every row.

## Terminals and their output shapes

| Terminal | Output |
|---|---|
| `ToList` / `ToArray` / `ToHashSet` / `ToDictionary` | **Buffered collection**: every row is materialized into a list, array, set or dictionary. |
| `First` / `FirstOrDefault`, `Single` / `SingleOrDefault`, `Last` / `LastOrDefault` | **Single row**: one materialized row (or `default`). |
| `Count` / `LongCount`, `Sum` / `Avg` / `Min` / `Max`, `Stdev` / `Stdevp` / `Var` / `Varp` | **Aggregate**: the query is rewritten to a scalar `SELECT` (`COUNT(*)`, `SUM(...)`, ...) and one value comes back; nothing is materialized per row. |
| `Any` / `ExecuteScalar` | **Scalar check or value**: a `bool`, or the value of the single projected column. |
| `ToEnumerable` / `ToAsyncEnumerable` / `Pipeline` | **Streamed**: rows are materialized lazily as you iterate. |
| `WriteJson` / `WriteCsv` | **Serialized**: rows are written straight from the reader to JSON or CSV with **no materialization**. |
| `ToDataReader` / `ToStream` / `ToTextReader` | **Raw**: the underlying `DbDataReader`, `Stream` or `TextReader` is handed to the caller, who owns and disposes it. |
| `Prepare` | **Compiled, not executed**: returns an `IPreparedQueryCommand<TResult>` (see [plan cache and reuse](03-plan-cache-and-reuse.md)). |

All terminals come in synchronous and asynchronous form (`ToList`/`ToListAsync`, `ToEnumerable`/`ToAsyncEnumerable`, `WriteJson`/`WriteJsonAsync`, ...); `Prepare` is the only one without an asynchronous twin. Apart from `Prepare`, they take the query parameters positionally. The buffered and streamed terminals reuse the same plan and differ only in when rows are materialized; the serializing terminals bypass the row mapper entirely. The aggregate terminals are declared on the entity builder (`ctx.From<T>()`), where they take the entity selector — `Count()`/`LongCount()` take none — while `ToDataReader`, `ToStream`, `ToTextReader` and `Pipeline` are declared on the `QueryCommand<TResult>` that `Select` returns.

The mutation builders (`CreateInsertBuilder`/`CreateUpdateBuilder`/`CreateDeleteBuilder`/`CreateMergeBuilder`/`CreateBulkInsertBuilder`) expose the same idea from the other side: `ToSql()` renders the statement without executing it, while `Insert()`/`Update()`/`Delete()`/`Merge()`/`BulkInsert()` are their executing terminals — see the Guide pages linked from [builder entry points](index.md#builder-entry-points).

## In-memory

The [in-memory provider](../connecting-providers/in-memory.md) produces the same output shapes without a `DbDataReader`: the compiled query enumerates the in-memory data set and the materializer builds the objects directly.

## See also

* [Engine overview](index.md)
* [Query pipeline](01-query-pipeline.md)
* [Plan cache and reuse](03-plan-cache-and-reuse.md)
* [Projections](../guide/02-projections.md)
* [Grouping and aggregates](../guide/05-grouping-and-aggregates.md) — aggregate selection and the `Count`/`Sum`/`Avg`/... terminals.
* [Streaming large objects](../guide/28-large-objects.md)
