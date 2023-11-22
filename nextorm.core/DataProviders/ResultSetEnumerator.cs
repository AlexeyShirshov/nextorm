using System.Collections;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace nextorm.core;
public class ResultSetEnumerator<TResult> : IAsyncEnumerator<TResult>, IEnumerator<TResult>, IEnumerable<TResult>
{
    //private readonly QueryCommand<TResult> _cmd;
    private readonly SqlDataProvider _dataProvider;
    private readonly DatabaseCompiledQuery<TResult> _compiledQuery;
    private CancellationToken _cancellationToken;
    private object[]? _params;
    private readonly Func<IDataRecord, TResult> _map;
    // private List<Param>? _params;
    private DbDataReader? _reader;
    private readonly DbConnection _conn;
    private bool _disposedValue;
    public ResultSetEnumerator(SqlDataProvider dataProvider, DatabaseCompiledQuery<TResult> compiledQuery)
    {
        //_cmd = cmd;
        _dataProvider = dataProvider;
        _compiledQuery = compiledQuery;
        _map = compiledQuery.MapDelegate;

#if DEBUG
        if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Getting connection");
#endif
        _conn = _dataProvider.GetConnection();
    }
    public TResult Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return _map(_reader!);
        }
    }
    object? IEnumerator.Current => _map(_reader!);
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
    public void InitEnumerator(object[]? @params, CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _params = @params;
    }
    public void InitReader(object[]? @params)
    {
        var sqlCommand = _compiledQuery.DbCommand;
        sqlCommand.Connection = _conn;

        if (@params is not null)
            for (var i = 0; i < @params!.Length; i++)
            {
                var paramName = string.Format("norm_p{0}", i);
                sqlCommand.Parameters[paramName].Value = @params[i];
                // foreach (DbParameter p in sqlCommand.Parameters)
                // {
                //     if (p.ParameterName == paramName)
                //     {
                //         p.Value = @params[i];
                //         break;
                //     }
                // }
            }

        if (_reader is not null) return;

        if (_conn.State == ConnectionState.Closed)
        {
#if DEBUG
            if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Opening connection");
#endif
            _conn.Open();
        }

#if DEBUG
        if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false)
        {
            _dataProvider.Logger.LogDebug("Generated query: {sql}", sqlCommand.CommandText);

            if (_dataProvider.LogSensetiveData)
            {
                foreach (DbParameter p in sqlCommand.Parameters)
                {
                    _dataProvider.Logger.LogDebug("param {name} is {value}", p.ParameterName, p.Value);
                }
            }
            else if (sqlCommand.Parameters?.Count > 0)
            {
                _dataProvider.Logger.LogDebug("Use {method} to see param values", nameof(_dataProvider.LogSensetiveData));
            }
        }
#endif
        _reader = sqlCommand.ExecuteReader(_compiledQuery.Behavior);
    }
    public async Task InitReaderAsync(object[]? @params, CancellationToken cancellationToken)
    {
        var sqlCommand = _compiledQuery.DbCommand;
        sqlCommand.Connection = _conn;

        if (@params is not null)
            for (var i = 0; i < @params!.Length; i++)
            {
                var paramName = string.Format("norm_p{0}", i);
                sqlCommand.Parameters[paramName].Value = @params[i];
                // foreach (DbParameter p in sqlCommand.Parameters)
                // {
                //     if (p.ParameterName == paramName)
                //     {
                //         p.Value = @params[i];
                //         break;
                //     }
                // }
            }

        if (_reader is not null) return;

        if (_conn.State == ConnectionState.Closed)
        {
#if DEBUG
            if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Opening connection");
#endif
            await _conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

#if DEBUG
        if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false)
        {
            _dataProvider.Logger.LogDebug("Generated query: {sql}", sqlCommand.CommandText);

            if (_dataProvider.LogSensetiveData)
            {
                foreach (DbParameter p in sqlCommand.Parameters)
                {
                    _dataProvider.Logger.LogDebug("param {name} is {value}", p.ParameterName, p.Value);
                }
            }
            else if (sqlCommand.Parameters?.Count > 0)
            {
                _dataProvider.Logger.LogDebug("Use {method} to see param values", nameof(_dataProvider.LogSensetiveData));
            }
        }
#endif
        _reader = await sqlCommand.ExecuteReaderAsync(_compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
    }

    public bool MoveNext()
    {
#if DEBUG
        if (_reader is null) throw new InvalidOperationException("DbDataReader is not initialized");
#endif
        return _reader!.Read();
    }

    public void Reset()
    {
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_reader is not null)
        {
            if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Disposing data reader");
            _reader.Dispose();

            _reader = null;
        }
        if (!_disposedValue)
        {
            if (disposing)
            {
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
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
}
