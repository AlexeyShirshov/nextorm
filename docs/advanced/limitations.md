# Limitations and out-of-scope features

> nextorm is a read-only query builder: it generates `SELECT` statements and materialises their results, and deliberately leaves change tracking, DML and relationship inference to the caller.

**Prerequisites:** [Provider overview](../providers/overview.md) · [API reference](api-reference.md)

## Overview

This page lists the capabilities that are **not** part of nextorm, with a one-line reason for each. It is
derived from [SQL capabilities gap analysis](../sql-capabilities-gap-analysis.md); that document is the
source of truth and also tracks what *is* implemented. Read the two together: a feature listed there as
**Done** is not a limitation, even though the older sections of the analysis (sections 1–4) still describe
the pre-implementation baseline.

## Out of scope

| Not supported | Rationale |
|---|---|
| **DML** — `INSERT`, `UPDATE`, `DELETE`, `MERGE` | nextorm is a read-only, no-change-tracking mapper by design; writes are expected to go through your own commands or another tool. |
| **Navigation properties / relationship metadata** | There is no relationship metadata and no implicit join inference; you write joins explicitly with `Join`/`LeftJoin`/`RightJoin`/`FullJoin`/`CrossJoin`/`CrossApply`/`OuterApply` (see [Joins](../guide/03-joins.md)). |
| **Correlated `APPLY` / `LATERAL` source** | `CrossApply`/`OuterApply` are supported over a table, derived query, raw table or table-valued function, but the applied source cannot reference the outer row yet — there is no public API to author an outer reference inside a `FROM` subquery. Dialects without a lateral source (SQLite, ClickHouse) reject `APPLY` outright; the in-memory provider does not support it. |
| **`SelectMany` / `GroupJoin` on SQL providers** | These LINQ operators are implemented for the in-memory provider only, where correlation is a plain delegate. On a SQL provider `SelectMany`/`GroupJoin` throw `NotSupportedException`; the explicit `CrossApply`/`OuterApply` join surface covers the non-correlated equivalent (see [Joins](../guide/03-joins.md)). |
| **General correlated scalar projection** | Correlated subqueries are supported only through `EXISTS` / `IN` / `ANY` / `ALL` (`NORM.SQL`); a general correlated scalar subquery in the projection is not covered. |
| **`INTERSECT ALL` / `EXCEPT ALL` on SQL Server and SQLite** | Neither engine can express these variants, so the dialect rejects them with a `NotSupportedException`; `INTERSECT` / `EXCEPT` (without `ALL`) and `UNION` / `UNION ALL` work everywhere. PostgreSQL supports both `ALL` variants. |
| **Query hints outside SQL Server** | Statement-level hints are rendered only by the SQL Server dialect; SQLite, PostgreSQL, MySQL/MariaDB and ClickHouse reject a command that carries them with `NotSupportedException` (see [Query hints](../guide/17-query-hints.md)). Table hints (`WithTableHint("nolock")` → `WITH (NOLOCK)`) are supported on SQL Server only. |
| **Raw SQL as a composable source or subquery** | Raw SQL is supported for a whole query (`WithSql` / `PrepareFromSql`), but it cannot be used as a composable `FROM`/subquery fragment. |
| **Array functions outside PostgreSQL** | The array-parameter `any`/`all` quantifiers (`column = any(@array)`) and the array functions (`cardinality`, `array_length`, ...) require a provider with native arrays; only PostgreSQL opts in (`SupportsArrays`). Other dialects reject them with `NotSupportedException` (see [Scalar functions](../guide/11-scalar-functions.md#arrays-postgresql)). |
| **Native JSON functions outside PostgreSQL** | The `json`/`jsonb` functions and operators (`json_agg`, `json_build_object`, `->`, `->>`, `#>`, `@>`, `?`, ...) require a provider with a JSON type; only PostgreSQL opts in (`SupportsJson`). SQL Server provides the text-JSON subset (`json_value`/`json_query`/`json_modify`, `SupportsTextJson`) over a normal text column instead. Other dialects reject both surfaces with `NotSupportedException` (see [Scalar functions](../guide/11-scalar-functions.md#json-and-jsonb-postgresql)). |
| **`greatest`/`least` outside PostgreSQL, MySQL/MariaDB, ClickHouse and SQL Server** | The functions are gated by `SupportsGreatestLeast`; PostgreSQL, MySQL/MariaDB, ClickHouse and SQL Server 2022+ opt in, while SQLite (whose scalar `max`/`min` propagate NULL differently) rejects them with `NotSupportedException`. `nullif` is ANSI and always available. |
| **`date_trunc` outside PostgreSQL, SQL Server and ClickHouse** | `date_trunc` is gated by `SupportsDateTrunc`; PostgreSQL, SQL Server 2022+ (`datetrunc`) and ClickHouse (`dateTrunc`) opt in. |
| **`array_agg` outside PostgreSQL** | `array_agg` is gated by `SupportsArrayAgg` and requires a provider with an array type, so only PostgreSQL opts in. `string_agg` is gated separately by `SupportsStringAgg` and is also available on SQL Server 2017+ and ClickHouse (`arrayStringConcat(groupArray(x), delimiter)`). Note that an `array_agg` result is an array column, which the row reader cannot materialise yet, so use it inside the query (for example in a `HAVING`). |
| **Boolean and regression aggregates outside PostgreSQL** | `bool_and`/`bool_or`/`every` (`SupportsBooleanAggregates`) and the `regr_*` family (`SupportsRegressionAggregates`) are PostgreSQL-only. `corr`/`covar_*` (`SupportsStatisticalAggregates`) are also available on ClickHouse (`corr`/`covarPop`/`covarSamp`). |
| **Aggregate `FILTER` outside PostgreSQL and SQLite** | The `FILTER (WHERE ...)` clause is gated by `SupportsFilter`; PostgreSQL and SQLite opt in, MySQL/MariaDB and SQL Server do not. ClickHouse rejects it too, but exposes the filtered-aggregate equivalent as the `-If` combinators (`count_if`/`sum_if`/`avg_if`/`min_if`/`max_if`, `SupportsIfAggregates`). |
| **`GROUP BY CUBE`/`GROUPING SETS` on MySQL/MariaDB and the in-memory provider** | MySQL/MariaDB have no `CUBE` or `GROUPING SETS`, and the in-memory provider supports none of `ROLLUP`/`CUBE`/`GROUPING SETS`; these reject the modifier with `NotSupportedException`. `ROLLUP` works on every SQL provider (see [Grouping and aggregates](../guide/04-grouping-and-aggregates.md#rollup-and-cube)). |

## Not limitations

The following are fully implemented and verified; they appear in the *done* column of the gap analysis,
not in the list above:

- joins of every type (`INNER`, `LEFT`, `RIGHT`, `FULL`, `CROSS`, plus `CROSS APPLY`/`OUTER APPLY` and the lateral equivalent where supported) and arity 2–8;
- `CASE WHEN` / ternary / `switch`, `COALESCE`, numeric `CAST`;
- string, math and date/time scalar functions and `LIKE`;
- date arithmetic (`date_add`/`end_of_month`/`date_diff`/`date_from_parts` and `DateTime.Add*`) on PostgreSQL, SQL Server, ClickHouse, MySQL/MariaDB and SQLite, plus `date_trunc` on PostgreSQL/SQL Server/ClickHouse and `string_agg` on PostgreSQL/SQL Server/ClickHouse/MySQL/MariaDB/SQLite;
- the bitwise (`bit_and`/`bit_or`/`bit_xor`), statistical (`corr`/`covar_*`), `argMin`/`argMax` and `-If` (`count_if`/...) aggregates on ClickHouse;
- binary columns - a `byte[]` property or projection maps to `bytea` (PostgreSQL), `varbinary`/`image` (SQL Server) or `blob` (SQLite);
- `IN` over a list/array, logical `!` and unary operators;
- `SELECT DISTINCT`, `INTERSECT`/`EXCEPT`, CTEs (including recursive), window functions and `GROUP BY ROLLUP`/`CUBE`/`GROUPING SETS` (`CUBE` and `GROUPING SETS` except MySQL/MariaDB);
- user-defined scalar functions (`[SqlFunction]`) and table-valued functions (`[SqlTableFunction]`);
- full-text predicates (`NORM.SQL.contains`/`freetext`) on SQL Server, PostgreSQL and MySQL/MariaDB, SQL Server text-JSON functions (`json_value`/`json_query`/`json_modify`) and SQL Server `openjson`/`string_split` table functions;
- statement-level query hints and table hints on SQL Server (`Hint(...)`, `WithTableHint(...)`) and JSON output (`ForJson(...)` → `FOR JSON PATH`/`AUTO`) and XML output (`ForXml(...)` → `FOR XML`);
- raw SQL for a whole query and the implicit plan cache / `Prepare()` reuse paths.

Refer to [SQL capabilities gap analysis](../sql-capabilities-gap-analysis.md) for the status table and the
test evidence behind each item.

## See also

- [SQL capabilities gap analysis](../sql-capabilities-gap-analysis.md)
- [Query hints](../guide/17-query-hints.md)
- [Provider overview](../providers/overview.md)
- [API reference](api-reference.md)

---

Source: `docs/sql-capabilities-gap-analysis.md` (sections 1–4), implementation status table at the top of
that document.
