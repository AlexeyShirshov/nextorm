# Native extreme-row strategies (`SelectWhereMax` / `SelectWhereMin`)

> For a `SelectWhereMax`/`SelectWhereMin` request with `ExtremeRowTies.One`, PostgreSQL and ClickHouse render a native SQL strategy instead of the portable window-function lowering — automatically, from the provider and the query form, with no opt-in and no per-instance switch.

**Prerequisites:** [Keeping the extreme row](../guide/07-distinct.md) · [Provider overview](../providers/overview.md) · [API quick reference](api-reference.md)

## Overview

[`SelectWhereMax`](xref:NextORM.Core.EntityBuilder`1.SelectWhereMax``1(System.Linq.Expressions.Expression{System.Func{`0,``0}},NextORM.Core.ExtremeRowTies,System.Linq.Expressions.Expression{System.Func{`0,System.Object}})) and
[`SelectWhereMin`](xref:NextORM.Core.EntityBuilder`1.SelectWhereMin``1(System.Linq.Expressions.Expression{System.Func{`0,``0}},NextORM.Core.ExtremeRowTies,System.Linq.Expressions.Expression{System.Func{`0,System.Object}})) keep the row whose value under a selector is the greatest or smallest in the source, globally or per group. They are a row filter, not an aggregate: a surviving row keeps every column. The semantics are unchanged from the portable implementation — see [Keeping the extreme row](../guide/07-distinct.md) for the full contract.

What this page adds is *how* the `One` request reaches the database. Selection is automatic and deterministic **by provider and query form/type**:

- on PostgreSQL, native-eligible key and group selectors render `DISTINCT ON` (grouped) or an inner `ORDER BY ... LIMIT 1` (global);
- on ClickHouse, a native-eligible key, group and payload render a single `argMin`/`argMax` aggregate;
- every other combination keeps the portable window-function lowering.

There is no opt-in, no per-instance or per-query switch, and no retry-on-error: once the dialect accepts a request it renders it, and a rendering failure is surfaced instead of silently re-running the portable path. `ExtremeRowTies.All` is never rendered natively.

## Usage

The public API does not change; only the provider decides the SQL.

```csharp
// Global: the single row with the greatest Score.
var top = dataContext.From<Player>()
    .SelectWhereMax(p => p.Score)
    .ToList();

// Per group: the single row with the smallest Score in each TeamId.
// The group selector is compound; a bare p => p.TeamId would stay on the portable path.
var perTeam = dataContext.From<Player>()
    .SelectWhereMin(p => p.Score, ExtremeRowTies.One, p => new { p.TeamId })
    .ToList();
```

## Native or portable

| Provider | Query form | Strategy |
|---|---|---|
| PostgreSQL | grouped `One` | one `DISTINCT ON (<group columns>) ... ORDER BY <group columns>, <extreme key> ASC/DESC` |
| PostgreSQL | global `One` | an inner `ORDER BY <extreme key> ASC/DESC LIMIT 1` |
| ClickHouse | global `One` | one `argMin`/`argMax(tuple(<payload columns>), <extreme key>)`, plus `HAVING count() > 0` to suppress the empty-input default row |
| ClickHouse | grouped `One` | one `argMin`/`argMax(tuple(<payload columns>), <extreme key>)` next to the group columns and a `GROUP BY` |
| Any other provider, or a non-eligible request | any | the portable window-function lowering |

The PostgreSQL grouped form keeps the first row per group under the ordering, so ordering the group components first and the full extreme key after them selects the extreme row per group. The ClickHouse aggregate picks a whole payload tuple and projects its elements back to the canonical payload aliases. In both cases the shared builder still applies the outer projection, `DISTINCT` and the user's output ordering afterwards.

For the `Player` example above (`players(id, score, team_id, name)`) the two calls render as follows.

```sql
-- PostgreSQL, global (SelectWhereMax): the inner ORDER BY ... LIMIT 1
select t1.id, t1.score, t1.team_id as "TeamId", t1.name
from (select * from players
 where score is not null order by "score" desc limit 1) as "t1"

-- PostgreSQL, grouped (SelectWhereMin over new { TeamId }): DISTINCT ON over the group columns
select t1.id, t1.score, t1.team_id as "TeamId", t1.name
from (select distinct on ("team_id") * from (select * from players
 where score is not null) __nextorm_extreme order by "team_id", "score") as "t1"
```

```sql
-- ClickHouse, global (SelectWhereMax): one argMax aggregate over a payload tuple
select t1.id, t1.score, t1.team_id as `TeamId`, t1.name
from (select tupleElement(`__nextorm_extreme_tuple`, 1) as `id`, tupleElement(`__nextorm_extreme_tuple`, 2) as `score`, tupleElement(`__nextorm_extreme_tuple`, 3) as `team_id`, tupleElement(`__nextorm_extreme_tuple`, 4) as `name`
from (
 select argMax(tuple(`id`, `score`, `team_id`, `name`), `score`) as `__nextorm_extreme_tuple`
 from (select * from players
 where score is not null) as `__nextorm_extreme_src`
 having count() > 0)) as `t1`

-- ClickHouse, grouped (SelectWhereMin over new { TeamId }): the aggregate next to the group columns plus GROUP BY
select t1.id, t1.score, t1.team_id as `TeamId`, t1.name
from (select `team_id`, tupleElement(`__nextorm_extreme_tuple`, 1) as `id`, tupleElement(`__nextorm_extreme_tuple`, 2) as `score`, tupleElement(`__nextorm_extreme_tuple`, 4) as `name`
from (
 select `team_id`, argMin(tuple(`id`, `score`, `team_id`, `name`), `score`) as `__nextorm_extreme_tuple`
 from (select * from players
 where score is not null) as `__nextorm_extreme_src`
 group by `team_id`)) as `t1`
```

## Eligible queries

Eligibility is intentionally narrow, and is decided from a prepared description of the command before any SQL, alias or parameter is built.

| Part | Native eligibility |
|---|---|
| Extreme key | a direct mapped column bound to `short`/`int`/`long` (including nullable), or a composite of those |
| Group key | the same as the extreme key |
| ClickHouse payload | integral types (`sbyte`/`byte`/`short`/`ushort`/`int`/`uint`/`long`/`ulong`, including nullable) and `string` |
| Everything else | the portable window-function lowering |

A composite key is compared lexicographically component by component. The group selector must be compound (`e => new { e.TeamId }`); a bare single-column group selector keeps the portable path. Everything outside the table above keeps the portable lowering with the same results contract: the other providers, `ExtremeRowTies.All`, floating-point keys (rejected as a whole on ClickHouse because their ordering is not portable), value converters, computed expressions, and other key or payload types.

## Semantics

The native strategies do not change the observable result:

- **One real row per partition.** `One` returns exactly one winning *source* row per group (one row for the global form), and every field comes from that same source row, so a full-entity read and a projection are consistent. Which tied row is chosen is unspecified — there is no tie-break API.
- **Output ordering is separate.** A user `OrderBy` sorts the result only. It is applied outside winner selection and is never folded into the winner ordering.
- **NULL handling.** A row with a `NULL` extreme-key component is excluded before winner selection, and a partition whose key components are all `NULL` yields no row. Nullable group keys and nullable payload values are preserved.
- **Empty input.** The result is zero rows, including the ClickHouse global form, where `HAVING count() > 0` suppresses the aggregate's default tuple row instead of returning it.
- **Rejected shapes.** The combinations the portable path forbids — joins, paging, `DistinctOn`, `GroupBy`, `Having`, named windows, CTEs, set operations, `LimitBy`, `ArrayJoin`, `PreWhere` and non-physical sources — still reject before dispatch on both paths.

## Dialect extensibility

The native strategy is a public dialect capability, not a hard-coded provider branch:

- [`ISqlDialect.ExtremeRowRenderer`](xref:NextORM.Core.ISqlDialect.ExtremeRowRenderer) is the optional renderer, declared as a default interface member returning `null`; [`SqlDialectBase.ExtremeRowRenderer`](xref:NextORM.Core.SqlDialectBase.ExtremeRowRenderer) overrides it virtually with the same default. `null` means no native strategy.
- [`IExtremeRowRenderer`](xref:NextORM.Core.IExtremeRowRenderer) exposes `CanRender(`[`ExtremeRowDescription`](xref:NextORM.Core.ExtremeRowDescription)`)`, a side-effect-free decision taken before any SQL is assembled, and `Render(`[`ExtremeRowRenderRequest`](xref:NextORM.Core.ExtremeRowRenderRequest)`)`, which returns the winning-row source. Only a positive `CanRender` leads to `Render`, and `Render` has no late fallback to the portable path.
- [`ExtremeRowRenderColumn`](xref:NextORM.Core.ExtremeRowRenderColumn), [`ExtremeRowDescription`](xref:NextORM.Core.ExtremeRowDescription) and [`ExtremeRowRenderRequest`](xref:NextORM.Core.ExtremeRowRenderRequest) carry only shape facts (CLR type, nullability, direct-mapped/converter flags, and the prepared source/aliases), never the expression or build context. See the [API quick reference](api-reference.md).

`PostgresDialect` and `ClickHouseDialect` are not `sealed`, so a custom dialect can derive from them and override the renderer (for example to change eligibility) without touching the core builder.

## See also

- [Keeping the extreme row (`SelectWhereMax` / `SelectWhereMin`)](../guide/07-distinct.md)
- [API quick reference](api-reference.md)
- [Provider overview](../providers/overview.md)
