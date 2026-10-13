# Execution and rows

> What happens after SQL exists: the `DbCommand` is executed, the `DbDataReader` is read, and the result is shaped — materialized into objects, streamed, serialized to JSON/CSV, or handed back raw. See the [engine overview](index.md) for the whole picture.

## Executing the command

The prepared SQL text is bound to a provider `DbCommand` (created by `DataContext.CreateCommand`, with the parameters added in order) and run by `QueryExecutor` through ADO.NET `ExecuteReader`. The command and the resulting `DbDataReader` are owned together (a `CommandReaderOwner`), so the reader and its command are disposed as one unit.

## Materialization

When the projection needs objects, a row mapper compiled by `RowMapperFactory` reads each row from the `DbDataReader` and builds the entity, DTO, record, tuple or anonymous type — the shapes are covered under [Projections](../guide/30-projections.md). The mapper is compiled once per plan shape and reused for every row.

## Terminals and their output shapes

| Terminal | Output |
|---|---|
| `ToList` / `ToArray`, `First` / `FirstOrDefault`, `Single`, `Any`, `ExecuteScalar` | Buffered: rows are materialized eagerly into a list or a scalar. |
| `ToAsyncEnumerable`, `Pipeline`, `ToEnumerable` | Streamed: rows are materialized lazily as you iterate. |
| `WriteJson` / `WriteCsv` | Serialized: rows are written straight from the reader to JSON or CSV with **no materialization**. |
| `ToDataReader` | Raw: the underlying `DbDataReader` is returned to the caller, who owns and disposes it. |

The buffered and streamed terminals both reuse the same plan; only the timing of materialization differs. The serializing terminals bypass the row mapper entirely.

## In-memory

The [in-memory provider](../connecting-providers/in-memory.md) produces the same output shapes without a `DbDataReader`: the compiled query enumerates the in-memory data set and the materializer builds the objects directly.

## See also

* [Engine overview](index.md)
* [Query pipeline](01-query-pipeline.md)
* [Plan cache and reuse](03-plan-cache-and-reuse.md)
* [Projections](../guide/30-projections.md)
* [Streaming large objects](../guide/26-large-objects.md)
