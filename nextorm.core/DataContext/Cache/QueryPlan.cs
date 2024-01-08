namespace nextorm.core;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S3897:Classes that provide \"Equals(<T>)\" should implement \"IEquatable<T>\"", Justification = "<Pending>")]
public sealed class QueryPlan
{
    public QueryCommand QueryCommand;
    //private readonly QueryPlanEqualityComparer _comparer;
    private int? _hashPlan;
    public QueryPlan(QueryCommand cmd)
    {
        QueryCommand = cmd;
        //_comparer = new QueryPlanEqualityComparer(cache, cmd);
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Bug", "S2328:\"GetHashCode\" should not reference mutable fields", Justification = "<Pending>")]
    public override int GetHashCode() => _hashPlan ??= QueryCommand.GetQueryPlanEqualityComparer().GetHashCode(QueryCommand);
    public override bool Equals(object? obj)
    {
        return Equals(obj as QueryPlan);
    }
    public bool Equals(QueryPlan? obj)
    {
        if (obj is null) return false;

        return QueryCommand.GetQueryPlanEqualityComparer().Equals(QueryCommand, obj.QueryCommand);
    }

    public QueryPlan GetCacheVersion()
    {
        QueryCommand = QueryCommand.Clone();
        return this;
    }
}