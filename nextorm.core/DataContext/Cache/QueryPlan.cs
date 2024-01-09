using System.Diagnostics;

namespace nextorm.core;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S3897:Classes that provide \"Equals(<T>)\" should implement \"IEquatable<T>\"", Justification = "<Pending>")]
public sealed class QueryPlan(QueryCommand cmd)
{
    public QueryCommand QueryCommand = cmd;
    private QueryPlanEqualityComparer _comparer = cmd.GetQueryPlanEqualityComparer();
    private int? _hashPlan;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Bug", "S2328:\"GetHashCode\" should not reference mutable fields", Justification = "<Pending>")]
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
        var newCmd = QueryCommand.Clone();
        Debug.Assert(_comparer == newCmd.GetQueryPlanEqualityComparer(), "QueryPlanEqualityComparer must be equals, if not see QueryCommand.CopyTo function");
        Debug.Assert(_hashPlan == _comparer.GetHashCode(newCmd), "Hash must be equals, if not see QueryCommand.CopyTo function");
        Debug.Assert(_comparer.Equals(newCmd, QueryCommand), "QueryCommands must be equals, if not see QueryCommand.CopyTo function");
        QueryCommand = newCmd;
        _comparer = newCmd.GetQueryPlanEqualityComparer();
        return this;
    }
}