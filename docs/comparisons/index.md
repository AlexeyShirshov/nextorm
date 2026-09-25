# Comparisons: Nextorm vs Dapper, linq2db and EF Core

This section is a high-level, **dated** comparison of Nextorm with the libraries it is most often measured
against:

| Library | Profile |
|---|---|
| **Dapper** | Micro-ORM: hand-written SQL with object mapping; no LINQ and no change tracking. |
| **linq2db** | Mature LINQ-to-SQL ORM: associations, change tracking, a broad provider matrix and database-first tooling. |
| **Entity Framework Core** | Full ORM with change tracking, relationships, migrations and LINQ. |

Nextorm is a **query builder and mapper without change tracking**: it covers the analytic query surface and an
explicit write surface, generates provider-portable SQL, and is benchmarked on the shipped scenarios.

The comparison has two parts:

- **[Capabilities](capabilities.md)** — a category-level matrix: what each library supports and where it
  does not.
- **[Benchmarks](benchmarks.md)** — the shipped SQLite scenarios, with links to the full BenchmarkDotNet
  reports.

## How to read the marks

- **yes** — first-class support.
- **partial** — supported with a named limitation, or implementable but not implemented yet.
- **no** — not supported.

Marks are judgement calls against the **public documentation** of each library, as of **2026-09-25**. Where a
competitor has an open tracking issue for a gap, it is linked. A Nextorm cell names a restriction imposed by
the database engine when one exists; that does not lower the mark. This is a summary — the [guide](../guide/01-querying-and-projections.md)
and the [API reference](../advanced/api-reference.md) are authoritative for Nextorm, and each competitor's own
documentation is authoritative for that competitor.

## Scope and fairness

- Capability marks describe the library, not a specific database. Per-provider restrictions are noted inline.
- Benchmarks are micro-benchmarks on one machine and one provider (SQLite); they are a point-in-time signal,
  not a guarantee. The methodology and caveats are on the [benchmarks](benchmarks.md) page.
- The comparison is intentionally about **overlap**. Relationship modelling and change tracking are
  deliberately outside Nextorm's scope and are listed as such rather than counted as gaps.

## Where Nextorm leads

Across the shared surface Nextorm matches or exceeds the three libraries, and on top of that it adds:

- **A complete analytic query surface**: every join type including `APPLY`/`LATERAL`, derived-table joins,
  ClickHouse join strictness/`GLOBAL`, `DISTINCT` (plus `DISTINCT ON`/`WITH TIES`/`TABLESAMPLE`), CTEs
  (recursive, and data-modifying on PostgreSQL), window functions (named windows, `GROUPS`, frame `EXCLUDE`),
  `ROLLUP`/`CUBE`/`GROUPING SETS`, temporal tables (`FOR SYSTEM_TIME`), row locking and
  statement/table/index hints.
- **A complete explicit write surface** with no change tracking: `INSERT`/`UPDATE`/`DELETE`/full `MERGE`,
  bulk insert (`COPY`/`SqlBulkCopy` or chunked `VALUES`), `CREATE TABLE AS SELECT`/temporary tables and
  transactions — all explicit commands, no `SaveChanges`.
- **More of the type and mapping surface**: native `Range<T>` (and the `[RangeColumns]` pair mapping the
  others lack), arrays and higher-order array functions, row values/tuples, dynamic result schemas,
  captured-collection lookup and an optional mapped entity type.
- **Configurable SQL output**: opt-in identifier quoting, naming conventions and keyword casing,
  overridable per command — whereas linq2db quotes by default and fixes names in its mapping schema.
- **Benchmarked performance**: fastest on every measured scenario with the smallest allocations on the
  prepared path, and ahead of regular linq2db and EF Core on the warm path (see [Benchmarks](benchmarks.md)).

## Growth points

The honest balance to the section above — the in-scope work still ahead, kept separate from the deliberate
boundaries:

- **Warm-path performance**: the prepared path is fastest on every measured scenario, but the implicit plan
  cache still trails Dapper by roughly 1.15–1.6× on CTE, recursive CTE, 4-table join and captured IN-list
  (inline IN-list is level); closing that gap is ongoing (see [Benchmarks](benchmarks.md)).
- **EF Core integration**: planned, not shipped — there is no `linq2db.EntityFrameworkCore`-style package yet.
- **Stored procedures, dynamic SQL and multiple result sets**: raw SQL covers a whole query, but calling a
  stored procedure and materialising several result sets in order are deferred.
- **Deeper query forms**: `SelectMany`/`GroupJoin` work on the in-memory provider only, `APPLY`/`LATERAL`
  cannot take a join projection, and in-memory correlation stops at depth one.
- **Provider-gated native types**: native range/multirange, arrays and native JSON columns are
  PostgreSQL-centric; the other providers use the pair/text mappings or reject them.
- **Provider breadth**: fewer providers than linq2db and EF Core (no Oracle, Firebird, DB2, SAP HANA,
  Informix, Sybase or SQL CE).

Relationship modelling, change tracking/`SaveChanges`, migrations and database-first scaffolding are
**deliberate boundaries**, not growth points — they are listed in
[Limitations and out-of-scope features](../advanced/limitations.md).

## Summary

For reading, reporting and explicit data modification over an existing schema, Nextorm is the stronger
choice: portable SQL across SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse, a broadly
complete query surface, an explicit write surface with no change-tracking overhead, and benchmark results
at or above Dapper, EF Core and linq2db on the shipped scenarios. Dapper stays the lightest option for
hand-written SQL with object mapping; linq2db and EF Core remain the better fit when the same layer must
also model relationships, track changes or generate the data layer from a live schema — surface Nextorm
deliberately leaves out.
