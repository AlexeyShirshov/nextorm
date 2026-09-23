namespace NextORM.Core;

/// <summary>
/// Clone and copy support for <see cref="QueryCommand"/>: the live clone used by prepared commands and
/// the trimmed clone kept as the plan-cache key.
/// </summary>
public partial class QueryCommand
{

    /// <summary>
    /// Copies the shared plan state of this command into <paramref name="dst"/>. When
    /// <paramref name="copyAll"/> is <c>true</c> the mutable query state (custom data, referenced
    /// queries, union, from and CTEs) is copied by reference for a live clone; otherwise those parts
    /// are deep-cloned so the plan-cache key cannot observe later mutations.
    /// </summary>
    /// <param name="dst">The target command.</param>
    /// <param name="copyAll">Whether to copy all state by reference instead of cloning the mutable parts.</param>
    protected virtual void CopyTo(QueryCommand dst, bool copyAll)
    {
        dst._selectList = _selectList;
        dst._groupingList = _groupingList;
        dst._isPrepared = _isPrepared;
        dst._srcType = _srcType;
        dst._dontCache = _dontCache;
        dst.ColumnsPlanHash = ColumnsPlanHash;
        dst.JoinPlanHash = JoinPlanHash;
        dst.WherePlanHash = WherePlanHash;
        dst.SortingPlanHash = SortingPlanHash;
        dst.PreparedCondition = PreparedCondition;
        dst.PreparedHaving = PreparedHaving;
        dst.InValuesShapeHash = InValuesShapeHash;
        dst.ResultPlanHash = ResultPlanHash;
        dst.GroupingPlanHash = GroupingPlanHash;
        // From/Union/ReferencedQueries hashes must travel with the clone too: QueryPlanEqualityComparer
        // .GetHashCode() reads them, and QueryPlan.Equals() considers the cloned members equal, so the
        // Equals => same-hash contract would otherwise be violated and a cached plan key could not be
        // found by a freshly built, logically equal plan.
        dst.FromPlanHash = FromPlanHash;
        dst.UnionPlanHash = UnionPlanHash;
        dst.ReferencedQueriesPlanHash = ReferencedQueriesPlanHash;
        dst.CtesPlanHash = CtesPlanHash;
        dst.HintsPlanHash = HintsPlanHash;
        dst.WindowsPlanHash = WindowsPlanHash;
        dst._hints = _hints;
        dst.QuoteIdentifiers = QuoteIdentifiers;
        dst.ResolvedQuoteIdentifiers = ResolvedQuoteIdentifiers;
        dst.NamingConvention = NamingConvention;
        dst.ResolvedNamingConvention = ResolvedNamingConvention;
        dst.KeywordCase = KeywordCase;
        dst.ResolvedKeywordCase = ResolvedKeywordCase;
        // Outer references participate in the plan key (QueryPlanEqualityComparer), so the cached
        // clone must carry them; otherwise the hash captured at construction would not match the
        // recomputed hash in QueryPlan.GetCacheVersion and the Debug.Assert would fail.
        dst._outerRefs = _outerRefs;
        dst.OuterRegistry = OuterRegistry;

        dst.ResultType = ResultType;
        dst.Paging = Paging;
        // The terminal flags participate in the plan key (QueryPlanEqualityComparer), so the cached
        // clone must carry them like the outer references; otherwise GetCacheVersion's Debug.Assert
        // fails and a stale plan's materializer could be reused.
        dst.DefaultOnEmpty = DefaultOnEmpty;
        dst.SingleScalar = SingleScalar;
        dst.SingleRow = SingleRow;
        dst.IsDistinct = IsDistinct;
        dst.GroupingType = GroupingType;
        dst.GroupingSets = GroupingSets;
        dst.GroupByWithTotals = GroupByWithTotals;
        dst.LimitBy = LimitBy;
        dst._limitByColumns = _limitByColumns;
        dst.DistinctOn = DistinctOn;
        dst._distinctOnColumns = _distinctOnColumns;
        dst.TableSample = TableSample;
        dst.Temporal = Temporal;
        dst.RowLock = RowLock;
        dst.Final = Final;
        dst.SampleRatio = SampleRatio;
        dst.SampleOffset = SampleOffset;
        dst.Settings = Settings;
        dst._preWhere = _preWhere;
        dst._arrayJoins = _arrayJoins;
        dst._windows = _windows;
        dst.ArrayJoinKind = ArrayJoinKind;
        dst.BindArrayJoinElement = BindArrayJoinElement;
        dst._preparedArrayJoin = _preparedArrayJoin;
        dst.PreparedPreWhere = PreparedPreWhere;
        dst.PreWhereShapeHash = PreWhereShapeHash;
        dst.HasPreWhereInValues = HasPreWhereInValues;
        dst.TableHints = TableHints;
        dst.IndexHints = IndexHints;
        dst.IndexHintKind = IndexHintKind;
        dst.ForJsonClause = ForJsonClause;
        dst.ForXmlClause = ForXmlClause;

        dst._queryPlanComparer = _queryPlanComparer;
        dst._fromExpressionPlanComparer = _fromExpressionPlanComparer;
        dst._expressionPlanComparer = _expressionPlanComparer;
        dst._selectExpressionPlanComparer = _selectExpressionPlanComparer;
        dst._joinExpressionPlanComparer = _joinExpressionPlanComparer;
        dst._sortingExpressionPlanComparer = _sortingExpressionPlanComparer;
        dst._unionType = _unionType;

        if (copyAll)
        {
            dst._customData = _customData;
            dst._referencedQueries = _referencedQueries;
            dst._union = _union;
            dst._from = _from;
            dst._ctes = _ctes;
        }
        else
        {
            if (_from is not null)
                dst._from = _from.CloneForCache();
            if (_union is not null)
                dst._union = _union.CloneForCache();

            if (_ctes?.Count > 0)
            {
                // The cached plan must own clones of the CTE queries (like From/Union/ReferencedQueries)
                // so later re-preparation of the live command cannot mutate what the cache compares against.
                dst._ctes = _ctes.Select(c => c.Mutation is not null
                    ? new CteDefinition(c.Name, c.Query.CloneForCache(), c.Mutation)
                    : new CteDefinition(c.Name, c.Query.CloneForCache(), c.Recursive, c.MaxRecursion)).ToList();
            }

            if (_referencedQueries?.Count > 0)
            {
                dst._referencedQueries = _referencedQueries.Select(it => it.CloneForCache()).ToList();
            }
        }
    }

    /// <summary>Creates an empty command of the same concrete type for a live clone.</summary>
    /// <returns>A new command bound to this command's data context.</returns>
    protected virtual QueryCommand CreateSelf()
    {
        return new QueryCommand(_dataContext, Definition);
    }
    /// <summary>Creates an empty command of the same type for a plan-cache key, detached from the data context.</summary>
    /// <returns>A new command with the mutable expressions cleared for later copying.</returns>
    protected virtual QueryCommand CreateSelfForClone()
    {
        return new QueryCommand(null, Definition with { Exp = null, Condition = null, Joins = CloneForCache(_joins), Group = null });
    }

    /// <summary>Deep-clones the join expressions so a cached plan cannot observe later mutations.</summary>
    /// <param name="joins">The joins to clone, or <c>null</c>.</param>
    /// <returns>The cloned joins, or <c>null</c> when <paramref name="joins"/> is <c>null</c>.</returns>
    protected static JoinExpression[]? CloneForCache(JoinExpression[]? joins)
    {
        if (joins is null) return null;

        var cnt = joins.Length;
        var newJoins = new JoinExpression[cnt];
        for (var idx = 0; idx < cnt; idx++)
        {
            newJoins[idx] = joins[idx].CloneForCache();
        }

        return newJoins;
    }

    /// <summary>
    /// Returns a live clone detached from its correlated scope: the outer references, referenced
    /// queries and outer registry are cleared, so the clone can be prepared and executed on its own by
    /// the in-memory correlated evaluator with the outer values supplied as runtime parameters.
    /// </summary>
    internal QueryCommand CloneForCorrelatedEvaluation()
    {
        var clone = (QueryCommand)((ICloneable)this).Clone();
        clone._outerRefs = null;
        clone._referencedQueries = null;
        clone.OuterRegistry = null;
        clone.ReferencedQueriesPlanHash = 0;
        clone.OneColumn = OneColumn;
        return clone;
    }

    /// <summary>Returns the detached clone that is stored in the plan cache and used as the cache key.</summary>
    /// <returns>The cache clone.</returns>
    public QueryCommand CloneForCache()
    {
        var cmd = CreateSelfForClone();
        CopyTo(cmd, false);
        return cmd;
    }
    /// <summary>Returns a deep, independently mutable copy of this command.</summary>
    /// <returns>The cloned command.</returns>
    public QueryCommand Clone()
    {
        return (QueryCommand)(this as ICloneable).Clone();
    }
    object ICloneable.Clone()
    {
        var cmd = CreateSelf();
        CopyTo(cmd, true);
        return cmd;
    }

}
