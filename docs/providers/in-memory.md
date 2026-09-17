# In-memory provider

> `InMemoryContext` runs queries against in-process CLR collections; use it for unit tests and query-shape/plan-cache tests where starting a database is unnecessary.

**Prerequisites:** [Provider overview](overview.md) · [Dependency injection](../getting-started/04-dependency-injection.md)

## Overview

The in-memory provider is built into `nextorm` (no extra package) and lives in
`src/nextorm.core/DataContext/InMemoryDataContext.cs`. The public type is `InMemoryContext`, which
implements `IDataContext` directly — it has no dialect, no connection and no SQL, and evaluates the
query expression tree over the data you attach to it.

Unlike the SQL providers, `InMemoryContext` requires no connection string. Query metadata and select
lists are shared process-wide with the SQL providers through `DataContextCache`, while compiled
delegates are per-instance (they capture the context they were compiled for).

Use it when you want:

- unit tests for application code that depends on `IDataContext`;
- fast tests of query shape, projection and join behaviour without a container or file;
- tests of the implicit plan cache (`QueryPlanEqualityComparer`, `GetCacheVersion`).

It is not a SQL engine: it does not validate that a query would run on a real provider, and several
features are deliberately unsupported (see below).

## Registration and usage

With dependency injection:

```csharp
using nextorm.core;

services.AddNextOrmContext<InMemoryContext>();
```

```csharp
using nextorm.core;

using var ctx = new InMemoryContext();
ctx.Create<SimpleEntity>().WithData(new[]
{
    new SimpleEntity { Id = 1 },
    new SimpleEntity { Id = 2 },
});

var ids = ctx.Create<SimpleEntity>()
    .Where(it => it.Id == 1)
    .Select(it => new { it.Id })
    .SingleOrDefault();

// ids.Id == 1
```

`WithData` and `WithAsyncData` (`src/nextorm.core/Builders/InMemoryCommandBuilder.cs`) attach a
collection to the context's `Data` dictionary keyed by entity type; `WithData` takes an `IEnumerable<T>`
and `WithAsyncData` an `IAsyncEnumerable<T>`. Both are no-ops for other providers.

```csharp
ctx.Create<SimpleEntity>().WithAsyncData(GetRows());

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
| `Where`, including `==` on nullable and captured values | `InMemoryTests.TestWhere` |
| Subquery used as a `FROM` source (`ctx.From(subQuery)`) | `InMemoryTests.TestWhere_Subquery` |
| Buffered and async sources (`WithData` / `WithAsyncData`) | `InMemoryTests.TestAsync` |
| Streaming with `Pipeline`, observing cancellation | `InMemoryTests.TestFetch`, `TestFetch_PipelineStopsOnCancellation` |
| `Limit` / `Offset` / `First` / `Single` and their `OrDefault` forms | `InMemoryTests.Top_ShouldLimitData`, `First_ShouldReturnFirst`, `Single_ShouldReturnSingle` |
| `OrderBy` / `OrderByDescending` (buffered sources) | `InMemoryTests.OrderBy_ShouldSortData` |
| `Any` and projected `exists` | `InMemoryTests.SelectAny_ShouldReturnData`, `Any` |
| `Distinct` over value-equality projections | `InMemoryTests.TestDistinct` |
| Joins: inner, left, right, full, cross, chained to 8 tables | `InMemoryJoinTests.TestJoin`, `TestLeftJoin`, `TestRightJoinChained`, `TestFullJoinChained`, `TestCrossJoin8Tables_ShouldCloneAndMaterializeAtEveryArity` |

The in-memory provider shares the same `Entity<T>` fluent API as the SQL providers, so the same query
object works against both.

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

- **`Distinct` over a reference type without value equality** throws `NotSupportedException` mentioning
  `DISTINCT`: SQL compares by value, which the CLR type cannot reproduce without an
  `Equals`/`GetHashCode` override. Project an anonymous type or a value type, or override equality.

  ```csharp
  ctx.Create<SimpleEntity>().Distinct().ToList();
  // NotSupportedException: "DISTINCT is not supported by the in-memory provider for projection type ..."
  ```

- **Raw SQL** (`PrepareFromSql`) is not implemented (`NotImplementedException`).
- **Ordering over an `IAsyncEnumerable` source** is not implemented; ordering is applied for buffered
  `IEnumerable` sources.

## Provider differences

| Aspect | In-memory |
|---|---|
| Package | core `nextorm` |
| Connection | none |
| SQL text | none (expression tree is evaluated in process) |
| Parameter placeholders | not applicable |
| TVF sources | `NotSupportedException` |
| Raw SQL | `NotImplementedException` |
| `Distinct` on non-value-equality types | `NotSupportedException` |

## See also

- [Provider overview](overview.md)
- [SQLite](sqlite.md)
- [SQL Server](sqlserver.md)
- [PostgreSQL](postgres.md)
- [Query reuse: cache vs Prepare](../guide/15-query-reuse.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `test/nextorm.core.tests/InMemoryTests.cs:28,46,55,212,226,244,349,374`,
`test/nextorm.core.tests/InMemoryJoinTests.cs:14,47,64,107,145,162`,
`src/nextorm.core/DataContext/InMemoryDataContext.cs`,
`src/nextorm.core/Builders/InMemoryCommandBuilder.cs`.
