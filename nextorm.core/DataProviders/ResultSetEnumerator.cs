using System.Data;
using System.Data.Common;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace nextorm.core;
public class ResultSetEnumerator<TResult> : IAsyncEnumerator<TResult>, IEnumeratorInit
{
    //private readonly QueryCommand<TResult> _cmd;
    private readonly SqlDataProvider _dataProvider;
    private readonly DatabaseCompiledQuery<TResult> _compiledQuery;
    private readonly CancellationToken _cancellationToken;
    private readonly Func<object, TResult> _map;
    private List<Param>? _params;
    private DbDataReader? _reader;
    private DbConnection? _conn;

    public ResultSetEnumerator(SqlDataProvider dataProvider, DatabaseCompiledQuery<TResult> compiledQuery, CancellationToken cancellationToken)
    {
        //_cmd = cmd;
        _dataProvider = dataProvider;
        _compiledQuery = compiledQuery;
        _cancellationToken = cancellationToken;
        _map = compiledQuery.MapDelegate;
    }
    public TResult Current => _map(_reader!);
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

    public void Init(object data)
    {
        _params = (List<Param>)data;
    }

    public async ValueTask<bool> MoveNextAsync()
    {
        if (_conn is null)
        {
            if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Creating connection");

            _conn = _dataProvider.GetConnection();
            await InitReader();
        }
        else if (_reader is null)
        {
            await InitReader();
        }

        if (_dataProvider.Logger?.IsEnabled(LogLevel.Trace) ?? false) _dataProvider.Logger.LogTrace("Move next");

        return await _reader!.ReadAsync(_cancellationToken);

        async Task InitReader()
        {
            var sqlCommand = _compiledQuery.GetCommand(_params!, _dataProvider);
            sqlCommand.Connection = _conn;

            if (_conn.State == ConnectionState.Closed)
            {
                if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Opening connection");

                await _conn.OpenAsync(_cancellationToken);
            }

            if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false)
            {
                _dataProvider.Logger.LogDebug(sqlCommand.CommandText);

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

            _reader = await sqlCommand.ExecuteReaderAsync(_cancellationToken);
        }
    }
}
