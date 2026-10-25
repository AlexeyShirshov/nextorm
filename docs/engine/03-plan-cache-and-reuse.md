# Plan cache and reuse: [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken))

> Keep the SQL and row mapper for a hot query instead of rebuilding them on every call, either through the implicit plan cache used by every terminal or through an explicit [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1) returned by [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)).

**Prerequisites:** [Quickstart](../getting-started/02-quickstart.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Dependency injection](../getting-started/04-dependency-injection.md).

## Overview

Every terminal such as [`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) does several things: it turns the fluently built [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) into SQL and a row mapper, prepares a `DbCommand`, and reads the result. Two independent mechanisms let the first part be paid once:

* the **implicit plan cache** - automatic, keyed by the structural shape of the query;
* **[`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken))** - explicit, returns an [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1) that you keep and execute yourself.

The two are easy to confuse because they look alike from the outside. They differ in key, lifetime, thread-safety and whether they populate the cache:

| | Implicit plan cache | Explicit [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) |
|---|---|---|
| Entry point | any terminal on [`EntityBuilder`](xref:NextORM.Core.EntityBuilder) / [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) ([`ToList`](xref:NextORM.Core.EntityBuilderExtensions.ToList``1(NextORM.Core.EntityBuilder{``0},System.ReadOnlySpan{System.Object})), [`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), [`ToAsyncEnumerable`](xref:NextORM.Core.EntityBuilderExtensions.ToAsyncEnumerable``1(NextORM.Core.EntityBuilder{``0},System.Object[])), ...) | [`Prepare`](xref:NextORM.Core.QueryCommand`1.Prepare(NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) / `EntityBuilder<T>.Prepare()` |
| Lookup key | structural hash of the query shape | none - you hold the returned command |
| Lifetime | until `PurgeQueryCache()` or process exit | as long as you keep the reference |
| Scope | **per thread**, shared by every [`IDataContext`](xref:NextORM.Core.IDataContext) on that thread | the instance you keep |
| Populates the cache | yes | **no** |
| Safe for concurrent use | yes, by construction (per-thread entry) | yes across **distinct** connections (one command per connection); on one connection, sequential executions only |
| Streamable (`IAsyncEnumerable`) | yes | only with `PrepareOptions { Streaming = true }` |

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
select id
 from simple_entity
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

## Explicit [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken))

The explicit path compiles the query once into an [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1) that you execute any number of times; `QueryPlanStore` is never consulted:

The explicit path is shown as a diagram on the [engine overview](index.md).

[`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) is declared on both [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) and `EntityBuilder<T>` and returns [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1):

```csharp
public IPreparedQueryCommand<TResult> Prepare(PrepareOptions options = default, CancellationToken cancellationToken = default)
```

The default (`default(PrepareOptions)`) is optimised for buffered and scalar results and reuses one underlying command per connection. [`PrepareOptions`](xref:NextORM.Core.PrepareOptions) selects the form (`Streaming`) and the command-reuse policy (`Cache`); both policies are described in [`PrepareOptions` and `PreparedCommandCache`](#prepareoptions-and-preparedcommandcache). The returned command is not bound to the context that created it: every terminal takes the [`IDataContext`](xref:NextORM.Core.IDataContext) as its first argument, so the same prepared command can be executed against another context of the same provider.

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

### `PrepareOptions` and `PreparedCommandCache`

`Prepare` takes one options value; `default(PrepareOptions)` is the parameterless form — buffered, with one command reused per connection.

```csharp
public enum PreparedCommandCache { Connection = 0, None = 1 }

public readonly record struct PrepareOptions
{
    public PreparedCommandCache Cache { get; init; }  // default: Connection
    public bool Streaming { get; init; }              // default: false (buffered)
}
```

| `Cache` | What each execution gets | Lifetime |
|---|---|---|
| `Connection` (default) | the command already cached for the executing connection, or a freshly created one on the first execution there | one command per connection, kept until that connection becomes collectable or the state is evicted |
| `None` | a fresh command, created for that execution only | disposed when the execution ends; for a streaming result, when the reader/enumerator is disposed |

```csharp
// Buffered, one command per connection (the parameterless form).
var cached = ctx.From<ISimpleEntity>().Select(x => x.Id).Prepare();

// Buffered, a fresh command per execution.
var fresh = ctx.From<ISimpleEntity>().Select(x => x.Id)
    .Prepare(new PrepareOptions { Cache = PreparedCommandCache.None });

// Streaming, one command per connection.
var streamed = ctx.From<ISimpleEntity>().Select(x => x.Id)
    .Prepare(new PrepareOptions { Streaming = true });
```

A `Cache` value that is not defined in the enum is rejected by `Prepare` with an `ArgumentOutOfRangeException` before anything is allocated. Neither policy stores the plan in the implicit cache.

**Per-connection state.** A prepared command does not own one mutable `DbCommand`. Everything that depends on the connection an execution runs on — the command, the parameter positions, the transaction binding and the execution lease — lives in a per-connection state that the prepared command holds weakly, keyed on the connection: an entry is released once its connection is collectable, and nothing on the prepared command keeps a connection, a transaction, a context or a reader alive. Cross-connection concurrency therefore works: the same prepared object can run on two distinct connections at the same time, each with its own parameters and its own reader.

**Same-connection restriction.** Two *overlapping* executions of one prepared object on the **same** connection fail fast with an `InvalidOperationException` before any command state is mutated; other commands and the implicit plan cache on that connection are unaffected, because this is not a connection-wide scheduler. Sequential executions on that connection are fine.

**Stream lifetime.** Under `None`, the command belongs to the execution and is disposed with the reader or enumerator; under `Connection`, disposing the reader releases the lease and returns the command to that connection's state. A streaming result holds the lease until it is disposed, so abandoning a live stream keeps that one connection busy for this prepared command until the stream is disposed of.

> **Sharing rules.** A prepared command may be shared across threads and contexts as long as each execution runs on its own connection; on a single connection, execute it sequentially and dispose every streaming result. Parameter values, the transaction and the reader belong to one execution on one connection, never to the prepared object. The implicit cache has a different rule: it is per thread.

### Migrating from the boolean `Prepare` overload

The boolean overloads were removed in this prerelease, with no obsolete shim. Map the old call by intent:

| Old call | New call |
|---|---|
| `Prepare()` or `Prepare(true)` | `Prepare()` (equivalently `Prepare(default, cancellationToken)`) — buffered |
| `Prepare(false)` | `Prepare(new PrepareOptions { Streaming = true })` |
| `Prepare(<bool>, cancellationToken)` | `Prepare(new PrepareOptions { Streaming = <negated bool> }, cancellationToken)` |

`EntityBuilderExtensions.Prepare` has the same shape, so `EntityBuilder<T>.Prepare(...)` migrates identically.

The removed prepared state has no replacement member; use the execution surface instead:

| Removed member | Use instead |
|---|---|
| `DbPreparedQueryCommand<TResult>.DbCommand`, `.DbCommandParams`, `.GetDbCommand(...)` | `SqlStmt` / `Behavior` for inspection, the execution terminals for running. Execution uses a per-connection command that is never exposed. |
| `DbPreparedQueryCommand<TResult>.Enumerator` (and the in-memory prepared command's `Enumerator`) | `CreateEnumerator(...)` / `CreateAsyncEnumerator(...)` on [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1) |
| `DbPreparedQueryCommand<TResult>.LastRowCount` (and the in-memory one) | the result of the terminal that executed the query (`ToList`, `First`, `ExecuteScalar`, ...); there is no stateful row count |
| `DbPreparedQueryCommand<TResult>.ResetConnection(...)` | nothing to reset: the per-connection state is released together with its connection; prepare the query again for another connection |

### Streaming requires `Streaming = true`

A prepared command created with the default options owns no [`ResultSetEnumerator<TResult>`](xref:NextORM.Core.ResultSetEnumerator`1), because the buffered form needs none. Buffered and scalar terminals work; a streaming terminal fails with an `InvalidOperationException`. Prepare the command for streaming instead, by setting `PrepareOptions.Streaming` to `true`. [`ToEnumerable`](xref:NextORM.Core.EntityBuilderExtensions.ToEnumerable``1(NextORM.Core.EntityBuilder{``0},System.Object[])) is a lazy iterator, so on that path the exception surfaces when enumeration starts, not when the call is made.

```csharp
// Buffered default: no enumerator is created.
var buffered = ctx.From<ISimpleEntity>().Select(x => x.Id).Prepare();
buffered.ToList(ctx); // fine

// Streaming: the only way to get a streamable prepared command.
var streaming = ctx.From<ISimpleEntity>()
    .Select(x => x.Id)
    .Prepare(new PrepareOptions { Streaming = true });

await foreach (var id in streaming.ToAsyncEnumerable(ctx))
{
    Console.WriteLine(id);
}
```

### [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) does not populate the plan cache

[`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) calls the planner with `storeInCache: false`, so it cannot be found by a later implicit lookup and cannot "pollute" the cache:

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
* **Per context type.** SQLite, PostgreSQL and SQL Server produce different SQL and different `DbCommand` implementations for the same shape, so they get separate entries.
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

## Cache controls

`PurgeQueryCache()` clears the plans of one context. Three process-wide controls complement it, and none of them touches the sticky `QueryCommand<TResult>.Cache` flag, so disabling the cache never leaks onto the shared `Any` command.

* [`DataContextCache.Clear()`](xref:NextORM.Core.DataContextCache.Clear) empties every process-wide cache (metadata, select lists, compiled expression and in-list delegates, the compiled `JoinInto` identity selectors, row mappers, projection aliases and FROM sources) and invalidates the thread-local plan cache. The identity selectors are re-derived from the current metadata on their next use, so a key configuration registered after the clear is picked up instead of a stale one. The plan cache is `[ThreadStatic]`, so a plan built on another thread is dropped when that thread next touches the cache. Call this after a schema change. It also drops the global query filters imported by the EF Core bridge: a bridge-bound context then fails closed with the documented `InvalidOperationException` on its next query or DML build instead of running unfiltered — create or rebind a context through the bridge to recover (see [EF Core query-filter bridge](../advanced/integration-efcore.md#lifecycle-recovery-and-bounded-concurrency)).
* [`DataContextBuilder.UseQueryCache(false)`](xref:NextORM.Core.DataContextBuilder.UseQueryCache(System.Boolean)) — or the `DataContext.QueryCacheEnabled` / `InMemoryDataContext.QueryCacheEnabled` property — rebuilds every plan instead of caching it. It is passed as `storeInCache: false` for the call and leaves `QueryCommand<TResult>.Cache` untouched.
* [`DataContextBuilder.UseCacheSlidingExpiration(ttl)`](xref:NextORM.Core.DataContextBuilder.UseCacheSlidingExpiration(System.TimeSpan)) enables lazy eviction of the process-wide caches: an entry not read within `ttl` is removed on its next access, and every read refreshes it. `TimeSpan.Zero` (the default) disables expiration. Evicting an imported EF Core query filter this way has the same fail-closed effect as `Clear`: the next query or DML build on a bridge-bound context throws the documented `InvalidOperationException` instead of running unfiltered.

```csharp
var builder = new DataContextBuilder().UseQueryCache(false);
using var ctx = new SqliteDataContext("Data Source=app.db", builder); // every terminal rebuilds its plan

new DataContextBuilder().UseCacheSlidingExpiration(TimeSpan.FromMinutes(10)); // lazy eviction, process-wide

DataContextCache.Clear(); // drop the process-wide caches and the plan cache
ctx.PurgeQueryCache();    // drop this context's plans
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

A captured collection indexed by a query expression (`dict[column]`) folds the same way: the entry count is part of the plan key, so the generated `CASE` is rebuilt when the collection changes. See [Filtering (WHERE)](../guide/03-filtering-where.md).

## Provider differences

| Provider | Behaviour |
|---|---|
| SQLite | Plan cache and [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) are core behaviour; a prepared command wraps a `SqliteCommand`. |
| SQL Server | Same mechanism; `TOP` vs `OFFSET ... FETCH` paging is part of the recorded plan shape, so the paging mode is baked into the cached SQL. |
| PostgreSQL | Same mechanism; a prepared command wraps an `NpgsqlCommand`. |
| MySQL | Plan cache and [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) are core behaviour; a prepared command wraps a `MySqlCommand`. |
| MariaDB | Same mechanism; a prepared command wraps a `MySqlCommand` (the MySQL driver). |
| ClickHouse | Plan cache and [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) are core behaviour; a prepared command wraps a `ClickHouseCommand`. |
| In-memory | [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) keeps its own compiled-query cache and returns an [`InMemoryPreparedQueryCommand<TResult>`](xref:NextORM.Core.InMemoryPreparedQueryCommand`1); [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) works, but raw-SQL [`PrepareFromSql`](xref:NextORM.Core.EntityExtensions.PrepareFromSql``1(NextORM.Core.EntityBuilder{``0},System.String)) is not supported (`NotSupportedException`). |

## See also

* [Connections](../infrastructure/01-connections.md)
* [Dependency injection](../getting-started/04-dependency-injection.md)
* [Documentation index](../index.md)

---

Source: `tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs:48` (buffered then streaming),
`tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs:108` (`Prepare(new PrepareOptions { Streaming = true })`),
`tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs:133` (buffered reuse),
`tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs:160` (streaming the default throws),
`tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs:404` ([`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) does not populate the cache);
`tests/nextorm.sqlite.tests/PlanCache/InListCacheTests.cs:41` (same-shape captured collection),
`tests/nextorm.sqlite.tests/PlanCache/InListCacheTests.cs:92` (reassigned array),
`tests/nextorm.sqlite.tests/PlanCache/InListCacheTests.cs:117` (grown list);
`tests/nextorm.core.tests/Caching/DataContextCacheScopeTests.cs:20` (cache sharing scope);
`tests/nextorm.core.tests/Caching/QueryCacheControlsTests.cs:45` (clear, disable without mutating `Cache`, sliding expiration);
`tests/nextorm.sqlite.tests/PlanCache/DataContextCacheClearTests.cs:31` (`DataContextCache.Clear` invalidates the plan cache);
`tests/nextorm.integration.tests/CommonTestSuite/Caching/CommonTestSuite.Cache.cs:6`;
`src/nextorm.core/Query/QueryCommand.TResult.cs:81` ([`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken))),
`src/nextorm.core/DataContext/Cache/PrepareOptions.cs:6` (`PreparedCommandCache` / `PrepareOptions`),
`src/nextorm.core/DataContext/Cache/DbPreparedQueryCommand.cs:199` (`Acquire`: per-connection state and the same-connection rejection),
`src/nextorm.core/DataContext/Cache/IPreparedQueryCommand.cs:5` (prepared terminals),
`src/nextorm.core/DataContext/DataContext.cs:315` (`GetPreparedQueryCommand`).
