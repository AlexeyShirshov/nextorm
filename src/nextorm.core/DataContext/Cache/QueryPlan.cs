using System.Diagnostics;

namespace NextORM.Core;

/// <summary>
/// Canonical, hashable description of a query, used as the key of the plan cache.
/// </summary>
public sealed class QueryPlan : IEquatable<QueryPlan>
{
    /// <summary>
    /// The query command this plan describes. Replaced by an equal clone when
    /// <see cref="GetCacheVersion"/> is called.
    /// </summary>
    public QueryCommand QueryCommand;
    private readonly string? _sql;
    private QueryPlanEqualityComparer _comparer;
    // QueryPlan is used as a dictionary key (DataContext.QueryPlanCache, InMemoryDataContext._cmdIdx).
    // The hash is captured from the state at construction and then frozen: GetCacheVersion() swaps
    // QueryCommand/_comparer for an equal clone (see QueryCommand.CloneForCache), so plan identity —
    // and therefore the hash — must not change. Keeping it in a readonly field makes GetHashCode
    // stable even after GetCacheVersion() mutates the plan, which a dictionary key requires.
    private readonly int _hashPlan;
    // Non-null only for the test-only collision seam. When set, ComputeHash returns it verbatim, so
    // the "_hashPlan == ComputeHash(...)" invariant (asserted by GetCacheVersion) holds even for a
    // forced collision; production plans leave it null and hash structurally as before.
    private readonly int? _forcedHash;
    private bool _isCacheVersion;

    /// <summary>
    /// Captures a canonical, hashable description of <paramref name="cmd"/> and computes its stable
    /// hash. Used as the key of the plan cache.
    /// </summary>
    /// <param name="cmd">The command to describe.</param>
    /// <param name="sql">The generated SQL, which participates in the hash when known.</param>
    public QueryPlan(QueryCommand cmd, string? sql)
    {
        QueryCommand = cmd;
        _sql = sql;
        _comparer = cmd.GetQueryPlanEqualityComparer();
        _hashPlan = ComputeHash(cmd, sql, _comparer);
    }

    /// <summary>
    /// Test-only construction path: builds a plan whose stable hash is supplied by the caller and
    /// honoured by <c>ComputeHash</c>, so the "<c>_hashPlan == ComputeHash(...)</c>" invariant still
    /// holds. It is never used by the production path; the tests reach it through
    /// <c>InternalsVisibleTo</c> to drive a real <see cref="QueryPlanStore"/> hash collision and prove
    /// the store still falls back to structural <see cref="Equals(QueryPlan?)"/>. No public surface and
    /// no mutable/thread-static switch.
    /// </summary>
    /// <param name="cmd">The command to describe.</param>
    /// <param name="sql">The generated SQL, which participates in equality.</param>
    /// <param name="forcedHash">The hash to freeze into the plan, so two unequal plans can share one.</param>
    internal QueryPlan(QueryCommand cmd, string? sql, int forcedHash)
    {
        QueryCommand = cmd;
        _sql = sql;
        _comparer = cmd.GetQueryPlanEqualityComparer();
        _forcedHash = forcedHash;
        _hashPlan = ComputeHash(cmd, sql, _comparer);
    }

    private int ComputeHash(QueryCommand cmd, string? sql, QueryPlanEqualityComparer comparer)
    {
        // The test-only forced hash is part of the hash semantics, not a bypass: routing it through
        // ComputeHash keeps _hashPlan == ComputeHash(...) true so GetCacheVersion's invariant assert
        // also holds for a collision-seam plan. Production plans have _forcedHash == null.
        if (_forcedHash is int forced)
        {
            return forced;
        }

        if (sql is not null)
        {
            unchecked
            {
                var hash = new XxHash32();
                hash.Add(cmd, comparer);
                hash.Add(sql);
                return hash.ToHashCode();
            }
        }
        return comparer.GetHashCode(cmd);
    }

    /// <summary>
    /// Returns the hash captured when the plan was constructed, keeping plan identity stable even
    /// after <see cref="GetCacheVersion"/> swaps in an equal command clone.
    /// </summary>
    /// <returns>The stable plan hash.</returns>
    public override int GetHashCode() => _hashPlan;

    /// <summary>
    /// Determines whether <paramref name="obj"/> is a plan equal to this one.
    /// </summary>
    /// <param name="obj">The object to compare with, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal <see cref="QueryPlan"/>.</returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as QueryPlan);
    }

    /// <summary>
    /// Determines whether another plan has the same SQL and an equivalent query command.
    /// </summary>
    /// <param name="obj">The plan to compare with, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the plans are equal.</returns>
    public bool Equals(QueryPlan? obj)
    {
        // A command that is executed repeatedly reuses its memoized key instance, so the plan store
        // compares the very same object on every warm lookup: reference identity is a full match and
        // skips the structural query comparison below.
        if (ReferenceEquals(this, obj)) return true;
        if (obj is null) return false;

        return _sql == obj._sql && _comparer.Equals(QueryCommand, obj.QueryCommand);
    }

    /// <summary>
    /// Replaces <see cref="QueryCommand"/> with an equal clone whose build-time state has been
    /// released, so the plan can be stored in the cache without retaining the original query tree.
    /// The plan's hash is unchanged, preserving its identity as a dictionary key.
    /// </summary>
    /// <returns>This plan, after the command has been swapped for its cache clone.</returns>
    public QueryPlan GetCacheVersion()
    {
        // The same plan key can be stored more than once (the per-thread store is cleared and a
        // reused command repopulates it), and after the first call the command is already the
        // detached cache clone — cloning again would be wasted work and a clone of a clone.
        if (_isCacheVersion)
            return this;

        var newCmd = QueryCommand.CloneForCache();
        Debug.Assert(_comparer == newCmd.GetQueryPlanEqualityComparer(), "QueryPlanEqualityComparer must be equals, if not see QueryCommand.CopyTo function");
        Debug.Assert(_hashPlan == ComputeHash(newCmd, _sql, _comparer), "Hash must be equals, if not see QueryCommand.CopyTo function");
        Debug.Assert(_comparer.Equals(newCmd, QueryCommand), "QueryCommands must be equals, if not see QueryCommand.CopyTo function");
        QueryCommand = newCmd;
        _comparer = newCmd.GetQueryPlanEqualityComparer();
        _isCacheVersion = true;
        return this;
    }
}
