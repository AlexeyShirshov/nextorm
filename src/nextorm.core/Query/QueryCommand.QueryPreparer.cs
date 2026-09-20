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
            var wherePlanHash = PrepareWhere(cmd, dontCalculateHash, cancellationToken);
            PreparePreWhere(cmd, dontCalculateHash, cancellationToken);
            PrepareArrayJoin(cmd, cancellationToken);

            var (groupingList, groupingPlanHash) = PrepareGrouping(cmd, dontCalculateHash, cancellationToken);
            var limitByColumns = PrepareLimitBy(cmd, cancellationToken);
            var distinctOnColumns = PrepareDistinctOn(cmd, cancellationToken);
            var sortingPlanHash = PrepareSorting(cmd, dontCalculateHash, cancellationToken);

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
        /// Re-evaluates the value-list shape of an already-prepared command. An implicit-cache command can
        /// be reused with a captured collection that was grown or reassigned between executions, so the
        /// plan key must follow the current shape or a stale (wrong parameter count) plan would be reused.
        /// </summary>
        internal static void RefreshInValuesShape(QueryCommand cmd)
        {
            if (!cmd.Cache || (cmd.PreparedCondition is null && cmd.PreparedPreWhere is null))
                return;

            Dictionary<Expression, InValuesPartition>? merged = null;

            if (cmd.HasTopLevelInValues && cmd.PreparedCondition is not null)
            {
                cmd.InValuesShapeHash = InValues.ComputeShapeHash(cmd.PreparedCondition, cmd, out var wherePartitions);
                merged = wherePartitions;
                unchecked
                {
                    cmd.WherePlanHash = cmd._whereBasePlanHash * 13 + cmd.InValuesShapeHash;
                }
            }

            if (cmd.HasPreWhereInValues && cmd.PreparedPreWhere is not null)
            {
                cmd.PreWhereShapeHash = InValues.ComputeShapeHash(cmd.PreparedPreWhere, cmd, out var preWherePartitions);

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
        }

        private static void PrepareCtes(QueryCommand cmd, bool noHash, CancellationToken cancellationToken)
        {
            if (cmd._ctes is not { Count: > 0 }) return;

            for (var (i, cnt) = (0, cmd._ctes.Count); i < cnt; i++)
            {
                var query = cmd._ctes[i].Query;
                if (!query.IsPrepared)
                    query.PrepareCommand(noHash, cancellationToken);
            }

            if (!cmd._dontCache && !noHash)
            {
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
                        hash.Add(cte.Query, cmd.GetQueryPlanEqualityComparer());
                    }
                    cmd.CtesPlanHash = hash.ToHashCode();
                }
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

        /// <summary>
        /// True for the types a projection maps to a single column. A <see cref="NewExpression"/> whose
        /// result is one of these (for example <c>new string('*', 4)</c>) is a scalar, not a composite
        /// (anonymous-type) projection, and must not be expanded into constructor arguments.
        /// </summary>
        private static bool IsSingleColumnType(Type type) =>
            type.IsPrimitive
            || type == typeof(string)
            || type == typeof(byte[])
            || type == typeof(DateTime)
            || type == typeof(decimal)
            || type == typeof(Guid)
            || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));

        private static (SelectExpression[]?, int) PrepareColumns(QueryCommand cmd, bool noHash, Type? srcType, CancellationToken cancellationToken)
        {
            var selectList = cmd._selectList;
            int columnsPlanHash = 7;
            if (selectList is null && !cmd.IgnoreColumns)
            {
                if (cmd._exp is not null)
                {
                    if (cmd._exp.Body is NewExpression ctor && !IsSingleColumnType(ctor.Type))
                    {
                        var args = ctor.Arguments;
                        var argsCount = args.Count;

                        selectList = new SelectExpression[argsCount];

                        var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(cmd._dataContext!, cmd, cancellationToken, cmd._dataContext!.Logger);
                        using var outerScope = innerQueryVisitor.PushOuter(cmd._exp.Parameters[0]);
                        for (var idx = 0; idx < argsCount; idx++)
                        {
                            if (cancellationToken.IsCancellationRequested)
                                return (selectList, columnsPlanHash);

                            SelectExpression selExp;
                            var arg = args[idx];
                            var ctorParam = ctor.Constructor!.GetParameters()[idx];

                            selExp = new SelectExpression(ctorParam.ParameterType)
                            {
                                Index = idx,
                                PropertyName = ctorParam.Name!,
                                Expression = innerQueryVisitor.Visit(arg)
                            };
                            selExp.DefaultOnNull = !selExp.Nullable && CorrelatedQueryExpressionVisitor.IsOrDefaultScalar(arg);
                            if (!cmd._dontCache && !noHash)
                                selExp.PlanHashCode = cmd.GetSelectExpressionPlanEqualityComparer().GetHashCode(selExp);
                            selectList[idx] = selExp;

                            if (!cmd._dontCache && !noHash) unchecked
                                {

                                    columnsPlanHash = columnsPlanHash * 13 + selExp.PlanHashCode;
                                }
                        }



                    }
                    else if (IsSingleColumnType(cmd._exp.Body.Type))
                    {

                        cmd.OneColumn = true;
                        var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(cmd._dataContext!, cmd, cancellationToken, cmd._dataContext!.Logger);
                        var selectExp = innerQueryVisitor.Visit(cmd._exp);

                        var selExp = new SelectExpression(cmd._exp.Body.Type)
                        {
                            Expression = selectExp,
                        };
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

                            var selExp = new SelectExpression(((PropertyInfo)binding!.Member).PropertyType)
                            {
                                Index = idx,
                                PropertyName = binding.Member.Name!,
                                Expression = innerQueryVisitor.Visit(binding.Expression)
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

                    if (cmd._dataContext!.NeedMapping)
                    {
                        if (/*!CacheList || */!DataContextCache.SelectListCache.TryGetValue(srcType, out selectList))
                        {

                            var p = Expression.Parameter(srcType);

                            if (DataContextCache.Metadata.TryGetValue(srcType, out var entityMeta))
                            {
                                var props = entityMeta.Properties;
                                var propsCount = props.Count;

                                selectList = new SelectExpression[propsCount];

                                for (int idx = 0; idx < propsCount; idx++)
                                {
                                    if (cancellationToken.IsCancellationRequested)
                                        return (selectList, columnsPlanHash);

                                    var prop = props[idx];

                                    var pi = prop.PropertyInfo;

                                    Expression exp = Expression.Lambda(Expression.Property(p, pi), p);

                                    var selExp = new SelectExpression(pi.PropertyType)
                                    {
                                        Index = idx,
                                        PropertyName = pi.Name,
                                        Expression = exp,
                                        PropertyInfo = pi
                                    };

                                    if (!cmd._dontCache && !noHash)
                                        selExp.PlanHashCode = cmd.GetSelectExpressionPlanEqualityComparer().GetHashCode(selExp);
                                    selectList[idx] = selExp;

                                    if (!cmd._dontCache && !noHash) unchecked
                                        {
                                            columnsPlanHash = columnsPlanHash * 13 + selExp.PlanHashCode;
                                        }
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

        private static int PrepareJoin(QueryCommand cmd, bool noHash, CancellationToken cancellationToken)
        {
            int joinPlanHash = 7;
            if (cmd._joins is not null)
            {

                for (var (idx, cnt) = (0, cmd._joins.Length); idx < cnt; idx++)
                {
                    var join = cmd._joins[idx];

                    PrepareFrom(join.From, noHash, cancellationToken);

                    if (!cmd._dontCache && !noHash) unchecked
                        {
                            joinPlanHash = joinPlanHash * 13 + cmd.GetJoinExpressionPlanEqualityComparer().GetHashCode(join);
                        }
                }
            }

            return joinPlanHash;
        }

        private static int PrepareSorting(QueryCommand cmd, bool noHash, CancellationToken cancellationToken)
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
                        sort.PreparedExpression = innerQueryVisitor.Visit(sort.SortExpression);

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

        private static int PrepareWhere(QueryCommand cmd, bool noHash, CancellationToken cancellationToken)
        {
            int wherePlanHash = 7;
            if (cmd._condition is not null)
            {
                var innerQueryVisitor = new CorrelatedQueryExpressionVisitor(cmd._dataContext!, cmd, cancellationToken, cmd._dataContext!.Logger);
                cmd.PreparedCondition = innerQueryVisitor.Visit(cmd._condition);

                if (!cmd._dontCache && !noHash) unchecked
                    {

                        wherePlanHash = wherePlanHash * 13 + cmd.GetExpressionPlanEqualityComparer().GetHashCode(cmd.PreparedCondition);
                        cmd._whereBasePlanHash = wherePlanHash;

                        // A captured collection contributes no shape to the expression hash, so fold the
                        // evaluated value-list shape in as well; otherwise a plan built for one list length
                        // would be reused for another. The renderer reuses the evaluated partitions.
                        cmd.InValuesShapeHash = InValues.ComputeShapeHash(cmd.PreparedCondition, cmd, out var partitions);
                        cmd.InValuesPartitions = partitions;
                        cmd.HasTopLevelInValues = partitions is not null;
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
                cmd.PreWhereShapeHash = InValues.ComputeShapeHash(cmd.PreparedPreWhere, cmd, out var partitions);
                cmd.HasPreWhereInValues = partitions is not null;

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

        private static SelectExpression[] BuildKeyColumns(LambdaExpression expression, string clauseName, CancellationToken cancellationToken)
        {
            if (expression.Body is NewExpression ctor && !IsSingleColumnType(ctor.Type))
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
                if (cmd._groupExp.Body is NewExpression ctor && !IsSingleColumnType(ctor.Type))
                {
                    var args = ctor.Arguments;
                    var argsCount = args.Count;

                    groupingList = new SelectExpression[argsCount];

                    for (var idx = 0; idx < argsCount; idx++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return (groupingList, groupingPlanHash);

                        var arg = args[idx];
                        var ctorParam = ctor.Constructor!.GetParameters()[idx];

                        var selExp = new SelectExpression(ctorParam.ParameterType)
                        {
                            Index = idx,
                            PropertyName = ctorParam.Name!,
                            Expression = arg
                        };
                        if (!noHash)
                            selExp.PlanHashCode = cmd.GetSelectExpressionPlanEqualityComparer().GetHashCode(selExp);
                        groupingList[idx] = selExp;

                        if (!cmd._dontCache && !noHash) unchecked
                            {
                                groupingPlanHash = groupingPlanHash * 13 + selExp.PlanHashCode;
                            }
                    }
                }
                else
                    throw new InvalidOperationException("Only new expression is supported");
            }

            if (cmd._having is not null)
            {
                // A subquery in HAVING is not carried through the correlated-query visitor (it is only
                // visited by the renderer), so it would emit garbage such as a raw inner-command node.
                // Reject it explicitly instead of emitting silently invalid SQL.
                if (ContainsSubquery(cmd._having))
                    throw new NotSupportedException("Subqueries are not supported in HAVING; move the condition to WHERE or project it in SELECT.");

                if (!cmd._dontCache && !noHash) unchecked
                    {
                        groupingPlanHash = groupingPlanHash * 13 + cmd.GetExpressionPlanEqualityComparer().GetHashCode(cmd._having);
                    }
            }

            return (groupingList, groupingPlanHash);
        }

        /// <summary>True when the expression contains a subquery (an <see cref="EntityBuilder{T}"/> chain or a query command).</summary>
        private static bool ContainsSubquery(Expression expression)
        {
            var detector = new SubqueryDetector();
            detector.Visit(expression);
            return detector.Found;
        }

        private sealed class SubqueryDetector : ExpressionVisitor
        {
            public bool Found { get; private set; }

            public override Expression? Visit(Expression? node)
                => Found || node is null ? node : base.Visit(node);

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (node.Object?.Type.IsAssignableTo(typeof(QueryCommand)) == true
                    || node.Type.IsAssignableTo(typeof(QueryCommand)))
                {
                    Found = true;
                    return node;
                }

                for (var i = 0; i < node.Arguments.Count; i++)
                {
                    if (node.Arguments[i].Type.IsAssignableTo(typeof(QueryCommand)))
                    {
                        Found = true;
                        return node;
                    }
                }

                return base.VisitMethodCall(node);
            }
        }
    }
}
