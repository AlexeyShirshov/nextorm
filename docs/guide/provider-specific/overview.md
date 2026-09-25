# Provider-specific SQL

> nextorm targets a portable subset of SQL, but it does not hide what a database can do. A construct is
> available when the active dialect advertises the matching [`ISqlDialect`](xref:NextORM.Core.ISqlDialect)
> capability, and a provider that does not opt in rejects it with `NotSupportedException`.
>
> This section catalogues the constructs that are **exclusive to a single provider** — every other
> dialect throws for them. Constructs that are merely *spelled* differently, or that several providers
> share (for example `greatest`/`least`, full-text search, JSON, table functions, `any_agg`, UUID
> generators or `PERCENTILE_CONT`), are documented with the matching concept page instead, and only
> three providers have an exclusive surface at all: ClickHouse, PostgreSQL and SQL Server.

**Prerequisites:** [Querying and projections](../01-querying-and-projections.md) · [Provider overview](../../providers/overview.md) · [Limitations and out-of-scope features](../../advanced/limitations.md)

## How provider-specific SQL is gated

The query builder is dialect-driven: every feature that is not part of the portable surface is guarded
by a `Supports*` property, a per-name predicate or a `Make*` rendering hook on
[`ISqlDialect`](xref:NextORM.Core.ISqlDialect). The provider implementations under
`src/nextorm.<provider>` turn those flags on and render the native syntax; `SqlDialectBase` keeps safe
defaults that throw. As a result:

* the same LINQ expression compiles against every provider, but only the providers that support the
  construct execute it — the rest throw `NotSupportedException` with a provider-specific message;
* the in-memory provider is the strictest: it only implements select, where, joins, grouping, set
  operations and windowing over in-memory collections, and throws for anything that needs a SQL engine;
* an unsupported construct fails when the query is **prepared** (SQL providers) or enumerated
  (in-memory), not when the expression is written.

## Providers with an exclusive surface

| Provider | Exclusive constructs |
|---|---|
| [ClickHouse](clickhouse.md) | Arrays and `ARRAY JOIN`, `LIMIT n BY expr`, `GROUP BY ... WITH TOTALS`, `FINAL`/`SAMPLE`/`PREWHERE`/`SETTINGS`, join strictness and `GLOBAL JOIN`, `GLOBAL IN`, `uniq`/`quantile`/`any`/`argMin`/`argMax` aggregates, the `-If` combinators, dictionaries, string-JSON `JSONExtract`, the `numbers`/`zeros` table functions |
| [MySQL and MariaDB](mysql.md) | Native string/conditional idioms (`FIND_IN_SET`/`FIELD`/`ELT`/`SUBSTRING_INDEX`/`FORMAT`), `%`-templated date conversion and Unix-epoch functions (`STR_TO_DATE`/`DATE_FORMAT`/`FROM_UNIXTIME`/`UNIX_TIMESTAMP`), hexadecimal hashes (`MD5`/`SHA1`/`SHA2`), IPv4 conversion (`INET_ATON`/`INET_NTOA`), the JSON mutation family and `UUID_TO_BIN`/`BIN_TO_UUID` (MySQL only) |
| [PostgreSQL](postgresql.md) | Native array types and operators, native `json`/`jsonb`, ordered-set aggregates (`percentile_* ... WITHIN GROUP`), regression/boolean/bit aggregates, extended scalar and regexp helpers, `DISTINCT ON`, `unnest` |
| [SQL Server](sqlserver.md) | `CHOOSE`, table hints and `OPTION (...)`, `FOR JSON`/`FOR XML`, `string_split`/`openjson` |

## Providers without an exclusive surface

SQLite has **no** provider-exclusive constructs: every capability flag it turns on is also set by at least
one other dialect. Its page therefore documents connection setup and limitations only, and the shared
constructs it implements are described on the concept pages:

* [SQLite](../../providers/sqlite.md) — scalar `max`/`min`, `strftime` date parts, JSON1, registered
  `stdev`/`var` aggregates.

The MySQL/MariaDB guide page also covers the shared constructs of the MySQL family: text JSON, full-text
`MATCH ... AGAINST`, `ANY_VALUE`, `PERCENTILE_CONT`/`MEDIAN` and `UUID_v4()`/`UUID_v7()`.

## Portability model

The provider pages describe a construct from the database's point of view; the concept pages carry the
full, provider-neutral walkthrough:

* [Filtering (WHERE)](../02-filtering-where.md) — `IN`/`Contains`, full-text predicates;
* [Joins](../03-joins.md) — join types, APPLY/LATERAL, ClickHouse strictness and `GLOBAL JOIN`;
* [Grouping and aggregates](../04-grouping-and-aggregates.md) — `ROLLUP`/`CUBE`, the aggregate families;
* [Sorting and paging](../05-sorting-and-paging.md) — `Limit`/`Offset`, `LIMIT n BY expr`;
* [Scalar functions](../11-scalar-functions.md) — arrays, JSON, UUID, session/info, provider surfaces;
* [Table-valued functions](../13-table-valued-functions.md) — `generate_series`, `string_split`, `numbers`, `zeros`;
* [Query hints](../17-query-hints.md) — table hints, `OPTION (RECOMPILE)`, ClickHouse query modifiers;
* [JSON support across providers](../18-json.md) — every JSON surface side by side.

Portable functions that are spelled differently on every database live on
[`CommonFunctions`](xref:NextORM.Core.CommonFunctions) and are documented with the concept pages:
`iif(condition, a, b)`, `gen_random_uuid()`/`uuidv7()`, and session/info functions such as
`current_user()`/`session_user()`/`current_schema()`/`version()`.

## See also

* [Limitations and out-of-scope features](../../advanced/limitations.md)
* [Provider overview](../../providers/overview.md)

---

Source: `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`, `src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs`,
`src/nextorm.{postgres,sqlserver,clickhouse}/*Dialect.cs`.
