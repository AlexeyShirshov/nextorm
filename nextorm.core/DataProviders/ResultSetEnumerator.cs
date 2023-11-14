//#define INITALGO_2

using System.Collections;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace nextorm.core;
public class ResultSetEnumerator<TResult> : IAsyncEnumerator<TResult>, IEnumerator<TResult>, IEnumerable<TResult>
{
    //private readonly QueryCommand<TResult> _cmd;
    private readonly SqlDataProvider _dataProvider;
    private readonly DatabaseCompiledQuery<TResult> _compiledQuery;
    private CancellationToken _cancellationToken;
#if INITALGO_2
    private readonly Func<object, object[]?, TResult> _map;
#else
    private readonly Func<object, TResult> _map;
#endif
    // private List<Param>? _params;
    private DbDataReader? _reader;
    private readonly DbConnection _conn;
    private bool disposedValue;
#if INITALGO_2
    private object[]? _buffer;
#endif
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
#if INITALGO_2
    public TResult Current => _map(_reader!, _buffer!);
#else
    public TResult Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return _map(_reader!);
        }
    }
    object? IEnumerator.Current => _map(_reader!);
#endif
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (_reader is not null)
        {
            if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Disposing data reader");
            await _reader.DisposeAsync();

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
        if (_reader is null) await InitReaderAsync(_cancellationToken);
#if DEBUG
        if (_dataProvider.Logger?.IsEnabled(LogLevel.Trace) ?? false) _dataProvider.Logger.LogTrace("Move next");
#endif
        return await _reader!.ReadAsync(_cancellationToken);
    }
    public void InitReader(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;

    }
    public async Task InitReaderAsync(CancellationToken cancellationToken)
    {
        if (_reader is not null) return;

        var sqlCommand = _compiledQuery.DbCommand;
        sqlCommand.Connection = _conn;

        if (_conn.State == ConnectionState.Closed)
        {
#if DEBUG
            if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Opening connection");
#endif
            await _conn.OpenAsync(cancellationToken);
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
        _reader = await sqlCommand.ExecuteReaderAsync(_compiledQuery.Behavior, cancellationToken);

#if INITALGO_2
        _buffer ??= new object[_reader!.FieldCount];
#endif
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
        if (!disposedValue)
        {
            if (disposing)
            {
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
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
