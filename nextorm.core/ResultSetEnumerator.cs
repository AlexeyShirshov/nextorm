using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Logging;

namespace nextorm.core;
public class ResultSetEnumerator<TResult> : IAsyncEnumerator<TResult>
{
    private readonly SqlCommand<TResult> _cmd;
    private readonly SqlClient _sqlClient;
    private readonly DbCommand _sqlCommand;
    private readonly CancellationToken _cancellationToken;
    private DbDataReader? _reader;
    private DbConnection? _conn;

    public ResultSetEnumerator(SqlCommand<TResult> cmd, SqlClient sqlClient, DbCommand sqlCommand, CancellationToken cancellationToken)
    {
        _cmd = cmd;
        _sqlClient = sqlClient;
        _sqlCommand = sqlCommand;
        _cancellationToken = cancellationToken;
    }
    public TResult Current => _cmd.Map(_reader!);
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (_reader is not null)
        {
            if (_sqlClient.Logger?.IsEnabled(LogLevel.Debug) ?? false) _sqlClient.Logger.LogDebug("Disposing data reader");
            await _reader.DisposeAsync();
        }

        if (_conn is not null)
        {
            if (_sqlClient.Logger?.IsEnabled(LogLevel.Debug) ?? false) _sqlClient.Logger.LogDebug("Disposing connection");
            await _conn.DisposeAsync();
        }
    }

    public async ValueTask<bool> MoveNextAsync()
    {
        if (_conn is null)
        {
            if (_sqlClient.Logger?.IsEnabled(LogLevel.Debug) ?? false) _sqlClient.Logger.LogDebug("Creating connection");

            _conn = _sqlClient.CreateConnection();
            _sqlCommand.Connection = _conn;

            if (_conn.State == ConnectionState.Closed)
            {
                if (_sqlClient.Logger?.IsEnabled(LogLevel.Debug) ?? false) _sqlClient.Logger.LogDebug("Opening connection");

                await _conn.OpenAsync(_cancellationToken);
            }

            if (_sqlClient.Logger?.IsEnabled(LogLevel.Debug) ?? false)
            {
                _sqlClient.Logger.LogDebug(_sqlCommand.CommandText);
            }

            _reader = await _sqlCommand.ExecuteReaderAsync(_cancellationToken);
        }

        return await _reader!.ReadAsync(_cancellationToken);
    }
}
