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
    private readonly static TimedDictionary<Type, SelectExpression[]> _selectListCache = new();
    private readonly static TimedDictionary<ExpressionKey, Delegate> _expCache = new();
    private readonly static TimedDictionary<ExpressionKey, Func<object?, object?>> _inValuesCache = new();
    private static long _cacheSlidingExpirationTicks;

    /// <summary>
    /// Entity metadata resolved for each CLR type, keyed by that type. Populated lazily on the first
    /// <c>From&lt;T&gt;</c> call and reused for the rest of the process.
    /// </summary>
    public static IDictionary<Type, IEntityMetadata> Metadata => _metadata;
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
    /// Clears every process-wide cache in the engine: the metadata, select-list, expression and
    /// in-values caches owned by this class, the compiled row-mapper cache, the projection-alias and
    /// FROM caches, and the thread-local plan cache. The next query rebuilds whatever it needs.
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
        _selectListCache.Clear();
        _expCache.Clear();
        _inValuesCache.Clear();
        MapperCache.Clear();
        ProjectionAliasCache.Clear();
        QueryPlanner.ClearFromCache();
        QueryPlanStore.Clear();
    }
}
