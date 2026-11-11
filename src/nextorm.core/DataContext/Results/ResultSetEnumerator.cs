using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace NextORM.Core;
/// <summary>
/// Enumerates the rows of a prepared database command. The data reader is created lazily on the
/// first move, using the connection supplied by <see cref="IConnectionManager"/>; rows are
/// materialized by the compiled query's map delegate inside <c>MoveNext</c>/<c>MoveNextAsync</c>,
/// leaving <see cref="Current"/> a plain field read.
/// </summary>
/// <typeparam name="TResult">The projected element type.</typeparam>
public sealed class ResultSetEnumerator<TResult> : IAsyncEnumerator<TResult>, IAsyncInit<TResult>
{
    //private readonly QueryCommand<TResult> _cmd;
    // Execution host: the connection lifecycle comes from the role, parameter creation from a
    // delegate. Neither is the concrete DataContext, so the enumerator no longer depends on the
    // context type (F6). The delegate is handed over once per enumeration, not built per call.
    private IConnectionManager? _connectionManager;
    private Func<DbCommand, string, object?, DbParameter>? _createParam;
    private Func<DbTransaction?>? _currentTransaction;
    private InterceptorHooks? _interceptors;
    private IDataContext? _context;
    private readonly DbPreparedQueryCommand<TResult> _compiledQuery;
    // Execution-local hold on the per-connection state: acquired when the reader opens and released
    // when the reader is disposed (stream completion or early abandon), never later.
    private CommandLease? _lease;
    private Action<DbCommand>? _onCommandCreated;
    private CancellationToken _cancellationToken;
    private object[]? _params;
    private readonly Func<IDataRecord, TResult>? _map;
    private readonly ObjectPool<StringBuilder> _sbPool;
    private ILogger? _logger;
    private bool _logDebug;
    private bool _logSensitiveData;
    private DbDataReader? _reader;
    // Not owned by this enumerator: _conn comes from IConnectionManager.GetConnection() and is
    // disposed by the context (DataContext.DisposeStaff). Disposing it here would close a connection
    // that is still in use, so it is only cleared (Reset/DisposeAsync).
    private DbConnection? _conn;
    // Owned only under the None policy: the execution created a fresh command that nothing else can
    // release. Under Connection the command belongs to the per-connection state, so this stays null.
    // Cleared before disposal so it is disposed once.
    private DbCommand? _ownedCommand;
    private bool _disposed;
    private TResult _current = default!;
    /// <summary>
    /// Initializes an enumerator for the given compiled database query.
    /// </summary>
    /// <param name="compiledQuery">The compiled query supplying the command text, behavior and row mapper.</param>
    /// <param name="sbPool">Pool used to build the sensitive-data log message; defaults to <c>StringBuilderPool.Shared</c> when <see langword="null"/>.</param>
    public ResultSetEnumerator(DbPreparedQueryCommand<TResult> compiledQuery, ObjectPool<StringBuilder>? sbPool = null)
    {
        //_cmd = cmd;
        _compiledQuery = compiledQuery;
        _map = compiledQuery.MapDelegate;
        _sbPool = sbPool ?? StringBuilderPool.Shared;
    }
    // The row is materialized in MoveNext/MoveNextAsync, so Current is a plain field read.
    // This keeps mapping out of the async-iterator's `yield return enumerator.Current` path
    // and out of the interface dispatch that can't be inlined.
    /// <summary>
    /// Gets the row materialized by the most recent successful <see cref="MoveNext"/> or
    /// <see cref="MoveNextAsync"/>.
    /// </summary>
    public TResult Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _current;
    }
    object? IEnumerator.Current => _current;

    /// <summary>
    /// Applies the logging configuration the context used to push through a <c>DataContext</c>
    /// property setter. Called once, right after construction.
    /// </summary>
    internal void InitEnvironment(ILogger? logger, bool logSensitiveData)
    {
        _logger = logger;
        _logDebug = logger?.IsEnabled(LogLevel.Debug) ?? false;
        _logSensitiveData = logSensitiveData;
    }

    /// <summary>
    /// Makes this enumerator responsible for disposing the command this execution owns (the
    /// <see cref="PreparedCommandCache.None"/> policy). The lease stops owning it so disposal happens
    /// exactly once; under <see cref="PreparedCommandCache.Connection"/> the state keeps the command.
    /// </summary>
    /// <param name="command">The command acquired by this execution.</param>
    internal void OwnCommand(DbCommand command)
    {
        // The lease is a struct: mutate this holder's own copy and write it back, otherwise the
        // cleared ownership flag would be lost and the lease would dispose the command as well.
        if (_lease is { } lease)
        {
            lease.ReleaseCommandOwnership();
            _lease = lease;
        }

        _ownedCommand = command;
    }

    /// <summary>
    /// Asynchronously disposes the active data reader, if any, releases an execution-owned command and
    /// releases this execution's per-connection lease. The underlying connection is owned by the
    /// context and is not closed here; the lease is released even when the reader disposal throws.
    /// </summary>
    /// <returns>A value task completing when the reader, command and lease have been released.</returns>
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        var reader = _reader;
        _reader = null;
        _conn = null;

        var command = _ownedCommand;
        _ownedCommand = null;

        var lease = _lease;
        _lease = null;

        if (reader is not null)
        {
            if (_logDebug) _logger!.LogDebug("Disposing data reader");
            return DisposeReaderAndCommandAsync(reader, command, lease);
        }

        try
        {
            DisposeOwnedCommand(command);
        }
        finally
        {
            lease?.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private async ValueTask DisposeReaderAndCommandAsync(DbDataReader reader, DbCommand? command, CommandLease? lease)
    {
        try
        {
            await reader.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            DisposeOwnedCommand(command);
            lease?.Dispose();
        }
    }

    /// <summary>
    /// Disposes the command this enumeration owns and records the disposal on the prepared command, so
    /// the diagnostic creation/disposal counters balance for the streaming form as well as the buffered
    /// one. The lease handed disposal over earlier
    /// (<see cref="CommandLease.ReleaseCommandOwnership"/>), so this runs exactly once per
    /// execution and never disposes a command the state still publishes.
    /// </summary>
    /// <param name="command">The execution-owned command, or <see langword="null"/> when the policy reuses one.</param>
    private void DisposeOwnedCommand(DbCommand? command)
    {
        if (command is null)
            return;

        command.Dispose();
        _compiledQuery.OnLeaseCommandDisposed();
    }

    // public void Init(object data)
    // {
    //     _params = (List<Parameter>)data;
    // }

    /// <summary>
    /// Asynchronously advances to the next row, opening the reader and executing the command on the
    /// first call. When the provider completes <c>ReadAsync</c> synchronously (the common buffered
    /// case) the row is read without crossing an await.
    /// </summary>
    /// <returns><see langword="true"/> when a row was read; otherwise <see langword="false"/>.</returns>
    public ValueTask<bool> MoveNextAsync()
    {
        var reader = _reader;
        if (reader is null)
            return InitReaderAndMoveNextAsync();

#if DEBUG
        if (_logger?.IsEnabled(LogLevel.Trace) ?? false) _logger.LogTrace("Move next");
#endif
        // Buffered providers (sqlite, Npgsql non-sequential) complete ReadAsync synchronously.
        // Staying on this side of the await keeps the per-row path free of the async state
        // machine and of the Task await machinery entirely.
        var task = reader.ReadAsync(_cancellationToken);
        if (task.IsCompletedSuccessfully)
            return new ValueTask<bool>(ReadSync(reader, task.Result));

        return AwaitAndReadAsync(task, reader);
    }
    private async ValueTask<bool> InitReaderAndMoveNextAsync()
    {
        await InitReaderAsync(_params, _cancellationToken).ConfigureAwait(false);
        return await MoveNextAsync().ConfigureAwait(false);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ReadSync(DbDataReader reader, bool hasRow)
    {
        if (hasRow)
        {
            _current = _map!(reader);
            return true;
        }

        return false;
    }
    private async ValueTask<bool> AwaitAndReadAsync(Task<bool> task, DbDataReader reader)
    {
        if (await task.ConfigureAwait(false))
        {
            _current = _map!(reader);
            return true;
        }

        return false;
    }
    /// <summary>
    /// Synchronously advances to the next row, opening the reader and executing the command on the
    /// first call.
    /// </summary>
    /// <returns><see langword="true"/> when a row was read; otherwise <see langword="false"/>.</returns>
    public bool MoveNext()
    {
        if (_reader is null) InitReader(_params);
#if DEBUG
        if (_logger?.IsEnabled(LogLevel.Trace) ?? false) _logger.LogTrace("Move next");
#endif
        if (_reader!.Read())
        {
            _current = _map!(_reader);
            return true;
        }

        return false;
    }
    internal void InitEnumerator(IConnectionManager connectionManager, Func<DbCommand, string, object?, DbParameter> createParam, object[]? @params, CancellationToken cancellationToken, Func<DbTransaction?> currentTransaction, InterceptorHooks interceptors, IDataContext context)
    {
        _cancellationToken = cancellationToken;
        _params = @params;
        _connectionManager = connectionManager;
        _createParam = createParam;
        _currentTransaction = currentTransaction;
        _interceptors = interceptors;
        _context = context;
        _conn = connectionManager.GetConnection();
        _onCommandCreated = RaiseCommandInitialized;
    }

    private void RaiseCommandInitialized(DbCommand command)
    {
        var interceptors = _interceptors!.QueryInterceptors;
        if (interceptors.Length == 0)
            return;

        InterceptorHooks.RaiseCommandInitialized(interceptors, _context!, command);
    }

    // Raises CommandExecuting/CommandExecuted/CommandFailed around the reader creation. With no
    // interceptor registered the execute call is made directly, so the streaming hot path is
    // unchanged (no event data, timestamp or async state machine).
    private DbDataReader ExecuteReader(DbCommand command)
    {
        var interceptors = _interceptors!.QueryInterceptors;
        if (interceptors.Length == 0)
            return command.ExecuteReader(_compiledQuery.Behavior);

        return ExecuteReaderCore(command, interceptors);
    }

    private DbDataReader ExecuteReaderCore(DbCommand command, IQueryInterceptor[] interceptors)
    {
        InterceptorHooks.RaiseCommandExecuting(interceptors, _context!, command);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var reader = command.ExecuteReader(_compiledQuery.Behavior);
            InterceptorHooks.RaiseCommandExecuted(interceptors, _context!, command, Stopwatch.GetElapsedTime(started));
            return reader;
        }
        catch (Exception exception)
        {
            InterceptorHooks.RaiseCommandFailed(interceptors, _context!, command, exception);
            throw;
        }
    }

    private Task<DbDataReader> ExecuteReaderAsync(DbCommand command, CancellationToken cancellationToken)
    {
        var interceptors = _interceptors!.QueryInterceptors;
        if (interceptors.Length == 0)
            return command.ExecuteReaderAsync(_compiledQuery.Behavior, cancellationToken);

        return ExecuteReaderCoreAsync(command, cancellationToken, interceptors);
    }

    private async Task<DbDataReader> ExecuteReaderCoreAsync(DbCommand command, CancellationToken cancellationToken, IQueryInterceptor[] interceptors)
    {
        InterceptorHooks.RaiseCommandExecuting(interceptors, _context!, command);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var reader = await command.ExecuteReaderAsync(_compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
            InterceptorHooks.RaiseCommandExecuted(interceptors, _context!, command, Stopwatch.GetElapsedTime(started));
            return reader;
        }
        catch (Exception exception)
        {
            InterceptorHooks.RaiseCommandFailed(interceptors, _context!, command, exception);
            throw;
        }
    }
    /// <summary>
    /// Opens the connection and executes the command synchronously, creating the data reader. Does
    /// nothing when a reader already exists.
    /// </summary>
    /// <param name="params">Positional parameter values, in the order the SQL references them.</param>
    /// <exception cref="InvalidOperationException">No connection or connection manager has been assigned.</exception>
    public void InitReader(object[]? @params)
    {
        if (_reader is not null) return;
        if (_conn is null) throw new InvalidOperationException("Connection is empty");
        if (_connectionManager is null) throw new InvalidOperationException("Connection manager is empty");

        // The role owns "make sure the connection is open". This block used to be a copy of
        // DataContext.EnsureConnectionOpen.
        _connectionManager.EnsureConnectionOpen();

        var lease = _compiledQuery.Acquire(_conn, _createParam!, @params, _currentTransaction?.Invoke(), _onCommandCreated);
        DbDataReader reader;
        try
        {
            var sqlCommand = lease.Command;
            if (_logDebug) LogCommand(sqlCommand);
            reader = ExecuteReader(sqlCommand);
        }
        catch
        {
            lease.Fail();
            throw;
        }

        _lease = lease;
        if (lease.OwnsCommand)
            OwnCommand(lease.Command);

        _reader = reader;
    }
    /// <summary>
    /// Asynchronously opens the connection and executes the command, creating the data reader. Does
    /// nothing when a reader already exists.
    /// </summary>
    /// <param name="params">Positional parameter values, in the order the SQL references them.</param>
    /// <param name="cancellationToken">Token used to cancel opening and execution.</param>
    /// <returns>A task completing when the reader has been created.</returns>
    /// <exception cref="InvalidOperationException">No connection or connection manager has been assigned.</exception>
    public async Task InitReaderAsync(object[]? @params, CancellationToken cancellationToken)
    {
        if (_reader is not null) return;
        if (_conn is null) throw new InvalidOperationException("Connection is empty");
        if (_connectionManager is null) throw new InvalidOperationException("Connection manager is empty");

        await _connectionManager.EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);

        var lease = _compiledQuery.Acquire(_conn, _createParam!, @params, _currentTransaction?.Invoke(), _onCommandCreated);
        DbDataReader reader;
        try
        {
            var sqlCommand = lease.Command;
            if (_logDebug) LogCommand(sqlCommand);
            reader = await ExecuteReaderAsync(sqlCommand, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            lease.Fail();
            throw;
        }

        _lease = lease;
        if (lease.OwnsCommand)
            OwnCommand(lease.Command);

        _reader = reader;
    }
    private void LogCommand(DbCommand sqlCommand)
    {
        if (!_logSensitiveData)
        {
            // No message buffer needed: the SQL is already a string and is logged as-is.
            _logger!.LogDebug("Executing query: {sql}" + Environment.NewLine, sqlCommand.CommandText);

            if (sqlCommand.Parameters?.Count > 0)
                _logger!.LogDebug("Use {method} to see param values", nameof(_logSensitiveData));

            return;
        }

        var sb = _sbPool.Get();
        try
        {
            sb.Append("Executing query: {sql}").AppendLine();

            var parameterCount = sqlCommand.Parameters?.Count ?? 0;
            var ps = new object?[1 + parameterCount * 2];
            ps[0] = sqlCommand.CommandText;

            for (var i = 0; i < parameterCount; i++)
            {
                var p = sqlCommand.Parameters![i];
                sb.Append("param {name_").Append(i).Append("} = {value_").Append(i).Append('}').AppendLine();
                ps[1 + i * 2] = p.ParameterName;
                ps[2 + i * 2] = p.Value;
            }

            sb.Length -= Environment.NewLine.Length;
            _logger!.LogDebug(sb.ToString(), ps);
        }
        finally
        {
            _sbPool.Return(sb);
        }
    }
    /// <summary>
    /// Disposes and clears the active data reader so the enumerator can start over. The connection is
    /// only dropped, not disposed, because it is owned by the context.
    /// </summary>
    public void Reset()
    {
        var reader = _reader;
        _reader = null;
        _conn = null;

        try
        {
            if (reader is not null)
            {
                if (_logDebug) _logger!.LogDebug("Disposing data reader");
                reader.Dispose();
            }
        }
        finally
        {
            var command = _ownedCommand;
            _ownedCommand = null;
            DisposeOwnedCommand(command);

            var lease = _lease;
            _lease = null;
            lease?.Dispose();
        }
    }

    private void Dispose(bool disposing)
    {
        Reset();

        if (!_disposed)
        {
            if (disposing)
            {
                // Reset() already released the reader, an execution-owned command and the lease.
            }

            _disposed = true;
        }
    }

    // ~ResultSetEnumerator()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    /// <summary>
    /// Releases the enumerator by resetting it, then suppresses finalization.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    // public IEnumerator<TResult> GetEnumerator()
    // {
    //     return this;
    // }

    // IEnumerator IEnumerable.GetEnumerator()
    // {
    //     return this;
    // }

    // public IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    // {
    //     return this;
    // }
}

internal interface IAsyncInit<out TResult> : IEnumerator<TResult>
{
    Task InitReaderAsync(object[]? @params, CancellationToken cancellationToken);
}