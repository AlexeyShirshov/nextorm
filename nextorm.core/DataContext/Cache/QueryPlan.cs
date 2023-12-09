#define PLAN_CACHE
#define ONLY_PLAN_CACHE

namespace nextorm.core;

#if PLAN_CACHE
public class QueryPlan
{
    public QueryCommand QueryCommand;
    private readonly QueryPlanEqualityComparer _comparer;
    private int? _hashPlan;
    public QueryPlan(QueryCommand cmd, IDictionary<ExpressionKey, Delegate>? cache)
    {
        QueryCommand = cmd;
        _comparer = new QueryPlanEqualityComparer(cache, cmd);
    }
    public override int GetHashCode() => _hashPlan ??= _comparer.GetHashCode(QueryCommand);
    public override bool Equals(object? obj)
    {
        return Equals(obj as QueryPlan);
    }
    public bool Equals(QueryPlan? obj)
    {
        if (obj is null) return false;

        return _comparer.Equals(QueryCommand, obj.QueryCommand);
    }

    public QueryPlan GetCacheVersion()
    {
        QueryCommand = QueryCommand.Clone();
        return this;
    }
}
#endif