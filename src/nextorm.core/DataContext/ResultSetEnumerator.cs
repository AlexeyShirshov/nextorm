using System.Collections;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace nextorm.core;
public sealed class ResultSetEnumerator<TResult> : IAsyncEnumerator<TResult>, IAsyncInit<TResult>
{
    //private readonly QueryCommand<TResult> _cmd;
    private DbContext? _dbContext;
    private readonly DbPreparedQueryCommand<TResult> _compiledQuery;
    private CancellationToken _cancellationToken;
    private object[]? _params;
    private readonly Func<IDataRecord, TResult>? _map;
    private ILogger? _logger;
    private bool _logDebug;
    private bool _logSensitiveData;
    private DbDataReader? _reader;
    private DbConnection? _conn;
    private bool _disposed;
    private TResult _current = default!;
    public ResultSetEnumerator(DbPreparedQueryCommand<TResult> compiledQuery)
    {
        //_cmd = cmd;
        _compiledQuery = compiledQuery;
        _map = compiledQuery.MapDelegate;
    }
    // The row is materialized in MoveNext/MoveNextAsync, so Current is a plain field read.
    // This keeps mapping out of the async-iterator's `yield return enumerator.Current` path
    // and out of the interface dispatch that can't be inlined.
    public TResult Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _current;
    }
    object? IEnumerator.Current => _current;

    public DbContext? DbContext
    {
        get => _dbContext;
        set
        {
            _dbContext = value;
            if (value is not null)
            {
                _logger = value.ResultSetEnumeratorLogger;
                _logDebug = _logger?.IsEnabled(LogLevel.Debug) ?? false;
                _logSensitiveData = value.LogSensitiveData;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (_reader is not null)
        {
            var r = _reader;
            _reader = null;
            _conn = null;
            if (_logDebug) _logger!.LogDebug("Disposing data reader");
            return r.DisposeAsync();
        }
        return ValueTask.CompletedTask;
        //_compiledQuery.DbCommand.Connection = null;
        // if (_conn is not null)
        // {
        //     if (_logDebug) _logger.LogDebug("Disposing connection");
        //     await _conn.DisposeAsync();
        // }
    }

    // public void Init(object data)
    // {
    //     _params = (List<Param>)data;
    // }

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
    public void InitEnumerator(DbContext dbContext, object[]? @params, CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _params = @params;
        DbContext = dbContext;
        _conn = dbContext.GetConnection();
    }
    public void InitReader(object[]? @params)
    {
        if (_reader is not null) return;
        if (_conn is null) throw new InvalidOperationException("Connection is empty");
        if (_dbContext is null) throw new InvalidOperationException("DbContext is empty");

        if (!_dbContext._connOpen)
        {
            if (_conn.State == ConnectionState.Closed)
            {
                if (_logDebug) _logger!.LogDebug("Opening connection");
                _conn.Open();
            }
            _dbContext._connOpen = true;
        }

        var sqlCommand = _compiledQuery.GetDbCommand(@params, _dbContext!, _conn!);

        if (_logDebug) LogCommand(sqlCommand);

        _reader = sqlCommand.ExecuteReader(_compiledQuery.Behavior);
    }
    public async Task InitReaderAsync(object[]? @params, CancellationToken cancellationToken)
    {
        if (_reader is not null) return;
        if (_conn is null) throw new InvalidOperationException("Connection is empty");
        if (_dbContext is null) throw new InvalidOperationException("DbContext is empty");

        if (!_dbContext._connOpen)
        {
            if (_conn.State == ConnectionState.Closed)
            {
                if (_logDebug) _logger!.LogDebug("Opening connection");
                await _conn.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            _dbContext._connOpen = true;
        }

        var sqlCommand = _compiledQuery.GetDbCommand(@params, _dbContext!, _conn!);

        if (_logDebug) LogCommand(sqlCommand);

        _reader = await sqlCommand.ExecuteReaderAsync(_compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
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

        var sb = DbContext._sbPool.Get();
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
            DbContext._sbPool.Return(sb);
        }
    }
    public void Reset()
    {
        if (_reader is not null)
        {
            if (_logDebug) _logger!.LogDebug("Disposing data reader");
            _reader.Dispose();

            _reader = null;
            _conn = null;
        }
    }

    private void Dispose(bool disposing)
    {
        Reset();

        if (!_disposed)
        {
            if (disposing)
            {
            }

            _disposed = true;
        }
    }

    // ~ResultSetEnumerator()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

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