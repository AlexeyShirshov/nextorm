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
| **Navigation properties / relationship metadata** | There is no relationship metadata and no implicit join inference; you write joins explicitly with `Join`/`LeftJoin`/`RightJoin`/`FullJoin`/`CrossJoin` (see [Joins](../guide/03-joins.md)). |
| **`APPLY` / `LATERAL` joins** | Absent from the builder, the parser and the dialects; a correlated set source is not expressible. |
| **General correlated scalar projection** | Correlated subqueries are supported only through `EXISTS` / `IN` / `ANY` / `ALL` (`NORM.SQL`); a general correlated scalar subquery in the projection is not covered. |
| **`INTERSECT ALL` / `EXCEPT ALL` on SQL Server and SQLite** | Neither engine can express these variants, so the dialect rejects them with a `NotSupportedException`; `INTERSECT` / `EXCEPT` (without `ALL`) and `UNION` / `UNION ALL` work everywhere. PostgreSQL supports both `ALL` variants. |
| **Raw SQL as a composable source or subquery** | Raw SQL is supported for a whole query (`WithSql` / `PrepareFromSql`), but it cannot be used as a composable `FROM`/subquery fragment. |

## Not limitations

The following are fully implemented and verified; they appear in the *done* column of the gap analysis,
not in the list above:

- joins of every type (`INNER`, `LEFT`, `RIGHT`, `FULL`, `CROSS`) and arity 2–8;
- `CASE WHEN` / ternary / `switch`, `COALESCE`, numeric `CAST`;
- string, math and date/time scalar functions and `LIKE`;
- `IN` over a list/array, logical `!` and unary operators;
- `SELECT DISTINCT`, `INTERSECT`/`EXCEPT`, CTEs (including recursive) and window functions;
- user-defined scalar functions (`[SqlFunction]`) and table-valued functions (`[SqlTableFunction]`);
- raw SQL for a whole query and the implicit plan cache / `Prepare()` reuse paths.

Refer to [SQL capabilities gap analysis](../sql-capabilities-gap-analysis.md) for the status table and the
test evidence behind each item.

## See also

- [SQL capabilities gap analysis](../sql-capabilities-gap-analysis.md)
- [Provider overview](../providers/overview.md)
- [API reference](api-reference.md)

---

Source: `docs/sql-capabilities-gap-analysis.md` (sections 1–4), implementation status table at the top of
that document.
