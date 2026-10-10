using System.Collections.Concurrent;
using System.Threading;

namespace NextORM.Core;

/// <summary>
/// Process-wide caches shared by every context, SQL and in-memory alike. This is the single source
/// of truth for entity metadata, select lists and compiled expression delegates.
/// </summary>
/// <remarks>
/// The sharing scope of each cache in the code base is stated here explicitly, so that the lifetime
/// of a cache is never a matter of archaeology:
/// <list type="bullet">
/// <item><description>this class and <c>MapperCache</c> — process-wide;</description></item>
/// <item><description><c>InMemoryDataContext.ExpressionsCache</c> — per context instance: its entries
///   embed <c>Expression.Constant(this)</c> and therefore must not be shared;</description></item>
/// <item><description><c>DataContext._queryPlanCache</c> — per thread, keyed by
///   <c>QueryPlanCacheKey(ContextType, Plan)</c> so that different providers do not collide.</description></item>
/// </list>
/// </remarks>
public static class DataContextCache
{
    private readonly static TimedDictionary<Type, IEntityMetadata> _metadata = new();
    private readonly static TimedDictionary<Type, IEntityMetadata> _tvpMetadata = new();
    private readonly static ConcurrentDictionary<Type, byte> _autoPublishedJunctionMetadata = new();
    private readonly static TimedDictionary<Type, SelectExpression[]> _selectListCache = new();
    private readonly static TimedDictionary<ExpressionKey, Delegate> _expCache = new();
    private readonly static TimedDictionary<ExpressionKey, Func<object?, object?>> _inValuesCache = new();
    private readonly static ConcurrentDictionary<string, Func<IDataContext, object>> _queryFilterContextAccessors = new();
    private static long _cacheSlidingExpirationTicks;

    /// <summary>
    /// Process-wide gate serializing every mutation of <see cref="Metadata"/>. The EF Core model mapper
    /// stages its whole import and publishes it under this gate, so it re-reads the current entries and
    /// revalidates conflicts immediately before writing. The core writers take the same gate and
    /// revalidate against the current entry before writing, so a concurrent auto/configured registration
    /// or junction publication can neither clobber a bridged entry nor be clobbered by it.
    /// </summary>
    /// <remarks>
    /// A single process-wide lock is used deliberately: the coordinated writers never acquire another
    /// lock while holding it, so no lock-ordering deadlock is possible. Metadata building runs outside
    /// the gate (and user configuration callbacks are never invoked under it), so the gate is held only
    /// across the read-revalidate-write publication itself.
    /// </remarks>
    internal static readonly object MetadataRegistrationGate = new();

    /// <summary>
    /// Entity metadata resolved for each CLR type, keyed by that type. Populated lazily on the first
    /// <c>From&lt;T&gt;</c> call and reused for the rest of the process.
    /// </summary>
    public static IDictionary<Type, IEntityMetadata> Metadata => _metadata;
    /// <summary>
    /// Auto-resolved entity metadata kept by the table-valued parameter binder for a row type that was
    /// never registered as a query source, keyed by that type. It is deliberately separate from
    /// <see cref="Metadata"/>: seeding the configured cache from the auto path would make a later
    /// <c>From&lt;T&gt;(cfg)</c> registration silently reuse the auto-built mapping instead of the
    /// configured one. The auto path always prefers an entry in <see cref="Metadata"/> and only falls
    /// back to this cache. Internal: not part of the public cache surface.
    /// </summary>
    internal static IDictionary<Type, IEntityMetadata> TvpMetadata => _tvpMetadata;
    /// <summary>
    /// Junction entity types whose mapping was auto-published into <see cref="Metadata"/> by the
    /// many-to-many resolver, without a user configuration, so the derived link source can read its
    /// columns. Tracked separately so a later <c>From&lt;TJunction&gt;(cfg)</c> can tell an auto-built
    /// entry from a configured one and rebuild it from the configuration instead of silently reusing
    /// the auto mapping. The auto path clears the marker when the configuration wins. Internal: not
    /// part of the public cache surface.
    /// </summary>
    internal static IDictionary<Type, byte> AutoPublishedJunctionMetadata => _autoPublishedJunctionMetadata;
    /// <summary>
    /// Cached select lists (the projected columns) of each CLR type, keyed by that type, so the
    /// projection is not rebuilt per query.
    /// </summary>
    public static IDictionary<Type, SelectExpression[]> SelectListCache => _selectListCache;
    /// <summary>
    /// Compiled expression delegates keyed by <see cref="ExpressionKey"/>, avoiding recompilation of
    /// the same expression tree when it is encountered again.
    /// </summary>
    public static IDictionary<ExpressionKey, Delegate> ExpressionsCache => _expCache;
    /// <summary>
    /// Compiled accessors that read the captured collection of an <c>in</c>/<c>Contains</c> predicate,
    /// keyed by the collection expression's shape. See <see cref="InValuesEvaluator"/>.
    /// </summary>
    public static IDictionary<ExpressionKey, Func<object?, object?>> InValuesCache => _inValuesCache;
    /// <summary>
    /// Compiled accessors for the owner-getter/member subtree a query filter imported from EF Core
    /// reads through <see cref="QueryFilterContext.Context"/>. The accessor takes the executing
    /// <see cref="IDataContext"/> as its only argument, so it is host-free; the key is the canonical
    /// string text of that accessor lambda, which therefore holds no live context, query command or
    /// owner instance. Internal: an engine detail of the EF Core bridge, deliberately kept out of the
    /// public cache surface.
    /// </summary>
    internal static IDictionary<string, Func<IDataContext, object>> QueryFilterContextAccessors => _queryFilterContextAccessors;

    /// <summary>
    /// Sliding expiration applied to every process-wide cache this class owns. An entry that has not
    /// been read within the window is evicted lazily on the next access; every read refreshes it.
    /// <see cref="TimeSpan.Zero"/> (the default) disables expiration and adds no per-access cost.
    /// Set through <c>DataContextBuilder.UseCacheSlidingExpiration</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The assigned value is negative.</exception>
    public static TimeSpan CacheSlidingExpiration
    {
        get => TimeSpan.FromTicks(Interlocked.Read(ref _cacheSlidingExpirationTicks));
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
            Interlocked.Exchange(ref _cacheSlidingExpirationTicks, value.Ticks);
        }
    }

    /// <summary>
    /// Clears every process-wide cache in the engine: the metadata, table-valued-parameter metadata,
    /// select-list, expression and in-values caches owned by this class, the compiled row-mapper cache,
    /// the projection-alias and FROM caches, the compiled <c>JoinInto</c> identity-selector cache, and
    /// the thread-local plan cache. The next query rebuilds whatever it needs.
    /// </summary>
    /// <remarks>
    /// The plan cache is <c>[ThreadStatic]</c>, so a plan created on another thread is dropped lazily
    /// when that thread next touches the cache (the store is invalidated through a process-wide
    /// generation counter). A context's per-instance caches (for example
    /// <c>InMemoryDataContext.CommandIndex</c>) are not reached; clear those with
    /// <c>PurgeQueryCache()</c>.
    /// </remarks>
    public static void Clear()
    {
        _metadata.Clear();
        _tvpMetadata.Clear();
        _autoPublishedJunctionMetadata.Clear();
        _selectListCache.Clear();
        _expCache.Clear();
        _inValuesCache.Clear();
        _queryFilterContextAccessors.Clear();
        MapperCache.Clear();
        RawMapperFactory.Clear();
        ProjectionAliasCache.Clear();
        QueryPlanner.ClearFromCache();
        QueryPlanStore.Clear();
        MemberInfoExtensions.ClearColumnNames();
        JoinIntoSpecHelpers.ClearIdentitySelectorCache();
    }

    /// <summary>
    /// Test-only helper that ages every timed entry in the process-wide caches this class owns past the
    /// current <see cref="CacheSlidingExpiration"/> window, so the real lazy-expiration path can be
    /// exercised deterministically without a wall-clock sleep. It only ages entries; the next read
    /// evicts them. A non-positive window leaves every store untouched. Internal: not part of the
    /// public cache surface.
    /// </summary>
    internal static void ExpireTimedEntriesForTesting()
    {
        _metadata.ExpireEntriesForTesting();
        _tvpMetadata.ExpireEntriesForTesting();
        _selectListCache.ExpireEntriesForTesting();
        _expCache.ExpireEntriesForTesting();
        _inValuesCache.ExpireEntriesForTesting();
    }
}
