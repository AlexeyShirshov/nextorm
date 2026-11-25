# Plan cache and reuse: [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken))

> Reuse the work that turns a frequently run query into SQL and a row mapper. Ordinary terminals use the implicit plan cache; [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) returns an [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1) that you keep and execute explicitly.

**Prerequisites:** [Quickstart](../getting-started/02-quickstart.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Dependency injection](../getting-started/04-dependency-injection.md).

## Overview

Every terminal such as [`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) does more than read rows. It turns the fluent [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) into SQL and a row mapper, prepares a `DbCommand`, and reads the result. The two mechanisms below let NextORM reuse the preparation work:

* the **implicit plan cache** - automatic, keyed by the structural shape of the query; for an APPLY join that shape includes the APPLY source expression, so two queries with the same projection type but different APPLY sources are different plans;
* **[`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken))** - explicit, returns an [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1) that you keep and execute yourself.

The two mechanisms can look alike from the outside, but differ in how you identify and retain prepared work, where it is available, and whether it enters the cache:

| | Implicit plan cache | Explicit [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) |
|---|---|---|
| Entry point | any terminal on [`EntityBuilder`](xref:NextORM.Core.EntityBuilder) / [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) ([`ToList`](xref:NextORM.Core.EntityBuilderExtensions.ToList``1(NextORM.Core.EntityBuilder{``0},System.ReadOnlySpan{System.Object})), [`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), [`ToAsyncEnumerable`](xref:NextORM.Core.EntityBuilderExtensions.ToAsyncEnumerable``1(NextORM.Core.EntityBuilder{``0},System.Object[])), ...) | [`Prepare`](xref:NextORM.Core.QueryCommand`1.Prepare(NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) / `EntityBuilder<T>.Prepare()` |
| Lookup key | structural hash of the query shape (for an APPLY join, including the APPLY source expression); never the rendered SQL | none - you hold the returned command |
| Lifetime | until `PurgeQueryCache()` or process exit | as long as you keep the reference |
| Scope | **per thread**, shared by every [`IDataContext`](xref:NextORM.Core.IDataContext) on that thread | the instance you keep |
| Populates the cache | yes | **no** |
| Safe for concurrent use | yes, by construction (per-thread entry) | yes across **distinct** connections (one command per connection); on one connection, sequential executions only |
| Streamable (`IAsyncEnumerable`) | yes | only with `PrepareOptions { Streaming = true }` |

Use this page to look up the API and caching rules. It does not repeat the measured cost of each option.

## Terminals on [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1)

`Select(...)` produces a [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1), which declares the terminal operators. Choose a buffered or scalar terminal when you need a completed value, or a streaming terminal when you want to consume rows as you iterate. Every terminal in this section routes through the plan cache.

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

The `params object[]` (or `ReadOnlySpan<object?>`) accepted by these terminals supplies runtime values for the query's [`Parameter`](xref:NextORM.Core.SqlFunctions.Parameter``1(System.Int32)) placeholders.

For example, this query selects each entity's `Id` and returns the rows in a list:
```csharp
using var ctx = new SqliteDataContext("Data Source=app.db", new DataContextBuilder());

var rows = await ctx.From<ISimpleEntity>()
    .Select(x => x.Id)
    .ToListAsync();
```

The query produces this SQL:
```sql
select id
 from simple_entity
```

The cache key represents query structure, not the lambda object's identity or parameter names. So calls with different lambda instances but the same shape reuse an entry, which makes the cache useful for queries written inline in a loop. An APPLY join adds its source expression to the shape: if that source changes, the query gets a different plan and SQL, even when the projection type stays the same. The key is never the rendered SQL:

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

Both iterations reuse the same cache entry even though `entityId` changes.

## Explicit [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken))

The explicit path compiles the query once into an [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1), which you can execute repeatedly. It bypasses `QueryPlanStore`:

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

`Prepare` takes one options value. `default(PrepareOptions)` is the parameterless form: buffered results, with one command reused per connection.

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

`Cache` controls command reuse for a prepared query; it is separate from the implicit plan cache described later on this page.

These examples configure buffered and streaming prepared queries:
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

A `Cache` value that is not defined in the enum is rejected by `Prepare` with an `ArgumentOutOfRangeException` before anything is allocated. Neither command-reuse policy stores the plan in the implicit cache.

**Per-connection state.** A prepared command does not own one mutable `DbCommand`. Each connection has its own state for the command, parameter positions, transaction binding, and execution lease. The prepared command holds that state weakly, keyed by connection. When a connection becomes collectable, its entry is released; the prepared command does not keep a connection, transaction, context, or reader alive.

This separation allows concurrent use across connections: the same prepared object can run on two distinct connections at once, each with its own parameters and reader.

**Same-connection restriction.** Two *overlapping* executions of one prepared object on the **same** connection fail fast with an `InvalidOperationException`, before either execution changes command state. This restriction does not affect other commands or the implicit plan cache on that connection; it is not a connection-wide scheduler. Sequential executions on the same connection are fine.

**Stream lifetime.** Under `None`, the command belongs to one execution and is disposed with its reader or enumerator. Under `Connection`, disposing the reader releases the execution lease—the reservation for that command—and returns the command to that connection's state. A streaming result holds the lease until you dispose it, so an abandoned live stream keeps that connection busy for this prepared command.

> **Sharing rules.** You can share a prepared command across threads and contexts when each execution uses its own connection. On one connection, execute it sequentially and dispose every streaming result.
>
> Each execution owns its parameter values, transaction, and reader; the prepared object does not. The implicit cache follows a different rule: it is per thread.

### Inspecting and executing a prepared command

The concrete database prepared command ([`DbPreparedQueryCommand<TResult>`](xref:NextORM.Core.DbPreparedQueryCommand`1)) exposes [`SqlStmt`](xref:NextORM.Core.DbPreparedQueryCommand`1.SqlStmt) and [`Behavior`](xref:NextORM.Core.DbPreparedQueryCommand`1.Behavior) for inspection. The command acquired for an execution is an implementation detail: each execution gets the command for its connection, applies its parameter values and transaction binding, and holds the execution lease there. The prepared object never exposes that command or keeps a connection, transaction, context, or reader alive.

Use these members according to what you need to inspect or run:

| Need | Use |
|---|---|
| the prepared SQL text and the requested reader behavior | `SqlStmt` / `Behavior` on the prepared command |
| rows, a single row or a scalar | the typed terminals on [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1): `ToList(...)`, `First(...)`, `Single(...)`, `ExecuteScalar(...)` and their async forms |
| a streaming result | `ToAsyncEnumerable(...)`, `ToEnumerable(...)`, `CreateEnumerator(...)` / `CreateAsyncEnumerator(...)` |
| running the query on another connection | execute the same prepared command against a context on that connection; its per-connection state is created on demand and released with the connection |

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

The implicit cache (`DataContext._queryPlanCache`) is a `[ThreadStatic]` dictionary keyed by `(ContextType, QueryPlan)`. In other words, each thread looks up a plan by both context type and query shape. For an APPLY join, the shape includes the APPLY source expression; it never uses the rendered SQL:

* **Per thread.** Thread B cannot see an entry created on thread A. After work moves to another thread-pool thread, the first call there misses until that thread has seen the shape.
* **Per context type.** SQLite, PostgreSQL and SQL Server produce different SQL and different `DbCommand` implementations for the same shape, so they get separate entries.
* **No automatic invalidation.** Entries live until you call `PurgeQueryCache()` or the process ends. After a schema change, purge the cache or recreate the context before reusing affected query shapes.
* `QueryCommand<TResult>.Cache = false` disables hashing and lookup for that command. This is not a cheap "skip the cache" switch: it forces a full plan rebuild on every call.

To clear the plans for one context, call:

```csharp
ctx.PurgeQueryCache();
```

Other caches use different sharing scopes. Metadata, select lists, compiled expression delegates, and in-list accessors are process-wide. The in-memory expression cache belongs to each context instance because its entries capture that context:

```csharp
using var ctx = new InMemoryDataContext();

ReferenceEquals(ctx.Metadata, DataContextCache.Metadata);                 // true  (process-wide)
ReferenceEquals(ctx.SelectListCache, DataContextCache.SelectListCache);   // true  (process-wide)
ReferenceEquals(ctx.ExpressionsCache, DataContextCache.ExpressionsCache); // false (per instance)
```

## Cache controls

`PurgeQueryCache()` clears plans for one context. The controls below clear process-wide caches or configure plan caching for a context. `QueryCommand<TResult>.Cache` is sticky: changing it changes the command's setting. These controls leave that flag alone, so disabling the cache this way does not leak onto the shared `Any` command:

* [`DataContextCache.Clear()`](xref:NextORM.Core.DataContextCache.Clear) empties every process-wide cache: metadata, select lists, compiled expression and in-list delegates, compiled `JoinInto` identity selectors, row mappers, projection aliases, and `FROM` sources. It also invalidates the thread-local plan cache. Identity selectors are rebuilt from current metadata on their next use, so a key configuration registered after the clear replaces stale data. Because the plan cache is `[ThreadStatic]`, a plan built on another thread is dropped when that thread next accesses the cache. Call `Clear()` after a schema change.

  `Clear()` also drops global query filters imported by the EF Core bridge. A bridge-bound context then fails closed with the documented `InvalidOperationException` on its next query or DML build instead of running unfiltered. Create or rebind a context through the bridge to recover (see [EF Core query-filter bridge](../advanced/integration-efcore.md#lifecycle-recovery-and-bounded-concurrency)).
* [`DataContextBuilder.UseQueryCache(false)`](xref:NextORM.Core.DataContextBuilder.UseQueryCache(System.Boolean)) — or the `DataContext.QueryCacheEnabled` / `InMemoryDataContext.QueryCacheEnabled` property — rebuilds every plan instead of caching it. It is passed as `storeInCache: false` for the call and leaves `QueryCommand<TResult>.Cache` untouched.
* [`DataContextBuilder.UseCacheSlidingExpiration(ttl)`](xref:NextORM.Core.DataContextBuilder.UseCacheSlidingExpiration(System.TimeSpan)) enables lazy eviction of the process-wide caches. An entry not read within `ttl` is removed on its next access, and each read refreshes its lifetime. `TimeSpan.Zero` (the default) disables expiration. Entity mappings that declare a shard key with `HasShardKey` don't expire. If expiration evicts an imported EF Core query filter, the bridge-bound context fails closed just as it does after `Clear`: its next query or DML build throws the documented `InvalidOperationException` instead of running unfiltered.

```csharp
var builder = new DataContextBuilder().UseQueryCache(false);
using var ctx = new SqliteDataContext("Data Source=app.db", builder); // every terminal rebuilds its plan

new DataContextBuilder().UseCacheSlidingExpiration(TimeSpan.FromMinutes(10)); // lazy eviction, process-wide

DataContextCache.Clear(); // drop the process-wide caches and the plan cache
ctx.PurgeQueryCache();    // drop this context's plans
```

## Captured `in`/`Contains` collections

An `in` list or a `Contains` call over a captured collection becomes a parameterized `IN` predicate. The plan key tracks the collection's *shape*—the evaluated item count and whether it contains `null`—not its *values*. This distinction determines when NextORM can reuse a plan:

* Builds with the same collection length and the same `null` presence share one plan, even from different call sites.
* If you grow or reassign the collection between executions, the shape hash changes. NextORM creates a plan with the new parameter count and reads the current values through the cached accessor.

This example builds a plan for one value, then rebuilds for two values after the collection changes:
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

The two-value query uses these parameters:
```sql
id in ($p0, $p1)
```

`values.Contains(c.Id)` is handled the same way. A `null` element adds an `is null` branch (for example `(nullableint in ($p0) or nullableint is null)`), and an empty or all-null collection folds to a constant (`1 = 0` / `is null`) with no parameters.

A captured collection indexed by a query expression (`dict[column]`) folds the same way: the entry count is part of the plan key, so the generated `CASE` is rebuilt when the collection changes. See [Filtering (WHERE)](../guide/03-filtering-where.md).

## Provider differences

Database providers use the same reuse mechanism but create provider-specific command types. SQL Server also includes its paging mode in the plan shape. The in-memory provider uses its own compiled-query cache, as shown below:

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
`src/nextorm.core/Query/Commands/QueryCommand.TResult.cs:81` ([`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken))),
`src/nextorm.core/DataContext/Cache/Preparation/PrepareOptions.cs:6` (`PreparedCommandCache` / `PrepareOptions`),
`src/nextorm.core/DataContext/Cache/Commands/DbPreparedQueryCommand.cs:199` (`Acquire`: per-connection state and the same-connection rejection),
`src/nextorm.core/DataContext/Cache/Commands/IPreparedQueryCommand.cs:5` (prepared terminals),
`src/nextorm.core/DataContext/DataContext.cs:315` (`GetPreparedQueryCommand`).
