using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.MariaDb;
using NextORM.MySql;
using NextORM.Postgres;
using NextORM.SqlServer;

namespace NextORM.Integration.Tests.T282;

[CollectionDefinition("T282 builtin catalog", DisableParallelization = true)]
public sealed class T282BuiltinCatalogCollectionDefinition;

/// <summary>
/// T282 (#282): native reconnaissance probes for the BASE64 and crypto surface. Each arm executes
/// the exact SQL the planned translator is going to emit, on the live server of the engine, and
/// asserts the returned value, the NULL behaviour and the CLR materialisation. The typed-NULL
/// tokens are the decisive part: the probe fails if a bare <c>null</c> argument is not accepted.
/// </summary>
[Collection("T282 builtin catalog")]
public sealed class BuiltinCatalogExecutionTests : IDisposable
{
    private readonly List<IDataContext> _contexts = [];

    public void Dispose()
    {
        foreach (var context in _contexts)
            context.Dispose();
    }

    private IDataContext Open(string provider)
    {
        IDataContext context = provider switch
        {
            "sqlserver" => new SqlServerDataContext(SqlServerContainer.ConnectionString, new DataContextBuilder()),
            "postgres" => new PostgresDataContext(PostgresContainer.ConnectionString, new DataContextBuilder()),
            "mysql" => new MySqlDataContext(MySqlContainer.ConnectionString, new DataContextBuilder()),
            "mariadb" => new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder()),
            _ => new ClickHouseDataContext(ClickHouseContainer.ConnectionString, new DataContextBuilder())
        };

        _contexts.Add(context);
        return context;
    }

    private static void Require(string provider, bool available, string? failure)
    {
        if (ProviderSkipPolicy.IsOptedOut(provider))
            Assert.Skip($"Provider '{provider}' is explicitly opted out: {failure}");
        available.Should().BeTrue($"provider '{provider}' is unavailable ({failure})");
    }

    private static object? Scalar(IDataContext context, string sql)
    {
        var dataContext = (DataContext)context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand(sql);
        var value = command.ExecuteScalar();
        return value is DBNull ? null : value;
    }

    private static string Text(object? value) => value switch
    {
        string text => text,
        byte[] bytes => Encoding.ASCII.GetString(bytes),
        _ => throw new InvalidOperationException($"Unexpected text value type {value?.GetType().FullName ?? "null"}.")
    };

    private static byte[] Bytes(object? value)
    {
        value.Should().NotBeNull();
        return value switch
        {
            byte[] bytes => bytes,
            ReadOnlyMemory<byte> memory => memory.ToArray(),
            _ => throw new InvalidOperationException($"Unexpected binary value type {value!.GetType().FullName}.")
        };
    }

    /// <summary>
    /// D1: the frozen BASE64 and crypto SQL runs natively on every capable engine, including the
    /// typed-NULL argument form and the <c>pgcrypto</c> prerequisite.
    /// </summary>
    [Fact]
    public void T282_Recon_NativeBase64AndCrypto()
    {
        Require("sqlserver", SqlServerTestProvider.Instance.IsAvailable, SqlServerTestProvider.Instance.SkipReason);
        using (var context = Open("sqlserver"))
        {
            Scalar(context, "select BASE64_ENCODE(0x000102FDFEFF)").Should().Be("AAEC/f7/");
            Bytes(Scalar(context, "select BASE64_DECODE('AAEC/f7/')")).Should().Equal(0, 1, 2, 253, 254, 255);
            Scalar(context, "select BASE64_ENCODE(CAST(NULL AS varbinary(8000)))").Should().BeNull();
            Scalar(context, "select BASE64_DECODE(CAST(NULL AS varchar(8000)))").Should().BeNull();
            Scalar(context, "select CASE WHEN CAST(NULL AS int) IS NULL THEN CAST(NULL AS varbinary(8000)) ELSE CRYPT_GEN_RANDOM(CAST(NULL AS int)) END").Should().BeNull();
            Bytes(Scalar(context, "select CRYPT_GEN_RANDOM(1)")).Should().HaveCount(1);
            Bytes(Scalar(context, "select CRYPT_GEN_RANDOM(16)")).Should().HaveCount(16);
            Bytes(Scalar(context, "select CRYPT_GEN_RANDOM(1024)")).Should().HaveCount(1024);
        }

        Require("postgres", PostgresTestProvider.Instance.IsAvailable, PostgresTestProvider.Instance.SkipReason);
        using (var context = Open("postgres"))
        {
            Scalar(context, "CREATE EXTENSION IF NOT EXISTS pgcrypto").Should().BeNull();
            Scalar(context, "select replace(encode(decode('AAEC/f7/', 'base64'), 'base64'), E'\\n', '')").Should().Be("AAEC/f7/");
            Bytes(Scalar(context, "select decode('AAEC/f7/', 'base64')")).Should().Equal(0, 1, 2, 253, 254, 255);
            Scalar(context, "select replace(encode(CAST(NULL AS bytea), 'base64'), E'\\n', '')").Should().BeNull();
            Scalar(context, "select decode(CAST(NULL AS text), 'base64')").Should().BeNull();
            Scalar(context, "select CASE WHEN CAST(NULL AS integer) IS NULL THEN CAST(NULL AS bytea) ELSE gen_random_bytes(CAST(NULL AS integer)) END").Should().BeNull();
            Bytes(Scalar(context, "select gen_random_bytes(16)")).Should().HaveCount(16);
            Bytes(Scalar(context, "select gen_random_bytes(1024)")).Should().HaveCount(1024);
        }

        Require("mysql", MySqlTestProvider.Instance.IsAvailable, MySqlTestProvider.Instance.SkipReason);
        using (var context = Open("mysql"))
        {
            Text(Scalar(context, "select REPLACE(REPLACE(TO_BASE64(0x000102FDFEFF), CHAR(13), ''), CHAR(10), '')")).Should().Be("AAEC/f7/");
            Bytes(Scalar(context, "select FROM_BASE64('AAEC/f7/')")).Should().Equal(0, 1, 2, 253, 254, 255);
            Scalar(context, "select REPLACE(REPLACE(TO_BASE64(CAST(NULL AS BINARY)), CHAR(13), ''), CHAR(10), '')").Should().BeNull();
            Scalar(context, "select FROM_BASE64(CAST(NULL AS CHAR))").Should().BeNull();
            Scalar(context, "select CASE WHEN CAST(NULL AS SIGNED) IS NULL THEN CAST(NULL AS BINARY) ELSE RANDOM_BYTES(CAST(NULL AS SIGNED)) END").Should().BeNull();
            Bytes(Scalar(context, "select RANDOM_BYTES(1)")).Should().HaveCount(1);
            Bytes(Scalar(context, "select RANDOM_BYTES(16)")).Should().HaveCount(16);
            Bytes(Scalar(context, "select RANDOM_BYTES(1024)")).Should().HaveCount(1024);
        }

        Require("mariadb", MariaDbContainer.IsAvailable, MariaDbContainer.Failure);
        using (var context = Open("mariadb"))
        {
            Text(Scalar(context, "select REPLACE(REPLACE(TO_BASE64(0x000102FDFEFF), CHAR(13), ''), CHAR(10), '')")).Should().Be("AAEC/f7/");
            Bytes(Scalar(context, "select FROM_BASE64('AAEC/f7/')")).Should().Equal(0, 1, 2, 253, 254, 255);
            Scalar(context, "select REPLACE(REPLACE(TO_BASE64(CAST(NULL AS BINARY)), CHAR(13), ''), CHAR(10), '')").Should().BeNull();
            Scalar(context, "select FROM_BASE64(CAST(NULL AS CHAR))").Should().BeNull();
            Scalar(context, "select CASE WHEN CAST(NULL AS SIGNED) IS NULL THEN CAST(NULL AS BINARY) ELSE RANDOM_BYTES(CAST(NULL AS SIGNED)) END").Should().BeNull();
            Bytes(Scalar(context, "select RANDOM_BYTES(16)")).Should().HaveCount(16);
        }

        Require("clickhouse", ClickHouseTestProvider.Instance.IsAvailable, ClickHouseTestProvider.Instance.SkipReason);
        using (var context = Open("clickhouse"))
        {
            Scalar(context, "select base64Encode(unhex('000102FDFEFF'))").Should().Be("AAEC/f7/");
            Scalar(context, "select hex(base64Decode('AAEC/f7/'))").Should().Be("000102FDFEFF");
            Scalar(context, "select base64Encode(CAST(NULL AS Nullable(String)))").Should().BeNull();
            Scalar(context, "select base64Decode(CAST(NULL AS Nullable(String)))").Should().BeNull();
        }
    }

    /// <summary>
    /// D6 / V13: a whole entity read behind a deterministic BASE64 predicate keeps the source
    /// entity's own fields and is rendered server-side (the predicate is in the WHERE clause, not
    /// evaluated client-side).
    /// </summary>
    [Fact]
    public void T282_Base64_WholeEntityAfterPredicate()
    {
        Require("sqlserver", SqlServerTestProvider.Instance.IsAvailable, SqlServerTestProvider.Instance.SkipReason);
        SqlServerTestProvider.Instance.EnsureSeeded();

        using var context = Open("sqlserver");
        var query = context.From<BinaryEntity>()
            .Where(x => SqlFunctions.Sql.base64_encode(x.Data) == "AQIDBA==");

        var sql = ((DbPreparedQueryCommand<BinaryEntity>)context.GetPreparedQueryCommand(
            query.ToCommand(), false, false, CancellationToken.None)).DbCommand.CommandText;
        sql.Should().Contain("BASE64_ENCODE");
        sql.ToLowerInvariant().Should().Contain(" where ");

        var rows = query.ToList();
        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(1);
        rows[0].Data.Should().Equal(1, 2, 3, 4);
    }

    private IDataContext OpenVerified(string provider)
    {
        switch (provider)
        {
            case "sqlserver":
                Require(provider, SqlServerTestProvider.Instance.IsAvailable, SqlServerTestProvider.Instance.SkipReason);
                break;
            case "postgres":
                Require(provider, PostgresTestProvider.Instance.IsAvailable, PostgresTestProvider.Instance.SkipReason);
                break;
            case "mysql":
                Require(provider, MySqlTestProvider.Instance.IsAvailable, MySqlTestProvider.Instance.SkipReason);
                break;
            case "mariadb":
                Require(provider, MariaDbContainer.IsAvailable, MariaDbContainer.Failure);
                break;
            default:
                Require(provider, ClickHouseTestProvider.Instance.IsAvailable, ClickHouseTestProvider.Instance.SkipReason);
                break;
        }

        var context = Open(provider);
        if (provider == "postgres")
            Execute(context, "CREATE EXTENSION IF NOT EXISTS pgcrypto");
        return context;
    }

    private static void Execute(IDataContext context, string sql)
    {
        var dataContext = (DataContext)context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private static string[] BinaryFixtureDdl(string provider) => provider switch
    {
        "sqlserver" =>
        [
            "drop table if exists t282_binary",
            "create table t282_binary (id int not null primary key, data varbinary(max) null, txt varchar(100) null, lenval int null)",
            "insert into t282_binary (id, data, txt, lenval) values (1, 0x000102FDFEFF, N'AAEC/f7/', 16), (2, null, null, null), (3, 0x, N'', 1)"
        ],
        "postgres" =>
        [
            "drop table if exists t282_binary",
            "create table t282_binary (id int primary key, data bytea null, txt text null, lenval int null)",
            "insert into t282_binary (id, data, txt, lenval) values (1, '\\x000102fdfeff'::bytea, 'AAEC/f7/', 16), (2, null, null, null), (3, '\\x'::bytea, '', 1)"
        ],
        "mysql" or "mariadb" =>
        [
            "drop table if exists t282_binary",
            "create table t282_binary (id int not null primary key, data varbinary(64) null, txt varchar(100) null, lenval int null)",
            "insert into t282_binary (id, data, txt, lenval) values (1, x'000102FDFEFF', 'AAEC/f7/', 16), (2, null, null, null), (3, x'', '', 1)"
        ],
        _ =>
        [
            "drop table if exists t282_binary",
            "create table t282_binary (id Int32, data Nullable(String), txt Nullable(String), lenval Nullable(Int32)) engine = Memory",
            "insert into t282_binary (id, data, txt, lenval) values (1, unhex('000102FDFEFF'), 'AAEC/f7/', 16), (2, null, null, null), (3, unhex(''), '', 1)"
        ]
    };

    private void SeedBinaryFixture(IDataContext context, string provider)
    {
        foreach (var sql in BinaryFixtureDdl(provider))
            Execute(context, sql);
    }

    private static readonly byte[] RoundtripValue = [0, 1, 2, 253, 254, 255];

    /// <summary>
    /// D6: the public <see cref="CommonFunctions.base64_encode"/> /
    /// <see cref="CommonFunctions.base64_decode"/> pair on every capable engine — a literal/parameter
    /// roundtrip, a nullable binary column, a nullable text column and the typed CLR results.
    /// </summary>
    [Theory]
    [InlineData("sqlserver")]
    [InlineData("postgres")]
    [InlineData("mysql")]
    [InlineData("mariadb")]
    [InlineData("clickhouse")]
    public void T282_Base64_ValueNullColumnsAndRoundtrip(string provider)
    {
        using var context = OpenVerified(provider);

        if (provider == "clickhouse")
        {
            SeedBinaryFixture(context, provider);
            Convert.ToInt64(Scalar(context, "select 1")).Should().Be(1);

            var encode = () => context.From<T282BinaryRow>().Where(x => x.Id == 1)
                .Select(x => new { Value = SqlFunctions.Sql.base64_encode(x.Data) }).ToList();
            encode.Should().Throw<NotSupportedException>()
                .WithMessage("The base64_encode function is not supported by this provider.");

            var decode = () => context.From<T282BinaryRow>().Where(x => x.Id == 1)
                .Select(x => new { Value = SqlFunctions.Sql.base64_decode(x.Text) }).ToList();
            decode.Should().Throw<NotSupportedException>()
                .WithMessage("The base64_decode function is not supported by this provider.");

            return;
        }

        SeedBinaryFixture(context, provider);

        var value = RoundtripValue;

        var literal = context.From<T282BinaryRow>().Where(x => x.Id == 1).Select(x => new
        {
            Encoded = SqlFunctions.Sql.base64_encode(value),
            Roundtrip = SqlFunctions.Sql.base64_decode(SqlFunctions.Sql.base64_encode(value)),
            NullEncoded = SqlFunctions.Sql.base64_encode(null),
            NullDecoded = SqlFunctions.Sql.base64_decode(null)
        }).ToList();

        literal.Should().ContainSingle();
        literal[0].Encoded.Should().Be("AAEC/f7/");
        literal[0].Encoded.Should().BeOfType<string>();
        literal[0].Roundtrip.Should().Equal(value);
        literal[0].Roundtrip.Should().BeOfType<byte[]>();
        literal[0].NullEncoded.Should().BeNull();
        literal[0].NullDecoded.Should().BeNull();

        var columns = context.From<T282BinaryRow>().OrderBy(x => x.Id).Select(x => new
        {
            x.Id,
            Encoded = SqlFunctions.Sql.base64_encode(x.Data),
            Decoded = SqlFunctions.Sql.base64_decode(x.Text)
        }).ToList();

        columns.Should().HaveCount(3);
        columns[0].Encoded.Should().Be("AAEC/f7/");
        columns[0].Decoded.Should().Equal(value);
        columns[1].Encoded.Should().BeNull();
        columns[1].Decoded.Should().BeNull();
        columns[2].Encoded.Should().BeEmpty();
        columns[2].Decoded.Should().BeEmpty();
    }

    /// <summary>
    /// D6: empty and longer-than-76-character inputs stay a single unwrapped BASE64 line and still
    /// roundtrip byte-for-byte on every capable engine.
    /// </summary>
    [Theory]
    [InlineData("sqlserver")]
    [InlineData("postgres")]
    [InlineData("mysql")]
    [InlineData("mariadb")]
    [InlineData("clickhouse")]
    public void T282_Base64_EmptyAndLongInput(string provider)
    {
        using var context = OpenVerified(provider);

        if (provider == "clickhouse")
        {
            SeedBinaryFixture(context, provider);
            Convert.ToInt64(Scalar(context, "select 1")).Should().Be(1);

            var encode = () => context.From<T282BinaryRow>().Where(x => x.Id == 1)
                .Select(x => new { Value = SqlFunctions.Sql.base64_encode(x.Data) }).ToList();
            encode.Should().Throw<NotSupportedException>()
                .WithMessage("The base64_encode function is not supported by this provider.");

            var decode = () => context.From<T282BinaryRow>().Where(x => x.Id == 1)
                .Select(x => new { Value = SqlFunctions.Sql.base64_decode(x.Text) }).ToList();
            decode.Should().Throw<NotSupportedException>()
                .WithMessage("The base64_decode function is not supported by this provider.");

            return;
        }

        SeedBinaryFixture(context, provider);

        var empty = Array.Empty<byte>();
        var longValue = Enumerable.Repeat((byte)0xAB, 120).ToArray();

        var rows = context.From<T282BinaryRow>().Where(x => x.Id == 1).Select(x => new
        {
            Empty = SqlFunctions.Sql.base64_encode(empty),
            EmptyRoundtrip = SqlFunctions.Sql.base64_decode(SqlFunctions.Sql.base64_encode(empty)),
            Long = SqlFunctions.Sql.base64_encode(longValue),
            LongRoundtrip = SqlFunctions.Sql.base64_decode(SqlFunctions.Sql.base64_encode(longValue))
        }).ToList();

        rows.Should().ContainSingle();
        rows[0].Empty.Should().BeEmpty();
        rows[0].EmptyRoundtrip.Should().BeEmpty();
        rows[0].Long.Should().HaveLength(160);
        rows[0].Long.Should().NotContain("\r").And.NotContain("\n");
        rows[0].LongRoundtrip.Should().Equal(longValue);
    }

    /// <summary>
    /// D6: the public <see cref="CommonFunctions.crypt_gen_random"/> on the four engines that declare
    /// it — 1/16/1024-byte values, a NULL literal and a nullable length column, all materialised as
    /// <see cref="byte"/> arrays. PostgreSQL requires the <c>pgcrypto</c> extension, installed by the
    /// fixture.
    /// </summary>
    [Theory]
    [InlineData("sqlserver")]
    [InlineData("postgres")]
    [InlineData("mysql")]
    [InlineData("mariadb")]
    public void T282_Crypto_LengthNullAndMaterialization(string provider)
    {
        using var context = OpenVerified(provider);
        SeedBinaryFixture(context, provider);

        var literal = context.From<T282BinaryRow>().Where(x => x.Id == 1).Select(x => new
        {
            One = SqlFunctions.Sql.crypt_gen_random(1),
            Sixteen = SqlFunctions.Sql.crypt_gen_random(16),
            Long = SqlFunctions.Sql.crypt_gen_random(1024),
            Null = SqlFunctions.Sql.crypt_gen_random(null)
        }).ToList();

        literal.Should().ContainSingle();
        literal[0].One.Should().HaveCount(1).And.BeOfType<byte[]>();
        literal[0].Sixteen.Should().HaveCount(16);
        literal[0].Long.Should().HaveCount(1024);
        literal[0].Null.Should().BeNull();

        var columns = context.From<T282BinaryRow>().OrderBy(x => x.Id).Select(x => new
        {
            x.Id,
            Bytes = SqlFunctions.Sql.crypt_gen_random(x.Length)
        }).ToList();

        columns.Should().HaveCount(3);
        columns[0].Bytes.Should().HaveCount(16);
        columns[1].Bytes.Should().BeNull();
        columns[2].Bytes.Should().HaveCount(1);
    }
}

/// <summary>The T282 probe table shared by the public-API BASE64 and crypto live tests.</summary>
[SqlTable("t282_binary")]
public sealed class T282BinaryRow
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("data")]
    public byte[]? Data { get; set; }

    [Column("txt")]
    public string? Text { get; set; }

    [Column("lenval")]
    public int? Length { get; set; }
}
