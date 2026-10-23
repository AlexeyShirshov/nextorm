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

The comparison has four parts:

- **[Capabilities](capabilities.md)** — one construct-level matrix: what each library supports and where it
  does not.
- **[Provider features](provider-features.md)** — per-engine API breadth against linq2db.
- **[Benchmarks](benchmarks.md)** — the shipped SQLite scenarios, with links to the full BenchmarkDotNet
  reports.
- **[Feature benchmarks (2026-10-17)](feature-benchmarks-2026-10-17.md)** — a warm-path-only, tiered
  report: no `Prepare()` arms, one table per tier, competitors as columns.

## How to read the marks

- **yes** — first-class support.
- **partial** — supported with a named limitation, or implementable but not implemented yet.
- **no** — not supported.

Marks are judgement calls against the **public documentation** of each library, as of **2026-10-04**. Where a
competitor has an open tracking issue for a gap, it is linked. A Nextorm cell names a restriction imposed by
the database engine when one exists; that does not lower the mark. This is a summary — the [guide](../engine/index.md)
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
- **Benchmarked performance**: fastest on every end-to-end scenario, fastest on every prepared feature
  (with the smallest allocations on all but the IN-list), and ahead of regular linq2db and EF Core on
  the warm path (see [Benchmarks](benchmarks.md)).

## Growth points

The honest balance to the section above — the in-scope work still ahead, kept separate from the deliberate
boundaries:

- **Warm-path performance**: the implicit plan cache still trails Dapper by roughly 1.32–1.75× on the
  4-table join, CTE, recursive CTE and IN-list (inline as well as captured), and its allocations stay
  above the prepared path; closing that gap is ongoing (see [Benchmarks](benchmarks.md)).
- **EF Core integration**: the `ToNextOrm` adapter translates only a bounded operator subset —
  `Select`, `Include`/navigations, `Join`, `GroupBy`,
  `EF.Property`/`EF.Functions`, `AsSplitQuery`, raw-SQL roots and subqueries are not translated — and the
  MariaDB shared-connection and ClickHouse no-EF-adapter slices remain open (see
  [EF Core integration](../advanced/integration-efcore.md)).
- **Eager loading and relationships**: `LoadWith` eager loading covers finite nested graphs (level 2+,
  with a single graph-wide mode and per-level ordering/top-N) in split and expressible single-query;
  `JoinInto` still rejects composite keys, composite many-to-many junction selectors and many-to-many
  under a single query.
- **Provider breadth**: fewer providers than linq2db and EF Core (no Oracle, Firebird, DB2, SAP HANA,
  Informix, Sybase or SQL CE).

Relationship modelling, change tracking/`SaveChanges`, migrations and database-first scaffolding are
**deliberate boundaries**, not growth points — they are listed in
[Limitations and out-of-scope features](../advanced/limitations.md). Two composition boundaries are of the
same kind: a correlated `APPLY` takes a single-entity source rather than an existing join projection, and
the in-memory provider stops correlated subqueries at depth one, while every SQL provider nests them
arbitrarily. These are API-shape choices, not missing SQL capabilities — the supported forms are covered by
the [capabilities](capabilities.md) matrix and the guide.

## Summary

For reading, reporting and explicit data modification over an existing schema, Nextorm is the stronger
choice: portable SQL across SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse, a broadly
complete query surface, an explicit write surface with no change-tracking overhead, and benchmark results
at or above Dapper, EF Core and linq2db on the shipped scenarios. Dapper stays the lightest option for
hand-written SQL with object mapping; linq2db and EF Core remain the better fit when the same layer must
also model relationships, track changes or generate the data layer from a live schema — surface Nextorm
deliberately leaves out.
