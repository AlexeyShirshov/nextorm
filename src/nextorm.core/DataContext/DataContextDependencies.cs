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
