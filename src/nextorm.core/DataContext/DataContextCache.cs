using System.Collections.Concurrent;

namespace nextorm.core;

/// <summary>
/// Process-wide caches shared by every context, SQL and in-memory alike. This is the single source
/// of truth for entity metadata, select lists and compiled expression delegates.
/// </summary>
/// <remarks>
/// The sharing scope of each cache in the code base is stated here explicitly, so that the lifetime
/// of a cache is never a matter of archaeology:
/// <list type="bullet">
/// <item><description>this class and <c>MapperCache</c> — process-wide;</description></item>
/// <item><description><c>InMemoryContext.ExpressionsCache</c> — per context instance: its entries
///   embed <c>Expression.Constant(this)</c> and therefore must not be shared;</description></item>
/// <item><description><c>DbContext._queryPlanCache</c> — per thread, keyed by
///   <c>QueryPlanCacheKey(ContextType, Plan)</c> so that different providers do not collide.</description></item>
/// </list>
/// </remarks>
public static class DataContextCache
{
    private readonly static ConcurrentDictionary<Type, IEntityMeta> _metadata = new();
    private readonly static ConcurrentDictionary<Type, SelectExpression[]> _selectListCache = new();
    private readonly static ExpressionCache<Delegate> _expCache = new();
    private readonly static ExpressionCache<Func<object?, object?>> _inValuesCache = new();
    public static IDictionary<Type, IEntityMeta> Metadata => _metadata;
    public static IDictionary<Type, SelectExpression[]> SelectListCache => _selectListCache;
    public static IDictionary<ExpressionKey, Delegate> ExpressionsCache => _expCache;
    /// <summary>
    /// Compiled accessors that read the captured collection of an <c>in</c>/<c>Contains</c> predicate,
    /// keyed by the collection expression's shape. See <see cref="InValuesEvaluator"/>.
    /// </summary>
    public static IDictionary<ExpressionKey, Func<object?, object?>> InValuesCache => _inValuesCache;

}