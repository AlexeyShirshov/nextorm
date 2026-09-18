namespace nextorm.core;

public partial class QueryCommand
{

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
        dst._hints = _hints;

        dst.ResultType = ResultType;
        dst.Paging = Paging;
        dst.SingleRow = SingleRow;
        dst.IsDistinct = IsDistinct;
        dst.GroupingType = GroupingType;
        dst.GroupingSets = GroupingSets;
        dst.TableHints = TableHints;
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
                dst._ctes = _ctes.Select(c => new CteDefinition(c.Name, c.Query.CloneForCache(), c.Recursive, c.MaxRecursion)).ToList();
            }

            if (_referencedQueries?.Count > 0)
            {
                dst._referencedQueries = _referencedQueries.Select(it => it.CloneForCache()).ToList();
            }
        }
    }

    protected virtual QueryCommand CreateSelf()
    {
        return new QueryCommand(_dataContext, _exp, _srcType, _condition, _joins, Paging, _sorting, _groupExp, _having, Logger);
    }
    protected virtual QueryCommand CreateSelfForClone()
    {
        return new QueryCommand(null, null, _srcType, null, CloneForCache(_joins), Paging, _sorting, null, _having, Logger);
    }

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

    public QueryCommand CloneForCache()
    {
        var cmd = CreateSelfForClone();
        CopyTo(cmd, false);
        return cmd;
    }
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
