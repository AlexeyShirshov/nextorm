using System.Diagnostics;

namespace nextorm.core;

/// <summary>
/// Canonical, hashable description of a query, used as the key of the plan cache.
/// </summary>
public sealed class QueryPlan : IEquatable<QueryPlan>
{
    public QueryCommand QueryCommand;
    private readonly string? _sql;
    private QueryPlanEqualityComparer _comparer;
    // QueryPlan is used as a dictionary key (DbContext.QueryPlanCache, InMemoryDataContext._cmdIdx).
    // The hash is captured from the state at construction and then frozen: GetCacheVersion() swaps
    // QueryCommand/_comparer for an equal clone (see QueryCommand.CloneForCache), so plan identity —
    // and therefore the hash — must not change. Keeping it in a readonly field makes GetHashCode
    // stable even after GetCacheVersion() mutates the plan, which a dictionary key requires.
    private readonly int _hashPlan;

    public QueryPlan(QueryCommand cmd, string? sql)
    {
        QueryCommand = cmd;
        _sql = sql;
        _comparer = cmd.GetQueryPlanEqualityComparer();
        _hashPlan = ComputeHash(cmd, sql, _comparer);
    }

    private static int ComputeHash(QueryCommand cmd, string? sql, QueryPlanEqualityComparer comparer)
    {
        if (sql is not null)
        {
            unchecked
            {
                var hash = new HashCode();
                hash.Add(cmd, comparer);
                hash.Add(sql);
                return hash.ToHashCode();
            }
        }
        return comparer.GetHashCode(cmd);
    }

    public override int GetHashCode() => _hashPlan;

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
        Debug.Assert(_hashPlan == ComputeHash(newCmd, _sql, _comparer), "Hash must be equals, if not see QueryCommand.CopyTo function");
        Debug.Assert(_comparer.Equals(newCmd, QueryCommand), "QueryCommands must be equals, if not see QueryCommand.CopyTo function");
        QueryCommand = newCmd;
        _comparer = newCmd.GetQueryPlanEqualityComparer();
        return this;
    }
}
