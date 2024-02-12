using System.Collections.Concurrent;

namespace nextorm.core;

public static class DataContextCache
{
    private readonly static ConcurrentDictionary<Type, IEntityMeta> _metadata = new();
    private readonly static ConcurrentDictionary<Type, SelectExpression[]> _selectListCache = new();
    private readonly static ExpressionCache<Delegate> _expCache = new();
    public static IDictionary<Type, IEntityMeta> Metadata => _metadata;
    public static IDictionary<Type, SelectExpression[]> SelectListCache => _selectListCache;
    public static IDictionary<ExpressionKey, Delegate> ExpressionsCache => _expCache;

}