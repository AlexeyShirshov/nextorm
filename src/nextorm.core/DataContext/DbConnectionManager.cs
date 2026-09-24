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
    private readonly Func<bool> _supportsTransactions;
    private readonly InterceptorHooks _interceptors;

    private DbConnection? _conn;
    private bool _connWasCreatedByMe;
    private DbTransaction? _transaction;
    private bool _transactionOwnedByMe;

    internal DbConnectionManager(
        IDataContext owner,
        ConnectionHooks hooks,
        string? connectionString,
        DbConnection? providedConnection,
        LoggingOptions logging,
        Func<bool> supportsTransactions,
        InterceptorHooks interceptors)
    {
        _owner = owner;
        _createDbConnection = hooks.CreateDbConnection;
        _onConnectionCreated = hooks.OnConnectionCreated;
        _connectionString = connectionString;
        _providedConnection = providedConnection;
        _logger = logging.Logger;
        _logSensitiveData = logging.LogSensitiveData;
        _supportsTransactions = supportsTransactions;
        _interceptors = interceptors;
    }

    public void EnsureConnectionOpen()
    {
        var conn = GetConnection();
        if (conn.State == ConnectionState.Closed)
        {
            var interceptors = _interceptors.ConnectionInterceptors;
            var eventData = default(ConnectionEventData);
            if (interceptors.Length != 0)
            {
                eventData = new ConnectionEventData(_owner, conn);
                InterceptorHooks.RaiseConnectionOpening(interceptors, eventData);
            }

            if (_logger?.IsEnabled(LogLevel.Debug) ?? false) _logger.LogDebug("Opening connection");
            conn.Open();

            if (interceptors.Length != 0)
                InterceptorHooks.RaiseConnectionOpened(interceptors, eventData);
        }
    }

    public async Task EnsureConnectionOpenAsync(CancellationToken cancellationToken = default)
    {
        var conn = GetConnection();
        if (conn.State == ConnectionState.Closed)
        {
            var interceptors = _interceptors.ConnectionInterceptors;
            var eventData = default(ConnectionEventData);
            if (interceptors.Length != 0)
            {
                eventData = new ConnectionEventData(_owner, conn);
                InterceptorHooks.RaiseConnectionOpening(interceptors, eventData);
            }

            if (_logger?.IsEnabled(LogLevel.Debug) ?? false) _logger.LogDebug("Opening connection");
            await conn.OpenAsync(cancellationToken);

            if (interceptors.Length != 0)
                InterceptorHooks.RaiseConnectionOpened(interceptors, eventData);
        }
    }

    /// <summary>
    /// The active transaction, or <see langword="null"/> when none is enlisted. A transaction that has
    /// already been committed, rolled back or disposed (its <see cref="DbTransaction.Connection"/> is
    /// gone) is dropped lazily so it is no longer bound to subsequent commands.
    /// </summary>
    public DbTransaction? CurrentTransaction
    {
        get
        {
            if (_transaction is not null && IsTransactionCompleted(_transaction))
                DisposeTransaction();

            return _transaction;
        }
    }

    // Providers disagree on how a completed transaction reports itself: most expose a null Connection,
    // while Npgsql throws ObjectDisposedException from the getter. Both mean "no longer usable".
    private static bool IsTransactionCompleted(DbTransaction transaction)
    {
        try
        {
            return transaction.Connection is null;
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    /// <summary>Starts a transaction on the context's connection with the provider's default isolation level.</summary>
    /// <returns>The started transaction.</returns>
    public DbTransaction BeginTransaction() => BeginTransaction(IsolationLevel.Unspecified);

    /// <summary>Starts a transaction on the context's connection with the given isolation level.</summary>
    /// <param name="isolationLevel">The isolation level to request from the provider; <see cref="IsolationLevel.Unspecified"/> uses the provider default.</param>
    /// <returns>The started transaction.</returns>
    public DbTransaction BeginTransaction(IsolationLevel isolationLevel)
    {
        EnsureTransactionsSupported();
        EnsureNoActiveTransaction();

        EnsureConnectionOpen();
        var conn = GetConnection();
        _transaction = isolationLevel == IsolationLevel.Unspecified
            ? conn.BeginTransaction()
            : conn.BeginTransaction(isolationLevel);
        _transactionOwnedByMe = true;
        return _transaction;
    }

    /// <summary>Asynchronously starts a transaction on the context's connection with the provider's default isolation level.</summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task producing the started transaction.</returns>
    public Task<DbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => BeginTransactionAsync(IsolationLevel.Unspecified, cancellationToken);

    /// <summary>Asynchronously starts a transaction on the context's connection with the given isolation level.</summary>
    /// <param name="isolationLevel">The isolation level to request from the provider; <see cref="IsolationLevel.Unspecified"/> uses the provider default.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task producing the started transaction.</returns>
    public async Task<DbTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
    {
        EnsureTransactionsSupported();
        EnsureNoActiveTransaction();

        await EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);
        var conn = GetConnection();
        _transaction = isolationLevel == IsolationLevel.Unspecified
            ? await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : await conn.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);
        _transactionOwnedByMe = true;
        return _transaction;
    }

    /// <summary>
    /// Enlists an externally owned transaction (the context only binds it and never commits, rolls back
    /// or disposes it); pass <see langword="null"/> to detach. The transaction must belong to this
    /// context's connection.
    /// </summary>
    /// <param name="transaction">The transaction to enlist, or <see langword="null"/> to detach.</param>
    public void UseTransaction(DbTransaction? transaction)
    {
        if (transaction is null)
        {
            // Detaching is only meaningful for a caller-supplied transaction. A context-owned one must
            // be finished by the caller (commit/rollback/dispose); silently dropping it would leave the
            // connection in a pending transaction and make the next command fail (issue #32).
            if (CurrentTransaction is not null && _transactionOwnedByMe)
                throw new InvalidOperationException("The active transaction was started by this context; commit, roll back or dispose it instead of detaching it with UseTransaction(null).");

            _transaction = null;
            _transactionOwnedByMe = false;
            return;
        }

        EnsureTransactionsSupported();
        EnsureNoActiveTransaction();

        var conn = GetConnection();
        if (!ReferenceEquals(transaction.Connection, conn))
            throw new InvalidOperationException("The transaction belongs to a different connection than this context.");

        _transaction = transaction;
        _transactionOwnedByMe = false;
    }

    private void EnsureTransactionsSupported()
    {
        if (!_supportsTransactions())
            throw new NotSupportedException("This provider does not support transactions: its connection has no ADO.NET transaction.");
    }

    private void EnsureNoActiveTransaction()
    {
        if (CurrentTransaction is not null)
            throw new InvalidOperationException("A transaction is already in progress on this context. Nested transactions are not supported; use the active transaction instead.");
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

            // The connection is gone, so the transaction bound to it is dead: drop the reference
            // without disposing. Disposing it here would touch a disposed connection, and the provider
            // has already aborted the transaction when its connection went away.
            _transaction = null;
            _transactionOwnedByMe = false;

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

        // A context-owned transaction is rolled back by Dispose before the connection goes away; a
        // caller-supplied one is only unbound (the caller owns its lifetime).
        DisposeTransaction();

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

    private void DisposeTransaction()
    {
        if (_transaction is not null && _transactionOwnedByMe)
            _transaction.Dispose();

        _transaction = null;
        _transactionOwnedByMe = false;
    }
}
