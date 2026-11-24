# FileContext provider design

**Date:** 2026-11-24 · **Milestone:** 1.0.12 · **Issue:** [#50](https://github.com/AlexeyShirshov/nextorm/issues/50)

**Status:** Written design approved by the user on 2026-11-24. A separate implementation plan is next for review; none has been created or approved. Implementation is not authorized and has not begun.

## Intent and approach

FileContext lets applications query tabular files through typed NextORM queries. It is a read-only provider, not `WriteCsv`, an export pipeline, or an application-side loader that feeds `InMemory`.

The user chose provider-specific execution over an additional common operator plan with two backends, FileContext and WebContext. A common plan could share validation, but it would couple local file execution to remote service translation and its different semantics. Use one file-specific query execution path with two row readers, not two local query engines. Reuse existing NextORM APIs and structures where useful; do not introduce a shared nonrelational planner, model, or executor. No common-core redesign is a precondition. The approved [Valkey #310](https://github.com/AlexeyShirshov/nextorm/issues/310) and [Mongo #411](https://github.com/AlexeyShirshov/nextorm/issues/411) designs are precedents, not dependencies or implementations for this feature.

## Agreed v1 contract

- The source is a filesystem path, not a public input `Stream`. Use typed `From<T>()` only. Column types come from the CLR model, not schema inference or a dynamic schema.
- CSV requires headers in the first row. Map columns by header name, not ordinal, so reordering columns preserves results.
- Excel input is XLSX only, not XLS. Select one worksheet explicitly. Read its first row as headers and the following rows as tabular data; Excel Tables and ranges are not query sources. Read cached formula results without evaluating formulas. If a formula has no cached result, report an error with the worksheet and cell address rather than fabricating null or zero.
- Reject `Insert`, `Update`, and `Delete`. Reject joins and correlated subqueries before reading the file, without fallback. Support independent scalar subqueries, including the aggregate example below; this does not imply support for all SQL-style subqueries.
- The smoke surface includes `Where`, typed `OrderBy`, `Take`, `ToList`, streaming, and the aggregate needed for the scalar example. This is not a full operator or file-format compatibility promise.

For example, given a typed numeric `Amount` column, a query can return positive amounts in ascending order and take the first two; `2` sorts before `10`. A second query can stream rows whose `Amount` exceeds the mean across the source. Compute that independent scalar aggregate once, then stream matching rows; the provider can reopen the file for the second pass.

## Execution defaults and planning latitude

`ToList` materializes the final result, not necessarily the whole source. Streaming must not accumulate all source or output rows in memory, including for an ordered stream without `Take`. An unsorted file can require a complete pass before the first ordered result. For large ordered streams, buffer sorted runs on scratch disk and merge them. The memory bound applies to reading XLSX too, including workbook structures, not just to the sort. Clean up scratch files on completion, error, early disposal, and cancellation.

Planning selects and records the readers and libraries, memory and spill thresholds, scratch layout, and parsing details. These are implementation choices, not fixed dependencies or user decisions; the chosen approach must satisfy the file and memory contracts.

## Acceptance evidence

- Use logically identical CSV and XLSX fixtures to verify filtering, numeric ordering (`2` before `10`), `Take`, materialized results, and streamed results. Reorder named columns and verify the same results.
- Check scratch cleanup on completion, injected failure, early disposal, and cancellation.
- Verify the above-mean query returns the expected rows in both formats and evaluates the independent aggregate once.
- Verify that joins and correlated subqueries fail before file reads and that writes are denied. Check rejection of XLS and other out-of-scope source shapes, including Excel Tables and ranges.
- Verify that a cached formula result is read as data and that a missing cache reports the worksheet and cell without substituting a value.

**Bounded-memory acceptance.** With fixed maximum decoded row and cell widths and a fixed reader/cache/sort working budget, retained provider input and sort state must have a bound independent of row count, XLSX shared-string and style cardinality, and the number of spilled runs. Use bounded merge fan-in and on-disk indexes if needed; include workbook and spill bookkeeping in the evidence. For both CSV and XLSX, test unordered streams and complete sorted streams without `Take`, using two materially different input sizes above the same budget and with the same width limits. Include high-cardinality XLSX strings, force and observe spill and merge on the sorted path, and consume and discard each row without collecting output. Use instrumentation or equivalent defensible evidence to verify the same configured bound on both sizes: the working budget plus documented fixed reader and merge overhead and one row. Caller-retained `ToList` results are excluded. Planning sets the fixture sizes, budget, and measurement method; this is not a universal RSS or MB guarantee or a whole-process allocation threshold.
