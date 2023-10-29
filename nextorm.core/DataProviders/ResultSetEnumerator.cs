using System.Data;
using System.Data.Common;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace nextorm.core;
public class ResultSetEnumerator<TResult> : IAsyncEnumerator<TResult>
{
    private readonly QueryCommand<TResult> _cmd;
    private readonly SqlDataProvider _dataProvider;
    private readonly DbCommand _sqlCommand;
    private readonly CancellationToken _cancellationToken;
    private DbDataReader? _reader;
    private DbConnection? _conn;

    public ResultSetEnumerator(QueryCommand<TResult> cmd, SqlDataProvider dataProvider, DbCommand sqlCommand, CancellationToken cancellationToken)
    {
        _cmd = cmd;
        _dataProvider = dataProvider;
        _sqlCommand = sqlCommand;
        _cancellationToken = cancellationToken;
    }
    public TResult Current => _cmd.Map(_reader!);
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (_reader is not null)
        {
            if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Disposing data reader");
            await _reader.DisposeAsync();
        }

        // if (_conn is not null)
        // {
        //     if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Disposing connection");
        //     await _conn.DisposeAsync();
        // }
    }
    public async ValueTask<bool> MoveNextAsync()
    {
        if (_conn is null)
        {
            if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Creating connection");

            _conn = _dataProvider.GetConnection();
            _sqlCommand.Connection = _conn;

            if (_conn.State == ConnectionState.Closed)
            {
                if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Opening connection");

                await _conn.OpenAsync(_cancellationToken);
            }

            if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false)
            {
                _dataProvider.Logger.LogDebug(_sqlCommand.CommandText);

                if (_dataProvider.LogSensetiveData)
                {
                    foreach (DbParameter p in _sqlCommand.Parameters)
                    {
                        _dataProvider.Logger.LogDebug("param {name} is {value}", p.ParameterName, p.Value);
                    }
                }
                else if (_sqlCommand.Parameters?.Count > 0)
                {
                    _dataProvider.Logger.LogDebug("Use {method} to see param values", nameof(_dataProvider.LogSensetiveData));
                }
            }

            _reader = await _sqlCommand.ExecuteReaderAsync(_cancellationToken);
        }

        if (_dataProvider.Logger?.IsEnabled(LogLevel.Trace) ?? false) _dataProvider.Logger.LogTrace("Move next");

        return await _reader!.ReadAsync(_cancellationToken);
    }
}
