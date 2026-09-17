using System.Data.Common;

namespace nextorm.core;

/// <summary>
/// Owns the lifecycle of the underlying database connection.
/// Deliberately not part of <see cref="IDataContext"/>: a provider without a connection
/// (the in-memory context) must not be forced to implement it as a no-op.
/// </summary>
public interface IConnectionManager
{
    /// <summary>Returns the connection in use, creating it on first access.</summary>
    DbConnection GetConnection();
    void EnsureConnectionOpen();
    Task EnsureConnectionOpenAsync();
}
