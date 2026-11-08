# Extensions package (`nextorm.extensions`)

> A separate product package that adds `SelectWhereMin` / `SelectWhereMax` — extreme-row selection built entirely on the public query-component layer — next to the frozen built-in `SelectWhereMin` / `SelectWhereMax`.

**Prerequisites:** [Keeping the extreme row (`SelectWhereMax` / `SelectWhereMin`)](../guide/09-distinct.md) · [Extensibility: custom SQL dialects and renderers](extensibility.md) · [API quick reference](api-reference.md)

## Overview

`nextorm.extensions` is its own NuGet package. It references `nextorm.core` only: no provider package, no `InternalsVisibleTo`, no raw SQL and no global handler registration. Everything it does goes through the public query-component surface ([`IQueryComponent`](xref:NextORM.Core.IQueryComponent) and [`EntityBuilder<TEntity>.WithComponent`](xref:NextORM.Core.EntityBuilder`1.WithComponent(NextORM.Core.IQueryComponent))).

It exposes two ordinary extension methods on an entity builder, `SelectWhereMin` and `SelectWhereMax`, plus the component wiring they attach. Both are **row filters, not aggregates**: a surviving row keeps every column, exactly like the built-in operator.

The built-in [`SelectWhereMin`](xref:NextORM.Core.EntityBuilder`1.SelectWhereMinOld``1(System.Linq.Expressions.Expression{System.Func{`0,``0}},NextORM.Core.ExtremeRowTies,System.Linq.Expressions.Expression{System.Func{`0,System.Object}})) / [`SelectWhereMax`](xref:NextORM.Core.EntityBuilder`1.SelectWhereMaxOld``1(System.Linq.Expressions.Expression{System.Func{`0,``0}},NextORM.Core.ExtremeRowTies,System.Linq.Expressions.Expression{System.Func{`0,System.Object}})) are **not changed, wrapped or reimplemented** by this package. They stay the compatibility path and the performance baseline; the extensions package is an independent, public-layer alternative that renders its own query shape.

## Install

```sh
dotnet add package nextorm.extensions
```

The package pulls in `nextorm.core`. Add the provider package for your database as usual (`nextorm.sqlite`, `nextorm.postgres`, …); the extension methods work on any provider the core supports.

## API

All four overloads live in `NextORM.Extensions.ExtremeRowExtensions`:

| Overload | Returns | Purpose |
|---|---|---|
| `SelectWhereMin(source, valueSelector, ties = One, groupBy = null)` | `EntityBuilder<TEntity>` | Keep the row(s) with the smallest value; continue building the query. |
| `SelectWhereMax(source, valueSelector, ties = One, groupBy = null)` | `EntityBuilder<TEntity>` | Keep the row(s) with the greatest value; continue building the query. |
| `SelectWhereMin(source, valueSelector, projection, ties = One, groupBy = null)` | `QueryCommand<TResult>` | Same selection, with the projection applied to each surviving row. |
| `SelectWhereMax(source, valueSelector, projection, ties = One, groupBy = null)` | `QueryCommand<TResult>` | Same selection, with the projection applied to each surviving row. |

```csharp
using System.Linq.Expressions;
using NextORM.Core;

namespace NextORM.Extensions;

public static class ExtremeRowExtensions
{
    public static EntityBuilder<TEntity> SelectWhereMin<TEntity, TValue>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TValue>> valueSelector,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null);

    public static EntityBuilder<TEntity> SelectWhereMax<TEntity, TValue>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TValue>> valueSelector,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null);

    public static QueryCommand<TResult> SelectWhereMin<TEntity, TValue, TResult>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TValue>> valueSelector,
        Expression<Func<TEntity, TResult>> projection,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null);

    public static QueryCommand<TResult> SelectWhereMax<TEntity, TValue, TResult>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TValue>> valueSelector,
        Expression<Func<TEntity, TResult>> projection,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null);
}
```

The whole-entity overloads return the builder, so `.Select(...)`, `.Distinct()` and an outer `.OrderBy(...)` can follow. The projection overloads return a typed command whose terminal methods (`ToList`, `First`, …) execute it.

## One versus `All`

[`ExtremeRowTies`](xref:NextORM.Core.ExtremeRowTies) controls ties and is passed straight through:

- [`ExtremeRowTies.One`](xref:NextORM.Core.ExtremeRowTies.One) (the default) keeps exactly **one** row per partition — one row globally, or one row per group. Which row wins a tie is unspecified, exactly as with the built-in operator; there is no tie-break API.
- [`ExtremeRowTies.All`](xref:NextORM.Core.ExtremeRowTies.All) keeps **every** row tied on the extreme value — all of them globally, or all of them per group.

Ties are decided by the selector only: `All` returns the complete tied set, `One` returns one arbitrary member of it.

## Grouping and composite keys

Without `groupBy` the extremum is taken over the whole source. With `groupBy` the extreme row(s) are selected within each distinct group; a `null` group key is a group of its own.

A composite extreme key is written as an anonymous type (or a value tuple) and is compared **lexicographically**, component by component: `(9, 2)` beats `(8, 100)`. A composite group key partitions on the tuple as a whole. The same decomposition works for both tie policies:

```csharp
// One row per (TeamId, Season) with the greatest (Score, Assists).
var best = dataContext.From<Player>()
    .SelectWhereMax(p => new { p.Score, p.Assists }, ExtremeRowTies.One, p => new { p.TeamId, p.Season })
    .ToList();
```

## NULL behaviour

The contract matches the built-in operator:

- a row whose extreme-key component is `null` never wins the extremum and never appears in the result;
- a group whose key values are all `null` yields no rows at all, and the global form returns nothing when every value is `null`;
- nullable **group** keys are preserved; a `null` group key forms its own group. An `All` selection over a nullable group key uses a partitioned rank window instead of the aggregate-and-join shape, so no null-safe join predicate is needed and those rows are kept on every provider, including ClickHouse. A floating-point value key keeps the same partitioned rank window, because ClickHouse's aggregate and ordering disagree about `NaN`;
- nullable payload columns are preserved unchanged.

## Projections, ordering and composition

- **Input `Where`** applied before the operator is kept and combined with the selection (captured and positional parameters included).
- **Projections** — entity rows, scalar, DTO or anonymous type — work both through the projection overloads and through a `.Select(...)` after the whole-entity overloads; the projection shapes the result only and never participates in winner selection.
- **Outer ordering** — `.OrderBy(...)` / `.ThenBy(...)` sorts the surviving rows; it never participates in winner selection.
- **`Distinct`** — accepted like on the built-in path; duplicates of the projected tuple are removed by the shared builder after the reshape.
- **Multiple components** — an independent component (for example an additional filter) composes. The extreme-row operator occupies one stable slot: a second `SelectWhereMin`/`SelectWhereMax` on the same builder **replaces** it in place (`Min → Max`, `Max → Min`, no nesting), and a foreign component claiming the same exclusive reshape slot is rejected during preparation.
- **Hidden column names** — the component's internal helper column is a private alias (`__nextorm_ext_rn` and suffixes), chosen so it cannot collide with a user column of the same name (`rn`, `rank`, …). The alias never leaks into the result.

## Rejection boundaries

The component refuses the same shapes as the built-in operator, at preparation/build time, instead of silently producing a different answer:

| Rejected | Notes |
|---|---|
| paging (`Skip` / `Take` / `Page` / `Limit`) | The reshape wraps the source in a derived table; paging is rejected. |
| joins, `GroupBy`, `Having`, named windows, window functions in the select list | The component ranks source rows itself. |
| CTEs, set operations (`Union` / `Concat` / `Intersect` / `Except`) | Not expressible over the reshape. |
| `DistinctOn`, `LimitBy`, `ArrayJoin`, `PreWhere` | Provider row-shaping modifiers. |
| `TABLESAMPLE`, `FOR SYSTEM_TIME`, row locking, query/table/index/tables-in-scope hints | Statement-level modifiers. |
| `FINAL`, `SAMPLE`, `SETTINGS`, `FOR JSON` / `FOR XML` | Row/statement modifiers. |
| aggregates (`min`, `max`, `sum`, `avg`, `count`, …) in the select list | The hidden rank column would be aggregated over reshaped rows; rejected explicitly. |
| the in-memory provider | `NotSupportedException`; use the built-in operator or a real database. |

The whole-entity overloads require a **mapped entity source** (`From<TEntity>()`); a name-addressed raw source has no mapped columns to materialise, so it is rejected the same way the built-in operator rejects it.

## Relation to the built-in operator

`SelectWhereMin` / `SelectWhereMax` implement the same observable contract as `SelectWhereMin` / `SelectWhereMax` — `One`/`All`, scalar/composite keys, global/grouped, NULL keys, input `Where`, projections, outer ordering and the rejection boundaries above — and are verified against them with paired execution on the same database. They are *not* an internal alias: they attach a public query component, produce their own SQL shape and share no code with the frozen `ExtremeRow*` implementation.

The built-in operator stays the default and the baseline. The extension component builds its query once per plan (on the cache miss; a confirmed warm hit reuses the cached plan without rebuilding), so the two operators can be compared and mixed in the same application without changing either one's behaviour.

## Complete example

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Sqlite;

[SqlTable("players")]
public sealed class Player
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("team_id")]
    public int TeamId { get; set; }

    [Column("season")]
    public int Season { get; set; }

    [Column("score")]
    public int Score { get; set; }

    [Column("assists")]
    public int? Assists { get; set; }
}

using var db = new SqliteDataContext("Data Source=app.db", new DataContextBuilder());

// Whole-entity rows: global top scorer (one row; ties resolved by the provider, as with the built-in operator).
var top = db.From<Player>()
    .Where(p => p.Season == 2026)
    .SelectWhereMax(p => p.Score)
    .OrderByDescending(p => p.Score)
    .ToList();

// Projection overload: every row tied on the best (Score, Assists) in each (TeamId, Season),
// projected to the columns the caller needs.
var captains = db.From<Player>()
    .SelectWhereMax(
        p => new { p.Score, p.Assists },
        p => new { p.Id, p.TeamId, p.Season, p.Score },
        ExtremeRowTies.All,
        p => new { p.TeamId, p.Season })
    .OrderBy(p => p.TeamId)
    .ThenBy(p => p.Season)
    .ToList();

// The whole-entity builder form composes with the rest of the fluent API.
var ids = db.From<Player>()
    .SelectWhereMin(p => p.Score)
    .Select(p => p.Id)
    .Distinct()
    .ToList();
```

## See also

- [Keeping the extreme row (`SelectWhereMax` / `SelectWhereMin`)](../guide/09-distinct.md) — the shared result contract
- [Native extreme-row strategies](select-where-extrema-native.md) — how the built-in operator renders `One` natively on some providers
- [Extensibility: custom SQL dialects and renderers](extensibility.md)
- [API quick reference](api-reference.md)
