using System.Diagnostics;

namespace nextorm.core;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S3897:Classes that provide \"Equals(<T>)\" should implement \"IEquatable<T>\"", Justification = "<Pending>")]
public sealed class QueryPlan(QueryCommand cmd, string? sql)
{
    public QueryCommand QueryCommand = cmd;
    private readonly string? _sql = sql;
    private QueryPlanEqualityComparer _comparer = cmd.GetQueryPlanEqualityComparer();
    private int? _hashPlan;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Bug", "S2328:\"GetHashCode\" should not reference mutable fields", Justification = "<Pending>")]
    public override int GetHashCode()
    {
        if (!_hashPlan.HasValue)
        {
            if (_sql != null)
            {
                unchecked
                {
                    var hash = new HashCode();
                    hash.Add(QueryCommand, _comparer);
                    hash.Add(_sql);
                    _hashPlan = hash.ToHashCode();
                }
            }
            else
                _hashPlan = _comparer.GetHashCode(QueryCommand);
        }
        return _hashPlan.Value;
    }
    public override bool Equals(object? obj)
    {
        return Equals(obj as QueryPlan);
    }
    public bool Equals(QueryPlan? obj)
    {
        if (obj is null) return false;

        return _sql == obj._sql && _comparer.Equals(QueryCommand, obj.QueryCommand);
    }

    public QueryPlan GetCacheVersion()
    {
        var newCmd = QueryCommand.CloneForCache();
        Debug.Assert(_comparer == newCmd.GetQueryPlanEqualityComparer(), "QueryPlanEqualityComparer must be equals, if not see QueryCommand.CopyTo function");
        Debug.Assert(GetHashCode() == new HashCode().Map(hash =>
        {
            if (_sql != null)
            {
                unchecked
                {
                    hash.Add(newCmd, _comparer);
                    hash.Add(_sql);
                    return hash.ToHashCode();
                }
            }
            else
                return _comparer.GetHashCode(QueryCommand);
        }), "Hash must be equals, if not see QueryCommand.CopyTo function");
        Debug.Assert(_comparer.Equals(newCmd, QueryCommand), "QueryCommands must be equals, if not see QueryCommand.CopyTo function");
        QueryCommand = newCmd;
        _comparer = newCmd.GetQueryPlanEqualityComparer();
        return this;
    }
}