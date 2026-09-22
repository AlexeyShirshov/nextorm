using System.Data.Common;

namespace NextORM.Core;

/// <summary>
/// Owns the lifecycle of the underlying database connection.
/// Deliberately not part of <see cref="IDataContext"/>: a provider without a connection
/// (the in-memory context) must not be forced to implement it as a no-op.
/// </summary>
public interface IConnectionManager
{
    /// <summary>Returns the connection in use, creating it on first access.</summary>
    DbConnection GetConnection();
    /// <summary>
    /// Opens the connection if it is closed, blocking until it is open or the open fails.
    /// </summary>
    void EnsureConnectionOpen();
    /// <summary>
    /// Opens the connection if it is closed. The token is honoured while opening, so a caller that
    /// starts reading a result set can still cancel a slow connection open.
    /// </summary>
    Task EnsureConnectionOpenAsync(CancellationToken cancellationToken = default);
}
