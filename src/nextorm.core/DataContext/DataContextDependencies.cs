using Microsoft.Extensions.Logging;
using System.Data.Common;
using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// The logging switches shared by the connection, execution and planning axes, bundled so each
/// constructor takes one value instead of a logger plus several booleans.
/// </summary>
internal readonly record struct LoggingOptions(
    ILogger? Logger,
    ILogger? ResultSetEnumeratorLogger = null,
    bool LogSensitiveData = false,
    bool LogParams = false);

/// <summary>
/// The provider hooks the planning axis invokes lazily. They are passed as delegates (not the
/// context) so the planner never depends on the concrete <see cref="DataContext"/>.
/// </summary>
internal readonly record struct ProviderHooks(
    Func<SelectExpression, Expression, Expression> MapColumn,
    Func<string, object?, DbParameter> CreateParam,
    Func<string, DbCommand> CreateCommand);

/// <summary>
/// The provider hooks the connection axis invokes: how to create a connection and how to initialise
/// one just created.
/// </summary>
internal readonly record struct ConnectionHooks(
    Func<string?, DbConnection> CreateDbConnection,
    Action<DbConnection> OnConnectionCreated);

/// <summary>
/// The interceptors registered for a context, grouped by the lifecycle they observe. A context
/// creates one instance from its builder options and shares it with the connection, planning and
/// execution axes, so a per-instance <c>AddInterceptor</c> is seen by all of them.
/// </summary>
/// <remarks>
/// The lists are copy-on-write and read through <see cref="Volatile"/> so a <c>DataContext.AddInterceptor</c>
/// racing an execution never tears: a reader observes either the old or the new array. Appending is
/// rare, so the copy is not on the hot path.
/// </remarks>
internal sealed class InterceptorHooks
{
    private readonly object _sync = new();
    private IQueryInterceptor[] _query;
    private IConnectionInterceptor[] _connection;

    internal InterceptorHooks(IQueryInterceptor[] query, IConnectionInterceptor[] connection)
    {
        _query = query;
        _connection = connection;
    }

    internal IQueryInterceptor[] QueryInterceptors => Volatile.Read(ref _query);

    internal IConnectionInterceptor[] ConnectionInterceptors => Volatile.Read(ref _connection);

    internal void Add(IQueryInterceptor interceptor)
    {
        lock (_sync)
        {
            var next = new IQueryInterceptor[_query.Length + 1];
            Array.Copy(_query, next, _query.Length);
            next[^1] = interceptor;
            Volatile.Write(ref _query, next);
        }
    }

    internal void Add(IConnectionInterceptor interceptor)
    {
        lock (_sync)
        {
            var next = new IConnectionInterceptor[_connection.Length + 1];
            Array.Copy(_connection, next, _connection.Length);
            next[^1] = interceptor;
            Volatile.Write(ref _connection, next);
        }
    }

    // The event loops are centralised here so the execution, streaming and planning axes share one
    // implementation. The callers keep the empty-array check so the no-interceptor path stays free
    // of event-data construction, timestamps and delegate calls.

    internal static void RaiseCommandInitialized(IQueryInterceptor[] interceptors, IDataContext context, DbCommand command)
    {
        var eventData = new CommandEventData(context, command.CommandText);
        for (var i = 0; i < interceptors.Length; i++)
            interceptors[i].CommandInitialized(eventData, command);
    }

    internal static void RaiseCommandExecuting(IQueryInterceptor[] interceptors, IDataContext context, DbCommand command)
    {
        var eventData = new CommandEventData(context, command.CommandText);
        for (var i = 0; i < interceptors.Length; i++)
            interceptors[i].CommandExecuting(eventData, command);
    }

    internal static void RaiseCommandExecuted(IQueryInterceptor[] interceptors, IDataContext context, DbCommand command, TimeSpan elapsed)
    {
        var eventData = new CommandEventData(context, command.CommandText);
        for (var i = 0; i < interceptors.Length; i++)
            interceptors[i].CommandExecuted(eventData, command, elapsed);
    }

    internal static void RaiseCommandFailed(IQueryInterceptor[] interceptors, IDataContext context, DbCommand command, Exception exception)
    {
        var eventData = new CommandEventData(context, command.CommandText);
        for (var i = 0; i < interceptors.Length; i++)
            interceptors[i].CommandFailed(eventData, command, exception);
    }

    internal static void RaiseConnectionOpening(IConnectionInterceptor[] interceptors, in ConnectionEventData eventData)
    {
        for (var i = 0; i < interceptors.Length; i++)
            interceptors[i].ConnectionOpening(eventData);
    }

    internal static void RaiseConnectionOpened(IConnectionInterceptor[] interceptors, in ConnectionEventData eventData)
    {
        for (var i = 0; i < interceptors.Length; i++)
            interceptors[i].ConnectionOpened(eventData);
    }
}
