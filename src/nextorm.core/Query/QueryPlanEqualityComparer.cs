using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace NextORM.Core;

public sealed class QueryPlanEqualityComparer : IEqualityComparer<QueryCommand?>
{
    // private readonly IDictionary<ExpressionKey, Delegate> _cache;
    // private readonly ILogger? _logger;
    private readonly ExpressionPlanEqualityComparer _expComparer;
    private readonly SelectExpressionPlanEqualityComparer _selectComparer;
    private readonly FromExpressionPlanEqualityComparer _fromComparer;
    private readonly JoinExpressionPlanEqualityComparer _joinComparer;
    private readonly SortingExpressionPlanEqualityComparer _sortComparer;
    //private readonly IQueryRegistry _queryProvider;

    // public PreciseExpressionEqualityComparer()
    //     : this(new ExpressionCache<Delegate>())
    // {
    // }
    public QueryPlanEqualityComparer(IQueryRegistry queryProvider)
    {
        //_queryProvider = queryProvider;
        _expComparer = queryProvider.GetExpressionPlanEqualityComparer();
        _selectComparer = queryProvider.GetSelectExpressionPlanEqualityComparer();
        _fromComparer = queryProvider.GetFromExpressionPlanEqualityComparer();
        _joinComparer = queryProvider.GetJoinExpressionPlanEqualityComparer();
        _sortComparer = queryProvider.GetSortingExpressionPlanEqualityComparer();
    }
    // private QueryPlanEqualityComparer() { }
    // public static QueryPlanEqualityComparer Instance => new();
    public bool Equals(QueryCommand? x, QueryCommand? y)
    {
        if (x == y) return true;
        if (x is null || y is null) return false;

        if (x.EntityType != y.EntityType) return false;

        if (x.ResultType != y.ResultType) return false;

        if (x.SingleRow != y.SingleRow) return false;

        if (x.IsDistinct != y.IsDistinct) return false;

        if (x.GroupByWithTotals != y.GroupByWithTotals) return false;

        var xLimitBy = x.LimitBy;
        var yLimitBy = y.LimitBy;
        if (xLimitBy is not null || yLimitBy is not null)
        {
            if (xLimitBy is null || yLimitBy is null) return false;

            if (xLimitBy.Limit != yLimitBy.Limit || xLimitBy.Offset != yLimitBy.Offset) return false;

            if (!_expComparer.Equals(xLimitBy.Expression, yLimitBy.Expression)) return false;
        }

        var xDistinctOn = x.DistinctOn;
        var yDistinctOn = y.DistinctOn;
        if (xDistinctOn is not null || yDistinctOn is not null)
        {
            if (xDistinctOn is null || yDistinctOn is null) return false;

            if (!_expComparer.Equals(xDistinctOn.Expression, yDistinctOn.Expression)) return false;
        }

        if (x.TableSample != y.TableSample) return false;

        if (x.Temporal != y.Temporal) return false;

        if (x.RowLock != y.RowLock) return false;

        if (x.Final != y.Final) return false;

        if (x.SampleRatio != y.SampleRatio || x.SampleOffset != y.SampleOffset) return false;

        if (!KeyValueListsEqual(x.Settings, y.Settings)) return false;

        if (!_expComparer.Equals(x.PreparedPreWhere, y.PreparedPreWhere)) return false;

        if (x.PreWhereShapeHash != y.PreWhereShapeHash) return false;

        if (x.ArrayJoinKind != y.ArrayJoinKind) return false;

        if (x.BindArrayJoinElement != y.BindArrayJoinElement) return false;

        if (!ExpressionListsEqual(x.ArrayJoinExpressions, y.ArrayJoinExpressions)) return false;

        if (x.GroupingType != y.GroupingType) return false;

        if (!GroupingSetsEqual(x.GroupingSets, y.GroupingSets)) return false;

        if (!StringListsEqual(x.TableHints, y.TableHints)) return false;

        if (x.ForJsonClause != y.ForJsonClause) return false;

        if (x.ForXmlClause != y.ForXmlClause) return false;

        if (x.Paging.Limit != y.Paging.Limit || x.Paging.Offset != y.Paging.Offset || x.Paging.HasWithTies != y.Paging.HasWithTies) return false;

        // First vs FirstOrDefault (and Single vs SingleOrDefault) share SQL and Paging.Limit, so the
        // terminal flags must be part of the key; otherwise the cached plan's materializer would
        // default-on-null for the wrong terminal (or throw for the other one).
        if (x.DefaultOnEmpty != y.DefaultOnEmpty) return false;

        if (x.SingleScalar != y.SingleScalar) return false;

        if (x.UnionType != y.UnionType) return false;

        if (!_fromComparer.Equals(x.From, y.From)) return false;

        if (!_selectComparer.Equals(x.SelectList, y.SelectList)) return false;

        if (!_expComparer.Equals(x.PreparedCondition, y.PreparedCondition)) return false;

        // The condition expression alone cannot distinguish captured collections of different lengths
        // (the closure access is shape independent), so the evaluated value-list shape is compared
        // explicitly to keep a shorter/longer list from reusing a stale plan.
        if (x.InValuesShapeHash != y.InValuesShapeHash) return false;

        if (!_joinComparer.Equals(x.Joins, y.Joins)) return false;

        if (!_sortComparer.ValueEquals(x.Sorting, y.Sorting)) return false;

        if (!_selectComparer.Equals(x.GroupingList, y.GroupingList)) return false;

        if (!_expComparer.Equals(x.Having, y.Having)) return false;

        if (!Equals(x.UnionQuery, y.UnionQuery)) return false;

        if (!CteDefinitionsEqual(x.Ctes, y.Ctes)) return false;

        if (!StringListsEqual(x.Hints, y.Hints)) return false;

        if (!IEqualityComparerExtensions.Equals(this, x.ReferencedQueries, y.ReferencedQueries)) return false;

        // The outer references are the actual expressions a correlated subquery points at (for
        // example `it.Id`). Two commands that differ only in that target share the same marker
        // indices and the same referenced-query SQL, so without comparing the targets a cached
        // plan for `o.A` would be reused for `o.B` and emit the wrong column.
        if (!_expComparer.Equals(x.OuterReferences, y.OuterReferences)) return false;

        return true;
    }

    private static bool StringListsEqual(IReadOnlyList<string>? x, IReadOnlyList<string>? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        if (x.Count != y.Count) return false;

        for (var (i, cnt) = (0, x.Count); i < cnt; i++)
        {
            if (!string.Equals(x[i], y[i], StringComparison.Ordinal)) return false;
        }

        return true;
    }

    private static bool KeyValueListsEqual(IReadOnlyList<KeyValuePair<string, string>>? x, IReadOnlyList<KeyValuePair<string, string>>? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        if (x.Count != y.Count) return false;

        for (var (i, cnt) = (0, x.Count); i < cnt; i++)
        {
            if (!string.Equals(x[i].Key, y[i].Key, StringComparison.Ordinal)) return false;
            if (!string.Equals(x[i].Value, y[i].Value, StringComparison.Ordinal)) return false;
        }

        return true;
    }

    private bool ExpressionListsEqual(IReadOnlyList<Expression>? x, IReadOnlyList<Expression>? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        if (x.Count != y.Count) return false;

        for (var (i, cnt) = (0, x.Count); i < cnt; i++)
        {
            if (!_expComparer.Equals(x[i], y[i])) return false;
        }

        return true;
    }

    private static bool GroupingSetsEqual(IReadOnlyList<int[]>? x, IReadOnlyList<int[]>? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        if (x.Count != y.Count) return false;

        for (var (s, cnt) = (0, x.Count); s < cnt; s++)
        {
            var a = x[s];
            var b = y[s];
            if (a.Length != b.Length) return false;

            for (var i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
        }

        return true;
    }

    private bool CteDefinitionsEqual(IReadOnlyList<CteDefinition>? x, IReadOnlyList<CteDefinition>? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        if (x.Count != y.Count) return false;

        for (var (i, cnt) = (0, x.Count); i < cnt; i++)
        {
            var a = x[i];
            var b = y[i];

            if (a.Name != b.Name) return false;
            if (a.Recursive != b.Recursive) return false;
            if (a.MaxRecursion != b.MaxRecursion) return false;
            if (!Equals(a.Query, b.Query)) return false;
        }

        return true;
    }

    public int GetHashCode(QueryCommand? obj)
    {
        if (obj is null)
            return 0;

        // #if PLAN_CACHE
        //         if (obj.PlanHash.HasValue)
        //             return obj.PlanHash.Value;
        // #endif
        unchecked
        {
            XxHash32 hash = new();

            if (obj.FromPlanHash != 0)
                hash.Add(obj.FromPlanHash);

            if (obj.EntityType is not null)
                hash.Add(obj.EntityType);

            if (obj.ResultPlanHash != 0)
                hash.Add(obj.ResultPlanHash);

            hash.Add(obj.SingleRow);

            hash.Add(obj.IsDistinct);

            hash.Add(obj.GroupByWithTotals);

            if (obj.GroupingType != GroupingType.None)
                hash.Add(obj.GroupingType);

            if (obj.GroupingSets is { Count: > 0 })
            {
                foreach (var set in obj.GroupingSets)
                {
                    hash.Add(set.Length);
                    foreach (var index in set)
                        hash.Add(index);
                }
            }

            if (obj.TableHints is { Count: > 0 })
                foreach (var hint in obj.TableHints)
                    hash.Add(hint);

            if (obj.ForJsonClause is { } forJson)
                hash.Add(forJson);

            if (obj.ForXmlClause is { } forXml)
                hash.Add(forXml);

            if (obj.WherePlanHash != 0)
                hash.Add(obj.WherePlanHash);

            hash.Add(obj.InValuesShapeHash);

            if (obj.ColumnsPlanHash != 0)
                hash.Add(obj.ColumnsPlanHash);

            if (obj.JoinPlanHash != 0)
                hash.Add(obj.JoinPlanHash);

            if (obj.SortingPlanHash != 0)
                hash.Add(obj.SortingPlanHash);

            hash.Add(obj.Paging.Limit);
            hash.Add(obj.Paging.Offset);
            hash.Add(obj.Paging.HasWithTies);

            hash.Add(obj.DefaultOnEmpty);
            hash.Add(obj.SingleScalar);

            if (obj.GroupingPlanHash != 0)
                hash.Add(obj.GroupingPlanHash);

            if (obj.LimitBy is { } limitBy)
            {
                hash.Add(limitBy.Limit);
                hash.Add(limitBy.Offset);
                hash.Add(limitBy.Expression, _expComparer);
            }

            if (obj.DistinctOn is { } distinctOn)
            {
                hash.Add(distinctOn.Expression, _expComparer);
            }

            if (obj.TableSample is { } tablesample)
            {
                hash.Add(tablesample.Method);
                hash.Add(tablesample.Percent);
                hash.Add(tablesample.Seed);
            }

            if (obj.Temporal is { } temporal)
            {
                hash.Add(temporal.Kind);
                hash.Add(temporal.From);
                hash.Add(temporal.To);
            }

            if (obj.RowLock is { } rowLock)
            {
                hash.Add(rowLock.Mode);
            }

            hash.Add(obj.Final);
            hash.Add(obj.SampleRatio);
            hash.Add(obj.SampleOffset);

            if (obj.Settings is { Count: > 0 } settings)
                foreach (var setting in settings)
                {
                    hash.Add(setting.Key);
                    hash.Add(setting.Value);
                }

            if (obj.PreparedPreWhere is not null)
                hash.Add(obj.PreparedPreWhere, _expComparer);

            hash.Add(obj.PreWhereShapeHash);

            hash.Add(obj.ArrayJoinKind);

            hash.Add(obj.BindArrayJoinElement);

            if (obj.ArrayJoinExpressions is { Count: > 0 } arrayJoinExpressions)
            {
                for (var (i, cnt) = (0, arrayJoinExpressions.Count); i < cnt; i++)
                    hash.Add(arrayJoinExpressions[i], _expComparer);
            }

            if (obj.UnionPlanHash != 0)
                hash.Add(obj.UnionPlanHash);

            if (obj.ReferencedQueriesPlanHash != 0)
                hash.Add(obj.ReferencedQueriesPlanHash);

            if (obj.OuterReferences is { Count: > 0 } outerReferences)
            {
                for (var (i, cnt) = (0, outerReferences.Count); i < cnt; i++)
                    hash.Add(outerReferences[i], _expComparer);
            }

            if (obj.CtesPlanHash != 0)
                hash.Add(obj.CtesPlanHash);

            if (obj.HintsPlanHash != 0)
                hash.Add(obj.HintsPlanHash);

            return hash.ToHashCode();
        }
    }
}