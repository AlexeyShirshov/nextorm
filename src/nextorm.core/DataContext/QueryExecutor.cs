using Microsoft.Extensions.Logging;
using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Execution axis of a database-backed context: turns a prepared command into a <see cref="DbCommand"/>,
/// runs it and materialises the result. Extracted out of <c>DataContext</c> so the context no longer owns
/// the execution algorithm (SRP). Everything it needs arrives as a constructor dependency — the
/// connection role, the parameter factory, the logging configuration and the disposal state — so it
/// never sees the concrete context (DIP).
/// </summary>
internal sealed class QueryExecutor : IQueryExecutor, IRowReaderFactory
{
    private readonly IDataContext _context;
    private readonly IConnectionManager _connectionManager;
    private readonly Func<DbCommand, string, object?, DbParameter> _createParam;
    private readonly Func<string, object?, DbParameter> _createParamSimple;
    private readonly Func<DbCommand, ProcedureParameter, DbParameter> _createProcedureParam;
    private readonly ILogger? _logger;
    private readonly bool _logParams;
    private readonly bool _logSensitiveData;
    private readonly Func<bool> _isDisposed;
    private readonly Func<DbTransaction?> _currentTransaction;
    private readonly InterceptorHooks _interceptors;
    private readonly BatchRunner _batchRunner;

    internal QueryExecutor(
        IDataContext context,
        IConnectionManager connectionManager,
        Func<DbCommand, string, object?, DbParameter> createParam,
        Func<string, object?, DbParameter> createParamSimple,
        Func<DbCommand, ProcedureParameter, DbParameter> createProcedureParam,
        LoggingOptions logging,
        Func<bool> isDisposed,
        Func<DbTransaction?> currentTransaction,
        InterceptorHooks interceptors)
    {
        _context = context;
        _connectionManager = connectionManager;
        _createParam = createParam;
        _createParamSimple = createParamSimple;
        _createProcedureParam = createProcedureParam;
        _logger = logging.Logger;
        _logParams = logging.LogParams;
        _logSensitiveData = logging.LogSensitiveData;
        _isDisposed = isDisposed;
        _currentTransaction = currentTransaction;
        _interceptors = interceptors;
        _batchRunner = new BatchRunner(connectionManager, createParam, createParamSimple, currentTransaction, isDisposed, logging, context.CommandTimeout);
    }

    // The interception helpers below are the single place the command lifecycle events are raised.
    // Each wrapper checks the interceptor count first, so with none registered the call is a branch
    // and an inlined direct execute — no delegates, closures, timestamps or async state machines.
    // The event loops themselves live on InterceptorHooks, shared with the streaming and planning paths.

    private void RaiseCommandInitialized(DbCommand command)
    {
        var interceptors = _interceptors.QueryInterceptors;
        if (interceptors.Length == 0)
            return;

        InterceptorHooks.RaiseCommandInitialized(interceptors, _context, command);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long BeginCommand(DbCommand command, IQueryInterceptor[] interceptors)
    {
        InterceptorHooks.RaiseCommandExecuting(interceptors, _context, command);
        return Stopwatch.GetTimestamp();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EndCommand(DbCommand command, IQueryInterceptor[] interceptors, long started)
        => InterceptorHooks.RaiseCommandExecuted(interceptors, _context, command, Stopwatch.GetElapsedTime(started));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FailCommand(DbCommand command, IQueryInterceptor[] interceptors, Exception exception)
        => InterceptorHooks.RaiseCommandFailed(interceptors, _context, command, exception);

    private int RunNonQuery(DbCommand command)
    {
        var interceptors = _interceptors.QueryInterceptors;
        return interceptors.Length == 0 ? command.ExecuteNonQuery() : RunNonQueryCore(command, interceptors);
    }

    private int RunNonQueryCore(DbCommand command, IQueryInterceptor[] interceptors)
    {
        var started = BeginCommand(command, interceptors);
        try
        {
            var result = command.ExecuteNonQuery();
            EndCommand(command, interceptors, started);
            return result;
        }
        catch (Exception exception)
        {
            FailCommand(command, interceptors, exception);
            throw;
        }
    }

    private Task<int> RunNonQueryAsync(DbCommand command, CancellationToken cancellationToken)
    {
        var interceptors = _interceptors.QueryInterceptors;
        return interceptors.Length == 0
            ? command.ExecuteNonQueryAsync(cancellationToken)
            : RunNonQueryCoreAsync(command, cancellationToken, interceptors);
    }

    private async Task<int> RunNonQueryCoreAsync(DbCommand command, CancellationToken cancellationToken, IQueryInterceptor[] interceptors)
    {
        var started = BeginCommand(command, interceptors);
        try
        {
            var result = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            EndCommand(command, interceptors, started);
            return result;
        }
        catch (Exception exception)
        {
            FailCommand(command, interceptors, exception);
            throw;
        }
    }

    private object? RunScalar(DbCommand command)
    {
        var interceptors = _interceptors.QueryInterceptors;
        return interceptors.Length == 0 ? command.ExecuteScalar() : RunScalarCore(command, interceptors);
    }

    private object? RunScalarCore(DbCommand command, IQueryInterceptor[] interceptors)
    {
        var started = BeginCommand(command, interceptors);
        try
        {
            var result = command.ExecuteScalar();
            EndCommand(command, interceptors, started);
            return result;
        }
        catch (Exception exception)
        {
            FailCommand(command, interceptors, exception);
            throw;
        }
    }

    private Task<object?> RunScalarAsync(DbCommand command, CancellationToken cancellationToken)
    {
        var interceptors = _interceptors.QueryInterceptors;
        return interceptors.Length == 0
            ? command.ExecuteScalarAsync(cancellationToken)
            : RunScalarCoreAsync(command, cancellationToken, interceptors);
    }

    private async Task<object?> RunScalarCoreAsync(DbCommand command, CancellationToken cancellationToken, IQueryInterceptor[] interceptors)
    {
        var started = BeginCommand(command, interceptors);
        try
        {
            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            EndCommand(command, interceptors, started);
            return result;
        }
        catch (Exception exception)
        {
            FailCommand(command, interceptors, exception);
            throw;
        }
    }

    private DbDataReader RunReader(DbCommand command, CommandBehavior behavior)
    {
        var interceptors = _interceptors.QueryInterceptors;
        return interceptors.Length == 0 ? command.ExecuteReader(behavior) : RunReaderCore(command, behavior, interceptors);
    }

    private DbDataReader RunReaderCore(DbCommand command, CommandBehavior behavior, IQueryInterceptor[] interceptors)
    {
        var started = BeginCommand(command, interceptors);
        try
        {
            var reader = command.ExecuteReader(behavior);
            EndCommand(command, interceptors, started);
            return reader;
        }
        catch (Exception exception)
        {
            FailCommand(command, interceptors, exception);
            throw;
        }
    }

    private Task<DbDataReader> RunReaderAsync(DbCommand command, CommandBehavior behavior, CancellationToken cancellationToken)
    {
        var interceptors = _interceptors.QueryInterceptors;
        return interceptors.Length == 0
            ? command.ExecuteReaderAsync(behavior, cancellationToken)
            : RunReaderCoreAsync(command, behavior, cancellationToken, interceptors);
    }

    private async Task<DbDataReader> RunReaderCoreAsync(DbCommand command, CommandBehavior behavior, CancellationToken cancellationToken, IQueryInterceptor[] interceptors)
    {
        var started = BeginCommand(command, interceptors);
        try
        {
            var reader = await command.ExecuteReaderAsync(behavior, cancellationToken).ConfigureAwait(false);
            EndCommand(command, interceptors, started);
            return reader;
        }
        catch (Exception exception)
        {
            FailCommand(command, interceptors, exception);
            throw;
        }
    }

    private void LogParams(DbCommand sqlCommand)
    {
        _logger!.LogDebug("Executing query: {sql}", sqlCommand.CommandText);

        if (_logSensitiveData)
        {
            foreach (DbParameter p in sqlCommand.Parameters)
            {
                _logger!.LogDebug("param {name} is {value}", p.ParameterName, p.Value);
            }
        }
        else if (sqlCommand.Parameters?.Count > 0)
        {
            _logger!.LogDebug("Use {method} to see param values", nameof(_logSensitiveData));
        }
    }

    private DbCommand GetDbCommand<TResult>(DbPreparedQueryCommand<TResult> compiledQuery, object[]? @params)
        => GetDbCommand(compiledQuery, @params is null ? ReadOnlySpan<object?>.Empty : @params);

    private DbCommand GetDbCommand<TResult>(DbPreparedQueryCommand<TResult> compiledQuery, ReadOnlySpan<object?> @params)
    {
        _connectionManager.EnsureConnectionOpen();
        var conn = _connectionManager.GetConnection();

        var cmd = compiledQuery.GetDbCommand(@params, _createParam, conn, _currentTransaction());

        if (_logParams) LogParams(cmd);

        return cmd;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Bug", "S2583:Conditionally executed code should be reachable", Justification = "A pooled connection may be open or closed depending on prior use, so the connection-state check is reachable on both sides; the analyzer cannot assume state across the pool.")]
    private async ValueTask<DbCommand> GetDbCommand<TResult>(DbPreparedQueryCommand<TResult> compiledQuery, object[]? @params, CancellationToken cancellationToken)
    {
        await _connectionManager.EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);
        var conn = _connectionManager.GetConnection();

        var cmd = compiledQuery.GetDbCommand(@params, _createParam, conn, _currentTransaction());

        if (_logParams) LogParams(cmd);

        return cmd;
    }

    // Per-call binding state carried by value so the command builder allocates no closure on the
    // mutation/scalar hot path. The binder delegates are static and capture nothing, so they are
    // cached once for the process.
    private readonly record struct MutationParameterState(IReadOnlyList<Parameter> Parameters, Func<DbCommand, string, object?, DbParameter> CreateParam);
    private readonly record struct ProcedureParameterState(IReadOnlyList<ProcedureParameter> Parameters, Func<DbCommand, ProcedureParameter, DbParameter> CreateParam);

    private static readonly Action<DbCommand, MutationParameterState> BindMutationParameters = static (cmd, state) =>
    {
        for (var i = 0; i < state.Parameters.Count; i++)
            cmd.Parameters.Add(state.CreateParam(cmd, state.Parameters[i].Name, state.Parameters[i].Value));
    };

    private static readonly Action<DbCommand, ProcedureParameterState> BindProcedureParameters = static (cmd, state) =>
    {
        for (var i = 0; i < state.Parameters.Count; i++)
            cmd.Parameters.Add(state.CreateParam(cmd, state.Parameters[i]));
    };

    /// <summary>
    /// Creates a fresh <see cref="DbCommand"/> for a mutation, binds its parameters and attaches it to
    /// the current connection. Unlike a prepared query command, a mutation command is not reused across
    /// executions, so the parameters are added each time.
    /// </summary>
    private DbCommand CreateMutationCommand(string sql, IReadOnlyList<Parameter> parameters)
        => CreateCommand(sql, new MutationParameterState(parameters, _createParam), BindMutationParameters);

    /// <summary>
    /// Creates a fresh <see cref="DbCommand"/> for a raw/procedure command, binding each descriptor
    /// through the provider's command-aware
    /// <see cref="DataContext.CreateProcedureParameter(DbCommand, ProcedureParameter)"/> hook.
    /// </summary>
    private DbCommand CreateRawCommand(string commandText, IReadOnlyList<ProcedureParameter> parameters, CommandType commandType)
        => CreateCommand(commandText, new ProcedureParameterState(parameters, _createProcedureParam), BindProcedureParameters, commandType);

    private DbCommand CreateCommand<TState>(string commandText, TState state, Action<DbCommand, TState> bindParameters, CommandType commandType = CommandType.Text)
    {
        _connectionManager.EnsureConnectionOpen();
        return InitCommand(_connectionManager.GetConnection(), commandText, state, bindParameters, commandType);
    }

    // Shared per-call command construction: create the command, attach the active transaction and the
    // configured timeout, bind the parameters, then raise the interceptor and logging hooks. The
    // command is disposed when any step after its creation throws, so a rejected descriptor (for
    // example an unsupported TypeName) or a throwing interceptor cannot leak it.
    private DbCommand InitCommand<TState>(DbConnection conn, string commandText, TState state, Action<DbCommand, TState> bindParameters, CommandType commandType = CommandType.Text)
    {
        var cmd = conn.CreateCommand();
        try
        {
            cmd.CommandText = commandText;

            if (commandType != CommandType.Text)
                cmd.CommandType = commandType;

            if (_currentTransaction() is { } transaction)
                cmd.Transaction = transaction;

            if (_context.CommandTimeout is int commandTimeout)
                cmd.CommandTimeout = commandTimeout;

            bindParameters(cmd, state);

            RaiseCommandInitialized(cmd);

            if (_logParams) LogParams(cmd);

            return cmd;
        }
        catch
        {
            cmd.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Executes <paramref name="commandText"/> with <paramref name="parameters"/> and returns an owner
    /// of the resulting reader and its per-call command. <paramref name="commandType"/> selects text or
    /// stored-procedure execution. The caller must dispose the owner; on a failed execution the command
    /// is disposed here and nothing leaks.
    /// </summary>
    internal CommandReaderOwner OpenReader(string commandText, IReadOnlyList<ProcedureParameter> parameters, CommandType commandType)
    {
        CheckDisposed();
        DbCommand? command = null;
        try
        {
            command = CreateRawCommand(commandText, parameters, commandType);
            var reader = RunReader(command, CommandBehavior.Default);
            var owner = new CommandReaderOwner(command, reader);
            command = null;
            return owner;
        }
        finally
        {
            command?.Dispose();
        }
    }

    /// <summary>Asynchronously executes <paramref name="commandText"/> and returns an owner of the reader and its per-call command.</summary>
    internal async Task<CommandReaderOwner> OpenReaderAsync(string commandText, IReadOnlyList<ProcedureParameter> parameters, CommandType commandType, CancellationToken cancellationToken)
    {
        CheckDisposed();
        DbCommand? command = null;
        try
        {
            await _connectionManager.EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);

            command = InitCommand(
                _connectionManager.GetConnection(),
                commandText,
                new ProcedureParameterState(parameters, _createProcedureParam),
                BindProcedureParameters,
                commandType);

            var reader = await RunReaderAsync(command, CommandBehavior.Default, cancellationToken).ConfigureAwait(false);
            var owner = new CommandReaderOwner(command, reader);
            command = null;
            return owner;
        }
        finally
        {
            command?.Dispose();
        }
    }

    /// <summary>
    /// Opens a reader for a LOB command with the command's <see cref="CommandBehavior"/> (which carries
    /// <see cref="CommandBehavior.SequentialAccess"/>) and returns an owner of the reader and the
    /// per-call command. The caller must dispose the owner.
    /// </summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="compiledQuery">The per-call LOB command to execute.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>An owner of the open reader and its command.</returns>
    internal CommandReaderOwner OpenLobReader<TResult>(DbPreparedQueryCommand<TResult> compiledQuery, ReadOnlySpan<object?> @params)
    {
        ObjectDisposedException.ThrowIf(_isDisposed(), nameof(DataContext));

        // GetDbCommand returns the planner-created command, but it opens the connection first: if that
        // throws, the command has already been minted and still has to be released here. Seeding the
        // local with the plan's command makes the finally below cover that open-failure path.
        DbCommand? command = compiledQuery.DbCommand;
        try
        {
            command = GetDbCommand(compiledQuery, @params);
            var reader = RunReader(command!, compiledQuery.Behavior);
            var owner = new CommandReaderOwner(command, reader);
            command = null;
            return owner;
        }
        finally
        {
            command?.Dispose();
        }
    }

    /// <summary>Asynchronously opens a reader for a LOB command with the command's <see cref="CommandBehavior"/>.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="compiledQuery">The per-call LOB command to execute.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <param name="cancellationToken">Cancels opening the reader.</param>
    /// <returns>A task producing an owner of the open reader and its command.</returns>
    internal async Task<CommandReaderOwner> OpenLobReaderAsync<TResult>(DbPreparedQueryCommand<TResult> compiledQuery, object[]? @params, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed(), nameof(DataContext));

        // See the sync overload: the assignment only happens on success, so the finally still releases
        // the planner-created command when the connection fails to open.
        DbCommand? command = compiledQuery.DbCommand;
        try
        {
            command = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
            var reader = await RunReaderAsync(command!, compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
            var owner = new CommandReaderOwner(command, reader);
            command = null;
            return owner;
        }
        finally
        {
            command?.Dispose();
        }
    }

    /// <summary>
    /// Opens a plain multi-column reader for a prepared command using the command's own
    /// <see cref="CommandBehavior"/> (the buffered default for a non-LOB command): no sequential
    /// access and no locator column are required. Returns an owner of the reader and the per-call
    /// command; the caller must dispose it.
    /// </summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="compiledQuery">The per-call command to execute.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>An owner of the open reader and its command.</returns>
    internal CommandReaderOwner OpenResultReader<TResult>(DbPreparedQueryCommand<TResult> compiledQuery, ReadOnlySpan<object?> @params)
    {
        ObjectDisposedException.ThrowIf(_isDisposed(), nameof(DataContext));

        // See OpenLobReader: seeding the local with the plan's command lets the finally dispose it when
        // GetDbCommand throws while opening the connection.
        DbCommand? command = compiledQuery.DbCommand;
        try
        {
            command = GetDbCommand(compiledQuery, @params);
            var reader = RunReader(command!, compiledQuery.Behavior);
            var owner = new CommandReaderOwner(command, reader);
            command = null;
            return owner;
        }
        finally
        {
            command?.Dispose();
        }
    }

    /// <summary>Asynchronously opens a plain multi-column reader for a prepared command with the command's own <see cref="CommandBehavior"/>.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="compiledQuery">The per-call command to execute.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <param name="cancellationToken">Cancels opening the reader.</param>
    /// <returns>A task producing an owner of the open reader and its command.</returns>
    internal async Task<CommandReaderOwner> OpenResultReaderAsync<TResult>(DbPreparedQueryCommand<TResult> compiledQuery, object[]? @params, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed(), nameof(DataContext));

        // See the sync overload: the assignment only happens on success, so the finally still releases
        // the planner-created command when the connection fails to open.
        DbCommand? command = compiledQuery.DbCommand;
        try
        {
            command = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
            var reader = await RunReaderAsync(command!, compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
            var owner = new CommandReaderOwner(command, reader);
            command = null;
            return owner;
        }
        finally
        {
            command?.Dispose();
        }
    }

    /// <summary>Executes a mutation and returns the number of affected rows.</summary>
    /// <param name="sql">The parameterised statement text.</param>
    /// <param name="parameters">The parameters referenced by <paramref name="sql"/>.</param>
    /// <returns>The number of rows affected, as reported by the provider.</returns>
    public int ExecuteNonQuery(string sql, IReadOnlyList<Parameter> parameters)
    {
        CheckDisposed();
        using var cmd = CreateMutationCommand(sql, parameters);
        return RunNonQuery(cmd);
    }

    /// <summary>Asynchronously executes a mutation and returns the number of affected rows.</summary>
    /// <param name="sql">The parameterised statement text.</param>
    /// <param name="parameters">The parameters referenced by <paramref name="sql"/>.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of rows affected.</returns>
    public async Task<int> ExecuteNonQueryAsync(string sql, IReadOnlyList<Parameter> parameters, CancellationToken cancellationToken)
    {
        CheckDisposed();
        using var cmd = CreateMutationCommand(sql, parameters);
        return await RunNonQueryAsync(cmd, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Executes a mutation and returns the first column of its first row, or <see langword="null"/>.</summary>
    /// <param name="sql">The parameterised statement text.</param>
    /// <param name="parameters">The parameters referenced by <paramref name="sql"/>.</param>
    /// <returns>The scalar value, with <see cref="DBNull"/> normalized to <see langword="null"/>.</returns>
    public object? ExecuteScalar(string sql, IReadOnlyList<Parameter> parameters)
    {
        CheckDisposed();
        using var cmd = CreateMutationCommand(sql, parameters);
        var result = RunScalar(cmd);
        return result is DBNull ? null : result;
    }

    /// <summary>Asynchronously executes a mutation and returns the first column of its first row, or <see langword="null"/>.</summary>
    /// <param name="sql">The parameterised statement text.</param>
    /// <param name="parameters">The parameters referenced by <paramref name="sql"/>.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the scalar value, with <see cref="DBNull"/> normalized to <see langword="null"/>.</returns>
    public async Task<object?> ExecuteScalarAsync(string sql, IReadOnlyList<Parameter> parameters, CancellationToken cancellationToken)
    {
        CheckDisposed();
        using var cmd = CreateMutationCommand(sql, parameters);
        var result = await RunScalarAsync(cmd, cancellationToken).ConfigureAwait(false);
        return result is DBNull ? null : result;
    }

    /// <summary>
    /// Executes a mutation that returns rows (<c>INSERT ... RETURNING</c>/<c>OUTPUT</c>) and materialises
    /// every returned row through <paramref name="mapper"/>.
    /// </summary>
    /// <typeparam name="TResult">The materialized row type.</typeparam>
    /// <param name="sql">The parameterised statement text.</param>
    /// <param name="parameters">The parameters referenced by <paramref name="sql"/>.</param>
    /// <param name="mapper">The compiled row mapper.</param>
    /// <returns>The returned rows, materialized in result-set order.</returns>
    public List<TResult> ExecuteReader<TResult>(string sql, IReadOnlyList<Parameter> parameters, Func<IDataRecord, TResult> mapper)
    {
        CheckDisposed();
        using var cmd = CreateMutationCommand(sql, parameters);
        using var reader = RunReader(cmd, CommandBehavior.Default);

        var list = new List<TResult>();
        while (reader.Read())
            list.Add(mapper(reader));

        return list;
    }

    /// <summary>Asynchronously executes a mutation that returns rows and materialises every returned row.</summary>
    /// <typeparam name="TResult">The materialized row type.</typeparam>
    /// <param name="sql">The parameterised statement text.</param>
    /// <param name="parameters">The parameters referenced by <paramref name="sql"/>.</param>
    /// <param name="mapper">The compiled row mapper.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the returned rows, materialized in result-set order.</returns>
    public async Task<List<TResult>> ExecuteReaderAsync<TResult>(string sql, IReadOnlyList<Parameter> parameters, Func<IDataRecord, TResult> mapper, CancellationToken cancellationToken)
    {
        CheckDisposed();
        using var cmd = CreateMutationCommand(sql, parameters);
        using var reader = await RunReaderAsync(cmd, CommandBehavior.Default, cancellationToken).ConfigureAwait(false);

        var list = new List<TResult>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(mapper(reader));

        return list;
    }

    /// <summary>Executes a rendered batch in one round trip and materialises its result rows.</summary>
    public List<TResult> RunBatch<TResult>(BatchPlan plan, Func<IDataRecord, TResult> mapper)
        => _batchRunner.Run(plan, mapper);

    /// <summary>Asynchronously executes a rendered batch in one round trip and materialises its result rows.</summary>
    public Task<List<TResult>> RunBatchAsync<TResult>(BatchPlan plan, Func<IDataRecord, TResult> mapper, CancellationToken cancellationToken)
        => _batchRunner.RunAsync(plan, mapper, cancellationToken);

    /// <summary>Streams a rendered batch's result rows; the reader stays open for the whole batch.</summary>
    public IAsyncEnumerable<TResult> RunBatchStream<TResult>(BatchPlan plan, Func<IDataRecord, TResult> mapper, CancellationToken cancellationToken)
        => _batchRunner.RunStream(plan, mapper, cancellationToken);

    /// <summary>Executes a rendered batch in one round trip and eagerly materialises every result set.</summary>
    /// <param name="plan">The rendered plan.</param>
    /// <param name="materializers">One materialiser per result set, in result-set order.</param>
    /// <returns>The eagerly materialised result sets.</returns>
    public BatchResult RunBatchMultiple(BatchPlan plan, IReadOnlyList<IBatchResultMaterializer> materializers)
        => _batchRunner.RunBatchMultiple(plan, materializers);

    /// <summary>Asynchronously executes a rendered batch in one round trip and eagerly materialises every result set.</summary>
    /// <param name="plan">The rendered plan.</param>
    /// <param name="materializers">One materialiser per result set, in result-set order.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the eagerly materialised result sets.</returns>
    public Task<BatchResult> RunBatchMultipleAsync(BatchPlan plan, IReadOnlyList<IBatchResultMaterializer> materializers, CancellationToken cancellationToken)
        => _batchRunner.RunBatchMultipleAsync(plan, materializers, cancellationToken);

    /// <summary>
    /// Single entry guard for the public execution overloads: a context only executes the command
    /// representation it produces itself (mixing storage backends is not supported). Foreign
    /// implementations are rejected here, once, instead of in every overload.
    /// </summary>
    private static DbPreparedQueryCommand<TResult> AsDbCommand<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand)
        => preparedQueryCommand as DbPreparedQueryCommand<TResult>
           ?? throw new ArgumentException($"Expected {nameof(DbPreparedQueryCommand<TResult>)}, got {preparedQueryCommand.GetType().Name}", nameof(preparedQueryCommand));

    /// <summary>
    /// Resolves the enumerator a streaming terminal needs. A command prepared with
    /// <c>nonStreamUsing: true</c> (the <see cref="QueryCommand{TResult}.Prepare"/> default) is optimised for
    /// buffered/scalar results and never gets one, so fail with an actionable message instead of a
    /// NullReferenceException.
    /// </summary>
    private static ResultSetEnumerator<TResult> RequireEnumerator<TResult>(DbPreparedQueryCommand<TResult> compiledQuery)
        => compiledQuery.Enumerator
           ?? throw new InvalidOperationException(
               "This prepared command has no enumerator because it was created with nonStreamUsing: true, "
               + "which is optimised for buffered and scalar results. Use Prepare(nonStreamUsing: false) "
               + "to stream (ToAsyncEnumerable/ToEnumerable/CreateEnumerator).");

    public IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        if (compiledQuery.PendingBatch is { } batch)
            return _batchRunner.RunStream(batch, BeginBatch(compiledQuery, @params), cancellationToken).GetAsyncEnumerator(cancellationToken);

        var sqlEnumerator = RequireEnumerator(compiledQuery);
        sqlEnumerator.InitEnumerator(_connectionManager, _createParam, @params, cancellationToken, _currentTransaction, _interceptors, _context);
        return sqlEnumerator;
    }

    public IEnumerator<TResult> CreateEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        if (compiledQuery.PendingBatch is { } batch)
            return _batchRunner.Run(batch, BeginBatch(compiledQuery, @params)).GetEnumerator();

        var sqlEnumerator = RequireEnumerator(compiledQuery);
        sqlEnumerator.InitEnumerator(_connectionManager, _createParam, @params, CancellationToken.None, _currentTransaction, _interceptors, _context);
        sqlEnumerator.InitReader(@params);

        return sqlEnumerator;
    }

    async Task<IEnumerator<TResult>> IRowReaderFactory.CreateEnumeratorAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        if (compiledQuery.PendingBatch is { } batch)
        {
            var rows = await _batchRunner.RunAsync(batch, BeginBatch(compiledQuery, @params), cancellationToken).ConfigureAwait(false);
            return rows.GetEnumerator();
        }

        var enumerator = CreateAsyncEnumerator(preparedQueryCommand, @params, cancellationToken);
        await ((IAsyncInit<TResult>)enumerator).InitReaderAsync(@params, cancellationToken).ConfigureAwait(false);
        return (IEnumerator<TResult>)enumerator;
    }

    // A batch has no way to bind positional runtime parameters (@params): its parameters are baked
    // into the plan when it is rendered, and the render already happens per execution with the source
    // query's current captured values. Reject a non-empty @params instead of silently ignoring it.
    private static Func<IDataRecord, TResult> BeginBatch<TResult>(DbPreparedQueryCommand<TResult> compiledQuery, ReadOnlySpan<object?> @params)
    {
        RequireNoRuntimeParameters(@params);
        return BuildBatchMapper(compiledQuery);
    }

    private static Func<IDataRecord, TResult> BeginBatch<TResult>(DbPreparedQueryCommand<TResult> compiledQuery, object[]? @params)
    {
        if (@params is { Length: > 0 })
            throw new NotSupportedException(RuntimeParametersNotSupported);

        return BuildBatchMapper(compiledQuery);
    }

    private static void RequireNoRuntimeParameters(ReadOnlySpan<object?> @params)
    {
        if (!@params.IsEmpty)
            throw new NotSupportedException(RuntimeParametersNotSupported);
    }

    private static Func<IDataRecord, TResult> BuildBatchMapper<TResult>(DbPreparedQueryCommand<TResult> compiledQuery)
        => compiledQuery.MapDelegate
           ?? throw new NotSupportedException("A temporary-table read has no row projection to materialise.");

    private const string RuntimeParametersNotSupported =
        "A temporary-table read does not support positional runtime parameters; capture the value in a local variable in the source query instead.";

    public async Task<List<TResult>> ToListAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        if (compiledQuery.PendingBatch is { } batch)
        {
            var rows = await _batchRunner.RunAsync(batch, BeginBatch(compiledQuery, @params), cancellationToken).ConfigureAwait(false);
            compiledQuery.LastRowCount = rows.Count;
            return rows;
        }

        var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
        var reader = await RunReaderAsync(sqlCommand, compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
        using (reader)
        {
            var l = new List<TResult>(compiledQuery.LastRowCount);
            var mapper = compiledQuery.MapDelegate!;
            while (reader.Read())
            {
                l.Add(mapper(reader));
            }

            compiledQuery.LastRowCount = l.Count;
            return l;
        }
    }

    public List<TResult> ToList<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        if (compiledQuery.PendingBatch is { } batch)
        {
            var rows = _batchRunner.Run(batch, BeginBatch(compiledQuery, @params));
            compiledQuery.LastRowCount = rows.Count;
            return rows;
        }

        var sqlCommand = GetDbCommand(compiledQuery, @params);
        var reader = RunReader(sqlCommand, compiledQuery.Behavior);
        using (reader)
        {
            var l = new List<TResult>(compiledQuery.LastRowCount);
            var mapper = compiledQuery.MapDelegate!;
            while (reader.Read())
            {
                l.Add(mapper(reader));
            }

            compiledQuery.LastRowCount = l.Count;
            return l;
        }
    }

    /// <summary>
    /// Converts a raw scalar value to <typeparamref name="TResult"/>. SQLite has no bool type
    /// (comparisons/EXISTS come back as INTEGER/<see cref="long"/>), so typed fast paths are used
    /// to avoid boxing and <see cref="IConvertible"/> dispatch for the common scalar types.
    /// </summary>
    private static TResult ConvertScalar<TResult>(object value)
    {
        // Already the wanted runtime type (bool/int/long/double/decimal/string/...): unbox, no
        // IConvertible dispatch. A boxed underlying value also satisfies `is T?`.
        if (value is TResult result) return result;

        // A nullable projection (TResult = T?) is converted through its underlying type:
        // Convert.ChangeType does not understand Nullable<T>, and the typed fast paths below compare
        // against the non-nullable type. Boxing the underlying value is a valid unboxing to T?.
        // This is what lets a provider's wider/native type map onto a nullable projection, e.g.
        // MySQL timestampdiff BIGINT -> int? or SQLite datetime TEXT -> DateTime?.
        if (Nullable.GetUnderlyingType(typeof(TResult)) is { } underlyingType)
            return (TResult)Convert.ChangeType(value, underlyingType)!;

        var type = typeof(TResult);

        if (type == typeof(bool))
        {
            // SQLite has no bool type: comparisons/EXISTS come back as INTEGER (long).
            var v = value switch
            {
                long l => l != 0,
                int i => i != 0,
                short s => s != 0,
                byte b => b != 0,
                _ => Convert.ToBoolean(value),
            };
            return Unsafe.As<bool, TResult>(ref v);
        }
        if (type == typeof(int))
        {
            var v = value switch
            {
                // Convert.ToInt32(long) is checked; a narrowing cast must throw on overflow too.
                long l => checked((int)l),
                _ => Convert.ToInt32(value),
            };
            return Unsafe.As<int, TResult>(ref v);
        }
        if (type == typeof(long))
        {
            var v = value switch
            {
                int i => i,
                _ => Convert.ToInt64(value),
            };
            return Unsafe.As<long, TResult>(ref v);
        }
        if (type == typeof(double))
        {
            var v = value switch
            {
                // Widening float->double; Convert.ToDouble(double) would round-trip IConvertible.
                float f => f,
                _ => Convert.ToDouble(value),
            };
            return Unsafe.As<double, TResult>(ref v);
        }

        return (TResult)Convert.ChangeType(value, type);
    }

    public TResult? ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params, bool throwIfNull)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        object? r;
        if (compiledQuery.PendingBatch is { } batch)
        {
            r = _batchRunner.RunScalar(batch);
        }
        else
        {
            var sqlCommand = GetDbCommand(compiledQuery, @params);
            r = RunScalar(sqlCommand);
        }

        if (r is TResult res) return res;
        if (r is null or DBNull)
        {
            if (throwIfNull) throw new InvalidOperationException();
            return default;
        }

        return ConvertScalar<TResult>(r);
    }

    public async Task<TResult?> ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, bool throwIfNull, CancellationToken cancellationToken)
    {
        CheckDisposed();
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        object? r;
        if (compiledQuery.PendingBatch is { } batch)
        {
            r = await _batchRunner.RunScalarAsync(batch, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
            r = await RunScalarAsync(sqlCommand, cancellationToken).ConfigureAwait(false);
        }

        if (r is TResult res) return res;
        if (r is null or DBNull)
        {
            if (throwIfNull) throw new InvalidOperationException();
            return default;
        }

        return ConvertScalar<TResult>(r);
    }

    [Conditional("DEBUG")]
    private void CheckDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed(), nameof(DataContext));
    }

    public TResult First<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        // Scalar mode: no row mapper was compiled, read the value directly.
        if (compiledQuery.MapDelegate is null)
            return ExecuteScalar<TResult>(preparedQueryCommand, @params, true)!;

        if (compiledQuery.PendingBatch is { } batch)
        {
            var rows = _batchRunner.Run(batch, BeginBatch(compiledQuery, @params));
            if (rows.Count > 0)
                return rows[0];

            throw new InvalidOperationException();
        }

        var sqlCommand = GetDbCommand(compiledQuery, @params);
        var reader = RunReader(sqlCommand, compiledQuery.Behavior);
        var mapper = compiledQuery.MapDelegate!;
        using (reader)
        {
            if (reader.Read())
                return mapper(reader);

            throw new InvalidOperationException();
        }
    }

    public async Task<TResult> FirstAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        // Scalar mode: no row mapper was compiled, read the value directly.
        if (compiledQuery.MapDelegate is null)
            return (await ExecuteScalar<TResult>(preparedQueryCommand, @params, true, cancellationToken))!;

        if (compiledQuery.PendingBatch is { } batch)
        {
            var rows = await _batchRunner.RunAsync(batch, BeginBatch(compiledQuery, @params), cancellationToken).ConfigureAwait(false);
            if (rows.Count > 0)
                return rows[0];

            throw new InvalidOperationException();
        }

        var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
        var reader = await RunReaderAsync(sqlCommand, compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
        var mapper = compiledQuery.MapDelegate!;
        using (reader)
        {
            if (reader.Read())
                return mapper(reader);

            throw new InvalidOperationException();
        }
    }

    public TResult? FirstOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        // Scalar mode: no row mapper was compiled, read the value directly.
        if (compiledQuery.MapDelegate is null)
            return ExecuteScalar<TResult>(preparedQueryCommand, @params, false);

        if (compiledQuery.PendingBatch is { } batch)
        {
            var rows = _batchRunner.Run(batch, BeginBatch(compiledQuery, @params));
            return rows.Count > 0 ? rows[0] : default;
        }

        var sqlCommand = GetDbCommand(compiledQuery, @params);
        var reader = RunReader(sqlCommand, compiledQuery.Behavior);
        var mapper = compiledQuery.MapDelegate!;
        using (reader)
        {
            if (reader.Read())
                return mapper(reader);

            return default;
        }
    }

    public async Task<TResult?> FirstOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        // Scalar mode: no row mapper was compiled, read the value directly.
        if (compiledQuery.MapDelegate is null)
            return await ExecuteScalar<TResult>(preparedQueryCommand, @params, false, cancellationToken);

        if (compiledQuery.PendingBatch is { } batch)
        {
            var rows = await _batchRunner.RunAsync(batch, BeginBatch(compiledQuery, @params), cancellationToken).ConfigureAwait(false);
            return rows.Count > 0 ? rows[0] : default;
        }

        var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
        var reader = await RunReaderAsync(sqlCommand, compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
        var mapper = compiledQuery.MapDelegate!;
        using (reader)
        {
            if (reader.Read())
                return mapper(reader);

            return default;
        }
    }

    public TResult Single<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        if (compiledQuery.PendingBatch is { } batch)
        {
            var rows = _batchRunner.Run(batch, BeginBatch(compiledQuery, @params));
            if (rows.Count != 1)
                throw new InvalidOperationException();

            return rows[0];
        }

        var sqlCommand = GetDbCommand(compiledQuery, @params);
        var reader = RunReader(sqlCommand, compiledQuery.Behavior);
        using (reader)
        {
            TResult r = default!;
            var hasResult = false;
            var mapper = compiledQuery.MapDelegate!;
            while (reader.Read())
            {
                if (hasResult)
                    throw new InvalidOperationException();

                r = mapper(reader);
                hasResult = true;
            }

            if (!hasResult)
                throw new InvalidOperationException();

            return r!;
        }
    }

    public async Task<TResult> SingleAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        if (compiledQuery.PendingBatch is { } batch)
        {
            var rows = await _batchRunner.RunAsync(batch, BeginBatch(compiledQuery, @params), cancellationToken).ConfigureAwait(false);
            if (rows.Count != 1)
                throw new InvalidOperationException();

            return rows[0];
        }

        var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
        var reader = await RunReaderAsync(sqlCommand, compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
        using (reader)
        {
            TResult r = default!;
            var hasResult = false;
            var mapper = compiledQuery.MapDelegate!;
            while (reader.Read())
            {
                if (hasResult)
                    throw new InvalidOperationException();

                r = mapper(reader);
                hasResult = true;
            }

            if (!hasResult)
                throw new InvalidOperationException();

            return r!;
        }
    }

    public TResult? SingleOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        if (compiledQuery.PendingBatch is { } batch)
        {
            var rows = _batchRunner.Run(batch, BeginBatch(compiledQuery, @params));
            if (rows.Count > 1)
                throw new InvalidOperationException();

            return rows.Count == 1 ? rows[0] : default;
        }

        var sqlCommand = GetDbCommand(compiledQuery, @params);
        var reader = RunReader(sqlCommand, compiledQuery.Behavior);

        using (reader)
        {
            TResult? r = default;
            var hasResult = false;
            var mapper = compiledQuery.MapDelegate!;
            while (reader.Read())
            {
                if (hasResult)
                    throw new InvalidOperationException();

                r = mapper(reader);
                hasResult = true;
            }

            return r;
        }
    }

    public async Task<TResult?> SingleOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        if (compiledQuery.PendingBatch is { } batch)
        {
            var rows = await _batchRunner.RunAsync(batch, BeginBatch(compiledQuery, @params), cancellationToken).ConfigureAwait(false);
            if (rows.Count > 1)
                throw new InvalidOperationException();

            return rows.Count == 1 ? rows[0] : default;
        }

        var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
        var reader = await RunReaderAsync(sqlCommand, compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
        using (reader)
        {
            TResult? r = default;
            var hasResult = false;
            var mapper = compiledQuery.MapDelegate!;
            while (reader.Read())
            {
                if (hasResult)
                    throw new InvalidOperationException();

                r = mapper(reader);
                hasResult = true;
            }

            return r;
        }
    }

    /// <summary>
    /// Streams a prepared command's rows directly to <paramref name="output"/> as JSON, reading each
    /// row through a typed <see cref="JsonRowWriter"/> instead of materializing the projected result.
    /// The caller-owned <paramref name="output"/> is never closed; the reader is released in
    /// <c>finally</c> and the rented buffer is returned by the writer's disposal.
    /// </summary>
    /// <typeparam name="TResult">The projected result type; it is never materialized on this path.</typeparam>
    /// <param name="compiledQuery">The prepared, mapper-less command to execute.</param>
    /// <param name="rowWriter">The compiled per-row JSON writer, or <see langword="null"/> on the native document path.</param>
    /// <param name="plan">The frozen shape plan used to reject an incompatible reader schema before writing.</param>
    /// <param name="output">The caller-owned destination stream; it is never closed.</param>
    /// <param name="options">The validated container options.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <param name="native">Whether the prepared command carries a native <c>FOR JSON</c> document column to copy instead of managed row serialization.</param>
    internal void WriteJson<TResult>(DbPreparedQueryCommand<TResult> compiledQuery, JsonRowWriter? rowWriter, JsonShapePlan plan, Stream output, JsonStreamOptions options, ReadOnlySpan<object?> @params, bool native)
    {
        ObjectDisposedException.ThrowIf(_isDisposed(), nameof(DataContext));
        ThrowIfJsonBatch(compiledQuery);

        if (native)
        {
            WriteJsonNative(compiledQuery, output, @params);
            return;
        }

        var sqlCommand = GetDbCommand(compiledQuery, @params);
        var reader = RunReader(sqlCommand, compiledQuery.Behavior);
        try
        {
            // Position on the first row so the provider field types are available, then reject an
            // incompatible reader schema before the destination is touched by anything, framing
            // included. The validation is metadata-based, so it must run even when the result set is
            // empty (`hasRow == false`); a compatible empty result still emits `[]` below, and a
            // one-row look-ahead is not buffering: the row is written immediately below and not lost.
            var hasRow = reader.Read();
            JsonRowWriterFactory.ValidateReaderBinding(plan, reader);

            using var stream = new JsonStreamWriter(output, rowWriter!, options);
            while (hasRow)
            {
                stream.WriteRow(reader);
                hasRow = reader.Read();
            }

            stream.Complete();
        }
        finally
        {
            reader.Dispose();
        }
    }

    /// <summary>
    /// Asynchronously streams a prepared command's rows directly to <paramref name="output"/> as JSON.
    /// The caller-owned <paramref name="output"/> is never closed; the reader is released in
    /// <c>finally</c> and the rented buffer is returned by the writer's disposal.
    /// </summary>
    /// <typeparam name="TResult">The projected result type; it is never materialized on this path.</typeparam>
    /// <param name="compiledQuery">The prepared, mapper-less command to execute.</param>
    /// <param name="rowWriter">The compiled per-row JSON writer, or <see langword="null"/> on the native document path.</param>
    /// <param name="plan">The frozen shape plan used to reject an incompatible reader schema before writing.</param>
    /// <param name="output">The caller-owned destination stream; it is never closed.</param>
    /// <param name="options">The validated container options.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token observed while reading rows and writing to the stream.</param>
    /// <param name="native">Whether the prepared command carries a native <c>FOR JSON</c> document column to copy instead of managed row serialization.</param>
    internal async Task WriteJsonAsync<TResult>(DbPreparedQueryCommand<TResult> compiledQuery, JsonRowWriter? rowWriter, JsonShapePlan plan, Stream output, JsonStreamOptions options, object[]? @params, CancellationToken cancellationToken, bool native)
    {
        ObjectDisposedException.ThrowIf(_isDisposed(), nameof(DataContext));
        ThrowIfJsonBatch(compiledQuery);

        if (native)
        {
            await WriteJsonNativeAsync(compiledQuery, output, @params, cancellationToken).ConfigureAwait(false);
            return;
        }

        var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
        var reader = await RunReaderAsync(sqlCommand, compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
        try
        {
            // Metadata-based reader-binding validation runs unconditionally, so an empty result set with
            // an incompatible schema is rejected before any destination write (not excused by hasRow).
            var hasRow = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            JsonRowWriterFactory.ValidateReaderBinding(plan, reader);

            using var stream = new JsonStreamWriter(output, rowWriter!, options);
            while (hasRow)
            {
                await stream.WriteRowAsync(reader, cancellationToken).ConfigureAwait(false);
                hasRow = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }

            await stream.CompleteAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await reader.DisposeAsync().ConfigureAwait(false);
        }
    }

    // The native path copies the single FOR JSON document column straight to the caller-owned
    // destination. SequentialAccess lets the provider stream the document column in chunks instead of
    // buffering the whole nvarchar(max); the reader is released in finally and the destination is never
    // flushed or disposed. The transport has its own reader-binding validation (one string column),
    // rather than the projected-column validation that only fits the managed path.
    private void WriteJsonNative<TResult>(DbPreparedQueryCommand<TResult> compiledQuery, Stream output, ReadOnlySpan<object?> @params)
    {
        var sqlCommand = GetDbCommand(compiledQuery, @params);
        var reader = RunReader(sqlCommand, compiledQuery.Behavior | CommandBehavior.SequentialAccess);
        try
        {
            JsonNativeStream.WriteDocument(reader, output);
        }
        finally
        {
            reader.Dispose();
        }
    }

    private async Task WriteJsonNativeAsync<TResult>(DbPreparedQueryCommand<TResult> compiledQuery, Stream output, object[]? @params, CancellationToken cancellationToken)
    {
        var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
        var reader = await RunReaderAsync(sqlCommand, compiledQuery.Behavior | CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);
        try
        {
            await JsonNativeStream.WriteDocumentAsync(reader, output, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await reader.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static void ThrowIfJsonBatch<TResult>(DbPreparedQueryCommand<TResult> compiledQuery)
    {
        if (compiledQuery.PendingBatch is not null)
            throw new NotSupportedException(
                "JSON streaming validation [unsupported-execution-form]: WriteJson does not support a query backed by a lazy temporary table; materialize the temporary table source first.");
    }
}
