# Comparisons: Nextorm vs Dapper, linq2db and EF Core

Use this section to choose a library based on the query features, database providers, and
change-tracking needs of your application. It compares Nextorm with the libraries it is most often
measured against; the results are a dated snapshot, not a universal ranking:

| Library | Profile |
|---|---|
| **Dapper** | Micro-ORM: hand-written SQL with object mapping; no LINQ and no change tracking. |
| **linq2db** | Mature LINQ-to-SQL ORM: associations, change tracking, a broad provider matrix and database-first tooling. |
| **Entity Framework Core** | Full ORM with change tracking, relationships, migrations and LINQ. |

Nextorm is a **query builder and mapper without change tracking**. It supports reporting and
analytic queries, plus explicit write commands against an existing schema. It generates
provider-portable SQL; the benchmark results cover only the shipped scenarios.

The comparison has four parts:

- **[Capabilities](capabilities.md)** — one construct-level matrix: what each library supports and where it
  does not.
- **[Provider features](provider-features.md)** — per-engine API breadth against linq2db.
- **[Benchmarks](benchmarks.md)** — the shipped SQLite scenarios, with links to the full BenchmarkDotNet
  reports.
- **[Feature benchmarks](feature-benchmarks.md)** — per-feature results grouped by query shape. This
  view uses Nextorm's implicit plan cache, omits `Prepare()` runs, and puts the libraries in
  columns.

## How to read the marks

- **yes** — the library exposes the feature directly through a documented API.
- **partial** — the feature has a named limitation, or it could be implemented but is not.
- **no** — the feature is not supported.

Marks are judgement calls against the **public documentation** of each library, as of **2026-10-04**. Where a
competitor has an open tracking issue for a gap, it is linked. A Nextorm cell names a restriction imposed by
the database engine when one exists; that does not lower the mark. This is a summary — the [guide](../engine/index.md)
and the [API reference](../advanced/api-reference.md) are authoritative for Nextorm, and each competitor's own
documentation is authoritative for that competitor.

## Scope and fairness

- Capability marks describe the library, not a specific database. Per-provider restrictions are noted inline.
- Benchmarks use one machine and one provider (SQLite). Treat them as a signal for the listed
  scenarios, not a guarantee about another workload; see the [benchmarks](benchmarks.md) page for
  methodology and caveats.
- The comparison focuses on **overlap**. Relationship modelling and change tracking are deliberate
  boundaries in Nextorm, not missing implementations of features it claims to support.

## Where Nextorm leads

Across the overlapping constructs in the matrix, Nextorm matches or exceeds the three libraries. The
following points highlight additional features; provider-specific restrictions still apply where
noted:

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
- **Extensibility through query components.** A third party can add a structural operator that
  changes
  generated SQL, such as an added predicate or a reshaped row source. The new operator keeps the
  ordinary
  query's plan cache, parameter binding, and provider independence. It needs no friend access,
  global
  registry, or raw SQL. linq2db and EF Core expose interceptors and function mappings, but no
  structural
  component contract integrated with the plan cache.
- **Zero-config NativeAOT migration.** In the verified surface—all six providers on .NET 11 /
  `linux-x64`—the only consumer change is `PublishAot=true`. No compiled model, precompilation step,
  analyzer/interceptor opt-in, entity or query registration, linker descriptor, or root is needed.
  EF Core requires a compiled model and interceptor-based query precompilation, and does not support
  dynamically composed queries. Dapper requires the `Dapper.AOT` package, `[module: DapperAot]`, an
  `InterceptorsNamespaces` property, and statically known SQL. linq2db does not officially support
  NativeAOT.

  On a 13 GB SQLite reporting app, Nextorm published natively with no source changes (8.2 MB binary,
  0.12 s, 30 MB RSS); EF Core published at 17.3 MB / 0.23 s / 42 MB after two code-generation
  workarounds. NativeAOT supports representative query, write, and raw SQL scenarios across all six providers.
  The EF Core bridge requires JIT. The advantage is migration cost,
  not full AOT feature parity. See the [NativeAOT guide](../infrastructure/05-nativeaot.md).
- **Benchmarked performance**: fastest on every end-to-end scenario, fastest on every prepared feature
  (with the smallest allocations on all but the IN-list), and ahead of regular linq2db and EF Core on
  the warm path (see [Benchmarks](benchmarks.md)).

## Growth points

The in-scope work still ahead is separate from deliberate boundaries. This distinction helps you
tell an implementation gap from a feature Nextorm does not aim to provide:

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
  `JoinInto` joins and groups on every declared key component, composite foreign, principal and junction
  keys included, while loading a many-to-many graph in a single eager-loading query remains out of scope.
- **Deeper query forms**: `SelectMany`/`GroupJoin` are not translated on the SQL providers, `APPLY`/`LATERAL`
  cannot take a join projection, and in-memory correlation stops at depth one.
- **Dynamic SQL assembly**: building SQL at runtime stays outside the builder surface.
- **Provider-gated native types**: native range/multirange, native JSON columns and arrays are not available
  on every provider; the others fall back to the pair/text mappings or reject them.
- **Provider breadth**: fewer providers than linq2db and EF Core (no Oracle, Firebird, DB2, SAP HANA,
  Informix, Sybase or SQL CE).

Relationship modelling, change tracking/`SaveChanges`, migrations and database-first scaffolding are
**deliberate boundaries**, not growth points — they are listed in
[Limitations and out-of-scope features](../advanced/limitations.md). Two composition boundaries are of the
same kind: a correlated `APPLY` extends an existing join chain but stops at eight total sources, and the
in-memory provider stops correlated subqueries at depth one, while every SQL provider nests them
arbitrarily. `APPLY`/`LATERAL` itself needs a lateral source: MariaDB 11.4, SQLite and ClickHouse refuse it
before execution, and the in-memory provider does not support it. These are API-shape and provider
boundaries, not missing SQL capabilities — the supported forms are covered by the
[capabilities](capabilities.md) matrix and the guide.

## Summary

For reading, reporting, and explicit data changes over an existing schema, consider Nextorm when its
supported query features and provider restrictions fit your application. It provides portable SQL
across SQL Server, PostgreSQL, MySQL/MariaDB, SQLite, and ClickHouse, a broad query surface, and
explicit writes without change-tracking overhead. On the shipped scenarios, its benchmark results
are at or above Dapper, EF Core, and linq2db; those measurements use SQLite and do not predict every
workload.

Within the verified NativeAOT surface, Nextorm requires only `PublishAot=true`; EF Core requires a
compiled model and query precompilation, while Dapper requires the `Dapper.AOT` package and an
interceptor opt-in. Dapper remains the lightest option for hand-written SQL with object mapping.
Choose linq2db or EF Core when the same layer also needs to model relationships, track changes, or
generate the data layer from a live schema—features Nextorm deliberately leaves out.
