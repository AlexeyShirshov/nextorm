# Provider overview

> A provider supplies a dialect (SQL text rules) and a [`DataContext`](xref:NextORM.Core.DataContext) subclass (connection + parameter creation); pick the one that matches the database you already run.

**Prerequisites:** [Quickstart](../getting-started/02-quickstart.md) · [Dependency injection](../getting-started/04-dependency-injection.md)

## Overview

nextorm is split into a provider-neutral core (`nextorm`, namespace [`NextORM.Core`](xref:NextORM.Core)) and one package per
database: `nextorm.sqlite`, `nextorm.sqlserver`, `nextorm.postgres`, `nextorm.mysql`, `nextorm.mariadb`
and `nextorm.clickhouse`. The in-memory provider is built
into the core package. A provider contributes two things:

1. a **dialect** — a stateless object that renders everything that differs between databases (parameter
   placeholders, paging, quoting, function names, capability flags); and
2. a **context** — a [`DataContext`](xref:NextORM.Core.DataContext) subclass that knows how to create a connection and parameters, and
   exposes the dialect through its `Dialect` property.

SQL generation is driven entirely by [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) (`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`),
so the SQL builder and expression visitors never contain provider names. Query semantics (projection,
filtering, joins, grouping, set operations, CTE, window functions, scalar/UDF/TVF support) are shared;
only the rendering differs.

## Choosing a provider

| Situation | Provider |
|---|---|
| Local development, tests, embedded database, small apps | `nextorm.sqlite` |
| You already run Microsoft SQL Server / Azure SQL | `nextorm.sqlserver` |
| You already run PostgreSQL | `nextorm.postgres` |
| You already run MySQL | `nextorm.mysql` |
| You already run MariaDB | `nextorm.mariadb` |
| You already run ClickHouse | `nextorm.clickhouse` |
| Unit tests that must not touch a database, plan-cache tests, query-shape tests | [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) (core) |

Use the same query code against every provider; the provider differences below are the only things that
change.

## Support matrix

| Capability | SQLite | SQL Server | PostgreSQL | MySQL | MariaDB | ClickHouse | In-memory |
|---|---|---|---|---|---|---|---|
| Package | `nextorm.sqlite` | `nextorm.sqlserver` | `nextorm.postgres` | `nextorm.mysql` | `nextorm.mariadb` | `nextorm.clickhouse` | built into `nextorm` |
| Parameter placeholder | `$name` | `@name` | `@name` | `@name` | `@name` | `@name` | not applicable |
| Limit only | `limit n` | `top(n)` | `limit n` | `limit n` | `limit n` | `limit n` | in-process take |
| Limit + offset | `limit n offset m` | `offset m rows fetch next n rows only` | `limit n offset m` | `limit n offset m` | `limit n offset m` | `limit n offset m` | in-process skip/take |
| Offset only | `limit -1 offset m` | `offset m rows` | `offset m` | `limit 18446744073709551615 offset m` | `limit 18446744073709551615 offset m` | `limit 18446744073709551615 offset m` | in-process skip |
| Paging without `ORDER BY` | accepted | injects `order by (select null as anyorder)` | accepted | accepted | accepted | accepted | accepted |
| `INTERSECT ALL` / `EXCEPT ALL` (`*ALL`) | throws `NotSupportedException` | throws `NotSupportedException` | supported | throws `NotSupportedException` | supported | supported | not applicable |
| Recursive CTE keyword | `with recursive` | `with` (plus `option (maxrecursion n)`) | `with recursive` | `with recursive` | `with recursive` | `with` (no recursive CTE support) | not applicable |
| Named windows / `GROUPS` / `EXCLUDE` | named window + `GROUPS` + `EXCLUDE` | throws (no `WINDOW` clause) | named window + `GROUPS` + `EXCLUDE` | named window only | named window only | named window + `GROUPS` | not applicable |
| String concatenation | `\|\|` | `+` | `\|\|` | `concat(a, b)` | `concat(a, b)` | `concat(a, b)` | not applicable |
| Boolean literal | `1` / `0` | `1` / `0` (via `bit`) | `true` / `false` | `1` / `0` (`true` / `false`) | `1` / `0` (`true` / `false`) | `true` / `false` | not applicable |
| `??` (coalesce) | `ifnull(a, b)` | `isnull(a, b)` | `coalesce(a, b)` | `coalesce(a, b)` | `coalesce(a, b)` | `coalesce(a, b)` | not applicable |
| `stdev` / `stdevp` | `stdev` / `stdevp` (custom) | `stdev` / `stdevp` (native) | `stddev` / `stddev_pop` | `stddev_samp` / `stddev_pop` | `stddev_samp` / `stddev_pop` | `stddevSamp` / `stddevPop` | not applicable |
| `var` / `varp` | `var` / `varp` (custom) | `var` / `varp` (native) | `variance` / `var_pop` | `var_samp` / `var_pop` | `var_samp` / `var_pop` | `varSamp` / `varPop` | not applicable |
| `date_trunc` | throws | `datetrunc(...)` (2022+) | supported | throws | throws | `dateTrunc(...)` | throws |
| Date arithmetic (`date_add`, `date_diff`, `end_of_month`, `date_from_parts`, `DateTime.Add*`) | `datetime(x, n \|\| ' days')` / `date(...)` / `strftime` difference | supported | supported | supported | supported | `addDays(...)` … / `toLastDayOfMonth(...)` | throws |
| Date conversion / parts (`SqlFunctions.ClickHouse.to_*`) | throws | throws | throws | throws | throws | `toDate`/`toDateTime`/`toDate32`, `toYear`/…, `toStartOf*`, `toMonday`, `toYYYYMM`/`toYYYYMMDD`, `toUnixTimestamp` | throws |
| `string_agg` | `group_concat(x, delimiter)` | supported (2017+) | supported | `group_concat(x separator delimiter)` | `group_concat(x separator delimiter)` | `arrayStringConcat(groupArray(...), ...)` | throws |
| Full-text `contains` / `freetext` | throws | `contains` / `freetext` | `to_tsvector(...) @@ ...tsquery(...)` | `match(...) against(...)` | `match(...) against(...)` | throws | throws |
| Full-text ranking / score | throws | `containstable` / `freetexttable` (`RANK`, table function) | `ts_rank` / `ts_rank_cd` | throws | throws | throws | throws |
| Bit / statistical / `-If` aggregates | throws | throws | supported | throws | throws | `groupBit*`, `corr`/`covarPop`, `countIf`/… | throws |
| `multi_if` (multi-branch) | throws | throws | throws | throws | throws | `multiIf(c1, v1, …, else)` | not applicable |
| `lag_in_frame` / `lead_in_frame` | throws | throws | throws | throws | throws | `lagInFrame` / `leadInFrame` | not applicable |
| Identifier / alias quoting | single quotes: `as 't1'` | brackets: `as [t1]` | double quotes: `as "t1"` | backticks: `` as `t1` `` | backticks: `` as `t1` `` | backticks: `` as `t1` `` | not applicable |
| Derived table (subquery in `FROM`) alias | not required | required | required | required | required | required | not applicable |
| Table-valued function alias | not required | required | required | required | required | required | TVF source not supported |
| `LEFT` / `RIGHT` / `FULL` / `CROSS` join | yes | yes | yes | no `FULL` | no `FULL` | yes | yes |
| `RIGHT` / `FULL` join capability | supported | supported | supported | `RIGHT` only | `RIGHT` only | supported | supported |

For the feature-by-feature comparison against EF Core and linq2db, see
[SQL capabilities gap analysis](../specs/roadmap/sql-capabilities-gap-analysis.md).

## Provider differences: unification decisions

Where providers differ, nextorm either **unifies** the surface in code, **gates** the feature so an
unsupported provider throws `NotSupportedException`, or **documents** the difference and leaves it
provider-specific. The decision for every known divergence:

| Difference | Decision | Rationale / hook |
|---|---|---|
| `date_add`/`date_diff`/`date_trunc` accepted fields | **Leave provider-specific, gated per field** | `SupportsDateAddField`/`SupportsDateDiffField`/`SupportsDateTruncField` reject an unsupported field; a single normalized set would silently change results (SQLite folds `millisecond`/`quarter`, SQL Server has no `decade`/`century`/`millennium`). |
| `FULL JOIN` on MySQL/MariaDB | **Gated, no polyfill** | `SupportsFullJoin => false`; a `LEFT JOIN … UNION … RIGHT JOIN` rewrite changes row shape/deduplication and can defeat the planner, so it is never emitted implicitly. |
| `CUBE`/`GROUPING SETS` on MySQL/MariaDB (and all of `ROLLUP`/`CUBE`/`GROUPING SETS` in-memory) | **Gated, no polyfill** | `SupportsCube`/`SupportsGroupingSets`; a `UNION ALL` emulation multiplies scans and changes semantics (`GROUPING()`), so it is left to raw SQL. |
| `GREATEST`/`LEAST` NULL semantics | **Documented difference** | PostgreSQL/SQL Server 2022+/ClickHouse ignore NULL arguments; MySQL/MariaDB and SQLite return NULL when any argument is NULL. Availability is gated by `SupportsGreatestLeast`; the NULL behaviour is not rewritten. |
| Date/number formatting templates (`to_char`, `FORMAT`, `strftime`, `formatDateTime`) | **Closed — not unifiable** | The template languages are incompatible, so formatting stays provider-specific `[SqlFunction]` UDFs; there is no portable `template` argument. |
| `FOR JSON` / `FOR XML` | **Gated (SQL Server)** | `SupportsForJson`/`SupportsForXml`. |
| Statement-level query hints | **Unified** | SQL Server `OPTION (...)`, PostgreSQL/MySQL/MariaDB inline `/*+ ... */`; SQLite/ClickHouse have no syntax and stay gated (see [Query hints](../guide/17-query-hints.md)). |
| Table hints vs index hints | **Leave provider-specific** | `WITH (NOLOCK)` has no equivalent in MySQL/MariaDB/SQLite index hints (`USE INDEX`/`INDEXED BY` change the plan, not locking), so only SQL Server is wired (`SupportsTableHints`). |
| Raw SQL as a composable `FROM` source | **Unified** | `FromSql` + `SupportsRawSqlSource` on every SQL provider (see [Raw SQL](../guide/14-raw-sql.md#compositing-raw-sql-as-a-from-source)). |
| `INTERSECT ALL`/`EXCEPT ALL` | **Gated** | PostgreSQL and MariaDB support them; SQL Server/SQLite/MySQL reject via `SupportsIntersectExceptAll`. |

The per-feature rows in the [limitations](../advanced/limitations.md) table spell out the resulting
runtime behaviour.

## How a dialect plugs in

A dialect implements [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) or derives from [`SqlDialectBase`](xref:NextORM.Core.SqlDialectBase). In [`SqlDialectBase`](xref:NextORM.Core.SqlDialectBase) only
[`MakeParam`](xref:NextORM.Core.ISqlDialect) and [`MakePage`](xref:NextORM.Core.ISqlDialect) are abstract; every other member has a working ANSI default, so a dialect
overrides just what is different. Capability differences (paging requiring an `ORDER BY`, required
subquery aliases, `INTERSECT ALL`/`EXCEPT ALL`) are expressed as properties rather than special cases in
the SQL builder.

```csharp
// The built-in dialects are singletons exposed as a static Instance.
ISqlDialect sqlite = SqliteDialect.Instance;
ISqlDialect sqlServer = SqlServerDialect.Instance;
ISqlDialect postgres = PostgresDialect.Instance;
ISqlDialect mysql = MySqlDialect.Instance;
ISqlDialect mariaDb = MariaDbDialect.Instance;
ISqlDialect clickHouse = ClickHouseDialect.Instance;
```

A provider context returns its dialect from an overridden property:

```csharp
public class SqliteDataContext : DataContext
{
    public override ISqlDialect Dialect => SqliteDialect.Instance;
    // CreateDbConnection / CreateParam are provider specific.
}
```

The dialect contract deliberately excludes connection/parameter creation (`CreateConnection`/
`CreateParam`) and column mapping (`MapColumnExpression`): those live on the context, because they are a
separate axis from SQL text.

## Registering a provider

Every provider package adds `UseXxx` extension methods on [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder), and every context also has a
public constructor that takes a connection string or an existing `DbConnection`:

```csharp
using NextORM.Core;
using NextORM.Sqlite;      // or nextorm.sqlserver / nextorm.postgres

var builder = new DataContextBuilder().UseSqlite("app.db");   // provider-specific overload
using var ctx = builder.CreateDataContext();                   // returns IDataContext
```

With dependency injection:

```csharp
services.AddNextOrmContext(builder => builder.UseSqlite("app.db"));
```

See [Dependency injection](../getting-started/04-dependency-injection.md) for keyed and generic
registrations.

## See also

- [SQLite](sqlite.md)
- [SQL Server](sqlserver.md)
- [PostgreSQL](postgres.md)
- [MySQL](mysql.md)
- [MariaDB](mariadb.md)
- [ClickHouse](clickhouse.md)
- [In-memory](in-memory.md)
- [SQL capabilities gap analysis](../specs/roadmap/sql-capabilities-gap-analysis.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`, `src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs`,
`tests/nextorm.sqlite.tests/SqliteDialectTests.cs`, `tests/nextorm.sqlserver.tests/SqlServerDialectTests.cs`,
`tests/nextorm.postgres.tests/PostgresDialectTests.cs`, `tests/nextorm.mysql.tests/MySqlDialectTests.cs`,
`tests/nextorm.mariadb.tests/MariaDbDialectTests.cs`, `tests/nextorm.clickhouse.tests/ClickHouseDialectTests.cs`.
