# Prepared vs Cached: reusing a query

NextORM has two independent ways to avoid re-doing plan work on every execution. They look similar from
the outside (`await dataContext.SimpleEntity.Where(...).ToListAsync()` vs a `prepared.ToListAsync(ctx, ...)`),
but they have different cost, different lifetime and different safety rules.

| | Implicit plan cache | Explicit `Prepare()` |
|---|---|---|
| Entry point | any terminal method on `EntityBuilder` / `QueryCommand` (`ToList`, `First`, `ToListAsync`, `ToAsyncEnumerable`, ...) | `EntityBuilder.Prepare(...)` / `QueryCommand.Prepare(...)` |
| Lookup key | structural hash of the query shape (`QueryPlanEqualityComparer` over the `*PlanHash` fields) | none — you hold the returned `IPreparedQueryCommand<TResult>` |
| Lifetime | process/thread, until `PurgeQueryCache()` | as long as you keep the reference |
| Scope | **per thread**, shared by every `IDataContext` on that thread | the instance you keep |
| Populates the cache | yes (`storeInCache: true`) | **no** (`storeInCache: false`) |
| Cost per call (SQLite, 1 row) | 17.56 µs / 3.76 KB | **11.94 µs / 0.76 KB** |
| Relative | 1.47× slower, 4.94× more allocated | **1.00×** |
| GC | 0 Gen1 | 0 Gen1 |
| Safe for concurrent use | yes, by construction (per-thread entry) | **no** (shared mutable `DbCommand` and enumerator) |
| Streamable (`IAsyncEnumerable`) | yes | only with `nonStreamUsing: false` |

Measured on SQLite with `SqliteBenchmarkCachedPlan` (100 executions per invocation, `Job.Default`,
out-of-process). Absolute numbers drift between machines/runs — compare ratios.

---

## 1. Implicit plan cache

### What is cached

`DbContext._queryPlanCache` is a **`[ThreadStatic]` static** dictionary
(`src/nextorm.core/DataContext/DbContext.cs:40-41`). The key is a `(Type ContextType, QueryPlan Plan)`
pair, so the same query shape produces independent entries for SQLite, PostgreSQL and SQL Server.

The value is a fully materialised `DbPreparedQueryCommand<TResult>`: the provider `DbCommand` with its
`CommandText` and parameter collection, the row-mapping delegate, `SqlStmt`, `NoParams`,
`NeedsParamRefresh`, `ParamMap` and a `ResultSetEnumerator<TResult>`.

### When it hits

Two commands are the same plan if they are structurally equal — `QueryPlanEqualityComparer` first compares
the pre-computed `FromPlanHash` / `WherePlanHash` / `ColumnsPlanHash` / `JoinPlanHash` / `SortingPlanHash` /
`GroupingPlanHash` / `UnionPlanHash` / `ReferencedQueriesPlanHash` / `ResultPlanHash` fields and only falls
back to a clause-by-clause comparison on a hash collision. A **different lambda instance** with the same
shape (`x => x.Id == 7`) is therefore a cache hit, which is what makes the cache useful for queries written
inline in a loop.

The stored key is a clone of the command (`QueryPlan.GetCacheVersion()` →
`QueryCommand.CloneForCache()`), so mutating the caller's command afterwards cannot corrupt the key.

### What it costs on every call

Even on a hit the public API has to *identify* the plan, so every call pays:

| Step | Cost/call | Share |
|---|---:|---:|
| Build the command: `EntityBuilder.Clone` + `Where` + `Select` (fresh expression tree) | 1.12 µs | 31% |
| `PrepareCommand` (walks every clause **and** computes all `*PlanHash`) + cache lookup | 1.63 µs | 46% |
| `ExtractParams` (runtime parameter values) | 0.79 µs | 22% |
| **Total (cache-hit, before touching the database)** | **3.56 µs** | |

`PrepareCommand(bool dontCalculateHash, CancellationToken)` is called as
`PrepareCommand(!storeInCache, ct)` (`DbContext.cs:303`). The public terminals always pass
`storeInCache: true`, so `dontCalculateHash` is `false` and the **whole expression tree is hashed on every
call** — that is the single largest component of the cache tax.

### Parameters

* Runtime parameters are named `norm_p{index}` (`NORM.Param<T>(idx)` in the query). A command
  "needs a refresh" when any parameter matches `IsRuntimeParam` (`DbContext.cs:174`).
* On a hit, values are only refreshed when `NeedsParamRefresh` is set; otherwise the values captured at
  build time are reused (`DbContext.cs:426-446`).
* Non-runtime values (captured constants) are baked into the SQL text at build time — a different constant
  is a different plan, i.e. a different cache entry.
* `ParamMap` memoises the provider's linear `parameters.IndexOf(name)` scan, so it runs at most once per
  parameter per compiled command.

### Lifetime and invalidation

* Entries live until `PurgeQueryCache()` is called (`DbContext.cs:1202`; the in-memory provider clears
  `_cmdIdx`) or the process ends. **There is no automatic invalidation** — after a schema change you must
  call `PurgeQueryCache()` yourself, otherwise a stale `DbCommand` (old `CommandText`, old parameter
  layout) is reused.
* `Dispose()` iterates *all* cached entries on the thread and calls `ResetConnection` when the context owns
  the connection (`DbContext.cs:697-716`), which nulls `DbCommand.Connection` so the command rebinds on the
  next use. Cached commands therefore outlive the context that created them.
* Because the cache is `[ThreadStatic]`, an entry created on thread A is **invisible to thread B**. Per
  thread-pool hop (`Task.Run`, continuations on a different pool thread) the hit rate drops to zero until
  the shape is seen again on that thread. This is also the reason the shared, mutable `DbCommand` is safe:
  only one thread can ever reach a given entry.
* `QueryCommand.Cache = false` disables hashing **and** lookup for that command. Note this is *not* a
  cheap "skip the cache" switch: it forces a full plan rebuild on every call and measured
  **3.52× slower** end-to-end (42.06 µs vs 17.56 µs) with a Gen1 collection per call. Do not use it as a
  tuning knob.

---

## 2. Explicit `Prepare()`

```csharp
var prepared = dataContext.SimpleEntity
    .Where(x => x.Id == NORM.Param<int>(0))
    .Select(x => x.Id)
    .Prepare();                 // buffered/scalar; streaming needs nonStreamUsing: false

var rows = prepared.ToList(dataContext, 42);          // IDataContext is passed on every execution
var one  = prepared.First(dataContext, 42);
```

`QueryCommand.Prepare(bool nonStreamUsing = true, CancellationToken ct)` calls
`GetPreparedQueryCommand(this, !nonStreamUsing, storeInCache: false, ct)` (`QueryCommand.cs:810`), which has
two consequences that explain the whole performance difference:

1. **`storeInCache: false` means the plan cache is never populated**, so `Prepare()` cannot "pollute" the
   cache and cannot be found by a later implicit lookup.
2. The same flag is passed as `dontCalculateHash: true`, so **no `*PlanHash` is computed at all**. That
   removes 46% of the per-call work (the hashing/lookup step) plus the repeated `ExtractParams` cost, and is
   why `Prepare()` allocates 4.94× less.

### `nonStreamUsing`

| Value | Use with | `ResultSetEnumerator` | Notes |
|---|---|---|---|
| `true` (default) | buffered / scalar terminals: `ToList`, `ToListAsync`, `First`, `Single`, `ExecuteScalar`, `Any`, `Count` | not created | streaming fails with an actionable `InvalidOperationException` telling you to use `nonStreamUsing: false` |
| `false` | `ToAsyncEnumerable`, `CreateAsyncEnumerator`, `ToEnumerable`, `Pipeline` | created | also usable with buffered terminals |

### Rules and limitations

* The terminal methods take the `IDataContext` as the first argument — a prepared command is *not* bound to
  the context that produced it, so it can be executed against another context of the same provider
  (pooled/scoped contexts are fine).
* It **is** bound to the provider *representation*: `DbContext.AsDbCommand` rejects anything that is not a
  `DbPreparedQueryCommand<TResult>` with an `ArgumentException`, so a command prepared by
  `SqliteDbContext` cannot be executed by a `PostgresDbContext`.
* A prepared command owns **one mutable `DbCommand` and one `ResultSetEnumerator`**. Parameter values,
  `DbCommand.Connection` and the enumerator's reader are overwritten on every execution, so:
  * do not share one prepared command across threads;
  * do not have two overlapping iterations over the same prepared command (the second
    `InitEnumerator`/reader replaces the first).
  Buffered terminals do not use the enumerator, but they still mutate parameter values and the connection.

---

## 3. Choosing

* **Repeated execution of the same query shape, latency/allocation sensitive** → `Prepare()` and keep the
  returned `IPreparedQueryCommand<TResult>`. Best measured option in every scenario benchmarked.
* **A query executed once, or a handful of times, written inline** → the implicit cache (or nothing) is
  fine; `Prepare()` is not worth the ceremony.
* **Schema changes at runtime** → call `PurgeQueryCache()` on the context (or recreate the context) before
  reusing the affected shapes.
* **Multi-threaded use of one command object** → never share a `PreparedQueryCommand`; the implicit cache
  is per-thread and safe by construction.
* **Do not disable the cache** hoping to go faster: `Cache = false` is the slowest measured configuration.

Cached vs `Prepare()` end-to-end, across benchmark classes (same SQL, same plan):

| Benchmark | `Prepare()` | implicit cache | Time | Allocated |
|---|---:|---:|---:|---:|
| `SqliteBenchmarkAny` | 943.2 µs / 85.2 KB | 1438.4 µs / 352.4 KB | 1.53× | 4.14× |
| `SqliteBenchmarkJoin` | 109.5 µs / 12.3 KB | 348.0 µs / 92.2 KB | 3.18× | 7.49× |
| `SqliteBenchmarkWhere` | 950.1 µs / 100.6 KB | 1699.7 µs / 485.8 KB | 1.79× | 4.83× |
| `SqliteBenchmarkFirst` | 94.8 µs / 8.6 KB | 159.1 µs / 44.1 KB | 1.68× | 5.15× |
| `SqliteBenchmarkSimulateWork` (stream) | 7.00 ms / 6.24 MB | 57.07 ms / 30.55 MB | 8.15× | 4.90× |
| `SqliteBenchmarkCachedPlan` (1 row) | 11.94 µs / 0.76 KB | 17.56 µs / 3.76 KB | 1.47× | 4.94× |

---

## 4. Known problems

### Fixed

1. **Crash: buffered then streaming on the same query shape** (fixed 2026-09-16).
   The cache-hit branch of `GetPreparedQueryCommand` used to ignore `createEnumerator`, so a shape first
   executed with `ToList`/`First`/`ExecuteScalar` was stored **without** a `ResultSetEnumerator`; a later
   `ToAsyncEnumerable` / `ToEnumerable` on the same shape then dereferenced `compiledQuery.Enumerator!`
   and threw `NullReferenceException`. The hit branch now creates the enumerator on demand
   (`if (createEnumerator && compiledQuery.Enumerator is null)` in `DbContext.cs`). Covered by
   `PlanCacheTests.BufferedThenAsyncStreaming_OnTheSameShape_ShouldNotThrow` and
   `PlanCacheTests.BufferedThenSyncStreaming_OnTheSameShape_ShouldNotThrow`.
2. **`Prepare()` default + streaming failed with a bare `NullReferenceException`** (fixed 2026-09-16).
   Streaming a command prepared with `nonStreamUsing: true` (the default) is still unsupported, but it now
   fails through `DbContext.RequireEnumerator` with an `InvalidOperationException` that says to use
   `Prepare(nonStreamUsing: false)`. Covered by `PlanCacheTests.Prepared_Default_ShouldNotBeStreamable`.
   Note `ToEnumerable` is a lazy iterator, so on that path the exception surfaces when enumeration starts,
   not when the call is made.

### Open

1. **No cache invalidation on schema change** (see above).
2. **Thread hopping loses cache hits** — the cache is `[ThreadStatic]`, so per-request thread-pool
   affinity determines the hit rate.
3. **`GetAnyCommand` / `ReplaceCommand`** mutates the shared cached `AnyCommand`, so concurrent use of one
   `DbContext` can let different predicates observe the wrong plan (correctness, tracked separately).

---

## 5. Reproducing the measurements

```bash
# Decisive comparison (prepare / cache / no cache), reliable job:
NEXTORM_BENCH_FULL=1 ./benchmarks/nextorm.benchmark/bin/linux/Release/net10.0/nextorm.benchmark \
    --filter "*SqliteBenchmarkCachedPlan*"

# Default quick job (Job.ShortRun, in-process) — good for ratios, not for 1-2 ns effects:
./benchmarks/nextorm.benchmark/bin/linux/Release/net10.0/nextorm.benchmark \
    --filter "*SqliteBenchmarkCachedPlan*"

# Characterisation tests for both reuse paths:
dotnet run --project test/nextorm.sqlite.tests -c Release
```

See also `performance-findings.md` (finding M12) for the full decomposition and the history of this
investigation.
