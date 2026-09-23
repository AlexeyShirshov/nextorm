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

            var pageApplied = !_ctx.ParamMode && !(cmd.IgnoreColumns || selectList is null) && cmd.Paging.IsTop && _ctx.Dialect.MakeTop(cmd.Paging.Limit, cmd.Paging.HasWithTies, out topStmt, _ctx.KeywordCase);

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

                var tableHints = cmd.TableHints;
                if (cmd.RowLock is { } lockClause && _ctx.Dialect.Lock is { UsesTableHints: true } lockHint)
                    tableHints = AppendHint(tableHints, lockHint.Render(lockClause.Mode, _ctx.KeywordCase));

                var fromStr = SqlSourceRenderer.MakeFrom(in _ctx, from, new FromRenderOptions(needAlias, entityType, hasJoins, tableHints, cmd.Temporal, cmd.IndexHints, cmd.IndexHintKind));
                if (!_ctx.ParamMode)
                {
                    sqlBuilder!.Append(Kw(" from ")).Append(fromStr);

                    if (cmd.TableSample is { } tablesample)
                    {
                        if (_ctx.Dialect.TableSample is not { } tableSample)
                            throw new NotSupportedException("The TABLESAMPLE modifier is not supported by this SQL dialect");

                        if (!tableSample.Supports(tablesample.Method))
                            throw new NotSupportedException($"The TABLESAMPLE {tablesample.Method} sampling method is not supported by this SQL dialect");

                        sqlBuilder.Append(tableSample.Render(tablesample.Method, tablesample.Percent, tablesample.Seed, _ctx.KeywordCase));
                    }

                    if (cmd.Final)
                    {
                        if (!_ctx.Dialect.SupportsFinal)
                            throw new NotSupportedException("The FINAL modifier is not supported by this SQL dialect");

                        sqlBuilder.Append(_ctx.Dialect.MakeFinal(_ctx.KeywordCase));
                    }

                    if (cmd.SampleRatio is { } sampleRatio)
                    {
                        if (!_ctx.Dialect.SupportsSample)
                            throw new NotSupportedException("The SAMPLE modifier is not supported by this SQL dialect");

                        sqlBuilder.Append(_ctx.Dialect.MakeSample(sampleRatio, cmd.SampleOffset, _ctx.KeywordCase));
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
                    if (_ctx.Dialect.ArrayJoinClause is not { } arrayJoinClause)
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
                                ? expressionSql + Kw(" as ") + ArrayJoinNames.ElementAlias
                                : expressionSql;
                        }
                    }

                    if (!_ctx.ParamMode)
                        sqlBuilder!.AppendLine().Append(arrayJoinClause.Render(cmd.ArrayJoinKind, renderedArrayJoins!, _ctx.KeywordCase));
                }

                if (cmd.PreparedPreWhere is not null)
                {
                    if (!_ctx.Dialect.SupportsPreWhere)
                        throw new NotSupportedException("The PREWHERE clause is not supported by this SQL dialect");

                    if (!_ctx.ParamMode) sqlBuilder!.AppendLine().Append(Kw(" prewhere "));
                    SqlSourceRenderer.MakeWhere(in _ctx, sqlBuilder, entityType, cmd.PreparedPreWhere, 0);
                }
                if (cmd.PreparedCondition is not null)
                {
                    if (!_ctx.ParamMode) sqlBuilder!.AppendLine().Append(Kw(" where "));
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

                        sqlBuilder!.AppendLine().Append(Kw(" group by "));

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

                            groupingSql = _ctx.Dialect.MakeGroupingSets(rendered, _ctx.KeywordCase);
                        }
                        else
                        {
                            groupingSql = _ctx.Dialect.MakeGrouping(string.Join(", ", columns!), cmd.GroupingType, _ctx.KeywordCase);
                        }

                        if (cmd.GroupByWithTotals)
                            groupingSql = _ctx.Dialect.MakeGroupByTotals(groupingSql, _ctx.KeywordCase);

                        sqlBuilder.Append(groupingSql);
                    }

                    var having = cmd.PreparedHaving ?? cmd.Having;
                    if (having is not null)
                    {
                        if (!_ctx.ParamMode) sqlBuilder!.AppendLine().Append(Kw(" having "));
                        SqlSourceRenderer.MakeWhere(in _ctx, sqlBuilder, entityType, having, 0);
                    }
                }

                var windows = cmd.Windows;
                if (windows is { Count: > 0 })
                {
                    if (!_ctx.Dialect.SupportsNamedWindows)
                        throw new NotSupportedException("Named windows (the WINDOW clause) are not supported by this SQL dialect");

                    var renderedWindows = _ctx.ParamMode ? null : new string[windows.Count];

                    for (var wi = 0; wi < windows.Count; wi++)
                    {
                        var window = windows[wi];
                        var windowSpec = MakeNamedWindow(in _ctx, entityType, window);

                        if (!_ctx.ParamMode)
                            renderedWindows![wi] = window.Name + Kw(" as (") + windowSpec + ")";
                    }

                    if (!_ctx.ParamMode)
                        sqlBuilder!.AppendLine().Append(Kw(" window ")).Append(string.Join(", ", renderedWindows!));
                }

                if (cmd.UnionQuery is not null)
                {
                    if (cmd.UnionType is UnionType.IntersectAll or UnionType.ExceptAll && !_ctx.Dialect.SupportsIntersectExceptAll)
                        throw new NotSupportedException($"The {cmd.UnionType} set operation is not supported by this SQL dialect");

                    if (!_ctx.ParamMode)
                    {
                        sqlBuilder!.AppendLine().Append(cmd.UnionType switch
                        {
                            UnionType.Distinct => Kw(" union "),
                            UnionType.All => Kw(" union all "),
                            UnionType.Intersect => Kw(" intersect "),
                            UnionType.IntersectAll => Kw(" intersect all "),
                            UnionType.Except => Kw(" except "),
                            UnionType.ExceptAll => Kw(" except all "),
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
                    if (!_ctx.ParamMode) sqlBuilder!.AppendLine().Append(Kw(" order by "));

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
                                    sqlBuilder.Append(Kw(" desc"));

                                sqlBuilder.Append(", ");
                            }
                        }
                        else if (!_ctx.ParamMode && sorting.ColumnIndex.HasValue)
                        {
                            sqlBuilder!.Append(sorting.ColumnIndex);
                            if (sorting.Direction == OrderDirection.Desc)
                                sqlBuilder.Append(Kw(" desc"));

                            sqlBuilder.Append(", ");
                        }
                    }

                    if (!_ctx.ParamMode) sqlBuilder!.Length -= 2;
                }
                else if (!pageApplied && !_ctx.ParamMode && _ctx.Dialect.GetPagingOrderBy(cmd) is { } pagingOrderBy)
                {
                    sqlBuilder!.AppendLine().Append(Kw(" order by ")).Append(pagingOrderBy);
                }

                if (cmd.LimitBy is { } limitBy)
                {
                    if (_ctx.Dialect.LimitBy is not { } limitByRenderer)
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
                        sqlBuilder.Append(limitByRenderer.Render(limitBy.Limit, limitBy.Offset, renderedLimitBy!, _ctx.KeywordCase));
                    }
                }

                if (cmd.Settings is { Count: > 0 } settings)
                {
                    if (!_ctx.Dialect.SupportsSettings)
                        throw new NotSupportedException("The SETTINGS clause is not supported by this SQL dialect");

                    if (!_ctx.ParamMode)
                    {
                        sqlBuilder!.AppendLine();
                        sqlBuilder.Append(_ctx.Dialect.MakeSettings(settings, _ctx.KeywordCase));
                    }
                }

                if (!pageApplied && !_ctx.ParamMode && !cmd.Paging.IsEmpty)
                {
                    sqlBuilder!.AppendLine();
                    _ctx.Dialect.MakePage(cmd.Paging, sqlBuilder, _ctx.KeywordCase);
                }
            }
            else if (!_ctx.ParamMode && sqlBuilder!.Length > 0)
                sqlBuilder.Length -= 2;

            if (!_ctx.ParamMode && cmd.RowLock is { } rowLock)
            {
                if (_ctx.Dialect.Lock is not { } lockRenderer)
                    throw new NotSupportedException("The FOR UPDATE/FOR SHARE clause is not supported by this SQL dialect");

                // Table-hint dialects already rendered the lock on the primary source.
                if (!lockRenderer.UsesTableHints)
                    sqlBuilder!.AppendLine().Append(lockRenderer.Render(rowLock.Mode, _ctx.KeywordCase));
            }


            if (!_ctx.ParamMode)
            {
                selectBuilder!.Append(Kw("select "));

                if (cmd.DistinctOn is not null)
                {
                    if (_ctx.Dialect.DistinctOn is not { } distinctOn)
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

                    selectBuilder.Append(distinctOn.Render(renderedDistinctOn, _ctx.KeywordCase));
                }
                // SQL Server renders the limit as TOP(n); DISTINCT has to precede it
                // ("select distinct top(n) ..."), so the flag is emitted before the select-list branch.
                else if (cmd.IsDistinct)
                    selectBuilder.Append(Kw("distinct "));
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
                            selectBuilder.Append(_ctx.Dialect.MakeColumnAlias(item.PropertyName, _ctx.KeywordCase));
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

                    sqlBuilder!.Append(' ').Append(_ctx.Dialect.MakeForJson(forJson, _ctx.KeywordCase));
                }

                if (cmd.ForXmlClause is { } forXml)
                {
                    if (!_ctx.Dialect.SupportsForXml)
                        throw new NotSupportedException("FOR XML is not supported by this SQL dialect");

                    sqlBuilder!.Append(' ').Append(_ctx.Dialect.MakeForXml(forXml, _ctx.KeywordCase));
                }

                var hints = cmd.Hints;
                if (hints is { Count: > 0 })
                {
                    if (!_ctx.Dialect.SupportsQueryHints)
                        throw new NotSupportedException("Query hints are not supported by this SQL dialect");

                    // The dialect owns the final placement. It also receives the CTE maxrecursion
                    // option so a dialect that must coalesce it into one trailing OPTION clause
                    // (SQL Server) can do so instead of the caller appending a second clause.
                    r = _ctx.Dialect.RenderQueryHints(sqlBuilder!.ToString(), hints, maxRecursionStmt, _ctx.KeywordCase);
                }
                else
                {
                    if (maxRecursionStmt is not null)
                        sqlBuilder!.Append(' ').Append(maxRecursionStmt);

                    r = sqlBuilder!.ToString();
                }
            }

            if (!_ctx.ParamMode && cmd.From?.RawSqlSource is not null)
                EnsureUniqueParameterNames(_ctx.Params);

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

    /// <summary>Resolves a lower-case keyword fragment (keywords and separators only) to the configured <see cref="KeywordCase"/>.</summary>
    private string Kw(string text) => SqlKeywords.Of(_ctx.KeywordCase, text);

    /// <summary>
    /// Returns <paramref name="hints"/> with <paramref name="hint"/> appended, or a single-element list
    /// when <paramref name="hints"/> is <c>null</c> or empty. Used to fold a row-locking table hint into
    /// the command's own table hints.
    /// </summary>
    private static IReadOnlyList<string> AppendHint(IReadOnlyList<string>? hints, string hint)
    {
        if (hints is not { Count: > 0 })
            return [hint];

        var combined = new string[hints.Count + 1];
        for (var i = 0; i < hints.Count; i++)
            combined[i] = hints[i];
        combined[^1] = hint;
        return combined;
    }

    /// <summary>
    /// Renders the inner specification of a named window (<c>partition by ... order by ... frame</c>).
    /// In parameter mode it renders nothing but still walks every key so captured constants become
    /// parameters in the same order as the SQL pass.
    /// </summary>
    private static string MakeNamedWindow(in SqlBuildContext ctx, Type entityType, WindowDefinition window)
    {
        var spec = StringBuilderPool.Shared.Get();

        try
        {
            for (var i = 0; i < window.PartitionBy.Count; i++)
            {
                if (i == 0)
                {
                    if (!ctx.ParamMode) spec.Append(SqlKeywords.Of(ctx.KeywordCase, "partition by "));
                }
                else if (!ctx.ParamMode)
                {
                    spec.Append(", ");
                }

                var (_, column) = SqlSourceRenderer.MakeColumn(
                    in ctx,
                    new SelectExpression(window.PartitionBy[i].ReturnType) { Expression = window.PartitionBy[i] },
                    entityType,
                    dontNeedAlias: true);

                if (!ctx.ParamMode)
                    spec.Append(column);
            }

            for (var i = 0; i < window.OrderBy.Count; i++)
            {
                if (i == 0)
                {
                    if (!ctx.ParamMode)
                        spec.Append(window.PartitionBy.Count > 0 ? SqlKeywords.Of(ctx.KeywordCase, " order by ") : SqlKeywords.Of(ctx.KeywordCase, "order by "));
                }
                else if (!ctx.ParamMode)
                {
                    spec.Append(", ");
                }

                var key = window.OrderBy[i];
                var (_, column) = SqlSourceRenderer.MakeColumn(
                    in ctx,
                    new SelectExpression(key.Expression.ReturnType) { Expression = key.Expression },
                    entityType,
                    dontNeedAlias: true);

                if (!ctx.ParamMode)
                {
                    spec.Append(column);

                    if (key.Direction == OrderDirection.Desc)
                        spec.Append(SqlKeywords.Of(ctx.KeywordCase, " desc"));
                }
            }

            if (window.Frame is { } frame)
            {
                if (frame.Type == WindowFrameType.Groups && !ctx.Dialect.SupportsWindowFrameGroups)
                    throw new NotSupportedException("The GROUPS window frame unit is not supported by this SQL dialect");

                if (frame.Exclusion is not null && !ctx.Dialect.SupportsWindowFrameExclusion)
                    throw new NotSupportedException("The EXCLUDE window frame clause is not supported by this SQL dialect");

                if (!ctx.ParamMode)
                {
                    if (window.PartitionBy.Count > 0 || window.OrderBy.Count > 0)
                        spec.Append(' ');

                    spec.Append(WindowSql.RenderWindowFrame(frame, ctx.KeywordCase));
                }
            }

            return ctx.ParamMode ? string.Empty : spec.ToString();
        }
        finally
        {
            StringBuilderPool.Shared.Return(spec);
        }
    }

    // A raw SQL FROM source adds its parameters by property name; a captured variable of the same name
    // in the surrounding query would add a second parameter with that name, which most providers reject
    // at execution. Fail at build time with an actionable message instead.
    private static void EnsureUniqueParameterNames(List<Parameter> @params)
    {
        for (var i = 0; i < @params.Count; i++)
        {
            for (var j = i + 1; j < @params.Count; j++)
            {
                if (string.Equals(@params[i].Name, @params[j].Name, StringComparison.Ordinal))
                    throw new BuildSqlCommandException(
                        $"The raw SQL source and the query produced two parameters named '{@params[i].Name}'. Rename the colliding captured variable or raw-SQL parameter.");
            }
        }
    }
}
