# Query reuse: plan cache and [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken))

> Keep the SQL and row mapper for a hot query instead of rebuilding them on every call, either through the implicit plan cache used by every terminal or through an explicit [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1) returned by [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)).

**Prerequisites:** [Quickstart](../getting-started/02-quickstart.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Dependency injection](../getting-started/04-dependency-injection.md).

## Overview

Every terminal such as [`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) does several things: it turns the fluently built [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) into SQL and a row mapper, prepares a [`DbCommand`](xref:NextORM.Core.DbPreparedQueryCommand`1.DbCommand), and reads the result. Two independent mechanisms let the first part be paid once:

* the **implicit plan cache** - automatic, keyed by the structural shape of the query;
* **[`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken))** - explicit, returns an [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1) that you keep and execute yourself.

The two are easy to confuse because they look alike from the outside. They differ in key, lifetime, thread-safety and whether they populate the cache:

| | Implicit plan cache | Explicit [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)) |
|---|---|---|
| Entry point | any terminal on [`EntityBuilder`](xref:NextORM.Core.EntityBuilder) / [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) ([`ToList`](xref:NextORM.Core.EntityBuilderExtensions.ToList``1(NextORM.Core.EntityBuilder{``0},System.ReadOnlySpan{System.Object})), [`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), [`ToAsyncEnumerable`](xref:NextORM.Core.EntityBuilderExtensions.ToAsyncEnumerable``1(NextORM.Core.EntityBuilder{``0},System.Object[])), ...) | [`Prepare`](xref:NextORM.Core.QueryCommand`1.Prepare(System.Boolean,System.Threading.CancellationToken)) / `EntityBuilder<T>.Prepare()` |
| Lookup key | structural hash of the query shape | none - you hold the returned command |
| Lifetime | until `PurgeQueryCache()` or process exit | as long as you keep the reference |
| Scope | **per thread**, shared by every [`IDataContext`](xref:NextORM.Core.IDataContext) on that thread | the instance you keep |
| Populates the cache | yes | **no** |
| Safe for concurrent use | yes, by construction (per-thread entry) | **no** (one mutable [`DbCommand`](xref:NextORM.Core.DbPreparedQueryCommand`1.DbCommand) and enumerator) |
| Streamable (`IAsyncEnumerable`) | yes | only with `nonStreamUsing: false` |

This page documents the API and the caching rules; the measured cost of each option is not repeated here.

## Terminals on [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1)

`Select(...)` (and the rest of the fluent surface) produces a [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1); the terminal operators are declared on that type. They split into buffered/scalar and streaming terminals, and every one of them routes through the plan cache.

Buffered / scalar:

| Terminal | Returns |
|---|---|
| [`ToList`](xref:NextORM.Core.EntityBuilderExtensions.ToList``1(NextORM.Core.EntityBuilder{``0},System.ReadOnlySpan{System.Object})) / `ToList(params ReadOnlySpan<object?>)` | `List<TResult>` |
| `ToListAsync(...)` (optional `CancellationToken`, optional `params object[]`) | `Task<List<TResult>>` |
| [`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})) / `FirstAsync(...)` | `TResult` |
| [`FirstOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.FirstOrDefault``1(NextORM.Core.EntityBuilder{``0})) / `FirstOrDefaultAsync(...)` | `TResult?` |
| [`Single`](xref:NextORM.Core.EntityBuilderExtensions.Single``1(NextORM.Core.EntityBuilder{``0})) / `SingleAsync(...)` | `TResult` |
| [`SingleOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.SingleOrDefault``1(NextORM.Core.EntityBuilder{``0})) / `SingleOrDefaultAsync(...)` | `TResult?` |
| [`Any`](xref:NextORM.Core.EntityBuilderExtensions.Any``1(NextORM.Core.EntityBuilder{``0})) / `AnyAsync(...)` | `bool` |
| [`ExecuteScalar`](xref:NextORM.Core.QueryCommand`1.ExecuteScalar(System.ReadOnlySpan{System.Object})) / `ExecuteScalarAsync(...)` | `TResult?` |

Streaming:

| Terminal | Returns |
|---|---|
| `ToAsyncEnumerable(...)` | `IAsyncEnumerable<TResult>` |
| `Pipeline(...)` | `IAsyncEnumerable<TResult>` |
| `ToEnumerable(...)` | `IEnumerable<TResult>` |
| `CreateAsyncEnumerator(...)` | `IAsyncEnumerator<TResult>` |
| `CreateEnumeratorAsync(...)` | `Task<IEnumerator<TResult>>` |

The `params object[]` (or `ReadOnlySpan<object?>`) accepted by the terminals carries the runtime values for the [`Parameter`](xref:NextORM.Core.SqlFunctions.Parameter``1(System.Int32)) placeholders in the query.

```csharp
using var ctx = new SqliteDataContext("Data Source=app.db", new DataContextBuilder());

var rows = await ctx.From<ISimpleEntity>()
    .Select(x => x.Id)
    .ToListAsync();
```

```sql
select id from simple_entity
```

Because the cache key is structural, two calls with a *different lambda instance* but the same shape are a cache hit. That is what makes the cache useful for queries written inline in a loop:

```csharp
for (var i = 0; i < 2; i++)
{
    var entityId = i;

    var row = await ctx.From<ISimpleEntity>()
        .Where(entity => entity.Id == entityId)
        .Select(entity => new { Id = (long)entity.Id })
        .FirstOrDefaultAsync();
}
```

Both iterations reuse the same cache entry.

## Explicit [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken))

[`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)) is declared on both [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) and `EntityBuilder<T>` and returns [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1):

```csharp
public IPreparedQueryCommand<TResult> Prepare(bool nonStreamUsing = true, CancellationToken cancellationToken = default)
```

The default (`nonStreamUsing: true`) is optimised for buffered and scalar results. The returned command is not bound to the context that created it: every terminal takes the [`IDataContext`](xref:NextORM.Core.IDataContext) as its first argument, so the same prepared command can be executed against another context of the same provider.

```csharp
var prepared = ctx.From<ISimpleEntity>()
    .Select(x => x.Id)
    .Prepare();

// Terminal methods declared on IPreparedQueryCommand<TResult>.
List<int> all = prepared.ToList(ctx);
int first = prepared.First(ctx);
List<int> asyncAll = await prepared.ToListAsync(ctx);
int? scalar = prepared.ExecuteScalar(ctx, throwIfNull: false);

// The IDataContext extension overloads in DataContextExtensions are equivalent.
List<int> all2 = ctx.ToList(prepared);
List<int> asyncAll2 = await ctx.ToListAsync(prepared);
int first2 = await ctx.FirstAsync(prepared);
```

The same command can be re-executed with different runtime parameter values:

```csharp
var byId = ctx.From<ISimpleEntity>()
    .Where(x => x.Id == SqlFunctions.Parameter<int>(0))
    .Select(x => x.Id)
    .Prepare();

var one = await byId.FirstAsync(ctx, 42);    // 42 fills norm_p0
var many = await byId.ToListAsync(ctx, 43);  // same prepared command, new value
```

### Streaming requires `nonStreamUsing: false`

A prepared command created with the default owns no [`ResultSetEnumerator<TResult>`](xref:NextORM.Core.ResultSetEnumerator`1).#ctor(NextORM.Core.DbPreparedQueryCommand{`0},Microsoft.Extensions.ObjectPool.ObjectPool{System.Text.StringBuilder}). Buffered and scalar terminals work; a streaming terminal fails with an `InvalidOperationException` whose message tells you to use `Prepare(nonStreamUsing: false)`. [`ToEnumerable`](xref:NextORM.Core.EntityBuilderExtensions.ToEnumerable``1(NextORM.Core.EntityBuilder{``0},System.Object[])) is a lazy iterator, so on that path the exception surfaces when enumeration starts, not when the call is made.

```csharp
// Buffered default: no enumerator is created.
var buffered = ctx.From<ISimpleEntity>().Select(x => x.Id).Prepare();
buffered.ToList(ctx); // fine

// Streaming: the only way to get a streamable prepared command.
var streaming = ctx.From<ISimpleEntity>()
    .Select(x => x.Id)
    .Prepare(nonStreamUsing: false);

await foreach (var id in streaming.ToAsyncEnumerable(ctx))
{
    Console.WriteLine(id);
}
```

> **Sharing rules.** A prepared command owns one mutable [`DbCommand`](xref:NextORM.Core.DbPreparedQueryCommand`1.DbCommand) and one [`ResultSetEnumerator<TResult>`](xref:NextORM.Core.ResultSetEnumerator`1).#ctor(NextORM.Core.DbPreparedQueryCommand{`0},Microsoft.Extensions.ObjectPool.ObjectPool{System.Text.StringBuilder}). Parameter values, `DbCommand.Connection` and the enumerator's reader are overwritten on every execution. Do not share one prepared command across threads and do not run two overlapping iterations over the same prepared command. The implicit cache does not have this problem: it is per thread.

### [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)) does not populate the plan cache

[`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)) calls the planner with `storeInCache: false`, so it cannot be found by a later implicit lookup and cannot "pollute" the cache:

```csharp
ctx.PurgeQueryCache();

var prepared = ctx.From<ISimpleEntity>().Select(x => x.Id).Prepare();
prepared.ToList(ctx); // executes, but adds no plan-cache entry

// A later implicit terminal builds its own plan.
var implicitRows = ctx.From<ISimpleEntity>().Select(x => x.Id).ToList();
```

## Plan-cache scope and purging

The implicit cache (`DataContext._queryPlanCache`) is a `[ThreadStatic]` dictionary keyed by `(ContextType, QueryPlan)`:

* **Per thread.** An entry created on thread A is invisible to thread B; per thread-pool hop the hit rate drops to zero until the shape is seen again on that thread.
* **Per context type.** SQLite, PostgreSQL and SQL Server produce different SQL and different [`DbCommand`](xref:NextORM.Core.DbPreparedQueryCommand`1.DbCommand) implementations for the same shape, so they get separate entries.
* **No automatic invalidation.** Entries live until `PurgeQueryCache()` is called (or the process ends). After a schema change, purge the cache (or recreate the context) before reusing the affected shapes.
* `QueryCommand<TResult>.Cache = false` disables hashing and lookup for that command. This is not a cheap "skip the cache" switch: it forces a full plan rebuild on every call.

```csharp
ctx.PurgeQueryCache();
```

Other caches have a deliberately different sharing scope. Metadata, select lists, compiled expression delegates and the in-list accessors are process-wide; the in-memory expression cache is per context instance because its entries capture the context itself:

```csharp
using var ctx = new InMemoryDataContext();

ReferenceEquals(ctx.Metadata, DataContextCache.Metadata);                 // true  (process-wide)
ReferenceEquals(ctx.SelectListCache, DataContextCache.SelectListCache);   // true  (process-wide)
ReferenceEquals(ctx.ExpressionsCache, DataContextCache.ExpressionsCache); // false (per instance)
```

## Captured `in`/`Contains` collections

An `in` list or a `Contains` call over a captured collection is translated to a parameterised `IN` predicate. The *values* are not part of the plan key - the *shape* is (the evaluated item count and whether a `null` is present). This has two consequences:

* two same-shape builds (the same collection length, same null presence) share one plan, even from different call sites;
* if the collection is grown or reassigned between executions, the shape hash changes, so a fresh plan matching the new parameter count is produced and the current values are read through the cached accessor.

```csharp
var values = new long[] { 1 };

var first = ctx.GetPreparedQueryCommand(
    ctx.From<IComplexEntity>().Where(c => SqlFunctions.Sql.@in(c.Id, values)).Select(c => c.Id),
    createEnumerator: false, storeInCache: true, CancellationToken.None);
// one parameter: p0

values = new long[] { 2, 3 };

var second = ctx.GetPreparedQueryCommand(
    ctx.From<IComplexEntity>().Where(c => SqlFunctions.Sql.@in(c.Id, values)).Select(c => c.Id),
    createEnumerator: false, storeInCache: true, CancellationToken.None);
// two parameters: p0, p1
```

```sql
id in ($p0, $p1)
```

`values.Contains(c.Id)` is handled the same way. A `null` element adds an `is null` branch (for example `(nullableint in ($p0) or nullableint is null)`), and an empty or all-null collection folds to a constant (`1 = 0` / `is null`) with no parameters.

A captured collection indexed by a query expression (`dict[column]`) folds the same way: the entry count is part of the plan key, so the generated `CASE` is rebuilt when the collection changes. See [Filtering (WHERE)](02-filtering-where.md).

## Provider differences

| Provider | Behaviour |
|---|---|
| SQLite | Plan cache and [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)) are core behaviour; a prepared command wraps a `SqliteCommand`. |
| SQL Server | Same mechanism; `TOP` vs `OFFSET ... FETCH` paging is part of the recorded plan shape, so the paging mode is baked into the cached SQL. |
| PostgreSQL | Same mechanism; a prepared command wraps an `NpgsqlCommand`. |
| MySQL | Plan cache and [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)) are core behaviour; a prepared command wraps a `MySqlCommand`. |
| MariaDB | Same mechanism; a prepared command wraps a `MySqlCommand` (the MySQL driver). |
| ClickHouse | Plan cache and [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)) are core behaviour; a prepared command wraps a `ClickHouseCommand`. |
| In-memory | [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) keeps its own compiled-query cache and returns an [`InMemoryPreparedQueryCommand<TResult>`](xref:NextORM.Core.InMemoryPreparedQueryCommand`1); [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)) works, but raw-SQL [`PrepareFromSql`](xref:NextORM.Core.EntityExtensions.PrepareFromSql``1(NextORM.Core.EntityBuilder{``0},System.String)) is not supported (`NotSupportedException`). |

## See also

* [Connections and logging](16-connections-and-logging.md)
* [Dependency injection](../getting-started/04-dependency-injection.md)
* [Documentation index](../index.md)

---

Source: `tests/nextorm.sqlite.tests/PlanCacheTests.cs:49` (buffered then streaming),
`tests/nextorm.sqlite.tests/PlanCacheTests.cs:99` (`Prepare(nonStreamUsing: false)`),
`tests/nextorm.sqlite.tests/PlanCacheTests.cs:124` (buffered reuse),
`tests/nextorm.sqlite.tests/PlanCacheTests.cs:152` (streaming the default throws),
`tests/nextorm.sqlite.tests/PlanCacheTests.cs:257` ([`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)) does not populate the cache);
`tests/nextorm.sqlite.tests/InListCacheTests.cs:41` (same-shape captured collection),
`tests/nextorm.sqlite.tests/InListCacheTests.cs:92` (reassigned array),
`tests/nextorm.sqlite.tests/InListCacheTests.cs:117` (grown list);
`tests/nextorm.core.tests/DataContextCacheScopeTests.cs:20` (cache sharing scope);
`tests/nextorm.integration.tests/CommonTestSuite.Cache.cs:6`;
`src/nextorm.core/Query/QueryCommand.TResult.cs:38` ([`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken))),
`src/nextorm.core/DataContext/Cache/IPreparedQueryCommand.cs:5` (prepared terminals),
`src/nextorm.core/DataContext/DataContext.cs:315` (`GetPreparedQueryCommand`).
