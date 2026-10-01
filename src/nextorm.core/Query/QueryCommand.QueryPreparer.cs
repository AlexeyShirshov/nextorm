using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

public partial class QueryCommand
{
    /// <summary>
    /// The build pipeline of a <see cref="QueryCommand"/>: turns the configured expression tree into the
    /// prepared pieces (<c>from</c>, joins, columns, <c>where</c>, grouping, sorting, CTEs and set
    /// operations) plus the per-part plan hashes that describe them. <see cref="PrepareCommand(CancellationToken)"/> is the
    /// only public entry point; everything here is an implementation detail of the command.
    /// </summary>
    /// <remarks>
    /// Nested on purpose. The pipeline reads and writes the command's private preparation state
    /// (<see cref="PreparedCondition"/>, <see cref="InValuesShapeHash"/>, the per-part plan hashes, ...)
    /// as well as its <c>protected</c> members. A top-level helper would force those <c>protected</c>
    /// members to <c>internal</c> — a breaking change for external subclasses of <see cref="QueryCommand"/> —
    /// so the pipeline is nested instead: same access, without enlarging the command's own method surface.
    /// </remarks>
    internal static class QueryPreparer
    {
        internal static void Prepare(QueryCommand cmd, bool dontCalculateHash, CancellationToken cancellationToken)
        {
            if (cmd._dataContext is null) throw new InvalidOperationException("Cannot prepare command in cache");
            cmd.InvalidatePlanKey();
            cmd.ResolvedQuoteIdentifiers = cmd.QuoteIdentifiers ?? cmd._dataContext.QuoteIdentifiers;
            cmd.ResolvedNamingConvention = cmd.NamingConvention ?? cmd._dataContext.NamingConvention;
            cmd.ResolvedKeywordCase = cmd.KeywordCase ?? cmd._dataContext.KeywordCase;
            cmd.ResolvedCommandTimeout = cmd.CommandTimeout is int commandTimeout && commandTimeout > 0
                ? commandTimeout
                : cmd._dataContext.CommandTimeout is int contextTimeout && contextTimeout > 0 ? contextTimeout : null;

            // The shape of captured collections (value lists, dictionary lookups) is folded into the plan
            // key only when the command participates in the plan cache; otherwise a lookup may be rendered
            // without a shape entry, so it must be evaluated on the fly instead of reusing a stale form.
            cmd.ShapeScanned = !dontCalculateHash && !cmd._dontCache;
            cmd.LookupPartitions = null;

            // Raw-source filter skips are collected for this preparation only (main source + every join),
            // then emitted once by the planner on the cache miss. A re-preparation starts from a clean list
            // so a sibling join's or a previous run's skips can never leak into the emitted set.
            cmd.PendingRawSourceFilterSkips = null;
            // CROSS/APPLY bound-join filters are deferred to the main-source pass; a re-preparation must
            // not replay a previous run's deferred filters.
            cmd.PendingCrossJoinFilters = null;
#if DEBUG
            if (cmd.Logger?.IsEnabled(LogLevel.Debug) ?? false) cmd.Logger.LogDebug("Preparing command");
#endif
            cmd.OneColumn = false;

            var srcType = cmd._srcType;
            if (srcType is null)
            {
                if (cmd._exp is null)
                    throw new QueryPreparationException("Lambda expression for anonymous type must exists");

                srcType = cmd._exp.Parameters[0].Type;
            }

            FromExpression? from = cmd._from ?? cmd._dataContext.GetFrom(srcType, cmd);
            PrepareFrom(from, dontCalculateHash, cancellationToken, cmd);
            var joinPlanHash = PrepareJoin(cmd, dontCalculateHash, cancellationToken);
            var (selectList, columnsPlanHash) = PrepareColumns(cmd, dontCalculateHash, srcType, cancellationToken);
            var wherePlanHash = PrepareWhere(cmd, InjectMainSourceFilters(cmd, srcType), dontCalculateHash, cancellationToken);
            PreparePreWhere(cmd, dontCalculateHash, cancellationToken);
            PrepareArrayJoin(cmd, cancellationToken);

            var (groupingList, groupingPlanHash) = PrepareGrouping(cmd, dontCalculateHash, cancellationToken);
            var limitByColumns = PrepareLimitBy(cmd, cancellationToken);
            var distinctOnColumns = PrepareDistinctOn(cmd, cancellationToken);
            var (extremeRowColumns, extremeRowGroupByColumns) = PrepareExtremeRow(cmd, cancellationToken);
            PrepareWindows(cmd, dontCalculateHash);
            var sortingPlanHash = PrepareSorting(cmd, selectList, dontCalculateHash, cancellationToken);

            cmd._union?.PrepareCommand(dontCalculateHash, cancellationToken);
            if (cmd._union is { } union)
                TransferNestedFilterSkips(union, cmd);
            PrepareCtes(cmd, dontCalculateHash, cancellationToken);
            PrepareHints(cmd, dontCalculateHash);

            cmd._isPrepared = true;
            cmd._selectList = selectList ?? [];
            cmd._groupingList = groupingList ?? [];
            cmd._limitByColumns = limitByColumns ?? [];
            cmd._distinctOnColumns = distinctOnColumns ?? [];
            cmd._extremeRowColumns = extremeRowColumns ?? [];
            cmd._extremeRowGroupByColumns = extremeRowGroupByColumns ?? [];
            cmd._srcType = srcType;
            cmd._from = from;

            cmd.ColumnsPlanHash = columnsPlanHash == 7 ? 0 : columnsPlanHash;
            cmd.JoinPlanHash = joinPlanHash == 7 ? 0 : joinPlanHash;
            cmd.SortingPlanHash = sortingPlanHash == 7 ? 0 : sortingPlanHash;
            cmd.WherePlanHash = wherePlanHash == 7 ? 0 : wherePlanHash;
            cmd.GroupingPlanHash = groupingPlanHash == 7 ? 0 : groupingPlanHash;
            cmd.FromPlanHash = dontCalculateHash ? 0 : cmd.GetFromExpressionPlanEqualityComparer().GetHashCode(from);

            if (!dontCalculateHash && cmd._union is not null)
            {
                unchecked
                {
                    var h = 7 * 13 + ((int)cmd._unionType);
                    // Hash the right-hand command itself, not the comparer instance. Calling the
                    // parameterless GetHashCode() resolved to object.GetHashCode(), i.e. the comparer's
                    // identity, so a freshly built command chain produced a different hash than the
                    // cached plan and every set operation missed the plan cache.
                    h = h * 13 + cmd.GetQueryPlanEqualityComparer().GetHashCode(cmd._union);
                    cmd.UnionPlanHash = h;
                }
            }

            if (!dontCalculateHash)
            {
                if (cmd._referencedQueries?.Count == 1)
                {
                    cmd.ReferencedQueriesPlanHash = cmd.GetQueryPlanEqualityComparer().GetHashCode(cmd._referencedQueries[0]);
                }
                else if (cmd._referencedQueries?.Count > 1)
                {
                    XxHash32 hash = new();
                    unchecked
                    {
                        for (var (i, cnt) = (0, cmd._referencedQueries.Count); i < cnt; i++)
                        {
                            var item = cmd._referencedQueries[i];
                            hash.Add(item, cmd.GetQueryPlanEqualityComparer());
                        }
                        cmd.ReferencedQueriesPlanHash = hash.ToHashCode();
                    }
                }
            }
        }

        /// <summary>
        /// Re-evaluates the captured-collection shape (value lists and dictionary lookups) of an
        /// already-prepared command. An implicit-cache command can be reused with a collection that was
        /// grown or reassigned between executions, so the plan key must follow the current shape or a
        /// stale (wrong parameter count) plan would be reused. When the command was first prepared
        /// without hashing, the shape is folded in here for the first time.
        /// </summary>
        internal static void RefreshInValuesShape(QueryCommand cmd)
        {
            if (!cmd.Cache || (cmd.PreparedCondition is null && cmd.PreparedPreWhere is null))
                return;

            if (!cmd.ShapeScanned)
            {
                // The command was first prepared without hashing (storeInCache:false) and is now reused
                // through the cache, so its shape was never folded in. Do it now, once, before the normal
                // refresh path: otherwise a captured-collection lookup inside the condition would not be
                // in LookupPartitions and would be refused as sitting outside the condition.
                cmd.LookupPartitions = null;
                Dictionary<Expression, InValuesPartition>? initial = null;

                if (cmd.PreparedCondition is not null)
                {
                    cmd.InValuesShapeHash = InValues.ComputeShapeHash(cmd.PreparedCondition, cmd, out var wherePartitions, out var hasWhere);
                    cmd.HasTopLevelInValues = hasWhere;
                    initial = wherePartitions;
                    unchecked
                    {
                        cmd.WherePlanHash = cmd._whereBasePlanHash * 13 + cmd.InValuesShapeHash;
                    }
                }

                if (cmd.PreparedPreWhere is not null)
                {
                    cmd.PreWhereShapeHash = InValues.ComputeShapeHash(cmd.PreparedPreWhere, cmd, out var preWherePartitions, out var hasPreWhere);
                    cmd.HasPreWhereInValues = hasPreWhere;

                    if (preWherePartitions is { Count: > 0 })
                    {
                        initial ??= new Dictionary<Expression, InValuesPartition>(ReferenceEqualityComparer.Instance);

                        foreach (var (key, value) in preWherePartitions)
                            initial[key] = value;
                    }
                }

                cmd.InValuesPartitions = initial;
                cmd.ShapeScanned = true;
                return;
            }

            // The shapes are recomputed from scratch: a captured collection or dictionary may have been
            // grown or reassigned between executions of the cached plan.
            cmd.LookupPartitions = null;
            Dictionary<Expression, InValuesPartition>? merged = null;

            if (cmd.HasTopLevelInValues && cmd.PreparedCondition is not null)
            {
                cmd.InValuesShapeHash = InValues.ComputeShapeHash(cmd.PreparedCondition, cmd, out var wherePartitions, out _);
                merged = wherePartitions;
                unchecked
                {
                    cmd.WherePlanHash = cmd._whereBasePlanHash * 13 + cmd.InValuesShapeHash;
                }
            }

            if (cmd.HasPreWhereInValues && cmd.PreparedPreWhere is not null)
            {
                cmd.PreWhereShapeHash = InValues.ComputeShapeHash(cmd.PreparedPreWhere, cmd, out var preWherePartitions, out _);

                if (preWherePartitions is { Count: > 0 })
                {
                    merged ??= new Dictionary<Expression, InValuesPartition>(ReferenceEqualityComparer.Instance);

                    foreach (var (key, value) in preWherePartitions)
                        merged[key] = value;
                }
            }

            cmd.InValuesPartitions = merged;
        }

        private static void PrepareFrom(FromExpression? from, bool dontCalculateHash, CancellationToken cancellationToken, QueryCommand owner)
        {
            if (from?.SubQuery is not null)
            {
                if (!from.SubQuery._isPrepared)
                    from.SubQuery.PrepareCommand(dontCalculateHash, cancellationToken);

                TransferNestedFilterSkips(from.SubQuery, owner);
            }

            if (from?.Pivot is not null)
                PrepareFrom(from.Pivot.Inner, dontCalculateHash, cancellationToken, owner);
        }

        // Moves the raw-source filter skips collected while preparing a nested command (a derived table,
        // CTE body, set-operation branch or correlated applied source) into the owning preparation, so the
        // planner emits them on the owner's cache miss. The child list is drained so a later or shared
        // preparation can never replay the warning (no duplication on a cache hit).
        private static void TransferNestedFilterSkips(QueryCommand child, QueryCommand owner)
        {
            if (ReferenceEquals(child, owner) || child.PendingRawSourceFilterSkips is not { Count: > 0 } childSkips)
                return;

            (owner.PendingRawSourceFilterSkips ??= []).AddRange(childSkips);
            child.PendingRawSourceFilterSkips = null;
        }

        private static void PrepareCtes(QueryCommand cmd, bool noHash, CancellationToken cancellationToken)
        {
            if (cmd._ctes is { Count: > 0 })
            {
                // A CTE body may itself carry a WITH (a query built as With(...).From(...) passed as the
                // body of another With). Flatten the declaration tree into one ordered list before the
                // plan hash is taken and before the SQL pass, so every provider sees a single top-level
                // WITH (SQL Server rejects WITH inside a derived table). Definitions are shared, not
                // mutated.
                cmd._ctes = CteHoister.Hoist(cmd._ctes);

                for (var (i, cnt) = (0, cmd._ctes!.Count); i < cnt; i++)
                {
                    var cte = cmd._ctes[i];
                    if (!cte.Query.IsPrepared)
                        cte.Query.PrepareCommand(noHash, cancellationToken);
                    TransferNestedFilterSkips(cte.Query, cmd);

                    // An INSERT ... SELECT used as a data-modifying CTE body renders its source select
                    // in the enclosing statement, so the source must be prepared like any other command.
                    if (cte.Mutation is { Source: { } mutationSource })
                    {
                        if (!mutationSource.IsPrepared)
                            mutationSource.PrepareCommand(noHash, cancellationToken);
                        TransferNestedFilterSkips(mutationSource, cmd);
                    }

                    // A data-modifying CTE is a side-effecting statement: never share its plan, because
                    // the mutation's shape (row count, target columns) is not fully captured by the CTE
                    // query. The planner detects the mutation through QueryCommand.HasDataModifyingCte
                    // and bypasses the plan cache call-locally (storeInCache := false) rather than
                    // mutating the command's sticky Cache flag here, which would leak to every later
                    // query on a shared context command.
                }
            }

            // A declaration nested anywhere else (a derived-table subquery, a correlated reference or a
            // set-operation branch) cannot be hoisted into the top-level WITH; fail before emitting the
            // non-portable nested form instead of rendering invalid SQL. Commands without declarations
            // are checked too, so `From(<query that carries a WITH>)` fails fast rather than nesting.
            CteHoister.EnsureNoUnhoistedCtes(cmd, cmd._ctes ?? Array.Empty<CteDefinition>());

            if (cmd._ctes is not { Count: > 0 } || cmd._dontCache || noHash)
                return;

            XxHash32 hash = new();
            unchecked
            {
                for (var (i, cnt) = (0, cmd._ctes.Count); i < cnt; i++)
                {
                    var cte = cmd._ctes[i];
                    hash.Add(cte.Name);
                    hash.Add(cte.Recursive);
                    if (cte.MaxRecursion is int maxRecursion)
                        hash.Add(maxRecursion);
                    // Hash the body without its own nested declarations: after the hoist above they are
                    // already represented as separate top-level entries, and the equivalent flat
                    // With(...).With(...) chain carries no nested list on the body. Including them here
                    // would double-count and give the nested and flat forms different plan keys.
                    hash.Add(cmd.GetQueryPlanEqualityComparer().GetCteBodyHashCode(cte.Query));
                }
                cmd.CtesPlanHash = hash.ToHashCode();
            }
        }

        /// <summary>
        /// Folds the statement-level query hints into the plan key. Hints change the emitted SQL, so a
        /// command with hints must not share a cached plan with an otherwise identical command without
        /// them (or with different ones).
        /// </summary>
        private static void PrepareHints(QueryCommand cmd, bool noHash)
        {
            if (cmd._hints is not { Count: > 0 } hints) return;
            if (cmd._dontCache || noHash) return;

            XxHash32 hash = new();
            unchecked
            {
                for (var (i, cnt) = (0, hints.Count); i < cnt; i++)
                    hash.Add(hints[i]);

                cmd.HintsPlanHash = hash.ToHashCode();
            }
        }

        private static readonly MethodInfo CountAllMI = typeof(CommonFunctions).GetMethod(nameof(CommonFunctions.count), [typeof(object[])])!;
    private static readonly MethodInfo AbsMI = typeof(Math).GetMethod(nameof(Math.Abs), [typeof(long)])!;

    /// <summary>
    /// Wraps a scalar-subquery projection in a
    /// <c>case when count(*) &gt; 1 then &lt;numeric overflow&gt; else value end</c> guard so a dialect
    /// without scalar-subquery cardinality enforcement (SQLite) raises a database error on a second row
    /// instead of silently returning the first. Non-numeric projections are left untouched; the dialect
    /// rejects those (<see cref="QueryCommand.IsCardinalityGuardable"/>).
    /// </summary>
    private static Expression WrapSingleScalarCardinalityGuard(Expression expression)
    {
        if (expression is not LambdaExpression lambda || !QueryCommand.IsCardinalityGuardable(lambda.Body.Type))
            return expression;

        var count = Expression.Call(CommonFunctions.SQLExpression, CountAllMI, Expression.NewArrayInit(typeof(object)));
        var moreThanOne = Expression.GreaterThan(count, Expression.Constant(1));
        var overflow = Expression.Call(AbsMI, Expression.Constant(long.MinValue));
        var body = Expression.Condition(moreThanOne, Expression.Convert(overflow, lambda.Body.Type), lambda.Body);
        return Expression.Lambda(body, lambda.Parameters);
    }

    private static (SelectExpression[]?, int) PrepareColumns(QueryCommand cmd, bool noHash, Type? srcType, CancellationToken cancellationToken)
        {
            var selectList = cmd._selectList;
            int columnsPlanHash = 7;
            if (selectList is null && !cmd.IgnoreColumns)
            {
                if (cmd._exp is not null)
                {
                    // Resolve the projection source metadata once: it is consulted for every projected
                    // column (duration unit, converter), so the shared lookup is not repeated per column.
                    IEntityMetadata? srcMetadata = srcType is not null && DataContextCache.Metadata.TryGetValue(srcType, out var resolvedSource)
                        ? resolvedSource
                        : null;

                    // A bare member projection of a value-converted property is a single column whose
                    // converter has to reach materialization; computed once so the scalar branch and its
                    // condition do not each scan the entity metadata.
                    var bodyConverter = cmd._exp.Body is MemberExpression ? ResolveConverter(srcMetadata, cmd._exp.Body) : null;

                    // A streaming named-column accessor projects one LOB column even though its CLR type
                    // (Stream/TextReader) is not one of the scalar types in IsSingleColumnProjection.
                    var bodyIsStreaming = TableAliasAccessors.IsStreaming(cmd._exp.Body);

                    if (cmd._exp.Body is NewExpression ctor && !TypeFacts.IsSingleColumnProjection(ctor.Type))
                    {
                        var args = ctor.Arguments;
                        var argsCount = args.Count;

                        var expanded = new List<SelectExpression>(argsCount);

                        var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(cmd._dataContext!, cmd, cancellationToken, cmd._dataContext!.Logger);
                        using var outerScope = innerQueryVisitor.PushOuter(cmd._exp.Parameters[0]);
                        for (var idx = 0; idx < argsCount; idx++)
                        {
                            if (cancellationToken.IsCancellationRequested)
                                return (expanded.ToArray(), columnsPlanHash);

                            var arg = args[idx];
                            var ctorParam = ctor.Constructor!.GetParameters()[idx];

                            // An entity-typed projection item (for example Right = p.Item2) is expanded
                            // into its mapped scalar columns so the SQL row mapper can read them; the
                            // materializer rebuilds the entity from the group (and produces null when
                            // every column is SQL NULL, i.e. the missing side of an outer join).
                            if (cmd._dataContext!.NeedMapping
                                && TryExpandEntityItem(ctorParam.ParameterType, arg, idx, null, innerQueryVisitor, expanded, cancellationToken))
                                continue;

                            var (durationUnit, durationPrecision) = ResolveDuration(srcMetadata, arg);
                            var converter = ResolveConverter(srcMetadata, arg);

                            var selExp = new SelectExpression(ctorParam.ParameterType)
                            {
                                Index = expanded.Count,
                                PropertyName = ctorParam.Name!,
                                Expression = innerQueryVisitor.Visit(arg),
                                DurationUnit = durationUnit,
                                DurationPrecision = durationPrecision,
                                ProviderType = converter?.ProviderType,
                                Converter = converter,
                                IsLobStreaming = TableAliasAccessors.IsStreaming(arg),
                            };
                            selExp.DefaultOnNull = !selExp.Nullable && CorrelatedQueryExpressionVisitor.IsOrDefaultScalar(arg);
                            expanded.Add(selExp);
                        }

                        selectList = expanded.ToArray();
                        for (var i = 0; i < selectList.Length; i++)
                            selectList[i].Index = i;

                        if (!cmd._dontCache && !noHash) unchecked
                        {
                            for (var i = 0; i < selectList.Length; i++)
                            {
                                selectList[i].PlanHashCode = cmd.GetSelectExpressionPlanEqualityComparer().GetHashCode(selectList[i]);
                                columnsPlanHash = columnsPlanHash * 13 + selectList[i].PlanHashCode;
                            }
                        }
                    }
                    else if (TypeFacts.IsSingleColumnProjection(cmd._exp.Body.Type)
                        || bodyIsStreaming
                        || (cmd._exp.Body is not NewExpression && TypeFacts.IsTupleType(cmd._exp.Body.Type))
                        || bodyConverter is not null)
                    {

                        cmd.OneColumn = true;
                        var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(cmd._dataContext!, cmd, cancellationToken, cmd._dataContext!.Logger);
                        var selectExp = innerQueryVisitor.Visit(cmd._exp);
                        var (scalarDurationUnit, scalarDurationPrecision) = ResolveDuration(srcMetadata, cmd._exp.Body);

                        var selExp = new SelectExpression(cmd._exp.Body.Type)
                        {
                            Expression = selectExp,
                            DurationUnit = scalarDurationUnit,
                            DurationPrecision = scalarDurationPrecision,
                            ProviderType = bodyConverter?.ProviderType,
                            Converter = bodyConverter,
                            IsLobStreaming = bodyIsStreaming,
                        };
                        // A dialect that does not enforce scalar-subquery cardinality (SQLite) would
                        // silently return the first row for Single/SingleOrDefault, so wrap the
                        // projection in a guard that raises a database error on a second row.
                        if (cmd.SingleScalar
                            && (cmd._dataContext as DataContext)?.Dialect is { EnforcesScalarSubqueryCardinality: false })
                            selExp.Expression = WrapSingleScalarCardinalityGuard(selExp.Expression);
                        selExp.DefaultOnNull = !selExp.Nullable && CorrelatedQueryExpressionVisitor.IsOrDefaultScalar(cmd._exp.Body);
                        if (!cmd._dontCache && !noHash)
                            selExp.PlanHashCode = cmd.GetSelectExpressionPlanEqualityComparer().GetHashCode(selExp);

                        if (!cmd._dontCache && !noHash) unchecked
                            {
                                columnsPlanHash = columnsPlanHash * 13 + selExp.PlanHashCode;
                            }

                        selectList = [selExp];
                    }
                    else if (cmd._exp.Body is MemberInitExpression init)
                    {
                        var bindings = init.Bindings;
                        var bindingsCount = bindings.Count;

                        selectList = new SelectExpression[bindingsCount];

                        var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(cmd._dataContext!, cmd, cancellationToken, cmd._dataContext!.Logger);
                        using var outerScope = innerQueryVisitor.PushOuter(cmd._exp.Parameters[0]);
                        for (var idx = 0; idx < bindingsCount; idx++)
                        {
                            if (cancellationToken.IsCancellationRequested)
                                return (selectList, columnsPlanHash);

                            var binding = bindings[idx] as MemberAssignment;
                            var (bindingDurationUnit, bindingDurationPrecision) = ResolveDuration(srcMetadata, binding!.Expression);
                            var bindingConverter = ResolveConverter(srcMetadata, binding.Expression);

                            var selExp = new SelectExpression(((PropertyInfo)binding!.Member).PropertyType)
                            {
                                Index = idx,
                                PropertyName = binding.Member.Name!,
                                Expression = innerQueryVisitor.Visit(binding.Expression),
                                DurationUnit = bindingDurationUnit,
                                DurationPrecision = bindingDurationPrecision,
                                ProviderType = bindingConverter?.ProviderType,
                                Converter = bindingConverter,
                                IsLobStreaming = TableAliasAccessors.IsStreaming(binding.Expression),
                            };
                            selExp.DefaultOnNull = !selExp.Nullable && CorrelatedQueryExpressionVisitor.IsOrDefaultScalar(binding.Expression);
                            if (!cmd._dontCache && !noHash)
                                selExp.PlanHashCode = cmd.GetSelectExpressionPlanEqualityComparer().GetHashCode(selExp);
                            selectList[idx] = selExp;

                            if (!cmd._dontCache && !noHash) unchecked
                                {

                                    columnsPlanHash = columnsPlanHash * 13 + selExp.PlanHashCode;
                                }
                        }


                    }
                }
                else
                {
                    if (srcType is null)
                        throw new QueryPreparationException("Lambda expression or source type must exists");

                    if (cmd._dataContext!.NeedMapping
                        && (cmd.ProjectionType ?? srcType).IsAssignableTo(typeof(IProjection))
                        && TryBuildProjectionSelectList(cmd, noHash, cmd.ProjectionType ?? srcType, cancellationToken, out var projectionColumns))
                    {
                        selectList = projectionColumns;
                        if (!cmd._dontCache && !noHash)
                            for (var i = 0; i < projectionColumns.Length; i++) unchecked
                            {
                                columnsPlanHash = columnsPlanHash * 13 + projectionColumns[i].PlanHashCode;
                            }
                    }
                    else if (cmd._dataContext!.NeedMapping)
                    {
                        DataContextCache.Metadata.TryGetValue(srcType, out var entityMeta);
                        if (entityMeta is not null
                            && entityMeta.DynamicColumnsStore is not null
                            && cmd.Joins is { Length: > 0 })
                            throw new NotSupportedException("A dynamic-columns store is only supported for a query over a single physical source; the query has joins.");

                        if (/*!CacheList || */!DataContextCache.SelectListCache.TryGetValue(srcType, out selectList))
                        {
                            if (entityMeta is not null)
                            {
                                // The projection is built by the shared helper (also used by raw-command
                                // mapping); the per-column plan hash is folded in here so the cached column
                                // shape stays identical to the previous inline build.
                                var (columns, completed) = EntitySelectListBuilder.Build(srcType, entityMeta, cancellationToken);
                                if (!completed)
                                    return (columns, columnsPlanHash);

                                selectList = columns;

                                if (entityMeta.DynamicColumnsStore is { } dynamicStore)
                                {
                                    var withStore = new SelectExpression[selectList.Length + 1];
                                    selectList.CopyTo(withStore, 0);
                                    withStore[selectList.Length] = new SelectExpression(typeof(Dictionary<string, object?>))
                                    {
                                        Index = selectList.Length,
                                        PropertyName = dynamicStore.PropertyInfo.Name,
                                        PropertyInfo = dynamicStore.PropertyInfo,
                                        IsDynamicColumnsStore = true,
                                    };
                                    selectList = withStore;
                                }

                                if (!cmd._dontCache && !noHash)
                                    for (var i = 0; i < selectList.Length; i++) unchecked
                                        {
                                            selectList[i].PlanHashCode = cmd.GetSelectExpressionPlanEqualityComparer().GetHashCode(selectList[i]);
                                            columnsPlanHash = columnsPlanHash * 13 + selectList[i].PlanHashCode;
                                        }
                            }

                            if (!(selectList?.Length > 0))
                                throw new QueryPreparationException("Select must return new anonymous type");

                            DataContextCache.SelectListCache[srcType] = selectList;
                        }
                        else if (!cmd._dontCache && !noHash)
                        {
                            for (var (i, cnt) = (0, selectList.Length); i < cnt; i++) unchecked
                                {
                                    var selExp = selectList[i];

                                    columnsPlanHash = columnsPlanHash * 13 + selExp.PlanHashCode;
                                }
                        }
                    }
                }
            }

            return (selectList, columnsPlanHash);
        }

        /// <summary>
        /// Builds the projection of a bare join command whose result type is an <see cref="IProjection"/>
        /// (a <c>JoinedEntityBuilder</c> used without <c>Select</c>): every entity-typed item is expanded
        /// into its mapped scalar columns, tagged with the item it belongs to so the row materializer can
        /// rebuild the entities. Returns <see langword="false"/> when the source is not such a projection,
        /// leaving the caller's existing behaviour untouched.
        /// </summary>
        private static bool TryBuildProjectionSelectList(QueryCommand cmd, bool noHash, Type srcType, CancellationToken cancellationToken, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out SelectExpression[]? selectList)
        {
            selectList = null;
            if (cmd._joins is not { Length: > 0 } || !srcType.IsAssignableTo(typeof(IProjection)))
                return false;

            var itemTypes = srcType.GetGenericArguments();
            var projectionParam = Expression.Parameter(srcType);
            var output = new List<SelectExpression>(itemTypes.Length);

            for (var i = 0; i < itemTypes.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;

                var memberName = $"Item{i + 1}";
                var itemExpression = (Expression)Expression.Property(projectionParam, memberName);

                // The first item of a JoinInto pair command is the query's own source: it renders
                // against the main table alias, so it must not be re-rooted onto the projection
                // parameter (that would drop the alias). Later items re-root onto their ItemN.
                var reRoot = !(i == 0 && cmd.ProjectionType is not null && itemTypes[i] == cmd._srcType);

                // Every item must be a mapped entity; a scalar item cannot be expanded from an entity
                // source and is left to the caller's existing (rejecting) path rather than materialized
                // as a wrong shape.
                if (!TryExpandEntityItem(itemTypes[i], itemExpression, i, srcType.GetProperty(memberName), null, output, cancellationToken, reRoot))
                    return false;
            }

            var columns = output.ToArray();
            for (var i = 0; i < columns.Length; i++)
            {
                columns[i].Index = i;
                if (!cmd._dontCache && !noHash)
                    columns[i].PlanHashCode = cmd.GetSelectExpressionPlanEqualityComparer().GetHashCode(columns[i]);
            }

            selectList = columns;
            return true;
        }

        /// <summary>
        /// Expands one entity-typed projection item into its mapped scalar <see cref="SelectExpression"/>s.
        /// Each column is re-rooted onto <paramref name="itemExpression"/> (so it renders against the
        /// item's table alias) and tagged with a shared <see cref="ProjectionEntityItem"/>. Returns
        /// <see langword="false"/> when <paramref name="itemType"/> is not a mapped entity.
        /// </summary>
        private static bool TryExpandEntityItem(
            Type itemType,
            Expression itemExpression,
            int slot,
            PropertyInfo? member,
            CorrelatedQueryExpressionVisitor? visitor,
            List<SelectExpression> output,
            CancellationToken cancellationToken,
            bool reRoot = true)
        {
            if (!DataContextCache.Metadata.TryGetValue(itemType, out var metadata) || metadata.Properties.Count == 0)
                return false;

            var (columns, completed) = EntitySelectListBuilder.Build(itemType, metadata, cancellationToken);
            if (!completed)
                return false;

            var group = new ProjectionEntityItem(slot, itemType, member);

            for (var i = 0; i < columns.Length; i++)
            {
                var column = columns[i];

                if (reRoot && column.Expression is LambdaExpression { Parameters.Count: 1 } lambda)
                    column.Expression = new ReplaceParameterInstanceVisitor(lambda.Parameters[0], itemExpression).Visit(lambda.Body);

                if (visitor is not null)
                    column.Expression = visitor.Visit(column.Expression!);

                column.ProjectionItem = group;
                output.Add(column);
            }

            return true;
        }

        // Resolves the declared duration storage unit of a directly projected entity property, so a
        // projection of a duration column (`Select(x => x.Dur)`) reads it back in the declared unit on
        // a provider without a native duration type. A non-duration or computed expression stays null.
        private static (DurationUnit? Unit, int Precision) ResolveDuration(IEntityMetadata? srcMetadata, Expression expression)
        {
            if (srcMetadata is not null
                && expression is MemberExpression { Member: PropertyInfo pi }
                && MemberTranslator.FindProperty(srcMetadata, pi) is { } property)
                return (property.DurationUnit, property.DurationPrecision);

            return (null, 0);
        }

        // Resolves the value converter of a directly projected entity property, so a scalar/anonymous
        // projection of a converted property carries the converter into materialization (the reader then
        // reads the provider representation and converts it back). A non-member or unmapped expression
        // stays null.
        private static IPropertyValueConverter? ResolveConverter(IEntityMetadata? srcMetadata, Expression expression)
        {
            expression = TypeFacts.UnwrapConvert(expression);
            if (expression is MemberExpression { Member: PropertyInfo pi })
                return MemberTranslator.ResolveProperty(pi, srcMetadata)?.Converter;

            return null;
        }

        private static int PrepareJoin(QueryCommand cmd, bool noHash, CancellationToken cancellationToken)
        {
            int joinPlanHash = 7;
            if (cmd._joins is not null)
            {

                for (var (idx, cnt) = (0, cmd._joins.Length); idx < cnt; idx++)
                {
                    // Copy-on-write: builders share JoinExpression elements by reference across
                    // clones, so preparation must mutate only this command's own copy. The copy is
                    // based on the pristine original condition before any filter injection, so a
                    // sibling's already-injected filters can never leak in (a plain join whose scope
                    // disables every filter injects nothing and must stay unfiltered). The _joins array
                    // itself is per-command, so replacing the element is local to this command.
                    var join = cmd._joins[idx] = cmd._joins[idx].CloneForPreparation();

                    if (join.ApplySource is { } applySource)
                    {
                        // A correlated CROSS/OUTER APPLY source: build the derived query with the
                        // left-hand row in scope, so its column references become outer references of
                        // this command; the built command is installed as the join source and rendered
                        // like any other derived table by the apply clause.
                        var applyVisitor = new CorrelatedQueryExpressionVisitor(cmd._dataContext!, cmd, cancellationToken, cmd._dataContext!.Logger);
                        using var outerScope = applyVisitor.PushOuter(applySource.Parameters[0]);

                        if (XmlNodesExpression.TryCreate(applySource, applyVisitor, out var xmlNodes))
                        {
                            // The xml.nodes() rowset is not a derived query: its source is the
                            // outer row's XML column. Register the row-shape metadata (as
                            // FromTableFunction does for its row type) and install the special
                            // FROM source; the apply clause renders it as <col>.nodes(...) as [alias]([col]).
                            cmd._dataContext!.From<SqlFunctions.IXmlNodesRow>();
                            join.SetFrom(new FromExpression(xmlNodes!));
                        }
                        else
                        {
                            var applyCommand = applyVisitor.BuildQueryCommand(applySource.Body);
                            if (!applyCommand.IsPrepared)
                                applyCommand.PrepareCommand(noHash, cancellationToken);
                            TransferNestedFilterSkips(applyCommand, cmd);
                            join.SetFrom(new FromExpression(applyCommand));
                        }
                    }
                    else
                    {
                        PrepareFrom(join.From, noHash, cancellationToken, cmd);
                    }

                    InjectJoinFilters(cmd, join, idx + 1);

                    if (!cmd._dontCache && !noHash) unchecked
                    {
                        joinPlanHash = joinPlanHash * 13 + cmd.GetJoinExpressionPlanEqualityComparer().GetHashCode(join);
                    }
                }
            }

            return joinPlanHash;
        }

        private static LambdaExpression? InjectMainSourceFilters(QueryCommand cmd, Type srcType)
        {
            // A joined query (a regular Join chain or a JoinInto pair) exposes the Projection<T1, …>
            // type as its lambda parameter while its physical main source is the first table. A filter
            // lookup keyed off the projection type would miss T1's filters, so resolve the main entity
            // type from the projection's first item and re-root the filter onto Item1.
            var isProjectionSource = srcType.IsAssignableTo(typeof(IProjection))
                && srcType.GetGenericArguments().Length > 0;
            var filterEntityType = isProjectionSource ? srcType.GetGenericArguments()[0] : srcType;

            // A direct raw/named source explicitly bound to an entity carries a caller-declared output
            // column shape; its filters are applied best-effort against that shape. An unbound raw source
            // has no binding (and resolves no filters), so its behavior is unchanged. A joined command
            // exposes the Projection<T1, …> type as its lambda parameter while the physical main source is
            // T1: the binding still belongs to that main source, so the bound path runs for a projection
            // source too and re-roots the retained predicates onto the projection's main alias (Item1).
            if (cmd._from?.SourceBinding is { } binding)
                return InjectBoundSourceFilters(cmd, srcType, binding, isProjectionSource);

            var filters = QueryFilterResolver.GetFilters(filterEntityType, cmd._filterScope, cmd._dataContext);
            var hasCrossJoinFilters = cmd.PendingCrossJoinFilters is { Count: > 0 };
            if (filters.Count == 0 && !hasCrossJoinFilters)
                return cmd._condition;

            var condition = cmd._condition;
            var parameter = condition is { Parameters.Count: >= 1 } typedCondition
                ? typedCondition.Parameters[0]
                : Expression.Parameter(srcType);
            Expression? body = condition?.Body;
            Expression filterTarget = isProjectionSource
                ? Expression.Property(parameter, "Item1")
                : parameter;

            for (var (i, cnt) = (0, filters.Count); i < cnt; i++)
            {
                if (!TryBuildFilterBody(filters[i], cmd._dataContext!, filterTarget, out var filterBody))
                    continue;

                body = body is null ? filterBody : Expression.AndAlso(body, filterBody);
            }

            body = AppendPendingCrossJoinFilters(cmd, parameter, body);

            return body is null ? null : Expression.Lambda(body, parameter);
        }

        // Applies the active filters to a raw/named source explicitly bound with BindEntity<TEntity>. The
        // caller declares the output columns, so each filter is first reduced to the column names it
        // depends on by walking its body (a filter-specific visitor, not a general SQL column extractor).
        // A filter whose dependencies cannot be determined, or whose columns are not all declared, is
        // skipped with a warning; the compatible filters stay applied in declaration order. Ignored/
        // disabled filters are already absent from the resolved list and do not warn.
        private static LambdaExpression? InjectBoundSourceFilters(QueryCommand cmd, Type srcType, FromExpression.EntityBinding binding, bool isProjectionSource)
        {
            var filters = QueryFilterResolver.GetFilters(binding.EntityType, cmd._filterScope, cmd._dataContext);
            if (filters.Count == 0)
                return cmd._condition;

            var condition = cmd._condition;
            var parameter = condition is { Parameters.Count: >= 1 } typedCondition
                ? typedCondition.Parameters[0]
                : Expression.Parameter(srcType);
            Expression? body = condition?.Body;

            // The filter is built against the main source's own binding/alias. In a joined command the
            // lambda parameter is the Projection<T1, …> type, so the main source alias is its Item1 —
            // never a join-side alias.
            Expression filterTarget = isProjectionSource
                ? Expression.Property(parameter, "Item1")
                : parameter;

            for (var (i, cnt) = (0, filters.Count); i < cnt; i++)
            {
                var filter = filters[i];

                // Resolve the filter to one lambda (invoking a builder-function filter exactly once) and
                // inspect that predicate; the same lambda is then merged, so the delegate never runs twice.
                if (!TryResolveFilterLambda(filter, cmd._dataContext!, out var filterLambda))
                    continue;

                if (!TryGetBoundFilterBody(cmd, binding, sourceOrdinal: 0, filter, filterLambda, filterTarget, out var filterBody))
                    continue;

                body = body is null ? filterBody : Expression.AndAlso(body, filterBody);
            }

            body = AppendPendingCrossJoinFilters(cmd, parameter, body);

            return body is null ? null : Expression.Lambda(body, parameter);
        }

        // Decides whether one resolved filter may be applied to a raw source bound with BindEntity<TEntity>
        // and, when accepted, builds its body against <paramref name="entityParameter"/> (the source's own
        // alias). Compatibility is evaluated from the predicate's proven column dependencies, never from the
        // mere emptiness of the declared shape: a constant (zero-column) predicate is applied even when the
        // caller declares no columns; a predicate with known dependencies is skipped as MissingColumns when
        // the declared shape is non-empty, and as UndeterminedColumns when the shape is empty (an empty
        // declaration cannot prove the dependency); an opaque/undetermined predicate is always skipped as
        // UndeterminedColumns. Each skip records its own warning entry.
        private static bool TryGetBoundFilterBody(
            QueryCommand cmd,
            FromExpression.EntityBinding binding,
            int sourceOrdinal,
            IQueryFilterMetadata filter,
            LambdaExpression filterLambda,
            Expression entityParameter,
            out Expression body)
        {
            body = null!;

            if (filterLambda.Parameters.Count == 0)
            {
                // A filter with no entity parameter is malformed; let the existing validation throw.
                _ = BuildFilterBody(filterLambda, cmd._dataContext!, entityParameter);
                return false;
            }

            var requirements = new FilterColumnDependencyVisitor(binding.EntityType, cmd.ResolvedNamingConvention, filterLambda.Parameters[0])
                .Analyze(filterLambda);
            if (requirements.Undetermined)
            {
                RecordRawSourceFilterSkip(cmd, binding, sourceOrdinal, filter.Key, "UndeterminedColumns", missingColumns: null);
                return false;
            }

            if (requirements.Columns.Count > 0)
            {
                if (binding.AvailableColumns.Count == 0)
                {
                    // A proven non-empty dependency set against an empty declared shape is undetermined,
                    // not a missing-column case: the caller declared nothing, so nothing can be matched.
                    RecordRawSourceFilterSkip(cmd, binding, sourceOrdinal, filter.Key, "UndeterminedColumns", missingColumns: null);
                    return false;
                }

                var missing = FilterColumnDependencyVisitor.FindMissing(requirements.Columns, binding.AvailableColumns);
                if (missing is { Count: > 0 })
                {
                    RecordRawSourceFilterSkip(cmd, binding, sourceOrdinal, filter.Key, "MissingColumns", string.Join(", ", missing));
                    return false;
                }
            }

            body = BuildFilterBody(filterLambda, cmd._dataContext!, entityParameter);
            return true;
        }

        // Collects the safe diagnostic data for one skipped filter. Only the entity type name, source
        // ordinal, filter key, reason and declared column names are stored — never SQL text, table names,
        // parameters or captured values.
        private static void RecordRawSourceFilterSkip(QueryCommand cmd, FromExpression.EntityBinding binding, int sourceOrdinal, string filterKey, string reason, string? missingColumns)
        {
            (cmd.PendingRawSourceFilterSkips ??= []).Add(
                new QueryCommand.RawSourceFilterSkip(binding.EntityType.Name, sourceOrdinal, filterKey, reason, missingColumns));
        }

        // Collects the active filters of a bound raw/named source joined with a CROSS/APPLY join. Such a
        // join has no ON clause, so the filters cannot be merged into it; they are resolved to one lambda
        // (so a builder-function filter runs once) and deferred to the main-source pass, which re-roots
        // them onto the join's projection alias in WHERE. Only inner CROSS semantics reach here: OUTER
        // APPLY and PASTE would change meaning if their predicates moved into WHERE, so they are left
        // unchanged. The child filter scope is unioned with the command's exactly like the ON path.
        private static void CollectBoundCrossJoinFilters(QueryCommand cmd, JoinExpression join, FromExpression.EntityBinding binding, int sourceOrdinal)
        {
            var scope = join.FilterScope is { } childScope
                ? childScope.Union(cmd._filterScope)
                : cmd._filterScope;

            var filters = QueryFilterResolver.GetFilters(binding.EntityType, scope, cmd._dataContext);
            if (filters.Count == 0)
                return;

            for (var (i, cnt) = (0, filters.Count); i < cnt; i++)
            {
                var filter = filters[i];
                if (!TryResolveFilterLambda(filter, cmd._dataContext!, out var filterLambda))
                    continue;

                (cmd.PendingCrossJoinFilters ??= []).Add(
                    new QueryCommand.PendingCrossJoinFilter(binding, sourceOrdinal, filter, filterLambda));
            }
        }

        // Appends the deferred bound CROSS/APPLY join filters (collected in PrepareJoin, because those
        // joins carry no ON clause) to the statement condition. Each predicate is re-rooted onto the
        // join's alias in the projection — join ordinal n surfaces as Item(n + 1), the main source being
        // Item1 — and placed in WHERE; a CROSS/APPLY join must never fabricate an ON clause. The same
        // per-filter compatibility decision and skip diagnostics as an ON filter apply, keyed by the
        // join's own source ordinal.
        private static Expression? AppendPendingCrossJoinFilters(QueryCommand cmd, Expression parameter, Expression? body)
        {
            if (cmd.PendingCrossJoinFilters is not { Count: > 0 } pending)
                return body;

            for (var (i, cnt) = (0, pending.Count); i < cnt; i++)
            {
                var item = pending[i];
                var joinAlias = Expression.Property(parameter, "Item" + (item.SourceOrdinal + 1));

                if (!TryGetBoundFilterBody(cmd, item.Binding, item.SourceOrdinal, item.Filter, item.Lambda, joinAlias, out var filterBody))
                    continue;

                body = body is null ? filterBody : Expression.AndAlso(body, filterBody);
            }

            return body;
        }

        private static void InjectJoinFilters(QueryCommand cmd, JoinExpression join, int sourceOrdinal)
        {
            if (join.From.SubQuery is not null)
                return;

            if (join.OriginalJoinCondition is not { Parameters: { Count: >= 2 } } joinCondition)
            {
                // A CROSS join / CROSS APPLY carries no ON clause. A bound raw/named source on such a
                // join still resolves its filters, but there is no predicate to attach them to, so they
                // are deferred to the statement's WHERE (the applicable placement for an inner
                // cross/apply) instead of fabricating an ON. OUTER APPLY / PASTE are excluded because a
                // WHERE predicate would change their result; their bound source keeps the prior behavior.
                if (join.From.SourceBinding is { } crossBinding
                    && join.JoinType is JoinType.Cross or JoinType.FullCross or JoinType.CrossApply)
                {
                    CollectBoundCrossJoinFilters(cmd, join, crossBinding, sourceOrdinal);
                }

                return;
            }

            // A join may carry the right-hand (child) side's own selective filter scope (single-query
            // LoadWith/JoinInto or a JoinInto child that disabled filters). The effective child scope is
            // always the union of the child's scope and the command's scope: `null` (a plain join) and an
            // empty child scope both inherit the command's whole scope, while a non-empty child scope adds
            // to it and All absorbs the union. So a parent IgnoreFilters() disables every child filter, and
            // a child IgnoreFilters(keys) is not narrowed by a selective parent scope.
            var scope = join.FilterScope is { } childScope
                ? childScope.Union(cmd._filterScope)
                : cmd._filterScope;

            var rightParameter = joinCondition.Parameters[1];
            var body = joinCondition.Body;

            if (join.From.SourceBinding is { } binding)
            {
                // The joined raw source carries its own caller binding: use that binding (never the main
                // source's or another earlier occurrence's) and apply the per-filter compatibility decision
                // against that joined source's alias. Incompatible filters are skipped individually with
                // their own warning; the compatible ones keep their place in the ON condition, so an outer
                // join's predicates stay in ON and never move into WHERE.
                var boundFilters = QueryFilterResolver.GetFilters(binding.EntityType, scope, cmd._dataContext);
                if (boundFilters.Count == 0)
                    return;

                for (var (i, cnt) = (0, boundFilters.Count); i < cnt; i++)
                {
                    var filter = boundFilters[i];
                    if (!TryResolveFilterLambda(filter, cmd._dataContext!, out var filterLambda))
                        continue;

                    if (!TryGetBoundFilterBody(cmd, binding, sourceOrdinal, filter, filterLambda, rightParameter, out var filterBody))
                        continue;

                    body = Expression.AndAlso(body, filterBody);
                }

                join.SetJoinCondition(Expression.Lambda(body, joinCondition.Parameters));
                return;
            }

            var rightType = join.EntityType ?? join.From.SourceType ?? joinCondition.Parameters[1].Type;
            var filters = QueryFilterResolver.GetFilters(rightType, scope, cmd._dataContext);
            if (filters.Count == 0)
                return;

            for (var (i, cnt) = (0, filters.Count); i < cnt; i++)
            {
                if (!TryBuildFilterBody(filters[i], cmd._dataContext!, rightParameter, out var filterBody))
                    continue;

                body = Expression.AndAlso(body, filterBody);
            }

            join.SetJoinCondition(Expression.Lambda(body, joinCondition.Parameters));
        }

        // Reduces one resolved filter to the body to AND into the source condition. A predicate filter is
        // used as declared; a builder-function filter is invoked once here (at plan build) and only the
        // predicate it added is kept. A filter that declares neither form contributes nothing.
        internal static bool TryBuildFilterBody(IQueryFilterMetadata filter, IDataContext dataContext, Expression entityParameter, out Expression body)
        {
            if (!TryResolveFilterLambda(filter, dataContext, out var filterLambda))
            {
                body = null!;
                return false;
            }

            body = BuildFilterBody(filterLambda, dataContext, entityParameter);
            return true;
        }

        // Resolves a filter to its predicate lambda, invoking a builder-function filter exactly once. A
        // filter that declares neither form contributes nothing (returns false).
        internal static bool TryResolveFilterLambda(IQueryFilterMetadata filter, IDataContext dataContext, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LambdaExpression? filterLambda)
        {
            if (filter.Lambda is { } lambda)
            {
                filterLambda = lambda;
                return true;
            }

            if (filter.Func is not null)
            {
                filterLambda = QueryFilterFunc.Apply(filter, dataContext);
                return true;
            }

            filterLambda = null;
            return false;
        }

        private static Expression BuildFilterBody(LambdaExpression filter, IDataContext dataContext, Expression entityParameter)
        {
            if (filter.Parameters.Count == 0)
                throw new NotSupportedException($"The query filter registered for the {entityParameter.Type.Name} source declares no entity parameter.");

            var filterEntityParameter = filter.Parameters[0];
            if (!filterEntityParameter.Type.IsAssignableFrom(entityParameter.Type))
                throw new NotSupportedException($"The query filter registered for {filterEntityParameter.Type.Name} cannot be applied to the {entityParameter.Type.Name} source: the filter's entity parameter type does not match the source type.");

            var body = filter.Body;

            if (filter.Parameters.Count > 1)
            {
                var host = new QueryFilterContext(dataContext);
                var context = Expression.Property(Expression.Constant(host), nameof(QueryFilterContext.Context));
                body = new ReplaceParameterInstanceVisitor(filter.Parameters[1], context).Visit(body);
            }

            var rewritten = new ReplaceParameterInstanceVisitor(filterEntityParameter, entityParameter).Visit(body);

            // Two distinct owner-getter chains that collapse to one source name would emit duplicate
            // placeholders and fail at parameter binding; reject the ambiguity during preparation, before
            // any SQL is built or executed.
            QueryFilterContextAccessor.ValidateDistinctIdentities(rewritten);

            return rewritten;
        }

        // Walks one filter predicate and reports the SQL column names it reads off its entity parameter.
        // It is intentionally filter-specific: it maps the *root* property of each member chain (unwrapping
        // nullable/`Value` chains) through the effective entity metadata by (DeclaringType, MetadataToken),
        // so it never becomes a general SQL-expression column extractor. A captured/context read is not a
        // column; opaque whole-entity use, dynamic access or an unmapped member makes the whole filter
        // undetermined (conservative best-effort skip).
        private sealed class FilterColumnDependencyVisitor : ExpressionVisitor
        {
            private readonly INamingConvention? _convention;
            private readonly ParameterExpression _entityParameter;
            private readonly IEntityMetadata? _metadata;
            private readonly Dictionary<FilterMemberKey, IPropertyMetadata>? _byMember;
            private HashSet<ParameterExpression>? _knownParameters;

            public FilterColumnDependencyVisitor(Type entityType, INamingConvention? convention, ParameterExpression entityParameter)
            {
                _convention = convention;
                _entityParameter = entityParameter;

                // Resolve through the normal path (configured mapping wins over the auto mapping), not
                // just DataContextCache.Metadata: a bound raw source's entity may only have been resolved
                // into the auto cache by BindEntity/GetFilters.
                var metadata = DataContextExtensions.ResolveMetadata(null, entityType);
                _metadata = metadata;
                _byMember = new Dictionary<FilterMemberKey, IPropertyMetadata>(metadata.Properties.Count);
                for (var i = 0; i < metadata.Properties.Count; i++)
                {
                    var property = metadata.Properties[i].PropertyInfo;
                    if (property.DeclaringType is { } declaringType)
                        _byMember[new FilterMemberKey(declaringType, property.MetadataToken)] = metadata.Properties[i];
                }
            }

            public bool Undetermined { get; private set; }

            public List<string> RequiredColumns { get; } = [];

            public FilterColumnRequirements Analyze(LambdaExpression filter)
            {
                // The filter's own non-entity parameters (its IDataContext parameter, if declared) are
                // not column dependencies; every *other* parameter reached from the body (a nested
                // lambda's element) leaves the root unresolved and makes the filter undetermined.
                if (filter.Parameters.Count > 1)
                {
                    _knownParameters = new HashSet<ParameterExpression>();
                    for (var i = 1; i < filter.Parameters.Count; i++)
                        _knownParameters.Add(filter.Parameters[i]);
                }

                Visit(filter.Body);
                return new FilterColumnRequirements(Undetermined, RequiredColumns);
            }

            // Returns the required column names not present (case-insensitively) in the declared shape,
            // or null when every dependency is declared.
            public static List<string>? FindMissing(IReadOnlyList<string> requiredColumns, IReadOnlyList<string> availableColumns)
            {
                List<string>? missing = null;
                for (var (i, cnt) = (0, requiredColumns.Count); i < cnt; i++)
                {
                    var required = requiredColumns[i];
                    var found = false;
                    for (var (j, availableCount) = (0, availableColumns.Count); j < availableCount; j++)
                    {
                        if (string.Equals(required, availableColumns[j], StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        (missing ??= []).Add(required);
                }

                return missing;
            }

            protected override Expression VisitMember(MemberExpression node)
            {
                if (!TryGetEntityRoot(node, out var rootProperty, out var chain))
                    return base.VisitMember(node);

                // The member read directly off the entity parameter must itself be a mapped column: a
                // bound raw source declares a flat column list, so a navigation/field root can never be
                // matched against it.
                if (rootProperty is null || !TryMapColumn(rootProperty, out _))
                {
                    Undetermined = true;
                    return node;
                }

                // A chain is compatible only when its *physical* column can be proven. The SQL renderer
                // reads the column of the outermost member that names one (skipping CLR-only wrappers
                // such as Nullable.Value / string.Length / DateTime parts), so a nested chain like
                // e.Navigation.Col depends on Col's column, not on the navigation's. Tracking only the
                // root would judge the filter compatible while the physical column is deeper; a member
                // that is neither a wrapper nor a mapped column leaves the dependency unproven.
                if (!TryResolvePhysicalColumn(chain, out var column))
                {
                    Undetermined = true;
                    return node;
                }

                if (column.Length > 0)
                    RequiredColumns.Add(column);

                return node;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                // The entity parameter reached other than as the instance of a member chain is opaque
                // whole-entity use (a comparison, an argument, an indexer). A parameter declared by the
                // filter itself (its IDataContext parameter) is not a column dependency; any other
                // parameter — for example a nested lambda's element — leaves the root unresolved.
                if (ReferenceEquals(node, _entityParameter))
                    Undetermined = true;
                else if (_knownParameters is null || !_knownParameters.Contains(node))
                    Undetermined = true;

                return node;
            }

            // Returns the member chain from the entity parameter outward: chain[0] is read directly off
            // the parameter, chain[^1] is the node's own member. A transparent Convert/ConvertChecked
            // over the parameter (an upcast or boxing) is normalized. Returns false when the chain is
            // not rooted at the entity parameter.
            private bool TryGetEntityRoot(MemberExpression node, out PropertyInfo? rootProperty, out List<MemberInfo> chain)
            {
                rootProperty = null;
                chain = null!;
                var members = new List<MemberInfo>();
                Expression? current = node;
                while (true)
                {
                    if (current is MemberExpression member)
                    {
                        members.Add(member.Member);
                        current = member.Expression;
                        continue;
                    }

                    if (current is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
                    {
                        current = unary.Operand;
                        continue;
                    }

                    break;
                }

                if (!ReferenceEquals(current, _entityParameter))
                    return false;

                members.Reverse();
                chain = members;
                rootProperty = members[0] as PropertyInfo;
                return true;
            }

            // Resolves the physical column a rooted chain reads by walking from the node's member inward,
            // skipping CLR-only wrappers, and returning the first member that maps to a column.
            private bool TryResolvePhysicalColumn(List<MemberInfo> chain, out string column)
            {
                for (var i = chain.Count - 1; i >= 0; i--)
                {
                    if (chain[i] is not PropertyInfo property)
                        break;

                    if (IsTransparentMember(property))
                        continue;

                    return TryMapAnyColumn(property, out column);
                }

                column = string.Empty;
                return false;
            }

            // A member whose SQL rendering keeps the underlying column (Nullable.Value, string.Length,
            // DateTime parts) contributes no dependency of its own.
            private static bool IsTransparentMember(PropertyInfo property)
            {
                var declaringType = property.DeclaringType;
                if (declaringType is null)
                    return false;

                if (property.Name == nameof(Nullable<int>.Value) && Nullable.GetUnderlyingType(declaringType) is not null)
                    return true;

                if (declaringType == typeof(string) && property.Name == nameof(string.Length))
                    return true;

                return declaringType == typeof(DateTime) && property.Name is
                    nameof(DateTime.Year) or nameof(DateTime.Month) or nameof(DateTime.Day)
                    or nameof(DateTime.Hour) or nameof(DateTime.Minute) or nameof(DateTime.Second)
                    or nameof(DateTime.DayOfYear);
            }

            private bool TryMapColumn(PropertyInfo property, out string column)
            {
                IPropertyMetadata? mapped = null;
                if (_byMember is not null && property.DeclaringType is { } declaringType
                    && _byMember.TryGetValue(new FilterMemberKey(declaringType, property.MetadataToken), out var indexed))
                    mapped = indexed;

                mapped ??= _metadata is null ? null : MemberTranslator.FindProperty(_metadata, property);

                return TryFormatColumn(mapped, out column);
            }

            // Resolves the column of a member that is not part of the entity's own metadata (a member of
            // a nested mapped type) through that member's declaring-type metadata, mirroring the
            // renderer's column lookup.
            private bool TryMapAnyColumn(PropertyInfo property, out string column)
            {
                IPropertyMetadata? mapped = null;
                if (_byMember is not null && property.DeclaringType is { } declaringType
                    && _byMember.TryGetValue(new FilterMemberKey(declaringType, property.MetadataToken), out var indexed))
                    mapped = indexed;

                mapped ??= MemberTranslator.ResolveProperty(property, _metadata);

                return TryFormatColumn(mapped, out column);
            }

            private bool TryFormatColumn(IPropertyMetadata? mapped, out string column)
            {
                column = string.Empty;

                if (mapped is null || mapped.IsDynamicColumnsStore || mapped.RangeColumns is not null)
                    return false;

                var name = mapped.ColumnName;
                if (string.IsNullOrEmpty(name))
                    return false;

                column = mapped.IsColumnNameAuto && _convention is not null ? _convention.ColumnName(name) : name;
                return true;
            }
        }

        private readonly record struct FilterMemberKey(Type DeclaringType, int MetadataToken);

        private readonly record struct FilterColumnRequirements(bool Undetermined, IReadOnlyList<string> Columns);

        private sealed class ReplaceParameterInstanceVisitor(ParameterExpression target, Expression replacement) : ExpressionVisitor
        {
            protected override Expression VisitParameter(ParameterExpression node)
                => ReferenceEquals(node, target) ? replacement : base.VisitParameter(node);
        }

        private static int PrepareSorting(QueryCommand cmd, SelectExpression[]? selectList, bool noHash, CancellationToken cancellationToken)
        {
            int sortingPlanHash = 7;
            if (cmd._sorting is not null)
            {
                var sortingSpan = cmd._sorting.AsSpan();
                var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(cmd._dataContext!, cmd, cancellationToken, cmd._dataContext!.Logger);

                for (var (i, cnt) = (0, cmd._sorting.Length); i < cnt; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    ref var sort = ref sortingSpan[i];

                    if (sort.SortExpression is not null)
                    {
                        var sortExpression = RewriteProjectionSort(cmd, selectList, sort.SortExpression);
                        sort.PreparedExpression = innerQueryVisitor.Visit(sortExpression);

                        if (!cmd._dontCache && !noHash) unchecked
                        {

                            sortingPlanHash = sortingPlanHash * 13 + cmd.GetSortingExpressionPlanEqualityComparer().GetHashCodeRef(in sort);
                        }
                    }
                    else if (sort.ColumnIndex.HasValue)
                    {
                        if (!cmd._dontCache && !noHash) unchecked
                            {

                                sortingPlanHash = sortingPlanHash * 13 + sort.ColumnIndex.Value;
                            }
                    }
                    else
                        throw new InvalidOperationException("Expression on column index must be specified");
                }
            }

            return sortingPlanHash;
        }

        /// <summary>
        /// Rewrites an <c>ORDER BY</c> expression written over the projected result type into one over
        /// the source type, so a member of the projection resolves to the expression that produced the
        /// already-selected column (for example <c>x.Cnt</c> becomes the aggregate call). An expression
        /// already over the source type is returned unchanged, as is any expression when the command has
        /// no projection or the projection is not available.
        /// </summary>
        private static Expression RewriteProjectionSort(QueryCommand cmd, SelectExpression[]? selectList, Expression sortExpression)
        {
            if (cmd._exp is not { Parameters.Count: 1 } projection || selectList is null)
                return sortExpression;

            if (sortExpression is not LambdaExpression { Parameters.Count: 1 } lambda)
                return sortExpression;

            var sortParam = lambda.Parameters[0];
            if (sortParam.Type == projection.Parameters[0].Type)
                return sortExpression;

            var body = new ProjectionSortRewriter(sortParam, selectList).Visit(lambda.Body);
            return Expression.Lambda(body, projection.Parameters[0]);
        }

        /// <summary>
        /// Replaces a member access on the projected result parameter by the projection expression that
        /// produces the same column. A member that is not part of the projection is rejected, because it
        /// cannot be rendered against the source.
        /// </summary>
        private sealed class ProjectionSortRewriter(ParameterExpression sortParameter, SelectExpression[] selectList) : ExpressionVisitor
        {
            protected override Expression VisitMember(MemberExpression node)
            {
                if (ReferenceEquals(node.Expression, sortParameter))
                {
                    for (var (i, cnt) = (0, selectList.Length); i < cnt; i++)
                    {
                        var item = selectList[i];
                        if (string.Equals(item.PropertyName, node.Member.Name, StringComparison.Ordinal) && item.Expression is not null)
                            return item.Expression is LambdaExpression projection ? projection.Body : item.Expression;
                    }

                    throw new QueryPreparationException($"The ORDER BY member '{node.Member.Name}' is not part of the projection.");
                }

                return base.VisitMember(node);
            }
        }

        private static int PrepareWhere(QueryCommand cmd, LambdaExpression? condition, bool noHash, CancellationToken cancellationToken)
        {
            int wherePlanHash = 7;
            if (condition is not null)
            {
                var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(cmd._dataContext!, cmd, cancellationToken, cmd._dataContext!.Logger);
                cmd.PreparedCondition = innerQueryVisitor.Visit(condition);

                if (!cmd._dontCache && !noHash) unchecked
                    {

                        wherePlanHash = wherePlanHash * 13 + cmd.GetExpressionPlanEqualityComparer().GetHashCode(cmd.PreparedCondition);
                        cmd._whereBasePlanHash = wherePlanHash;

                        // A captured collection contributes no shape to the expression hash, so fold the
                        // evaluated value-list and lookup shape in as well; otherwise a plan built for one
                        // list length (or dictionary size) would be reused for another. The renderer
                        // reuses the evaluated partitions.
                        cmd.InValuesShapeHash = InValues.ComputeShapeHash(cmd.PreparedCondition, cmd, out var partitions, out var hasMatch);
                        cmd.InValuesPartitions = partitions;
                        cmd.HasTopLevelInValues = hasMatch;
                        if (cmd.HasTopLevelInValues)
                            wherePlanHash = wherePlanHash * 13 + cmd.InValuesShapeHash;
                    }
            }

            return wherePlanHash;
        }

        private static void PreparePreWhere(QueryCommand cmd, bool noHash, CancellationToken cancellationToken)
        {
            if (cmd._preWhere is null)
                return;

            var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(cmd._dataContext!, cmd, cancellationToken, cmd._dataContext!.Logger);
            cmd.PreparedPreWhere = innerQueryVisitor.Visit(cmd._preWhere);

            if (!cmd._dontCache && !noHash)
            {
                cmd.PreWhereShapeHash = InValues.ComputeShapeHash(cmd.PreparedPreWhere, cmd, out var partitions, out var hasMatch);
                cmd.HasPreWhereInValues = hasMatch;

                if (partitions is { Count: > 0 })
                {
                    cmd.InValuesPartitions ??= new Dictionary<Expression, InValuesPartition>(ReferenceEqualityComparer.Instance);

                    foreach (var (key, value) in partitions)
                        cmd.InValuesPartitions[key] = value;
                }
            }
        }

        private static void PrepareArrayJoin(QueryCommand cmd, CancellationToken cancellationToken)
        {
            if (cmd._arrayJoins is not { Length: > 0 } arrayJoins)
                return;

            var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(cmd._dataContext!, cmd, cancellationToken, cmd._dataContext!.Logger);
            var prepared = new Expression[arrayJoins.Length];

            for (var (i, cnt) = (0, arrayJoins.Length); i < cnt; i++)
                prepared[i] = innerQueryVisitor.Visit(arrayJoins[i]);

            cmd._preparedArrayJoin = prepared;
        }

        private static SelectExpression[]? PrepareLimitBy(QueryCommand cmd, CancellationToken cancellationToken)
        {
            var columns = cmd._limitByColumns;

            if (cmd.LimitBy is { } limitBy && columns is null)
            {
                if (limitBy.Limit <= 0)
                    throw new QueryPreparationException("LIMIT BY requires a positive row count.");

                if (limitBy.Offset < 0)
                    throw new QueryPreparationException("LIMIT BY offset must be non-negative.");

                columns = BuildKeyColumns(limitBy.Expression, "LIMIT BY", cancellationToken);
            }

            return columns;
        }

        private static SelectExpression[]? PrepareDistinctOn(QueryCommand cmd, CancellationToken cancellationToken)
        {
            var columns = cmd._distinctOnColumns;

            if (cmd.DistinctOn is { } distinctOn && columns is null)
                columns = BuildKeyColumns(distinctOn.Expression, "DISTINCT ON", cancellationToken);

            return columns;
        }

        /// <summary>
        /// Prepares the value-selector (and optional group-by) key columns of a
        /// <c>SelectWhereMax</c>/<c>SelectWhereMin</c> clause. Rendering is provider-specific and happens
        /// later; this only resolves the clause lambdas into select expressions.
        /// </summary>
        private static (SelectExpression[]? ValueColumns, SelectExpression[]? GroupByColumns) PrepareExtremeRow(QueryCommand cmd, CancellationToken cancellationToken)
        {
            var valueColumns = cmd._extremeRowColumns;
            var groupByColumns = cmd._extremeRowGroupByColumns;

            if (cmd.ExtremeRow is { } extremeRow)
            {
                if (valueColumns is null)
                    valueColumns = BuildKeyColumns(extremeRow.ValueSelector, "SelectWhereMax/Min", cancellationToken);

                if (extremeRow.GroupBy is { } groupBy && groupByColumns is null)
                    groupByColumns = BuildKeyColumns(groupBy, "SelectWhereMax/Min GROUP BY", cancellationToken);
            }

            return (valueColumns, groupByColumns);
        }

        /// <summary>
        /// Folds the named window definitions into the plan key: their names, partition/order keys and
        /// frames all change the emitted <c>WINDOW</c> clause, so two otherwise identical commands must
        /// not share a cached plan.
        /// </summary>
        private static void PrepareWindows(QueryCommand cmd, bool noHash)
        {
            if (cmd.Windows is not { Count: > 0 } windows)
                return;

            if (cmd._dontCache || noHash)
                return;

            var comparer = cmd.GetExpressionPlanEqualityComparer();

            XxHash32 hash = new();
            unchecked
            {
                for (var i = 0; i < windows.Count; i++)
                {
                    var window = windows[i];
                    hash.Add(window.Name);

                    for (var p = 0; p < window.PartitionBy.Count; p++)
                        hash.Add((Expression)window.PartitionBy[p], comparer);

                    for (var o = 0; o < window.OrderBy.Count; o++)
                    {
                        var key = window.OrderBy[o];
                        hash.Add((int)key.Direction);
                        hash.Add(key.Expression, comparer);
                    }

                    if (window.Frame is { } frame)
                    {
                        hash.Add((int)frame.Type);
                        hash.Add((int)frame.Start.Kind);
                        hash.Add(frame.Start.Offset);
                        hash.Add((int)frame.End.Kind);
                        hash.Add(frame.End.Offset);
                        if (frame.Exclusion is { } exclusion)
                            hash.Add((int)exclusion);
                    }
                }

                cmd.WindowsPlanHash = hash.ToHashCode();
            }
        }

        private static SelectExpression[] BuildKeyColumns(LambdaExpression expression, string clauseName, CancellationToken cancellationToken)
        {
            if (expression.Body is NewExpression ctor && !TypeFacts.IsSingleColumnProjection(ctor.Type))
            {
                var args = ctor.Arguments;
                var argsCount = args.Count;

                if (argsCount == 0)
                    throw new QueryPreparationException($"{clauseName} requires at least one key column.");

                var columns = new SelectExpression[argsCount];

                for (var idx = 0; idx < argsCount; idx++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return columns;

                    var ctorParam = ctor.Constructor!.GetParameters()[idx];

                    columns[idx] = new SelectExpression(ctorParam.ParameterType)
                    {
                        Index = idx,
                        PropertyName = ctorParam.Name!,
                        Expression = args[idx]
                    };
                }

                return columns;
            }
            else
            {
                var body = expression.Body;
                return
                [
                    new SelectExpression(body.Type)
                    {
                        Index = 0,
                        PropertyName = body is MemberExpression member ? member.Member.Name : "key",
                        Expression = body
                    }
                ];
            }
        }

        private static (SelectExpression[]?, int) PrepareGrouping(QueryCommand cmd, bool noHash, CancellationToken cancellationToken)
        {
            var groupingList = cmd._groupingList;
            int groupingPlanHash = 7;
            if (cmd._groupExp is not null && groupingList is null)
            {
                var columns = BuildKeyColumns(cmd._groupExp, "GROUP BY", cancellationToken);
                groupingList = columns;

                for (var idx = 0; idx < columns.Length; idx++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return (groupingList, groupingPlanHash);

                    var selExp = columns[idx];
                    if (!noHash)
                        selExp.PlanHashCode = cmd.GetSelectExpressionPlanEqualityComparer().GetHashCode(selExp);

                    if (!cmd._dontCache && !noHash) unchecked
                        {
                            groupingPlanHash = groupingPlanHash * 13 + selExp.PlanHashCode;
                        }
                }
            }

            if (cmd._having is not null)
            {
                // Run the correlated-query visitor over HAVING just like WHERE: a subquery in the
                // predicate (correlated on the grouping key or not) then registers its outer references
                // on this command and renders through the normal select path. The visited form is cached
                // on the command and preferred by the renderer.
                var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(cmd._dataContext!, cmd, cancellationToken, cmd._dataContext!.Logger);
                cmd.PreparedHaving = innerQueryVisitor.Visit(cmd._having);

                if (!cmd._dontCache && !noHash) unchecked
                    {
                        // Hash the raw HAVING lambda, matching QueryPlanEqualityComparer.Equals which
                        // compares <c>Having</c>; the prepared form is a deterministic function of it and
                        // of the outer references that the plan key already covers.
                        groupingPlanHash = groupingPlanHash * 13 + cmd.GetExpressionPlanEqualityComparer().GetHashCode(cmd._having);
                    }
            }

            return (groupingList, groupingPlanHash);
        }

    }
}
