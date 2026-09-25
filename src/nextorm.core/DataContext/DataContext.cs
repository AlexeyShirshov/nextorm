using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Base class for database-backed contexts. Owns the connection lifecycle, the shared query-plan
/// cache and the execution/planning collaborators, and exposes the terminal operators over prepared
/// commands. Providers derive from it and supply the SQL dialect, the connection factory and
/// parameter creation.
/// </summary>
public abstract class DataContext : IDataContext, IConnectionManager, ITransactionManager, IMutationExecutor, IBulkInsertExecutor, IBatchExecutor
{
    private bool _disposed;
    private readonly ContextEnvironment _environment;
    private readonly QueryCache _queryCache;
    private readonly DbConnectionManager _connectionManager;
    private readonly QueryExecutor _executor;
    private readonly QueryPlanner _planner;
    private readonly InterceptorHooks _interceptors;

    /// <summary>
    /// Creates a context from builder options (logger factory, mapping mode, naming convention and
    /// identifier quoting). The connection is created lazily by the provider on first use.
    /// </summary>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public DataContext(DataContextBuilder optionsBuilder)
        : this(null, null, optionsBuilder)
    {
    }

    /// <summary>
    /// Creates a context with an optional connection string or a caller-supplied connection. A string
    /// produces a connection the context owns and disposes; a supplied connection is used as-is and
    /// is not disposed by the context (the caller keeps ownership).
    /// </summary>
    /// <param name="connectionString">Connection string used to create a context-owned connection, or <see langword="null"/>.</param>
    /// <param name="providedConnection">An already-created connection owned by the caller, or <see langword="null"/>.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    protected DataContext(string? connectionString, DbConnection? providedConnection, DataContextBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        // Ambient state (loggers, mapping mode, property bag) lives in its own collaborator, shared
        // with the in-memory context; the context keeps only its lifecycle and provider hooks.
        _environment = new ContextEnvironment(
            optionsBuilder.LoggerFactory,
            GetType(),
            needMapping: true,
            optionsBuilder.ShouldLogSensitiveData,
            optionsBuilder.QuoteIdentifiers,
            optionsBuilder.NamingConvention,
            optionsBuilder.KeywordCase,
            optionsBuilder.MultilineBatchSql);

        _queryCache = new QueryCache(QueryPlanStore.Clear);

        // Interceptors are created once and shared with every axis, so a per-instance
        // AddInterceptor is visible to the connection, planning and execution paths alike.
        _interceptors = new InterceptorHooks(optionsBuilder.QueryInterceptors, optionsBuilder.ConnectionInterceptors);

        // The connection axis owns the connection state machine; the provider keeps its two hooks on
        // the context and they are passed in as delegates (bound here, never invoked during
        // construction). `connectionString`/`providedConnection` differ in ownership: the context
        // disposes only the connection it creates from the string.
        _connectionManager = new DbConnectionManager(
            this,
            new ConnectionHooks(CreateDbConnection, OnConnectionCreated),
            connectionString,
            providedConnection,
            new LoggingOptions(_environment.Logger, LogSensitiveData: _environment.LogSensitiveData),
            () => Dialect.SupportsTransactions,
            _interceptors);

        // Bound once: parameter creation is handed to the execution/planning layers as a delegate
        // instead of passing the context itself, so they no longer depend on the concrete DataContext
        // (F6). Binding the abstract method keeps the provider override on the dispatch path, and a
        // field avoids allocating a delegate per command.
        _createParam = CreateParam;

        // The execution axis receives everything through its constructor (connection role, parameter
        // factory, logging config, disposal state) so it never sees the concrete context.
        _executor = new QueryExecutor(
            this,
            _connectionManager,
            _createParam,
            new LoggingOptions(_environment.Logger, LogSensitiveData: _environment.LogSensitiveData, LogParams: _environment.LogParams),
            () => _disposed,
            () => _connectionManager.CurrentTransaction,
            _interceptors);

        // The planning axis gets the provider hooks as delegates and invokes them lazily: calling the
        // abstract/virtual members here would run derived code before the derived constructor.
        _planner = new QueryPlanner(
            this,
            GetDialect,
            GetType(),
            new ProviderHooks(MapColumn, _createParam, CreateCommand),
            new LoggingOptions(_environment.Logger, _environment.ResultSetEnumeratorLogger, _environment.LogSensitiveData),
            _interceptors);
    }

    private readonly Func<string, object?, DbParameter> _createParam;

    // Late-bound provider hooks: only the delegates are created in the constructor, never the values.
    private ISqlDialect GetDialect() => Dialect;

    private Expression MapColumn(SelectExpression column, Expression param) => MapColumnExpression(column, param);

    /// <summary>
    /// SQL dialect used to render queries. Supplied by the provider; SQL generation depends on this
    /// interface rather than on the context itself.
    /// </summary>
    public abstract ISqlDialect Dialect { get; }

    /// <summary>Logger for the context's own diagnostic messages, or <see langword="null"/> when no logger factory was configured.</summary>
    public ILogger? Logger => _environment.Logger;
    /// <summary>Logger category used to trace executed commands and their parameters.</summary>
    public ILogger? CommandLogger => _environment.CommandLogger;
    /// <summary>Whether projected rows must be materialised into CLR objects.</summary>
    public bool NeedMapping => _environment.NeedMapping;
    /// <summary>
    /// Whether this context quotes physical identifiers by default (set with
    /// <c>DataContextBuilder.UseQuotedIdentifiers</c>). A command can override it with
    /// <c>WithQuotedIdentifiers</c>.
    /// </summary>
    public bool QuoteIdentifiers => _environment.QuoteIdentifiers;
    /// <summary>
    /// Convention applied to auto-derived table and column names by default (set with
    /// <c>DataContextBuilder.UseNamingConvention</c>), or <see langword="null"/> to emit CLR names
    /// verbatim. A command can override it with <c>WithNamingConvention</c>.
    /// </summary>
    public INamingConvention? NamingConvention => _environment.NamingConvention;
    /// <summary>
    /// The letter case in which this context emits SQL keywords by default (set with
    /// <c>DataContextBuilder.UseKeywordCase</c>). A command can override it with
    /// <c>WithKeywordCase</c>.
    /// </summary>
    public KeywordCase KeywordCase => _environment.KeywordCase;
    /// <summary>
    /// Whether rendered batch SQL places each statement on its own line (set with
    /// <c>DataContextBuilder.UseMultilineBatchSql</c>). Defaults to <see langword="false"/>
    /// (statements joined on one line with <c>"; "</c>).
    /// </summary>
    public bool MultilineBatchSql => _environment.MultilineBatchSql;
    /// <summary>User-owned bag of arbitrary state attached to this context.</summary>
    public Dictionary<string, object> Properties => _environment.Properties;
    /// <summary>
    /// Lazily created, cached <c>Any</c> query for this context, or <see langword="null"/> until it is
    /// first needed. Caching it lets repeated existence checks reuse one prepared command.
    /// </summary>
    public Lazy<QueryCommand<bool>>? AnyCommand
    {
        get => _queryCache.AnyCommand;
        set => _queryCache.AnyCommand = value;
    }

    /// <summary>Whether sensitive parameter values may be logged. Kept for provider subclasses.</summary>
    protected internal bool LogSensitiveData => _environment.LogSensitiveData;

    /// <summary>Starts a query over the raw physical table named <paramref name="table"/>.</summary>
    /// <param name="table">The physical table name to select from.</param>
    /// <returns>A builder over the table.</returns>
    public EntityBuilder From(string table) => new(this, table) { Logger = CommandLogger };

    /// <summary>Raised after the context has released its connection during disposal.</summary>
    public event EventHandler? Disposed;

    /// <summary>
    /// Registers a query interceptor on this context instance, after the ones configured on the
    /// builder. See <see cref="IQueryInterceptor"/> for the lifecycle it observes.
    /// </summary>
    /// <param name="interceptor">The interceptor to register; must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="interceptor"/> is <see langword="null"/>.</exception>
    public void AddInterceptor(IQueryInterceptor interceptor)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        _interceptors.Add(interceptor);
    }

    /// <summary>
    /// Registers a connection interceptor on this context instance, after the ones configured on the
    /// builder. See <see cref="IConnectionInterceptor"/> for the lifecycle it observes.
    /// </summary>
    /// <param name="interceptor">The interceptor to register; must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="interceptor"/> is <see langword="null"/>.</exception>
    public void AddInterceptor(IConnectionInterceptor interceptor)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        _interceptors.Add(interceptor);
    }

    /// <summary>Opens the connection if it is not already open.</summary>
    public void EnsureConnectionOpen() => _connectionManager.EnsureConnectionOpen();

    /// <summary>Opens the connection if it is not already open, honouring <paramref name="cancellationToken"/> while opening.</summary>
    /// <param name="cancellationToken">Token used to cancel a slow connection open.</param>
    /// <returns>A task that completes once the connection is open.</returns>
    public Task EnsureConnectionOpenAsync(CancellationToken cancellationToken = default)
        => _connectionManager.EnsureConnectionOpenAsync(cancellationToken);

    /// <summary>Returns the connection in use, creating it on first access (which may throw if the connection string is missing).</summary>
    /// <returns>The underlying database connection.</returns>
    public DbConnection GetConnection() => _connectionManager.GetConnection();

    // Transaction axis (ITransactionManager). Implemented explicitly so the concrete context does not
    // grow six public members; callers reach them through the ITransactionManager role, as documented.
    DbTransaction? ITransactionManager.CurrentTransaction => _connectionManager.CurrentTransaction;

    DbTransaction ITransactionManager.BeginTransaction() => _connectionManager.BeginTransaction();

    DbTransaction ITransactionManager.BeginTransaction(IsolationLevel isolationLevel) => _connectionManager.BeginTransaction(isolationLevel);

    Task<DbTransaction> ITransactionManager.BeginTransactionAsync(CancellationToken cancellationToken) => _connectionManager.BeginTransactionAsync(cancellationToken);

    Task<DbTransaction> ITransactionManager.BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken) => _connectionManager.BeginTransactionAsync(isolationLevel, cancellationToken);

    void ITransactionManager.UseTransaction(DbTransaction? transaction) => _connectionManager.UseTransaction(transaction);

    /// <summary>Creates the provider-specific connection. Called only when no connection was supplied.</summary>
    protected abstract DbConnection CreateDbConnection(string? connectionString);

    /// <summary>
    /// Called for every connection the context starts using (created or supplied) so a provider can
    /// run provider-specific setup, e.g. registering custom functions.
    /// </summary>
    protected virtual void OnConnectionCreated(DbConnection connection)
    {
    }

    /// <summary>The connection string of the supplied connection, or the one the context was built with.</summary>
    public string ConnectionString => _connectionManager.ConnectionString;

    /// <summary>Creates a command bound to the current connection with <paramref name="sql"/> as its text. The caller owns the returned command.</summary>
    /// <param name="sql">The SQL text to execute.</param>
    /// <returns>A new command on the current connection, bound to the active transaction when one is enlisted.</returns>
    public DbCommand CreateCommand(string sql)
    {
        var cmd = _connectionManager.GetConnection().CreateCommand();
        cmd.CommandText = sql;
        if (_connectionManager.CurrentTransaction is { } transaction)
            cmd.Transaction = transaction;
        return cmd;
    }

    // ExtractParams / IsRuntimeParam / MakeSelect live in QueryPlanner (the planning axis).
    // LogParams and the three GetDbCommand overloads live in QueryExecutor (the execution axis).

    /// <summary>
    /// Prepares <paramref name="queryCommand"/> against the current dialect, optionally building a
    /// streaming enumerator and storing the plan in the cache. When <paramref name="storeInCache"/> is
    /// set, later calls return the cached plan instead of re-planning.
    /// </summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="queryCommand">The command to prepare.</param>
    /// <param name="createEnumerator">When <see langword="true"/>, compiles a streaming row enumerator as part of preparation.</param>
    /// <param name="storeInCache">When <see langword="true"/>, stores the prepared command in the plan cache.</param>
    /// <param name="cancellationToken">Token used to cancel preparation.</param>
    /// <returns>The prepared command, ready to execute.</returns>
    public IPreparedQueryCommand<TResult> GetPreparedQueryCommand<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, CancellationToken cancellationToken)
    {
        // The check is allocation-free and false for every ordinary query, so the common preparation
        // path only pays a few branches.
        if (queryCommand.HasTemporaryTableSource())
        {
            var tempTables = new List<ITempTableSource>();
            queryCommand.CollectTempTableSources(tempTables);
            return GetPreparedTemporaryTableCommand(queryCommand, tempTables, createEnumerator, cancellationToken);
        }

        return _planner.GetPreparedQueryCommand(queryCommand, createEnumerator, storeInCache, cancellationToken);
    }

    // A query that reads a lazy temporary table is not a single statement: the table must be created on
    // the same session as the read. The command is prepared normally (for its mapper and result shape)
    // and a batch plan is attached, so the execution terminals run DROP + CREATE TEMPORARY TABLE + read
    // in one round trip. The plan is rebuilt on every preparation because the source query's captured
    // parameters may differ between executions; the read command is therefore never cached.
    private IPreparedQueryCommand<TResult> GetPreparedTemporaryTableCommand<TResult>(
        QueryCommand<TResult> queryCommand,
        List<ITempTableSource> tempTables,
        bool createEnumerator,
        CancellationToken cancellationToken)
    {
        // storeInCache: false is what keeps the read plan out of the plan cache (the source query's
        // captured parameters must be re-rendered on every execution). Setting Cache = false here
        // would leak: for `Any` the command is the context-shared AnyCommand, so it would disable
        // plan caching for every later query on that context.
        var prepared = (DbPreparedQueryCommand<TResult>)_planner.GetPreparedQueryCommand(queryCommand, createEnumerator, false, cancellationToken);

        prepared.PendingBatch = BuildTemporaryTableBatch(queryCommand, tempTables);
        return prepared;
    }

    BatchPlan IBatchExecutor.RenderTemporaryTableBatch(QueryCommand command)
    {
        var tempTables = new List<ITempTableSource>();
        command.CollectTempTableSources(tempTables);

        if (tempTables.Count == 0)
            throw new InvalidOperationException("The command does not read a temporary table; the batch form is only available for a source created with AsTempTable.");

        return BuildTemporaryTableBatch(command, tempTables);
    }

    private BatchPlan BuildTemporaryTableBatch(QueryCommand command, List<ITempTableSource> tempTables)
    {
        // The command's resolved quoting/case flags are set during preparation; ToBatchSql may be the
        // first to look at them, so prepare the command before rendering the DROP.
        if (!command.IsPrepared)
            command.PrepareCommand(false, CancellationToken.None);

        var steps = new BatchStepSpec[tempTables.Count * 2 + 1];
        var index = 0;
        foreach (var tempTable in tempTables)
        {
            steps[index++] = BatchStepSpec.ForRaw(RenderDropTemporaryTable(command, tempTable.Name));
            steps[index++] = BatchStepSpec.ForCreateTableAs(
                new CreateTableAsCommand(tempTable.Source.ResultType ?? typeof(object), tempTable.Name, temporary: true, tempTable.Source, tempTable.Options));
        }

        steps[index] = BatchStepSpec.ForResult(command);
        return ((IBatchExecutor)this).RenderBatch(steps);
    }

    private string RenderDropTemporaryTable(QueryCommand command, string name)
    {
        var quoted = command.ResolvedQuoteIdentifiers ? Dialect.QuoteIdentifier(name) : name;
        return SqlKeywords.Of(command.ResolvedKeywordCase, "drop table if exists ") + quoted;
    }

    /// <summary>Creates a provider-specific parameter with the given name and value.</summary>
    /// <param name="name">The parameter name, without the provider's prefix.</param>
    /// <param name="value">The parameter value, or <see langword="null"/>.</param>
    /// <returns>A new database parameter.</returns>
    public abstract DbParameter CreateParam(string name, object? value);

    /// <summary>
    /// Maps a projected column to a reader accessor. Providers whose reader does not widen CLR
    /// types (SqlClient throws when a typed getter does not match the field type, for example an
    /// int column projected as long) can override this to read the value and convert it.
    /// </summary>
    public virtual Expression MapColumnExpression(SelectExpression column, Expression param) => RowMapperFactory.MapColumn(column, param, Dialect.SupportsNativeDuration, Dialect);

    /// <summary>Resets the cached execution plan of <paramref name="queryCommand"/> so it is rebuilt on next use.</summary>
    /// <param name="queryCommand">The command whose plan should be discarded.</param>
    public void ResetPreparation(QueryCommand queryCommand)
        => _planner.ResetPreparation(queryCommand);

    /// <summary>Resolves the FROM source for <paramref name="srcType"/>, reusing the source carried by <paramref name="queryCommand"/> when present.</summary>
    /// <param name="srcType">The entity or source type to resolve.</param>
    /// <param name="queryCommand">The derived command whose source should be used, or <see langword="null"/>.</param>
    /// <returns>The resolved FROM expression, or <see langword="null"/> when the type is not mapped.</returns>
    public FromExpression? GetFrom(Type srcType, QueryCommand? queryCommand)
        => _planner.GetFrom(srcType, queryCommand);

    // DML execution (IMutationExecutor). Kept as explicit implementations so the mutation axis does
    // not widen the context's public surface; the public entry points live on the InsertBuilder.
    string IMutationExecutor.Render(MutationCommand command)
    {
        return BuildMutationSql(command).Sql;
    }

    bool IMutationExecutor.SupportsGeneratedColumns => Dialect.SupportsReturning || Dialect.SupportsOutput;

    string IMutationExecutor.RenderIdentityFunction(MutationCommand command)
    {
        EnsureIdentityFunctionSupported();
        // Mirror execution: a non-identity key cannot be read through the identity function.
        if (command is InsertCommand { IdentityColumn: not null })
            EnsureIdentityColumn(command);

        var (sql, _) = BuildInsertSql((InsertCommand)command);
        return sql + "; " + Dialect.MakeIdentityFunction(KeywordCase);
    }

    object? IMutationExecutor.ExecuteIdentityFunction(MutationCommand command)
    {
        EnsureIdentityFunctionSupported();
        var (sql, parameters) = BuildInsertSql((InsertCommand)command);
        // The identity function runs in the same batch as the insert: SCOPE_IDENTITY() is scoped to the
        // batch, so issuing it as a separate command would return NULL on SQL Server.
        return _executor.ExecuteScalar(sql + "; " + Dialect.MakeIdentityFunction(KeywordCase), parameters);
    }

    async Task<object?> IMutationExecutor.ExecuteIdentityFunction(MutationCommand command, CancellationToken cancellationToken)
    {
        EnsureIdentityFunctionSupported();
        var (sql, parameters) = BuildInsertSql((InsertCommand)command);
        return await _executor.ExecuteScalarAsync(sql + "; " + Dialect.MakeIdentityFunction(KeywordCase), parameters, cancellationToken).ConfigureAwait(false);
    }

    int IMutationExecutor.Execute(MutationCommand command)
    {
        var (sql, parameters) = BuildMutationSql(command);
        return _executor.ExecuteNonQuery(sql, parameters);
    }

    async Task<int> IMutationExecutor.Execute(MutationCommand command, CancellationToken cancellationToken)
    {
        var (sql, parameters) = BuildMutationSql(command);
        return await _executor.ExecuteNonQueryAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
    }

    object? IMutationExecutor.ExecuteIdentity(MutationCommand command)
    {
        if (Dialect.SupportsReturning || Dialect.SupportsOutput)
        {
            var (sql, parameters) = BuildInsertSql((InsertCommand)command);
            return _executor.ExecuteScalar(sql, parameters);
        }

        if (Dialect.SupportsLastInsertId)
        {
            EnsureIdentityColumn(command);
            var (sql, parameters) = BuildInsertSql((InsertCommand)command);
            _executor.ExecuteNonQuery(sql, parameters);
            return _executor.ExecuteScalar(Dialect.MakeLastInsertId(KeywordCase), []);
        }

        throw new NotSupportedException(
            $"{GetType().Name} cannot return a generated identity: the provider has no RETURNING, OUTPUT or LAST_INSERT_ID form. Use Insert() instead.");
    }

    async Task<object?> IMutationExecutor.ExecuteIdentity(MutationCommand command, CancellationToken cancellationToken)
    {
        if (Dialect.SupportsReturning || Dialect.SupportsOutput)
        {
            var (sql, parameters) = BuildInsertSql((InsertCommand)command);
            return await _executor.ExecuteScalarAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
        }

        if (Dialect.SupportsLastInsertId)
        {
            EnsureIdentityColumn(command);
            var (sql, parameters) = BuildInsertSql((InsertCommand)command);
            await _executor.ExecuteNonQueryAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
            return await _executor.ExecuteScalarAsync(Dialect.MakeLastInsertId(KeywordCase), [], cancellationToken).ConfigureAwait(false);
        }

        throw new NotSupportedException(
            $"{GetType().Name} cannot return a generated identity: the provider has no RETURNING, OUTPUT or LAST_INSERT_ID form. Use Insert() instead.");
    }

    IReadOnlyList<TResult> IMutationExecutor.ExecuteReturning<TResult>(MutationCommand command, SelectExpression[] selectList, bool oneColumn)
    {
        EnsureReturningSupported();
        EnsureReturningMaterializable<TResult>(oneColumn);
        var (sql, parameters) = BuildReturningSql(command);
        var mapper = RowMapperFactory.GetOrBuild<TResult>(sql, GetType(), selectList, oneColumn, MapColumnExpression);
        return _executor.ExecuteReader(sql, parameters, mapper);
    }

    async Task<IReadOnlyList<TResult>> IMutationExecutor.ExecuteReturning<TResult>(MutationCommand command, SelectExpression[] selectList, bool oneColumn, CancellationToken cancellationToken)
    {
        EnsureReturningSupported();
        EnsureReturningMaterializable<TResult>(oneColumn);
        var (sql, parameters) = BuildReturningSql(command);
        var mapper = RowMapperFactory.GetOrBuild<TResult>(sql, GetType(), selectList, oneColumn, MapColumnExpression);
        return await _executor.ExecuteReaderAsync(sql, parameters, mapper, cancellationToken).ConfigureAwait(false);
    }

    // Bulk insert (IBulkInsertExecutor). The native path is provider-supplied through the
    // BulkInsertRows hooks; the portable path is the shared INSERT ... VALUES loop and is chosen by the
    // builder when the request needs RETURNING/OUTPUT, conflict handling or an identity-insert form, or
    // when the provider has no native bulk API.
    int IBulkInsertExecutor.BulkInsert(BulkInsertCommand command)
    {
        var rows = command.SyncRows
            ?? throw new InvalidOperationException("BulkInsert is synchronous but the source is async; use BulkInsertAsync instead.");

        return BulkInsertRows(
            SqlMutationBuilder.RenderTableReference(Dialect, QuoteIdentifiers, command.TableName, command.TableSchema, command.IsTableNameAuto, command.EntityType, NamingConvention),
            ResolveBulkColumnNames(command.Columns),
            command.Columns,
            rows,
            command.TimeoutSeconds,
            command.Batch?.MaxBatchSize,
            command.Progress,
            command.NotifyEvery);
    }

    async Task<int> IBulkInsertExecutor.BulkInsertAsync(BulkInsertCommand command, CancellationToken cancellationToken)
    {
        var rows = command.AsyncRows ?? new SyncToAsyncEnumerable<object?[]>(command.SyncRows!);

        return await BulkInsertRowsAsync(
            SqlMutationBuilder.RenderTableReference(Dialect, QuoteIdentifiers, command.TableName, command.TableSchema, command.IsTableNameAuto, command.EntityType, NamingConvention),
            ResolveBulkColumnNames(command.Columns),
            command.Columns,
            rows,
            command.TimeoutSeconds,
            command.Batch?.MaxBatchSize,
            command.Progress,
            command.NotifyEvery,
            cancellationToken).ConfigureAwait(false);
    }

    private string[] ResolveBulkColumnNames(IReadOnlyList<IPropertyMetadata> columns)
    {
        var names = new string[columns.Count];
        for (var i = 0; i < names.Length; i++)
            names[i] = SqlMutationBuilder.ResolveColumnName(columns[i], NamingConvention);

        return names;
    }

    /// <summary>
    /// Writes <paramref name="rows"/> through the provider's native bulk API. The default throws: a
    /// provider opts in by setting <c>ISqlDialect.SupportsBulkCopy</c> and overriding this method (and
    /// <see cref="BulkInsertRowsAsync"/>). The column names are already convention-resolved (unquoted); the
    /// table reference is already convention-resolved, schema-qualified and quoted when configured.
    /// </summary>
    /// <param name="tableName">The rendered target table reference (schema-qualified, quoted when configured).</param>
    /// <param name="columnNames">The convention-resolved (unquoted) written column names, in row order.</param>
    /// <param name="columns">The mapped columns, in row order (for CLR types and identity flags).</param>
    /// <param name="rows">The rows to write; each array matches <paramref name="columnNames"/> by ordinal.</param>
    /// <param name="commandTimeoutSeconds">The command timeout in seconds, or <see langword="null"/> for the provider default.</param>
    /// <param name="maxBatchSize">The maximum rows per batch, or <see langword="null"/> for the provider default.</param>
    /// <param name="progress">The progress callback, called with the cumulative written-row count, or <see langword="null"/>.</param>
    /// <param name="notifyEvery">The progress reporting interval in rows; the provider drives its cadence from it.</param>
    /// <returns>The number of rows written.</returns>
    /// <exception cref="NotSupportedException">The provider has no native bulk implementation.</exception>
    protected virtual int BulkInsertRows(string tableName, IReadOnlyList<string> columnNames, IReadOnlyList<IPropertyMetadata> columns, IEnumerable<object?[]> rows, int? commandTimeoutSeconds, int? maxBatchSize, Action<int>? progress, int notifyEvery)
        => throw new NotSupportedException($"{GetType().Name} declares ISqlDialect.SupportsBulkCopy but does not implement the native bulk path.");

    /// <summary>Asynchronously writes <paramref name="rows"/> through the provider's native bulk API.</summary>
    /// <param name="tableName">The rendered target table reference (schema-qualified, quoted when configured).</param>
    /// <param name="columnNames">The convention-resolved (unquoted) written column names, in row order.</param>
    /// <param name="columns">The mapped columns, in row order (for CLR types and identity flags).</param>
    /// <param name="rows">The rows to write; each array matches <paramref name="columnNames"/> by ordinal.</param>
    /// <param name="commandTimeoutSeconds">The command timeout in seconds, or <see langword="null"/> for the provider default.</param>
    /// <param name="maxBatchSize">The maximum rows per batch, or <see langword="null"/> for the provider default.</param>
    /// <param name="progress">The progress callback, called with the cumulative written-row count, or <see langword="null"/>.</param>
    /// <param name="notifyEvery">The progress reporting interval in rows; the provider drives its cadence from it.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of rows written.</returns>
    /// <exception cref="NotSupportedException">The provider has no native bulk implementation.</exception>
    protected virtual Task<int> BulkInsertRowsAsync(string tableName, IReadOnlyList<string> columnNames, IReadOnlyList<IPropertyMetadata> columns, IAsyncEnumerable<object?[]> rows, int? commandTimeoutSeconds, int? maxBatchSize, Action<int>? progress, int notifyEvery, CancellationToken cancellationToken)
        => throw new NotSupportedException($"{GetType().Name} declares ISqlDialect.SupportsBulkCopy but does not implement the native bulk path.");

    private (string Sql, List<Parameter> Parameters) BuildReturningSql(MutationCommand command)
        => command switch
        {
            InsertCommand insert => BuildInsertSql(insert),
            UpdateCommand update => BuildUpdateSql(update),
            DeleteCommand delete => BuildDeleteSql(delete),
            MergeCommand merge => BuildMergeSql(merge),
            _ => throw new NotSupportedException($"Unsupported returning mutation command {command.GetType().Name}."),
        };

    private (string Sql, List<Parameter> Parameters) BuildInsertSql(InsertCommand command)
        => BuildInsertSql(command, new DefaultParameterProvider(), string.Empty);

    private (string Sql, List<Parameter> Parameters) BuildInsertSql(InsertCommand command, IParameterProvider parameterProvider, string parameterNamePrefix)
    {
        if (command.Source is null)
            return SqlMutationBuilder.MakeInsert(Dialect, QuoteIdentifiers, NamingConvention, command, KeywordCase, parameterProvider: parameterProvider);

        var (withSql, sourceSql, sourceParameters) = _planner.RenderSource(command.Source, parameterProvider, null, null, parameterNamePrefix);
        var (insertSql, parameters) = SqlMutationBuilder.MakeInsert(Dialect, QuoteIdentifiers, NamingConvention, command, KeywordCase, sourceSql, sourceParameters, parameterProvider);

        // A data-modifying CTE (or a hoisted read CTE) must precede INSERT, not sit inside the SELECT.
        return (withSql is null ? insertSql : withSql + insertSql, parameters);
    }

    private (string Sql, List<Parameter> Parameters) BuildUpdateSql(UpdateCommand command)
        => BuildUpdateSql(command, new DefaultParameterProvider(), new List<Parameter>(), string.Empty);

    private (string Sql, List<Parameter> Parameters) BuildUpdateSql(UpdateCommand command, IParameterProvider provider, List<Parameter> parameters, string parameterNamePrefix)
    {
        if (!Dialect.SupportsUpdate)
            throw new NotSupportedException(
                $"{GetType().Name} does not support UPDATE: the provider has no synchronous single-statement UPDATE form.");

        // One parameter provider for the whole statement: the SET list, the predicate and the key
        // values must not restart parameter numbering, or their names would collide.
        var (setSql, _) = _planner.RenderAssignments(command, provider, parameters, parameterNamePrefix);

        string? whereSql = null;
        if (command.Keys is not { Count: > 0 })
        {
            var (rendered, _) = _planner.RenderPredicate(command.Source, provider, parameters, parameterNamePrefix);
            whereSql = rendered;
        }

        return SqlMutationBuilder.MakeUpdate(Dialect, QuoteIdentifiers, NamingConvention, command, setSql, parameters, whereSql, provider, KeywordCase);
    }

    private (string Sql, List<Parameter> Parameters) BuildMutationSql(MutationCommand command)
    {
        EnsureReturningSupportedIfNeeded(command);
        return command switch
        {
            InsertCommand insert => BuildInsertSql(insert),
            UpdateCommand update => BuildUpdateSql(update),
            UpdateJoinCommand updateJoin => BuildUpdateJoinSql(updateJoin),
            MergeCommand merge => BuildMergeSql(merge),
            DeleteCommand delete => BuildDeleteSql(delete),
            DeleteJoinCommand deleteJoin => BuildDeleteJoinSql(deleteJoin),
            TruncateCommand truncate => BuildTruncateSql(truncate),
            CreateTableAsCommand createTableAs => BuildCreateTableAsSql(createTableAs),
            DropTableCommand dropTable => BuildDropTableSql(dropTable),
            _ => throw new NotSupportedException($"Unsupported mutation command {command.GetType().Name}."),
        };
    }

    private (string Sql, List<Parameter> Parameters) BuildMergeSql(MergeCommand command)
    {
        if (command.Branches is null)
        {
            if (command.Source is null)
                return SqlMutationBuilder.MakeMerge(Dialect, QuoteIdentifiers, NamingConvention, command, KeywordCase);

            var (withInsert, sourceInsert, sourceInsertParameters) = _planner.RenderSource(command.Source);
            var (insertSql, insertParameters) = SqlMutationBuilder.MakeMerge(Dialect, QuoteIdentifiers, NamingConvention, command, KeywordCase, null, sourceInsert, sourceInsertParameters);
            return (withInsert is null ? insertSql : withInsert + insertSql, insertParameters);
        }

        // Full MERGE: render the search conditions into the same accumulator the VALUES rows use, so their
        // autogenerated @pN names do not collide. Conditions are rendered first; the rows continue the sequence.
        var provider = new DefaultParameterProvider();
        var accumulator = new List<Parameter>();
        var quoteIdentifiers = command.Source?.ResolvedQuoteIdentifiers ?? QuoteIdentifiers;
        var namingConvention = command.Source?.ResolvedNamingConvention ?? NamingConvention;
        var keywordCase = command.Source?.ResolvedKeywordCase ?? KeywordCase;
        var registry = command.Registry!;

        string? matchConditionSql = null;
        if (command.MatchCondition is not null)
            matchConditionSql = _planner.RenderMergeCondition(command.MatchCondition, command.EntityType, registry, provider, accumulator, quoteIdentifiers, namingConvention, keywordCase);

        string?[]? branchConditions = null;
        for (var i = 0; i < command.Branches.Count; i++)
        {
            if (command.Branches[i].Condition is not { } condition)
                continue;

            branchConditions ??= new string?[command.Branches.Count];
            branchConditions[i] = _planner.RenderMergeCondition(condition, command.EntityType, registry, provider, accumulator, quoteIdentifiers, namingConvention, keywordCase);
        }

        if (command.Source is null)
        {
            var (sql, parameters) = SqlMutationBuilder.MakeMerge(Dialect, QuoteIdentifiers, NamingConvention, command, KeywordCase, provider, null, null, accumulator, matchConditionSql, branchConditions);
            return (sql, parameters);
        }

        var (withSql, sourceSql, _) = _planner.RenderSource(command.Source, provider, accumulator);
        // The source parameters already sit in the shared accumulator, so MakeMerge must not re-add them;
        // passing them again would duplicate every @pN.
        var (fullSql, fullParameters) = SqlMutationBuilder.MakeMerge(Dialect, QuoteIdentifiers, NamingConvention, command, KeywordCase, provider, sourceSql, null, accumulator, matchConditionSql, branchConditions);
        return (withSql is null ? fullSql : withSql + fullSql, fullParameters);
    }

    private (string Sql, List<Parameter> Parameters) BuildTruncateSql(TruncateCommand command)
    {
        if (!Dialect.SupportsTruncate)
            throw new NotSupportedException(
                $"{GetType().Name} does not support TRUNCATE; remove every row with DeleteFrom<T>().All() instead.");

        return (SqlMutationBuilder.MakeTruncate(Dialect, QuoteIdentifiers, NamingConvention, command, KeywordCase), []);
    }

    private (string Sql, List<Parameter> Parameters) BuildCreateTableAsSql(CreateTableAsCommand command)
    {
        // A SELECT ... INTO dialect (SQL Server) injects the target into the top-level select list; the
        // other dialects wrap the body (WITH kept inside the query, after AS) in CREATE TABLE ... AS SELECT.
        // Identifier quoting and keyword casing follow the source command (its override, else the context
        // default) so a per-command override applies to the target exactly as it does to the body.
        var quoteIdentifiers = command.Source.QuoteIdentifiers ?? QuoteIdentifiers;
        var keywordCase = command.Source.KeywordCase ?? KeywordCase;
        var selectInto = SqlMutationBuilder.ResolveCreateTableAsInto(Dialect, quoteIdentifiers, command, keywordCase);
        var (withSql, sourceSql, sourceParameters) = _planner.RenderSource(command.Source, selectInto);
        return SqlMutationBuilder.MakeCreateTableAsSelect(Dialect, quoteIdentifiers, withSql, sourceSql, sourceParameters, command, keywordCase);
    }

    private (string Sql, List<Parameter> Parameters) BuildDropTableSql(DropTableCommand command)
        => (SqlMutationBuilder.MakeDropTableIfExists(Dialect, command.QuoteIdentifiers ?? QuoteIdentifiers, command.TargetName, command.KeywordCase ?? KeywordCase), []);

    // A per-statement placeholder prefix for captured members: the shared parameter provider already
    // keeps inline values unique, but a member (closure variable) is named after the member, so two
    // statements capturing the same variable would otherwise collide in the ;-joined fallback.
    private static string BatchParameterPrefix(int index)
        => "b" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "_";

    // Batch execution (IBatchExecutor). Rendering shares one parameter provider across every step so
    // placeholder names never collide, which is what makes both the ;-joined SQLite fallback and the
    // per-command DbBatch parameters correct. The execution primitive lives on QueryExecutor.
    BatchPlan IBatchExecutor.RenderBatch(IReadOnlyList<BatchStepSpec> steps)
    {
        if (!Dialect.SupportsBatch)
            throw new NotSupportedException(
                $"{GetType().Name} cannot execute a batch: the provider has no single-round-trip batch form. "
                + "Run the statements separately, or use a provider whose dialect sets ISqlDialect.SupportsBatch.");

        if (steps.Count == 0 || steps[^1].Query is not { } resultQuery)
            throw new InvalidOperationException("A batch must end with a result-bearing query.");

        var provider = new DefaultParameterProvider();
        var accumulator = new List<Parameter>();
        var statements = new List<BatchStatement>(steps.Count);
        string? resultSql = null;

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var start = accumulator.Count;
            string sql;

            if (step.RawSql is { } rawSql)
            {
                sql = rawSql;
            }
            else if (step.CreateTableAs is { } createTableAs)
            {
                var quoteIdentifiers = createTableAs.Source.QuoteIdentifiers ?? QuoteIdentifiers;
                var keywordCase = createTableAs.Source.KeywordCase ?? KeywordCase;
                if (createTableAs.Options.DropExisting)
                    statements.Add(new BatchStatement(
                        SqlMutationBuilder.MakeDropTableIfExists(Dialect, quoteIdentifiers, createTableAs.TargetName, keywordCase),
                        Array.Empty<Parameter>()));

                var selectInto = SqlMutationBuilder.ResolveCreateTableAsInto(Dialect, quoteIdentifiers, createTableAs, keywordCase);
                var (withSql, sourceSql, _) = _planner.RenderSource(createTableAs.Source, provider, accumulator, selectInto, BatchParameterPrefix(i));
                (sql, _) = SqlMutationBuilder.MakeCreateTableAsSelect(Dialect, quoteIdentifiers, withSql, sourceSql, accumulator, createTableAs, keywordCase);
            }
            else if (step.Mutation is { } mutation)
            {
                (sql, var mutationParameters) = BuildBatchMutationSql(mutation, provider, BatchParameterPrefix(i));
                accumulator.AddRange(mutationParameters);
            }
            else
            {
                var (withSql, sourceSql, _) = _planner.RenderSource(step.Query!, provider, accumulator, null, BatchParameterPrefix(i));
                sql = withSql is null ? sourceSql : withSql + sourceSql;

                if (i == steps.Count - 1)
                    resultSql = sql;
            }

            var count = accumulator.Count - start;
            statements.Add(new BatchStatement(sql, SliceStatementParameters(accumulator, start, count)));
        }

        return new BatchPlan(statements, resultQuery, resultSql!, Dialect.BatchUsesJoinedCommand, MultilineBatchSql);
    }

    // A statement's parameters are the accumulator slice it produced. A captured member referenced more
    // than once in the same statement is visited once per reference, so its (member-named) parameter is
    // added repeatedly with the same name; keeping the first is enough for the SQL, which references the
    // one name, and providers such as SQLite/MySQL reject a duplicate parameter name.
    private static IReadOnlyList<Parameter> SliceStatementParameters(List<Parameter> accumulator, int start, int count)
    {
        if (count == 0)
            return Array.Empty<Parameter>();

        if (count == 1)
            return new[] { accumulator[start] };

        var parameters = new List<Parameter>(count);
        for (var i = start; i < start + count; i++)
        {
            var candidate = accumulator[i];
            var duplicate = false;
            for (var j = 0; j < parameters.Count; j++)
            {
                if (parameters[j].Name == candidate.Name)
                {
                    duplicate = true;
                    break;
                }
            }

            if (!duplicate)
                parameters.Add(candidate);
        }

        return parameters;
    }

    Func<IDataRecord, TResult> IBatchExecutor.BuildBatchMapper<TResult>(BatchPlan plan)
    {
        if (plan.ResultQuery is not QueryCommand<TResult> query)
            throw new InvalidOperationException("The batch has no result-bearing query to materialize.");

        return RowMapperFactory.GetOrBuild(query, plan.ResultSql, GetType(), Logger, MapColumnExpression);
    }

    List<TResult> IBatchExecutor.ExecuteBatch<TResult>(BatchPlan plan, Func<IDataRecord, TResult> mapper)
        => _executor.RunBatch(plan, mapper);

    Task<List<TResult>> IBatchExecutor.ExecuteBatchAsync<TResult>(BatchPlan plan, Func<IDataRecord, TResult> mapper, CancellationToken cancellationToken)
        => _executor.RunBatchAsync(plan, mapper, cancellationToken);

    IAsyncEnumerable<TResult> IBatchExecutor.StreamBatch<TResult>(BatchPlan plan, Func<IDataRecord, TResult> mapper, CancellationToken cancellationToken)
        => _executor.RunBatchStream(plan, mapper, cancellationToken);

    private (string Sql, List<Parameter> Parameters) BuildDeleteSql(DeleteCommand command)
        => BuildDeleteSql(command, new DefaultParameterProvider(), string.Empty);

    private (string Sql, List<Parameter> Parameters) BuildDeleteSql(DeleteCommand command, IParameterProvider provider, string parameterNamePrefix)
    {
        if (!Dialect.SupportsDelete)
            throw new NotSupportedException(
                $"{GetType().Name} does not support DELETE: the provider has no synchronous single-statement DELETE form (use its ALTER TABLE ... DELETE mutation directly).");

        if (command.Condition is null)
            return SqlMutationBuilder.MakeDelete(Dialect, QuoteIdentifiers, NamingConvention, command, null, [], KeywordCase, provider);

        var parameters = new List<Parameter>();
        var (whereSql, _) = _planner.RenderPredicate(command.Condition, provider, parameters, parameterNamePrefix);
        return SqlMutationBuilder.MakeDelete(Dialect, QuoteIdentifiers, NamingConvention, command, whereSql, parameters, KeywordCase, provider);
    }

    // Renders a side-effecting DML step of a batch with the batch's shared parameter provider, so its
    // placeholder names continue the batch-wide sequence and cannot collide with the other steps'. The
    // captured-member prefix keeps two DML steps that capture the same variable from colliding.
    private (string Sql, List<Parameter> Parameters) BuildBatchMutationSql(MutationCommand command, IParameterProvider provider, string parameterNamePrefix)
        => command switch
        {
            InsertCommand insert => BuildInsertSql(insert, provider, parameterNamePrefix),
            UpdateCommand update => BuildUpdateSql(update, provider, new List<Parameter>(), parameterNamePrefix),
            DeleteCommand delete => BuildDeleteSql(delete, provider, parameterNamePrefix),
            TruncateCommand truncate => BuildTruncateSql(truncate),
            _ => throw new NotSupportedException($"The mutation {command.GetType().Name} cannot be added to a batch."),
        };

    private (string Sql, List<Parameter> Parameters) BuildDeleteJoinSql(DeleteJoinCommand command)
    {
        if (!Dialect.SupportsDeleteJoin)
            throw new NotSupportedException(
                $"{GetType().Name} does not support deleting from a joined table: the provider has no native multi-table DELETE.");

        return _planner.RenderDeleteJoin(command);
    }

    private (string Sql, List<Parameter> Parameters) BuildUpdateJoinSql(UpdateJoinCommand command)
    {
        if (!Dialect.SupportsUpdateJoin)
            throw new NotSupportedException(
                $"{GetType().Name} does not support updating from a joined table: the provider has no native multi-table UPDATE.");

        return _planner.RenderUpdateJoin(command);
    }

    private void EnsureReturningSupported()
    {
        if (!Dialect.SupportsReturning && !Dialect.SupportsOutput)
            throw new NotSupportedException(
                $"{GetType().Name} cannot return written rows: the provider has no RETURNING or OUTPUT form.");
    }

    // An interface/abstract TResult cannot be materialized as a whole (no constructor). Reject it with a
    // clear message at execution time rather than letting RowMaterializerBuilder surface an opaque
    // QueryPreparationException; SQL generation (ToSql) stays provider- and provider-shape-agnostic.
    private static void EnsureReturningMaterializable<TResult>(bool oneColumn)
    {
        var resultType = typeof(TResult);

        if (!oneColumn && (resultType.IsInterface || resultType.IsAbstract))
            throw new NotSupportedException(
                $"Cannot materialize the returned rows into {resultType.Name}: it is an interface or abstract. Project the mapped columns instead, e.g. Returning(x => new {{ x.Id, x.Name }}).");
    }

    private void EnsureReturningSupportedIfNeeded(MutationCommand command)
    {
        if (command.OutputInto is not null)
            EnsureOutputIntoSupported();
        if (command.ReturningColumns is { Count: > 0 })
            EnsureReturningSupported();
    }

    private void EnsureOutputIntoSupported()
    {
        if (!Dialect.SupportsOutputInto)
            throw new NotSupportedException(
                $"{GetType().Name} cannot write modified rows into a table through OUTPUT ... INTO: the provider has no OUTPUT INTO form (SQL Server only).");
    }

    private void EnsureIdentityFunctionSupported()
    {
        if (!Dialect.SupportsIdentityFunction)
            throw new NotSupportedException(
                $"{GetType().Name} cannot return a generated identity without naming the column: the provider has no identity function (SCOPE_IDENTITY, lastval, LAST_INSERT_ID or last_insert_rowid). Project the key column instead, e.g. ReturningIdentity(x => x.Id).");
    }

    // LAST_INSERT_ID()/last_insert_rowid() return the auto-increment value regardless of the requested
    // column, so the fallback is only sound when the resolved column is the declared identity.
    private static void EnsureIdentityColumn(MutationCommand command)
    {
        if (command is not InsertCommand { IdentityColumn: { IsIdentity: true } })
            throw new NotSupportedException(
                "The provider returns the auto-increment value through LAST_INSERT_ID()/last_insert_rowid(), but the selected column is not declared as an identity. Use a provider with RETURNING/OUTPUT or select the identity column.");
    }

    /// <summary>
    /// Releases the connection owned by the context and raises <see cref="Disposed"/>. Called from
    /// <see cref="Dispose()"/>; <paramref name="disposing"/> is <see langword="true"/> for managed
    /// cleanup.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> when invoked from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                DisposeStaff();
            }

            _disposed = true;
        }
    }

    /// <summary>Closes the context-owned connection and raises <see cref="Disposed"/>. Safe to call more than once.</summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Asynchronously releases the context. Delegates to <see cref="Dispose()"/> so cleanup runs exactly once.</summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        // Route through Dispose() so the _disposed guard is honored and cleanup runs
        // exactly once, matching the synchronous disposal path (CA1816).
        Dispose();

        return ValueTask.CompletedTask;
    }

    private void DisposeStaff()
    {
        _connectionManager.DisposeConnection();
        Disposed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Creates an async enumerator that streams the prepared command's rows.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to enumerate.</param>
    /// <param name="params">Positional parameters bound to the command, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token used to cancel enumeration.</param>
    /// <returns>An async enumerator over the result rows.</returns>
    public IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => _executor.CreateAsyncEnumerator(preparedQueryCommand, @params, cancellationToken);

    /// <summary>Executes the command and buffers all rows into a list.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token used to cancel execution.</param>
    /// <returns>The materialised rows; empty when the query yields none.</returns>
    public async Task<List<TResult>> ToListAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => await _executor.ToListAsync(preparedQueryCommand, @params, cancellationToken).ConfigureAwait(false);

    /// <summary>Executes the command and buffers all rows into a list.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command.</param>
    /// <returns>The materialised rows; empty when the query yields none.</returns>
    public List<TResult> ToList<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
        => _executor.ToList(preparedQueryCommand, @params);

    // ConvertScalar lives in QueryExecutor together with the scalar terminal (the execution axis).
    /// <summary>Executes the command and converts the first column of the first row to <typeparamref name="TResult"/>.</summary>
    /// <typeparam name="TResult">The scalar type to convert the value to.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command.</param>
    /// <param name="throwIfNull">When <see langword="true"/>, throws <see cref="InvalidOperationException"/> when the result is null or <c>DBNull</c>; otherwise returns <see langword="default"/>.</param>
    /// <returns>The scalar value, or <see langword="default"/> when the result is null and <paramref name="throwIfNull"/> is <see langword="false"/>.</returns>
    public TResult? ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params, bool throwIfNull)
        => _executor.ExecuteScalar(preparedQueryCommand, @params, throwIfNull);

    /// <summary>Executes the command and converts the first column of the first row to <typeparamref name="TResult"/>.</summary>
    /// <typeparam name="TResult">The scalar type to convert the value to.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command, or <see langword="null"/>.</param>
    /// <param name="throwIfNull">When <see langword="true"/>, throws <see cref="InvalidOperationException"/> when the result is null or <c>DBNull</c>; otherwise returns <see langword="default"/>.</param>
    /// <param name="cancellationToken">Token used to cancel execution.</param>
    /// <returns>The scalar value, or <see langword="default"/> when the result is null and <paramref name="throwIfNull"/> is <see langword="false"/>.</returns>
    public async Task<TResult?> ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, bool throwIfNull, CancellationToken cancellationToken)
        => await _executor.ExecuteScalar(preparedQueryCommand, @params, throwIfNull, cancellationToken).ConfigureAwait(false);

    /// <summary>Creates a synchronous enumerator over the prepared command's rows.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to enumerate.</param>
    /// <param name="params">Positional parameters bound to the command, or <see langword="null"/>.</param>
    /// <returns>A synchronous enumerator over the result rows.</returns>
    public IEnumerator<TResult> CreateEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params)
        => _executor.CreateEnumerator(preparedQueryCommand, @params);

    /// <summary>Returns the first row of the result.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command.</param>
    /// <returns>The first projected row.</returns>
    /// <exception cref="InvalidOperationException">The result set is empty.</exception>
    public TResult First<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
        => _executor.First(preparedQueryCommand, @params);

    /// <summary>Asynchronously returns the first row of the result.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token used to cancel execution.</param>
    /// <returns>The first projected row.</returns>
    /// <exception cref="InvalidOperationException">The result set is empty.</exception>
    public async Task<TResult> FirstAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => await _executor.FirstAsync(preparedQueryCommand, @params, cancellationToken).ConfigureAwait(false);

    /// <summary>Returns the first row of the result, or <see langword="default"/> when the result set is empty.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command.</param>
    /// <returns>The first projected row, or <see langword="default"/> when there is none.</returns>
    public TResult? FirstOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
        => _executor.FirstOrDefault(preparedQueryCommand, @params);

    /// <summary>Asynchronously returns the first row of the result, or <see langword="default"/> when the result set is empty.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token used to cancel execution.</param>
    /// <returns>The first projected row, or <see langword="default"/> when there is none.</returns>
    public async Task<TResult?> FirstOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => await _executor.FirstOrDefaultAsync(preparedQueryCommand, @params, cancellationToken).ConfigureAwait(false);

    /// <summary>Returns the only row of the result.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command.</param>
    /// <returns>The single projected row.</returns>
    /// <exception cref="InvalidOperationException">The result set is empty or contains more than one row.</exception>
    public TResult Single<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
        => _executor.Single(preparedQueryCommand, @params);

    /// <summary>Asynchronously returns the only row of the result.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token used to cancel execution.</param>
    /// <returns>The single projected row.</returns>
    /// <exception cref="InvalidOperationException">The result set is empty or contains more than one row.</exception>
    public async Task<TResult> SingleAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => await _executor.SingleAsync(preparedQueryCommand, @params, cancellationToken).ConfigureAwait(false);

    /// <summary>Returns the only row of the result, or <see langword="default"/> when the result set is empty.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command.</param>
    /// <returns>The single projected row, or <see langword="default"/> when there is none.</returns>
    /// <exception cref="InvalidOperationException">The result set contains more than one row.</exception>
    public TResult? SingleOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
        => _executor.SingleOrDefault(preparedQueryCommand, @params);

    /// <summary>Asynchronously returns the only row of the result, or <see langword="default"/> when the result set is empty.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token used to cancel execution.</param>
    /// <returns>The single projected row, or <see langword="default"/> when there is none.</returns>
    /// <exception cref="InvalidOperationException">The result set contains more than one row.</exception>
    public async Task<TResult?> SingleOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => await _executor.SingleOrDefaultAsync(preparedQueryCommand, @params, cancellationToken).ConfigureAwait(false);

    /// <summary>Clears all cached query plans held by this context.</summary>
    public void PurgeQueryCache() => _queryCache.PurgeQueryCache();
}
