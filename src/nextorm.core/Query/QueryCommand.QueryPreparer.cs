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
            PrepareFrom(from, dontCalculateHash, cancellationToken);
            var joinPlanHash = PrepareJoin(cmd, dontCalculateHash, cancellationToken);
            var (selectList, columnsPlanHash) = PrepareColumns(cmd, dontCalculateHash, srcType, cancellationToken);
            var wherePlanHash = PrepareWhere(cmd, InjectMainSourceFilters(cmd, srcType), dontCalculateHash, cancellationToken);
            PreparePreWhere(cmd, dontCalculateHash, cancellationToken);
            PrepareArrayJoin(cmd, cancellationToken);

            var (groupingList, groupingPlanHash) = PrepareGrouping(cmd, dontCalculateHash, cancellationToken);
            var limitByColumns = PrepareLimitBy(cmd, cancellationToken);
            var distinctOnColumns = PrepareDistinctOn(cmd, cancellationToken);
            PrepareWindows(cmd, dontCalculateHash);
            var sortingPlanHash = PrepareSorting(cmd, selectList, dontCalculateHash, cancellationToken);

            cmd._union?.PrepareCommand(dontCalculateHash, cancellationToken);
            PrepareCtes(cmd, dontCalculateHash, cancellationToken);
            PrepareHints(cmd, dontCalculateHash);

            cmd._isPrepared = true;
            cmd._selectList = selectList ?? [];
            cmd._groupingList = groupingList ?? [];
            cmd._limitByColumns = limitByColumns ?? [];
            cmd._distinctOnColumns = distinctOnColumns ?? [];
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

        private static void PrepareFrom(FromExpression? from, bool dontCalculateHash, CancellationToken cancellationToken)
        {
            if (from?.SubQuery is not null && !from.SubQuery._isPrepared)
                from.SubQuery.PrepareCommand(dontCalculateHash, cancellationToken);

            if (from?.Pivot is not null)
                PrepareFrom(from.Pivot.Inner, dontCalculateHash, cancellationToken);
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

                    // An INSERT ... SELECT used as a data-modifying CTE body renders its source select
                    // in the enclosing statement, so the source must be prepared like any other command.
                    if (cte.Mutation is { Source: { IsPrepared: false } mutationSource })
                        mutationSource.PrepareCommand(noHash, cancellationToken);

                    // A data-modifying CTE is a side-effecting statement: never share its plan, because
                    // the mutation's shape (row count, target columns) is not fully captured by the CTE
                    // query.
                    if (cte.IsDataModifying)
                        cmd.Cache = false;
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
                            join.SetFrom(new FromExpression(applyCommand));
                        }
                    }
                    else
                    {
                        PrepareFrom(join.From, noHash, cancellationToken);
                    }

                    InjectJoinFilters(cmd, join);

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

            var filters = QueryFilterResolver.GetFilters(filterEntityType, cmd._filterScope);
            if (filters.Count == 0)
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
                if (!TryBuildFilterBody(cmd, filters[i], filterTarget, out var filterBody))
                    continue;

                body = body is null ? filterBody : Expression.AndAlso(body, filterBody);
            }

            return body is null ? null : Expression.Lambda(body, parameter);
        }

        private static void InjectJoinFilters(QueryCommand cmd, JoinExpression join)
        {
            if (join.From.SubQuery is not null)
                return;

            if (join.OriginalJoinCondition is not { Parameters: { Count: >= 2 } } joinCondition)
                return;

            var rightType = join.EntityType ?? join.From.SourceType ?? joinCondition.Parameters[1].Type;
            // A join may carry the right-hand (child) side's own selective filter scope (single-query
            // LoadWith/JoinInto or a JoinInto child that disabled filters). The effective child scope is
            // always the union of the child's scope and the command's scope: `null` (a plain join) and an
            // empty child scope both inherit the command's whole scope, while a non-empty child scope adds
            // to it and All absorbs the union. So a parent IgnoreFilters() disables every child filter, and
            // a child IgnoreFilters(keys) is not narrowed by a selective parent scope.
            var scope = join.FilterScope is { } childScope
                ? childScope.Union(cmd._filterScope)
                : cmd._filterScope;
            var filters = QueryFilterResolver.GetFilters(rightType, scope);
            if (filters.Count == 0)
                return;

            var rightParameter = joinCondition.Parameters[1];
            var body = joinCondition.Body;

            for (var (i, cnt) = (0, filters.Count); i < cnt; i++)
            {
                if (!TryBuildFilterBody(cmd, filters[i], rightParameter, out var filterBody))
                    continue;

                body = Expression.AndAlso(body, filterBody);
            }

            join.SetJoinCondition(Expression.Lambda(body, joinCondition.Parameters));
        }

        // Reduces one resolved filter to the body to AND into the source condition. A predicate filter is
        // used as declared; a builder-function filter is invoked once here (at plan build) and only the
        // predicate it added is kept. A filter that declares neither form contributes nothing.
        private static bool TryBuildFilterBody(QueryCommand cmd, IQueryFilterMetadata filter, Expression entityParameter, out Expression body)
        {
            LambdaExpression filterLambda;
            if (filter.Lambda is { } lambda)
                filterLambda = lambda;
            else if (filter.Func is not null)
                filterLambda = QueryFilterFunc.Apply(filter, cmd._dataContext!);
            else
            {
                body = null!;
                return false;
            }

            body = BuildFilterBody(filterLambda, cmd._dataContext!, entityParameter);
            return true;
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

            return new ReplaceParameterInstanceVisitor(filterEntityParameter, entityParameter).Visit(body);
        }

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
