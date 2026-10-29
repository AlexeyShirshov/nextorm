namespace NextORM.Integration.Tests;

/// <summary>
/// The xunit collection every T320 table-valued-function class that asserts prepared-command
/// <em>identity</em> joins.
/// </summary>
/// <remarks>
/// <para>
/// Those facts prepare the same statement twice with <c>storeInCache: true</c> and assert
/// <c>ReferenceEquals(first, second)</c> — the cached path is the subject of the test. The plan store
/// behind that identity is process-wide: <c>QueryPlanStore</c> keeps a <c>private static long
/// _generation</c> (<c>src/nextorm.core/DataContext/Cache/QueryPlanStore.cs:29</c>) and
/// <c>Clear() =&gt; Interlocked.Increment(ref _generation)</c> (<c>:181</c>); every thread drops its own
/// plan dictionary the next time it looks, so a hit before a purge and a miss after it are both correct.
/// <c>DataContext.PurgeQueryCache()</c> (<c>DataContext.cs:1996</c> → <c>QueryCache.PurgeQueryCache()</c>
/// → <c>_purge</c>) reaches it, and the integration suite calls it from many classes in many
/// collections.
/// </para>
/// <para>
/// Measured on this tree (step-1 n=3 probe, deleted afterwards): a single
/// <c>PurgeQueryCache()</c> between the two prepares forks the identity deterministically
/// (<c>sameInstance=False</c>), and a purger running on another thread forks it on all 400 of 400
/// iterations. The identity therefore only holds while nothing else in the process purges, which is
/// what <c>DisableParallelization</c> guarantees and why <c>Query plan cache</c> and
/// <c>T327 array rewind cache</c> use the same shape.
/// </para>
/// </remarks>
[CollectionDefinition("T320 tvf plan identity", DisableParallelization = true)]
public sealed class T320TvfPlanIdentityCollection;
