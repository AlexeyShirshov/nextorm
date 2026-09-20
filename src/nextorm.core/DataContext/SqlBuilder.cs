using System.Text;
using Microsoft.Extensions.Logging;

namespace NextORM.Core;

/// <summary>
/// Builds SQL text and collects parameters for one command using the active dialect and providers.
/// Statement assembly lives here; source/condition rendering is delegated to
/// <see cref="SqlSourceRenderer"/>.
/// </summary>
/// <remarks>
/// Implementation detail (note the nine-parameter constructor), intentionally kept <c>internal</c>.
/// See <c>docs/specs/design/API-NAMING-REVIEW.md</c> finding P1-18.
/// </remarks>
internal readonly struct SqlBuilder
{
    private readonly SqlBuildContext _ctx;

    public SqlBuilder(VisitorOptions options)
        : this(new SqlBuildContext(options))
    {
    }

    public SqlBuilder(in SqlBuildContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx.Params);

        _ctx = ctx;
    }

    public ILogger? Logger => _ctx.Logger;

    public string? MakeSelect(QueryCommand cmd)
    {
#if DEBUG
        if (!cmd.IsPrepared)
            throw new InvalidOperationException("Command not prepared");

        if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Making sql with param mode {mode}", _ctx.ParamMode);
#endif
        var entityType = cmd.EntityType;
        ArgumentNullException.ThrowIfNull(entityType);

        var selectList = cmd.SelectList;
        var from = cmd.From;
        var ctes = cmd.Ctes;

        var sqlBuilder = _ctx.ParamMode ? null : StringBuilderPool.Shared.Get();
        var selectBuilder = _ctx.ParamMode ? null : StringBuilderPool.Shared.Get();
        string? topStmt = null;
        string? withClause = null;
        string? maxRecursionStmt = null;

        // Only the source entries this command adds may satisfy its own alias lookups. Without the
        // boundary a nested command over the same entity type as a sibling (or as the outer query)
        // resolves to that earlier alias (e.g. "from x as 't3' where t2.id = ...").
        _ctx.ColumnsProvider.PushSourceScope();

        try
        {
            // CTEs are rendered (or, in parameter mode, only walked for parameters) before the outer
            // statement so both SQL and parameter passes see the CTE parameters first and in the same order.
            if (ctes is { Count: > 0 })
                withClause = SqlSourceRenderer.MakeWithClause(in _ctx, ctes, out maxRecursionStmt);

            if (cmd.Paging.HasWithTies && !_ctx.Dialect.SupportsWithTies)
                throw new NotSupportedException("The WITH TIES page modifier is not supported by this SQL dialect");

            if (cmd.Paging.HasWithTies && cmd.Paging.Limit <= 0)
                throw new BuildSqlCommandException("WITH TIES requires a positive page limit.");

            if (cmd.Paging.HasWithTies && (cmd.IsDistinct || cmd.DistinctOn is not null))
                throw new BuildSqlCommandException("WITH TIES cannot be combined with DISTINCT or DISTINCT ON.");

            var pageApplied = !_ctx.ParamMode && !(cmd.IgnoreColumns || selectList is null) && cmd.Paging.IsTop && _ctx.Dialect.MakeTop(cmd.Paging.Limit, cmd.Paging.HasWithTies, out topStmt);

            var joins = cmd.Joins;
            var hasJoins = joins?.Length > 0;
            var needAlias = hasJoins || _ctx.QueryProvider.OuterReferences?.Count > 0;

            if (from is not null)
            {
                if (cmd.Temporal is { } temporal)
                {
                    if (!_ctx.Dialect.SupportsTemporalTable)
                        throw new NotSupportedException("The FOR SYSTEM_TIME clause is not supported by this SQL dialect");

                    if (!_ctx.Dialect.SupportsTemporalKind(temporal.Kind))
                        throw new NotSupportedException($"The FOR SYSTEM_TIME {temporal.Kind} clause is not supported by this SQL dialect");
                }

                var fromStr = SqlSourceRenderer.MakeFrom(in _ctx, from, new FromRenderOptions(needAlias, entityType, hasJoins, cmd.TableHints, cmd.Temporal));
                if (!_ctx.ParamMode)
                {
                    sqlBuilder!.Append(" from ").Append(fromStr);

                    if (cmd.TableSample is { } tablesample)
                    {
                        if (!_ctx.Dialect.SupportsTableSample)
                            throw new NotSupportedException("The TABLESAMPLE modifier is not supported by this SQL dialect");

                        if (!_ctx.Dialect.SupportsTableSampleMethod(tablesample.Method))
                            throw new NotSupportedException($"The TABLESAMPLE {tablesample.Method} sampling method is not supported by this SQL dialect");

                        sqlBuilder.Append(_ctx.Dialect.MakeTableSample(tablesample.Method, tablesample.Percent, tablesample.Seed));
                    }

                    if (cmd.Final)
                    {
                        if (!_ctx.Dialect.SupportsFinal)
                            throw new NotSupportedException("The FINAL modifier is not supported by this SQL dialect");

                        sqlBuilder.Append(_ctx.Dialect.MakeFinal());
                    }

                    if (cmd.SampleRatio is { } sampleRatio)
                    {
                        if (!_ctx.Dialect.SupportsSample)
                            throw new NotSupportedException("The SAMPLE modifier is not supported by this SQL dialect");

                        sqlBuilder.Append(_ctx.Dialect.MakeSample(sampleRatio, cmd.SampleOffset));
                    }
                }

                if (hasJoins)
                {
                    for (var (idx, cnt) = (0, joins!.Length); idx < cnt; idx++)
                    {
                        var joinSql = SqlSourceRenderer.MakeJoin(in _ctx, joins[idx], entityType!);
                        if (!_ctx.ParamMode) sqlBuilder!.Append(joinSql);
                    }
                }

                if (cmd.ArrayJoinExpressions is { Count: > 0 } arrayJoinExpressions)
                {
                    if (!_ctx.Dialect.SupportsArrayJoinClause)
                        throw new NotSupportedException("The ARRAY JOIN clause is not supported by this SQL dialect");

                    var renderedArrayJoins = _ctx.ParamMode ? null : new string[arrayJoinExpressions.Count];

                    for (var i = 0; i < arrayJoinExpressions.Count; i++)
                    {
                        var expressionSql = SqlSourceRenderer.MakeArrayJoin(in _ctx, entityType, arrayJoinExpressions[i]);
                        if (!_ctx.ParamMode)
                        {
                            // The bound element is referenced by the projection (p.Element), so the
                            // last expression of the clause carries the alias the translator emits.
                            renderedArrayJoins![i] = cmd.BindArrayJoinElement && i == arrayJoinExpressions.Count - 1
                                ? expressionSql + " as " + ArrayJoinNames.ElementAlias
                                : expressionSql;
                        }
                    }

                    if (!_ctx.ParamMode)
                        sqlBuilder!.AppendLine().Append(_ctx.Dialect.MakeArrayJoin(cmd.ArrayJoinKind, renderedArrayJoins!));
                }

                if (cmd.PreparedPreWhere is not null)
                {
                    if (!_ctx.Dialect.SupportsPreWhere)
                        throw new NotSupportedException("The PREWHERE clause is not supported by this SQL dialect");

                    if (!_ctx.ParamMode) sqlBuilder!.AppendLine().Append(" prewhere ");
                    SqlSourceRenderer.MakeWhere(in _ctx, sqlBuilder, entityType, cmd.PreparedPreWhere, 0);
                }
                if (cmd.PreparedCondition is not null)
                {
                    if (!_ctx.ParamMode) sqlBuilder!.AppendLine().Append(" where ");
                    SqlSourceRenderer.MakeWhere(in _ctx, sqlBuilder, entityType, cmd.PreparedCondition, 0);
                }

                var grouping = cmd.GroupingList;
                if (grouping?.Length > 0)
                {
                    if (cmd.GroupingType == GroupingType.Rollup && !_ctx.Dialect.SupportsRollup)
                        throw new NotSupportedException("The ROLLUP grouping modifier is not supported by this SQL dialect");

                    if (cmd.GroupingType == GroupingType.Cube && !_ctx.Dialect.SupportsCube)
                        throw new NotSupportedException("The CUBE grouping modifier is not supported by this SQL dialect");

                    if (cmd.GroupingType == GroupingType.GroupingSets && !_ctx.Dialect.SupportsGroupingSets)
                        throw new NotSupportedException("The GROUPING SETS modifier is not supported by this SQL dialect");

                    var groupingListCount = grouping.Length;
                    var columns = _ctx.ParamMode ? null : new string[groupingListCount];

                    for (var i = 0; i < groupingListCount; i++)
                    {
                        var item = grouping[i];

                        // GROUP BY repeats the grouping expression; an `AS alias` is not valid here on
                        // any supported dialect (the alias belongs to the SELECT list only).
                        var (_, column) = SqlSourceRenderer.MakeColumn(in _ctx, item, entityType, false);

                        if (!_ctx.ParamMode)
                            columns![i] = column;
                    }

                    if (!_ctx.ParamMode)
                    {
                        if (cmd.GroupByWithTotals)
                        {
                            if (!_ctx.Dialect.SupportsGroupByWithTotals)
                                throw new NotSupportedException("The GROUP BY ... WITH TOTALS modifier is not supported by this SQL dialect");

                            if (cmd.GroupingType == GroupingType.GroupingSets)
                                throw new NotSupportedException("GROUP BY ... WITH TOTALS cannot be combined with GROUPING SETS.");
                        }

                        sqlBuilder!.AppendLine().Append(" group by ");

                        string groupingSql;
                        if (cmd.GroupingType == GroupingType.GroupingSets)
                        {
                            var sets = cmd.GroupingSets
                                ?? throw new BuildSqlCommandException("GROUPING SETS requires at least one set.");

                            var rendered = new string[sets.Count];
                            for (var s = 0; s < sets.Count; s++)
                            {
                                var set = sets[s];
                                var parts = new string[set.Length];
                                for (var i = 0; i < set.Length; i++)
                                {
                                    if (set[i] < 0 || set[i] >= groupingListCount)
                                        throw new BuildSqlCommandException($"Grouping set index {set[i]} is out of range 0..{groupingListCount - 1}.");

                                    parts[i] = columns![set[i]];
                                }

                                rendered[s] = "(" + string.Join(", ", parts) + ")";
                            }

                            groupingSql = _ctx.Dialect.MakeGroupingSets(rendered);
                        }
                        else
                        {
                            groupingSql = _ctx.Dialect.MakeGrouping(string.Join(", ", columns!), cmd.GroupingType);
                        }

                        if (cmd.GroupByWithTotals)
                            groupingSql = _ctx.Dialect.MakeGroupByTotals(groupingSql);

                        sqlBuilder.Append(groupingSql);
                    }

                    if (cmd.Having is not null)
                    {
                        if (!_ctx.ParamMode) sqlBuilder!.AppendLine().Append(" having ");
                        SqlSourceRenderer.MakeWhere(in _ctx, sqlBuilder, entityType, cmd.Having, 0);
                    }
                }

                if (cmd.UnionQuery is not null)
                {
                    if (cmd.UnionType is UnionType.IntersectAll or UnionType.ExceptAll && !_ctx.Dialect.SupportsIntersectExceptAll)
                        throw new NotSupportedException($"The {cmd.UnionType} set operation is not supported by this SQL dialect");

                    if (!_ctx.ParamMode)
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

                    var builder = new SqlBuilder(_ctx with { AliasProvider = new DefaultAliasProvider() });
                    var sql = builder.MakeSelect(cmd.UnionQuery);
                    if (!_ctx.ParamMode) sqlBuilder!.Append(sql);
                }

                var sortingList = cmd.Sorting;
                if (sortingList is not null)
                {
                    if (!_ctx.ParamMode) sqlBuilder!.AppendLine().Append(" order by ");

                    for (var (i, cnt) = (0, sortingList.Length); i < cnt; i++)
                    {
                        var sorting = sortingList[i];
                        if (sorting.PreparedExpression is not null)
                        {
                            var sortingSql = SqlSourceRenderer.MakeSort(in _ctx, entityType, sorting.PreparedExpression, 0);
                            if (!_ctx.ParamMode)
                            {
                                sqlBuilder!.Append(sortingSql);
                                if (sorting.Direction == OrderDirection.Desc)
                                    sqlBuilder.Append(" desc");

                                sqlBuilder.Append(", ");
                            }
                        }
                        else if (!_ctx.ParamMode && sorting.ColumnIndex.HasValue)
                        {
                            sqlBuilder!.Append(sorting.ColumnIndex);
                            if (sorting.Direction == OrderDirection.Desc)
                                sqlBuilder.Append(" desc");

                            sqlBuilder.Append(", ");
                        }
                    }

                    if (!_ctx.ParamMode) sqlBuilder!.Length -= 2;
                }
                else if (!pageApplied && !_ctx.ParamMode && _ctx.Dialect.GetPagingOrderBy(cmd) is { } pagingOrderBy)
                {
                    sqlBuilder!.AppendLine().Append(" order by ").Append(pagingOrderBy);
                }

                if (cmd.LimitBy is { } limitBy)
                {
                    if (!_ctx.Dialect.SupportsLimitBy)
                        throw new NotSupportedException("The LIMIT BY clause is not supported by this SQL dialect");

                    var limitByColumns = cmd.LimitByColumns;
                    var renderedLimitBy = _ctx.ParamMode ? null : new string[limitByColumns?.Length ?? 0];

                    if (limitByColumns is not null)
                    {
                        for (var i = 0; i < limitByColumns.Length; i++)
                        {
                            // LIMIT BY takes an expression list, not a select list, so no column alias.
                            var (_, column) = SqlSourceRenderer.MakeColumn(in _ctx, limitByColumns[i], entityType, false);
                            if (!_ctx.ParamMode)
                                renderedLimitBy![i] = column;
                        }
                    }

                    if (!_ctx.ParamMode)
                    {
                        sqlBuilder!.AppendLine();
                        _ctx.Dialect.MakeLimitBy(limitBy.Limit, limitBy.Offset, renderedLimitBy!, sqlBuilder);
                    }
                }

                if (cmd.Settings is { Count: > 0 } settings)
                {
                    if (!_ctx.Dialect.SupportsSettings)
                        throw new NotSupportedException("The SETTINGS clause is not supported by this SQL dialect");

                    if (!_ctx.ParamMode)
                    {
                        sqlBuilder!.AppendLine();
                        sqlBuilder.Append(_ctx.Dialect.MakeSettings(settings));
                    }
                }

                if (!pageApplied && !_ctx.ParamMode && !cmd.Paging.IsEmpty)
                {
                    sqlBuilder!.AppendLine();
                    _ctx.Dialect.MakePage(cmd.Paging, sqlBuilder);
                }
            }
            else if (!_ctx.ParamMode && sqlBuilder!.Length > 0)
                sqlBuilder.Length -= 2;

            if (!_ctx.ParamMode && cmd.RowLock is { } rowLock)
            {
                if (!_ctx.Dialect.SupportsLocking)
                    throw new NotSupportedException("The FOR UPDATE/FOR SHARE clause is not supported by this SQL dialect");

                sqlBuilder!.AppendLine().Append(_ctx.Dialect.MakeLock(rowLock.Mode));
            }


            if (!_ctx.ParamMode)
            {
                selectBuilder!.Append("select ");

                if (cmd.DistinctOn is not null)
                {
                    if (!_ctx.Dialect.SupportsDistinctOn)
                        throw new NotSupportedException("The DISTINCT ON clause is not supported by this SQL dialect");

                    var distinctOnColumns = cmd.DistinctOnColumns;
                    var renderedDistinctOn = new string[distinctOnColumns?.Length ?? 0];

                    if (distinctOnColumns is not null)
                    {
                        for (var i = 0; i < distinctOnColumns.Length; i++)
                        {
                            // DISTINCT ON takes an expression list, not a select list, so no column alias.
                            var (_, column) = SqlSourceRenderer.MakeColumn(in _ctx, distinctOnColumns[i], entityType, false);
                            renderedDistinctOn[i] = column;
                        }
                    }

                    selectBuilder.Append(_ctx.Dialect.MakeDistinctOn(renderedDistinctOn));
                }
                // SQL Server renders the limit as TOP(n); DISTINCT has to precede it
                // ("select distinct top(n) ..."), so the flag is emitted before the select-list branch.
                else if (cmd.IsDistinct)
                    selectBuilder.Append("distinct ");
            }
            if (cmd.IgnoreColumns || selectList is null)
            {
                if (!_ctx.ParamMode) selectBuilder!.Append("*, ");
            }
            else
            {
                if (!_ctx.ParamMode && !string.IsNullOrEmpty(topStmt))
                {
                    selectBuilder!.Append(topStmt).Append(' ');
                }

                var selectListCount = selectList.Length;
                for (var i = 0; i < selectListCount; i++)
                {
                    var item = selectList[i];
                    var (needAliasForColumn, column) = SqlSourceRenderer.MakeColumn(in _ctx, item, entityType, !needAlias, renameAware: true);

                    if (!_ctx.ParamMode)
                    {
                        selectBuilder!.Append(column);

                        if (needAliasForColumn)
                        {
                            selectBuilder.Append(_ctx.Dialect.MakeColumnAlias(item.PropertyName));
                        }

                        selectBuilder.Append(", ");
                    }
                }
            }


            string? r = null;
            if (!_ctx.ParamMode)
            {
                selectBuilder!.Length -= 2;
                sqlBuilder!.Insert(0, selectBuilder!.ToString());

                if (withClause is not null)
                    sqlBuilder!.Insert(0, withClause);

                // FOR JSON / FOR XML come after ORDER BY but before the trailing OPTION clause.
                if (cmd.ForJsonClause is not null && cmd.ForXmlClause is not null)
                    throw new NotSupportedException("FOR JSON and FOR XML cannot be combined.");

                if (cmd.ForJsonClause is { } forJson)
                {
                    if (!_ctx.Dialect.SupportsForJson)
                        throw new NotSupportedException("FOR JSON is not supported by this SQL dialect");

                    sqlBuilder!.Append(' ').Append(_ctx.Dialect.MakeForJson(forJson));
                }

                if (cmd.ForXmlClause is { } forXml)
                {
                    if (!_ctx.Dialect.SupportsForXml)
                        throw new NotSupportedException("FOR XML is not supported by this SQL dialect");

                    sqlBuilder!.Append(' ').Append(_ctx.Dialect.MakeForXml(forXml));
                }

                var hints = cmd.Hints;
                if (hints is { Count: > 0 })
                {
                    if (!_ctx.Dialect.SupportsQueryHints)
                        throw new NotSupportedException("Query hints are not supported by this SQL dialect");

                    // The dialect owns the final placement. It also receives the CTE maxrecursion
                    // option so a dialect that must coalesce it into one trailing OPTION clause
                    // (SQL Server) can do so instead of the caller appending a second clause.
                    r = _ctx.Dialect.RenderQueryHints(sqlBuilder!.ToString(), hints, maxRecursionStmt);
                }
                else
                {
                    if (maxRecursionStmt is not null)
                        sqlBuilder!.Append(' ').Append(maxRecursionStmt);

                    r = sqlBuilder!.ToString();
                }
            }


#if DEBUG
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Generated sql with param mode {mode}: {sql}", _ctx.ParamMode, r);
#endif

            return r;
        }
        finally
        {
            _ctx.ColumnsProvider.PopSourceScope();

            if (selectBuilder is not null)
                StringBuilderPool.Shared.Return(selectBuilder);

            if (sqlBuilder is not null)
                StringBuilderPool.Shared.Return(sqlBuilder);
        }
    }

    public (bool NeedAliasForColumn, string Column) MakeColumn(SelectExpression selExp, Type entityType, bool dontNeedAlias, bool renameAware = false)
        => SqlSourceRenderer.MakeColumn(in _ctx, selExp, entityType, dontNeedAlias, renameAware);
}
