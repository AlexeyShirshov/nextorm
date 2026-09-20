using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;

namespace NextORM.Core;

/// <summary>
/// Connection axis of a database-backed context: owns the connection state machine (lazy creation,
/// ownership tracking, opening and teardown) and implements <see cref="IConnectionManager"/>.
/// Extracted out of <c>DataContext</c> so the context no longer owns the connection algorithm (SRP).
/// The provider keeps its hooks (<c>CreateDbConnection</c> / <c>OnConnectionCreated</c>) on the
/// context; this type receives them as delegates, and the context itself only as the
/// <see cref="IDataContext"/> role it already exposes (used to reset cached plans).
/// </summary>
internal sealed class DbConnectionManager : IConnectionManager
{
    private readonly IDataContext _owner;
    private readonly Func<string?, DbConnection> _createDbConnection;
    private readonly Action<DbConnection> _onConnectionCreated;
    private readonly string? _connectionString;
    private readonly DbConnection? _providedConnection;
    private readonly ILogger? _logger;
    private readonly bool _logSensitiveData;

    private DbConnection? _conn;
    private bool _connWasCreatedByMe;

    internal DbConnectionManager(
        IDataContext owner,
        ConnectionHooks hooks,
        string? connectionString,
        DbConnection? providedConnection,
        LoggingOptions logging)
    {
        _owner = owner;
        _createDbConnection = hooks.CreateDbConnection;
        _onConnectionCreated = hooks.OnConnectionCreated;
        _connectionString = connectionString;
        _providedConnection = providedConnection;
        _logger = logging.Logger;
        _logSensitiveData = logging.LogSensitiveData;
    }

    public void EnsureConnectionOpen()
    {
        var conn = GetConnection();
        if (conn.State == ConnectionState.Closed)
        {
            if (_logger?.IsEnabled(LogLevel.Debug) ?? false) _logger.LogDebug("Opening connection");
            conn.Open();
        }
    }

    public async Task EnsureConnectionOpenAsync(CancellationToken cancellationToken = default)
    {
        var conn = GetConnection();
        if (conn.State == ConnectionState.Closed)
        {
            if (_logger?.IsEnabled(LogLevel.Debug) ?? false) _logger.LogDebug("Opening connection");
            await conn.OpenAsync(cancellationToken);
        }
    }

    public DbConnection GetConnection()
    {
        if (_conn is null)
        {
            if (_logger?.IsEnabled(LogLevel.Debug) ?? false) _logger.LogDebug("Getting connection");
            _connWasCreatedByMe = true;
            _conn = CreateConnection();
            if (!_connWasCreatedByMe)
                _conn.Disposed += ConnDisposed;
        }

        return _conn;
    }

    /// <summary>
    /// Resolves the connection to use, creating it on first access. The shared skeleton — logging,
    /// the caller-supplied connection, hook invocation and ownership tracking — lives here; a provider
    /// only supplies the concrete connection via <c>CreateDbConnection</c> and, if it needs to,
    /// initialises it in <c>OnConnectionCreated</c>.
    /// </summary>
    private DbConnection CreateConnection()
    {
        if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
        {
            if (_logSensitiveData)
                _logger.LogDebug("Creating connection with {connStr}", _connectionString);
            else
                _logger.LogDebug("Creating connection");
        }

        if (_providedConnection is not null)
        {
            // Caller-owned: the context must not dispose it at shutdown.
            _connWasCreatedByMe = false;
            _onConnectionCreated(_providedConnection);
            return _providedConnection;
        }

        var conn = _createDbConnection(_connectionString);
        _onConnectionCreated(conn);
        return conn;
    }

    private void ConnDisposed(object? sender, EventArgs e)
    {
        if (_logger?.IsEnabled(LogLevel.Debug) ?? false) _logger.LogDebug("Connection disposed");
        if (sender is DbConnection conn)
        {
            conn.Disposed -= ConnDisposed;

            foreach (var cached in QueryPlanStore.Values)
            {
                cached.ResetConnection(conn, _owner);
            }
        }
    }

    /// <summary>The connection string of the supplied connection, or the one the context was built with.</summary>
    public string ConnectionString => string.IsNullOrEmpty(_connectionString)
        ? _providedConnection!.ConnectionString
        : _connectionString!;

    /// <summary>
    /// Tears down the connection this manager created, if any. A caller-supplied connection is left
    /// alone (the caller owns it) and only unsubscribed from, and cached plans are detached from the
    /// connection first.
    /// </summary>
    internal void DisposeConnection()
    {
        if (_conn is null)
            return;

        foreach (var cached in QueryPlanStore.Values)
        {
            cached.ResetConnection(_conn, _owner);
        }

        if (_connWasCreatedByMe)
        {
            if (_logger?.IsEnabled(LogLevel.Debug) ?? false) _logger.LogDebug("Dispose connection");
            _conn.Dispose();
        }
        else
        {
            // Caller-owned: do not dispose it, but drop the subscription so a long-lived connection
            // does not keep this manager (and the disposed context) reachable until the caller
            // eventually disposes the connection.
            _conn.Disposed -= ConnDisposed;
        }

        _conn = null;
    }
}
