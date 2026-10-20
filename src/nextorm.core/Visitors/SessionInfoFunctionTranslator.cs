using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the session/information functions of <see cref="CommonFunctions"/> (<c>current_user</c>,
/// <c>session_user</c>, <c>current_schema</c>, <c>current_database</c>, <c>version</c>, and the shared
/// server/connection identity functions <c>pg_backend_pid</c>, <c>inet_*</c>, <c>connection_id</c> and
/// <c>user</c>) together with the vendor identity members declared on <see cref="SqlServerFunctions"/>
/// and <see cref="ClickHouseFunctions"/>.
/// <para>
/// The family is guarded by <see cref="ISqlDialect.SessionInfoFunctions"/> and each function by
/// <see cref="ISessionInfoFunctions.Supports"/>; the SQL Server members are additionally gated by
/// <see cref="ISqlServerFunctions.SupportsSessionIdentity"/>, so the T-SQL scalar library alone does not
/// enable them. The registry is keyed by the CLR declaring type, which keeps the same-named members of
/// different surfaces apart (<c>host_name</c> is the client host on SQL Server and the server host on
/// ClickHouse). A provider that cannot express a function fails with a clear message before any SQL is
/// emitted; there is no fallback rendering.
/// </para>
/// </summary>
internal static class SessionInfoFunctionTranslator
{
    /// <summary>
    /// The legacy session family declared on <see cref="CommonFunctions"/>; the key is the member name,
    /// unchanged from the pre-existing contract.
    /// </summary>
    private static readonly Dictionary<string, string> LegacyCommonKeys = new(StringComparer.Ordinal)
    {
        [nameof(CommonFunctions.current_user)] = "current_user",
        [nameof(CommonFunctions.session_user)] = "session_user",
        [nameof(CommonFunctions.current_schema)] = "current_schema",
        [nameof(CommonFunctions.current_database)] = "current_database",
        [nameof(CommonFunctions.version)] = "version"
    };

    /// <summary>The shared identity functions added on the <c>SqlFunctions.Sql</c> surface.</summary>
    private static readonly Dictionary<string, string> IdentityCommonKeys = new(StringComparer.Ordinal)
    {
        [nameof(CommonFunctions.pg_backend_pid)] = "pg_backend_pid",
        [nameof(CommonFunctions.inet_server_addr)] = "inet_server_addr",
        [nameof(CommonFunctions.inet_server_port)] = "inet_server_port",
        [nameof(CommonFunctions.inet_client_addr)] = "inet_client_addr",
        [nameof(CommonFunctions.inet_client_port)] = "inet_client_port",
        [nameof(CommonFunctions.connection_id)] = "connection_id",
        [nameof(CommonFunctions.user)] = "user"
    };

    /// <summary>The SQL Server identity members, keyed per vendor on the shared session-info renderer.</summary>
    private static readonly Dictionary<string, string> SqlServerKeys = new(StringComparer.Ordinal)
    {
        [nameof(SqlServerFunctions.server_name)] = "sqlserver.server_name",
        [nameof(SqlServerFunctions.host_id)] = "sqlserver.host_id",
        [nameof(SqlServerFunctions.host_name)] = "sqlserver.host_name",
        [nameof(SqlServerFunctions.app_name)] = "sqlserver.app_name",
        [nameof(SqlServerFunctions.suser_name)] = "sqlserver.suser_name",
        [nameof(SqlServerFunctions.original_login)] = "sqlserver.original_login"
    };

    /// <summary>The ClickHouse identity members, keyed per vendor on the shared session-info renderer.</summary>
    private static readonly Dictionary<string, string> ClickHouseKeys = new(StringComparer.Ordinal)
    {
        [nameof(ClickHouseFunctions.host_name)] = "clickhouse.host_name",
        [nameof(ClickHouseFunctions.tcp_port)] = "clickhouse.tcp_port"
    };

    /// <summary>Translates a session/identity call; returns <c>false</c> when it is not one of them.</summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var declaring = node.Method.DeclaringType;
        string? key;

        if (declaring == typeof(CommonFunctions))
        {
            if (!LegacyCommonKeys.TryGetValue(node.Method.Name, out key)
                && !IdentityCommonKeys.TryGetValue(node.Method.Name, out key))
                return false;
        }
        else if (declaring == typeof(SqlServerFunctions))
        {
            if (!SqlServerKeys.TryGetValue(node.Method.Name, out key))
                return false;

            if (visitor.Dialect.SqlServerFunctions is not { SupportsSessionIdentity: true })
                throw new NotSupportedException($"The {node.Method.Name} function is not supported by this provider.");
        }
        else if (declaring == typeof(ClickHouseFunctions))
        {
            if (!ClickHouseKeys.TryGetValue(node.Method.Name, out key))
                return false;
        }
        else
        {
            return false;
        }

        var dialect = visitor.Dialect;

        if (dialect.SessionInfoFunctions is not { } sessionInfo)
            throw new NotSupportedException("The session information functions are not supported by this provider.");

        if (!sessionInfo.Supports(key))
            throw new NotSupportedException($"The {node.Method.Name} function is not supported by this provider.");

        if (!visitor.IsParamMode)
        {
            visitor.NeedAliasForColumn = true;
            visitor.Builder!.Append(sessionInfo.Render(key));
        }

        return true;
    }

    /// <summary>
    /// True when the member is one of the server/connection identity functions added with the session
    /// family (the legacy <c>current_user</c> family is not included). Callers that cannot evaluate the
    /// family — the in-memory provider — fail closed on it instead of compiling the marker call.
    /// </summary>
    internal static bool IsIdentityFunction(Type? declaringType, string name) =>
        declaringType == typeof(CommonFunctions)
            ? IdentityCommonKeys.ContainsKey(name)
            : declaringType == typeof(SqlServerFunctions)
                ? SqlServerKeys.ContainsKey(name)
                : declaringType == typeof(ClickHouseFunctions) && ClickHouseKeys.ContainsKey(name);
}
