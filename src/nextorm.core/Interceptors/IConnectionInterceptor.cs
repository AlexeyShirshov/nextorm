using System.Data.Common;

namespace NextORM.Core;

/// <summary>
/// Metadata about a connection as it is opened. Created by the context's connection axis and passed
/// to the interceptors registered for it.
/// </summary>
/// <param name="DataContext">The context that owns the connection.</param>
/// <param name="Connection">The connection being opened.</param>
public readonly record struct ConnectionEventData(IDataContext DataContext, DbConnection Connection);

/// <summary>
/// Observes the connection lifecycle of a database-backed context.
/// </summary>
/// <remarks>
/// Register implementations with <c>DataContextBuilder.AddInterceptor</c> (applies to every context the
/// builder creates) or <c>DataContext.AddInterceptor</c> (per context instance). Interceptors are
/// invoked in registration order and must be thread-safe, since a context may open its connection
/// concurrently.
/// </remarks>
public interface IConnectionInterceptor
{
    /// <summary>Called immediately before the connection is opened.</summary>
    /// <param name="eventData">Metadata about the connection.</param>
    void ConnectionOpening(ConnectionEventData eventData) { }

    /// <summary>Called after the connection has been opened.</summary>
    /// <param name="eventData">Metadata about the connection.</param>
    void ConnectionOpened(ConnectionEventData eventData) { }
}
