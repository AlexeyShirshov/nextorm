using System.Linq.Expressions;
using System.Reflection;
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
            maxRecursionStmt = ctx.Dialect.MakeMaxRecursion(depth, ctx.KeywordCase);

        if (ctx.ParamMode)
        {
            for (var (i, cnt) = (0, ctes.Count); i < cnt; i++)
            {
                var cte = ctes[i];

                // A command whose WITH carries a data-modifying CTE disables plan caching
                // (QueryPreparer.PrepareCtes), so this parameter pass never runs for one; only read CTEs
                // are walked here.
                if (cte.Mutation is not null)
                    continue;

                var walker = new SqlBuilder(ctx with { ParamMode = true, ColumnsProvider = new DefaultColumnsProvider(), QueryProvider = cte.Query, AliasProvider = null });
                walker.MakeSelect(cte.Query);
            }

            return null;
        }

        var withBuilder = StringBuilderPool.Shared.Get();
        try
        {
            withBuilder.Append(ctx.Dialect.MakeWith(anyRecursive, ctx.KeywordCase));

            for (var (i, cnt) = (0, ctes.Count); i < cnt; i++)
            {
                var cte = ctes[i];

                if (i > 0)
                    withBuilder.Append(", ");

                withBuilder.Append(ctx.QuoteIdentifiers ? QuoteQualifiedIdentifier(ctx.Dialect, cte.Name) : cte.Name).Append(SqlKeywords.Of(ctx.KeywordCase, " as ("));

                if (cte.Mutation is not null)
                {
                    // A data-modifying CTE body is the INSERT itself; its parameters share the enclosing
                    // command's provider so the SQL pass and the (rarer) parameter pass number them alike.
                    var (insertSql, insertParams) = RenderMutation(in ctx, cte);
                    ctx.Params.AddRange(insertParams);
                    withBuilder.Append(insertSql);
                }
                else
                {
                    // Each CTE is rendered in isolation: a fresh columns provider keeps the outer source
                    // list untouched, and a fresh alias provider makes alias numbering self-contained
                    // (mirroring how UNION branches are rendered).
                    var builder = new SqlBuilder(ctx with { ParamMode = false, ColumnsProvider = new DefaultColumnsProvider(), QueryProvider = cte.Query, AliasProvider = new DefaultAliasProvider() });
                    withBuilder.Append(builder.MakeSelect(cte.Query));
                }

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

    /// <summary>
    /// Renders the <c>INSERT ... RETURNING</c> body of a data-modifying CTE. Only PostgreSQL accepts a
    /// data-modifying CTE body. The body may be a <c>VALUES</c> insert or an <c>INSERT ... SELECT</c>
    /// whose source is rendered here; any CTEs the source carries are dropped because they are already
    /// declared by the enclosing <c>WITH</c> (a data-modifying CTE sees only the outer CTEs).
    /// </summary>
    private static (string Sql, List<Parameter> Parameters) RenderMutation(in SqlBuildContext ctx, CteDefinition cte)
    {
        if (!ctx.Dialect.SupportsDataModifyingCtes)
            throw new NotSupportedException("Data-modifying common table expressions are only supported by PostgreSQL.");

        var mutation = cte.Mutation!;
        string? sourceSql = null;

        if (mutation.Source is not null)
        {
            var source = mutation.Source;
            if (source.Ctes is { Count: > 0 })
            {
                // The outer WITH already declares these CTEs; re-declaring them inside the body would
                // nest a data-modifying statement and be rejected by PostgreSQL.
                var stripped = source.CloneForCache();
                stripped.Ctes = null;
                source = stripped;
            }

            var start = ctx.Params.Count;
            var sourceCtx = ctx with
            {
                ColumnsProvider = new DefaultColumnsProvider(),
                QueryProvider = source,
                AliasProvider = ctx.ParamMode ? null : new DefaultAliasProvider(),
            };
            sourceSql = new SqlBuilder(in sourceCtx).MakeSelect(source);

            for (var i = start; i < ctx.Params.Count; i++)
            {
                if (NormParam.IsName(ctx.Params[i].Name))
                    throw new NotSupportedException("An INSERT ... SELECT source cannot use SqlFunctions.Parameter runtime placeholders; capture the value in a local variable instead.");
            }
        }

        return SqlMutationBuilder.MakeInsert(
            ctx.Dialect,
            ctx.QuoteIdentifiers,
            ctx.NamingConvention,
            mutation,
            ctx.KeywordCase,
            sourceSql,
            sourceSql is null ? null : [],
            parameterProvider: ctx.ParameterProvider);
    }

    internal static string? MakeJoin(in SqlBuildContext ctx, JoinExpression join, Type entityType)
    {
        if (join.JoinType is JoinType.Right && !ctx.Dialect.SupportsRightFullJoin
            || join.JoinType is JoinType.Full && (!ctx.Dialect.SupportsRightFullJoin || !ctx.Dialect.SupportsFullJoin))
            throw new NotSupportedException($"The {join.JoinType} join is not supported by this SQL dialect");

        if (join.JoinType is JoinType.Semi or JoinType.Anti or JoinType.Paste)
        {
            if (join.Strictness is not JoinStrictness.Default || join.IsGlobal)
                throw new NotSupportedException($"The join modifier cannot be applied to a {join.JoinType} join");

            if (join.JoinType is JoinType.Semi or JoinType.Anti)
            {
                if (!ctx.Dialect.SupportsSemiAntiJoin)
                    throw new NotSupportedException($"The {join.JoinType} join is not supported by this SQL dialect");
            }
            else if (!ctx.Dialect.SupportsPasteJoin)
            {
                throw new NotSupportedException("The PASTE join is not supported by this SQL dialect");
            }
        }

        ValidateJoinModifiers(in ctx, join);

        if (join.JoinType is JoinType.CrossApply or JoinType.OuterApply)
            return MakeApplyJoin(in ctx, join);

        var sqlBuilder = ctx.ParamMode ? null : StringBuilderPool.Shared.Get();

        try
        {
            if (!ctx.ParamMode)
            {
                sqlBuilder!.Append(ctx.Dialect.MakeJoinKeyword(join.JoinType, join.Strictness, join.IsGlobal, ctx.KeywordCase));
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
            var scopedAdded = JoinNeedsScope(joinCondition.Parameters);
            if (scopedAdded)
                ctx.ColumnsProvider.PushScope(joinCondition.Parameters);

            try
            {
                var dim = JoinDimension(joinCondition);

                var fromSql = MakeFrom(in ctx, join.From, new FromRenderOptions(true, joinCondition.Parameters[1].Type, false));
                if (!ctx.ParamMode)
                {
                    sqlBuilder!.Append(fromSql);
                }

                if (!ctx.ParamMode) sqlBuilder!.Append(SqlKeywords.Of(ctx.KeywordCase, " on "));
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
    /// Renders the aliased <c>FROM</c> source and the <c>ON</c> condition of a join separately, under the
    /// same column scope a repeated parameter type requires. Used by the multi-table <c>DELETE</c>
    /// renderer: PostgreSQL needs the source in <c>USING</c> and the condition in <c>WHERE</c>, while the
    /// alias-style dialects use <see cref="MakeJoin"/> instead. The condition is empty in parameter mode.
    /// </summary>
    internal static (string From, string Condition) MakeJoinParts(in SqlBuildContext ctx, JoinExpression join, Type entityType)
    {
        ValidateJoinModifiers(in ctx, join);

        var condition = join.JoinCondition
            ?? throw new BuildSqlCommandException("A multi-table DELETE only supports INNER joins, which carry a condition.");

        var scopedAdded = JoinNeedsScope(condition.Parameters);
        if (scopedAdded)
            ctx.ColumnsProvider.PushScope(condition.Parameters);

        try
        {
            var fromSql = MakeFrom(in ctx, join.From, new FromRenderOptions(true, condition.Parameters[1].Type, false));

            var conditionBuilder = StringBuilderPool.Shared.Get();
            try
            {
                if (!ctx.ParamMode)
                    MakeWhere(in ctx, conditionBuilder, entityType, condition.Body, JoinDimension(condition));

                return (fromSql, conditionBuilder.ToString());
            }
            finally
            {
                StringBuilderPool.Shared.Return(conditionBuilder);
            }
        }
        finally
        {
            if (scopedAdded)
                ctx.ColumnsProvider.PopScope();
        }
    }

    // The strictness/GLOBAL modifiers are ClickHouse-only and their applicability depends on the join
    // type; shared so the multi-table DELETE USING path rejects them exactly like the SELECT path.
    private static void ValidateJoinModifiers(in SqlBuildContext ctx, JoinExpression join)
    {
        if (join.Strictness is JoinStrictness.Default && !join.IsGlobal)
            return;

        if (join.Strictness is not JoinStrictness.Default && !ctx.Dialect.SupportsJoinStrictness)
            throw new NotSupportedException($"The {join.Strictness} join modifier is not supported by this SQL dialect");
        if (join.IsGlobal && !ctx.Dialect.SupportsGlobalJoin)
            throw new NotSupportedException("The GLOBAL join modifier is not supported by this SQL dialect");
        if (join.JoinType is not (JoinType.Inner or JoinType.Left or JoinType.Right or JoinType.Full))
            throw new NotSupportedException($"The join modifier cannot be applied to a {join.JoinType} join");
    }

    // A join condition needs a pushed column scope when two of its parameter types are the same (for
    // example a self-join), so each alias resolves within its own scope.
    private static bool JoinNeedsScope(IReadOnlyList<ParameterExpression> parameters)
    {
        for (var i = 1; i < parameters.Count; i++)
        {
            var type = parameters[i].Type;
            for (var j = 0; j < i; j++)
            {
                if (parameters[j].Type == type)
                    return true;
            }
        }

        return false;
    }

    // The join condition's dimension is the projection dimension of its left-hand parameter (1 for a
    // plain entity, N for a projection of N joined tables).
    private static int JoinDimension(LambdaExpression condition)
        => condition.Parameters[0].Type.TryGetProjectionDimension(out var dim) ? dim : 1;

    /// <summary>
    /// Renders a <c>CROSS APPLY</c>/<c>OUTER APPLY</c> (or lateral) source. Unlike a regular join
    /// there is no <c>ON</c> condition: the clause is produced entirely by the dialect. The source is
    /// still rendered through <see cref="MakeFrom(in SqlBuildContext, FromExpression, FromRenderOptions)"/> so a derived table/table-valued function is
    /// parenthesised and aliased, and in parameter mode it is walked for captured parameters.
    /// </summary>
    private static string? MakeApplyJoin(in SqlBuildContext ctx, JoinExpression join)
    {
        if (!ctx.Dialect.SupportsApply)
            throw new NotSupportedException($"The {join.JoinType} join is not supported by this SQL dialect");

        var source = MakeFrom(in ctx, join.From, new FromRenderOptions(true, join.EntityType ?? join.From.SourceType, false));

        if (ctx.ParamMode)
            return null;

        if (!string.IsNullOrEmpty(join.From.Table) && !ctx.Dialect.SupportsApplyOnPlainTable)
            return MakePlainTableApply(in ctx, join.JoinType, source);

        return ctx.Dialect.MakeApply(join.JoinType, source, ctx.KeywordCase);
    }

    // A plain table cannot reference the left-hand row, so CROSS/OUTER APPLY over it is an ordinary
    // CROSS/LEFT join. Required for dialects whose apply spelling uses LATERAL, because LATERAL is
    // only valid before a subquery, function or composite expression, never a bare table name.
    private static string MakePlainTableApply(in SqlBuildContext ctx, JoinType applyType, string source) => applyType switch
    {
        JoinType.CrossApply => SqlKeywords.Of(ctx.KeywordCase, " cross join ") + source,
        JoinType.OuterApply => SqlKeywords.Of(ctx.KeywordCase, " left join ") + source + SqlKeywords.Of(ctx.KeywordCase, " on true"),
        _ => throw new ArgumentOutOfRangeException(nameof(applyType), applyType, "Not an APPLY join type")
    };


    internal static string MakeFrom(in SqlBuildContext ctx, FromExpression from, FromRenderOptions options)
        => MakeFrom(in ctx, from, options, out _);

    /// <summary>
    /// Renders a <c>FROM</c> source. <paramref name="alias"/> receives the alias assigned to a physical
    /// table source (used by the multi-table <c>DELETE</c> renderer, which names the target alias), or
    /// <see langword="null"/> for sources that are not aliased physical tables.
    /// </summary>
    internal static string MakeFrom(in SqlBuildContext ctx, FromExpression from, FromRenderOptions options, out string? alias)
    {
        var (needAlias, entityType, hasJoins, tableHints, temporal, indexHints, indexHintKind) = options;
        alias = null;

        if (from.LinqSource is not null)
            throw new NotSupportedException("SelectMany/GroupJoin sources are not supported by the SQL providers; they are only available on the in-memory provider.");

        if (temporal is not null && string.IsNullOrEmpty(from.Table))
            throw new NotSupportedException("The FOR SYSTEM_TIME clause can only be applied to a physical table source.");

        if (from.TableFunction is not null)
            return MakeTableFunction(in ctx, from, needAlias, entityType, hasJoins);

        if (from.Pivot is not null)
            return MakePivot(in ctx, from);

        if (from.XmlNodes is not null)
            return MakeXmlNodes(in ctx, from, entityType);

        if (from.RawSqlSource is not null)
            return MakeRawSqlSource(in ctx, from, needAlias, entityType);

        if (!ctx.ParamMode && !string.IsNullOrEmpty(from.Table))
        {
            if (tableHints is { Count: > 0 } && !ctx.Dialect.SupportsTableHints)
                throw new NotSupportedException("Table hints are not supported by this SQL dialect");

            var indexRenderer = indexHints is not null ? ctx.Dialect.IndexHints : null;
            if (indexHints is not null && indexRenderer is null)
                throw new NotSupportedException("Index hints are not supported by this SQL dialect");

            var indexHintSql = indexHints is not null
                ? indexRenderer!.RenderIndexHint(indexHints, indexHintKind, ctx.KeywordCase)
                : null;

            var sqlBuilder = StringBuilderPool.Shared.Get();
            try
            {
                var tableName = from.Table!;
                if (from.IsAutoMapped && ctx.NamingConvention is { } namingConvention)
                    tableName = namingConvention.TableName(tableName, from.SourceIsInterface);

                sqlBuilder.Append(ctx.QuoteIdentifiers ? QuoteQualifiedIdentifier(ctx.Dialect, tableName) : tableName);

                // SQL Server places table hints and the index hint after the table name and before its
                // alias; MySQL/SQLite place the standalone index clause there too.
                if (indexHintSql is not null && indexRenderer is { MergesWithTableHints: true })
                    sqlBuilder.Append(ctx.Dialect.MakeTableHints(TableHintsWithIndex(tableHints, indexHintSql), ctx.KeywordCase));
                else
                {
                    if (tableHints is { Count: > 0 })
                        sqlBuilder.Append(ctx.Dialect.MakeTableHints(tableHints, ctx.KeywordCase));

                    if (indexHintSql is not null)
                        sqlBuilder.Append(indexHintSql);
                }

                // Both SQL Server and MariaDB require FOR SYSTEM_TIME before the alias.
                if (temporal is not null)
                    sqlBuilder.Append(ctx.Dialect.MakeTemporalTable(temporal, ctx.KeywordCase));

                if (needAlias || from.ColumnShape is not null)
                {
                    if (from.ColumnShape is not null)
                        ctx.ColumnsProvider.Add(from.ColumnShape, false);
                    else if (hasJoins && typeof(IProjection).IsAssignableFrom(entityType))
                        ctx.ColumnsProvider.Add(entityType!.GetGenericArguments()[0], false);
                    else
                        ctx.ColumnsProvider.Add(entityType!, false);

                    alias = ctx.AliasProvider!.GetNextAlias(from);
                    sqlBuilder.Append(ctx.Dialect.MakeTableAlias(alias, ctx.KeywordCase));
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
                    sqlBuilder.Append(ctx.Dialect.MakeTableAlias(ctx.AliasProvider!.GetNextAlias(from), ctx.KeywordCase));

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
    /// Returns <paramref name="hints"/> with <paramref name="indexToken"/> appended, or a single-element
    /// list when <paramref name="hints"/> is <c>null</c> or empty. Used to fold an index hint into the
    /// dialect's single <c>WITH (...)</c> table-hint clause (SQL Server).
    /// </summary>
    private static IReadOnlyList<string> TableHintsWithIndex(IReadOnlyList<string>? hints, string indexToken)
    {
        if (hints is not { Count: > 0 })
            return [indexToken];

        var combined = new string[hints.Count + 1];
        for (var i = 0; i < hints.Count; i++)
            combined[i] = hints[i];
        combined[^1] = indexToken;
        return combined;
    }

    /// <summary>
    /// Renders a raw SQL fragment as a derived table: <c>(&lt;sql&gt;) AS alias</c>. The fragment's
    /// named parameters (the public properties of <see cref="RawSqlSourceExpression.Parameters"/>) are
    /// bound into the enclosing command in both the parameter and the SQL pass, so the order matches.
    /// Columns are read through <see cref="TableAlias"/> accessors.
    /// </summary>
    private static string MakeRawSqlSource(in SqlBuildContext ctx, FromExpression from, bool needAlias, Type? entityType)
    {
        if (!ctx.Dialect.SupportsRawSqlSource)
            throw new NotSupportedException("Raw SQL as a FROM source is not supported by this SQL dialect");

        var raw = from.RawSqlSource!;

        if (raw.Parameters is not null)
        {
            var props = raw.Parameters.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            for (var (i, cnt) = (0, props.Length); i < cnt; i++)
            {
                var prop = props[i];
                ctx.Params.Add(new Parameter(prop.Name, prop.GetValue(raw.Parameters)));
            }
        }

        if (ctx.ParamMode)
            return string.Empty;

        var sqlBuilder = StringBuilderPool.Shared.Get();
        try
        {
            sqlBuilder.Append('(').Append(raw.Sql).Append(')');

            if (needAlias || ctx.Dialect.RequireSubqueryAlias)
            {
                if (entityType is not null)
                    ctx.ColumnsProvider.Add(entityType, false);

                sqlBuilder.Append(ctx.Dialect.MakeTableAlias(ctx.AliasProvider!.GetNextAlias(from), ctx.KeywordCase));
            }

            return sqlBuilder.ToString();
        }
        finally
        {
            StringBuilderPool.Shared.Return(sqlBuilder);
        }
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
                if (IsVerbatimArgument(function, i))
                    continue;

                using var visitor = new BaseExpressionVisitor(new VisitorOptions(entityType ?? typeof(object), ctx.Dialect, ctx.ColumnsProvider, 0, ctx.AliasProvider, ctx.ParameterProvider, ctx.QueryProvider, true, true, ctx.Params, ctx.Logger) { QuoteIdentifiers = ctx.QuoteIdentifiers, NamingConvention = ctx.NamingConvention, KeywordCase = ctx.KeywordCase, ParameterNamePrefix = ctx.ParameterNamePrefix });
                visitor.Visit(arguments[i]);
            }

            return string.Empty;
        }

        // A caller-declared result schema is rendered into the SQL; a provider that cannot express the
        // form rejects it even for a user-defined function, which is otherwise emitted verbatim.
        var columnDefinitions = function.ResultSchema == TableFunctionSchema.None ? null : RenderResultSchema(in ctx, function);
        var leadsWithSchema = columnDefinitions is not null && function.ResultSchema == TableFunctionSchema.LeadingArgument;

        var sqlBuilder = StringBuilderPool.Shared.Get();
        try
        {
            sqlBuilder.Append(ctx.Dialect.MakeFunction(function.Name, function.Schema)).Append('(');

            if (leadsWithSchema)
                sqlBuilder.Append(SqlLiteral.ToSqlStringLiteral(columnDefinitions!));

            for (var (i, cnt) = (0, arguments.Count); i < cnt; i++)
            {
                if (i > 0 || leadsWithSchema)
                    sqlBuilder.Append(", ");

                if (IsVerbatimArgument(function, i))
                {
                    sqlBuilder.Append(GetVerbatimArgument(arguments[i]));
                    continue;
                }

                using var visitor = new BaseExpressionVisitor(new VisitorOptions(entityType ?? typeof(object), ctx.Dialect, ctx.ColumnsProvider, 0, ctx.AliasProvider, ctx.ParameterProvider, ctx.QueryProvider, true, false, ctx.Params, ctx.Logger) { QuoteIdentifiers = ctx.QuoteIdentifiers, NamingConvention = ctx.NamingConvention, KeywordCase = ctx.KeywordCase, ParameterNamePrefix = ctx.ParameterNamePrefix });
                visitor.Visit(arguments[i]);
                sqlBuilder.Append(visitor.ToString());
            }

            if (!string.IsNullOrEmpty(function.CallClause))
                sqlBuilder.Append(function.CallClause);

            sqlBuilder.Append(')');

            var callSql = sqlBuilder.ToString();
            var wrappedCall = ctx.Dialect.WrapTableFunction(function.Name, callSql);
            if (!string.Equals(wrappedCall, callSql, StringComparison.Ordinal))
            {
                sqlBuilder.Clear();
                sqlBuilder.Append(wrappedCall);
            }

            if (!string.IsNullOrEmpty(function.WithClause))
                sqlBuilder.Append(SqlKeywords.Of(ctx.KeywordCase, " with (")).Append(function.WithClause).Append(')');

            if (needAlias || ctx.Dialect.RequireSubqueryAlias)
            {
                if (entityType is not null)
                    ctx.ColumnsProvider.Add(hasJoins && typeof(IProjection).IsAssignableFrom(entityType) ? entityType.GetGenericArguments()[0] : entityType, false);

                sqlBuilder.Append(ctx.Dialect.MakeTableFunctionAlias(
                    ctx.AliasProvider!.GetNextAlias(from),
                    function.ResultSchema == TableFunctionSchema.AliasColumnList ? columnDefinitions : null,
                    ctx.KeywordCase));
            }

            return sqlBuilder.ToString();
        }
        finally
        {
            StringBuilderPool.Shared.Return(sqlBuilder);
        }
    }

    /// <summary>
    /// Renders the column-definition list of a table function whose result schema is derived from its
    /// mapped row type: <c>name type, name type</c> over the row type's readable properties. The column
    /// name follows the active naming convention (or the declared <c>[Column]</c> name) and the type is
    /// the dialect's native mapping of the property's CLR type. A provider that cannot render the
    /// declared form (<see cref="ISqlDialect.SupportsResultSchema(TableFunctionSchema)"/>) rejects the
    /// source.
    /// </summary>
    private static string RenderResultSchema(in SqlBuildContext ctx, TableFunctionExpression function)
    {
        if (!ctx.Dialect.SupportsResultSchema(function.ResultSchema))
            throw new NotSupportedException(
                $"The table function '{function.Name}' declares a result schema in the '{function.ResultSchema}' form, which is not supported by this provider.");

        if (function.ResultType is null || !DataContextCache.Metadata.TryGetValue(function.ResultType, out var metadata) || metadata.Properties.Count == 0)
            throw new BuildSqlCommandException(
                $"The table function '{function.Name}' declares a result schema but its row type '{function.ResultType?.Name}' has no mapped properties.");

        var builder = StringBuilderPool.Shared.Get();
        try
        {
            var properties = metadata.Properties;
            for (var (i, cnt) = (0, properties.Count); i < cnt; i++)
            {
                if (i > 0)
                    builder.Append(", ");

                var property = properties[i];
                var name = SqlMutationBuilder.RenderColumnReference(ctx.Dialect, ctx.QuoteIdentifiers, property, ctx.NamingConvention);

                var propertyType = property.PropertyInfo.PropertyType;
                builder.Append(name)
                       .Append(' ')
                       .Append(ctx.Dialect.MakeResultColumnType(propertyType, Nullable.GetUnderlyingType(propertyType) is not null));
            }

            return builder.ToString();
        }
        finally
        {
            StringBuilderPool.Shared.Return(builder);
        }
    }

    /// <summary>
    /// Renders a SQL Server <c>xml.nodes()</c> rowset: <c>&lt;xml&gt;.nodes('xpath') as [alias]([column])</c>.
    /// The XML operand is an outer reference of the enclosing command (the left-hand row the apply is
    /// correlated with), so it renders as its table alias and column; the column alias is required for
    /// the unfolded <see cref="SqlFunctions.IXmlNodesRow.Value"/> to be addressable. Only SQL Server
    /// opts in (<see cref="IXmlFunctions.Supports"/> with <c>nodes</c>); every other provider throws.
    /// </summary>
    private static string MakeXmlNodes(in SqlBuildContext ctx, FromExpression from, Type? entityType)
    {
        var nodes = from.XmlNodes!;

        if (ctx.Dialect.XmlFunctions is not { } xmlFunctions || !xmlFunctions.Supports("nodes"))
            throw new NotSupportedException("The XML xml.nodes() rowset method is not supported by this provider.");

        if (ctx.ParamMode)
            return string.Empty;

        using var visitor = ctx.CreateColumnVisitor(entityType ?? typeof(object), 0, dontNeedAlias: false);
        visitor.Visit(nodes.Operand);
        var operand = visitor.ToString();

        var rendered = xmlFunctions.Render("nodes", operand, new[] { SqlLiteral.ToSqlStringLiteral(nodes.XPath) });

        ctx.ColumnsProvider.Add(entityType!, false);
        var alias = ctx.AliasProvider!.GetNextAlias(from);
        var column = GetSingleColumnName(entityType!, ctx.NamingConvention);

        var sqlBuilder = StringBuilderPool.Shared.Get();
        try
        {
            sqlBuilder.Append(rendered)
                      .Append(ctx.Dialect.MakeTableAlias(alias, ctx.KeywordCase))
                      .Append('(')
                      .Append(ctx.QuoteIdentifiers ? ctx.Dialect.QuoteIdentifier(column) : column)
                      .Append(')');

            return sqlBuilder.ToString();
        }
        finally
        {
            StringBuilderPool.Shared.Return(sqlBuilder);
        }
    }

    /// <summary>Resolves the single mapped column of an <c>xml.nodes()</c> row shape.</summary>
    private static string GetSingleColumnName(Type rowType, INamingConvention? convention)
    {
        var props = rowType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        for (var (i, cnt) = (0, props.Length); i < cnt; i++)
        {
            var name = props[i].GetPropertyColumnName(convention);
            if (!string.IsNullOrEmpty(name))
                return name;
        }

        throw new BuildSqlCommandException($"The {rowType.Name} row shape does not declare a mapped column.");
    }

    private static bool IsVerbatimArgument(TableFunctionExpression function, int index)
    {
        var indexes = function.VerbatimArguments;
        if (indexes is null)
            return false;

        for (var (i, cnt) = (0, indexes.Count); i < cnt; i++)
        {
            if (indexes[i] == index)
                return true;
        }

        return false;
    }

    private static string GetVerbatimArgument(Expression argument) =>
        argument is ConstantExpression { Value: string text }
            ? text
            : throw new NotSupportedException("A verbatim table-function argument must be a constant string.");

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
            ? pivotRenderer.RenderUnpivot(pivot, source, alias, ctx.KeywordCase)
            : pivotRenderer.RenderPivot(pivot, source, aggregateColumn, forColumn, alias, ctx.KeywordCase);
    }

    /// <summary>Renders one pivot column expression (aggregate argument or <c>FOR</c> column) unqualified.</summary>
    private static string RenderPivotColumn(in SqlBuildContext ctx, Type innerEntityType, Expression expression)
    {
        using var visitor = ctx.CreateColumnVisitor(innerEntityType, 0, dontNeedAlias: true);
        visitor.Visit(expression);
        return ctx.ParamMode ? string.Empty : visitor.ToString();
    }

    internal static void MakeWhere(in SqlBuildContext ctx, StringBuilder? target, Type entityType, Expression condition, int dim)
        => MakeWhere(in ctx, target, entityType, condition, dim, dontNeedAlias: false);

    /// <summary>
    /// Renders a condition without a table qualifier. Used by the statement types whose target table
    /// is not aliased (<c>DELETE FROM &lt;table&gt; WHERE ...</c>): a single-source condition resolves
    /// its columns unqualified instead of failing to find an alias.
    /// </summary>
    internal static void MakeWhere(in SqlBuildContext ctx, StringBuilder? target, Type entityType, Expression condition, int dim, bool dontNeedAlias)
    {
        using var visitor = ctx.CreateWhereVisitor(entityType, dim, dontNeedAlias);
        visitor.VisitCondition(condition);

        // In parameter mode nothing is emitted (the walk still collects parameters); otherwise the
        // rendered clause is appended straight into the caller's builder, avoiding a temp string.
        if (ctx.ParamMode || target is null) return;

        visitor.WriteTo(target);
    }

    /// <summary>
    /// Renders the <c>SET</c> list of a multi-table <c>UPDATE</c> (without the <c>SET</c> keyword). The
    /// right-hand side is always rendered in aliased mode, so a column of any joined table keeps its
    /// table alias; a constant right-hand side is bound as a parameter. When
    /// <paramref name="qualifyTarget"/> is <c>false</c> (PostgreSQL and SQLite, whose <c>UPDATE ... FROM</c>
    /// target is implicit) the left-hand column is rendered unqualified, because a table-qualified target
    /// is parsed as a composite-field access there. The parameter provider is shared with the join
    /// conditions and the filter so numbering stays contiguous.
    /// </summary>
    internal static string MakeUpdateAssignments(in SqlBuildContext ctx, Type entityType, IReadOnlyList<UpdateJoinAssignment> assignments, bool qualifyTarget)
    {
        var builder = StringBuilderPool.Shared.Get();
        try
        {
            for (var i = 0; i < assignments.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");

                var assignment = assignments[i];

                using (var target = ctx.CreateColumnVisitor(entityType, 0, dontNeedAlias: !qualifyTarget))
                {
                    target.Visit(assignment.Target);
                    if (!ctx.ParamMode) builder.Append(target.ToString());
                }

                builder.Append(" = ");

                if (assignment.Kind == UpdateValueKind.Constant)
                {
                    var name = ctx.ParameterProvider.GetParamName();
                    ctx.Params.Add(new Parameter(name, DurationStorage.ToParameterValue(assignment.Constant, ResolveTargetProperty(entityType, assignment.Target), ctx.Dialect)));
                    builder.Append(ctx.Dialect.MakeParam(name));
                }
                else
                {
                    using var value = ctx.CreateColumnVisitor(entityType, 0, dontNeedAlias: false);
                    value.Visit(assignment.Value!);
                    if (!ctx.ParamMode) builder.Append(value.ToString());
                }
            }

            return builder.ToString();
        }
        finally
        {
            StringBuilderPool.Shared.Return(builder);
        }
    }

    // Resolves the mapped property written by an update-by-join assignment, so its duration storage
    // unit can be applied to a captured constant. The target member may sit behind a projection member
    // (p.Item1.Name), but the final MemberExpression's PropertyInfo belongs to the target entity type.
    private static IPropertyMetadata? ResolveTargetProperty(Type entityType, Expression target)
    {
        if (target is MemberExpression { Member: PropertyInfo pi }
            && DataContextCache.Metadata.TryGetValue(entityType, out var metadata))
            return metadata.Properties.FirstOrDefault(p => p.PropertyInfo == pi);

        return null;
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

    /// <summary>
    /// Quotes a possibly schema-qualified identifier (<c>schema.table</c>, <c>db.schema.table</c>) by
    /// quoting each dot-separated part separately, so a quoted qualified name stays a qualified name
    /// instead of becoming one identifier that contains dots.
    /// </summary>
    private static string QuoteQualifiedIdentifier(ISqlDialect dialect, string name)
    {
        if (name.IndexOf('.') < 0)
            return dialect.QuoteIdentifier(name);

        var parts = name.Split('.');
        for (var i = 0; i < parts.Length; i++)
            parts[i] = dialect.QuoteIdentifier(parts[i]);

        return string.Join('.', parts);
    }
}
