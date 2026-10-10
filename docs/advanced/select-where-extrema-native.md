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
| Extreme key | a direct mapped column bound to `short`/`int`/`long` or `float`/`double` (including nullable), or a composite of such components; a composite that contains a floating component is limited to three components, while a purely integral composite has no arity limit; a floating component is adapted NaN-safe |
| Group key | the same integral types as the extreme key (`short`/`int`/`long`, including nullable), or a composite of those of any arity; a floating group key keeps the portable path |
| ClickHouse payload | integral types (`sbyte`/`byte`/`short`/`ushort`/`int`/`uint`/`long`/`ulong`, including nullable), floating-point types (`float`/`double`, including nullable) and `string` |
| Everything else | the portable window-function lowering |

A composite key is compared lexicographically component by component. A `Float32`/`Float64` component of the extreme key is adapted in the same query instead of being passed to `argMin`/`argMax` verbatim: a leading `isNaN` rank keeps NaN last in both directions, matching the portable contract, and a `Float32` component is widened to `Float64` for the comparison. The raw direct aggregate is deliberately not used, because ClickHouse seeds it with the first row and `x > NaN`/`x < NaN` are both false, so a leading NaN would otherwise win. The adaptation covers single, two- and three-component keys across `Min`/`Max`, global/grouped, and nullable/non-nullable components. The arity limits are deliberately asymmetric: a purely integral extreme key or group key is a plain lexicographic tuple and is not arity-capped, while only a key that contains a floating component is capped at three components, because only that adaptation was proven arity by arity. The group selector must be compound (`e => new { e.TeamId }`); a bare single-column group selector keeps the portable path. Everything outside the table above keeps the portable lowering with the same results contract: the other providers, `ExtremeRowTies.All`, floating-point group keys, floating extreme keys with more than three components, `Float16`/`Decimal` keys, value converters, computed expressions, and other key or payload types.

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
- Each of the three DTOs also exposes a **public eager constructor**, so an assembly without friend access can build them and drive its own [`IExtremeRowRenderer`](xref:NextORM.Core.IExtremeRowRenderer). Construction validates references and collection elements immediately and structurally: a required reference that is `null` throws `ArgumentNullException`; a `null` element, an empty-string alias element or a `SourceSql` that is empty/whitespace-only throws `ArgumentException`; an undeclared `KeywordCase` value throws `ArgumentException`. It performs **no** SQL parsing or provider-type/security validation, adds no minimum-count, cross-list cardinality or alias-disjointness rule (empty shapes and overlapping aliases are allowed), never reads `ExtremeRowDescription.Payload`, and never calls `CanRender`/`Render`. The collections are **borrowed**, not copied and not made immutable: the same instance is observed through the property, and a caller must not mutate it while the DTO is in use, including from a concurrent caller. The core's internal lazy payload/candidate-preparation path (and the unvalidated trusted request construction used on the hot path) remains an implementation detail and is **not** public API.

`PostgresDialect` and `ClickHouseDialect` are deliberately **not `sealed`**: keeping them inheritable, together with their public constructors (`PostgresDialect()`, `PostgresDialect(Version?)`, `ClickHouseDialect()`), the shared `Instance` singleton and the virtual `ExtremeRowRenderer` hook, is the supported external extension boundary — a public compatibility commitment, not a test detail. A custom dialect can derive from either provider dialect, override the hook and install its own public [`IExtremeRowRenderer`](xref:NextORM.Core.IExtremeRowRenderer) implementation; the built-in renderers (`PostgresExtremeRowRenderer`, `ClickHouseExtremeRowRenderer`) are `internal` and are not part of that contract. Returning `null` from the override — or installing a renderer whose `CanRender` answers `false` — keeps the portable window-function lowering, and the provider defaults and constructors are unchanged.

```csharp
// A non-friend assembly subclasses the provider dialect and installs a custom renderer.
public sealed class MyPostgresDialect : PostgresDialect
{
    public override IExtremeRowRenderer? ExtremeRowRenderer => MyRenderer.Instance;
}

// Only the global (ungrouped) single-key form is handled natively; every other shape
// falls back to the portable lowering because CanRender answers false.
public sealed class MyRenderer : IExtremeRowRenderer
{
    public static readonly MyRenderer Instance = new();

    public bool CanRender(ExtremeRowDescription description)
        => description.Groups.Count == 0 && description.Keys.Count == 1;

    public string Render(ExtremeRowRenderRequest request)
    {
        var direction = request.IsMax ? "desc" : "asc";
        return request.SourceSql
            + " order by \"" + request.KeyAliases[0] + "\" " + direction + " limit 1";
    }
}

// The second example: returning null from the override forces the portable window-function
// lowering for every request, without installing a renderer.
public sealed class PortablePostgresDialect : PostgresDialect
{
    public override IExtremeRowRenderer? ExtremeRowRenderer => null;
}
```

## See also

- [Keeping the extreme row (`SelectWhereMax` / `SelectWhereMin`)](../guide/07-distinct.md)
- [API quick reference](api-reference.md)
- [Provider overview](../providers/overview.md)
