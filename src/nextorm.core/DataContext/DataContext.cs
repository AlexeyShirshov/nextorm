using Microsoft.Extensions.Logging;
using System.Collections;
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
            optionsBuilder.MultilineBatchSql,
            optionsBuilder.CommandTimeout);

        QueryCacheEnabled = optionsBuilder.QueryCacheEnabled;

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
        _createParamSimple = CreateParam;
        _createProcedureParam = CreateProcedureParameter;

        // The execution axis receives everything through its constructor (connection role, parameter
        // factory, logging config, disposal state) so it never sees the concrete context.
        _executor = new QueryExecutor(
            this,
            _connectionManager,
            _createParam,
            _createParamSimple,
            _createProcedureParam,
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
            new LoggingOptions(_environment.Logger, _environment.ResultSetEnumeratorLogger, _environment.LogSensitiveData, QueryFilterLogger: _environment.QueryFilterLogger),
            _interceptors);
    }

    private readonly Func<DbCommand, string, object?, DbParameter> _createParam;
    private readonly Func<string, object?, DbParameter> _createParamSimple;
    private readonly Func<DbCommand, ProcedureParameter, DbParameter> _createProcedureParam;

    /// <summary>
    /// Whether prepared plans may be stored in and reused from the plan cache. Initialized from
    /// <c>DataContextBuilder.UseQueryCache</c> (default <see langword="true"/>); setting it to
    /// <see langword="false"/> makes every preparation pass <c>storeInCache: false</c> without touching
    /// the sticky <see cref="QueryCommand.Cache"/> flag on a shared command.
    /// </summary>
    public bool QueryCacheEnabled { get; set; } = true;

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
    /// <summary>
    /// The context-wide default command timeout in seconds (set with
    /// <c>DataContextBuilder.UseCommandTimeout</c>), or <see langword="null"/> when no timeout is
    /// configured and the provider default applies. A command can override it with
    /// <c>WithCommandTimeout</c>.
    /// </summary>
    public int? CommandTimeout => _environment.CommandTimeout;
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
    /// <param name="storeInCache">When <see langword="true"/> (and <see cref="QueryCacheEnabled"/> is set), stores the prepared command in the plan cache.</param>
    /// <param name="cancellationToken">Token used to cancel preparation.</param>
    /// <returns>The prepared command, ready to execute.</returns>
    public IPreparedQueryCommand<TResult> GetPreparedQueryCommand<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, CancellationToken cancellationToken)
        => GetPreparedQueryCommand(queryCommand, createEnumerator, storeInCache, streamingRows: false, cancellationToken);

    /// <summary>
    /// Prepares the command for the <c>ToAsyncEnumerable</c> terminal. This is the only preparation
    /// entry point allowed to promote a row projection containing a live <c>Stream</c>/<c>TextReader</c>
    /// member to sequential access; every other terminal keeps the buffered path and rejects the shape.
    /// </summary>
    internal IPreparedQueryCommand<TResult> GetStreamingRowsQueryCommand<TResult>(QueryCommand<TResult> queryCommand, CancellationToken cancellationToken)
        => GetPreparedQueryCommand(queryCommand, createEnumerator: true, storeInCache: true, streamingRows: true, cancellationToken);

    private IPreparedQueryCommand<TResult> GetPreparedQueryCommand<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, bool streamingRows, CancellationToken cancellationToken)
    {
        // The check is allocation-free and false for every ordinary query, so the common preparation
        // path only pays a few branches.
        if (queryCommand.HasTemporaryTableSource())
        {
            var tempTables = new List<ITempTableSource>();
            queryCommand.CollectTempTableSources(tempTables);
            return GetPreparedTemporaryTableCommand(queryCommand, tempTables, createEnumerator, cancellationToken);
        }

        return _planner.GetPreparedQueryCommand(queryCommand, createEnumerator, storeInCache && QueryCacheEnabled, false, streamingRows, cancellationToken);
    }

    /// <summary>
    /// Streams a query's projected rows as JSON to a caller-owned stream, without a row mapper. The
    /// command is prepared once with the no-mapper flag (a live <c>DocumentMode</c> clone) and
    /// the shape plan and row writer are built per call; no writer cache is used.
    /// </summary>
    /// <typeparam name="TResult">The projected result type; it is never materialized on this path.</typeparam>
    /// <param name="queryCommand">The command whose projection drives the JSON shape.</param>
    /// <param name="output">The caller-owned destination stream; it is never closed.</param>
    /// <param name="options">The requested JSON container and shaping options.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token observed while reading rows and writing to the stream.</param>
    internal void WriteJson<TResult>(QueryCommand<TResult> queryCommand, Stream output, JsonStreamOptions options, object[]? @params, CancellationToken cancellationToken)
    {
        var (prepared, rowWriter) = PrepareJsonStream(queryCommand, options, cancellationToken);
        _executor.WriteJson(prepared, rowWriter, output, options, @params is null ? ReadOnlySpan<object?>.Empty : @params);
    }

    /// <summary>Asynchronously streams a query's projected rows as JSON to a caller-owned stream; see <see cref="WriteJson{TResult}"/>.</summary>
    /// <typeparam name="TResult">The projected result type; it is never materialized on this path.</typeparam>
    /// <param name="queryCommand">The command whose projection drives the JSON shape.</param>
    /// <param name="output">The caller-owned destination stream; it is never closed.</param>
    /// <param name="options">The requested JSON container and shaping options.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token observed while reading rows and writing to the stream.</param>
    /// <returns>A task that completes when the whole document has been written.</returns>
    internal Task WriteJsonAsync<TResult>(QueryCommand<TResult> queryCommand, Stream output, JsonStreamOptions options, object[]? @params, CancellationToken cancellationToken)
    {
        var (prepared, rowWriter) = PrepareJsonStream(queryCommand, options, cancellationToken);
        return _executor.WriteJsonAsync(prepared, rowWriter, output, options, @params, cancellationToken);
    }

    // A JSON stream needs the SQL rendered and the command attached, but no Func<IDataRecord,TResult>:
    // DocumentMode is the planner's existing no-mapper flag. It is set on a live clone so the caller's
    // command is never mutated (DocumentMode is part of the plan key and the sticky-state hazard is the
    // same as Cache). The clone's populated SelectList/OneColumn feed the shape plan.
    private (DbPreparedQueryCommand<TResult> Prepared, JsonRowWriter RowWriter) PrepareJsonStream<TResult>(QueryCommand<TResult> queryCommand, JsonStreamOptions options, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(DataContext));
        ArgumentNullException.ThrowIfNull(options);

        // ResetPreparation clears the prepared pieces, including the source. A temporary-table source
        // cannot be reconstructed from the entity metadata (there is none for TableAlias), so preserve
        // it explicitly; otherwise the temp marker is lost and the batch guard never fires.
        var tempSource = queryCommand.From?.TempTable is not null ? queryCommand.From : null;

        var cmd = (QueryCommand<TResult>)queryCommand.Clone();
        cmd.DocumentMode = true;
        cmd.ResetPreparation();
        if (tempSource is not null)
            cmd.From = tempSource;

        // Route through the context's preparation wrapper (not the planner directly) so a lazy
        // temporary-table source is still detected; the executor then rejects that shape, since a
        // batch read cannot be streamed through a single reader.
        var prepared = (DbPreparedQueryCommand<TResult>)GetPreparedQueryCommand(
            cmd, createEnumerator: false, storeInCache: false, streamingRows: false, cancellationToken);

        var plan = JsonShapePlan.Build(cmd.SelectList, cmd.OneColumn, options);
        return (prepared, JsonRowWriterFactory.Build(plan));
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

    internal CommandReaderOwner OpenLobReader<TResult>(QueryCommand<TResult> queryCommand, ReadOnlySpan<object?> @params, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var prepared = PrepareLobCommand(queryCommand, cancellationToken);
        return _executor.OpenLobReader(prepared, @params);
    }

    internal async Task<CommandReaderOwner> OpenLobReaderAsync<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var prepared = PrepareLobCommand(queryCommand, cancellationToken);
        return await _executor.OpenLobReaderAsync(prepared, @params, cancellationToken).ConfigureAwait(false);
    }

    private DbPreparedQueryCommand<TResult> PrepareLobCommand<TResult>(QueryCommand<TResult> queryCommand, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(DataContext));

        if (!Dialect.SupportsSequentialAccess)
            throw new NotSupportedException(
                $"Streaming LOB terminals (ToStream/ToTextReader) are not supported by the {Dialect.GetType().Name} provider; they require sequential-access support (PostgreSQL, SQL Server or SQLite).");

        return (DbPreparedQueryCommand<TResult>)_planner.GetPreparedQueryCommand(queryCommand, false, false, true, cancellationToken);
    }

    // The plain (non-LOB) multi-column reader seam: unlike PrepareLobCommand it demands neither
    // sequential access nor a locator column, so it runs on every relational provider (including
    // SQLite, whose rowid locator would otherwise be appended). The plan is prepared per call with
    // storeInCache: false and without touching QueryCommand.Cache, so a shared command (for example
    // the context-cached AnyCommand) is never mutated and no plan is promoted into the plan cache.
    internal CommandReaderOwner OpenResultReader<TResult>(QueryCommand<TResult> queryCommand, ReadOnlySpan<object?> @params, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var prepared = PrepareResultCommand(queryCommand, cancellationToken);
        return _executor.OpenResultReader(prepared, @params);
    }

    internal async Task<CommandReaderOwner> OpenResultReaderAsync<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var prepared = PrepareResultCommand(queryCommand, cancellationToken);
        return await _executor.OpenResultReaderAsync(prepared, @params, cancellationToken).ConfigureAwait(false);
    }

    private DbPreparedQueryCommand<TResult> PrepareResultCommand<TResult>(QueryCommand<TResult> queryCommand, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(DataContext));

        // The plain multi-column reader cannot run a lazy temporary-table source: the source is not a
        // single statement but a batch (DROP + CREATE TEMPORARY TABLE AS + read) that must share one
        // session, and it is consumed through the batch-aware enumerable/scalar terminals. Preparing the
        // read here would strip the batch and execute a lone SELECT against a table that was never
        // created, surfacing the provider's raw "table does not exist" error. Fail closed instead, before
        // the CSV terminal writes the header.
        if (queryCommand.HasTemporaryTableSource())
            throw new NotSupportedException(
                "The CSV terminal (WriteCsv/WriteCsvAsync) does not support a query that reads a lazy temporary table (AsTempTable): the DROP + CREATE TABLE + read batch it requires cannot be streamed as CSV. Materialise the query first (for example ToList) and write the rows yourself.");

        return (DbPreparedQueryCommand<TResult>)_planner.GetPreparedQueryCommand(
            queryCommand,
            createEnumerator: false,
            storeInCache: false,
            sequentialAccess: false,
            streamingRowsRequested: false,
            cancellationToken);
    }

    internal bool IsDisposed => _disposed;

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
    /// Creates a provider-specific parameter from the <paramref name="command"/> the parameter is
    /// bound to. The default delegates to <see cref="CreateParam(string, object?)"/>, preserving every
    /// provider's current behaviour; a provider whose ADO driver rejects parameters minted by another
    /// driver (for example MySQL with <c>MySql.Data</c> vs <c>MySqlConnector</c>) overrides this to
    /// call <c>command.CreateParameter()</c>.
    /// </summary>
    /// <param name="command">The executing command the parameter will be added to.</param>
    /// <param name="name">The parameter name, without the provider's prefix.</param>
    /// <param name="value">The parameter value, or <see langword="null"/>.</param>
    /// <returns>A new database parameter.</returns>
    protected internal virtual DbParameter CreateParam(DbCommand command, string name, object? value)
        => CreateParam(name, value);

    /// <summary>
    /// Creates a provider-specific parameter from a <see cref="ProcedureParameter"/> descriptor: it
    /// delegates name/value to <see cref="CreateParam(string, object?)"/> and then applies the
    /// descriptor's <see cref="ParameterDirection"/>, <see cref="System.Data.DbType"/> and size.
    /// Providers with extra parameter options (for example SQL Server structured parameters) override
    /// this.
    /// </summary>
    /// <param name="parameter">The parameter descriptor.</param>
    /// <returns>A new database parameter configured from <paramref name="parameter"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="parameter"/>.<see cref="ProcedureParameter.Name"/> is <see langword="null"/>, empty or whitespace.</exception>
    /// <exception cref="NotSupportedException">The descriptor requests a structured parameter type the provider does not support.</exception>
    protected internal virtual DbParameter CreateProcedureParameter(ProcedureParameter parameter)
    {
        // Validate the descriptor before any provider work, so a malformed parameter fails with a
        // clear message and a fresh per-call command is disposed by the existing leak-safe path.
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter.Name);

        // Reject an unsupported structured/table parameter before touching the provider, so no parameter
        // is allocated (and no provider-specific side effect runs) for a descriptor we cannot honour.
        if (parameter.Value is TableParameterValue || parameter.TypeName is not null)
        {
            if (!Dialect.SupportsTableValuedParameters)
                throw new NotSupportedException(
                    $"{GetType().Name} does not support table-valued parameters. "
                    + "Use SQL Server (native user-defined table type), or PostgreSQL, MySQL/MariaDB, SQLite or ClickHouse (array/JSON emulation).");

            // A provider that advertises the capability must override CreateProcedureParameter; reaching
            // the base means its override is missing the table-parameter branch.
            throw new NotSupportedException(
                $"{GetType().Name} advertises table-valued parameters but does not implement their binding.");
        }

        var dbParameter = CreateParam(parameter.Name, parameter.Value);

        return ApplyProcedureParameterMetadata(dbParameter, parameter);
    }

    /// <summary>
    /// Applies a descriptor's non-default <see cref="ParameterDirection"/>, <see cref="System.Data.DbType"/>
    /// and size to an already-minted provider parameter. Shared by the command-unaware
    /// <see cref="CreateProcedureParameter(ProcedureParameter)"/> and the provider command-aware override
    /// (<c>MySqlDataContext</c>) so both procedure paths apply the same metadata rule.
    /// </summary>
    /// <param name="dbParameter">The provider parameter to configure.</param>
    /// <param name="parameter">The descriptor carrying the metadata.</param>
    /// <returns><paramref name="dbParameter"/>, configured from <paramref name="parameter"/>.</returns>
    protected static DbParameter ApplyProcedureParameterMetadata(DbParameter dbParameter, ProcedureParameter parameter)
    {
        if (parameter.Direction != ParameterDirection.Input)
            dbParameter.Direction = parameter.Direction;

        if (parameter.DbType is { } dbType)
            dbParameter.DbType = dbType;

        if (parameter.Size is { } size)
            dbParameter.Size = size;

        return dbParameter;
    }

    /// <summary>
    /// Command-aware variant of <see cref="CreateProcedureParameter(ProcedureParameter)"/> used by the
    /// raw/procedure execution paths: it receives the command the parameter will be added to, so a
    /// provider whose ADO driver rejects a parameter minted by another driver can mint it through
    /// <c>command.CreateParameter()</c>. The default delegates to the command-unaware overload,
    /// preserving every provider's existing behavior; <c>MySqlDataContext</c> overrides it.
    /// </summary>
    /// <param name="command">The executing command the parameter will be added to.</param>
    /// <param name="parameter">The parameter descriptor.</param>
    /// <returns>A new database parameter configured from <paramref name="parameter"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="parameter"/>.<see cref="ProcedureParameter.Name"/> is <see langword="null"/>, empty or whitespace.</exception>
    /// <exception cref="NotSupportedException">The descriptor requests a structured parameter type the provider does not support.</exception>
    protected internal virtual DbParameter CreateProcedureParameter(DbCommand command, ProcedureParameter parameter)
        => CreateProcedureParameter(parameter);

    /// <summary>
    /// Maps a projected column to a reader accessor. Providers whose reader does not widen CLR
    /// types (SqlClient throws when a typed getter does not match the field type, for example an
    /// int column projected as long) can override this to read the value and convert it.
    /// </summary>
    public virtual Expression MapColumnExpression(SelectExpression column, Expression param) => RowMapperFactory.MapColumn(column, param, Dialect.SupportsNativeDuration, Dialect);

    /// <summary>
    /// Maps a projected column when the reader's actual field (storage) type is known. The CSV terminal
    /// calls this after the reader is open, passing <c>reader.GetFieldType(column.Index)</c>, so a
    /// provider can pick a typed getter for the storage type and convert to the projected type without
    /// going through <see cref="IDataRecord.GetValue"/>. It is a provider extension point: <c>protected</c>
    /// so it does not widen the context's public surface, and overridden by providers (for example
    /// <c>SqlServerDataContext</c>) whose reader does not widen numerics. The default ignores
    /// <paramref name="storageType"/> and delegates to <see cref="MapColumnExpression"/> so buffered
    /// materialization is unchanged.
    /// </summary>
    /// <param name="column">The projected column being read.</param>
    /// <param name="record">The data-reader expression the accessor is built from.</param>
    /// <param name="storageType">The reader's CLR field type for the column's ordinal.</param>
    /// <returns>An expression that reads (and, when needed, converts) the column value.</returns>
    protected virtual Expression MapTypedColumnExpression(SelectExpression column, Expression record, Type storageType)
        => MapColumnExpression(column, record);

    /// <summary>
    /// True when <see cref="MapTypedColumnExpression"/> will read <paramref name="column"/> through a
    /// typed getter even though <see cref="MapColumnExpression"/> (which has no storage type to work
    /// with) would box through <see cref="IDataRecord.GetValue"/>/<c>Convert.ChangeType(object)</c>. The
    /// CSV terminal calls this before the query is executed, when only the static projection is known,
    /// so it can reject a column that will box on every row without falsely rejecting a provider whose
    /// typed hook is storage-driven (SQL Server numeric widening). The default is <see langword="false"/>.
    /// </summary>
    /// <param name="column">The projected column about to be validated.</param>
    /// <returns><see langword="true"/> when the typed hook can bind the column box-free.</returns>
    protected virtual bool SupportsTypedColumnMapping(SelectExpression column) => false;

    // The CSV terminal lives outside the context type hierarchy, so it reaches the protected hook through
    // this internal seam, which still virtual-dispatches to the provider override.
    internal bool SupportsTypedColumn(SelectExpression column) => SupportsTypedColumnMapping(column);

    // The CSV terminal lives outside the context type hierarchy, so it reaches the protected hook through
    // this internal seam, which still virtual-dispatches to the provider override.
    internal Expression MapTypedColumn(SelectExpression column, Expression record, Type storageType)
        => MapTypedColumnExpression(column, record, storageType);

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

    // Raw command execution (IRawCommandExecutor). Public so a concrete context exposes the entry
    // point directly; the role interface carries a throwing default for providers without raw support.
    // The returned ProcedureResult owns the per-call command and reader; the connection stays owned by
    // the context.
    /// <summary>
    /// Executes <paramref name="sql"/> with <paramref name="parameters"/> and returns a
    /// <see cref="ProcedureResult"/> owning the reader and command.
    /// </summary>
    /// <param name="sql">The command text to execute.</param>
    /// <param name="parameters">The parameters referenced by <paramref name="sql"/>.</param>
    /// <returns>The result, which must be disposed to release the reader and command.</returns>
    /// <remarks>
    /// <para>
    /// <b>SQL injection.</b> <paramref name="sql"/> is sent to the provider verbatim and never
    /// parameterised by the planner. Never concatenate untrusted input into it; pass values through
    /// <see cref="ProcedureParameter"/> so the provider binds them.
    /// </para>
    /// <para>
    /// <b>Open reader.</b> The returned <see cref="ProcedureResult"/> holds the command and its reader
    /// open until disposed. On SQL Server without MARS, an open reader blocks every other command on the
    /// same connection — dispose the result (and read/close it) before issuing another command on the
    /// context. On providers that support multiple active result sets this is provider-dependent.
    /// </para>
    /// <para>
    /// <b>Return values.</b> A <see cref="ParameterDirection.ReturnValue"/> parameter is populated only
    /// for a stored-procedure command type issued through
    /// <see cref="ExecuteProcedure(string, IReadOnlyList{ProcedureParameter})"/>, and only where the
    /// provider has a return status (SQL Server). Text commands like <c>EXEC</c> are provider-dependent
    /// and typically leave it unset; capture a value with an
    /// <see cref="ParameterDirection.Output"/> parameter instead.
    /// </para>
    /// <para>
    /// <b>Mapping.</b> <c>Read&lt;T&gt;()</c> accepts a scalar or a mapped entity. A mapped entity's
    /// metadata is resolved from <see cref="DataContextCache.Metadata"/> or, when absent, registered on
    /// demand with the default mapping (attributes and auto-derived names, the context's naming
    /// convention applied). A scalar read of SQL <c>NULL</c> returns <c>default</c>; use a nullable
    /// <c>T</c> (for example <c>int?</c>) to observe a <c>NULL</c>.
    /// </para>
    /// </remarks>
    public ProcedureResult ExecuteRaw(string sql, params IReadOnlyList<ProcedureParameter> parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(parameters);

        var owner = _executor.OpenReader(sql, parameters, CommandType.Text);
        return new ProcedureResult(this, owner);
    }

    /// <summary>
    /// Asynchronously executes <paramref name="sql"/> with <paramref name="parameters"/> and returns a
    /// <see cref="ProcedureResult"/> owning the reader and command. See
    /// <see cref="ExecuteRaw(string, IReadOnlyList{ProcedureParameter})"/> for the SQL-injection, open
    /// reader (MARS) and return-value notes.
    /// </summary>
    /// <param name="sql">The command text to execute.</param>
    /// <param name="parameters">The parameters referenced by <paramref name="sql"/>.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the result, which must be disposed to release the reader and command.</returns>
    public async Task<ProcedureResult> ExecuteRawAsync(string sql, IReadOnlyList<ProcedureParameter> parameters, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(parameters);

        var owner = await _executor.OpenReaderAsync(sql, parameters, CommandType.Text, cancellationToken).ConfigureAwait(false);
        return new ProcedureResult(this, owner);
    }

    /// <summary>
    /// Asynchronously executes <paramref name="sql"/> with <paramref name="parameters"/>, using
    /// <see cref="CancellationToken.None"/>. Convenience overload of
    /// <see cref="ExecuteRawAsync(string, IReadOnlyList{ProcedureParameter}, CancellationToken)"/> for
    /// callers whose static type is the concrete context.
    /// </summary>
    /// <param name="sql">The command text to execute.</param>
    /// <param name="parameters">The parameters referenced by <paramref name="sql"/>.</param>
    /// <returns>A task producing the result, which must be disposed to release the reader and command.</returns>
    public Task<ProcedureResult> ExecuteRawAsync(string sql, params IReadOnlyList<ProcedureParameter> parameters)
        => ExecuteRawAsync(sql, parameters, CancellationToken.None);

    /// <summary>
    /// Executes the stored procedure <paramref name="name"/> with <paramref name="parameters"/> and
    /// returns a <see cref="ProcedureResult"/> owning the reader and command. The command is sent with
    /// <see cref="CommandType.StoredProcedure"/>.
    /// </summary>
    /// <param name="name">The procedure name, passed to the provider as-is.</param>
    /// <param name="parameters">The procedure parameters (input, output, input/output and return value).</param>
    /// <returns>The result, which must be disposed to release the reader and command.</returns>
    /// <remarks>
    /// <para>
    /// <b>Name.</b> <paramref name="name"/> is not escaped, quoted or parameterised — it is set as the
    /// command's text. Never pass untrusted input as the procedure name. Parameter names are supplied
    /// without the provider's prefix (for example <c>@</c> for SQL Server).
    /// </para>
    /// <para>
    /// <b>PostgreSQL.</b> Npgsql maps <see cref="CommandType.StoredProcedure"/> to <c>CALL name(...)</c>,
    /// which invokes a <em>procedure</em> (PostgreSQL 11+), not a function. Call a function through
    /// <see cref="ExecuteRaw(string, IReadOnlyList{ProcedureParameter})"/> instead (for example
    /// <c>select * from f(@a)</c>). An <c>OUT</c>/<c>INOUT</c> parameter's value is read through
    /// <see cref="ProcedureResult.OutputParameters"/>.
    /// </para>
    /// <para>
    /// <b>Return values.</b> <see cref="ProcedureResult.ReturnValue"/> is populated only where the
    /// provider supports a procedure return status (SQL Server, through a
    /// <see cref="ParameterDirection.ReturnValue"/> parameter). MySQL and PostgreSQL have no such
    /// return value.
    /// </para>
    /// <para>
    /// <b>Capability.</b> SQL Server, PostgreSQL and MySQL/MariaDB support this call; SQLite,
    /// ClickHouse and the in-memory context throw <see cref="NotSupportedException"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="name"/> is <see langword="null"/>, empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The provider has no stored procedures (<c>ISqlDialect.SupportsStoredProcedures</c> is <c>false</c>).</exception>
    public ProcedureResult ExecuteProcedure(string name, params IReadOnlyList<ProcedureParameter> parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(parameters);
        ThrowIfStoredProceduresUnsupported();

        var owner = _executor.OpenReader(name, parameters, CommandType.StoredProcedure);
        return new ProcedureResult(this, owner);
    }

    /// <summary>
    /// Asynchronously executes the stored procedure <paramref name="name"/> with
    /// <paramref name="parameters"/> and returns a <see cref="ProcedureResult"/> owning the reader and
    /// command. See <see cref="ExecuteProcedure(string, IReadOnlyList{ProcedureParameter})"/> for the
    /// name, PostgreSQL and return-value notes.
    /// </summary>
    /// <param name="name">The procedure name, passed to the provider as-is.</param>
    /// <param name="parameters">The procedure parameters (input, output, input/output and return value).</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the result, which must be disposed to release the reader and command.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is <see langword="null"/>, empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The provider has no stored procedures (<c>ISqlDialect.SupportsStoredProcedures</c> is <c>false</c>).</exception>
    public async Task<ProcedureResult> ExecuteProcedureAsync(string name, IReadOnlyList<ProcedureParameter> parameters, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(parameters);
        ThrowIfStoredProceduresUnsupported();

        var owner = await _executor.OpenReaderAsync(name, parameters, CommandType.StoredProcedure, cancellationToken).ConfigureAwait(false);
        return new ProcedureResult(this, owner);
    }

    /// <summary>
    /// Asynchronously executes the stored procedure <paramref name="name"/> with
    /// <paramref name="parameters"/>, using <see cref="CancellationToken.None"/>. Convenience overload
    /// of <see cref="ExecuteProcedureAsync(string, IReadOnlyList{ProcedureParameter}, CancellationToken)"/>
    /// for callers whose static type is the concrete context.
    /// </summary>
    /// <param name="name">The procedure name, passed to the provider as-is.</param>
    /// <param name="parameters">The procedure parameters (input, output, input/output and return value).</param>
    /// <returns>A task producing the result, which must be disposed to release the reader and command.</returns>
    public Task<ProcedureResult> ExecuteProcedureAsync(string name, params IReadOnlyList<ProcedureParameter> parameters)
        => ExecuteProcedureAsync(name, parameters, CancellationToken.None);

    private void ThrowIfStoredProceduresUnsupported()
    {
        if (!Dialect.SupportsStoredProcedures)
            throw new NotSupportedException(
                $"{GetType().Name} does not support stored procedures. "
                + "Use SQL Server, PostgreSQL or MySQL/MariaDB, or run the source as a text command with ExecuteRaw.");
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
            command.NotifyEvery,
            command.BulkCopy);
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
            command.BulkCopy,
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
    /// <param name="bulkCopy">The bulk-copy flags requested by the caller; only the provider's native path can express them.</param>
    /// <returns>The number of rows written.</returns>
    /// <exception cref="NotSupportedException">The provider has no native bulk implementation.</exception>
    protected virtual int BulkInsertRows(string tableName, IReadOnlyList<string> columnNames, IReadOnlyList<IPropertyMetadata> columns, IEnumerable<object?[]> rows, int? commandTimeoutSeconds, int? maxBatchSize, Action<int>? progress, int notifyEvery, BulkCopyFlags bulkCopy)
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
    /// <param name="bulkCopy">The bulk-copy flags requested by the caller; only the provider's native path can express them.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of rows written.</returns>
    /// <exception cref="NotSupportedException">The provider has no native bulk implementation.</exception>
    protected virtual Task<int> BulkInsertRowsAsync(string tableName, IReadOnlyList<string> columnNames, IReadOnlyList<IPropertyMetadata> columns, IAsyncEnumerable<object?[]> rows, int? commandTimeoutSeconds, int? maxBatchSize, Action<int>? progress, int notifyEvery, BulkCopyFlags bulkCopy, CancellationToken cancellationToken)
        => throw new NotSupportedException($"{GetType().Name} declares ISqlDialect.SupportsBulkCopy but does not implement the native bulk path.");

    private (string Sql, List<Parameter> Parameters) BuildReturningSql(MutationCommand command)
        => command switch
        {
            InsertCommand insert => BuildInsertSql(insert),
            UpdateCommand update => BuildUpdateSql(update),
            DeleteCommand delete => BuildDeleteSql(delete),
            UpdateJoinCommand updateJoin => BuildUpdateJoinSql(updateJoin),
            DeleteJoinCommand deleteJoin => BuildDeleteJoinSql(deleteJoin),
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

        // The source's prepared condition carries the target entity's global filter (injected during
        // preparation) and, for the predicate form, the user's WHERE. It is rendered in both forms: the
        // key form ANDs it to the key equalities so the row must match the key and the filter.
        var (whereSql, _) = _planner.RenderPredicate(command.Source, provider, parameters, parameterNamePrefix);

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
            // A filtered SQL Server key upsert (SupportsMerge) is routed through the same filtered MERGE
            // renderer as the full-MERGE form: the target predicate is rendered into its ON. Every other
            // dialect has already refused in the builder's capability guard, so there is no filter here.
            IParameterProvider? keyProvider = null;
            List<Parameter>? keyAccumulator = null;
            string? keyFilterSql = null;
            if (Dialect.SupportsMerge && command.Registry is { } keyRegistry)
            {
                keyProvider = new DefaultParameterProvider();
                keyAccumulator = [];
                keyFilterSql = _planner.RenderMergeTargetFilter(command.EntityType, command.FilterScope, keyRegistry, keyProvider, keyAccumulator, QuoteIdentifiers, NamingConvention, KeywordCase);
            }

            if (command.Source is null)
                return SqlMutationBuilder.MakeMerge(Dialect, QuoteIdentifiers, NamingConvention, command, KeywordCase, keyProvider, null, null, keyAccumulator, null, null, keyFilterSql);

            var (withInsert, sourceInsert, sourceInsertParameters) = _planner.RenderSource(command.Source);
            var (insertSql, insertParameters) = SqlMutationBuilder.MakeMerge(Dialect, QuoteIdentifiers, NamingConvention, command, KeywordCase, keyProvider, sourceInsert, sourceInsertParameters, keyAccumulator, null, null, keyFilterSql);
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

        // The active target filter joins the ON predicate and every WHEN NOT MATCHED BY SOURCE arm. The
        // capability guard already refused forms that cannot carry it, and RenderMergeTargetFilter returns
        // null when the effective scope leaves no filter active (IgnoreFilters / no filter configured).
        var targetFilterSql = _planner.RenderMergeTargetFilter(command.EntityType, command.FilterScope, registry, provider, accumulator, quoteIdentifiers, namingConvention, keywordCase);

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
            var (sql, parameters) = SqlMutationBuilder.MakeMerge(Dialect, QuoteIdentifiers, NamingConvention, command, KeywordCase, provider, null, null, accumulator, matchConditionSql, branchConditions, targetFilterSql);
            return (sql, parameters);
        }

        var (withSql, sourceSql, _) = _planner.RenderSource(command.Source, provider, accumulator);
        // The source parameters already sit in the shared accumulator, so MakeMerge must not re-add them;
        // passing them again would duplicate every @pN.
        var (fullSql, fullParameters) = SqlMutationBuilder.MakeMerge(Dialect, QuoteIdentifiers, NamingConvention, command, KeywordCase, provider, sourceSql, null, accumulator, matchConditionSql, branchConditions, targetFilterSql);
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

        if (steps.Count == 0)
            throw new InvalidOperationException("A batch must contain at least one statement.");

        var firstResult = -1;
        for (var i = 0; i < steps.Count; i++)
        {
            if (steps[i].Query is null)
            {
                if (firstResult >= 0)
                    throw new InvalidOperationException(
                        "A batch's result-bearing queries must be the trailing statements; a side-effecting statement cannot follow a result-bearing query.");
            }
            else if (firstResult < 0)
            {
                firstResult = i;
            }
        }

        if (firstResult < 0)
            throw new InvalidOperationException("A batch must end with at least one result-bearing query.");

        var provider = new DefaultParameterProvider();
        var accumulator = new List<Parameter>();
        var statements = new List<BatchStatement>(steps.Count);
        var results = new List<BatchResultSpec>(steps.Count - firstResult);

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
                results.Add(new BatchResultSpec(step.Query!, sql));
            }

            var count = accumulator.Count - start;
            statements.Add(new BatchStatement(sql, SliceStatementParameters(accumulator, start, count)));
        }

        return new BatchPlan(statements, results, Dialect.BatchUsesJoinedCommand, MultilineBatchSql);
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
        => ((IBatchExecutor)this).BuildBatchMapper<TResult>(plan, 0);

    Func<IDataRecord, TResult> IBatchExecutor.BuildBatchMapper<TResult>(BatchPlan plan, int resultIndex)
    {
        var spec = plan.Results[resultIndex];
        if (spec.Query is not QueryCommand<TResult> query)
            throw new InvalidOperationException(
                $"The result set at index {resultIndex} projects '{spec.Query.ResultType?.Name ?? "unknown"}', not '{typeof(TResult).Name}'.");

        return RowMapperFactory.GetOrBuild(query, spec.Sql, GetType(), Logger, MapColumnExpression);
    }

    List<TResult> IBatchExecutor.ExecuteBatch<TResult>(BatchPlan plan, Func<IDataRecord, TResult> mapper)
        => _executor.RunBatch(plan, mapper);

    Task<List<TResult>> IBatchExecutor.ExecuteBatchAsync<TResult>(BatchPlan plan, Func<IDataRecord, TResult> mapper, CancellationToken cancellationToken)
        => _executor.RunBatchAsync(plan, mapper, cancellationToken);

    IAsyncEnumerable<TResult> IBatchExecutor.StreamBatch<TResult>(BatchPlan plan, Func<IDataRecord, TResult> mapper, CancellationToken cancellationToken)
        => _executor.RunBatchStream(plan, mapper, cancellationToken);

    BatchResult IBatchExecutor.ExecuteBatchResults(BatchPlan plan, IReadOnlyList<IBatchResultMaterializer> resultMaterializers)
        => _executor.RunBatchMultiple(plan, resultMaterializers);

    Task<BatchResult> IBatchExecutor.ExecuteBatchResultsAsync(BatchPlan plan, IReadOnlyList<IBatchResultMaterializer> resultMaterializers, CancellationToken cancellationToken)
        => _executor.RunBatchMultipleAsync(plan, resultMaterializers, cancellationToken);

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
