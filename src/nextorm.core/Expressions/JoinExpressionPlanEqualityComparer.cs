using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
namespace NextORM.Core;

/// <summary>
/// Compares and hashes joins for query-plan caching, taking the join type, strictness, global flag,
/// condition and joined source into account.
/// </summary>
public sealed class JoinExpressionPlanEqualityComparer : IEqualityComparer<JoinExpression>
{
    //private readonly IDictionary<ExpressionKey, Delegate> _cache;
    private readonly IQueryRegistry _queryProvider;
    //private readonly ILogger? _logger;
    //private ExpressionPlanEqualityComparer? _expComparer;
    //private QueryPlanEqualityComparer? _cmdComparer;

    // public PreciseExpressionEqualityComparer()
    //     : this(new ExpressionCache<Delegate>())
    // {
    // }
    /// <summary>Initializes a comparer without a logger.</summary>
    /// <param name="queryProvider">The registry that supplies the nested plan comparers.</param>
    public JoinExpressionPlanEqualityComparer(IQueryRegistry queryProvider)
        : this(queryProvider, null)
    {
    }
    /// <summary>Initializes a comparer.</summary>
    /// <param name="queryProvider">The registry that supplies the nested plan comparers.</param>
    /// <param name="logger">An optional logger; currently unused.</param>
    public JoinExpressionPlanEqualityComparer(IQueryRegistry queryProvider, ILogger? logger)
    {
        //_cache = cache ?? new ExpressionCache<Delegate>();
        _queryProvider = queryProvider;
        //_logger = logger;
    }
    //     private JoinExpressionPlanEqualityComparer() { }
    //     public static JoinExpressionPlanEqualityComparer Instance => new();
    /// <summary>Determines whether two joins are equal for plan-cache purposes.</summary>
    /// <param name="x">The first join.</param>
    /// <param name="y">The second join.</param>
    /// <returns><c>true</c> when both joins describe the same operation with equivalent expressions.</returns>
    public bool Equals(JoinExpression? x, JoinExpression? y)
    {
        if (x == y) return true;
        if (x is null || y is null) return false;

        if (x.JoinType != y.JoinType) return false;
        if (x.Strictness != y.Strictness) return false;
        if (x.IsGlobal != y.IsGlobal) return false;
        if (!string.Equals(x.JoinHint, y.JoinHint, StringComparison.Ordinal)) return false;
        if (!TableHintsEqual(x.TableHints, y.TableHints)) return false;

        // A JoinInto join carries the identity of its declaration (child type, keys, collection, kind),
        // which the rendered condition/source alone cannot distinguish. Distinct declarations must not
        // share a cached plan even when they render the same SQL.
        if (!Equals(x.JoinIntoIdentity, y.JoinIntoIdentity)) return false;

        //_expComparer ??= new ExpressionPlanEqualityComparer(_cache, _queryProvider);
        if (!_queryProvider.GetExpressionPlanEqualityComparer().Equals(x.JoinCondition, y.JoinCondition)) return false;

        //_cmdComparer ??= new QueryPlanEqualityComparer(_cache, _queryProvider);
        if (!_queryProvider.GetFromExpressionPlanEqualityComparer().Equals(x.From, y.From)) return false;

        return true;
    }

    // Ordinal sequence equality for the optional per-join table-hint list. Null and an empty list are
    // equivalent (the fluent setter normalizes blank-only input to null), so both mean "no hints" and
    // must map to the same plan key; otherwise the lists are compared order-sensitively by count.
    private static bool TableHintsEqual(IReadOnlyList<string>? x, IReadOnlyList<string>? y)
    {
        if (ReferenceEquals(x, y)) return true;

        var xCount = x?.Count ?? 0;
        var yCount = y?.Count ?? 0;
        if (xCount != yCount) return false;

        for (var i = 0; i < xCount; i++)
        {
            if (!string.Equals(x![i], y![i], StringComparison.Ordinal)) return false;
        }

        return true;
    }
    /// <summary>Returns a hash code for a join consistent with the equality comparison.</summary>
    /// <param name="obj">The join to hash.</param>
    /// <returns>The hash code, or zero when <paramref name="obj"/> is <c>null</c>.</returns>
    public int GetHashCode([DisallowNull] JoinExpression obj)
    {
        if (obj is null) return 0;

        unchecked
        {
            var hash = new XxHash32();

            hash.Add(obj.JoinType);

            hash.Add(obj.Strictness);

            hash.Add(obj.IsGlobal);

            hash.Add(obj.JoinHint);

            if (obj.TableHints is { Count: > 0 } tableHints)
            {
                for (var i = 0; i < tableHints.Count; i++)
                    hash.Add(tableHints[i], StringComparer.Ordinal);
            }

            if (obj.JoinIntoIdentity is { } identity)
                hash.Add(identity.GetHashCode());

            hash.Add(obj.JoinCondition, _queryProvider.GetExpressionPlanEqualityComparer());

            hash.Add(obj.From, _queryProvider.GetFromExpressionPlanEqualityComparer());

            return hash.ToHashCode();
        }
    }
}