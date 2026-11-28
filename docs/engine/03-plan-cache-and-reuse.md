# Plan cache and reuse

> NextORM reuses SQL and row mappers through its implicit plan cache. To reuse a command explicitly, call [`PrepareBuffered`](xref:NextORM.Core.EntityBuilderExtensions.PrepareBuffered``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) or [`PrepareStreaming`](xref:NextORM.Core.EntityBuilderExtensions.PrepareStreaming``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) and keep the returned command.

**Prerequisites:** [Quickstart](../getting-started/02-quickstart.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Dependency injection](../getting-started/04-dependency-injection.md).

## Overview

Every terminal such as [`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) does more than read rows. It turns the fluent [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) into SQL and a row mapper, prepares a `DbCommand`, and reads the result. The two mechanisms below let NextORM reuse the preparation work:

* the **implicit plan cache** - automatic, keyed by the structural shape of the query; for an APPLY join that shape includes the APPLY source expression, so two queries with the same projection type but different APPLY sources are different plans;
* **Explicit preparation** — [`PrepareBuffered`](xref:NextORM.Core.EntityBuilderExtensions.PrepareBuffered``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) or [`PrepareStreaming`](xref:NextORM.Core.EntityBuilderExtensions.PrepareStreaming``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) returns a command for repeated execution.

The two mechanisms can look alike from the outside, but differ in how you identify and retain prepared work, where it is available, and whether it enters the cache:

| | Implicit plan cache | Explicit preparation |
|---|---|---|
| Entry point | [`PrepareBuffered`](xref:NextORM.Core.EntityBuilderExtensions.PrepareBuffered``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) / [`PrepareStreaming`](xref:NextORM.Core.EntityBuilderExtensions.PrepareStreaming``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) |
| Lookup key | compatible provider and mapping settings, plus the verified structure of the query | none — you hold the returned command |
| Lifetime | until the plan cache is cleared or the process exits; some custom query shapes also need a live command for comparison | as long as you keep the reference |
| Scope | **process-wide** for compatible SQL contexts | the instance you keep |
| Populates the cache | yes | **no** |
| Concurrent use | independent contexts on **distinct** connections reuse a compatible plan | independent contexts on **distinct** connections reuse the prepared object |
| Streamable (`IAsyncEnumerable`) | yes | with `PrepareStreaming()` |

Use this page to look up the API and caching rules. It does not repeat the measured cost of each option.

## Terminals on [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1)

`Select(...)` produces a [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1), which declares the terminal operators. Choose a buffered or scalar terminal when you need a completed value, or a streaming terminal when you want to consume rows as you iterate. The terminals in this section use the implicit cache when the query is eligible.

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

The cache compares query structure after it finds a matching hash. Different lambda instances with the same shape can reuse an entry. An APPLY join includes its source expression in the shape, so changing that expression can produce a different plan even when the projection type stays the same. The following loop reuses a plan while binding a different `entityId` for each call:

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

## Explicit preparation

Explicit preparation compiles the query once and bypasses `QueryPlanStore`.

The explicit path is shown as a diagram on the [engine overview](index.md).

Both methods are available on `QueryCommand<TResult>`, `EntityBuilder<T>`, and `IRepository<T>`. Choose the method based on how you read results:

```csharp
public IPreparedBufferedQueryCommand<TResult> PrepareBuffered(PrepareOptions options = default, CancellationToken cancellationToken = default)
public IPreparedStreamingQueryCommand<TResult> PrepareStreaming(PrepareOptions options = default, CancellationToken cancellationToken = default)
```

By default, both methods reuse one underlying command per connection. Set `PrepareOptions.Cache` to change that policy. Every terminal takes `IDataContext` as its first argument, so you can execute the command against another context of the same provider.

```csharp
var prepared = ctx.From<ISimpleEntity>()
    .Select(x => x.Id)
    .PrepareBuffered();

// Terminal methods declared on IPreparedBufferedQueryCommand<TResult>.
List<int> all = prepared.ToList(ctx);
int first = prepared.First(ctx);
List<int> asyncAll = await prepared.ToListAsync(ctx);
int? scalar = prepared.ExecuteScalar(ctx, throwIfNull: false);
```

The same command can be re-executed with different runtime parameter values:

```csharp
var byId = ctx.From<ISimpleEntity>()
    .Where(x => x.Id == SqlFunctions.Parameter<int>(0))
    .Select(x => x.Id)
    .PrepareBuffered();

var one = await byId.FirstAsync(ctx, 42);    // 42 fills norm_p0
var many = await byId.ToListAsync(ctx, 43);  // same prepared command, new value
```

### `PrepareOptions` and `PreparedCommandCache`

Both methods accept `PrepareOptions`. The default reuses one command per connection.

```csharp
public enum PreparedCommandCache { Connection = 0, None = 1 }

public readonly record struct PrepareOptions
{
    public PreparedCommandCache Cache { get; init; }  // default: Connection
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
var cached = ctx.From<ISimpleEntity>().Select(x => x.Id).PrepareBuffered();

// Buffered, a fresh command per execution.
var fresh = ctx.From<ISimpleEntity>().Select(x => x.Id)
    .PrepareBuffered(new PrepareOptions { Cache = PreparedCommandCache.None });

// Streaming, one command per connection.
var streamed = ctx.From<ISimpleEntity>().Select(x => x.Id)
    .PrepareStreaming();
```

A `Cache` value that is not defined in the enum is rejected by either preparation method with an `ArgumentOutOfRangeException` before anything is allocated. Neither command-reuse policy stores the plan in the implicit cache.

**Per-connection state.** A prepared command does not own one mutable `DbCommand`. Each connection has its own state for the command, parameter positions, transaction binding, and execution lease. The prepared command holds that state weakly, keyed by connection. When a connection becomes collectable, its entry is released; the prepared command does not keep a connection, transaction, context, or reader alive.

This separation allows concurrent use across connections: the same prepared object can run on two distinct connections at once, each with its own parameters and reader.

**Same-connection restriction.** If an execution already holds the command state for a plan on a connection, another execution of that plan on the same connection fails with `InvalidOperationException` before it changes the command state. This guard covers that plan's command state. Other overlaps on a context or connection are unsupported and might not raise the same exception.

**Stream lifetime.** Under `None`, the command belongs to one execution and is disposed with its reader or enumerator. Under `Connection`, disposing the reader releases the execution lease—the reservation for that command—and returns the command to that connection's state. A streaming result holds the lease until you dispose it, so an abandoned live stream keeps that connection busy for this prepared command.

> **Sharing rules.** Run concurrent queries through independent contexts on distinct connections. Allow only one active operation per context and per connection, including when two contexts wrap the same connection. Sequential use can resume on a different thread after `await`. A streaming operation stays active until the reader or enumerator is exhausted or disposed.

### Buffered and streaming execution

`PrepareBuffered()` returns a command with `ToList`, `First`, `Single`, `ExecuteScalar`, and their async variants. `PrepareStreaming()` returns a command with `ToEnumerable`, `ToAsyncEnumerable`, and enumerator creation methods. Each interface exposes only its own mode’s operations.

```csharp
// Buffered default: no enumerator is created.
var buffered = ctx.From<ISimpleEntity>().Select(x => x.Id).PrepareBuffered();
buffered.ToList(ctx); // fine

// Streaming: the only way to get a streamable prepared command.
var streaming = ctx.From<ISimpleEntity>()
    .Select(x => x.Id)
    .PrepareStreaming();

await foreach (var id in streaming.ToAsyncEnumerable(ctx))
{
    Console.WriteLine(id);
}
```

### Explicit preparation bypasses the plan cache

Both methods call the planner with `storeInCache: false`:

```csharp
ctx.PurgeQueryCache();

var prepared = ctx.From<ISimpleEntity>().Select(x => x.Id).PrepareBuffered();
prepared.ToList(ctx); // executes, but adds no plan-cache entry

// A later implicit terminal builds its own plan.
var implicitRows = ctx.From<ISimpleEntity>().Select(x => x.Id).ToList();
```

## Plan-cache scope and purging

The implicit SQL plan cache is shared within the process. To reuse a plan, contexts must agree on the provider and effective dialect, mappings, and SQL-generation options. The following rules apply to compatible queries:

* **Plan reuse.** The lookup checks the query structure, including inputs that change the SQL shape, such as literals and `IN` list lengths. A physical thread, a connection, or an ordinary captured scalar doesn't split an otherwise compatible plan.
* **Concurrent first use.** Two callers can build candidates for the same shape. The first published compatible plan is the one subsequent calls reuse.
* **Execution state.** NextORM binds the current values for each invocation. Each exact `DbConnection` has its own command state for the plan, and each execution gets its own reader.
* **Opt out for a command.** Set `QueryCommand<TResult>.Cache = false` to skip implicit lookup and publication for that command.

To clear the shared SQL plans, call the following method on a SQL context after active database operations finish:

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

On a SQL context, `PurgeQueryCache()` clears the shared implicit SQL plans and that context's local query artifacts. On an in-memory context, it clears that instance's compiled queries. Run cache clears and mapping changes only when no affected queries are active. The following controls leave the command's `Cache` flag alone:

* [`DataContextCache.Clear()`](xref:NextORM.Core.DataContextCache.Clear) clears the shared implicit SQL plans and process-wide metadata, mappers, and other translation caches. Call `Clear()` after a schema or mapping change. Later queries rebuild from the current metadata.

  `Clear()` also drops global query filters imported by the EF Core bridge. A bridge-bound context then fails closed with the documented `InvalidOperationException` on its next query or DML build instead of running unfiltered. Create or rebind a context through the bridge to recover (see [EF Core query-filter bridge](../advanced/integration-efcore.md#lifecycle-recovery-and-bounded-concurrency)).
* [`DataContextBuilder.UseQueryCache(false)`](xref:NextORM.Core.DataContextBuilder.UseQueryCache(System.Boolean)) — or the `DataContext.QueryCacheEnabled` / `InMemoryDataContext.QueryCacheEnabled` property — rebuilds every plan instead of caching it. It is passed as `storeInCache: false` for the call and leaves `QueryCommand<TResult>.Cache` untouched.
* [`DataContextBuilder.UseCacheSlidingExpiration(ttl)`](xref:NextORM.Core.DataContextBuilder.UseCacheSlidingExpiration(System.TimeSpan)) enables lazy eviction of the process-wide caches. An entry not read within `ttl` is removed on its next access, and each read refreshes its lifetime. `TimeSpan.Zero` (the default) disables expiration. Entity mappings that declare a shard key with `HasShardKey` don't expire. If expiration evicts an imported EF Core query filter, the bridge-bound context fails closed just as it does after `Clear`: its next query or DML build throws the documented `InvalidOperationException` instead of running unfiltered.

```csharp
var builder = new DataContextBuilder().UseQueryCache(false);
using var ctx = new SqliteDataContext("Data Source=app.db", builder); // every terminal rebuilds its plan

new DataContextBuilder().UseCacheSlidingExpiration(TimeSpan.FromMinutes(10)); // lazy eviction, process-wide

DataContextCache.Clear(); // drop the process-wide caches and the plan cache
ctx.PurgeQueryCache();    // clear shared SQL plans and local query artifacts
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
| SQLite | Plan cache and [`PrepareBuffered`](xref:NextORM.Core.EntityBuilderExtensions.PrepareBuffered``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) are core behaviour; a prepared command wraps a `SqliteCommand`. |
| SQL Server | Same mechanism; `TOP` vs `OFFSET ... FETCH` paging is part of the recorded plan shape, so the paging mode is baked into the cached SQL. |
| PostgreSQL | Same mechanism; a prepared command wraps an `NpgsqlCommand`. |
| MySQL | Plan cache and [`PrepareBuffered`](xref:NextORM.Core.EntityBuilderExtensions.PrepareBuffered``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) are core behaviour; a prepared command wraps a `MySqlCommand`. |
| MariaDB | Same mechanism; a prepared command wraps a `MySqlCommand` (the MySQL driver). |
| ClickHouse | Plan cache and [`PrepareBuffered`](xref:NextORM.Core.EntityBuilderExtensions.PrepareBuffered``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) are core behaviour; a prepared command wraps a `ClickHouseCommand`. |
| In-memory | [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) keeps its own compiled-query cache and returns an [`InMemoryPreparedQueryCommand<TResult>`](xref:NextORM.Core.InMemoryPreparedQueryCommand`1); [`PrepareBuffered`](xref:NextORM.Core.EntityBuilderExtensions.PrepareBuffered``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) works, but raw-SQL [`PrepareFromSql`](xref:NextORM.Core.EntityExtensions.PrepareFromSql``1(NextORM.Core.EntityBuilder{``0},System.String)) is not supported (`NotSupportedException`). |

## See also

* [Connections](../infrastructure/01-connections.md)
* [Dependency injection](../getting-started/04-dependency-injection.md)
* [Documentation index](../index.md)

---

Source: `tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs:48` (buffered then streaming),
`tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs:108` (`PrepareStreaming()`),
`tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs:133` (buffered reuse),
`tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs` (separate execution interfaces),
`tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs:404` ([`PrepareBuffered`](xref:NextORM.Core.EntityBuilderExtensions.PrepareBuffered``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) does not populate the cache);
`tests/nextorm.sqlite.tests/PlanCache/InListCacheTests.cs:41` (same-shape captured collection),
`tests/nextorm.sqlite.tests/PlanCache/InListCacheTests.cs:92` (reassigned array),
`tests/nextorm.sqlite.tests/PlanCache/InListCacheTests.cs:117` (grown list);
`tests/nextorm.core.tests/Caching/DataContextCacheScopeTests.cs:20` (cache sharing scope);
`tests/nextorm.core.tests/Caching/QueryCacheControlsTests.cs:45` (clear, disable without mutating `Cache`, sliding expiration);
`tests/nextorm.sqlite.tests/PlanCache/DataContextCacheClearTests.cs:31` (`DataContextCache.Clear` invalidates the plan cache);
`tests/nextorm.integration.tests/CommonTestSuite/Caching/CommonTestSuite.Cache.cs:6`;
`src/nextorm.core/Query/Commands/QueryCommand.TResult.cs:81` ([`PrepareBuffered`](xref:NextORM.Core.EntityBuilderExtensions.PrepareBuffered``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken))),
`src/nextorm.core/DataContext/Cache/Preparation/PrepareOptions.cs:6` (`PreparedCommandCache` / `PrepareOptions`),
`src/nextorm.core/DataContext/Cache/Commands/DbPreparedQueryCommand.cs:199` (`Acquire`: per-connection state and the same-connection rejection),
`src/nextorm.core/DataContext/Cache/Commands/IPreparedBufferedQueryCommand.cs`, `src/nextorm.core/DataContext/Cache/Commands/IPreparedStreamingQueryCommand.cs`,
`src/nextorm.core/DataContext/DataContext.cs:315` (`GetPreparedQueryCommand`).
