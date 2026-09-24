# In-memory provider

> [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) runs queries against in-process CLR collections; use it for unit tests and query-shape/plan-cache tests where starting a database is unnecessary.

**Prerequisites:** [Provider overview](overview.md) · [Dependency injection](../getting-started/04-dependency-injection.md)

## Overview

The in-memory provider is built into `nextorm` (no extra package) and lives in
`src/nextorm.core/DataContext/InMemoryDataContext.cs`. The public type is [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext), which
implements [`IDataContext`](xref:NextORM.Core.IDataContext) directly — it has no dialect, no connection and no SQL, and evaluates the
query expression tree over the data you attach to it.

Unlike the SQL providers, [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) requires no connection string. Query metadata and select
lists are shared process-wide with the SQL providers through [`DataContextCache`](xref:NextORM.Core.DataContextCache), while compiled
delegates are per-instance (they capture the context they were compiled for).

Use it when you want:

- unit tests for application code that depends on [`IDataContext`](xref:NextORM.Core.IDataContext);
- fast tests of query shape, projection and join behaviour without a container or file;
- tests of the implicit plan cache ([`QueryPlanEqualityComparer`](xref:NextORM.Core.QueryPlanEqualityComparer), [`GetCacheVersion`](xref:NextORM.Core.QueryPlan.GetCacheVersion)).

It is not a SQL engine: it does not validate that a query would run on a real provider, and several
features are deliberately unsupported (see below).

## Registration and usage

With dependency injection:

```csharp
using NextORM.Core;

services.AddNextOrmContext<InMemoryDataContext>();
```

```csharp
using NextORM.Core;

using var ctx = new InMemoryDataContext();
ctx.From<SimpleEntity>().WithData(new[]
{
    new SimpleEntity { Id = 1 },
    new SimpleEntity { Id = 2 },
});

var ids = ctx.From<SimpleEntity>()
    .Where(it => it.Id == 1)
    .Select(it => new { it.Id })
    .SingleOrDefault();

// ids.Id == 1
```

[`WithData`](xref:NextORM.Core.InMemoryCommandBuilderExtensions.WithData``1(NextORM.Core.EntityBuilder{``0},System.Collections.Generic.IEnumerable{``0})) and [`WithAsyncData`](xref:NextORM.Core.InMemoryCommandBuilderExtensions.WithAsyncData``1(NextORM.Core.EntityBuilder{``0},System.Collections.Generic.IAsyncEnumerable{``0})) (`src/nextorm.core/Builders/InMemoryCommandBuilder.cs`) attach a
collection to the context's `Data` dictionary keyed by entity type; [`WithData`](xref:NextORM.Core.InMemoryCommandBuilderExtensions.WithData``1(NextORM.Core.EntityBuilder{``0},System.Collections.Generic.IEnumerable{``0})) takes an `IEnumerable<T>`
and [`WithAsyncData`](xref:NextORM.Core.InMemoryCommandBuilderExtensions.WithAsyncData``1(NextORM.Core.EntityBuilder{``0},System.Collections.Generic.IAsyncEnumerable{``0})) an `IAsyncEnumerable<T>`. Both are no-ops for other providers.

```csharp
ctx.From<SimpleEntity>().WithAsyncData(GetRows());

static async IAsyncEnumerable<SimpleEntity> GetRows()
{
    yield return new SimpleEntity { Id = 1 };
    await Task.Delay(0);
    yield return new SimpleEntity { Id = 2 };
}
```

## Supported feature subset

Covered by `InMemoryTests` and `InMemoryJoinTests`:

| Feature | Evidence |
|---|---|
| Projection: anonymous, primitive/scalar, `Tuple` | `InMemoryTests.SelectPrimitive_ShouldReturnData`, `TestTuples` |
| [`Where`](xref:NextORM.Core.EntityBuilder`1.Where(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}})), including `==` on nullable and captured values | `InMemoryTests.TestWhere`, `Contains_ShouldFilterData` |
| Subquery used as a `FROM` source (`ctx.From(subQuery)`) | `InMemoryTests.TestWhere_Subquery` |
| Buffered and async sources ([`WithData`](xref:NextORM.Core.InMemoryCommandBuilderExtensions.WithData``1(NextORM.Core.EntityBuilder{``0},System.Collections.Generic.IEnumerable{``0})) / [`WithAsyncData`](xref:NextORM.Core.InMemoryCommandBuilderExtensions.WithAsyncData``1(NextORM.Core.EntityBuilder{``0},System.Collections.Generic.IAsyncEnumerable{``0}))) | `InMemoryTests.TestAsync` |
| Streaming with [`Pipeline`](xref:NextORM.Core.QueryCommand`1.Pipeline(System.Object[])), observing cancellation | `InMemoryTests.TestFetch`, `TestFetch_PipelineStopsOnCancellation` |
| [`Limit`](xref:NextORM.Core.Paging.Limit) / [`Offset`](xref:NextORM.Core.Paging.Offset) / [`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})) / [`Single`](xref:NextORM.Core.EntityBuilderExtensions.Single``1(NextORM.Core.EntityBuilder{``0})) and their `OrDefault` forms | `InMemoryTests.Top_ShouldLimitData`, `First_ShouldReturnFirst`, `Single_ShouldReturnSingle` |
| [`OrderBy`](xref:NextORM.Core.EntityBuilder`1.OrderBy(System.Int32)) / [`OrderByDescending`](xref:NextORM.Core.EntityBuilder`1.OrderByDescending(System.Int32)), including async sources | `InMemoryTests.OrderBy_ShouldSortData`, `OrderByOverAsyncSource_ShouldSortData` |
| Materializers [`ToArray`](xref:NextORM.Core.EntityBuilderExtensions.ToArray``1(NextORM.Core.EntityBuilder{``0},System.ReadOnlySpan{System.Object})) / [`ToHashSet`](xref:NextORM.Core.EntityBuilderExtensions.ToHashSet``1(NextORM.Core.EntityBuilder{``0},System.Collections.Generic.IEqualityComparer{``0},System.ReadOnlySpan{System.Object})) / [`ToDictionary`](xref:NextORM.Core.EntityBuilderExtensions.ToDictionary``2(NextORM.Core.EntityBuilder{``0},System.Func{``0,``1},System.Collections.Generic.IEqualityComparer{``1},System.ReadOnlySpan{System.Object})) (and async) | `InMemoryTests.ToArray_ShouldReturnData`, `ToHashSet_ShouldReturnData`, `ToDictionary_ShouldReturnData` |
| [`Any`](xref:NextORM.Core.EntityBuilderExtensions.Any``1(NextORM.Core.EntityBuilder{``0})) and projected `exists` | `InMemoryTests.SelectAny_ShouldReturnData`, [`Any`](xref:NextORM.Core.EntityBuilderExtensions.Any``1(NextORM.Core.EntityBuilder{``0})) |
| [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) over value-equality projections | `InMemoryTests.TestDistinct` |
| Joins: inner, left, right, full, cross, chained to 8 tables | `InMemoryJoinTests.TestJoin`, `TestLeftJoin`, `TestRightJoinChained`, `TestFullJoinChained`, `TestCrossJoin8Tables_ShouldCloneAndMaterializeAtEveryArity` |
| Aggregates [`Count`](xref:NextORM.Core.EntityBuilderExtensions.Count``1(NextORM.Core.EntityBuilder{``0}))/[`Sum`](xref:NextORM.Core.EntityBuilderExtensions.Sum``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}}))/[`Min`](xref:NextORM.Core.EntityBuilderExtensions.Min``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}}))/[`Max`](xref:NextORM.Core.EntityBuilderExtensions.Max``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}}))/[`Avg`](xref:NextORM.Core.EntityBuilderExtensions.Avg``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}}))/[`Stdev`](xref:NextORM.Core.EntityBuilderExtensions.Stdev``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}}))/[`Var`](xref:NextORM.Core.EntityBuilderExtensions.Var``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}})) (buffered sources) | `InMemoryTests.Count_ShouldReturnRowCount`, `Sum_ShouldReturnSum`, `MinMax_ShouldReturnBounds`, `Avg_ShouldReturnAverage` |
| [`GroupBy`](xref:NextORM.Core.EntityBuilder`1.GroupBy``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) / [`Having`](xref:NextORM.Core.EntityBuilder`1.Having(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}})) with per-group aggregates (buffered sources) | `InMemoryTests.GroupBy_ShouldAggregatePerGroup`, `GroupBy_Having_ShouldFilterGroups`, `GroupBy_Avg_ShouldAggregatePerGroup`, `GroupBy_OrderByColumn_ShouldSortGroups` |
| Set operations `UNION` / `UNION ALL` / `INTERSECT` / `INTERSECT ALL` / `EXCEPT` / `EXCEPT ALL` | `InMemoryTests.Union_ShouldRemoveDuplicates`, `UnionAll_ShouldKeepDuplicates`, `Intersect_ShouldReturnOnlyCommonRows`, `Except_ShouldReturnOnlyLeftRows`, `SetOperations_WhenChained_ShouldApplyLeftToRight` |
| [`Last`](xref:NextORM.Core.EntityBuilderExtensions.Last``1(NextORM.Core.EntityBuilder{``0})) / [`LastOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.LastOrDefault``1(NextORM.Core.EntityBuilder{``0})) over an ordered query (reversed `ORDER BY`) | `InMemoryTests.Last_ShouldReturnLastOrderedRow`, `LastOrDefault_ShouldReturnLastOrderedRow` |
| Buffered subquery source (`ctx.From(cmd)`) with sync materialization and aggregates | `InMemoryTests.SubquerySource_SyncToList_ShouldReturnRows`, `SubquerySource_Count_ShouldReturnRowCount`, `SetOperation_AsSubquery_Count_ShouldReturnRowCount` |
| [`SelectMany`](xref:NextORM.Core.EntityBuilder`1.SelectMany``1(System.Linq.Expressions.Expression{System.Func{`0,System.Collections.Generic.IEnumerable{``0}}})) (flatten, correlated) and [`GroupJoin`](xref:NextORM.Core.EntityBuilder`1.GroupJoin``3(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``1}},System.Linq.Expressions.Expression{System.Func{``0,``1}},System.Linq.Expressions.Expression{System.Func{`0,System.Collections.Generic.IEnumerable{``0},``2}})) (grouped inner rows) | `InMemorySelectManyTests.SelectMany_Correlated_ShouldFlattenPerRow`, `GroupJoin_ShouldGroupInnerRows` |

The in-memory provider shares the same `EntityBuilder<T>` fluent API as the SQL providers, so the same query
object works against both — **except** [`SelectMany`](xref:NextORM.Core.EntityBuilder`1.SelectMany``1(System.Linq.Expressions.Expression{System.Func{`0,System.Collections.Generic.IEnumerable{``0}}}))/[`GroupJoin`](xref:NextORM.Core.EntityBuilder`1.GroupJoin``3(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``1}},System.Linq.Expressions.Expression{System.Func{``0,``1}},System.Linq.Expressions.Expression{System.Func{`0,System.Collections.Generic.IEnumerable{``0},``2}})), which are in-memory only: on a SQL provider
they throw `NotSupportedException` immediately (see [Limitations](../advanced/limitations.md)).

## Unsupported

The provider fails loudly instead of returning wrong results:

- **Table-valued functions** throw `NotSupportedException` with a message mentioning the in-memory
  provider. A database TVF cannot be evaluated in process.

  ```csharp
  var act = () => ctx
      .FromTableFunction(() => Tvf.SimpleTvf())
      .Select(it => new { it.Id })
      .ToList();
  // NotSupportedException: "... not supported by the in-memory provider."
  ```

- **[`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) over a reference type without value equality** throws `NotSupportedException` mentioning
  `DISTINCT`: SQL compares by value, which the CLR type cannot reproduce without an
  `Equals`/`GetHashCode` override. Project an anonymous type or a value type, or override equality.

  ```csharp
  ctx.From<SimpleEntity>().Distinct().ToList();
  // NotSupportedException: "DISTINCT is not supported by the in-memory provider for projection type ..."
  ```

- **Raw SQL** ([`PrepareFromSql`](xref:NextORM.Core.EntityExtensions.PrepareFromSql``1(NextORM.Core.EntityBuilder{``0},System.String))) is not supported (`NotSupportedException`).
- **Aggregates or [`GroupBy`](xref:NextORM.Core.EntityBuilder`1.GroupBy``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) over an `IAsyncEnumerable` source** throw `NotSupportedException`; both are
  computed for buffered `IEnumerable` sources.
- **Filtered aggregates** (`SqlFunctions.Sql.count(e => filter)` and the `(value, filter)` overloads) and
  **grouped ordering by expression** throw `NotSupportedException`; order groups by column index instead.
- **Set operations over an `IAsyncEnumerable` source** throw `NotSupportedException`; operands are
  buffered.
- **[`Last`](xref:NextORM.Core.EntityBuilderExtensions.Last``1(NextORM.Core.EntityBuilder{``0}))/[`LastOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.LastOrDefault``1(NextORM.Core.EntityBuilder{``0})) without `ORDER BY`** throw `InvalidOperationException`: a query without
  ordering has no defined last row.
- **Aggregates over an async subquery source** throw `NotSupportedException`; buffered subquery sources
  are folded.
- **Correlated subqueries** (a subquery referencing the outer row, for example
  `SqlFunctions.Sql.exists(inner.Where(i => i.Id == outer.Id))`) are evaluated once per outer row for
  depth-one correlation: scalar subqueries, aggregate terminals, `EXISTS` and `IN` work in `SELECT`,
  `WHERE` and `ORDER BY` (see [Subqueries](../guide/06-subqueries.md#correlated-scalar-subquery)).
  Correlation depth greater than one, an outer reference inside the inner projection or `ORDER BY`, a
  correlated `GROUP BY`/`HAVING` and an async inner source throw `NotSupportedException`.

## Provider differences

| Aspect | In-memory |
|---|---|
| Package | core `nextorm` |
| Connection | none |
| SQL text | none (expression tree is evaluated in process) |
| Parameter placeholders | not applicable |
| TVF sources | `NotSupportedException` |
| Raw SQL | `NotSupportedException` |
| [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) on non-value-equality types | `NotSupportedException` |
| Correlated subqueries | `NotSupportedException` |
| [`SelectMany`](xref:NextORM.Core.EntityBuilder`1.SelectMany``1(System.Linq.Expressions.Expression{System.Func{`0,System.Collections.Generic.IEnumerable{``0}}})) / [`GroupJoin`](xref:NextORM.Core.EntityBuilder`1.GroupJoin``3(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``1}},System.Linq.Expressions.Expression{System.Func{``0,``1}},System.Linq.Expressions.Expression{System.Func{`0,System.Collections.Generic.IEnumerable{``0},``2}})) | supported (these operators are in-memory only) |

## See also

- [Provider overview](overview.md)
- [SQLite](sqlite.md)
- [SQL Server](sqlserver.md)
- [PostgreSQL](postgres.md)
- [Query reuse: cache vs Prepare](../guide/15-query-reuse.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `tests/nextorm.core.tests/InMemoryTests.cs:28,46,55,212,226,244,349,374`,
`tests/nextorm.core.tests/InMemoryJoinTests.cs:14,47,64,107,145,162`,
`tests/nextorm.core.tests/InMemorySelectManyTests.cs`,
`src/nextorm.core/DataContext/InMemoryDataContext.cs`,
`src/nextorm.core/Builders/InMemoryCommandBuilder.cs`.
