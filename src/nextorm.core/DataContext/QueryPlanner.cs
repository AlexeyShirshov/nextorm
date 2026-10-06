using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace NextORM.Core;

/// <summary>
/// Planning axis of a database-backed context: turns a <see cref="QueryCommand{TResult}"/> into a
/// prepared command (SQL, compiled row mapper, bound parameters) and resolves table sources.
/// Extracted out of <c>DataContext</c> so the context no longer owns the planning algorithm (SRP).
/// Provider hooks (dialect, column-mapping policy, parameter factory) and the connection / logger /
/// enumerator configuration arrive as constructor dependencies, so the planner never sees the
/// concrete context (DIP).
/// </summary>
internal sealed class QueryPlanner : IQueryPlanner
{
    private readonly IDataContext _context;
    private readonly Func<ISqlDialect> _dialect;
    private readonly ILogger? _logger;
    private readonly Type _contextType;
    private readonly Func<SelectExpression, Expression, Expression> _mapColumn;
    private readonly Func<DbCommand, string, object?, DbParameter> _createParam;
    private readonly Func<string, DbCommand> _createCommand;
    private readonly ILogger? _resultSetEnumeratorLogger;
    private readonly ILogger? _queryFilterLogger;
    private readonly bool _logSensitiveData;
    private readonly InterceptorHooks _interceptors;
    private readonly Func<CteMutation, SqlBuildContext, string> _renderMutationBody;

    // Cached-path diagnostic counter for the plans that must fall back to the original extraction path
    // (no recipe or a rejected one). Process-wide but updated with Interlocked and read with Volatile,
    // so the increment path is lock-free and allocation-free.
    private static int _fallbackRefreshes;

    /// <summary>Number of cached-hit parameter refreshes that used the original <c>ExtractParams</c> path.</summary>
    internal static int FallbackRefreshes => Volatile.Read(ref _fallbackRefreshes);

    /// <summary>Resets the planner's cached-path diagnostic counters to zero. Test seam.</summary>
    internal static void ResetCounters() => Volatile.Write(ref _fallbackRefreshes, 0);

    internal QueryPlanner(
        IDataContext context,
        Func<ISqlDialect> dialect,
        Type contextType,
        ProviderHooks hooks,
        LoggingOptions logging,
        InterceptorHooks interceptors)
    {
        _context = context;
        _dialect = dialect;
        _logger = logging.Logger;
        _contextType = contextType;
        _mapColumn = hooks.MapColumn;
        _createParam = hooks.CreateParam;
        _createCommand = hooks.CreateCommand;
        _resultSetEnumeratorLogger = logging.ResultSetEnumeratorLogger;
        _queryFilterLogger = logging.QueryFilterLogger;
        _logSensitiveData = logging.LogSensitiveData;
        _interceptors = interceptors;
        _renderMutationBody = RenderMutationBody;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private List<Parameter> ExtractParams(QueryCommand queryCommand)
    {
        var @params = new List<Parameter>();
        MakeSelect(queryCommand, true, @params, queryCommand, null, false);
        return @params;
    }

    // SqlFunctions.Parameter placeholders carry no value - the runtime values are applied by
    // DbPreparedQueryCommand.GetDbCommand. Only computed parameters (captured variables,
    // closures) need to be re-extracted on every cached execution.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsRuntimeParam(string name) => NormParam.IsName(name);

    // Renders a standalone SELECT (the source of an INSERT ... SELECT) to SQL plus its parameters, and
    // hoists any CTE the source declares out of the SELECT. A data-modifying CTE must sit at the top
    // level of the statement, so the caller places WithSql before "insert into"; a plain read CTE is
    // hoisted the same way. The WITH and the SELECT share one parameter provider so the numbering matches
    // the emitted SQL. Runtime SqlFunctions.Parameter placeholders cannot be bound through a mutation
    // command, so they are rejected with an actionable message.
    internal (string? WithSql, string Sql, List<Parameter> Parameters) RenderSource(QueryCommand source)
        => RenderSource(source, null, null, null);

    // Renders the source with a SELECT ... INTO clause injected into the top-level select list (SQL Server
    // CTAS). Only the outer select carries it; CTE, UNION and derived-table bodies render without it.
    internal (string? WithSql, string Sql, List<Parameter> Parameters) RenderSource(QueryCommand source, string? selectInto)
        => RenderSource(source, null, null, selectInto);

    // Overload used by a full MERGE so the source body, the match/branch conditions and the VALUES rows
    // share one parameter provider and accumulator (otherwise their autogenerated names would collide).
    internal (string? WithSql, string Sql, List<Parameter> Parameters) RenderSource(QueryCommand source, IParameterProvider? parameterProvider, List<Parameter>? parameters, string? selectInto = null, string parameterNamePrefix = "")
    {
        if (!source.IsPrepared)
            source.PrepareCommand(false, CancellationToken.None);

        var @params = parameters ?? new List<Parameter>();
        var ctx = new SqlBuildContext
        {
            Dialect = _dialect(),
            ParamMode = false,
            Params = @params,
            ColumnsProvider = new DefaultColumnsProvider(),
            QueryProvider = source,
            ParameterProvider = parameterProvider ?? new DefaultParameterProvider(),
            AliasProvider = new DefaultAliasProvider(),
            Logger = _logger!,
            QuoteIdentifiers = source.ResolvedQuoteIdentifiers,
            NamingConvention = source.ResolvedNamingConvention,
            KeywordCase = source.ResolvedKeywordCase,
            ParameterNamePrefix = parameterNamePrefix,
            RenderMutationBody = _renderMutationBody,
        };

        string? withSql = null;
        var body = source;
        if (source.Ctes is { Count: > 0 } ctes)
        {
            withSql = SqlSourceRenderer.MakeWithClause(in ctx, ctes, out _);

            body = source.CloneForCache();
            body.Ctes = null;
            ctx = ctx with { QueryProvider = body };
        }

        var sql = new SqlBuilder(in ctx).MakeSelect(body, selectInto);

        for (var i = 0; i < @params.Count; i++)
        {
            if (NormParam.IsName(@params[i].Name))
                throw new NotSupportedException(
                    "A query rendered as a source (INSERT ... SELECT, MERGE or a batch) cannot use SqlFunctions.Parameter runtime placeholders; capture the value in a local variable instead.");
        }

        return (withSql, sql!, @params);
    }

    // Renders the WHERE predicate of a command as a standalone, unqualified condition, used by
    // DELETE FROM <table> WHERE <condition>. The command is prepared first so the condition matches
    // the SELECT pipeline (captured values become parameters, correlated subqueries are rewritten);
    // only the condition is rendered - no FROM or select list.
    internal (string Sql, List<Parameter> Parameters) RenderPredicate(QueryCommand command)
        => RenderPredicate(command, null, null);

    // Overload used by UPDATE so the WHERE parameters continue the sequence started by the SET list
    // (a fresh provider/list would restart parameter numbering and collide with the SET parameters).
    internal (string Sql, List<Parameter> Parameters) RenderPredicate(QueryCommand command, IParameterProvider? parameterProvider, List<Parameter>? parameters, string parameterNamePrefix = "")
    {
        if (!command.IsPrepared)
            command.PrepareCommand(false, CancellationToken.None);

        var @params = parameters ?? new List<Parameter>();
        if (command.PreparedCondition is null)
            return (string.Empty, @params);

        var ctx = new SqlBuildContext
        {
            Dialect = _dialect(),
            ParamMode = false,
            Params = @params,
            ColumnsProvider = new DefaultColumnsProvider(),
            QueryProvider = command,
            ParameterProvider = parameterProvider ?? new DefaultParameterProvider(),
            AliasProvider = new DefaultAliasProvider(),
            Logger = _logger!,
            QuoteIdentifiers = command.ResolvedQuoteIdentifiers,
            NamingConvention = command.ResolvedNamingConvention,
            KeywordCase = command.ResolvedKeywordCase,
            ParameterNamePrefix = parameterNamePrefix,
        };
        ctx.ColumnsProvider.Add(command.EntityType!, false);

        var builder = new StringBuilder();
        SqlSourceRenderer.MakeWhere(in ctx, builder, command.EntityType!, command.PreparedCondition, 0, dontNeedAlias: true);
        return (builder.ToString(), @params);
    }

    // Renders the body of a data-modifying CTE whose command is a single-table UPDATE/DELETE carrying a
    // RETURNING projection. The SET list and the WHERE reuse the same helpers as a standalone mutation,
    // and their parameters are written into the enclosing statement's accumulator/provider (carried on
    // ctx) so the body's placeholders continue the outer statement's numbering. A body without a
    // RETURNING projection, or a multi-table mutation, is rejected until its rendering is implemented.
    private string RenderMutationBody(CteMutation mutation, SqlBuildContext ctx)
    {
        switch (mutation.Command)
        {
            case UpdateCommand update:
            {
                if (update.ReturningColumns is not { Count: > 0 })
                    throw new NotSupportedException("A data-modifying CTE UPDATE body requires a RETURNING projection; call Returning(...) on the update builder.");

                var (setSql, _) = RenderAssignments(update, ctx.ParameterProvider, ctx.Params, ctx.ParameterNamePrefix);
                var (whereSql, _) = RenderPredicate(update.Source, ctx.ParameterProvider, ctx.Params, ctx.ParameterNamePrefix);
                var (sql, _) = SqlMutationBuilder.MakeUpdate(ctx.Dialect, ctx.QuoteIdentifiers, ctx.NamingConvention, update, setSql, ctx.Params, whereSql, ctx.ParameterProvider, ctx.KeywordCase);
                return sql;
            }

            case DeleteCommand delete:
            {
                if (delete.ReturningColumns is not { Count: > 0 })
                    throw new NotSupportedException("A data-modifying CTE DELETE body requires a RETURNING projection; call Returning(...) on the delete builder.");

                string? whereSql = null;
                if (delete.Condition is not null)
                {
                    var (rendered, _) = RenderPredicate(delete.Condition, ctx.ParameterProvider, ctx.Params, ctx.ParameterNamePrefix);
                    if (rendered.Length > 0)
                        whereSql = rendered;
                }

                var (sql, _) = SqlMutationBuilder.MakeDelete(ctx.Dialect, ctx.QuoteIdentifiers, ctx.NamingConvention, delete, whereSql, ctx.Params, ctx.KeywordCase, ctx.ParameterProvider);
                return sql;
            }

            case UpdateJoinCommand updateJoin:
            {
                if (updateJoin.ReturningProjection is null || updateJoin.ReturningColumns is not { Count: > 0 })
                    throw new NotSupportedException("A data-modifying CTE multi-table UPDATE body requires a RETURNING projection; call Returning(...) on the update builder.");

                if (!ctx.Dialect.SupportsUpdateJoinReturning)
                    throw new NotSupportedException("A multi-table UPDATE data-modifying common table expression body is only supported by PostgreSQL.");

                return RenderUpdateJoinCore(updateJoin, ctx.ParameterProvider, ctx.Params, suppressCtes: true).Sql;
            }

            case DeleteJoinCommand deleteJoin:
            {
                if (deleteJoin.ReturningProjection is null || deleteJoin.ReturningColumns is not { Count: > 0 })
                    throw new NotSupportedException("A data-modifying CTE multi-table DELETE body requires a RETURNING projection; call Returning(...) on the delete builder.");

                if (!ctx.Dialect.SupportsDeleteJoinReturning)
                    throw new NotSupportedException("A multi-table DELETE data-modifying common table expression body is only supported by PostgreSQL.");

                return RenderDeleteJoinCore(deleteJoin, ctx.ParameterProvider, ctx.Params, suppressCtes: true).Sql;
            }

            default:
                throw new NotSupportedException($"A data-modifying common table expression body of type '{mutation.Command.GetType().Name}' is not supported yet; only a single-table UPDATE/DELETE ... RETURNING body is supported.");
        }
    }

    // Renders an ON/AND search condition of a full MERGE over the (target, source) row. The two lambda
    // parameters map to the literal aliases "target"/"source" (via MergeAliasProvider); the condition's
    // parameters join the same accumulator as the MERGE's VALUES rows and branch conditions.
    internal string RenderMergeCondition(
        LambdaExpression condition,
        Type entityType,
        IQueryRegistry registry,
        IParameterProvider parameterProvider,
        List<Parameter> parameters,
        bool quoteIdentifiers,
        INamingConvention? namingConvention,
        KeywordCase keywordCase)
    {
        var ctx = new SqlBuildContext
        {
            Dialect = _dialect(),
            ParamMode = false,
            Params = parameters,
            ColumnsProvider = new DefaultColumnsProvider(),
            QueryProvider = registry,
            ParameterProvider = parameterProvider,
            AliasProvider = MergeAliasProvider.Instance,
            Logger = _logger!,
            QuoteIdentifiers = quoteIdentifiers,
            NamingConvention = namingConvention,
            KeywordCase = keywordCase,
        };

        ctx.ColumnsProvider.Add(entityType, false);
        ctx.ColumnsProvider.Add(entityType, false);
        ctx.ColumnsProvider.PushScope(condition.Parameters);

        try
        {
            var builder = new StringBuilder();
            SqlSourceRenderer.MakeWhere(in ctx, builder, entityType, condition.Body, 1);
            return builder.ToString();
        }
        finally
        {
            ctx.ColumnsProvider.PopScope();
        }
    }

    // Renders the target entity's active global filter (after the effective IgnoreFilters scope) as a
    // "target"-alias-qualified MERGE search condition, appending its bound parameters to `parameters`.
    // The filters are reduced through the same entity/context substitution the SELECT pipeline uses
    // (QueryPreparer.TryBuildFilterBody) and rendered by RenderMergeCondition, so target/source
    // qualification, quoting and parameter binding are identical to the user's On(...)/branch condition.
    // Returns null only when the scope leaves no filter active (IgnoreFilters / no filter configured).
    // Fail-closed: an active, non-ignored filter that cannot be reduced to a predicate throws
    // NotSupportedException here (before any mutation) instead of being dropped. This render seam is the
    // single injection point for the target predicate: a caller under a scope that leaves a filter active
    // gets the AND-combined predicate or the refusal, never a silently omitted filter.
    internal string? RenderMergeTargetFilter(
        Type entityType,
        QueryFilterScope scope,
        QueryCommand registry,
        IParameterProvider parameterProvider,
        List<Parameter> parameters,
        bool quoteIdentifiers,
        INamingConvention? namingConvention,
        KeywordCase keywordCase)
    {
        var filters = QueryFilterResolver.GetFilters(entityType, scope, _context);
        if (filters.Count == 0)
            return null;

        var target = Expression.Parameter(entityType, "target");
        var source = Expression.Parameter(entityType, "source");
        Expression? body = null;
        for (var (i, cnt) = (0, filters.Count); i < cnt; i++)
        {
            if (!QueryCommand.QueryPreparer.TryBuildFilterBody(filters[i], _context, target, out var filterBody))
                throw new NotSupportedException(
                    $"An active global query filter on entity type '{entityType.Name}' cannot be translated into an atomic predicate on the merge write target, so the filter would be silently bypassed. Call IgnoreFilters() to disable the filter, or use a filter declared as a predicate or builder function.");

            body = body is null ? filterBody : Expression.AndAlso(body, filterBody);
        }

        if (body is null)
            return null;

        var condition = Expression.Lambda(body, target, source);
        return RenderMergeCondition(condition, entityType, registry, parameterProvider, parameters, quoteIdentifiers, namingConvention, keywordCase);
    }

    // Renders the SET list of an UPDATE command (without the SET keyword) together with its parameters.
    // A constant RHS is bound as a parameter, a column RHS renders an unqualified column reference, and
    // an arbitrary RHS expression is rendered by the same column visitor the SELECT/WHERE pipeline uses,
    // so captured variables become parameters and mapped members become quoted columns. The provider and
    // parameter list are shared with the WHERE renderer so names and numbering stay contiguous.
    internal (string SetSql, List<Parameter> Parameters) RenderAssignments(
        UpdateCommand command,
        IParameterProvider parameterProvider,
        List<Parameter> parameters,
        string parameterNamePrefix = "")
    {
        var source = command.Source;
        if (!source.IsPrepared)
            source.PrepareCommand(false, CancellationToken.None);

        var quoteIdentifiers = source.ResolvedQuoteIdentifiers;
        var namingConvention = source.ResolvedNamingConvention;

        var ctx = new SqlBuildContext
        {
            Dialect = _dialect(),
            ParamMode = false,
            Params = parameters,
            ColumnsProvider = new DefaultColumnsProvider(),
            QueryProvider = source,
            ParameterProvider = parameterProvider,
            AliasProvider = new DefaultAliasProvider(),
            Logger = _logger!,
            QuoteIdentifiers = quoteIdentifiers,
            NamingConvention = namingConvention,
            KeywordCase = source.ResolvedKeywordCase,
            ParameterNamePrefix = parameterNamePrefix,
        };
        ctx.ColumnsProvider.Add(command.EntityType!, false);

        var builder = new StringBuilder();
        for (var i = 0; i < command.Assignments.Count; i++)
        {
            if (i > 0)
                builder.Append(", ");

            var assignment = command.Assignments[i];
            builder.Append(SqlMutationBuilder.RenderColumnReference(ctx.Dialect, quoteIdentifiers, assignment.Property, namingConvention));
            builder.Append(" = ");

            switch (assignment.Kind)
            {
                case UpdateValueKind.Constant:
                    var name = parameterProvider.GetParamName();
                    parameters.Add(new Parameter(name, DurationStorage.ToParameterValue(assignment.Constant, assignment.Property, ctx.Dialect)));
                    builder.Append(ctx.Dialect.MakeParam(name));
                    break;
                case UpdateValueKind.Column:
                    builder.Append(SqlMutationBuilder.RenderColumnReference(ctx.Dialect, quoteIdentifiers, assignment.Column!, namingConvention));
                    break;
                default:
                    using (var visitor = ctx.CreateColumnVisitor(command.EntityType!, 0, dontNeedAlias: true))
                    {
                        visitor.Visit(assignment.Expression!);
                        visitor.WriteTo(builder);
                    }
                    break;
            }
        }

        // The entity store's dynamic columns are appended after the mapped assignments, each physical key
        // quoted through the dialect regardless of the global identifier-quoting flag. A present key is
        // always written; an omitted key leaves the column unchanged (it cannot be cleared by omission).
        if (command.DynamicColumns is not null)
        {
            var dynamicKeys = command.DynamicColumns.RenderKeys(ctx.Dialect);
            var dynamicNames = command.DynamicColumns.AddValueParameters(0, parameters, parameterProvider, ctx.Dialect);
            for (var d = 0; d < dynamicKeys.Length; d++)
            {
                if (builder.Length > 0)
                    builder.Append(", ");

                builder.Append(dynamicKeys[d]).Append(" = ").Append(ctx.Dialect.MakeParam(dynamicNames[d]));
            }
        }

        return (builder.ToString(), parameters);
    }

    // Renders a multi-table DELETE: the target is the first table of the prepared joined command, whose
    // joins and condition are rendered through the same source/condition pipeline as a SELECT, so aliases
    // and parameters match the equivalent read query.
    internal (string Sql, List<Parameter> Parameters) RenderDeleteJoin(DeleteJoinCommand command)
        => RenderDeleteJoinCore(command, new DefaultParameterProvider(), new List<Parameter>(), suppressCtes: false);

    // Core multi-table DELETE renderer shared by the standalone statement and the body of a data-modifying
    // CTE. The provider and parameter accumulator come from the caller so a CTE body continues the enclosing
    // statement's numbering. When the command is hoisted as a CTE body (suppressCtes) its own WITH clause
    // must not be re-emitted: the hoisted CTEs already sit at the top level of the statement.
    private (string Sql, List<Parameter> Parameters) RenderDeleteJoinCore(
        DeleteJoinCommand command,
        IParameterProvider parameterProvider,
        List<Parameter> parameters,
        bool suppressCtes)
    {
        var source = command.Source;
        if (!source.IsPrepared)
            source.PrepareCommand(false, CancellationToken.None);

        var ctx = new SqlBuildContext
        {
            Dialect = _dialect(),
            ParamMode = false,
            Params = parameters,
            ColumnsProvider = new DefaultColumnsProvider(),
            QueryProvider = source,
            ParameterProvider = parameterProvider,
            AliasProvider = new DefaultAliasProvider(),
            Logger = _logger!,
            QuoteIdentifiers = source.ResolvedQuoteIdentifiers,
            NamingConvention = source.ResolvedNamingConvention,
            KeywordCase = source.ResolvedKeywordCase,
            RenderMutationBody = _renderMutationBody,
        };

        string? withSql = null;
        string? maxRecursionStmt = null;
        if (!suppressCtes && source.Ctes is { Count: > 0 } ctes)
            withSql = SqlSourceRenderer.MakeWithClause(in ctx, ctes, out maxRecursionStmt);

        var (sql, renderedParams) = new SqlBuilder(in ctx).MakeDeleteJoin(command);
        if (withSql is not null)
            sql = withSql + sql;
        if (maxRecursionStmt is not null)
            sql += " " + maxRecursionStmt;
        return (sql, renderedParams);
    }

    // Renders a multi-table UPDATE: the target is the first table of the prepared joined command, whose
    // joins and condition are rendered through the same source/condition pipeline as a SELECT, so aliases
    // and parameters match the equivalent read query. The SET list shares the same parameter provider.
    internal (string Sql, List<Parameter> Parameters) RenderUpdateJoin(UpdateJoinCommand command)
        => RenderUpdateJoinCore(command, new DefaultParameterProvider(), new List<Parameter>(), suppressCtes: false);

    // Core multi-table UPDATE renderer shared by the standalone statement and the body of a data-modifying
    // CTE. The provider and parameter accumulator come from the caller so a CTE body continues the enclosing
    // statement's numbering. When the command is hoisted as a CTE body (suppressCtes) its own WITH clause
    // must not be re-emitted: the hoisted CTEs already sit at the top level of the statement.
    private (string Sql, List<Parameter> Parameters) RenderUpdateJoinCore(
        UpdateJoinCommand command,
        IParameterProvider parameterProvider,
        List<Parameter> parameters,
        bool suppressCtes)
    {
        var source = command.Source;
        if (!source.IsPrepared)
            source.PrepareCommand(false, CancellationToken.None);

        var ctx = new SqlBuildContext
        {
            Dialect = _dialect(),
            ParamMode = false,
            Params = parameters,
            ColumnsProvider = new DefaultColumnsProvider(),
            QueryProvider = source,
            ParameterProvider = parameterProvider,
            AliasProvider = new DefaultAliasProvider(),
            Logger = _logger!,
            QuoteIdentifiers = source.ResolvedQuoteIdentifiers,
            NamingConvention = source.ResolvedNamingConvention,
            KeywordCase = source.ResolvedKeywordCase,
            RenderMutationBody = _renderMutationBody,
        };

        string? withSql = null;
        string? maxRecursionStmt = null;
        if (!suppressCtes && source.Ctes is { Count: > 0 } ctes)
            withSql = SqlSourceRenderer.MakeWithClause(in ctx, ctes, out maxRecursionStmt);

        var (sql, renderedParams) = new SqlBuilder(in ctx).MakeUpdateJoin(command);
        if (withSql is not null)
            sql = withSql + sql;
        if (maxRecursionStmt is not null)
            sql += " " + maxRecursionStmt;
        return (sql, renderedParams);
    }

    private string? MakeSelect(QueryCommand queryCommand, bool paramMode, List<Parameter> @params, IQueryRegistry queryProvider, IAliasProvider? aliasProvider, bool sequentialAccess)
    {
        // The single render funnel for cold, warm-miss and prepared-miss renders (including the cached-hit
        // parameter refresh through ExtractParams). A previously prepared command skips re-preparation, so
        // the per-resolution guard cannot see that the bridge metadata was dropped; refuse to render the
        // unfiltered statement instead. A context with no imported-filter expectations is untouched.
        QueryFilterExpectations.EnsureExpectedFiltersPresent(_context);

        var ctx = new SqlBuildContext
        {
            Dialect = _dialect(),
            ParamMode = paramMode,
            Params = @params,
            ColumnsProvider = new DefaultColumnsProvider(),
            QueryProvider = queryProvider,
            ParameterProvider = new DefaultParameterProvider(),
            AliasProvider = aliasProvider,
            Logger = _logger!,
            QuoteIdentifiers = queryCommand.ResolvedQuoteIdentifiers,
            NamingConvention = queryCommand.ResolvedNamingConvention,
            KeywordCase = queryCommand.ResolvedKeywordCase,
            SequentialAccess = sequentialAccess,
            RenderMutationBody = _renderMutationBody,
        };
        var sqlBuilder = new SqlBuilder(in ctx);
        return sqlBuilder.MakeSelect(queryCommand);
    }

    public IPreparedQueryCommand<TResult> GetPreparedQueryCommand<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, CancellationToken cancellationToken)
        => GetPreparedQueryCommand(queryCommand, createEnumerator, storeInCache, false, false, cancellationToken);

    internal IPreparedQueryCommand<TResult> GetPreparedQueryCommand<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, bool sequentialAccess, CancellationToken cancellationToken)
        => GetPreparedQueryCommand(queryCommand, createEnumerator, storeInCache, sequentialAccess, false, cancellationToken);

    internal IPreparedQueryCommand<TResult> GetPreparedQueryCommand<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, bool sequentialAccess, bool streamingRowsRequested, CancellationToken cancellationToken)
    {
        // A data-modifying CTE makes the whole statement side-effecting, so its plan must never be
        // shared: the mutation's shape (row count, target columns) is not captured by the CTE query.
        // Clear the call-local storeInCache rather than queryCommand.Cache, which is sticky and would
        // disable plan caching for a shared command (the context-wide Any/Count command) on every later
        // query. Both the lookup and store gates below test storeInCache, so they skip.
        if (queryCommand.HasDataModifyingCte)
            storeInCache = false;

        QueryPlan? queryPlan = null;
        IDbCommandHolder? planCache = null;

        if (!queryCommand.IsPrepared) queryCommand.PrepareCommand(!storeInCache, cancellationToken);
        else queryCommand.RefreshInValuesShape();

        // A tuple value list in a clause whose shape is not part of the plan key (HAVING / JOIN ON /
        // SELECT / a nested subquery) renders a SQL/parameter shape that follows the captured collection.
        // Suppress the call-local cache for this call rather than mutating the sticky QueryCommand.Cache
        // flag, which would disable plan caching for a shared command (the context-wide Any/Count command)
        // on every later query. Both the lookup and store gates below test storeInCache, so they skip.
        if (queryCommand.HasUnkeyedTupleInValues)
            storeInCache = false;

        // A row enumeration that projects a live Stream/TextReader member needs the same sequential
        // access as the single-column LOB terminals: the SQL gains the dialect locator (SQLite rowid),
        // the reader behavior carries SequentialAccess and the compiled mapper reads GetStream/
        // GetTextReader instead of buffering. Such a plan is never cached, so a later buffered
        // preparation of the same shape cannot pick up the streaming behavior/mapper.
        //
        // Only the ToAsyncEnumerable terminal requests this (streamingRowsRequested), matching the
        // documented contract; the other enumerator terminals (ToEnumerable/Pipeline/CreateEnumerator)
        // stay on the buffered path and reject the shape. The projection is validated before the SQL
        // is rendered so an unsupported shape fails with an actionable message.
        var streamingRows = streamingRowsRequested && createEnumerator && RowMapperFactory.HasStreamingColumns(queryCommand.SelectList);
        if (streamingRows)
        {
            RowMapperFactory.ValidateStreamingColumns(queryCommand.SelectList);

            if (!_dialect().SupportsSequentialAccess)
                throw new NotSupportedException(
                    $"Streaming row projections are not supported by the {_dialect().GetType().Name} provider; they require sequential-access support (PostgreSQL, SQL Server or SQLite).");

            sequentialAccess = true;
            storeInCache = false;
        }

        var ext = queryCommand.CustomData as RawSqlOverride;

        if (queryCommand.Cache && storeInCache)
        {
            queryPlan = queryCommand.GetOrCreatePlanKey(ext?.ManualSql);
            if (QueryPlanStore.TryGet(_contextType, queryPlan, out planCache, out var storedPlan))
            {
                // Remember the cached instance so the next lookup of this command can match it by
                // reference rather than re-comparing the whole expression tree.
                queryCommand.CacheStoredPlanKey(storedPlan!, ext?.ManualSql);
            }
        }

        if (planCache is null)
        {
            if ((_logger?.IsEnabled(LogLevel.Debug) ?? false) && storeInCache && queryCommand.Cache)
                _logger.LogDebug("Query plan cache miss with hash: {hash}", queryPlan!.GetHashCode());

            // One diagnostic per prepared pair command: a query with two or more collection JoinInto
            // declarations renders a cartesian product of parent rows. The flag is command state, never
            // part of the plan key, and is cleared here so a command warns at most once even when its
            // plan is not cached.
            if (queryCommand.PendingJoinIntoCartesianWarning)
            {
                queryCommand.PendingJoinIntoCartesianWarning = false;
                _logger?.LogWarning(
                    "JoinInto.MultipleCollections: {Message}",
                    "The query declares two or more collection JoinInto navigations; their joins multiply the parent rows. " +
                    "Pass JoinOptions.SuppressCartesianWarning() to silence this warning.");
            }

            // Raw-source global-filter skips are collected during preparation and emitted here, on the
            // cache miss only: the list is cleared before logging so a cache hit never re-emits, and a
            // re-preparation replaces (never appends to) the previous list. The warning carries only the
            // entity name, filter key, reason and declared column names — never SQL text, table names,
            // parameters or captured values.
            if (queryCommand.PendingRawSourceFilterSkips is { Count: > 0 } rawSourceFilterSkips)
            {
                queryCommand.PendingRawSourceFilterSkips = null;

                for (var i = 0; i < rawSourceFilterSkips.Count; i++)
                {
                    var skip = rawSourceFilterSkips[i];
                    _queryFilterLogger?.LogWarning(
                        "RawSourceFilterSkipped: EntityType={EntityType}; SourceOrdinal={SourceOrdinal}; FilterKey={FilterKey}; Reason={Reason}; MissingColumns={MissingColumns}",
                        skip.EntityType,
                        skip.SourceOrdinal,
                        skip.FilterKey,
                        skip.Reason,
                        skip.MissingColumns);
                }
            }

            var (sql, @params) = ext is null
                ? MakeSelectInternal()
                : (ext.ManualSql, ext.MakeParams?.Invoke());

            // A streaming row projection needs a compiled mapper (it materializes the row, including
            // the streaming column); the single-column LOB terminals keep the map-less path below.
            Func<IDataRecord, TResult>? map = streamingRows
                ? GetMapCached(queryCommand, sql, streaming: true)
                : queryCommand.DocumentMode || sequentialAccess || (queryCommand.SingleRow && queryCommand.OneColumn)
                    ? null
                    : GetMapCached(queryCommand, sql, streaming: false);

            var noParams = !(@params?.Count > 0);
            var needsParamRefresh = false;
            if (!noParams)
            {
                var parameterList = @params!;
                for (var i = 0; i < parameterList.Count; i++)
                {
                    var parameter = parameterList[i];
                    if (!IsRuntimeParam(parameter.Name) && !parameter.Stable)
                    {
                        needsParamRefresh = true;
                        break;
                    }
                }
            }

            var dbCommand = _createCommand(sql!);

            // The resolved timeout is part of the plan key, so a cached command is only ever reused
            // with the timeout it was created with; applying it here is safe (it never leaks between
            // commands, unlike mutating a shared command on execution).
            if (queryCommand.ResolvedCommandTimeout is int commandTimeout)
                dbCommand.CommandTimeout = commandTimeout;

            if (!noParams)
            {
                var parameterList = @params!;
                for (var i = 0; i < parameterList.Count; i++)
                {
                    var p = parameterList[i];
                    dbCommand.Parameters.Add(_createParam(dbCommand, p.Name, p.Value));
                }
            }

            RaiseCommandInitialized(dbCommand);

            // Some drivers (ClickHouse) turn CommandBehavior.SingleRow into an extra LIMIT 1. The
            // dialect already renders a limit for a single-row command, so the hint must be dropped
            // there to avoid a duplicated clause.
            var singleRow = queryCommand.SingleRow && _dialect().SupportsCommandBehaviorSingleRow;

            var compiledQuery = new DbPreparedQueryCommand<TResult>(dbCommand, map, new PreparedCommandOptions(singleRow, ext is null ? sql! : null, noParams, needsParamRefresh)
            {
                SequentialAccess = sequentialAccess,
            });

            // Build the immutable guarded refresh recipe once, on the miss. Unsupported shapes leave it
            // null and keep the original ExtractParams refresh on every hit.
            if (needsParamRefresh)
                compiledQuery.SetParamRecipe(ParamRefreshRecipe.TryCreate(queryCommand, @params!));

            if (createEnumerator)
            {
                var enumerator = CreateResultSetEnumerator(compiledQuery!, ownsCommand: streamingRows);
                compiledQuery.Enumerator = enumerator;
            }

            if (storeInCache && queryCommand.Cache)
            {
                QueryPlanStore.Set(_contextType, queryPlan!.GetCacheVersion(), compiledQuery);
            }

            return compiledQuery;
        }
        else
        {
#if DEBUG
            if (_logger?.IsEnabled(LogLevel.Debug) ?? false) _logger.LogDebug("Query plan cache hit");
#endif

            // A diagnostic collected while preparing a command that then hit the plan cache belongs to a
            // preparation whose SQL was already built (and warned about) by the earlier miss. Discarding
            // it here keeps the list from surviving on the command, where a later uncached call on the
            // same command would replay it outside its owning preparation.
            queryCommand.PendingRawSourceFilterSkips = null;

            var compiledQuery = (DbPreparedQueryCommand<TResult>)planCache;

            if (queryCommand.CustomData is RawSqlOverride)
            {
                Debug.Assert(string.IsNullOrEmpty(compiledQuery.SqlStmt), "SqlStmt must be null");
            }
            else if (string.IsNullOrEmpty(compiledQuery.SqlStmt))
            {
                Debug.Fail("SqlStmt must be not null");
            }
            else
            {
                if (!compiledQuery.NoParams && compiledQuery.NeedsParamRefresh)
                    RefreshParameters(queryCommand, compiledQuery);
            }

            // A plan can be stored by a buffered terminal (createEnumerator: false) and later requested
            // by a streaming one, so the enumerator has to be created on demand. The plan cache is
            // [ThreadStatic], so this entry is only ever touched by the current thread.
            if (createEnumerator && compiledQuery.Enumerator is null)
                compiledQuery.Enumerator = CreateResultSetEnumerator(compiledQuery);

            return compiledQuery;
        }
        (string?, List<Parameter>) MakeSelectInternal()
        {
            var @params = new List<Parameter>();
            var aliasProvider = new DefaultAliasProvider();
            return (MakeSelect(queryCommand, false, @params, queryCommand, aliasProvider, sequentialAccess), @params);
        }
    }

    // Refreshes the cached command's parameter values for the current command. A supported plan uses the
    // immutable recipe (validated count/name/order and shape before any accessor runs); any mismatch
    // falls through to the original ExtractParams path. The cached statement's placeholder set is
    // authoritative: a same-count name/order difference is bound positionally exactly like the original
    // Release extractor (whose name check was only a Debug.Assert), so a cache hit never crashes, while a
    // count mismatch binds nothing (it would leave trailing cached values stale or reach past the
    // collection) and invalidates the recipe so the next preparation re-plans.
    private void RefreshParameters<TResult>(QueryCommand queryCommand, DbPreparedQueryCommand<TResult> compiledQuery)
    {
        var dbCommandParams = compiledQuery.DbCommandParams;

        if (compiledQuery.ParamRecipe is { } recipe && recipe.TryBind(queryCommand, dbCommandParams))
            return;

        Interlocked.Increment(ref _fallbackRefreshes);

        var pp = ExtractParams(queryCommand);

        if (pp.Count != dbCommandParams.Count)
        {
            compiledQuery.SetParamRecipe(null);
            return;
        }

        for (var i = 0; i < dbCommandParams.Count; i++)
        {
            // Normalize null to DBNull for providers that reject null parameter values.
            dbCommandParams[i].Value = pp[i].Value ?? DBNull.Value;
        }
    }

    public void ResetPreparation(QueryCommand queryCommand)
    {
        //_clearCache = true;
    }

    // A table source is immutable, so one instance can be shared by every query (and cached plan)
    // that selects from the entity. This removes a small allocation and a metadata lookup per join.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, FromExpression> _fromCache = new();

    internal static void ClearFromCache() => _fromCache.Clear();

    private FromExpression GetFrom(Type t)
    {
        if (_fromCache.TryGetValue(t, out var cached))
            return cached;

        if (DataContextCache.Metadata.TryGetValue(t, out var entity) && !string.IsNullOrEmpty(entity.TableName))
        {
            var from = new FromExpression(entity.TableName, entity.IsTableNameAuto, t.IsInterface);
            _fromCache.TryAdd(t, from);
            return from;
        }

        // Table names come from the entity metadata, registered the first time the entity is
        // materialized (From<T>()/EntityBuilder<T>). Reaching this point means the type was never
        // registered, so a table name cannot be produced.
        throw new BuildSqlCommandException(
            $"Table name is not registered for type {t}. Materialize the entity first (for example with {nameof(DataContextExtensions.From)}<{t.Name}>()) so its metadata is registered.");
    }

    public FromExpression? GetFrom(Type srcType, QueryCommand? queryCommand)
    {
        if (srcType != typeof(TableAlias))
        {
            if (queryCommand?.Joins?.Length > 0 && srcType.IsAssignableTo(typeof(IProjection)))
            {
                var propT1 = srcType.GetProperty("Item1") ?? throw new BuildSqlCommandException($"Projection {srcType} must have Item1 property");

                var f = GetFrom(propT1.PropertyType);

                return f;
            }
            else
                return GetFrom(srcType);
        }
        else
            return null;
    }

    private Func<IDataRecord, TResult> GetMapCached<TResult>(QueryCommand<TResult> queryCommand, string? sql, bool streaming)
    {
        return RowMapperFactory.GetOrBuild(queryCommand, sql, _contextType, _logger, _mapColumn, streaming);
    }

    /// <summary>
    /// Notifies the registered query interceptors that a plan's command has been created and its
    /// parameters bound. A cached plan is only initialised once, so the event does not fire again when
    /// the plan is reused.
    /// </summary>
    private void RaiseCommandInitialized(DbCommand command)
    {
        var interceptors = _interceptors.QueryInterceptors;
        if (interceptors.Length == 0)
            return;

        InterceptorHooks.RaiseCommandInitialized(interceptors, _context, command);
    }

    /// <summary>
    /// Creates the streaming enumerator and applies the context's logging configuration to it. The
    /// enumerator receives the logger directly rather than pulling it from the context through a
    /// property, which is what kept the enumerator tied to the concrete <c>DataContext</c>.
    /// </summary>
    private ResultSetEnumerator<TResult> CreateResultSetEnumerator<TResult>(DbPreparedQueryCommand<TResult> compiledQuery, bool ownsCommand = false)
    {
        var enumerator = new ResultSetEnumerator<TResult>(compiledQuery);
        if (ownsCommand)
            enumerator.OwnCommand();
        enumerator.InitEnvironment(_resultSetEnumeratorLogger, _logSensitiveData);
        return enumerator;
    }
}
