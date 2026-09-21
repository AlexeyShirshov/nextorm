using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

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
    private readonly Func<ISqlDialect> _dialect;
    private readonly ILogger? _logger;
    private readonly Type _contextType;
    private readonly Func<SelectExpression, Expression, Expression> _mapColumn;
    private readonly Func<string, object?, DbParameter> _createParam;
    private readonly Func<string, DbCommand> _createCommand;
    private readonly ILogger? _resultSetEnumeratorLogger;
    private readonly bool _logSensitiveData;

    internal QueryPlanner(
        Func<ISqlDialect> dialect,
        Type contextType,
        ProviderHooks hooks,
        LoggingOptions logging)
    {
        _dialect = dialect;
        _logger = logging.Logger;
        _contextType = contextType;
        _mapColumn = hooks.MapColumn;
        _createParam = hooks.CreateParam;
        _createCommand = hooks.CreateCommand;
        _resultSetEnumeratorLogger = logging.ResultSetEnumeratorLogger;
        _logSensitiveData = logging.LogSensitiveData;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private List<Parameter> ExtractParams(QueryCommand queryCommand)
    {
        var @params = new List<Parameter>();
        MakeSelect(queryCommand, true, @params, queryCommand, null);
        return @params;
    }

    // SqlFunctions.Parameter placeholders carry no value - the runtime values are applied by
    // DbPreparedQueryCommand.GetDbCommand. Only computed parameters (captured variables,
    // closures) need to be re-extracted on every cached execution.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsRuntimeParam(string name) => NormParam.IsName(name);

    private string? MakeSelect(QueryCommand queryCommand, bool paramMode, List<Parameter> @params, IQueryRegistry queryProvider, IAliasProvider? aliasProvider)
    {
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
        };
        var sqlBuilder = new SqlBuilder(in ctx);
        return sqlBuilder.MakeSelect(queryCommand);
    }

    public IPreparedQueryCommand<TResult> GetPreparedQueryCommand<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, CancellationToken cancellationToken)
    {
        QueryPlan? queryPlan = null;
        IDbCommandHolder? planCache = null;

        if (!queryCommand.IsPrepared) queryCommand.PrepareCommand(!storeInCache, cancellationToken);
        else queryCommand.RefreshInValuesShape();

        var ext = queryCommand.CustomData as RawSqlOverride;

        if (queryCommand.Cache && storeInCache)
        {
            queryPlan = new QueryPlan(queryCommand, ext?.ManualSql);
            QueryPlanStore.TryGet(_contextType, queryPlan, out planCache);
        }

        if (planCache is null)
        {
            if ((_logger?.IsEnabled(LogLevel.Debug) ?? false) && storeInCache && queryCommand.Cache)
                _logger.LogDebug("Query plan cache miss with hash: {hash}", queryPlan!.GetHashCode());

            var (sql, @params) = ext is null
                ? MakeSelectInternal()
                : (ext.ManualSql, ext.MakeParams?.Invoke());

            Func<IDataRecord, TResult>? map = queryCommand.SingleRow && queryCommand.OneColumn
                ? null
                : GetMapCached(queryCommand, sql);

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
            if (!noParams)
            {
                var parameterList = @params!;
                for (var i = 0; i < parameterList.Count; i++)
                {
                    var p = parameterList[i];
                    dbCommand.Parameters.Add(_createParam(p.Name, p.Value));
                }
            }

            // Some drivers (ClickHouse) turn CommandBehavior.SingleRow into an extra LIMIT 1. The
            // dialect already renders a limit for a single-row command, so the hint must be dropped
            // there to avoid a duplicated clause.
            var singleRow = queryCommand.SingleRow && _dialect().SupportsCommandBehaviorSingleRow;

            var compiledQuery = new DbPreparedQueryCommand<TResult>(dbCommand, map, new PreparedCommandOptions(singleRow, ext is null ? sql! : null, noParams, needsParamRefresh));

            if (createEnumerator)
            {
                var enumerator = CreateResultSetEnumerator(compiledQuery!);
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
                {
                    var dbCommandParams = compiledQuery.DbCommandParams;

                    var pp = ExtractParams(queryCommand);
                    for (int i = 0; i < pp.Count; i++)
                    {
                        // Normalize null to DBNull for providers that reject null parameter values.
                        dbCommandParams[i].Value = pp[i].Value ?? DBNull.Value;
                        Debug.Assert(dbCommandParams[i].ParameterName == pp[i].Name, $"ParameterName {dbCommandParams[i].ParameterName} not equals {pp[i].Name}");
                    }
                }
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
            return (MakeSelect(queryCommand, false, @params, queryCommand, aliasProvider), @params);
        }
    }

    public void ResetPreparation(QueryCommand queryCommand)
    {
        //_clearCache = true;
    }

    // A table source is immutable, so one instance can be shared by every query (and cached plan)
    // that selects from the entity. This removes a small allocation and a metadata lookup per join.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, FromExpression> _fromCache = new();

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

    private Func<IDataRecord, TResult> GetMapCached<TResult>(QueryCommand<TResult> queryCommand, string? sql)
    {
        return RowMapperFactory.GetOrBuild(queryCommand, sql, _contextType, _logger, _mapColumn);
    }

    /// <summary>
    /// Creates the streaming enumerator and applies the context's logging configuration to it. The
    /// enumerator receives the logger directly rather than pulling it from the context through a
    /// property, which is what kept the enumerator tied to the concrete <c>DataContext</c>.
    /// </summary>
    private ResultSetEnumerator<TResult> CreateResultSetEnumerator<TResult>(DbPreparedQueryCommand<TResult> compiledQuery)
    {
        var enumerator = new ResultSetEnumerator<TResult>(compiledQuery);
        enumerator.InitEnvironment(_resultSetEnumeratorLogger, _logSensitiveData);
        return enumerator;
    }
}
