using System.Collections;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace nextorm.core;
public class ResultSetEnumerator<TResult> : IAsyncEnumerator<TResult>, IAsyncInit<TResult>
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
    public ResultSetEnumerator(DbPreparedQueryCommand<TResult> compiledQuery)
    {
        //_cmd = cmd;
        _compiledQuery = compiledQuery;
        _map = compiledQuery.MapDelegate;
    }
    public TResult Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return _map!(_reader!);
        }
    }
    object? IEnumerator.Current => _map!(_reader!);

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

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (_reader is not null)
        {
            if (_logDebug) _logger!.LogDebug("Disposing data reader");
            await _reader.DisposeAsync().ConfigureAwait(false);

            _reader = null;
        }

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

    public async ValueTask<bool> MoveNextAsync()
    {
        if (_reader is null) await InitReaderAsync(_params, _cancellationToken).ConfigureAwait(false);
#if DEBUG
        if (_logger?.IsEnabled(LogLevel.Trace) ?? false) _logger.LogTrace("Move next");
#endif
        return await _reader!.ReadAsync(_cancellationToken).ConfigureAwait(false);
    }
    public bool MoveNext()
    {
        if (_reader is null) InitReader(_params);
#if DEBUG
        if (_logger?.IsEnabled(LogLevel.Trace) ?? false) _logger.LogTrace("Move next");
#endif
        return _reader!.Read();
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
#if DEBUG
        if (_conn is null) throw new InvalidOperationException("Connection is empty");
        if (_dbContext is null) throw new InvalidOperationException("DbContext is empty");
#endif
        var sqlCommand = CreateCommand(@params);

        if (_conn.State == ConnectionState.Closed)
        {
            if (_logDebug) _logger!.LogDebug("Opening connection");
            _conn.Open();
        }

        LogCommand(sqlCommand);

        _reader = sqlCommand.ExecuteReader(_compiledQuery.Behavior);
    }
    public async Task InitReaderAsync(object[]? @params, CancellationToken cancellationToken)
    {
        if (_reader is not null) return;
#if DEBUG
        if (_conn is null) throw new InvalidOperationException("Connection is empty");
        if (_dbContext is null) throw new InvalidOperationException("DbContext is empty");
#endif
        var sqlCommand = CreateCommand(@params);

        if (_conn.State == ConnectionState.Closed)
        {
            if (_logDebug) _logger!.LogDebug("Opening connection");
            await _conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        LogCommand(sqlCommand);

        _reader = await sqlCommand.ExecuteReaderAsync(_compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
    }
    private DbCommand CreateCommand(object[]? @params)
    {
        return _compiledQuery.GetDbCommand(@params, _dbContext!, _conn!);
    }
    private void LogCommand(DbCommand sqlCommand)
    {
        if (_logDebug)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Executing query: {sql}");

            //_logger.LogDebug("Executing query: {sql}", sqlCommand.CommandText);

            var ps = new ArrayList
            {
                sqlCommand.CommandText
            };

            if (_logSensitiveData)
            {
                var idx = 0;
                foreach (DbParameter p in sqlCommand.Parameters)
                {
                    sb.AppendLine($"param {{name_{idx}}} = {{value_{idx}}}");
                    ps.Add(p.ParameterName);
                    ps.Add(p.Value);
                    idx++;
                    //_logger.LogDebug("param {name} is {value}", p.ParameterName, p.Value);
                }
            }
            else if (sqlCommand.Parameters?.Count > 0)
            {
                _logger!.LogDebug(sb.ToString(), sqlCommand.CommandText);
                _logger!.LogDebug("Use {method} to see param values", nameof(_logSensitiveData));
                return;
            }

            if (_logSensitiveData)
            {
                sb.Length -= Environment.NewLine.Length;
                _logger!.LogDebug(sb.ToString(), ps.ToArray());
            }
            else
                _logger!.LogDebug(sb.ToString(), sqlCommand.CommandText);

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

    protected virtual void Dispose(bool disposing)
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