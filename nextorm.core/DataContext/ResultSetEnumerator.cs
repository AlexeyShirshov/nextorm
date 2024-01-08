using System.Collections;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace nextorm.core;
public class ResultSetEnumerator<TResult> : IAsyncEnumerator<TResult>, IAsyncEnumerable<TResult>, IEnumerable<TResult>, IAsyncInit<TResult>
{
    //private readonly QueryCommand<TResult> _cmd;
    private readonly DbContext _dataProvider;
    private readonly DbCompiledQuery<TResult> _compiledQuery;
    private CancellationToken _cancellationToken;
    private object[]? _params;
    private readonly Func<IDataRecord, TResult>? _map;
    // private List<Param>? _params;
    private DbDataReader? _reader;
    private DbConnection? _conn;
    private bool _disposed;
    public ResultSetEnumerator(DbContext dataProvider, DbCompiledQuery<TResult> compiledQuery)
    {
        //_cmd = cmd;
        _dataProvider = dataProvider;
        _compiledQuery = compiledQuery;
        _map = compiledQuery.MapDelegate;

        // if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Getting connection");
        //_conn = _dataProvider.GetConnection();
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
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (_reader is not null)
        {
            if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Disposing data reader");
            await _reader.DisposeAsync().ConfigureAwait(false);

            _reader = null;
        }

        //_compiledQuery.DbCommand.Connection = null;
        // if (_conn is not null)
        // {
        //     if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Disposing connection");
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
        if (_dataProvider.Logger?.IsEnabled(LogLevel.Trace) ?? false) _dataProvider.Logger.LogTrace("Move next");
#endif
        return await _reader!.ReadAsync(_cancellationToken).ConfigureAwait(false);
    }
    public bool MoveNext()
    {
        if (_reader is null) InitReader(_params);
#if DEBUG
        if (_dataProvider.Logger?.IsEnabled(LogLevel.Trace) ?? false) _dataProvider.Logger.LogTrace("Move next");
#endif
        return _reader!.Read();
    }
    public void InitEnumerator(object[]? @params, CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _params = @params;
        _conn = _dataProvider.GetConnection();
    }
    public void InitReader(object[]? @params)
    {
        if (_reader is not null) return;
#if DEBUG
        if (_conn is null) throw new InvalidOperationException("Connection is empty");
#endif
        var sqlCommand = CreateCommand(@params);

        if (_conn.State == ConnectionState.Closed)
        {
            if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Opening connection");
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
#endif
        var sqlCommand = CreateCommand(@params);

        if (_conn.State == ConnectionState.Closed)
        {
            if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Opening connection");
            await _conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        LogCommand(sqlCommand);

        _reader = await sqlCommand.ExecuteReaderAsync(_compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
    }
    private DbCommand CreateCommand(object[]? @params)
    {
        _compiledQuery.InitParams(@params, _dataProvider);

        var sqlCommand = _compiledQuery.DbCommand;
        sqlCommand.Connection = _conn;
        return sqlCommand;
    }
    private void LogCommand(DbCommand sqlCommand)
    {
        if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Executing query: {sql}");

            //_dataProvider.Logger.LogDebug("Executing query: {sql}", sqlCommand.CommandText);

            var ps = new ArrayList
            {
                sqlCommand.CommandText
            };

            if (_dataProvider.LogSensitiveData)
            {
                var idx = 0;
                foreach (DbParameter p in sqlCommand.Parameters)
                {
                    sb.AppendLine($"param {{name_{idx}}} = {{value_{idx}}}");
                    ps.Add(p.ParameterName);
                    ps.Add(p.Value);
                    idx++;
                    //_dataProvider.Logger.LogDebug("param {name} is {value}", p.ParameterName, p.Value);
                }
            }
            else if (sqlCommand.Parameters?.Count > 0)
            {
                _dataProvider.Logger.LogDebug(sb.ToString(), sqlCommand.CommandText);
                _dataProvider.Logger.LogDebug("Use {method} to see param values", nameof(_dataProvider.LogSensitiveData));
                return;
            }

            if (_dataProvider.LogSensitiveData)
            {
                sb.Length -= Environment.NewLine.Length;
                _dataProvider.Logger.LogDebug(sb.ToString(), ps.ToArray());
            }
            else
                _dataProvider.Logger.LogDebug(sb.ToString(), sqlCommand.CommandText);

        }
    }
    public void Reset()
    {
        if (_reader is not null)
        {
            if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Disposing data reader");
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

    public IEnumerator<TResult> GetEnumerator()
    {
        return this;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this;
    }

    public IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return this;
    }
}

internal interface IAsyncInit<out TResult> : IEnumerator<TResult>
{
    Task InitReaderAsync(object[]? @params, CancellationToken cancellationToken);
}