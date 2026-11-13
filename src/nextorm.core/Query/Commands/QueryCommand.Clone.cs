namespace NextORM.Core;

/// <summary>
/// Clone and copy support for <see cref="QueryCommand"/>: the live clone used by prepared commands and
/// the trimmed clone kept as the plan-cache key.
/// </summary>
public partial class QueryCommand
{

    /// <summary>
    /// Copies the complete live state (including preparation-derived state) of this command into
    /// <paramref name="dst"/>. Used when a non-generic command must be adopted by a typed wrapper without
    /// re-preparing it — the correlated evaluator executes a clone whose condition has already been
    /// rewritten to parameter placeholders, so preparing it again would discard that rewrite.
    /// </summary>
    /// <param name="dst">The target command; it must be assignable from this command's runtime state.</param>
    internal void CopyAllStateTo(QueryCommand dst)
    {
        CopyTo(dst, copyAll: true);

        // These two are project state rather than plan state, so CopyTo does not carry them; a wrapper
        // that inherits a prepared flag without them would compile the wrong row shape (a scalar command
        // would try to construct the scalar type from its columns).
        dst.OneColumn = OneColumn;
        dst.IgnoreColumns = IgnoreColumns;
    }

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
        // Prepared-only columns travel as one payload block: an unprepared source must not make the
        // clone allocate a payload for state it does not have (issue #319).
        if (_preparedState is { } prepared)
        {
            var preparedTarget = dst.Prepared;
            preparedTarget.GroupingList = prepared.GroupingList;
            preparedTarget.LimitByColumns = prepared.LimitByColumns;
            preparedTarget.DistinctOnColumns = prepared.DistinctOnColumns;
            preparedTarget.ExtremeRowColumns = prepared.ExtremeRowColumns;
            preparedTarget.ExtremeRowGroupByColumns = prepared.ExtremeRowGroupByColumns;
            preparedTarget.PerParentPartitionColumns = prepared.PerParentPartitionColumns;
            preparedTarget.PreparedArrayJoin = prepared.PreparedArrayJoin;
            preparedTarget.PreparedCondition = prepared.PreparedCondition;
            preparedTarget.PreparedHaving = prepared.PreparedHaving;
            preparedTarget.PreparedPreWhere = prepared.PreparedPreWhere;
            preparedTarget.PreWhereShapeHash = prepared.PreWhereShapeHash;
            preparedTarget.HasPreWhereInValues = prepared.HasPreWhereInValues;
            preparedTarget.LookupPartitions = prepared.LookupPartitions;
            preparedTarget.ShapeScanned = prepared.ShapeScanned;
            preparedTarget.HasUnkeyedTupleInValues = prepared.HasUnkeyedTupleInValues;
            preparedTarget.HasUnkeyedScalarInValues = prepared.HasUnkeyedScalarInValues;
            preparedTarget.OuterRefs = prepared.OuterRefs;
            preparedTarget.OuterRefBindings = prepared.OuterRefBindings;
            preparedTarget.BoxedResultType = prepared.BoxedResultType;
            preparedTarget.EffectiveGroupingKeys = prepared.EffectiveGroupingKeys;
            preparedTarget.EffectiveHavingMode = prepared.EffectiveHavingMode;
            preparedTarget.EffectiveHaving = prepared.EffectiveHaving;
            preparedTarget.EffectiveSourcePredicate = prepared.EffectiveSourcePredicate;
            preparedTarget.EffectiveRequireDistinct = prepared.EffectiveRequireDistinct;
            preparedTarget.PreparedComponents = prepared.PreparedComponents;
            preparedTarget.PreparedComponentsView = prepared.PreparedComponentsView;
            preparedTarget.ComponentRenderShape = null;
        }
        dst._isPrepared = _isPrepared;
        dst._srcType = _srcType;
        dst._dontCache = _dontCache;
        dst._filterScope = _filterScope;
        dst.MappingScope = MappingScope;
        dst.ColumnsPlanHash = ColumnsPlanHash;
        dst.JoinPlanHash = JoinPlanHash;
        dst.WherePlanHash = WherePlanHash;
        dst.SortingPlanHash = SortingPlanHash;
        dst.InValuesShapeHash = InValuesShapeHash;
        // ShapeValuesStable is deliberately not copied: a clone's evaluated InValuesPartitions are not
        // transferred either, so a clone must keep the pre-existing refresh path instead of skipping it.
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
        dst.Tag = Tag;
        dst.QuoteIdentifiers = QuoteIdentifiers;
        dst.ResolvedQuoteIdentifiers = ResolvedQuoteIdentifiers;
        dst.NamingConvention = NamingConvention;
        dst.ResolvedNamingConvention = ResolvedNamingConvention;
        dst.KeywordCase = KeywordCase;
        dst.ResolvedKeywordCase = ResolvedKeywordCase;
        dst.CommandTimeout = CommandTimeout;
        dst.ResolvedCommandTimeout = ResolvedCommandTimeout;
        // The eager-load strategy and shape participate in the plan key (QueryPlanEqualityComparer), so
        // the cached clone must carry them; otherwise the hash captured at construction would not match
        // the recomputed hash in QueryPlan.GetCacheVersion and the Debug.Assert would fail.
        dst.EagerLoadMode = EagerLoadMode;
        dst.ResolvedEagerLoadMode = ResolvedEagerLoadMode;
        dst.EagerLoadShape = EagerLoadShape;
        // The collection-slot graph shape participates in the plan key too (QueryPlanEqualityComparer), so the
        // cached clone must carry it or GetCacheVersion's recomputed hash would differ from the frozen one.
        dst.CollectionSlotShape = CollectionSlotShape;
        dst.HasPerParentEagerTopN = HasPerParentEagerTopN;
        // Outer references participate in the plan key (QueryPlanEqualityComparer), so the cached
        // clone must carry them; otherwise the hash captured at construction would not match the
        // recomputed hash in QueryPlan.GetCacheVersion and the Debug.Assert would fail.
        // The occurrence bindings travel with the outer references so a cached clone keeps the metadata
        // needed to render its markers. They are runtime metadata only: neither is part of the plan key
        // (QueryPlanEqualityComparer reads OuterReferences, never the binding side table).
        dst.OuterRegistry = OuterRegistry;

        dst.ResultType = ResultType;
        dst.Paging = Paging;
        // The terminal flags participate in the plan key (QueryPlanEqualityComparer), so the cached
        // clone must carry them like the outer references; otherwise GetCacheVersion's Debug.Assert
        // fails and a stale plan's materializer could be reused.
        dst.DefaultOnEmpty = DefaultOnEmpty;
        dst.SingleScalar = SingleScalar;
        dst.SingleRow = SingleRow;
        dst.DocumentMode = DocumentMode;
        dst.IsDistinct = IsDistinct;
        dst.GroupingType = GroupingType;
        dst.GroupingSets = GroupingSets;
        dst.GroupByWithTotals = GroupByWithTotals;
        // The inferred SelectGroup state is derived at preparation, but `_isPrepared` travels with the
        dst.LimitBy = LimitBy;
        dst.DistinctOn = DistinctOn;
        dst.ExtremeRow = ExtremeRow;
        // #368: component state is stored flat; share the immutable component/prepared lists and views,
        // and drop the render-scoped shape on the clone (it belongs to the source instance).
        dst._components = _components;
        dst._componentsView = _componentsView;
        // The effective state the component contributions produced during preparation. The clone
        // constructor already copies it; a staged raw apply (ApplyPreparationFrom) targets a fresh
        // command, so it must arrive through here or the hit cannot re-extract captured parameters from
        // the component's replacement HAVING/grouping.
        dst.PerParentPaging = PerParentPaging;
        dst.PerParentPagingEmbedded = PerParentPagingEmbedded;
        dst.PerParentPartition = PerParentPartition;
        dst.TableSample = TableSample;
        dst.Temporal = Temporal;
        dst.RowLock = RowLock;
        dst.SourceLockClause = SourceLockClause;
        dst.Final = Final;
        dst.SampleRatio = SampleRatio;
        dst.SampleOffset = SampleOffset;
        dst.Settings = Settings;
        dst.NavigationPaths = NavigationPaths;
        dst.JunctionChildFilter = JunctionChildFilter;
        dst.NavigationChain = NavigationChain;
        dst.IsWideNavigationCount = IsWideNavigationCount;
        dst._preWhere = _preWhere;
        dst._arrayJoins = _arrayJoins;
        dst._windows = _windows;
        dst.ArrayJoinKind = ArrayJoinKind;
        dst.BindArrayJoinElement = BindArrayJoinElement;
        dst.TableHints = TableHints;
        dst.IndexHints = IndexHints;
        dst.IndexHintKind = IndexHintKind;
        dst.TablesInScopeHints = TablesInScopeHints;
        dst.ForJsonClause = ForJsonClause;
        dst.ForXmlClause = ForXmlClause;

        // The six lazily-created plan comparers are deliberately not copied: a comparer resolves
        // ReferencedQueries through the command it was constructed with, so sharing one between command
        // instances lets a reset/"discarded preparation" command hash through a foreign registry whose
        // ReferencedQueries is null (DEF-T327-05 NullReferenceException in VisitIndex). Every command
        // creates its own from the lazy getters.
        dst._unionType = _unionType;

        if (_preparedState is { } preparedReferences && preparedReferences.ReferencedQueries is not null)
            dst.Prepared.ReferencedQueries = preparedReferences.ReferencedQueries;

        if (copyAll)
        {
            dst._customData = _customData;
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
                // The typed-projection flag and the recursive reference travel with the declaration: a plan
                // served from cache must keep the typed aliasing and the recursive shape validation.
                dst._ctes = _ctes.Select(c => c.Mutation is not null
                    ? new CteDefinition(c.Name, c.Query.CloneForCache(), c.Mutation)
                    : new CteDefinition(c.Name, c.Query.CloneForCache(), c.Recursive, c.MaxRecursion)
                    {
                        TypedProjection = c.TypedProjection,
                        RecursiveReference = c.RecursiveReference,
                    }).ToList();
            }

            if (Prep?.ReferencedQueries is { Count: > 0 } referencedQueries)
            {
                dst.Prepared.ReferencedQueries = referencedQueries.Select(it => it.CloneForCache()).ToList();
            }
        }
    }

    /// <summary>Creates an empty command of the same concrete type for a live clone.</summary>
    /// <returns>A new command bound to this command's data context.</returns>
    protected virtual QueryCommand CreateSelf()
    {
        // Copy-constructor path: transfers the shape field by field, so no QueryDefinition record is
        // allocated on the per-step clone path.
        return new QueryCommand(_dataContext, this, forCache: false);
    }
    /// <summary>Creates an empty command of the same type for a plan-cache key, detached from the data context.</summary>
    /// <returns>A new command with the mutable expressions cleared for later copying.</returns>
    protected virtual QueryCommand CreateSelfForClone()
    {
        // The cache clone deep-clones the joins and clears the raw shape, again without a shape record.
        return new QueryCommand(null, this, forCache: true);
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
        clone._outerRefBindings = null;
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
