using FluentAssertions;
using NextORM.Core;
using NextORM.MySql;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// Session/server identity functions (T219 / #219) on MariaDB (no database): the capability keys every provider
/// answers through <see cref="ISessionInfoFunctions"/> and the expression-level SQL generation for the
/// 15 functions added by the task, plus the legacy session family as a regression.
/// </summary>
public class SessionIdentitySqlGenerationTests
{
    private static ISessionInfoFunctions Session => MariaDbDialect.Instance.SessionInfoFunctions!;

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static string MethodName(string key)
    {
        var dot = key.LastIndexOf('.');
        return dot < 0 ? key : key[(dot + 1)..];
    }

    /// <summary>Every identity/session key the provider reports as supported renders; the others fail closed.</summary>
    [Theory]
    [InlineData("sqlserver.server_name", null)]
    [InlineData("sqlserver.host_id", null)]
    [InlineData("sqlserver.host_name", null)]
    [InlineData("sqlserver.app_name", null)]
    [InlineData("sqlserver.suser_name", null)]
    [InlineData("sqlserver.original_login", null)]
    [InlineData("pg_backend_pid", null)]
    [InlineData("inet_server_addr", null)]
    [InlineData("inet_server_port", null)]
    [InlineData("inet_client_addr", null)]
    [InlineData("inet_client_port", null)]
    [InlineData("connection_id", "CAST(CONNECTION_ID() AS UNSIGNED)")]
    [InlineData("user", "USER()")]
    [InlineData("clickhouse.host_name", null)]
    [InlineData("clickhouse.tcp_port", null)]
    [InlineData("current_user", "current_user()")]
    [InlineData("session_user", "session_user()")]
    [InlineData("current_schema", "schema()")]
    [InlineData("current_database", "database()")]
    [InlineData("version", "version()")]
    public void SessionInfoKeys_ShouldRenderNativeSqlOrFailClosed(string key, string? expected)
    {
        if (expected is null)
        {
            Session.Supports(key).Should().BeFalse($"MariaDB does not implement {key}");
            var render = () => Session.Render(key);
            render.Should().Throw<NotSupportedException>().WithMessage($"*{key}*");
            return;
        }

        Session.Supports(key).Should().BeTrue();
        Session.Render(key).Should().Be(expected);
    }

    /// <summary>The expression-level path: every added function emits the native SQL or throws before any SQL.</summary>
    [Theory]
    [InlineData("sqlserver.server_name", null)]
    [InlineData("sqlserver.host_id", null)]
    [InlineData("sqlserver.host_name", null)]
    [InlineData("sqlserver.app_name", null)]
    [InlineData("sqlserver.suser_name", null)]
    [InlineData("sqlserver.original_login", null)]
    [InlineData("pg_backend_pid", null)]
    [InlineData("inet_server_addr", null)]
    [InlineData("inet_server_port", null)]
    [InlineData("inet_client_addr", null)]
    [InlineData("inet_client_port", null)]
    [InlineData("connection_id", "CAST(CONNECTION_ID() AS UNSIGNED)")]
    [InlineData("user", "USER()")]
    [InlineData("clickhouse.host_name", null)]
    [InlineData("clickhouse.tcp_port", null)]
    public void NewIdentityFunctions_ShouldGenerateSqlOrFailClosed(string key, string? expected)
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        Func<string> act = key switch
        {
            "sqlserver.server_name" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.server_name() })),
            "sqlserver.host_id" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.host_id() })),
            "sqlserver.host_name" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.host_name() })),
            "sqlserver.app_name" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.app_name() })),
            "sqlserver.suser_name" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.suser_name() })),
            "sqlserver.original_login" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.original_login() })),
            "pg_backend_pid" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.pg_backend_pid() })),
            "inet_server_addr" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.inet_server_addr() })),
            "inet_server_port" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.inet_server_port() })),
            "inet_client_addr" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.inet_client_addr() })),
            "inet_client_port" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.inet_client_port() })),
            "connection_id" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.connection_id() })),
            "user" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.user() })),
            "clickhouse.host_name" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.host_name() })),
            "clickhouse.tcp_port" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.tcp_port() })),
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, null)
        };

        if (expected is null)
            act.Should().Throw<NotSupportedException>().WithMessage($"*{MethodName(key)}*");
        else
            act().Should().Contain(expected);
    }

    /// <summary>MariaDB inherits the MySQL session-info object, identity keys included.</summary>
    [Fact]
    public void SessionInfoFunctions_ShouldBeTheInheritedMySqlRenderer()
    {
        MariaDbDialect.Instance.SessionInfoFunctions.Should().BeSameAs(MySqlDialect.Instance.SessionInfoFunctions);
        MariaDbDialect.Instance.SessionInfoFunctions!.Render("connection_id").Should().Be("CAST(CONNECTION_ID() AS UNSIGNED)");
        MariaDbDialect.Instance.SessionInfoFunctions!.Render("user").Should().Be("USER()");
    }
}
