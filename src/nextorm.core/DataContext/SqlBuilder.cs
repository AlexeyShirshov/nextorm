using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public struct SqlBuilder
{
    private readonly ISqlDialect _dialect;
    private readonly IColumnsProvider _columnsProvider;
    private readonly IAliasProvider? _aliasProvider;
    private readonly IParamProvider _paramProvider;
    private readonly IQueryProvider _queryProvider;
    private readonly List<Param> _params;
    private readonly bool _paramMode;
    public SqlBuilder(ISqlDialect dialect, bool paramMode, List<Param> @params, IColumnsProvider columnsProvider, IQueryProvider queryProvider, IParamProvider paramProvider, IAliasProvider? aliasProvider, ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(@params);

        _dialect = dialect;
        _paramMode = paramMode;
        _params = @params;
        _columnsProvider = columnsProvider;
        _queryProvider = queryProvider;
        _paramProvider = paramProvider;
        _aliasProvider = aliasProvider;
        // _scope = paramScope;
        Logger = logger;
    }
    public ILogger? Logger { get; }
    public string? MakeSelect(QueryCommand cmd)
    {
#if DEBUG
        if (!cmd.IsPrepared)
            throw new InvalidOperationException("Command not prepared");

        if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Making sql with param mode {mode}", _paramMode);
#endif
        //ArgumentNullException.ThrowIfNull(cmd.SelectList);
        //ArgumentNullException.ThrowIfNull(cmd.From);
        var entityType = cmd.EntityType;
        ArgumentNullException.ThrowIfNull(entityType);

        var selectList = cmd.SelectList;
        var from = cmd.From;
        var ctes = cmd.Ctes;

        var sqlBuilder = _paramMode ? null : StringBuilderPool.Shared.Get();
        var selectBuilder = _paramMode ? null : StringBuilderPool.Shared.Get();
        string? topStmt = null;
        string? withClause = null;
        string? maxRecursionStmt = null;

        try
        {
            // CTEs are rendered (or, in parameter mode, only walked for parameters) before the outer
            // statement so both SQL and parameter passes see the CTE parameters first and in the same order.
            if (ctes is { Count: > 0 })
                withClause = MakeWithClause(ctes, out maxRecursionStmt);

            var pageApplied = !_paramMode && !(cmd.IgnoreColumns || selectList is null) && cmd.Paging.IsTop && _dialect.MakeTop(cmd.Paging.Limit, out topStmt);

            var joins = cmd.Joins;
            var hasJoins = joins?.Length > 0;
            var needAlias = hasJoins || _queryProvider.OuterReferences?.Count > 0;

            if (from is not null)
            {
                var fromStr = MakeFrom(from, needAlias, entityType, hasJoins);
                if (!_paramMode)
                {
                    sqlBuilder!.Append(" from ").Append(fromStr);
                }

                if (hasJoins)
                {
                    for (var (idx, cnt) = (0, joins!.Length); idx < cnt; idx++)
                    {
                        var join = joins[idx];

                        var joinSql = MakeJoin(join, entityType!);
                        if (!_paramMode) sqlBuilder!.Append(joinSql);
                    }
                }

                if (cmd.PreparedCondition is not null)
                {
                    if (!_paramMode) sqlBuilder!.AppendLine().Append(" where ");
                    MakeWhere(sqlBuilder, entityType, cmd.PreparedCondition, 0);
                }

                var grouping = cmd.GroupingList;
                if (grouping?.Length > 0)
                {
                    if (!_paramMode) sqlBuilder!.AppendLine().Append(" group by ");

                    var groupingListCount = grouping.Length;
                    for (var i = 0; i < groupingListCount; i++)
                    {
                        var item = grouping[i];
                        var (needAliasForColumn, column) = MakeColumn(item, entityType, false);

                        if (!_paramMode)
                        {
                            sqlBuilder!.Append(column);

                            if (needAliasForColumn)
                            {
                                sqlBuilder.Append(_dialect.MakeColumnAlias(item.PropertyName));
                            }

                            sqlBuilder.Append(", ");
                        }
                    }

                    if (!_paramMode)
                    {
                        sqlBuilder!.Length -= 2;
                    }

                    if (cmd.Having is not null)
                    {
                        if (!_paramMode) sqlBuilder!.AppendLine().Append(" having ");
                        MakeWhere(sqlBuilder, entityType, cmd.Having, 0);
                    }
                }

                if (cmd.UnionQuery is not null)
                {
                    if (cmd.UnionType is UnionType.IntersectAll or UnionType.ExceptAll && !_dialect.SupportsIntersectExceptAll)
                        throw new NotSupportedException($"The {cmd.UnionType} set operation is not supported by this SQL dialect");

                    if (!_paramMode)
                    {
                        sqlBuilder!.AppendLine().Append(cmd.UnionType switch
                        {
                            UnionType.Distinct => " union ",
                            UnionType.All => " union all ",
                            UnionType.Intersect => " intersect ",
                            UnionType.IntersectAll => " intersect all ",
                            UnionType.Except => " except ",
                            UnionType.ExceptAll => " except all ",
                            _ => throw new NotSupportedException(cmd.UnionType.ToString("G"))
                        }).AppendLine();
                    }

                    var builder = new SqlBuilder(_dialect, _paramMode, _params, _columnsProvider, _queryProvider, _paramProvider, new DefaultAliasProvider(), Logger);
                    var sql = builder.MakeSelect(cmd.UnionQuery);
                    if (!_paramMode) sqlBuilder!.Append(sql);
                }

                var sortingList = cmd.Sorting;
                if (sortingList is not null)
                {
                    if (!_paramMode) sqlBuilder!.AppendLine().Append(" order by ");

                    for (var (i, cnt) = (0, sortingList.Length); i < cnt; i++)
                    {
                        var sorting = sortingList[i];
                        if (sorting.PreparedExpression is not null)
                        {
                            var sortingSql = MakeSort(entityType, sorting.PreparedExpression, 0);
                            if (!_paramMode)
                            {
                                sqlBuilder!.Append(sortingSql);
                                if (sorting.Direction == OrderDirection.Desc)
                                    sqlBuilder.Append(" desc");

                                sqlBuilder.Append(", ");
                            }
                        }
                        else if (!_paramMode && sorting.ColumnIndex.HasValue)
                        {
                            sqlBuilder!.Append(sorting.ColumnIndex);
                            if (sorting.Direction == OrderDirection.Desc)
                                sqlBuilder.Append(" desc");

                            sqlBuilder.Append(", ");
                        }
                    }

                    if (!_paramMode) sqlBuilder!.Length -= 2;
                }
                else if (!pageApplied && !_paramMode && _dialect.GetPagingOrderBy(cmd) is { } pagingOrderBy)
                {
                    sqlBuilder!.AppendLine().Append(" order by ").Append(pagingOrderBy);
                }

                if (!pageApplied && !_paramMode && !cmd.Paging.IsEmpty)
                {
                    sqlBuilder!.AppendLine();
                    _dialect.MakePage(cmd.Paging, sqlBuilder);
                }
            }
            else if (!_paramMode && sqlBuilder!.Length > 0)
                sqlBuilder.Length -= 2;


            if (!_paramMode)
            {
                selectBuilder!.Append("select ");

                // SQL Server renders the limit as TOP(n); DISTINCT has to precede it
                // ("select distinct top(n) ..."), so the flag is emitted before the select-list branch.
                if (cmd.IsDistinct)
                    selectBuilder.Append("distinct ");
            }
            if (cmd.IgnoreColumns || selectList is null)
            {
                if (!_paramMode) selectBuilder!.Append("*, ");
            }
            else
            {
                if (!_paramMode && !string.IsNullOrEmpty(topStmt))
                {
                    selectBuilder!.Append(topStmt).Append(' ');
                }

                var selectListCount = selectList.Length;
                for (var i = 0; i < selectListCount; i++)
                {
                    var item = selectList[i];
                    var (needAliasForColumn, column) = MakeColumn(item, entityType, !needAlias, renameAware: true);

                    if (!_paramMode)
                    {
                        selectBuilder!.Append(column);

                        if (needAliasForColumn)
                        {
                            selectBuilder.Append(_dialect.MakeColumnAlias(item.PropertyName));
                        }

                        selectBuilder.Append(", ");
                    }
                }
            }


            string? r = null;
            if (!_paramMode)
            {
                selectBuilder!.Length -= 2;
                sqlBuilder!.Insert(0, selectBuilder!.ToString());

                if (withClause is not null)
                    sqlBuilder!.Insert(0, withClause);

                if (maxRecursionStmt is not null)
                    sqlBuilder!.Append(' ').Append(maxRecursionStmt);

                r = sqlBuilder!.ToString();
            }


#if DEBUG
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Generated sql with param mode {mode}: {sql}", _paramMode, r);
#endif

            return r;
        }
        finally
        {
            if (selectBuilder is not null)
                StringBuilderPool.Shared.Return(selectBuilder);

            if (sqlBuilder is not null)
                StringBuilderPool.Shared.Return(sqlBuilder);
        }
    }
    /// <summary>
    /// Renders the <c>with [recursive] name as (&lt;select&gt;), ...</c> prefix for <paramref name="ctes"/>.
    /// In parameter mode nothing is rendered but every CTE query is still walked so its parameters are
    /// collected in the same order as the SQL pass. <paramref name="maxRecursionStmt"/> receives the
    /// dialect's recursion option (if any), which the caller appends after the statement.
    /// </summary>
    private string? MakeWithClause(IReadOnlyList<CteDefinition> ctes, out string? maxRecursionStmt)
    {
        maxRecursionStmt = null;

        var anyRecursive = false;
        int? maxRecursion = null;

        for (var (i, cnt) = (0, ctes.Count); i < cnt; i++)
        {
            var cte = ctes[i];
            if (cte.Recursive) anyRecursive = true;
            if (maxRecursion is null && cte.MaxRecursion is int value) maxRecursion = value;
        }

        if (maxRecursion is int depth)
            maxRecursionStmt = _dialect.MakeMaxRecursion(depth);

        if (_paramMode)
        {
            for (var (i, cnt) = (0, ctes.Count); i < cnt; i++)
            {
                var cte = ctes[i];
                var walker = new SqlBuilder(_dialect, true, _params, new DefaultColumnsProvider(), cte.Query, _paramProvider, null, Logger);
                walker.MakeSelect(cte.Query);
            }

            return null;
        }

        var withBuilder = StringBuilderPool.Shared.Get();
        try
        {
            withBuilder.Append(_dialect.MakeWith(anyRecursive));

            for (var (i, cnt) = (0, ctes.Count); i < cnt; i++)
            {
                var cte = ctes[i];

                if (i > 0)
                    withBuilder.Append(", ");

                withBuilder.Append(cte.Name).Append(" as (");

                // Each CTE is rendered in isolation: a fresh columns provider keeps the outer source
                // list untouched, and a fresh alias provider makes alias numbering self-contained
                // (mirroring how UNION branches are rendered).
                var builder = new SqlBuilder(_dialect, false, _params, new DefaultColumnsProvider(), cte.Query, _paramProvider, new DefaultAliasProvider(), Logger);
                withBuilder.Append(builder.MakeSelect(cte.Query));

                withBuilder.Append(')');
            }

            withBuilder.Append(' ');
            return withBuilder.ToString();
        }
        finally
        {
            StringBuilderPool.Shared.Return(withBuilder);
        }
    }
    public string? MakeJoin(JoinExpression join, Type entityType)
    {
        if (join.JoinType is JoinType.Right or JoinType.Full && !_dialect.SupportsRightFullJoin)
            throw new NotSupportedException($"The {join.JoinType} join is not supported by this SQL dialect");

        var sqlBuilder = _paramMode ? null : StringBuilderPool.Shared.Get();

        try
        {
            if (!_paramMode)
            {
                sqlBuilder!.Append(join.JoinType switch
                {
                    JoinType.Inner => " join ",
                    JoinType.Left => " left join ",
                    JoinType.Right => " right join ",
                    JoinType.Full => " full join ",
                    JoinType.Cross => " cross join ",
                    JoinType.FullCross => " cross join ",
                    _ => throw new NotSupportedException(join.JoinType.ToString())
                });
            }

            var joinCondition = join.JoinCondition;

            if (joinCondition is null)
            {
                var fromSql = MakeFrom(join.From, true, join.EntityType ?? join.From.SourceType, false);
                if (!_paramMode)
                {
                    sqlBuilder!.Append(fromSql);
                }

                return _paramMode ? null : sqlBuilder!.ToString();
            }

            // A scope is pushed only when the condition's parameters contain a repeated type. Doing
            // this with a double loop avoids the Select/Distinct LINQ allocations on every build.
            var joinParameters = joinCondition.Parameters;
            var scopedAdded = false;
            for (var i = 1; i < joinParameters.Count && !scopedAdded; i++)
            {
                var type = joinParameters[i].Type;
                for (var j = 0; j < i; j++)
                {
                    if (joinParameters[j].Type == type)
                    {
                        scopedAdded = true;
                        break;
                    }
                }
            }
            if (scopedAdded)
                _columnsProvider.PushScope(joinCondition.Parameters);

            try
            {
                var dim = 1;
                if (joinCondition.Parameters[0].Type.TryGetProjectionDimension(out var joinDim))
                    dim = joinDim;

                // FromExpression fromExp;
                // if (visitor.JoinType is not null)
                // {
                //     if (visitor.JoinType.IsAnonymous())
                //     {
                //         //fromExp = GetFrom(join.Query.EntityType);
                //         var sql = MakeSelect(join.Query!);

                //         _columnsProvider.Add(join.Query!, false);

                //         if (!_paramMode)
                //         {
                //             sqlBuilder!.Append('(').Append(sql).Append(") ");
                //             sqlBuilder.Append(_aliasProvider.GetNextAlias(join.Query!));
                //         }
                //     }
                //     else
                //     {
                //         fromExp = _dialect.GetFrom(visitor.JoinType);

                //         // if (!_paramMode)
                //         //     fromExp.TableAlias = GetAliasFromProjection(entityType, visitor.JoinType, dim);

                //         var fromSql = MakeFrom(fromExp, true, visitor.JoinType, false);
                //         if (!_paramMode)
                //         {
                //             sqlBuilder!.Append(fromSql);
                //         }
                //     }
                // }

                var fromSql = MakeFrom(join.From, true, joinCondition.Parameters[1].Type, false);
                if (!_paramMode)
                {
                    sqlBuilder!.Append(fromSql);
                }

                if (!_paramMode) sqlBuilder!.Append(" on ");
                MakeWhere(sqlBuilder, entityType, joinCondition.Body, dim);
            }
            finally
            {
                if (scopedAdded)
                    _columnsProvider.PopScope();
            }

            return _paramMode ? null : sqlBuilder!.ToString();
        }
        finally
        {
            if (sqlBuilder is not null)
                StringBuilderPool.Shared.Return(sqlBuilder);
        }
    }
    private void MakeWhere(StringBuilder? target, Type entityType, Expression condition, int dim)
    {
        // if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Where expression: {exp}", condition);
        using var visitor = new WhereExpressionVisitor(entityType, _dialect, _columnsProvider, dim, _aliasProvider, _paramProvider, _queryProvider, _paramMode, _params, Logger);
        visitor.Visit(condition);

        // In parameter mode nothing is emitted (the walk still collects parameters); otherwise the
        // rendered clause is appended straight into the caller's builder, avoiding a temp string.
        if (_paramMode || target is null) return;

        visitor.WriteTo(target);
    }
    private string MakeSort(Type entityType, Expression sorting, int dim)
    {
        // if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Where expression: {exp}", condition);
        using var visitor = new BaseExpressionVisitor(entityType, _dialect, _columnsProvider, dim, _aliasProvider, _paramProvider, _queryProvider, true, _paramMode, _params, Logger);
        visitor.Visit(sorting);

        if (_paramMode) return string.Empty;

        return visitor.ToString();
    }
    public (bool NeedAliasForColumn, string Column) MakeColumn(SelectExpression selExp, Type entityType, bool dontNeedAlias, bool renameAware = false)
    {
        using var visitor = new BaseExpressionVisitor(entityType, _dialect, _columnsProvider, 0, _aliasProvider, _paramProvider, _queryProvider, dontNeedAlias, _paramMode, _params, Logger);
        visitor.Visit(selExp.Expression);

        if (_paramMode) return (false, string.Empty);

        // A projected column must be aliased when its property name differs from the column it is
        // read from (MyId = t.Id, or two sources exposing the same physical name). Without it an
        // outer query that references the projection by property name emits the wrong identifier,
        // which PostgreSQL rejects as missing or ambiguous. renameAware is only set by callers that
        // emit a select list - GROUP BY must keep the physical column name.
        var needAliasForColumn = visitor.NeedAliasForColumn
            || (renameAware
                && visitor.ColumnName is not null
                && selExp.PropertyName is not null
                && !string.Equals(visitor.ColumnName, selExp.PropertyName, StringComparison.OrdinalIgnoreCase));

        return (needAliasForColumn, visitor.ToString());
    }
    public string MakeFrom(FromExpression from, bool needAlias, Type? entityType, bool hasJoins)
    {
        if (from.TableFunction is not null)
            return MakeTableFunction(from, needAlias, entityType, hasJoins);

        if (!_paramMode && !string.IsNullOrEmpty(from.Table))
        {
            var sqlBuilder = StringBuilderPool.Shared.Get();
            try
            {
                sqlBuilder.Append(from.Table);

                if (needAlias)
                {
                    if (hasJoins)
                    {
                        Debug.Assert(typeof(IProjection).IsAssignableFrom(entityType));
                        _columnsProvider.Add(entityType!.GetGenericArguments()[0], false);
                    }
                    else
                        _columnsProvider.Add(entityType!, false);

                    sqlBuilder.Append(_dialect.MakeTableAlias(_aliasProvider!.GetNextAlias(from)));
                }

                return sqlBuilder.ToString();
            }
            finally
            {
                StringBuilderPool.Shared.Return(sqlBuilder);
            }
        }
        else
        {
            var cmd = from.SubQuery;

            if (cmd is not null)
            {
#if DEBUG
                if (!cmd.IsPrepared) throw new BuildSqlCommandException("Inner query is not prepared");
#endif
                var sql = MakeSelect(cmd);

                if (_paramMode) return string.Empty;

                _columnsProvider.Add(cmd, false);

                var sqlBuilder = StringBuilderPool.Shared.Get();
                try
                {
                    sqlBuilder.Append('(').Append(sql).Append(')');
                    if (needAlias || _dialect.RequireSubqueryAlias)
                    {
                        sqlBuilder.Append(_dialect.MakeTableAlias(_aliasProvider!.GetNextAlias(from)));
                    }

                    return sqlBuilder.ToString();
                }
                finally
                {
                    StringBuilderPool.Shared.Return(sqlBuilder);
                }
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// Renders a table-valued function call (<c>[schema.]name(arg1, arg2, ...)</c>) as a FROM source.
    /// The arguments go through the regular expression visitor, so captured values become parameters
    /// exactly like everywhere else; the parameter pass walks them (in the same order) without
    /// emitting text. The function is aliased like a derived table when a join or the dialect requires
    /// it (<see cref="ISqlDialect.RequireSubqueryAlias"/>).
    /// </summary>
    private string MakeTableFunction(FromExpression from, bool needAlias, Type? entityType, bool hasJoins)
    {
        var function = from.TableFunction!;
        var arguments = function.Arguments;

        if (_paramMode)
        {
            for (var (i, cnt) = (0, arguments.Count); i < cnt; i++)
            {
                using var visitor = new BaseExpressionVisitor(entityType ?? typeof(object), _dialect, _columnsProvider, 0, _aliasProvider, _paramProvider, _queryProvider, true, true, _params, Logger);
                visitor.Visit(arguments[i]);
            }

            return string.Empty;
        }

        var sqlBuilder = StringBuilderPool.Shared.Get();
        try
        {
            sqlBuilder.Append(_dialect.MakeFunction(function.Name, function.Schema)).Append('(');

            for (var (i, cnt) = (0, arguments.Count); i < cnt; i++)
            {
                if (i > 0)
                    sqlBuilder.Append(", ");

                using var visitor = new BaseExpressionVisitor(entityType ?? typeof(object), _dialect, _columnsProvider, 0, _aliasProvider, _paramProvider, _queryProvider, true, false, _params, Logger);
                visitor.Visit(arguments[i]);
                sqlBuilder.Append(visitor.ToString());
            }

            sqlBuilder.Append(')');

            if (needAlias || _dialect.RequireSubqueryAlias)
            {
                if (hasJoins)
                {
                    Debug.Assert(typeof(IProjection).IsAssignableFrom(entityType));
                    _columnsProvider.Add(entityType!.GetGenericArguments()[0], false);
                }
                else if (entityType is not null)
                    _columnsProvider.Add(entityType, false);

                sqlBuilder.Append(_dialect.MakeTableAlias(_aliasProvider!.GetNextAlias(from)));
            }

            return sqlBuilder.ToString();
        }
        finally
        {
            StringBuilderPool.Shared.Return(sqlBuilder);
        }
    }
}