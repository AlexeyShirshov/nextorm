using System.Collections;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Text;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.SqlServer;

namespace NextORM.Core.Tests;

/// <summary>
/// T219 (#219) core coverage for the session/server identity functions: the in-memory fail-closed guard
/// (no local/default evaluation), the scalar materialization of the unsigned <c>connection_id</c> and of
/// a NULL network result, the <see cref="ISqlServerFunctions.SupportsSessionIdentity"/> default-interface
/// contract and the unchanged legacy session family on the provider dialects this project references.
/// </summary>
public class SessionIdentityInMemoryTests
{
    private readonly InMemoryRepository _sut;

    public SessionIdentityInMemoryTests(InMemoryRepository sut)
    {
        _sut = sut;
        _sut.SimpleEntity.WithData(new[] { new SimpleEntity { Id = 1 } });
    }

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    /// <summary>
    /// The in-memory provider has no server/connection identity, so every added function must be refused
    /// before the marker call is compiled against the null sentinel surface.
    /// </summary>
    [Theory]
    [InlineData("sqlserver.server_name")]
    [InlineData("sqlserver.host_id")]
    [InlineData("sqlserver.host_name")]
    [InlineData("sqlserver.app_name")]
    [InlineData("sqlserver.suser_name")]
    [InlineData("sqlserver.original_login")]
    [InlineData("pg_backend_pid")]
    [InlineData("inet_server_addr")]
    [InlineData("inet_server_port")]
    [InlineData("inet_client_addr")]
    [InlineData("inet_client_port")]
    [InlineData("connection_id")]
    [InlineData("user")]
    [InlineData("clickhouse.host_name")]
    [InlineData("clickhouse.tcp_port")]
    public void IdentityFunctions_ShouldFailClosedInMemory(string key)
    {
        Func<object?> act = key switch
        {
            "sqlserver.server_name" => () => _sut.SimpleEntity.Select(_ => SqlFunctions.SqlServer.server_name()).ToList(),
            "sqlserver.host_id" => () => _sut.SimpleEntity.Select(_ => SqlFunctions.SqlServer.host_id()).ToList(),
            "sqlserver.host_name" => () => _sut.SimpleEntity.Select(_ => SqlFunctions.SqlServer.host_name()).ToList(),
            "sqlserver.app_name" => () => _sut.SimpleEntity.Select(_ => SqlFunctions.SqlServer.app_name()).ToList(),
            "sqlserver.suser_name" => () => _sut.SimpleEntity.Select(_ => SqlFunctions.SqlServer.suser_name()).ToList(),
            "sqlserver.original_login" => () => _sut.SimpleEntity.Select(_ => SqlFunctions.SqlServer.original_login()).ToList(),
            "pg_backend_pid" => () => _sut.SimpleEntity.Select(_ => SqlFunctions.Sql.pg_backend_pid()).ToList(),
            "inet_server_addr" => () => _sut.SimpleEntity.Select(_ => SqlFunctions.Sql.inet_server_addr()).ToList(),
            "inet_server_port" => () => _sut.SimpleEntity.Select(_ => SqlFunctions.Sql.inet_server_port()).ToList(),
            "inet_client_addr" => () => _sut.SimpleEntity.Select(_ => SqlFunctions.Sql.inet_client_addr()).ToList(),
            "inet_client_port" => () => _sut.SimpleEntity.Select(_ => SqlFunctions.Sql.inet_client_port()).ToList(),
            "connection_id" => () => _sut.SimpleEntity.Select(_ => SqlFunctions.Sql.connection_id()).ToList(),
            "user" => () => _sut.SimpleEntity.Select(_ => SqlFunctions.Sql.user()).ToList(),
            "clickhouse.host_name" => () => _sut.SimpleEntity.Select(_ => SqlFunctions.ClickHouse.host_name()).ToList(),
            _ => () => _sut.SimpleEntity.Select(_ => SqlFunctions.ClickHouse.tcp_port()).ToList()
        };

        act.Should().Throw<NotSupportedException>($"{key} has no in-memory evaluation");
    }

    /// <summary><c>connection_id()</c> is <see cref="ulong"/>: the reader conversion must keep the full range.</summary>
    [Fact]
    public void ConnectionId_ShouldMaterializeTheWholeUnsignedRange()
    {
        var column = new SelectExpression(typeof(ulong)) { PropertyName = "connection_id", Index = 0 };
        var param = Expression.Parameter(typeof(DbDataReader), "reader");
        var mapper = Expression.Lambda<Func<DbDataReader, ulong>>(RowMapperFactory.MapColumn(column, param), param).Compile();

        mapper(new ScalarReader(ulong.MaxValue)).Should().Be(ulong.MaxValue);
        mapper(new ScalarReader((ulong)0)).Should().Be(0);
        mapper(new ScalarReader((ulong)4_294_967_296)).Should().Be(4_294_967_296);
    }

    /// <summary>A SQL NULL (PostgreSQL <c>inet_*</c> on a unix socket) stays NULL, never an empty default.</summary>
    [Fact]
    public void NetworkAddress_ShouldMaterializeSqlNullAsNull()
    {
        var column = new SelectExpression(typeof(string)) { PropertyName = "inet_server_addr", Index = 0 };
        var param = Expression.Parameter(typeof(DbDataReader), "reader");
        var mapper = Expression.Lambda<Func<DbDataReader, string?>>(RowMapperFactory.MapColumn(column, param), param).Compile();

        mapper(new ScalarReader(DBNull.Value)).Should().BeNull();
        mapper(new ScalarReader(null)).Should().BeNull();
        mapper(new ScalarReader("10.0.0.1")).Should().Be("10.0.0.1");

        var portColumn = new SelectExpression(typeof(int?)) { PropertyName = "inet_client_port", Index = 0 };
        var portMapper = Expression.Lambda<Func<DbDataReader, int?>>(RowMapperFactory.MapColumn(portColumn, param), param).Compile();

        portMapper(new ScalarReader(DBNull.Value)).Should().BeNull();
        portMapper(new ScalarReader(5432)).Should().Be(5432);
    }

    /// <summary>
    /// The SQL Server identity gate is a default interface method (<c>false</c>), so an external
    /// <see cref="ISqlServerFunctions"/> written before the member existed keeps the safe default.
    /// </summary>
    [Fact]
    public void SupportsSessionIdentity_ShouldBeADefaultInterfaceMethod()
    {
        ISqlServerFunctions functions = FlaglessSqlServerFunctions.Instance;
        functions.SupportsSessionIdentity.Should().BeFalse();

        var getter = typeof(ISqlServerFunctions)
            .GetProperty(nameof(ISqlServerFunctions.SupportsSessionIdentity))!
            .GetGetMethod()!;

        getter.IsAbstract.Should().BeFalse();
        getter.IsVirtual.Should().BeTrue();
    }

    /// <summary>
    /// Even when a custom dialect's session-info renderer claims the vendor key, the SQL Server surface
    /// flag alone decides: without it the call fails closed instead of emitting T-SQL.
    /// </summary>
    [Fact]
    public void SqlServerIdentity_WithoutTheSurfaceFlag_ShouldFailClosed()
    {
        using var ctx = new FlaglessIdentityContext();
        var cmd = ctx.From<ISimpleEntity>().Select(_ => SqlFunctions.SqlServer.server_name());

        var act = () => SqlOf(ctx, cmd);

        act.Should().Throw<NotSupportedException>().WithMessage("*server_name*");
    }

    /// <summary>R219-02 regression: the pre-existing session family keeps its rendering.</summary>
    [Fact]
    public void LegacySessionKeys_ShouldBeUnchanged()
    {
        using var pg = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        var pgEntity = pg.From<ISimpleEntity>();
        var pgSql = SqlOf(pg, pgEntity.Select(_ => new
        {
            CU = SqlFunctions.Sql.current_user(),
            SU = SqlFunctions.Sql.session_user(),
            CS = SqlFunctions.Sql.current_schema(),
            CD = SqlFunctions.Sql.current_database(),
            V = SqlFunctions.Sql.version()
        }));

        pgSql.Should().Contain("current_user");
        pgSql.Should().Contain("session_user");
        pgSql.Should().Contain("current_schema");
        pgSql.Should().Contain("current_database()");
        pgSql.Should().Contain("version()");

        using var ss = new SqlServerDataContext("Server=localhost", new DataContextBuilder());
        var ssEntity = ss.From<ISimpleEntity>();
        var ssSql = SqlOf(ss, ssEntity.Select(_ => new
        {
            CU = SqlFunctions.Sql.current_user(),
            CS = SqlFunctions.Sql.current_schema(),
            CD = SqlFunctions.Sql.current_database(),
            V = SqlFunctions.Sql.version()
        }));

        ssSql.Should().Contain("current_user");
        ssSql.Should().Contain("schema_name()");
        ssSql.Should().Contain("db_name()");
        ssSql.Should().Contain("@@version");

        using var ch = new ClickHouseDataContext("Host=localhost", new DataContextBuilder());
        var chEntity = ch.From<ISimpleEntity>();
        var chSql = SqlOf(ch, chEntity.Select(_ => new
        {
            CU = SqlFunctions.Sql.current_user(),
            CD = SqlFunctions.Sql.current_database(),
            V = SqlFunctions.Sql.version()
        }));

        chSql.Should().Contain("currentUser()");
        chSql.Should().Contain("currentDatabase()");
        chSql.Should().Contain("version()");
    }

    /// <summary>A dialect renderer that answers the vendor key without the SQL Server surface flag.</summary>
    private sealed class FlaglessSessionInfoFunctions : ISessionInfoFunctions
    {
        internal static readonly FlaglessSessionInfoFunctions Instance = new();

        public bool Supports(string name) => name == "sqlserver.server_name";

        public string Render(string name) => "@@SERVERNAME";
    }

    /// <summary>An <see cref="ISqlServerFunctions"/> implementation that keeps the default flag.</summary>
    private sealed class FlaglessSqlServerFunctions : ISqlServerFunctions
    {
        internal static readonly FlaglessSqlServerFunctions Instance = new();

        public bool Supports(string name) => name == "sqlserver.server_name";

        public string Render(string name, IReadOnlyList<string> args) => "@@SERVERNAME";
    }

    private sealed class FlaglessIdentityDialect : SqlDialectBase
    {
        internal static readonly FlaglessIdentityDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }

        public override ISessionInfoFunctions? SessionInfoFunctions => FlaglessSessionInfoFunctions.Instance;

        public override ISqlServerFunctions? SqlServerFunctions => FlaglessSqlServerFunctions.Instance;
    }

    private sealed class FlaglessIdentityContext : DataContext
    {
        public FlaglessIdentityContext() : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => FlaglessIdentityDialect.Instance;

        public override DbParameter CreateParam(string name, object? value) => throw new NotSupportedException();

        protected override DbConnection CreateDbConnection(string? connectionString) => throw new NotSupportedException();
    }

    /// <summary>Minimal single-column reader for the scalar materialization contract.</summary>
    private sealed class ScalarReader(object? value) : DbDataReader
    {
        public override int FieldCount => 1;

        public override int Depth => 0;

        public override bool HasRows => true;

        public override bool IsClosed => false;

        public override int RecordsAffected => 0;

        public override object this[int ordinal] => GetValue(ordinal);

        public override object this[string name] => GetValue(0);

        public override bool IsDBNull(int ordinal) => value is null or DBNull;

        public override object GetValue(int ordinal) => value ?? DBNull.Value;

        public override T GetFieldValue<T>(int ordinal) => (T)GetValue(ordinal);

        public override string GetName(int ordinal) => "connection_id";

        public override int GetOrdinal(string name) => 0;

        public override string GetDataTypeName(int ordinal) => "text";

        public override Type GetFieldType(int ordinal) => typeof(string);

        public override int GetValues(object[] values)
        {
            if (values is { Length: > 0 })
                values[0] = GetValue(0);

            return 1;
        }

        public override IEnumerator GetEnumerator()
        {
            yield return this;
        }

        public override bool NextResult() => false;

        public override bool Read() => false;

        public override bool GetBoolean(int ordinal) => throw new NotSupportedException();

        public override byte GetByte(int ordinal) => throw new NotSupportedException();

        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();

        public override char GetChar(int ordinal) => throw new NotSupportedException();

        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();

        public override DateTime GetDateTime(int ordinal) => throw new NotSupportedException();

        public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();

        public override double GetDouble(int ordinal) => throw new NotSupportedException();

        public override float GetFloat(int ordinal) => throw new NotSupportedException();

        public override Guid GetGuid(int ordinal) => throw new NotSupportedException();

        public override short GetInt16(int ordinal) => throw new NotSupportedException();

        public override int GetInt32(int ordinal) => (int)GetValue(ordinal);

        public override long GetInt64(int ordinal) => (long)GetValue(ordinal);

        public override string GetString(int ordinal) => (string)GetValue(ordinal);
    }
}
