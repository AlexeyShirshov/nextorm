using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;
using Microsoft.Extensions.Logging;

namespace NextORM.Core;

/// <summary>
/// Renders the source/condition parts of a statement — CTEs, FROM (tables, derived tables and
/// table-valued functions), JOIN/APPLY, WHERE/HAVING, ORDER BY and columns. Split out of
/// <see cref="SqlBuilder"/> (which keeps the statement assembly) so both stay cohesive and under the
/// god-class threshold; the emitted SQL and the walk order are unchanged.
/// </summary>
internal static class SqlSourceRenderer
{
    /// <summary>
    /// Renders the <c>with [recursive] name as (&lt;select&gt;), ...</c> prefix for <paramref name="ctes"/>.
    /// In parameter mode nothing is rendered but every CTE query is still walked so its parameters are
    /// collected in the same order as the SQL pass. <paramref name="maxRecursionStmt"/> receives the
    /// dialect's recursion option (if any), which the caller appends after the statement.
    /// </summary>
    internal static string? MakeWithClause(in SqlBuildContext ctx, IReadOnlyList<CteDefinition> ctes, out string? maxRecursionStmt)
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
            maxRecursionStmt = ctx.Dialect.MakeMaxRecursion(depth);

        if (ctx.ParamMode)
        {
            for (var (i, cnt) = (0, ctes.Count); i < cnt; i++)
            {
                var cte = ctes[i];
                var walker = new SqlBuilder(ctx with { ParamMode = true, ColumnsProvider = new DefaultColumnsProvider(), QueryProvider = cte.Query, AliasProvider = null });
                walker.MakeSelect(cte.Query);
            }

            return null;
        }

        var withBuilder = StringBuilderPool.Shared.Get();
        try
        {
            withBuilder.Append(ctx.Dialect.MakeWith(anyRecursive));

            for (var (i, cnt) = (0, ctes.Count); i < cnt; i++)
            {
                var cte = ctes[i];

                if (i > 0)
                    withBuilder.Append(", ");

                withBuilder.Append(cte.Name).Append(" as (");

                // Each CTE is rendered in isolation: a fresh columns provider keeps the outer source
                // list untouched, and a fresh alias provider makes alias numbering self-contained
                // (mirroring how UNION branches are rendered).
                var builder = new SqlBuilder(ctx with { ParamMode = false, ColumnsProvider = new DefaultColumnsProvider(), QueryProvider = cte.Query, AliasProvider = new DefaultAliasProvider() });
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

    internal static string? MakeJoin(in SqlBuildContext ctx, JoinExpression join, Type entityType)
    {
        if (join.JoinType is JoinType.Right && !ctx.Dialect.SupportsRightFullJoin
            || join.JoinType is JoinType.Full && (!ctx.Dialect.SupportsRightFullJoin || !ctx.Dialect.SupportsFullJoin))
            throw new NotSupportedException($"The {join.JoinType} join is not supported by this SQL dialect");

        if (join.Strictness is not JoinStrictness.Default || join.IsGlobal)
        {
            if (join.Strictness is not JoinStrictness.Default && !ctx.Dialect.SupportsJoinStrictness)
                throw new NotSupportedException($"The {join.Strictness} join modifier is not supported by this SQL dialect");
            if (join.IsGlobal && !ctx.Dialect.SupportsGlobalJoin)
                throw new NotSupportedException("The GLOBAL join modifier is not supported by this SQL dialect");
            if (join.JoinType is not (JoinType.Inner or JoinType.Left or JoinType.Right or JoinType.Full))
                throw new NotSupportedException($"The join modifier cannot be applied to a {join.JoinType} join");
        }

        if (join.JoinType is JoinType.CrossApply or JoinType.OuterApply)
            return MakeApplyJoin(in ctx, join);

        var sqlBuilder = ctx.ParamMode ? null : StringBuilderPool.Shared.Get();

        try
        {
            if (!ctx.ParamMode)
            {
                sqlBuilder!.Append(ctx.Dialect.MakeJoinKeyword(join.JoinType, join.Strictness, join.IsGlobal));
            }

            var joinCondition = join.JoinCondition;

            if (joinCondition is null)
            {
                var fromSql = MakeFrom(in ctx, join.From, new FromRenderOptions(true, join.EntityType ?? join.From.SourceType, false));
                if (!ctx.ParamMode)
                {
                    sqlBuilder!.Append(fromSql);
                }

                return ctx.ParamMode ? null : sqlBuilder!.ToString();
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
                ctx.ColumnsProvider.PushScope(joinCondition.Parameters);

            try
            {
                var dim = 1;
                if (joinCondition.Parameters[0].Type.TryGetProjectionDimension(out var joinDim))
                    dim = joinDim;

                var fromSql = MakeFrom(in ctx, join.From, new FromRenderOptions(true, joinCondition.Parameters[1].Type, false));
                if (!ctx.ParamMode)
                {
                    sqlBuilder!.Append(fromSql);
                }

                if (!ctx.ParamMode) sqlBuilder!.Append(" on ");
                MakeWhere(in ctx, sqlBuilder, entityType, joinCondition.Body, dim);
            }
            finally
            {
                if (scopedAdded)
                    ctx.ColumnsProvider.PopScope();
            }

            return ctx.ParamMode ? null : sqlBuilder!.ToString();
        }
        finally
        {
            if (sqlBuilder is not null)
                StringBuilderPool.Shared.Return(sqlBuilder);
        }
    }

    /// <summary>
    /// Renders a <c>CROSS APPLY</c>/<c>OUTER APPLY</c> (or lateral) source. Unlike a regular join
    /// there is no <c>ON</c> condition: the clause is produced entirely by the dialect. The source is
    /// still rendered through <see cref="MakeFrom"/> so a derived table/table-valued function is
    /// parenthesised and aliased, and in parameter mode it is walked for captured parameters.
    /// </summary>
    private static string? MakeApplyJoin(in SqlBuildContext ctx, JoinExpression join)
    {
        if (!ctx.Dialect.SupportsApply)
            throw new NotSupportedException($"The {join.JoinType} join is not supported by this SQL dialect");

        var source = MakeFrom(in ctx, join.From, new FromRenderOptions(true, join.EntityType ?? join.From.SourceType, false));

        return ctx.ParamMode ? null : ctx.Dialect.MakeApply(join.JoinType, source);
    }

    internal static string MakeFrom(in SqlBuildContext ctx, FromExpression from, FromRenderOptions options)
    {
        var (needAlias, entityType, hasJoins, tableHints, temporal) = options;

        if (from.LinqSource is not null)
            throw new NotSupportedException("SelectMany/GroupJoin sources are not supported by the SQL providers; they are only available on the in-memory provider.");

        if (temporal is not null && string.IsNullOrEmpty(from.Table))
            throw new NotSupportedException("The FOR SYSTEM_TIME clause can only be applied to a physical table source.");

        if (from.TableFunction is not null)
            return MakeTableFunction(in ctx, from, needAlias, entityType, hasJoins);

        if (from.Pivot is not null)
            return MakePivot(in ctx, from);

        if (!ctx.ParamMode && !string.IsNullOrEmpty(from.Table))
        {
            if (tableHints is { Count: > 0 } && !ctx.Dialect.SupportsTableHints)
                throw new NotSupportedException("Table hints are not supported by this SQL dialect");

            var sqlBuilder = StringBuilderPool.Shared.Get();
            try
            {
                sqlBuilder.Append(from.Table);

                // SQL Server places table hints after the table name and before its alias.
                if (tableHints is { Count: > 0 })
                    sqlBuilder.Append(ctx.Dialect.MakeTableHints(tableHints));

                // Both SQL Server and MariaDB require FOR SYSTEM_TIME before the alias.
                if (temporal is not null)
                    sqlBuilder.Append(ctx.Dialect.MakeTemporalTable(temporal));

                if (needAlias)
                {
                    if (hasJoins)
                    {
                        Debug.Assert(typeof(IProjection).IsAssignableFrom(entityType));
                        ctx.ColumnsProvider.Add(entityType!.GetGenericArguments()[0], false);
                    }
                    else
                        ctx.ColumnsProvider.Add(entityType!, false);

                    sqlBuilder.Append(ctx.Dialect.MakeTableAlias(ctx.AliasProvider!.GetNextAlias(from)));
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
                var sql = new SqlBuilder(in ctx).MakeSelect(cmd);

                if (ctx.ParamMode) return string.Empty;

                ctx.ColumnsProvider.Add(cmd, false);

                var sqlBuilder = StringBuilderPool.Shared.Get();
                try
                {
                    sqlBuilder.Append('(').Append(sql).Append(')');
                    if (needAlias || ctx.Dialect.RequireSubqueryAlias)
                    {
                        sqlBuilder.Append(ctx.Dialect.MakeTableAlias(ctx.AliasProvider!.GetNextAlias(from)));
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
    private static string MakeTableFunction(in SqlBuildContext ctx, FromExpression from, bool needAlias, Type? entityType, bool hasJoins)
    {
        var function = from.TableFunction!;
        var arguments = function.Arguments;

        // The built-in SqlFunctions.Sql table functions are provider-specific; user-defined [SqlTableFunction]
        // functions are emitted verbatim and are the caller's responsibility.
        if (typeof(CommonFunctions).IsAssignableFrom(function.Call.Method.DeclaringType) && !ctx.Dialect.SupportsTableFunction(function.Name))
            throw new NotSupportedException($"The table function '{function.Name}' is not supported by this provider.");

        if (ctx.ParamMode)
        {
            for (var (i, cnt) = (0, arguments.Count); i < cnt; i++)
            {
                using var visitor = new BaseExpressionVisitor(new VisitorOptions(entityType ?? typeof(object), ctx.Dialect, ctx.ColumnsProvider, 0, ctx.AliasProvider, ctx.ParameterProvider, ctx.QueryProvider, true, true, ctx.Params, ctx.Logger));
                visitor.Visit(arguments[i]);
            }

            return string.Empty;
        }

        var sqlBuilder = StringBuilderPool.Shared.Get();
        try
        {
            sqlBuilder.Append(ctx.Dialect.MakeFunction(function.Name, function.Schema)).Append('(');

            for (var (i, cnt) = (0, arguments.Count); i < cnt; i++)
            {
                if (i > 0)
                    sqlBuilder.Append(", ");

                using var visitor = new BaseExpressionVisitor(new VisitorOptions(entityType ?? typeof(object), ctx.Dialect, ctx.ColumnsProvider, 0, ctx.AliasProvider, ctx.ParameterProvider, ctx.QueryProvider, true, false, ctx.Params, ctx.Logger));
                visitor.Visit(arguments[i]);
                sqlBuilder.Append(visitor.ToString());
            }

            sqlBuilder.Append(')');

            var callSql = sqlBuilder.ToString();
            var wrappedCall = ctx.Dialect.WrapTableFunction(function.Name, callSql);
            if (!string.Equals(wrappedCall, callSql, StringComparison.Ordinal))
            {
                sqlBuilder.Clear();
                sqlBuilder.Append(wrappedCall);
            }

            if (!string.IsNullOrEmpty(function.WithClause))
                sqlBuilder.Append(" with (").Append(function.WithClause).Append(')');

            if (needAlias || ctx.Dialect.RequireSubqueryAlias)
            {
                if (hasJoins)
                {
                    Debug.Assert(typeof(IProjection).IsAssignableFrom(entityType));
                    ctx.ColumnsProvider.Add(entityType!.GetGenericArguments()[0], false);
                }
                else if (entityType is not null)
                    ctx.ColumnsProvider.Add(entityType, false);

                sqlBuilder.Append(ctx.Dialect.MakeTableAlias(ctx.AliasProvider!.GetNextAlias(from)));
            }

            return sqlBuilder.ToString();
        }
        finally
        {
            StringBuilderPool.Shared.Return(sqlBuilder);
        }
    }

    /// <summary>
    /// Renders a <c>PIVOT</c>/<c>UNPIVOT</c> source: the inner source is rendered recursively (without an
    /// alias, so its columns stay unqualified inside the pivot clause) and the provider dialect composes
    /// the clause. The result is always aliased (T-SQL requires it) and registered as a
    /// <see cref="TableAlias"/> source so its named output columns resolve. In parameter mode the pivot
    /// column expressions are still walked so their parameters are collected in the same order.
    /// </summary>
    private static string MakePivot(in SqlBuildContext ctx, FromExpression from)
    {
        var pivot = from.Pivot!;

        if (ctx.Dialect.Pivot is not { } pivotRenderer)
            throw new NotSupportedException(pivot.IsUnpivot
                ? "The UNPIVOT source construct is not supported by this provider."
                : "The PIVOT source construct is not supported by this provider.");

        var source = MakeFrom(in ctx, pivot.Inner, new FromRenderOptions(false, pivot.InnerEntityType, false, null, null));

        var aggregateColumn = string.Empty;
        var forColumn = string.Empty;
        if (!pivot.IsUnpivot)
        {
            aggregateColumn = RenderPivotColumn(in ctx, pivot.InnerEntityType, pivot.AggregateColumn!);
            forColumn = RenderPivotColumn(in ctx, pivot.InnerEntityType, pivot.ForColumn!);
        }

        if (ctx.ParamMode) return string.Empty;

        ctx.ColumnsProvider.Add(typeof(TableAlias), false);

        var alias = ctx.AliasProvider!.GetNextAlias(from);
        return pivot.IsUnpivot
            ? pivotRenderer.RenderUnpivot(pivot, source, alias)
            : pivotRenderer.RenderPivot(pivot, source, aggregateColumn, forColumn, alias);
    }

    /// <summary>Renders one pivot column expression (aggregate argument or <c>FOR</c> column) unqualified.</summary>
    private static string RenderPivotColumn(in SqlBuildContext ctx, Type innerEntityType, Expression expression)
    {
        using var visitor = ctx.CreateColumnVisitor(innerEntityType, 0, dontNeedAlias: true);
        visitor.Visit(expression);
        return ctx.ParamMode ? string.Empty : visitor.ToString();
    }

    internal static void MakeWhere(in SqlBuildContext ctx, StringBuilder? target, Type entityType, Expression condition, int dim)
    {
        using var visitor = ctx.CreateWhereVisitor(entityType, dim);
        visitor.VisitCondition(condition);

        // In parameter mode nothing is emitted (the walk still collects parameters); otherwise the
        // rendered clause is appended straight into the caller's builder, avoiding a temp string.
        if (ctx.ParamMode || target is null) return;

        visitor.WriteTo(target);
    }

    internal static string MakeSort(in SqlBuildContext ctx, Type entityType, Expression sorting, int dim)
    {
        // An ORDER BY expression can reference joined tables, so columns must keep their table alias
        // (otherwise a column name shared by two joined tables is ambiguous).
        using var visitor = ctx.CreateColumnVisitor(entityType, dim, dontNeedAlias: false);
        visitor.Visit(sorting);

        if (ctx.ParamMode) return string.Empty;

        return visitor.ToString();
    }

    internal static string MakeArrayJoin(in SqlBuildContext ctx, Type entityType, Expression expression)
    {
        using var visitor = ctx.CreateColumnVisitor(entityType, 0, dontNeedAlias: false);
        visitor.Visit(expression);

        if (ctx.ParamMode) return string.Empty;

        return visitor.ToString();
    }

    internal static (bool NeedAliasForColumn, string Column) MakeColumn(in SqlBuildContext ctx, SelectExpression selExp, Type entityType, bool dontNeedAlias, bool renameAware = false)
    {
        using var visitor = ctx.CreateColumnVisitor(entityType, 0, dontNeedAlias);
        visitor.Visit(selExp.Expression);

        if (ctx.ParamMode) return (false, string.Empty);

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
}
