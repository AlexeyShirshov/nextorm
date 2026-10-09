using System.Linq.Expressions;
using System.Reflection;
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

    public string? MakeSelect(QueryCommand cmd, string? selectInto = null)
    {
#if DEBUG
        if (!cmd.IsPrepared)
            throw new InvalidOperationException("Command not prepared");

        if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Making sql with param mode {mode}", _ctx.ParamMode);
#endif
        var entityType = cmd.EntityType;
        ArgumentNullException.ThrowIfNull(entityType);

        // A SelectWhereMax/SelectWhereMin command is lowered to a portable window-function derived
        // table (see MakeExtremeRowSelect) instead of the plain single-statement rendering below.
        if (cmd.ExtremeRow is not null)
            return MakeExtremeRowSelect(cmd, selectInto);

        var selectList = cmd.SelectList;
        var from = cmd.From;
        // A command whose declarations were hoisted into the enclosing statement's top-level WITH must
        // not emit a WITH of its own (see CteHoister); only the root of the render emits it.
        var ctes = _ctx.SuppressCtes ? null : cmd.Ctes;

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
            // A dynamic-columns store projection appends an unqualified "*" after the mapped columns. On
            // MySQL/MariaDB that is a syntax error unless the star is qualified with the source alias, so
            // such a dialect needs the source aliased even without joins (see RequiresQualifiedSelectStar).
            var hasDynamicStore = !cmd.IgnoreColumns && selectList is not null
                && Array.Exists(selectList, static item => item.IsDynamicColumnsStore);
            var needAlias = hasJoins || _ctx.QueryProvider.OuterReferences?.Count > 0
                || (hasDynamicStore && _ctx.Dialect.RequiresQualifiedSelectStar);

            // Tables-in-scope hints are structural on SQL Server (a WITH(...) on every physical table)
            // and part of the statement-level comment elsewhere. A dialect that supports neither rejects
            // them here, before any SQL is assembled.
            var scopeHints = _ctx.Dialect.SupportsTablesInScopeHints ? cmd.TablesInScopeHints : null;
            if (cmd.TablesInScopeHints is { Count: > 0 }
                && !_ctx.Dialect.SupportsInlineHints
                && !_ctx.Dialect.SupportsTablesInScopeHints)
                throw new NotSupportedException("Tables-in-scope hints are not supported by this SQL dialect");

            // Join/subquery/tables-in-scope hints on an inline-comment dialect are folded into one
            // statement-level /*+ ... */ comment rather than rendered structurally.
            var inlineHints = _ctx.Dialect.SupportsInlineHints ? CollectInlineHints(cmd) : null;

            // The alias assigned to the FROM source (null for non-aliased or non-physical sources); the
            // dynamic-columns star uses it to qualify the appended "*" on dialects that require it.
            string? fromAlias = null;

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
                    tableHints = AppendHint(tableHints, lockHint.Render(lockClause.Mode, lockClause.Wait, _ctx.KeywordCase));

                var fromStr = SqlSourceRenderer.MakeFrom(in _ctx, from, new FromRenderOptions(needAlias, entityType, hasJoins, tableHints, cmd.Temporal, cmd.IndexHints, cmd.IndexHintKind, scopeHints), out fromAlias);
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
                        var joinSql = SqlSourceRenderer.MakeJoin(in _ctx, joins[idx], entityType!, scopeHints);
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
                    sqlBuilder!.AppendLine().Append(lockRenderer.Render(rowLock.Mode, rowLock.Wait, _ctx.KeywordCase));
            }


            if (!_ctx.ParamMode)
            {
                selectBuilder!.Append(Kw("select "));

                if (cmd.Tag is { Length: > 0 } tag)
                    selectBuilder.Append(MakeTagComment(tag)).Append(' ');

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
                    if (item.IsDynamicColumnsStore)
                    {
                        if (!_ctx.ParamMode)
                        {
                            // MySQL/MariaDB reject an unqualified "*" next to explicit columns, so the
                            // star is qualified with the FROM alias on dialects that require it. Other
                            // dialects and the plain-star path keep emitting the bare "*".
                            if (_ctx.Dialect.RequiresQualifiedSelectStar)
                            {
                                // The qualifier must be the same token the FROM rendered (see
                                // SqlSourceRenderer's MakeTableAlias call), escaping included. Fail closed:
                                // the flag is set only for dialects that cannot emit a bare star, so a
                                // source without an alias is a bug, not a reason to silently fall back to
                                // SQL that would be rejected (or, worse, accepted against the wrong source).
                                if (fromAlias is null)
                                    throw new BuildSqlCommandException(
                                        "A dynamic-columns store projection requires an aliased physical FROM source on this dialect.");

                                selectBuilder!.Append(_ctx.Dialect.Escape(fromAlias)).Append(".*, ");
                            }
                            else
                                selectBuilder!.Append("*").Append(", ");
                        }

                        continue;
                    }

                    var (needAliasForColumn, column) = SqlSourceRenderer.MakeColumn(in _ctx, item, entityType, !needAlias, renameAware: true);

                    if (!_ctx.ParamMode)
                    {
                        selectBuilder!.Append(column);

                        if (needAliasForColumn)
                        {
                            // A generated output name (an unaliased computed scalar) is an alias only for
                            // a typed-CTE declaration body; anywhere else the column keeps its implicit
                            // name (the alias text was null before, and MakeColumnAlias emits nothing).
                            var alias = item.PropertyName ?? (_ctx.ExactProjectionAliases ? item.OutputName : null);
                            selectBuilder.Append(_ctx.Dialect.MakeColumnAlias(alias, _ctx.KeywordCase));
                        }

                        selectBuilder.Append(", ");
                    }
                }

                // A LOB-only projection may need a trailing locator (SQLite's rowid) to switch the
                // driver to a seekable streaming blob; the payload keeps ordinal 0. The locator is
                // appended with the loop's ", " separator so the trailing-trim below drops it again.
                // The CSV terminal sets SuppressLobLocator so its bounded binary read keeps the SQL
                // (and the reader's field set) identical to the buffered path.
                if (!_ctx.ParamMode && !_ctx.SuppressLobLocator && _ctx.SequentialAccess && _ctx.Dialect.LobLocatorColumn is { } lobLocator)
                    selectBuilder!.Append(lobLocator).Append(", ");
            }


            string? r = null;
            if (!_ctx.ParamMode)
            {
                selectBuilder!.Length -= 2;
                if (selectInto is not null)
                    selectBuilder.Append(selectInto);

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

                var hints = MergeHints(cmd.Hints, inlineHints);
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

    /// <summary>
    /// Renders a <c>SelectWhereMax</c>/<c>SelectWhereMin</c> command. Compatibility is validated first,
    /// then the dialect's optional <see cref="ISqlDialect.ExtremeRowRenderer"/> decides - from a
    /// prepared description, before any side effect - whether to render natively; otherwise the command
    /// is lowered to the portable window-function query:
    /// <code>
    /// select &lt;projection&gt; from (
    ///     select *, row_number() | rank() over (partition by ... order by ... desc|asc) as rn
    ///     from &lt;source&gt; where &lt;condition&gt;
    /// ) t1 where t1.rn = 1
    /// </code>
    /// The source rows plus the synthetic rank column form the derived table; the outer statement
    /// projects the command's select list over it. The rank column is dropped by the projection and is
    /// named to avoid colliding with a mapped source column. Works on every provider whose dialect
    /// reports <see cref="ISqlDialect.SupportsSelectWhereMinMax"/>.
    /// </summary>
    private string? MakeExtremeRowSelect(QueryCommand cmd, string? selectInto)
    {
        if (!_ctx.Dialect.SupportsSelectWhereMinMax)
            throw new NotSupportedException($"{_ctx.Dialect.GetType().Name} does not support SelectWhereMax/SelectWhereMin.");

        EnsureExtremeRowCompatible(cmd, selectInto);

        // Native generation is an optional dialect capability, limited to the single-row One form: All
        // keeps every tied winner and stays on the portable rank lowering. The eligibility decision is
        // made from a side-effect-free description BEFORE any build-context/parameter/alias work; a
        // missing capability or a negative answer keeps the portable window-function lowering. A
        // renderer failure propagates as an error and never falls back late.
        var renderer = _ctx.Dialect.ExtremeRowRenderer;
        if (renderer is not null
            && cmd.ExtremeRow!.Ties == ExtremeRowTies.One
            // A source without mapped metadata (a name-addressed or temporary table) cannot provide the
            // canonical payload aliases and column types the native renderer decides on. Such a source
            // keeps the portable lowering instead of failing the read on a native-capable provider.
            && DataContextCache.Metadata.TryGetValue(cmd.EntityType!, out var entityMeta))
        {
            // Build the entity projection once for the whole native preparation: the eligibility
            // description resolves its payload lazily from this list (a renderer that only reads
            // Keys/Groups never builds it), and the alias and outer stages reuse the same instance.
            var (entitySelectList, _) = EntitySelectListBuilder.Build(
                cmd.EntityType!,
                entityMeta,
                CancellationToken.None);

            var description = MakeExtremeRowDescription(cmd, entitySelectList);
            if (renderer.CanRender(description))
                return MakeNativeExtremeRowSelect(cmd, renderer, entitySelectList, description.Keys);
        }

        return MakePortableExtremeRowSelect(cmd);
    }

    /// <summary>
    /// Builds the portable lowering: the ranked inner derived table plus the outer statement that
    /// projects the select list and keeps <c>rn = 1</c>.
    /// </summary>
    private string? MakePortableExtremeRowSelect(QueryCommand cmd)
    {
        var entityType = cmd.EntityType!;
        var from = cmd.From!;
        var rankColumn = MakeExtremeRowRankName(entityType);
        var innerSql = MakePortableExtremeRowInner(cmd, entityType, from, rankColumn);
        return MakeExtremeRowOuter(cmd, entityType, innerSql, rankColumn);
    }

    /// <summary>
    /// Builds the inner derived table of the portable lowering: the filtered source rows plus the
    /// synthetic window-rank column. The filter (key components <c>IS NOT NULL</c> ANDed with the
    /// source condition) is shared with the native source builder.
    /// </summary>
    private string? MakePortableExtremeRowInner(QueryCommand cmd, Type entityType, FromExpression from, string rankColumn)
    {
        // --- inner derived table: the source rows plus the window rank ---
        string? innerSql = null;
        _ctx.ColumnsProvider.PushSourceScope();
        try
        {
            var inner = _ctx.ParamMode ? null : StringBuilderPool.Shared.Get();
            try
            {
                var fromSql = SqlSourceRenderer.MakeFrom(in _ctx, from, new FromRenderOptions(false, entityType, false));
                var rankSql = MakeExtremeRowRankExpression(cmd, entityType, rankColumn);

                if (!_ctx.ParamMode)
                    inner!.Append(Kw("select ")).Append('*').Append(", ").Append(rankSql).Append(Kw(" from ")).Append(fromSql);

                AppendExtremeRowSourceFilter(cmd, entityType, inner);

                innerSql = inner?.ToString();
            }
            finally
            {
                if (inner is not null) StringBuilderPool.Shared.Return(inner);
            }
        }
        finally
        {
            _ctx.ColumnsProvider.PopSourceScope();
        }

        return innerSql;
    }

    /// <summary>
    /// Appends the source filter shared by the portable inner derived table and the native source: an
    /// <c>IS NOT NULL</c> predicate per extreme-key component (comparison nulls must not win the
    /// extremum and would surface an all-null group) ANDed with the source's own condition. In
    /// parameter mode it walks the same expressions in the same order and emits nothing; a
    /// condition-only source still opens the <c>where</c>.
    /// </summary>
    private void AppendExtremeRowSourceFilter(QueryCommand cmd, Type entityType, StringBuilder? inner)
    {
        var valueColumns = cmd.ExtremeRowColumns ?? [];
        var hasNotNullFilter = valueColumns.Length > 0;
        var hasCondition = cmd.PreparedCondition is not null;

        if (!hasNotNullFilter && !hasCondition)
            return;

        if (!_ctx.ParamMode)
            inner!.AppendLine().Append(Kw(" where "));

        if (hasNotNullFilter)
        {
            for (var i = 0; i < valueColumns.Length; i++)
            {
                if (!_ctx.ParamMode && i > 0)
                    inner!.Append(Kw(" and "));

                var (_, valueColumn) = SqlSourceRenderer.MakeColumn(in _ctx, valueColumns[i], entityType, dontNeedAlias: true);
                if (!_ctx.ParamMode)
                    inner!.Append(valueColumn).Append(' ').Append(Kw("is not null"));
            }

            if (hasCondition && !_ctx.ParamMode)
                inner!.Append(Kw(" and "));
        }

        if (cmd.PreparedCondition is { } condition)
            SqlSourceRenderer.MakeWhere(in _ctx, inner, entityType, condition, 0);
    }

    /// <summary>
    /// Renders the command through the dialect's optional native extreme-row capability. The shared
    /// path builds the same filtered source as the portable lowering (without the rank), hands it to
    /// the renderer together with the canonical payload/key/group aliases, and wraps the returned
    /// winning-row source in the same outer projection/order statement. A renderer exception
    /// propagates unchanged.
    /// </summary>
    private string? MakeNativeExtremeRowSelect(
        QueryCommand cmd,
        IExtremeRowRenderer renderer,
        SelectExpression[] entitySelectList,
        IReadOnlyList<ExtremeRowRenderColumn> keyColumns)
    {
        var entityType = cmd.EntityType!;
        var from = cmd.From!;

        var sourceSql = MakeNativeExtremeRowSource(cmd, entityType, from);

        // Parameter mode emits no SQL; the outer call still walks the projection and ordering so the
        // collected parameters stay in step with the SQL pass.
        if (_ctx.ParamMode || sourceSql is null)
            return MakeExtremeRowOuter(cmd, entityType, null, rankColumn: null, entitySelectList);

        var (payloadAliases, keyAliases, groupAliases) = MakeExtremeRowAliases(cmd, entitySelectList);
        var winnerSql = renderer.Render(new ExtremeRowRenderRequest(
            default(ExtremeRowTrustedConstruction),
            sourceSql,
            cmd.ExtremeRow!.Kind == ExtremeKind.Max,
            payloadAliases,
            keyAliases,
            keyColumns,
            groupAliases,
            _ctx.KeywordCase));

        return MakeExtremeRowOuter(cmd, entityType, winnerSql, rankColumn: null, entitySelectList);
    }

    /// <summary>
    /// Builds the native strategy's filtered source: <c>select * from &lt;source&gt; where
    /// &lt;key-component IS NOT NULL&gt; [and &lt;condition&gt;]</c> - the portable inner minus the
    /// synthetic rank. The renderer turns this into the winning-row source. Parameter mode walks the
    /// same expressions in the same order and emits nothing.
    /// </summary>
    private string? MakeNativeExtremeRowSource(QueryCommand cmd, Type entityType, FromExpression from)
    {
        string? sourceSql = null;
        _ctx.ColumnsProvider.PushSourceScope();
        try
        {
            var source = _ctx.ParamMode ? null : StringBuilderPool.Shared.Get();
            try
            {
                var fromSql = SqlSourceRenderer.MakeFrom(in _ctx, from, new FromRenderOptions(false, entityType, false));

                if (!_ctx.ParamMode)
                    source!.Append(Kw("select ")).Append('*').Append(Kw(" from ")).Append(fromSql);

                AppendExtremeRowSourceFilter(cmd, entityType, source);

                sourceSql = source?.ToString();
            }
            finally
            {
                if (source is not null) StringBuilderPool.Shared.Return(source);
            }
        }
        finally
        {
            _ctx.ColumnsProvider.PopSourceScope();
        }

        return sourceSql;
    }

    /// <summary>
    /// Resolves the canonical payload aliases (every mapped source column, in declaration order) and
    /// the key/group aliases (the physical column names of the direct mapped key components) from the
    /// projection built once by the native dispatch. Only reached after
    /// <see cref="IExtremeRowRenderer.CanRender"/> accepted the description, which constraints the
    /// key/group components to direct mapped columns.
    /// </summary>
    private (string[] Payload, string[] Keys, string[] Groups) MakeExtremeRowAliases(QueryCommand cmd, SelectExpression[] entityColumns)
    {
        var payload = new string[entityColumns.Length];
        for (var i = 0; i < entityColumns.Length; i++)
        {
            var column = entityColumns[i];
            payload[i] = column.RangeColumns is not null
                ? column.PropertyName!
                : column.PropertyInfo!.GetPropertyColumnName(_ctx.NamingConvention);
        }

        return (payload, MakeExtremeRowKeyAliases(cmd.ExtremeRowColumns), MakeExtremeRowKeyAliases(cmd.ExtremeRowGroupByColumns));
    }

    private string[] MakeExtremeRowKeyAliases(SelectExpression[]? columns)
    {
        if (columns is null || columns.Length == 0)
            return [];

        var aliases = new string[columns.Length];
        for (var i = 0; i < columns.Length; i++)
        {
            if (columns[i].Expression is not MemberExpression { Member: PropertyInfo property })
                throw new InvalidOperationException("The native extreme-row renderer requires direct mapped key columns.");

            aliases[i] = property.GetPropertyColumnName(_ctx.NamingConvention);
        }

        return aliases;
    }

    /// <summary>
    /// Builds the side-effect-free description the dialect's optional native renderer decides on. It
    /// reads only prepared metadata: it never renders SQL, allocates an alias or touches the parameter
    /// list, so a rejected candidate leaves no state behind for the portable lowering. The payload is
    /// projected lazily from the prebuilt <paramref name="entitySelectList"/>, so a renderer that only
    /// reads <see cref="ExtremeRowDescription.Keys"/>/<see cref="ExtremeRowDescription.Groups"/> never
    /// materializes it.
    /// </summary>
    private static ExtremeRowDescription MakeExtremeRowDescription(QueryCommand cmd, SelectExpression[] entitySelectList)
        => new(
            cmd.ExtremeRow!.Kind == ExtremeKind.Max,
            MakeExtremeRowDescriptionColumns(cmd.ExtremeRowColumns),
            MakeExtremeRowDescriptionColumns(cmd.ExtremeRowGroupByColumns),
            () => MakeExtremeRowPayloadDescription(entitySelectList));

    private static ExtremeRowRenderColumn[] MakeExtremeRowDescriptionColumns(SelectExpression[]? columns)
    {
        if (columns is null || columns.Length == 0)
            return [];

        var result = new ExtremeRowRenderColumn[columns.Length];
        for (var i = 0; i < columns.Length; i++)
        {
            var expression = columns[i].Expression;
            var property = (expression as MemberExpression)?.Member as PropertyInfo;
            IPropertyMetadata? metadata = null;
            var hasMetadata = property is not null
                && DataContextCache.Metadata.TryGetValue(property.DeclaringType!, property, out metadata);

            result[i] = new ExtremeRowRenderColumn(
                columns[i].PropertyType,
                columns[i].Nullable,
                hasMetadata && metadata!.Converter is null && !metadata.IsComputed,
                hasMetadata && metadata!.Converter is not null);
        }

        return result;
    }

    private static ExtremeRowRenderColumn[] MakeExtremeRowPayloadDescription(SelectExpression[] columns)
    {
        var result = new ExtremeRowRenderColumn[columns.Length];
        for (var i = 0; i < columns.Length; i++)
        {
            var column = columns[i];

            // EntitySelectListBuilder stores the property access wrapped in a LambdaExpression, unlike
            // the key/group columns (which carry the bare member-access body), so the lambda must be
            // unwrapped before the direct-mapped test. A Range<T> property expands into two physical
            // columns and a computed property is not a plain mapped column, so neither is native-eligible
            // as payload; both keep the portable lowering.
            var body = column.Expression is LambdaExpression lambda ? lambda.Body : column.Expression;
            IPropertyMetadata? metadata = null;
            var hasMetadata = body is MemberExpression { Member: PropertyInfo property }
                && DataContextCache.Metadata.TryGetValue(property.DeclaringType!, property, out metadata);

            result[i] = new ExtremeRowRenderColumn(
                column.PropertyType,
                column.Nullable,
                hasMetadata && metadata!.Converter is null && !metadata.IsComputed && column.RangeColumns is null,
                hasMetadata && metadata!.Converter is not null);
        }

        return result;
    }

    /// <summary>
    /// Builds the entity projection on demand for the outer expansion when the native dispatch did not
    /// already hand one in (the portable lowering). Throws for a source without mapped metadata.
    /// </summary>
    private static SelectExpression[] BuildExtremeRowEntitySelectList(Type entityType)
    {
        if (!DataContextCache.Metadata.TryGetValue(entityType, out var entityMeta))
            throw new BuildSqlCommandException("SelectWhereMax/SelectWhereMin requires a mapped entity source.");

        var (columns, _) = EntitySelectListBuilder.Build(entityType, entityMeta, CancellationToken.None);
        return columns;
    }

    /// <summary>
    /// Wraps the already-rendered <paramref name="innerSql"/> (the portable ranked derived table or the
    /// native winning-row source) in the outer statement: the command's projection, DISTINCT, the
    /// user's output ordering, and - for the portable strategy - the <c>rn = 1</c> filter on
    /// <paramref name="rankColumn"/>. <paramref name="rankColumn"/> is <see langword="null"/> for the
    /// native strategy, whose source already selects the winners. <paramref name="entitySelectList"/> is
    /// the projection built once by the native dispatch and reused here; the portable path passes
    /// <see langword="null"/> and the projection is built on demand.
    /// </summary>
    private string? MakeExtremeRowOuter(
        QueryCommand cmd,
        Type entityType,
        string? innerSql,
        string? rankColumn,
        SelectExpression[]? entitySelectList = null)
    {
        // --- outer statement over the derived table ---
        string? result = null;
        _ctx.ColumnsProvider.PushSourceScope();
        try
        {
            string? alias = null;
            if (!_ctx.ParamMode)
            {
                // Register the derived table as a physical-shaped source so member access of the
                // command's select list resolves to its aliased columns; the inner `select *` exposes
                // the source's physical column names.
                _ctx.ColumnsProvider.Add(entityType, false);
                alias = _ctx.AliasProvider!.GetNextAlias(new FromExpression(entityType));
            }

            var outer = _ctx.ParamMode ? null : StringBuilderPool.Shared.Get();
            try
            {
                var selectList = cmd.SelectList;

                if (!_ctx.ParamMode)
                {
                    outer!.Append(Kw("select "));

                    if (cmd.Tag is { Length: > 0 } tag)
                        outer.Append(MakeTagComment(tag)).Append(' ');

                    if (cmd.IsDistinct)
                        outer.Append(Kw("distinct "));

                    if (cmd.IgnoreColumns || selectList is null || selectList.Length == 0)
                    {
                        // The inner derived table selects the source columns plus the synthetic rank
                        // column, so a bare `*` here would leak that rank column into the result. With no
                        // explicit projection, expand the entity's mapped columns explicitly (the same
                        // shape the normal whole-row select path prepares) so the rank is dropped. The
                        // native dispatch hands its single projection in; the portable path builds it.
                        var entityColumns = entitySelectList ?? BuildExtremeRowEntitySelectList(entityType);
                        for (var i = 0; i < entityColumns.Length; i++)
                        {
                            if (i > 0) outer.Append(", ");

                            var item = entityColumns[i];
                            var (needAliasForColumn, column) = SqlSourceRenderer.MakeColumn(in _ctx, item, entityType, dontNeedAlias: false, renameAware: true);
                            outer.Append(column);

                            if (needAliasForColumn)
                                outer.Append(_ctx.Dialect.MakeColumnAlias(item.PropertyName, _ctx.KeywordCase));
                        }
                    }
                    else
                    {
                        for (var i = 0; i < selectList.Length; i++)
                        {
                            if (i > 0) outer.Append(", ");

                            var item = selectList[i];
                            var (needAliasForColumn, column) = SqlSourceRenderer.MakeColumn(in _ctx, item, entityType, dontNeedAlias: false, renameAware: true);
                            outer.Append(column);

                            if (needAliasForColumn)
                                outer.Append(_ctx.Dialect.MakeColumnAlias(item.PropertyName, _ctx.KeywordCase));
                        }
                    }

                    outer.AppendLine().Append(Kw("from ")).Append('(').Append(innerSql).Append(')')
                        .Append(_ctx.Dialect.MakeTableAlias(alias!, _ctx.KeywordCase));

                    if (rankColumn is not null)
                    {
                        outer.AppendLine().Append(Kw(" where ")).Append(alias).Append('.')
                            .Append(_ctx.Dialect.MakeColumnReference(rankColumn)).Append(" = 1");
                    }

                    AppendOrderBy(cmd, entityType, outer);
                }
                else
                {
                    // Parameter pass: walk the same expressions in the same order as the SQL pass.
                    if (!cmd.IgnoreColumns && selectList is not null)
                    {
                        for (var i = 0; i < selectList.Length; i++)
                            SqlSourceRenderer.MakeColumn(in _ctx, selectList[i], entityType, dontNeedAlias: false, renameAware: true);
                    }

                    if (cmd.Sorting is { Length: > 0 } sorting)
                    {
                        for (var i = 0; i < sorting.Length; i++)
                        {
                            if (sorting[i].PreparedExpression is { } prepared)
                                SqlSourceRenderer.MakeSort(in _ctx, entityType, prepared, 0);
                        }
                    }
                }

                result = outer?.ToString();
            }
            finally
            {
                if (outer is not null) StringBuilderPool.Shared.Return(outer);
            }
        }
        finally
        {
            _ctx.ColumnsProvider.PopSourceScope();
        }

        return result;
    }

    /// <summary>
    /// Renders the <c>order by</c> of the surviving rows on the outer statement. Mirrors the plain
    /// select-list ordering: a prepared expression is rendered through the column visitor, an ordinal
    /// is emitted verbatim. A parameter-mode caller walks the expressions separately.
    /// </summary>
    private void AppendOrderBy(QueryCommand cmd, Type entityType, StringBuilder target)
    {
        if (cmd.Sorting is not { Length: > 0 } sorting)
            return;

        target.AppendLine().Append(Kw("order by "));
        for (var i = 0; i < sorting.Length; i++)
        {
            if (i > 0) target.Append(", ");

            if (sorting[i].PreparedExpression is { } prepared)
                target.Append(SqlSourceRenderer.MakeSort(in _ctx, entityType, prepared, 0));
            else
                target.Append(sorting[i].ColumnIndex);

            if (sorting[i].Direction == OrderDirection.Desc)
                target.Append(Kw(" desc"));
        }
    }

    /// <summary>
    /// Renders <c>row_number()</c> (single survivor) or <c>rank()</c> (every tied survivor) over the
    /// optional group partition and the value ordering. <c>Max</c> orders the value descending,
    /// <c>Min</c> ascending (the default direction). Multiple keys of a composite selector are emitted
    /// in order. Parameter mode visits the keys but emits nothing.
    /// </summary>
    private string MakeExtremeRowRankExpression(QueryCommand cmd, Type entityType, string rankColumn)
    {
        var extreme = cmd.ExtremeRow!;
        var valueColumns = cmd.ExtremeRowColumns ?? [];
        var groupColumns = cmd.ExtremeRowGroupByColumns ?? [];

        var builder = _ctx.ParamMode ? null : StringBuilderPool.Shared.Get();
        try
        {
            if (builder is not null)
                builder.Append(extreme.Ties == ExtremeRowTies.All ? "rank()" : "row_number()").Append(Kw(" over ("));

            for (var i = 0; i < groupColumns.Length; i++)
            {
                if (builder is not null)
                    builder.Append(i == 0 ? Kw("partition by ") : ", ");

                var (_, column) = SqlSourceRenderer.MakeColumn(in _ctx, groupColumns[i], entityType, dontNeedAlias: true);
                if (builder is not null) builder.Append(column);
            }

            for (var i = 0; i < valueColumns.Length; i++)
            {
                if (builder is not null)
                {
                    if (i == 0)
                        builder.Append(groupColumns.Length > 0 ? Kw(" order by ") : Kw("order by "));
                    else
                        builder.Append(", ");
                }

                var (_, column) = SqlSourceRenderer.MakeColumn(in _ctx, valueColumns[i], entityType, dontNeedAlias: true);
                if (builder is not null)
                {
                    builder.Append(column);
                    if (extreme.Kind == ExtremeKind.Max)
                        builder.Append(Kw(" desc"));
                }
            }

            if (builder is not null)
                builder.Append(')').Append(_ctx.Dialect.MakeColumnAlias(rankColumn, _ctx.KeywordCase));

            return builder?.ToString() ?? string.Empty;
        }
        finally
        {
            if (builder is not null) StringBuilderPool.Shared.Return(builder);
        }
    }

    /// <summary>
    /// Returns a rank-column name that no mapped column of <paramref name="entityType"/> uses, so the
    /// synthetic column cannot shadow or be shadowed by a source column when the derived table selects
    /// <c>*</c> plus the rank.
    /// </summary>
    private static string MakeExtremeRowRankName(Type entityType)
    {
        const string seed = "__nextorm_rn";
        if (!DataContextCache.Metadata.TryGetValue(entityType, out var metadata))
            return seed;

        var properties = metadata.Properties;
        var name = seed;
        while (true)
        {
            var collides = false;
            for (var i = 0; i < properties.Count; i++)
            {
                if (string.Equals(properties[i].ColumnName, name, StringComparison.OrdinalIgnoreCase))
                {
                    collides = true;
                    break;
                }
            }

            if (!collides) return name;
            name += "_";
        }
    }

    /// <summary>
    /// Rejects the combinations the derived-table lowering cannot express, before any SQL is assembled.
    /// The modifier checks are shared with the in-memory evaluator (see
    /// <see cref="ExtremeRowCompatibility.EnsureModifiersCompatible"/>); only the SQL-only source shape
    /// and the SELECT ... INTO wrapper are checked here.
    /// </summary>
    private static void EnsureExtremeRowCompatible(QueryCommand cmd, string? selectInto)
    {
        if (selectInto is not null)
            throw new BuildSqlCommandException("SelectWhereMax/SelectWhereMin cannot be rendered as a SELECT ... INTO source.");

        ExtremeRowCompatibility.EnsureModifiersCompatible(cmd);

        if (cmd.From is null || cmd.From.SubQuery is not null)
            throw new BuildSqlCommandException("SelectWhereMax/SelectWhereMin requires a single physical-table source.");
    }

    /// <summary>
    /// Renders a query tag as a single-line block comment. Line breaks are folded to spaces and both
    /// comment delimiters are neutralised: SQL Server nests block comments, so a literal <c>/*</c> in
    /// the tag would open a nested comment that the closing <c>*/</c> could not close. The comment opens
    /// with a space (<c>/* </c>) so a tag starting with <c>!</c> or <c>+</c> cannot turn into a
    /// MySQL/MariaDB executable comment or optimizer hint.
    /// </summary>
    /// <param name="tag">The non-empty tag text.</param>
    /// <returns>The SQL block comment.</returns>
    private static string MakeTagComment(string tag)
    {
        var sb = new StringBuilder(tag.Length + 6);
        sb.Append("/* ");

        for (var i = 0; i < tag.Length; i++)
        {
            var c = tag[i];
            switch (c)
            {
                case '\r':
                    sb.Append(' ');
                    if (i + 1 < tag.Length && tag[i + 1] == '\n') i++;
                    break;
                case '\n':
                    sb.Append(' ');
                    break;
                case '*':
                    if (i + 1 < tag.Length && tag[i + 1] == '/')
                    {
                        sb.Append("* /");
                        i++;
                    }
                    else
                    {
                        sb.Append('*');
                    }
                    break;
                case '/':
                    if (i + 1 < tag.Length && tag[i + 1] == '*')
                    {
                        sb.Append("/ *");
                        i++;
                    }
                    else
                    {
                        sb.Append('/');
                    }
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        sb.Append(" */");
        return sb.ToString();
    }

    public (bool NeedAliasForColumn, string Column) MakeColumn(SelectExpression selExp, Type entityType, bool dontNeedAlias, bool renameAware = false)
        => SqlSourceRenderer.MakeColumn(in _ctx, selExp, entityType, dontNeedAlias, renameAware);

    /// <summary>
    /// Renders a multi-table <c>DELETE</c> over the prepared joined source of <paramref name="cmd"/>.
    /// The target is the first table of the chain; the join conditions and the optional user filter are
    /// rendered by the shared <c>SELECT</c> source pipeline, so column aliasing matches the equivalent
    /// <c>SELECT</c>. The dialect composes its native form (SQL Server/MySQL
    /// <c>DELETE &lt;alias&gt; FROM ... JOIN</c>, PostgreSQL <c>DELETE ... USING</c>).
    /// </summary>
    internal (string Sql, List<Parameter> Parameters) MakeDeleteJoin(DeleteJoinCommand cmd)
    {
        if (!_ctx.Dialect.SupportsDeleteJoin)
            throw new NotSupportedException($"{_ctx.Dialect.GetType().Name} cannot render a multi-table DELETE.");

        var source = cmd.Source;

        if (source.EntityType is not { } entityType)
            throw new BuildSqlCommandException("A multi-table DELETE command is missing its source entity type.");

        if (source.Joins is not { Length: > 0 } joins)
            throw new BuildSqlCommandException("A multi-table DELETE needs at least one join.");

        if (source.From is not { } targetFrom)
            throw new BuildSqlCommandException("A multi-table DELETE is missing its target source.");

        // Scope hints are structural on SQL Server (a WITH(...) on every physical table) and part of the
        // statement-level comment elsewhere. A dialect that supports neither rejects them here, before any
        // SQL is assembled — the same capability rule the SELECT pipeline applies (MakeSelect).
        var scopeHints = _ctx.Dialect.SupportsTablesInScopeHints ? source.TablesInScopeHints : null;
        if (source.TablesInScopeHints is { Count: > 0 }
            && !_ctx.Dialect.SupportsInlineHints
            && !_ctx.Dialect.SupportsTablesInScopeHints)
            throw new NotSupportedException("Tables-in-scope hints are not supported by this SQL dialect");

        var inlineHints = _ctx.Dialect.SupportsInlineHints ? MergeHints(source.Hints, CollectInlineHints(source)) : null;
        if (inlineHints is { Count: > 0 } && !_ctx.Dialect.SupportsQueryHints)
            throw new NotSupportedException("Query hints are not supported by this SQL dialect");

        _ctx.ColumnsProvider.PushSourceScope();
        try
        {
            var fromAndJoins = StringBuilderPool.Shared.Get();
            var usingSources = StringBuilderPool.Shared.Get();
            var joinConditions = StringBuilderPool.Shared.Get();
            var whereSql = StringBuilderPool.Shared.Get();
            try
            {
                var targetSql = SqlSourceRenderer.MakeFrom(in _ctx, targetFrom, new FromRenderOptions(true, entityType, true, TablesInScopeHints: scopeHints), out var targetAlias);
                if (string.IsNullOrEmpty(targetAlias))
                    throw new NotSupportedException("A multi-table DELETE can only target a physical table, not a derived or function source.");

                // Table aliases are always escaped for the dialect (even when identifier quoting is off).
                var targetAliasToken = _ctx.Dialect.Escape(targetAlias);

                // The target without an alias, used by the PostgreSQL USING form (its DELETE target must
                // stay unaliased while the join conditions reference the target alias).
                var target = SqlSourceRenderer.MakeFrom(in _ctx, targetFrom, new FromRenderOptions(false, null, false, TablesInScopeHints: scopeHints));

                if (_ctx.Dialect.DeleteJoinRequiresUsing)
                {
                    for (var i = 0; i < joins.Length; i++)
                    {
                        var (fromSql, conditionSql) = SqlSourceRenderer.MakeJoinParts(in _ctx, joins[i], entityType, scopeHints);

                        if (i > 0)
                        {
                            usingSources.Append(", ");
                            joinConditions.Append(Kw(" and "));
                        }

                        usingSources.Append(fromSql);
                        joinConditions.Append(conditionSql);
                    }
                }
                else
                {
                    fromAndJoins.Append(targetSql);
                    for (var i = 0; i < joins.Length; i++)
                        fromAndJoins.Append(SqlSourceRenderer.MakeJoin(in _ctx, joins[i], entityType, scopeHints));
                }

                if (source.PreparedCondition is { } condition)
                    SqlSourceRenderer.MakeWhere(in _ctx, whereSql, entityType, condition, 0);

                var sql = _ctx.Dialect.MakeDeleteJoin(
                    target,
                    targetAliasToken,
                    fromAndJoins.ToString(),
                    usingSources.ToString(),
                    joinConditions.ToString(),
                    whereSql.Length == 0 ? null : whereSql.ToString(),
                    _ctx.KeywordCase);

                if (inlineHints is { Count: > 0 })
                    sql = ApplyInlineHints(sql, inlineHints);

                var returning = MakeJoinReturning(cmd.ReturningColumns, cmd.ReturningProjection, _ctx.Dialect.SupportsDeleteJoinReturning, "removed");
                if (returning is not null)
                    sql += _ctx.Dialect.MakeReturning(returning, _ctx.KeywordCase);

                return (sql, _ctx.Params);
            }
            finally
            {
                StringBuilderPool.Shared.Return(whereSql);
                StringBuilderPool.Shared.Return(joinConditions);
                StringBuilderPool.Shared.Return(usingSources);
                StringBuilderPool.Shared.Return(fromAndJoins);
            }
        }
        finally
        {
            _ctx.ColumnsProvider.PopSourceScope();
        }
    }

    /// <summary>
    /// Renders a multi-table <c>UPDATE</c> over the prepared joined source of <paramref name="cmd"/>. The
    /// target is the first table of the chain; the <c>SET</c> list and the join conditions are rendered
    /// through the shared <c>SELECT</c> source pipeline, so column aliasing matches the equivalent
    /// <c>SELECT</c>. The dialect composes its native form (PostgreSQL/SQLite <c>UPDATE ... FROM</c>,
    /// SQL Server <c>UPDATE &lt;alias&gt; ... FROM ... JOIN</c>, MySQL/MariaDB <c>UPDATE ... JOIN ... SET</c>).
    /// </summary>
    internal (string Sql, List<Parameter> Parameters) MakeUpdateJoin(UpdateJoinCommand cmd)
    {
        if (!_ctx.Dialect.SupportsUpdateJoin)
            throw new NotSupportedException($"{_ctx.Dialect.GetType().Name} cannot render a multi-table UPDATE.");

        var source = cmd.Source;

        if (source.EntityType is not { } entityType)
            throw new BuildSqlCommandException("A multi-table UPDATE command is missing its source entity type.");

        if (source.Joins is not { Length: > 0 } joins)
            throw new BuildSqlCommandException("A multi-table UPDATE needs at least one join.");

        if (source.From is not { } targetFrom)
            throw new BuildSqlCommandException("A multi-table UPDATE is missing its target source.");

        // Scope hints are structural on SQL Server (a WITH(...) on every physical table) and part of the
        // statement-level comment elsewhere. A dialect that supports neither rejects them here, before any
        // SQL is assembled — the same capability rule the SELECT pipeline applies (MakeSelect).
        var scopeHints = _ctx.Dialect.SupportsTablesInScopeHints ? source.TablesInScopeHints : null;
        if (source.TablesInScopeHints is { Count: > 0 }
            && !_ctx.Dialect.SupportsInlineHints
            && !_ctx.Dialect.SupportsTablesInScopeHints)
            throw new NotSupportedException("Tables-in-scope hints are not supported by this SQL dialect");

        var inlineHints = _ctx.Dialect.SupportsInlineHints ? MergeHints(source.Hints, CollectInlineHints(source)) : null;
        if (inlineHints is { Count: > 0 } && !_ctx.Dialect.SupportsQueryHints)
            throw new NotSupportedException("Query hints are not supported by this SQL dialect");

        _ctx.ColumnsProvider.PushSourceScope();
        try
        {
            var fromAndJoins = StringBuilderPool.Shared.Get();
            var usingSources = StringBuilderPool.Shared.Get();
            var joinConditions = StringBuilderPool.Shared.Get();
            var whereSql = StringBuilderPool.Shared.Get();
            try
            {
                var targetSql = SqlSourceRenderer.MakeFrom(in _ctx, targetFrom, new FromRenderOptions(true, entityType, true, TablesInScopeHints: scopeHints), out var targetAlias);
                if (string.IsNullOrEmpty(targetAlias))
                    throw new NotSupportedException("A multi-table UPDATE can only target a physical table, not a derived or function source.");

                // Table aliases are always escaped for the dialect (even when identifier quoting is off).
                var targetAliasToken = _ctx.Dialect.Escape(targetAlias);

                // The target without an alias, used by the FROM form (its UPDATE target must stay
                // unaliased in the source list while the assignments and conditions reference the alias).
                var target = SqlSourceRenderer.MakeFrom(in _ctx, targetFrom, new FromRenderOptions(false, null, false, TablesInScopeHints: scopeHints));

                if (_ctx.Dialect.UpdateJoinRequiresFrom)
                {
                    for (var i = 0; i < joins.Length; i++)
                    {
                        var (fromSql, conditionSql) = SqlSourceRenderer.MakeJoinParts(in _ctx, joins[i], entityType, scopeHints);

                        if (i > 0)
                        {
                            usingSources.Append(", ");
                            joinConditions.Append(Kw(" and "));
                        }

                        usingSources.Append(fromSql);
                        joinConditions.Append(conditionSql);
                    }
                }
                else
                {
                    fromAndJoins.Append(targetSql);
                    for (var i = 0; i < joins.Length; i++)
                        fromAndJoins.Append(SqlSourceRenderer.MakeJoin(in _ctx, joins[i], entityType, scopeHints));
                }

                // Rendered after the sources so each table alias is already registered. PostgreSQL and
                // SQLite keep the SET target unqualified (their UPDATE ... FROM target is implicit); the
                // alias-style dialects qualify it by the target alias.
                var assignments = SqlSourceRenderer.MakeUpdateAssignments(in _ctx, entityType, cmd.Assignments, qualifyTarget: !_ctx.Dialect.UpdateJoinRequiresFrom);

                if (source.PreparedCondition is { } condition)
                    SqlSourceRenderer.MakeWhere(in _ctx, whereSql, entityType, condition, 0);

                var sql = _ctx.Dialect.MakeUpdateJoin(
                    target,
                    targetAliasToken,
                    assignments,
                    fromAndJoins.ToString(),
                    usingSources.ToString(),
                    joinConditions.ToString(),
                    whereSql.Length == 0 ? null : whereSql.ToString(),
                    _ctx.KeywordCase);

                if (inlineHints is { Count: > 0 })
                    sql = ApplyInlineHints(sql, inlineHints);

                var returning = MakeJoinReturning(cmd.ReturningColumns, cmd.ReturningProjection, _ctx.Dialect.SupportsUpdateJoinReturning, "updated");
                if (returning is not null)
                    sql += _ctx.Dialect.MakeReturning(returning, _ctx.KeywordCase);

                return (sql, _ctx.Params);
            }
            finally
            {
                StringBuilderPool.Shared.Return(whereSql);
                StringBuilderPool.Shared.Return(joinConditions);
                StringBuilderPool.Shared.Return(usingSources);
                StringBuilderPool.Shared.Return(fromAndJoins);
            }
        }
        finally
        {
            _ctx.ColumnsProvider.PopSourceScope();
        }
    }

    /// <summary>Resolves a lower-case keyword fragment (keywords and separators only) to the configured <see cref="KeywordCase"/>.</summary>
    private string Kw(string text) => SqlKeywords.Of(_ctx.KeywordCase, text);

    /// <summary>
    /// Renders the <c>RETURNING</c> list of a multi-table mutation, qualifying every returned column by
    /// the alias of the joined table that owns it. The selector runs over the positional join projection,
    /// so a selected member may reference any joined source
    /// (<c>p =&gt; new { p.Item1.Id, p.Item2.Name }</c>). Returns <see langword="null"/> when the mutation
    /// returns nothing.
    /// </summary>
    /// <param name="columns">The mapped columns the mutation returns, or <see langword="null"/> for a plain mutation.</param>
    /// <param name="projection">The selector that defines the returned columns, or <see langword="null"/>.</param>
    /// <param name="returningSupported">Whether the dialect can return rows from this multi-table mutation.</param>
    /// <param name="operation">The past-tense operation name used in the rejection message.</param>
    /// <returns>The rendered returning expressions, or <see langword="null"/>.</returns>
    /// <exception cref="NotSupportedException">Rows were requested but the dialect cannot return them.</exception>
    private IReadOnlyList<string>? MakeJoinReturning(
        IReadOnlyList<IPropertyMetadata>? columns,
        LambdaExpression? projection,
        bool returningSupported,
        string operation)
    {
        if (columns is not { Count: > 0 } || projection is null)
            return null;

        if (!returningSupported)
            throw new NotSupportedException($"{_ctx.Dialect.GetType().Name} cannot return {operation} rows from a multi-table mutation.");

        var parameter = projection.Parameters[0];
        _ctx.ColumnsProvider.PushScope(projection.Parameters);
        try
        {
            var items = EnumerateReturningMembers(projection);
            var rendered = new List<string>(items.Count);
            var isIdentity = TypeFacts.UnwrapConvert(projection.Body) is ParameterExpression;
            for (var i = 0; i < items.Count; i++)
            {
                using var visitor = _ctx.CreateColumnVisitor(parameter.Type, 0, dontNeedAlias: false);
                try
                {
                    visitor.Visit(items[i].Expression);
                }
                catch (BuildSqlCommandException ex) when (isIdentity && TryGetIdentityAddress(items[i].Expression, out var addressSlot, out var addressMember))
                {
                    // A joined source that exposes only a partial shape cannot satisfy the full returned
                    // item; fail before the database with the slot/member identity rather than the raw
                    // column-resolution error.
                    var itemOrdinal = addressSlot + 1;
                    throw new QueryPreparationException(
                        $"The identity multi-table RETURNING form cannot resolve member '{addressMember}' of item {itemOrdinal} ({projection.Parameters[0].Type.GetProperty("Item" + itemOrdinal)?.PropertyType.Name ?? "item"}) against the joined source shape; add the member to the joined source projection or drop it from the returned item.", ex);
                }

                // The outer CTE read addresses a derived-style member under the name the body exposes:
                // a projection member that keeps its source name (p.Item1.Id -> "Id") is read back under
                // the mapped column name ("id"), so aliasing it to the CLR name would make the outer
                // reference miss it. Only a renamed member (TargetId = p.Item1.Id) exposes the alias and
                // must be aliased with the dialect's quoted-identifier style.
                //
                // An identity member's name IS the deterministic per-slot alias the
                // outer CTE shape stores, so it must be emitted unconditionally — including on a derived
                // joined slot, where the visitor exposes no physical column name to compare against.
                var renamed = isIdentity
                    || (visitor.ColumnName is { } physical
                        && !string.Equals(physical, items[i].Name, StringComparison.OrdinalIgnoreCase));
                rendered.Add(renamed
                    ? visitor.ToString() + _ctx.Dialect.MakeColumnAlias(items[i].Name, _ctx.KeywordCase)
                    : visitor.ToString());
            }

            return rendered;
        }
        finally
        {
            _ctx.ColumnsProvider.PopScope();
        }
    }

    // Expands a joined returning selector into the member expressions whose column references are
    // qualified in the RETURNING list, paired with the projection member name the outer CTE read
    // addresses.
    private static List<(Expression Expression, string Name)> EnumerateReturningMembers(LambdaExpression projection)
    {
        var body = TypeFacts.UnwrapConvert(projection.Body);
        List<(Expression, string)> members;

        // The identity form expands every mapped property of every slot and exposes
        // each under a deterministic per-slot alias, so a self-join of the same type keeps its Item1/Item2
        // columns distinct in the RETURNING list and in the outer CTE read.
        if (body is ParameterExpression)
            return EnumerateIdentityMembers(projection);

        switch (body)
        {
            case NewExpression { Arguments.Count: > 0 } newExpression:
            {
                var newMembers = newExpression.Members;
                members = new List<(Expression, string)>(newExpression.Arguments.Count);
                for (var i = 0; i < newExpression.Arguments.Count; i++)
                {
                    var name = newMembers is not null && i < newMembers.Count
                        ? newMembers[i].Name
                        : newExpression.Constructor!.GetParameters()[i].Name!;
                    members.Add((newExpression.Arguments[i], name));
                }

                break;
            }

            case MemberInitExpression { Bindings.Count: > 0 } memberInit:
            {
                members = new List<(Expression, string)>(memberInit.Bindings.Count);
                for (var i = 0; i < memberInit.Bindings.Count; i++)
                {
                    if (memberInit.Bindings[i] is MemberAssignment assignment)
                        members.Add((assignment.Expression, assignment.Member.Name));
                }

                break;
            }

            case MemberExpression memberExpression:
                members = [(body, memberExpression.Member.Name)];
                break;

            default:
                throw new NotSupportedException(
                    $"A joined RETURNING projection member of node kind '{body.NodeType}' is not supported; select a mapped member. Project the returned columns explicitly, for example p => new {{ p.Item1.Id, p.Item2.Name }}.");
        }

        // Fail closed: an empty RETURNING list is never valid, so a selector that expands to zero members
        // throws instead of emitting a bare RETURNING.
        if (members.Count == 0)
        {
            throw new NotSupportedException(
                "A joined RETURNING projection selected no mapped members; project at least one explicitly, for example p => new { p.Item1.Id, p.Item2.Name }.");
        }

        return members;
    }

    // Expands the identity selector into p.ItemN.Property expressions, one per
    // mapped property of every slot, paired with the deterministic per-slot alias the outer CTE read
    // addresses. Mirrors JoinedReturningProjection.ParseIdentity's slot/property order.
    private static List<(Expression Expression, string Name)> EnumerateIdentityMembers(LambdaExpression projection)
    {
        var projectionType = projection.Parameters[0].Type;
        var entityTypes = projectionType.GetGenericArguments();
        var parameter = projection.Parameters[0];
        var entries = new List<(Expression Expression, int Slot, string Column)>();

        for (var slot = 0; slot < entityTypes.Length; slot++)
        {
            // Same eligibility as JoinedReturningProjection.ParseIdentity: registered metadata when it
            // exists, otherwise the slot's readable CLR surface (a metadata-less derived/read-CTE joined
            // side). Keeping the two in lockstep is what lets the parse-accepted slot render instead of
            // throwing only at ToSql()/Single().
            IReadOnlyList<IPropertyMetadata> slotProperties;
            if (DataContextCache.Metadata.TryGetValue(entityTypes[slot], out var metadata) && metadata.Properties.Count > 0)
            {
                slotProperties = metadata.Properties;
            }
            else
            {
                slotProperties = JoinedReturningProjection.ShapeColumns(entityTypes[slot]);
                if (slotProperties.Count == 0)
                    throw new QueryPreparationException(
                        $"No metadata is registered for {entityTypes[slot].Name} and it exposes no readable columns, so the identity multi-table RETURNING form cannot expand item {slot + 1} ('{projectionType.GetProperty("Item" + (slot + 1))?.Name ?? "Item" + (slot + 1)}' of {projectionType.Name}). Register every joined entity with From<T>/Join<T> on the same data context.");
            }

            var itemAccess = Expression.Property(parameter, "Item" + (slot + 1));
            foreach (var property in slotProperties)
            {
                if (property.RangeColumns is not null)
                    throw RangeColumnPairs.NotReturnable(property);

                entries.Add((Expression.Property(itemAccess, property.PropertyInfo), slot, property.ColumnName));
            }
        }

        // Allocate across the whole flattened output so a repeated column or an alias-shaped column name
        // cannot collapse two slots onto one alias. The same ordered pairs drive the prepared CTE shape
        // (see MutationCteQuery.BuildShape), so the RETURNING list and the outer read always agree.
        var pairs = new (int Slot, string Column)[entries.Count];
        for (var i = 0; i < entries.Count; i++)
            pairs[i] = (entries[i].Slot, entries[i].Column);

        var aliases = ProjectionAliasCache.AllocateIdentityAliases(pairs);
        var members = new List<(Expression, string)>(entries.Count);
        for (var i = 0; i < entries.Count; i++)
            members.Add((entries[i].Expression, aliases[i]));

        return members;
    }

    /// <summary>
    /// Recognizes an identity member access of the form <c>p.ItemN.Member</c> and yields the zero-based
    /// item slot (<c>ItemN</c> is one-based, so <c>slot = ordinal - 1</c>) and the CLR member name. Used
    /// to report a member of a returned identity item that the joined source shape cannot resolve, with
    /// the slot/member address instead of the raw column-resolution error.
    /// </summary>
    /// <param name="expression">The returning member expression to inspect.</param>
    /// <param name="slot">The zero-based item slot when the expression addresses an identity member.</param>
    /// <param name="member">The CLR member name when the expression addresses an identity member.</param>
    /// <returns><see langword="true"/> when the expression is a <c>p.ItemN.Member</c> access.</returns>
    private static bool TryGetIdentityAddress(Expression expression, out int slot, out string member)
    {
        slot = -1;
        member = string.Empty;

        if (expression is not MemberExpression memberAccess || memberAccess.Member is not PropertyInfo memberProperty)
            return false;

        var target = memberAccess.Expression;
        while (target is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
            target = unary.Operand;

        if (target is not MemberExpression itemAccess || itemAccess.Member is not PropertyInfo itemProperty)
            return false;

        var itemName = itemProperty.Name;
        if (itemName.Length <= 4
            || !itemName.StartsWith("Item", StringComparison.Ordinal)
            || !int.TryParse(itemName.AsSpan(4), out var ordinal)
            || ordinal < 1)
        {
            return false;
        }

        slot = ordinal - 1;
        member = memberProperty.Name;
        return true;
    }

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
    /// Collects the join-level, subquery-level and tables-in-scope hints carried by a command into the
    /// single hint list an inline-comment dialect renders as one <c>/*+ ... */</c> comment. Returns
    /// <c>null</c> when the command carries none of them.
    /// </summary>
    private static IReadOnlyList<string>? CollectInlineHints(QueryCommand cmd)
    {
        List<string>? list = null;

        if (cmd.TablesInScopeHints is { Count: > 0 } scope)
            list = [.. scope];

        if (cmd.Joins is { Length: > 0 } joins)
        {
            for (var i = 0; i < joins.Length; i++)
            {
                var join = joins[i];

                if (join.JoinHint is { } joinHint)
                    (list ??= []).Add(joinHint);

                if (join.From.SubQueryHint is { } joinSubQueryHint)
                    (list ??= []).Add(joinSubQueryHint);
            }
        }

        if (cmd.From?.SubQueryHint is { } subQueryHint)
            (list ??= []).Add(subQueryHint);

        return list;
    }

    /// <summary>
    /// Merges the statement-level hints with the collected inline variant hints. Returns
    /// <paramref name="statementHints"/> when there is nothing to add, so the common no-variant path
    /// does not allocate.
    /// </summary>
    private static IReadOnlyList<string>? MergeHints(IReadOnlyList<string>? statementHints, IReadOnlyList<string>? inlineHints)
    {
        if (inlineHints is not { Count: > 0 })
            return statementHints;

        if (statementHints is not { Count: > 0 })
            return inlineHints;

        var combined = new string[statementHints.Count + inlineHints.Count];
        for (var i = 0; i < statementHints.Count; i++)
            combined[i] = statementHints[i];
        for (var i = 0; i < inlineHints.Count; i++)
            combined[statementHints.Count + i] = inlineHints[i];
        return combined;
    }

    /// <summary>
    /// Inserts the statement-level <c>/*+ ... */</c> hint comment immediately after the DML statement's
    /// leading keyword (<c>DELETE</c>/<c>UPDATE</c>) — the position PostgreSQL <c>pg_hint_plan</c> and
    /// MySQL optimizer hints read. The multi-table DML renderers never emit a leading <c>WITH</c> here
    /// (the planner prepends a hoisted <c>WITH</c> afterwards), so the first token is always the verb.
    /// </summary>
    private static string ApplyInlineHints(string sql, IReadOnlyList<string> hints)
    {
        var keywordEnd = 0;
        while (keywordEnd < sql.Length && !char.IsWhiteSpace(sql[keywordEnd]))
            keywordEnd++;

        return sql.Insert(keywordEnd, " /*+ " + string.Join(" ", hints) + " */");
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
