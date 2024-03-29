using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace nextorm.core;

public struct SqlBuilder
{
    private readonly static ObjectPool<StringBuilder> _sbPool = new DefaultObjectPoolProvider().Create(new StringBuilderPooledObjectPolicy());
    private readonly DbContext _dbContext;
    private readonly IColumnsProvider _columnsProvider;
    private readonly IAliasProvider? _aliasProvider;
    private readonly IParamProvider _paramProvider;
    private readonly IQueryProvider _queryProvider;
    private readonly Stack<(IColumnsProvider, ReadOnlyCollection<ParameterExpression>)> _scope;
    private readonly List<Param> _params;
    private readonly bool _paramMode;
    public SqlBuilder(DbContext dbContext, bool paramMode, List<Param> @params, IColumnsProvider columnsProvider, IQueryProvider queryProvider, IParamProvider paramProvider, IAliasProvider? aliasProvider, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(@params);

        _dbContext = dbContext;
        _paramMode = paramMode;
        _params = @params;
        _columnsProvider = columnsProvider;
        _queryProvider = queryProvider;
        _paramProvider = paramProvider;
        _aliasProvider = aliasProvider;
        // _scope = paramScope;
        Logger = logger;
    }
    public ILogger Logger { get; }
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

        var sqlBuilder = _paramMode ? null : _sbPool.Get();
        string? topStmt = null;

        var pageApplied = !_paramMode && !(cmd.IgnoreColumns || selectList is null) && cmd.Paging.IsTop && _dbContext.MakeTop(cmd.Paging.Limit, out topStmt);

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
                var whereSql = MakeWhere(entityType, cmd.PreparedCondition, 0);
                if (!_paramMode) sqlBuilder!.AppendLine().Append(" where ").Append(whereSql);
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
                            sqlBuilder.Append(_dbContext.MakeColumnAlias(item.PropertyName));
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
                    var havingSql = MakeWhere(entityType, cmd.Having, 0);
                    if (!_paramMode) sqlBuilder!.AppendLine().Append(" having ").Append(havingSql);
                }
            }

            if (cmd.UnionQuery is not null)
            {
                if (!_paramMode)
                {
                    sqlBuilder!.AppendLine().Append(cmd.UnionType switch
                    {
                        UnionType.Distinct => " union ",
                        UnionType.All => " union all ",
                        _ => throw new NotSupportedException(cmd.UnionType.ToString("G"))
                    }).AppendLine();
                }

                var builder = new SqlBuilder(_dbContext, _paramMode, _params, _columnsProvider, _queryProvider, _paramProvider, new DefaultAliasProvider(), Logger);
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
            else if (!pageApplied && _dbContext.RequireSorting(cmd) && !_paramMode)
            {
                sqlBuilder!.AppendLine().Append(" order by ").Append(_dbContext.EmptySorting());
            }

            if (!pageApplied && !_paramMode && !cmd.Paging.IsEmpty)
            {
                sqlBuilder!.AppendLine();
                _dbContext.MakePage(cmd.Paging, sqlBuilder);
            }
        }
        else if (!_paramMode && sqlBuilder!.Length > 0)
            sqlBuilder.Length -= 2;


        var selectBuilder = _paramMode ? null : _sbPool.Get();

        if (!_paramMode) selectBuilder!.Append("select ");
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
                var (needAliasForColumn, column) = MakeColumn(item, entityType, !needAlias);

                if (!_paramMode)
                {
                    selectBuilder!.Append(column);

                    if (needAliasForColumn)
                    {
                        selectBuilder.Append(_dbContext.MakeColumnAlias(item.PropertyName));
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
            _sbPool.Return(selectBuilder);

            r = sqlBuilder!.ToString();

            _sbPool.Return(sqlBuilder);
        }

#if DEBUG
        if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Generated sql with param mode {mode}: {sql}", _paramMode, r);
#endif

        return r;
    }
    public string? MakeJoin(JoinExpression join, Type entityType)
    {
        var sqlBuilder = _paramMode ? null : _sbPool.Get();

        if (!_paramMode)
        {
            switch (join.JoinType)
            {
                case JoinType.Inner:
                    sqlBuilder!.Append(" join ");
                    break;
                default:
                    throw new NotImplementedException(join.JoinType.ToString());
            }
        }

        // var visitor = new JoinExpressionVisitor();
        // visitor.Visit(join.JoinCondition);

        var scopedAdded = join.JoinCondition.Parameters.Count > join.JoinCondition.Parameters.Select(it => it.Type).Distinct().Count();
        if (scopedAdded)
            _columnsProvider.PushScope(join.JoinCondition.Parameters);

        try
        {
            var dim = 1;
            if (join.JoinCondition.Parameters[0].Type.TryGetProjectionDimension(out var joinDim))
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
            //         fromExp = _dbContext.GetFrom(visitor.JoinType);

            //         // if (!_paramMode)
            //         //     fromExp.TableAlias = GetAliasFromProjection(entityType, visitor.JoinType, dim);

            //         var fromSql = MakeFrom(fromExp, true, visitor.JoinType, false);
            //         if (!_paramMode)
            //         {
            //             sqlBuilder!.Append(fromSql);
            //         }
            //     }
            // }

            var fromSql = MakeFrom(join.From, true, join.JoinCondition.Parameters[1].Type, false);
            if (!_paramMode)
            {
                sqlBuilder!.Append(fromSql);
            }

            var whereSql = MakeWhere(entityType, join.JoinCondition.Body, dim);

            if (!_paramMode)
            {
                sqlBuilder!.Append(" on ");

                sqlBuilder.Append(whereSql);
            }
        }
        finally
        {
            if (scopedAdded)
                _columnsProvider.PopScope();
        }

        string? r = null;

        if (!_paramMode)
        {
            r = sqlBuilder!.ToString();

            _sbPool.Return(sqlBuilder);
        }

        return r;
    }
    private string MakeWhere(Type entityType, Expression condition, int dim)
    {
        // if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Where expression: {exp}", condition);
        using var visitor = new WhereExpressionVisitor(entityType, _dbContext, _columnsProvider, dim, _aliasProvider, _paramProvider, _queryProvider, _paramMode, _params, Logger);
        visitor.Visit(condition);

        if (_paramMode) return string.Empty;

        return visitor.ToString();
    }
    private string MakeSort(Type entityType, Expression sorting, int dim)
    {
        // if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Where expression: {exp}", condition);
        using var visitor = new BaseExpressionVisitor(entityType, _dbContext, _columnsProvider, dim, _aliasProvider, _paramProvider, _queryProvider, true, _paramMode, _params, Logger);
        visitor.Visit(sorting);

        if (_paramMode) return string.Empty;

        return visitor.ToString();
    }
    public (bool NeedAliasForColumn, string Column) MakeColumn(SelectExpression selExp, Type entityType, bool dontNeedAlias)
    {
        using var visitor = new BaseExpressionVisitor(entityType, _dbContext, _columnsProvider, 0, _aliasProvider, _paramProvider, _queryProvider, dontNeedAlias, _paramMode, _params, Logger);
        visitor.Visit(selExp.Expression);

        if (_paramMode) return (false, string.Empty);

        return (visitor.NeedAliasForColumn, visitor.ToString());
    }
    public string MakeFrom(FromExpression from, bool needAlias, Type? entityType, bool hasJoins)
    {
        if (!_paramMode && !string.IsNullOrEmpty(from.Table))
        {
            var sqlBuilder = _sbPool.Get();
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

                sqlBuilder.Append(_dbContext.MakeTableAlias(_aliasProvider!.GetNextAlias(from)));
            }
            var r = sqlBuilder.ToString();
            _sbPool.Return(sqlBuilder);
            return r;
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

                var sqlBuilder = _sbPool.Get();
                sqlBuilder.Append('(').Append(sql).Append(')');
                if (needAlias)
                {
                    sqlBuilder.Append(_dbContext.MakeTableAlias(_aliasProvider!.GetNextAlias(from)));
                }
                var r = sqlBuilder.ToString();
                _sbPool.Return(sqlBuilder);
                return r;
            }
        }
        return string.Empty;
    }
}