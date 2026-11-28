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
/// Provider hooks (dialect, column-mapping policy, parameter factory) and the logging
/// configuration arrive as constructor dependencies, so the planner never sees the
/// concrete context (DIP).
/// </summary>
internal sealed class QueryPlanner : IQueryPlanner
{
    private readonly IDataContext _context;
    private readonly IEntityMetadataProvider _metadataProvider;
    private readonly IEntityVariantResolver _scopeResolver;
    private readonly Func<ISqlDialect> _dialect;
    private readonly ILogger? _logger;
    private readonly Type _contextType;
    private readonly Func<SelectExpression, Expression, Expression> _mapColumn;
    private readonly Func<DbCommand, string, object?, DbParameter> _createParam;
    private readonly Func<string, object?, DbParameter> _createParamSimple;
    private readonly ILogger? _queryFilterLogger;
    private readonly Func<CteMutation, SqlBuildContext, string> _renderMutationBody;

    // Cached-path diagnostic counter for the plans that must fall back to the original extraction path
    // (no recipe or a rejected one). Process-wide but updated with Interlocked and read with Volatile,
    // so the increment path is lock-free and allocation-free.
    private static int _fallbackRefreshes;

    // D201-3 recipe diagnostics: how many pre-preparation candidates were published after a successful
    // cacheable Set, how many fresh commands actually reused one (staged apply passed validation), and
    // how many eligible probes were rejected (ineligible graph or staged-key/owner mismatch).
    private static int _recipePublishes;
    private static int _recipeReuses;
    private static int _recipeRejections;
    private static int _recipeApplyMismatches;

    /// <summary>Number of cached-hit parameter refreshes that used the original <c>ExtractParams</c> path.</summary>
    internal static int FallbackRefreshes => Volatile.Read(ref _fallbackRefreshes);

    /// <summary>Number of published pre-preparation recipe candidates. Diagnostic only.</summary>
    internal static int RecipePublishes => Volatile.Read(ref _recipePublishes);

    /// <summary>Number of fresh commands that reused a recipe candidate. Diagnostic only.</summary>
    internal static int RecipeReuses => Volatile.Read(ref _recipeReuses);

    /// <summary>Number of eligible probes rejected (ineligible graph or staged mismatch). Diagnostic only.</summary>
    internal static int RecipeRejections => Volatile.Read(ref _recipeRejections);

    /// <summary>Number of staged applies rejected by the normalized-key/owner validation. Diagnostic only.</summary>
    internal static int RecipeApplyMismatches => Volatile.Read(ref _recipeApplyMismatches);

    /// <summary>Resets the planner's cached-path diagnostic counters to zero. Test seam.</summary>
    internal static void ResetCounters()
    {
        Volatile.Write(ref _fallbackRefreshes, 0);
        Volatile.Write(ref _recipePublishes, 0);
        Volatile.Write(ref _recipeReuses, 0);
        Volatile.Write(ref _recipeRejections, 0);
        Volatile.Write(ref _recipeApplyMismatches, 0);
        QueryPlanStore.ResetNativeCounters();
    }

    internal QueryPlanner(
        IDataContext context,
        Func<ISqlDialect> dialect,
        Type contextType,
        ProviderHooks hooks,
        LoggingOptions logging,
        IEntityMetadataProvider metadataProvider,
        IEntityVariantResolver scopeResolver)
    {
        _context = context;
        _metadataProvider = metadataProvider;
        _scopeResolver = scopeResolver;
        _dialect = dialect;
        _logger = logging.Logger;
        _contextType = contextType;
        _mapColumn = hooks.MapColumn;
        _createParam = hooks.CreateParam;
        _createParamSimple = hooks.CreateParamSimple;
        _queryFilterLogger = logging.QueryFilterLogger;
        _renderMutationBody = RenderMutationBody;
    }

    // Test and internal callers that construct a planner directly retain the default mapping behavior.
    internal QueryPlanner(
        IDataContext context,
        Func<ISqlDialect> dialect,
        Type contextType,
        ProviderHooks hooks,
        LoggingOptions logging)
        : this(context, dialect, contextType, hooks, logging,
            new DefaultEntityMetadataProvider(), new DefaultEntityVariantResolver())
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private List<Parameter> ExtractParams(QueryCommand queryCommand)
    {
        var @params = new List<Parameter>();
        MakeSelect(queryCommand, true, @params, queryCommand, null, false, false);
        return @params;
    }

    // SqlFunctions.Parameter placeholders carry no value - the runtime values are applied by
    // DbPreparedQueryCommand.GetDbCommand. Only computed parameters (captured variables,
    // closures) need to be re-extracted on every cached execution.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsRuntimeParam(string name) => NormParam.IsName(name);

    // A stable expression can still produce mutable data, for example an inline IN element
    // converted to a byte array. Only an immutable provider value can be a shared seed.
    private static bool CanRetainPlanSeed(Parameter parameter)
        => parameter.Stable && PreparedCommandFactory.IsShareableSeedValue(parameter.Value);

    /// <summary>
    /// Whether a plan that needs parameter refresh may join the raw recipe store: only when the raw
    /// shape carries a captured-closure identity and every refreshed parameter is a captured member.
    /// A hit on the same closure instance reads the live members and binds their current values, so the
    /// statement can be reused. A parameter with no captured source (an ambient/static read) has no
    /// identity key, so such a plan keeps the ordinary route exactly as before.
    /// </summary>
    private static bool CanPublishCapturedRecipe(QueryCommand.RawShape shape, List<Parameter>? parameters)
    {
        if (shape.Closures is not { Length: > 0 } || parameters is null)
            return false;

        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];
            if (IsRuntimeParam(parameter.Name) || CanRetainPlanSeed(parameter)) continue;
            if (parameter.CapturedKey is null) return false;
        }

        return true;
    }

    // Renders a standalone SELECT (the source of an INSERT ... SELECT) to SQL plus its parameters, and
    // hoists any CTE the source declares out of the SELECT. A data-modifying CTE must sit at the top
    // level of the statement, so the caller places WithSql before "insert into"; a plain read CTE is
    // hoisted the same way. The WITH and the SELECT share one parameter provider so the numbering matches
    // the emitted SQL. MaxRecursionStmt is the dialect-specific statement tail (null when the source declares
    // no recursion limit): it is NOT part of WithSql, so the caller appends it once at the statement tail.
    // Runtime SqlFunctions.Parameter placeholders cannot be bound through a mutation
    // command, so they are rejected with an actionable message.
    internal (string? WithSql, string Sql, List<Parameter> Parameters, string? MaxRecursionStmt) RenderSource(QueryCommand source)
        => RenderSource(source, null, null, null);

    // Renders the source with a SELECT ... INTO clause injected into the top-level select list (SQL Server
    // CTAS). Only the outer select carries it; CTE, UNION and derived-table bodies render without it.
    internal (string? WithSql, string Sql, List<Parameter> Parameters, string? MaxRecursionStmt) RenderSource(QueryCommand source, string? selectInto)
        => RenderSource(source, null, null, selectInto);

    // Overload used by a full MERGE so the source body, the match/branch conditions and the VALUES rows
    // share one parameter provider and accumulator (otherwise their autogenerated names would collide).
    internal (string? WithSql, string Sql, List<Parameter> Parameters, string? MaxRecursionStmt) RenderSource(QueryCommand source, IParameterProvider? parameterProvider, List<Parameter>? parameters, string? selectInto = null, string parameterNamePrefix = "", string? keyProjection = null, string? runtimeParameterError = null, bool forceProjectionAliases = false)
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
            ParameterNamePrefix = string.IsNullOrEmpty(parameterNamePrefix) ? source.MappingVariant.ParameterPrefix : parameterNamePrefix,
            MappingVariant = source.MappingVariant,
            MetadataProvider = _metadataProvider,
            RenderMutationBody = _renderMutationBody,
            // A statement source renders a select list whose output is addressed by column identifier
            // (a CTAS target, an INSERT ... SELECT target and a MERGE source): an unaliased computed
            // scalar must declare its generated output name, exactly like a derived subquery body. The
            // flag is part of this one context value, so no shared command state is mutated.
            RequireDerivedScalarOutputName = true,
            ForceProjectionAliases = forceProjectionAliases,
        };

        string? withSql = null;
        string? maxRecursionStmt = null;
        var body = source;
        if (source.Ctes is { Count: > 0 } ctes)
        {
            withSql = SqlSourceRenderer.MakeWithClause(in ctx, ctes, out maxRecursionStmt);

            body = source.CloneForCache();
            body.Ctes = null;
            ctx = ctx with { QueryProvider = body };
        }

        var builder = new SqlBuilder(in ctx);
        var sql = keyProjection is null ? builder.MakeSelect(body, selectInto) : builder.MakeKeySelection(body, keyProjection);

        for (var i = 0; i < @params.Count; i++)
        {
            if (NormParam.IsName(@params[i].Name))
                throw new NotSupportedException(
                    runtimeParameterError ?? "A query rendered as a source (INSERT ... SELECT, MERGE or a batch) cannot use SqlFunctions.Parameter runtime placeholders; capture the value in a local variable instead.");
        }

        return (withSql, sql!, @params, maxRecursionStmt);
    }

    // Renders the WHERE predicate of a command as a standalone, unqualified condition, used by
    // DELETE FROM <table> WHERE <condition>. The command is prepared first so the condition matches
    // the SELECT pipeline (captured values become parameters, correlated subqueries are rewritten);
    // only the condition is rendered - no FROM or select list.
    internal (string Sql, List<Parameter> Parameters) RenderPredicate(QueryCommand command)
        => RenderPredicate(command, null, null);

    // Overload used by UPDATE so the WHERE parameters continue the sequence started by the SET list
    // (a fresh provider/list would restart parameter numbering and collide with the SET parameters).
    // A mutation fragment carries the statement's lowering, so a text value the visitor emits becomes the
    // same literal the mutation builder would emit for it (D4g).
    internal (string Sql, List<Parameter> Parameters) RenderPredicate(QueryCommand command, IParameterProvider? parameterProvider, List<Parameter>? parameters, string parameterNamePrefix = "", MutationParameterLowering? lowering = null)
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
            ParameterNamePrefix = string.IsNullOrEmpty(parameterNamePrefix) ? command.MappingVariant.ParameterPrefix : parameterNamePrefix,
            MappingVariant = command.MappingVariant,
            MetadataProvider = _metadataProvider,
            MutationLowering = lowering,
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
                var (sql, _) = SqlMutationBuilder.MakeUpdate(ctx.Dialect, ctx.QuoteIdentifiers, ctx.NamingConvention, update, setSql, ctx.Params, whereSql, ctx.ParameterProvider, ctx.KeywordCase, metadataProvider: ctx.MetadataProvider);
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

                var (sql, _) = SqlMutationBuilder.MakeDelete(ctx.Dialect, ctx.QuoteIdentifiers, ctx.NamingConvention, delete, whereSql, ctx.Params, ctx.KeywordCase, ctx.ParameterProvider, metadataProvider: ctx.MetadataProvider);
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

    // Renders an explicit MERGE assignment set against the roles of the statement shape. The roles come from
    // the assignment set's own parameter bindings (never from a member name), and the scalar translation is
    // shared with the condition pipeline so functions, closures, quoting and parameter numbering stay
    // identical to the rest of the statement. The fragment's parameters join the caller's accumulator.
    internal MergeAssignmentFragment RenderMergeAssignments(
        MergeAssignmentSet assignments,
        MergeAssignmentRenderInfo info,
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
            AliasProvider = new MergeAliasProvider(info.TargetReference, "source", info.SupportsExcluded ? "excluded" : null),
            Logger = _logger!,
            QuoteIdentifiers = quoteIdentifiers,
            NamingConvention = namingConvention,
            KeywordCase = keywordCase,
        };

        var roleParameters = new ParameterExpression[assignments.Bindings.Count];
        for (var i = 0; i < roleParameters.Length; i++)
        {
            roleParameters[i] = assignments.Bindings[i].Parameter;
            ctx.ColumnsProvider.Add(entityType, false);
        }

        ctx.ColumnsProvider.PushScope(Array.AsReadOnly(roleParameters));

        try
        {
            var options = new VisitorOptions(
                entityType,
                ctx.Dialect,
                ctx.ColumnsProvider,
                0,
                ctx.AliasProvider,
                parameterProvider,
                registry,
                DontNeedAlias: false,
                ParamMode: false,
                parameters,
                _logger)
            {
                QuoteIdentifiers = quoteIdentifiers,
                NamingConvention = namingConvention,
                KeywordCase = keywordCase,
            };

            using var visitor = new MergeAssignmentValueVisitor(options, assignments, info);
            return MergeAssignmentRenderer.Render(assignments, info, visitor.VisitToString);
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
        string parameterNamePrefix = "",
        MutationParameterLowering? lowering = null)
    {
        var source = command.Source;
        if (!source.IsPrepared)
            source.PrepareCommand(false, CancellationToken.None);

        var quoteIdentifiers = source.ResolvedQuoteIdentifiers;
        var namingConvention = source.ResolvedNamingConvention;
        var mappingVariant = source.MappingVariant;

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
            ParameterNamePrefix = string.IsNullOrEmpty(parameterNamePrefix) ? mappingVariant.ParameterPrefix : parameterNamePrefix,
            MappingVariant = mappingVariant,
            MetadataProvider = _metadataProvider,
            MutationLowering = lowering,
        };
        ctx.ColumnsProvider.Add(command.EntityType!, false);

        var builder = new StringBuilder();
        for (var i = 0; i < command.Assignments.Count; i++)
        {
            if (i > 0)
                builder.Append(", ");

            var assignment = command.Assignments[i];
            builder.Append(SqlMutationBuilder.RenderColumnReference(ctx.Dialect, quoteIdentifiers, assignment.Property, namingConvention, mappingVariant, _metadataProvider));
            builder.Append(" = ");

            switch (assignment.Kind)
            {
                case UpdateValueKind.Constant:
                    var name = parameterProvider.GetParamName();
                    var finalValue = DurationStorage.ToParameterValue(assignment.Constant, assignment.Property, ctx.Dialect);
                    if (lowering is not null && lowering.TryRender(name, finalValue, out var literal))
                    {
                        // The lowered literal occupies the assignment's position; the name was still
                        // allocated, so the remaining parameters keep their numbering.
                        builder.Append(literal);
                        break;
                    }

                    parameters.Add(new Parameter(name, finalValue));
                    builder.Append(ctx.Dialect.MakeParam(name));
                    break;
                case UpdateValueKind.Column:
                    builder.Append(SqlMutationBuilder.RenderColumnReference(ctx.Dialect, quoteIdentifiers, assignment.Column!, namingConvention, mappingVariant, _metadataProvider));
                    break;
                default:
                    using (var visitor = ctx.CreateColumnVisitor(command.EntityType!, 0, dontNeedAlias: true))
                    {
                        visitor.Visit(assignment.Expression!);

                        // The UPDATE statement's template capture is not part of D4b-1 (only the SELECT
                        // planner path attaches one), so the assignment fragment stays untracked here.
                        visitor.WriteTo(builder, null);
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
            var dynamicStart = parameters.Count;
            var dynamicNames = command.DynamicColumns.AddValueParameters(0, parameters, parameterProvider, ctx.Dialect);
            for (var d = 0; d < dynamicKeys.Length; d++)
            {
                if (builder.Length > 0)
                    builder.Append(", ");

                builder.Append(dynamicKeys[d]).Append(" = ");

                // The stored value is already converted by AddValueParameters, so the lowering inspects
                // the bound value rather than converting it a second time (the same rule as the VALUES rows
                // of an insert or a key upsert).
                var dynamicValue = parameters[dynamicStart + d].Value;
                if (lowering is not null && lowering.TryRender(dynamicNames[d], dynamicValue, out var dynamicLiteral))
                    builder.Append(dynamicLiteral);
                else
                    builder.Append(ctx.Dialect.MakeParam(dynamicNames[d]));
            }
        }

        return (builder.ToString(), parameters);
    }

    // Renders a multi-table DELETE: the target is the first table of the prepared joined command, whose
    // joins and condition are rendered through the same source/condition pipeline as a SELECT, so aliases
    // and parameters match the equivalent read query.
    internal (string Sql, List<Parameter> Parameters) RenderDeleteJoin(DeleteJoinCommand command)
        => RenderDeleteJoinCore(command, new DefaultParameterProvider(), new List<Parameter>(), suppressCtes: false);

    internal (string Sql, List<Parameter> Parameters) RenderDeleteJoin(DeleteJoinCommand command, IParameterProvider provider, OutputIntoClause captureInto, string parameterNamePrefix)
        => RenderDeleteJoinCore(command, provider, new List<Parameter>(), suppressCtes: false, captureInto, parameterNamePrefix);

    // Core multi-table DELETE renderer shared by the standalone statement and the body of a data-modifying
    // CTE. The provider and parameter accumulator come from the caller so a CTE body continues the enclosing
    // statement's numbering. When the command is hoisted as a CTE body (suppressCtes) its own WITH clause
    // must not be re-emitted: the hoisted CTEs already sit at the top level of the statement.
    private (string Sql, List<Parameter> Parameters) RenderDeleteJoinCore(
        DeleteJoinCommand command,
        IParameterProvider parameterProvider,
        List<Parameter> parameters,
        bool suppressCtes,
        OutputIntoClause? captureInto = null,
        string parameterNamePrefix = "")
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
            ParameterNamePrefix = parameterNamePrefix,
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

        var (sql, renderedParams) = new SqlBuilder(in ctx).MakeDeleteJoin(command, captureInto);
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

    internal (string Sql, List<Parameter> Parameters) RenderUpdateJoin(UpdateJoinCommand command, IParameterProvider provider, OutputIntoClause captureInto, string parameterNamePrefix)
        => RenderUpdateJoinCore(command, provider, new List<Parameter>(), suppressCtes: false, captureInto, parameterNamePrefix);

    // Core multi-table UPDATE renderer shared by the standalone statement and the body of a data-modifying
    // CTE. The provider and parameter accumulator come from the caller so a CTE body continues the enclosing
    // statement's numbering. When the command is hoisted as a CTE body (suppressCtes) its own WITH clause
    // must not be re-emitted: the hoisted CTEs already sit at the top level of the statement.
    private (string Sql, List<Parameter> Parameters) RenderUpdateJoinCore(
        UpdateJoinCommand command,
        IParameterProvider parameterProvider,
        List<Parameter> parameters,
        bool suppressCtes,
        OutputIntoClause? captureInto = null,
        string parameterNamePrefix = "")
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
            ParameterNamePrefix = parameterNamePrefix,
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

        var (sql, renderedParams) = new SqlBuilder(in ctx).MakeUpdateJoin(command, captureInto);
        if (withSql is not null)
            sql = withSql + sql;
        if (maxRecursionStmt is not null)
            sql += " " + maxRecursionStmt;
        return (sql, renderedParams);
    }

    private string? MakeSelect(
        QueryCommand queryCommand,
        bool paramMode,
        List<Parameter> @params,
        IQueryRegistry queryProvider,
        IAliasProvider? aliasProvider,
        bool sequentialAccess,
        bool suppressLobLocator,
        SqlParameterTemplateCapture? capture = null)
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
            SuppressLobLocator = suppressLobLocator,
            // The native FOR JSON streaming clone aliases every projected column to its JSON property
            // name, so a mapped column that differs from the property only by case (id vs Id) still gets
            // the exact alias FOR JSON PATH emits as the property name. Set only on that clone: a plain
            // JSON-shape preparation (ForJsonClause null) and the scalar ForJson terminal are untouched.
            ExactProjectionAliases = queryCommand.JsonShapeMode && queryCommand.ForJsonClause is not null,
            RenderMutationBody = _renderMutationBody,
            // The statement-level occurrence capture, created by the planning render only. The parameter
            // extraction pass (paramMode) never creates one, so it records nothing.
            Capture = capture,
            MappingVariant = queryCommand.MappingVariant,
            MetadataProvider = _metadataProvider,
            ParameterNamePrefix = queryCommand.MappingVariant.ParameterPrefix,
        };
        var sqlBuilder = new SqlBuilder(in ctx);
        return sqlBuilder.MakeSelect(queryCommand);
    }

    public IPreparedQueryCommand<TResult> GetPreparedQueryCommand<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, CancellationToken cancellationToken)
        => GetPreparedQueryCommand(queryCommand, createEnumerator, storeInCache, PreparedCommandCache.Connection, false, false, cancellationToken);

    internal IPreparedQueryCommand<TResult> GetPreparedQueryCommand<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, PreparedCommandCache cache, CancellationToken cancellationToken)
        => GetPreparedQueryCommand(queryCommand, createEnumerator, storeInCache, cache, false, false, cancellationToken);

    internal IPreparedQueryCommand<TResult> GetPreparedQueryCommand<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, PreparedCommandCache cache, bool sequentialAccess, CancellationToken cancellationToken)
        => GetPreparedQueryCommand(queryCommand, createEnumerator, storeInCache, cache, sequentialAccess, false, cancellationToken);

    internal IPreparedQueryCommand<TResult> GetPreparedQueryCommand<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, PreparedCommandCache cache, bool sequentialAccess, bool streamingRowsRequested, CancellationToken cancellationToken)
        => GetPreparedQueryCommand(queryCommand, createEnumerator, storeInCache, cache, sequentialAccess, streamingRowsRequested, suppressLobLocator: false, cancellationToken);

    internal IPreparedQueryCommand<TResult> GetPreparedQueryCommand<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, PreparedCommandCache cache, bool sequentialAccess, bool streamingRowsRequested, bool suppressLobLocator, CancellationToken cancellationToken)
        => GetPreparedQueryCommandCore<TResult>(
            queryCommand,
            boxed: false,
            boxedResultType: null,
            createEnumerator,
            storeInCache,
            cache,
            sequentialAccess,
            streamingRowsRequested,
            suppressLobLocator,
            cancellationToken);

    /// <summary>
    /// Boxed counterpart of <see cref="GetPreparedQueryCommand{TResult}(QueryCommand{TResult}, bool, bool, PreparedCommandCache, bool, bool, bool, CancellationToken)"/>
    /// for a non-generic <see cref="QueryCommand"/>: the statement is planned exactly as the typed path
    /// would plan it, but the row mapper materializes <see cref="QueryCommand.ResultType"/> and boxes each
    /// row, so the prepared command can be held as <see cref="IPreparedQueryCommand{TResult}"/> of
    /// <see cref="object"/> without constructing a generic command type at runtime.
    /// </summary>
    internal IPreparedQueryCommand<object?> GetObjectPreparedQueryCommand(QueryCommand queryCommand, bool createEnumerator, bool storeInCache, PreparedCommandCache cache, CancellationToken cancellationToken)
    {
        var resultType = queryCommand.ResultType
            ?? throw new InvalidOperationException("A non-generic query command must carry a result type before it can be prepared for boxed execution.");

        return GetPreparedQueryCommandCore<object?>(
            queryCommand,
            boxed: true,
            boxedResultType: resultType,
            createEnumerator,
            storeInCache,
            cache,
            sequentialAccess: false,
            streamingRowsRequested: false,
            suppressLobLocator: false,
            cancellationToken);
    }

    /// <summary>
    /// Resolves the row mapper for one preparation. Kept as a direct call (not a delegate parameter) so a
    /// warm execution path allocates nothing for the choice between the typed and boxed mapper.
    /// </summary>
    private Func<IDataRecord, TRow> BuildMap<TRow>(QueryCommand queryCommand, string? sql, bool streaming, bool boxed, Type? boxedResultType)
        => boxed
            ? (Func<IDataRecord, TRow>)(object)RowMapperFactory.GetOrBuildBoxed(queryCommand, boxedResultType!, sql, _contextType, _logger, _mapColumn, streaming)
            : GetMapCached<TRow>(queryCommand, sql, streaming);

    private IPreparedQueryCommand<TRow> GetPreparedQueryCommandCore<TRow>(
        QueryCommand queryCommand,
        bool boxed,
        Type? boxedResultType,
        bool createEnumerator,
        bool storeInCache,
        PreparedCommandCache cache,
        bool sequentialAccess,
        bool streamingRowsRequested,
        bool suppressLobLocator,
        CancellationToken cancellationToken)
    {
        // The command-reuse policy travels as this call's parameter only. None means a command per
        // execution, so the plan cache is neither read nor filled; clearing the local storeInCache keeps
        // QueryCommand.Cache (shared, sticky) untouched.
        if (cache == PreparedCommandCache.None)
            storeInCache = false;

        // A data-modifying CTE makes the whole statement side-effecting, so its plan must never be
        // shared: the mutation's shape (row count, target columns) is not captured by the CTE query.
        // Clear the call-local storeInCache rather than queryCommand.Cache, which is sticky and would
        // disable plan caching for a shared command (the context-wide Any/Count command) on every later
        // query. Both the lookup and store gates below test storeInCache, so they skip.
        if (queryCommand.HasDataModifyingCte)
            storeInCache = false;

        QueryPlan? queryPlan = null;
        IDbCommandHolder? planCache = null;

        var ext = queryCommand.CustomData as RawSqlOverride;

        // D201-3 pre-preparation reuse. Verify the fresh descriptor before applying any stored state:
        // a normalized key built after applying the owner's fields cannot prove they belonged to the caller.
        // Anything whose raw graph cannot be proved keeps its ordinary route, and the shared terminal command is excluded
        // by ACTUAL INSTANCE identity of the context's QueryCache.AnyCommand — never by IsPrepared,
        // TResult or command shape.
        QueryCommand.RawShape? recipeShape = null;
        if (!queryCommand.IsPrepared
            && !queryCommand.HasEverPrepared
            && queryCommand.Cache
            && storeInCache
            && ext is null
            && !streamingRowsRequested
            && !queryCommand.HasDataModifyingCte
            && !queryCommand.HasTemporarySource()
            && !IsSharedAnyCommand(queryCommand)
            // The value-free raw fingerprint describes the command graph, not the mapping variant: a
            // recipe prepared for one shard would be adopted by a command targeting another shard and
            // would render the wrong table. Scope-keyed plans keep the ordinary (normalized key) route.
            && queryCommand.MappingVariant == MappingVariant.Default)
        {
            // A global/builder query filter is injected only during preparation and is absent from the
            // value-free raw shape, so a fresh command whose raw graph resolves ANY applicable filter for
            // the live context could otherwise adopt a predicate prepared under a different context (a
            // permissive-to-restrictive transfer leaks rows; the reverse drops the filter). Such a graph is
            // ineligible: skip the whole raw path and take the ordinary preparation route. Metadata-only —
            // no user filter function is invoked for the probe.
            if (!QueryCommand.HasApplicableFiltersInCommandGraph(queryCommand, queryCommand.DataContext))
                recipeShape = QueryCommand.CaptureRawShape(queryCommand);

            if (recipeShape is null)
            {
                Interlocked.Increment(ref _recipeRejections);
            }
            else if (QueryPlanStore.TryGetRaw(_contextType, recipeShape, queryCommand, out var rawCandidate))
            {
                if (rawCandidate!.Plan.RequiresWeakOwner)
                {
                    // An opaque descriptor can own a previous invocation. Verify against a private
                    // copy and use only the holder, as a native hit does. Any path that needs live
                    // prepared state to bind parameters or mapping goes through ordinary preparation.
                    if (rawCandidate.Holder is DbPreparedQueryCommand<TRow>
                        { NeedsParamRefresh: false, RequiresInvocationMapper: false } opaquePlan)
                    {
                        var staged = queryCommand.Clone();
                        rawCandidate.ApplyTo(staged);
                        if (MatchesRawPlan(staged, rawCandidate.Plan))
                        {
                            if (createEnumerator)
                                opaquePlan.EnableStreaming();
                            return opaquePlan;
                        }
                    }
                }
                else
                {
                    // Discard the adoption if the resulting key does not describe the published owner.
                    var rawGraph = queryCommand.CaptureRawGraph();
                    rawCandidate.ApplyTo(queryCommand);
                    if (!MatchesRawPlan(queryCommand, rawCandidate.Plan))
                        queryCommand.DiscardStagedPreparation(rawGraph);
                }
            }
        }

        if (!queryCommand.IsPrepared) queryCommand.PrepareCommand(!storeInCache, cancellationToken);
        else queryCommand.RefreshInValuesShape();

        // A tuple value list in a clause whose shape is not part of the plan key (HAVING / JOIN ON /
        // SELECT / a nested subquery) renders a SQL/parameter shape that follows the captured collection.
        // The same holds for a scalar value list (HasUnkeyedScalarInValues), including the WHERE/PREWHERE
        // of a descendant query rendered through this command. Suppress the call-local cache for this call
        // rather than mutating the sticky QueryCommand.Cache flag, which would disable plan caching for a
        // shared command (the context-wide Any/Count command) on every later query. Both the lookup and
        // store gates below test storeInCache, so they skip.
        if (queryCommand.HasUnkeyedTupleInValues || queryCommand.HasUnkeyedScalarInValues)
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

        // A raw override's key verifies the ordered names. Only this invocation reads its bag.
        object?[]? boundValues = planCache is not null ? ext?.ReadValues() : null;
        if (ext is null && planCache is DbPreparedQueryCommand<TRow> { NeedsParamRefresh: true } hit)
        {
            boundValues = ReadInvocationValues(queryCommand, hit);
            if (boundValues is null)
            {
                // The rendered parameter layout disagrees with this candidate. Build an execution-
                // local command rather than using stale values or replacing the published recipe.
                planCache = null;
                storeInCache = false;
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

            // A planner-rendered statement captures its own parameter occurrences; caller-owned text
            // (RawSqlOverride) has no planner-owned emission and therefore never gets a template.
            var (sql, @params, capture) = ext is null
                ? MakeSelectInternal()
                : (ext.ManualSql, ext.MakeParams(), null);

            // A streaming row projection needs a compiled mapper (it materializes the row, including
            // the streaming column); the single-column LOB terminals keep the map-less path below.
            Func<IDataRecord, TRow>? map = streamingRows
                ? BuildMap<TRow>(queryCommand, sql, true, boxed, boxedResultType)
                : queryCommand.DocumentMode || sequentialAccess || (queryCommand.SingleRow && queryCommand.OneColumn)
                    ? null
                    : BuildMap<TRow>(queryCommand, sql, false, boxed, boxedResultType);

            var publish = storeInCache && queryCommand.Cache;
            var convertedProjection = publish && queryCommand.SelectList is { } columns
                && Array.Exists(columns, static column => column.Converter is not null);
            var useInvocationMapper = publish && (convertedProjection || _context is DataContext { UsesContextBoundColumnMapping: true });
            var invocationMapper = useInvocationMapper ? map : null;
            var planMapper = useInvocationMapper ? null : map;
            var generatedImplicitPlan = publish && ext is null;
            var noParams = !(@params?.Count > 0);
            var needsParamRefresh = false;
            if (!noParams)
            {
                var parameterList = @params!;
                for (var i = 0; i < parameterList.Count; i++)
                {
                    var parameter = parameterList[i];
                    if (!IsRuntimeParam(parameter.Name)
                        && !(generatedImplicitPlan ? CanRetainPlanSeed(parameter) : parameter.Stable))
                    {
                        needsParamRefresh = true;
                        break;
                    }
                }
            }

            // A published plan keeps caller-owned values out of both its factory and diagnostics.
            // Uncached preparation retains the values belonging to that particular prepared command.
            var requiresInvocationValues = publish && (ext is null ? needsParamRefresh : !noParams);
            if (requiresInvocationValues)
            {
                boundValues = new object?[@params!.Count];
                CopyInvocationValues(@params, boundValues);
            }

            var commandTimeout = queryCommand.ResolvedCommandTimeout;
            var factory = new PreparedCommandFactory(sql!, commandTimeout, @params, requiresInvocationValues);
            var planParameters = factory.CreatePlanParameters(_createParamSimple);

            // Some drivers (ClickHouse) turn CommandBehavior.SingleRow into an extra LIMIT 1. The
            // dialect already renders a limit for a single-row command, so the hint must be dropped
            // there to avoid a duplicated clause.
            var singleRow = queryCommand.SingleRow && _dialect().SupportsCommandBehaviorSingleRow;

            var compiledQuery = new DbPreparedQueryCommand<TRow>(factory, planParameters, planMapper, new PreparedCommandOptions(singleRow, ext is null ? sql! : null, noParams, needsParamRefresh)
            {
                SequentialAccess = sequentialAccess,
                Cache = cache,
            }, trackConnectionLifetime: publish)
            {
                RequiresInvocationMapper = invocationMapper is not null,
                MappingVariant = queryCommand.MappingVariant,
                RouteEntityType = queryCommand.RouteEntityType,
                RouteSources = CommandRouteSources.Collect(queryCommand),
            };

            // Attach the policy the planning dialect resolved and, when the statement is planner-rendered,
            // the immutable occurrence template. The policy is attached even without a template because it
            // also selects the PostgreSQL final-value guard on every checkout (D5); caller-owned text
            // (`RawSqlOverride`) keeps no template because it carries no planner-owned emission.
            compiledQuery.SetParameterTemplate(
                ext is null && capture is not null && sql is not null && @params is not null
                    ? capture.Finalize(sql, @params)
                    : null,
                SqlStringLiteralPolicies.For(_dialect()));

            // Build the immutable guarded refresh recipe once, on the miss. Unsupported shapes leave it
            // null and keep the original ExtractParams refresh on every hit.
            if (ext is null && needsParamRefresh)
                compiledQuery.SetParamRecipe(ParamRefreshRecipe.TryCreate(queryCommand, @params!));

            if (createEnumerator)
                compiledQuery.EnableStreaming();

            if (publish)
            {
                var (storedKey, holder) = QueryPlanStore.Set(_contextType, queryPlan!.GetSqlCacheVersion(), compiledQuery);
                queryCommand.CacheStoredPlanKey(storedKey, ext?.ManualSql);
                if (!ReferenceEquals(holder, compiledQuery))
                {
                    // This invocation already rendered its own values. Reuse them with the winning
                    // holder only if the two render layouts agree, without evaluating the inputs again.
                    if (holder is not DbPreparedQueryCommand<TRow> winner
                        || winner.CommandText != compiledQuery.CommandText
                        || winner.Behavior != compiledQuery.Behavior
                        || winner.NeedsParamRefresh != compiledQuery.NeedsParamRefresh
                        || winner.RequiresInvocationMapper != compiledQuery.RequiresInvocationMapper
                        || !winner.ParamSeeds.SequenceEqual(compiledQuery.ParamSeeds))
                    {
                        return BoundQueryInvocation<TRow>.Create(compiledQuery, boundValues, invocationMapper);
                    }

                    compiledQuery = winner;
                    if (createEnumerator)
                        compiledQuery.EnableStreaming();
                }

                // Publication happens ONLY after ordinary preparation succeeded, the normalized key
                // was constructed and the cacheable holder was stored through Set. Staging must not
                // supply the first projection's converter; prepare those projections on the live command.
                // A plan with refreshed parameters needs the captured closure identities for a raw hit
                // to read the current values. Captured-collection shapes cannot use that shortcut.
                if (recipeShape is not null
                    && !convertedProjection
                    && (!needsParamRefresh || CanPublishCapturedRecipe(recipeShape, @params))
                    && queryCommand.InValuesShapeHash == 0
                    && queryCommand.PreWhereShapeHash == 0
                    && !queryCommand.HasTopLevelInValues
                    && !queryCommand.HasPreWhereInValues
                    && !queryCommand.HasUnkeyedTupleInValues
                    && !queryCommand.HasUnkeyedScalarInValues
                    && QueryPlanStore.SetRaw(_contextType, storedKey, compiledQuery, recipeShape, queryCommand))
                {
                    Interlocked.Increment(ref _recipePublishes);
                }
            }

            return BoundQueryInvocation<TRow>.Create(compiledQuery, boundValues, invocationMapper);
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

            var compiledQuery = (DbPreparedQueryCommand<TRow>)planCache;

            if (queryCommand.CustomData is RawSqlOverride)
            {
                Debug.Assert(string.IsNullOrEmpty(compiledQuery.SqlStmt), "SqlStmt must be null");
            }
            else if (string.IsNullOrEmpty(compiledQuery.SqlStmt))
            {
                Debug.Fail("SqlStmt must be not null");
            }

            // A plan can be stored by a buffered terminal (createEnumerator: false) and later requested
            // by a streaming one, so enable streaming on demand. The executor creates and configures
            // a fresh enumerator for each consumption.
            if (createEnumerator && !compiledQuery.StreamingEnabled)
                compiledQuery.EnableStreaming();

            var invocationMapper = compiledQuery.RequiresInvocationMapper
                ? BuildMap<TRow>(queryCommand, compiledQuery.CommandText, streamingRows, boxed, boxedResultType)
                : null;
            return BoundQueryInvocation<TRow>.Create(compiledQuery, boundValues, invocationMapper);
        }
        (string?, List<Parameter>, SqlParameterTemplateCapture?) MakeSelectInternal()
        {
            var @params = new List<Parameter>();
            var aliasProvider = new DefaultAliasProvider();
            var capture = new SqlParameterTemplateCapture();
            var sql = MakeSelect(queryCommand, false, @params, queryCommand, aliasProvider, sequentialAccess, suppressLobLocator, capture);
            return (sql, @params, capture);
        }
    }

    private static bool MatchesRawPlan(QueryCommand command, QueryPlan recipePlan)
    {
        var stagedKey = command.GetOrCreatePlanKey(null);
        var matchesStored = recipePlan.Equals(stagedKey);
        if (matchesStored || stagedKey.Equals(recipePlan))
        {
            // The store uses this same direction of comparison. Remember the verified key so
            // the normalized lookup does not compare the staged expression graph a second time.
            if (matchesStored && !recipePlan.RequiresWeakOwner && recipePlan.GetHashCode() == stagedKey.GetHashCode())
                command.CacheStoredPlanKey(recipePlan, null);
            Interlocked.Increment(ref _recipeReuses);
            return true;
        }

        Interlocked.Increment(ref _recipeRejections);
        Interlocked.Increment(ref _recipeApplyMismatches);
        return false;
    }

    // The statement's placeholder order is authoritative. As before, a same-count fallback is positional.
    // A count mismatch rejects this candidate for the current invocation without changing the plan.
    private object?[]? ReadInvocationValues<TResult>(QueryCommand queryCommand, DbPreparedQueryCommand<TResult> compiledQuery)
    {
        var dbCommandParams = compiledQuery.DbCommandParams;
        var values = new object?[dbCommandParams.Count];

        if (compiledQuery.ParamRecipe is { } recipe && recipe.TryReadValues(queryCommand, dbCommandParams, values))
            return values;

        Interlocked.Increment(ref _fallbackRefreshes);

        var pp = ExtractParams(queryCommand);

        if (pp.Count != dbCommandParams.Count)
            return null;

        CopyInvocationValues(pp, values);
        return values;
    }

    private static void CopyInvocationValues(IReadOnlyList<Parameter> parameters, Span<object?> values)
    {
        for (var i = 0; i < values.Length; i++)
            values[i] = parameters[i].Value ?? DBNull.Value;
    }

    public void ResetPreparation(QueryCommand queryCommand)
    {
        //_clearCache = true;
    }

    // The shared terminal route (Any/AnyAsync/Count/...) reuses ONE command instance per DataContext
    // (QueryCache.AnyCommand) and mutates it in place via ReplaceCommand between executions. The
    // recipe must exclude that ACTUAL instance by identity, never by IsPrepared, TResult, command
    // shape or a guessed ownership flag. The Lazy value is not forced: if the shared command was
    // never created it cannot be this command.
    private bool IsSharedAnyCommand(QueryCommand command)
    {
        var anyCommand = _context.AnyCommand;
        if (anyCommand is null || !anyCommand.IsValueCreated)
            return false;

        return ReferenceEquals(anyCommand.Value, command);
    }

    // A table source is immutable, so one instance can be shared by every query (and cached plan)
    // that selects from the entity. This removes a small allocation and a metadata lookup per join.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, FromExpression> _fromCache = new();

    internal static void ClearFromCache() => _fromCache.Clear();

    private FromExpression GetFrom(Type t) => GetFrom(t, _scopeResolver.Resolve(t));

    private FromExpression GetFrom(Type t, MappingVariant variant)
    {
        // Only the built-in default mapping is process-wide. A custom provider can supply a
        // different physical source even for MappingVariant.Default.
        var cacheable = variant == MappingVariant.Default && _metadataProvider is DefaultEntityMetadataProvider;
        if (cacheable && _fromCache.TryGetValue(t, out var cached))
            return cached;

        // Keep the established registration boundary: resolving an arbitrary CLR type here must not
        // silently publish an auto-built table mapping. From<T>() (or another mapping entry point)
        // registers the entity before a query can reach this renderer. Scoped providers may then
        // derive the variant-specific variant from that registered entity.
        if (!DataContextCache.Metadata.TryGetValue(t, out var registered) || string.IsNullOrEmpty(registered.TableName))
            throw new BuildSqlCommandException(
                $"Table name is not registered for type {t}. Materialize the entity first (for example with {nameof(DataContextExtensions.From)}<{t.Name}>()) so its metadata is registered.");

        var entity = _metadataProvider.GetMetadata(t, variant);
        if (!string.IsNullOrEmpty(entity.TableName))
        {
            var from = new FromExpression(entity.TableName, entity.Schema, entity.IsTableNameAuto, t.IsInterface);
            if (cacheable)
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

                var scope = queryCommand is { MappingVariant: var selected } && selected != MappingVariant.Default
                    ? selected
                    : _scopeResolver.Resolve(propT1.PropertyType);
                var f = GetFrom(propT1.PropertyType, scope);

                return f;
            }
            else
            {
                var scope = queryCommand is { MappingVariant: var selected } && selected != MappingVariant.Default
                    ? selected
                    : _scopeResolver.Resolve(srcType);
                return GetFrom(srcType, scope);
            }
        }
        else
            return null;
    }

    private Func<IDataRecord, TResult> GetMapCached<TResult>(QueryCommand queryCommand, string? sql, bool streaming)
    {
        return RowMapperFactory.GetOrBuild<TResult>(queryCommand, sql, _contextType, _logger, _mapColumn, streaming);
    }
}
