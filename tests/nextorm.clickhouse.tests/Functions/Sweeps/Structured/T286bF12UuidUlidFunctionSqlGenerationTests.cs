using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.SqlServer;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// T286b D12 (F12 UUID/ULID): the ClickHouse-only UUID, ULID and Snowflake members. The tests pin
/// every rendered arm, the exact CLR representations (<see cref="Guid"/> for the native UUID type, an
/// explicit 32-hex-digit string for the binary 16-byte form, <see cref="ulong"/> for the Snowflake id,
/// <see cref="DateTime"/> for the DateTime64(3) timestamps), the DateTime64 wrapper the server requires
/// for <c>dateTime64ToSnowflakeID</c>, the declared nullability, and the fail-closed provider guard.
/// They never open a connection.
/// </summary>
public class T286bF12UuidUlidFunctionSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [SqlTable("t286b_f12_entity")]
    internal interface IT286bUuidEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("text")]
        string? Text { get; set; }

        [Column("u64")]
        ulong U64 { get; set; }

        [Column("uuid")]
        Guid Uuid { get; set; }

        [Column("at")]
        DateTime At { get; set; }
    }

    private static readonly string[] DeliveredNames =
    [
        "uuid_num_to_string", "uuid_string_to_num", "uuid_to_num", "uuid_v7_to_datetime",
        "to_uuid", "to_uuid_or_default", "to_uuid_or_null", "ulid_string_to_datetime",
        "generate_snowflake_id", "date_time_to_snowflake_id", "date_time64_to_snowflake_id",
        "date_time_to_uuid_v7", "snowflake_id_to_date_time", "snowflake_id_to_date_time64"
    ];

    [Fact]
    public void Render_ShouldEmitTheExactClickHouseArmForEveryDeliveredMember()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bUuidEntity>();

        using (new AssertionScope())
        {
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.uuid_num_to_string(x.Text) }))
                .Should().Contain("UUIDNumToString(toFixedString(unhex(text), 16)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.uuid_string_to_num(x.Text) }))
                .Should().Contain("hex(UUIDStringToNum(text)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.uuid_to_num(x.Uuid) }))
                .Should().Contain("hex(UUIDToNum(uuid)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.uuid_v7_to_datetime(x.Uuid) }))
                .Should().Contain("UUIDv7ToDateTime(uuid) as `V`");

            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_uuid(x.Text) }))
                .Should().Contain("toUUID(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_uuid_or_default(x.Text, x.Uuid) }))
                .Should().Contain("toUUIDOrDefault(text, uuid) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_uuid_or_null(x.Text) }))
                .Should().Contain("toUUIDOrNull(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.ulid_string_to_datetime(x.Text) }))
                .Should().Contain("ULIDStringToDateTime(text) as `V`");

            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.generate_snowflake_id() }))
                .Should().Contain("generateSnowflakeID() as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.date_time_to_snowflake_id(x.At) }))
                .Should().Contain("dateTimeToSnowflakeID(at) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.date_time64_to_snowflake_id(x.At) }))
                .Should().Contain("dateTime64ToSnowflakeID(toDateTime64(at, 3)) as `V`",
                    "the server requires DateTime64 and the format carries milliseconds");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.date_time_to_uuid_v7(x.At) }))
                .Should().Contain("dateTimeToUUIDv7(at) as `V`",
                    "dateTimeToUUIDv7 accepts DateTime only, so no DateTime64 wrapper is added");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.snowflake_id_to_date_time(x.U64) }))
                .Should().Contain("snowflakeIDToDateTime(u64) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.snowflake_id_to_date_time64(x.U64) }))
                .Should().Contain("snowflakeIDToDateTime64(u64) as `V`");
        }
    }

    [Fact]
    public void BinaryUuidValues_ShouldBeWrappedInHexOrUnpackedFromHex()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bUuidEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            S = SqlFunctions.ClickHouse.uuid_string_to_num(x.Text),
            N = SqlFunctions.ClickHouse.uuid_to_num(x.Uuid),
            Back = SqlFunctions.ClickHouse.uuid_num_to_string(x.Text)
        }));

        using (new AssertionScope())
        {
            // The binary 16-byte forms are explicit hex text, never a Guid (whose byte order differs)
            // and never a UTF-8-decoded string; the inverse unpacks the same representation.
            sql.Should().Contain("hex(UUIDStringToNum(text)) as `S`");
            sql.Should().Contain("hex(UUIDToNum(uuid)) as `N`");
            sql.Should().Contain("UUIDNumToString(toFixedString(unhex(text), 16)) as `Back`");
            sql.Should().NotContain("UUIDStringToNum(text) as", "the raw FixedString bytes must never be surfaced");
            sql.Should().NotContain("UUIDToNum(uuid) as", "the raw FixedString bytes must never be surfaced");
        }
    }

    [Fact]
    public void DeclaredShapes_ShouldMatchTheNativeResultTypes()
    {
        using (new AssertionScope())
        {
            ReturnType("uuid_num_to_string", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("uuid_string_to_num", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("uuid_to_num", [typeof(Guid?)]).Should().Be(typeof(string));
            ReturnType("uuid_v7_to_datetime", [typeof(Guid?)]).Should().Be(typeof(DateTime?),
                "UUIDv7ToDateTime returns DateTime64(3) and a NULL argument stays NULL");
            ReturnType("to_uuid", [typeof(string)]).Should().Be(typeof(Guid?),
                "the native UUID type maps exactly to System.Guid");
            ReturnType("to_uuid_or_default", [typeof(string), typeof(Guid)]).Should().Be(typeof(Guid?));
            ReturnType("to_uuid_or_null", [typeof(string)]).Should().Be(typeof(Guid?));
            ReturnType("ulid_string_to_datetime", [typeof(string)]).Should().Be(typeof(DateTime?));
            ReturnType("generate_snowflake_id", []).Should().Be(typeof(ulong),
                "generateSnowflakeID returns UInt64");
            ReturnType("date_time_to_snowflake_id", [typeof(DateTime?)]).Should().Be(typeof(ulong?));
            ReturnType("date_time64_to_snowflake_id", [typeof(DateTime?)]).Should().Be(typeof(ulong?));
            ReturnType("date_time_to_uuid_v7", [typeof(DateTime)]).Should().Be(typeof(Guid),
                "the input is non-null DateTime and the UUID result is non-nullable");
            ReturnType("snowflake_id_to_date_time", [typeof(ulong?)]).Should().Be(typeof(DateTime?));
            ReturnType("snowflake_id_to_date_time64", [typeof(ulong?)]).Should().Be(typeof(DateTime?));
        }

        static Type ReturnType(string name, Type[] arguments)
            => typeof(ClickHouseFunctions)
                .GetMethod(name, BindingFlags.Public | BindingFlags.Instance, arguments)!
                .ReturnType;
    }

    [Fact]
    public void TemporalConversions_ShouldUseTheVendorMillisecondForms()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bUuidEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            FromDateTime = SqlFunctions.ClickHouse.date_time_to_snowflake_id(x.At),
            FromDateTime64 = SqlFunctions.ClickHouse.date_time64_to_snowflake_id(x.At),
            ToUuidV7 = SqlFunctions.ClickHouse.date_time_to_uuid_v7(x.At),
            UuidV7Time = SqlFunctions.ClickHouse.uuid_v7_to_datetime(x.Uuid),
            UlidTime = SqlFunctions.ClickHouse.ulid_string_to_datetime(x.Text),
            FlakeTime = SqlFunctions.ClickHouse.snowflake_id_to_date_time(x.U64),
            FlakeTime64 = SqlFunctions.ClickHouse.snowflake_id_to_date_time64(x.U64)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("dateTimeToSnowflakeID(at) as `FromDateTime`");
            sql.Should().Contain("dateTime64ToSnowflakeID(toDateTime64(at, 3)) as `FromDateTime64`");
            sql.Should().Contain("dateTimeToUUIDv7(at) as `ToUuidV7`");
            sql.Should().Contain("UUIDv7ToDateTime(uuid) as `UuidV7Time`");
            sql.Should().Contain("ULIDStringToDateTime(text) as `UlidTime`");
            sql.Should().Contain("snowflakeIDToDateTime(u64) as `FlakeTime`");
            sql.Should().Contain("snowflakeIDToDateTime64(u64) as `FlakeTime64`");
        }
    }

    [Fact]
    public void CapturedValues_ShouldBindAsParameters()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bUuidEntity>();
        var value = "61f0c404-5cb3-11e7-907b-a6006ad3dba0";
        var fallback = Guid.Parse("00000000-0000-0000-0000-000000000001");

        var command = Prepare(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_uuid_or_default(value, fallback) }));

        Normalize(command.DbCommand.CommandText).Should().Contain("toUUIDOrDefault(@value, @fallback) as `V`");
        command.DbCommandParams.Cast<System.Data.Common.DbParameter>().Should().HaveCount(2);

        var numeric = "61F0C4045CB311E7907BA6006AD3DBA0";
        var numericCommand = Prepare(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.uuid_num_to_string(numeric) }));
        Normalize(numericCommand.DbCommand.CommandText)
            .Should().Contain("UUIDNumToString(toFixedString(unhex(@numeric), 16)) as `V`");
        numericCommand.DbCommandParams.Cast<System.Data.Common.DbParameter>().Should().ContainSingle()
            .Which.Value.Should().Be(numeric);
    }

    [Fact]
    public void Projection_ShouldRenderEveryDeliveredMemberInOneSelect()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bUuidEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            NumToText = SqlFunctions.ClickHouse.uuid_num_to_string(x.Text),
            TextToNum = SqlFunctions.ClickHouse.uuid_string_to_num(x.Text),
            UuidToNum = SqlFunctions.ClickHouse.uuid_to_num(x.Uuid),
            V7Time = SqlFunctions.ClickHouse.uuid_v7_to_datetime(x.Uuid),
            Parsed = SqlFunctions.ClickHouse.to_uuid(x.Text),
            Defaulted = SqlFunctions.ClickHouse.to_uuid_or_default(x.Text, x.Uuid),
            OrNull = SqlFunctions.ClickHouse.to_uuid_or_null(x.Text),
            UlidTime = SqlFunctions.ClickHouse.ulid_string_to_datetime(x.Text),
            Generated = SqlFunctions.ClickHouse.generate_snowflake_id(),
            FromDateTime = SqlFunctions.ClickHouse.date_time_to_snowflake_id(x.At),
            FromDateTime64 = SqlFunctions.ClickHouse.date_time64_to_snowflake_id(x.At),
            ToUuidV7 = SqlFunctions.ClickHouse.date_time_to_uuid_v7(x.At),
            FlakeTime = SqlFunctions.ClickHouse.snowflake_id_to_date_time(x.U64),
            FlakeTime64 = SqlFunctions.ClickHouse.snowflake_id_to_date_time64(x.U64)
        }));

        using (new AssertionScope())
        {
            foreach (var alias in new[]
                     {
                         "NumToText", "TextToNum", "UuidToNum", "V7Time", "Parsed", "Defaulted",
                         "OrNull", "UlidTime", "Generated", "FromDateTime", "FromDateTime64",
                         "ToUuidV7", "FlakeTime", "FlakeTime64"
                     })
                sql.Should().Contain($"as `{alias}`");
        }
    }

    [Fact]
    public void PostgresAndSqlServer_ShouldRejectEveryDeliveredMember()
    {
        using var postgres = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        using var sqlServer = new SqlServerDataContext(
            "Server=localhost;Database=nextorm;User Id=sa;Password=nextorm;TrustServerCertificate=True",
            new DataContextBuilder());

        using (new AssertionScope())
        {
            foreach (var name in DeliveredNames)
            {
                AssertRejected(postgres, name);
                AssertRejected(sqlServer, name);
            }
        }

        static void AssertRejected(IDataContext ctx, string name)
        {
            Func<string> query = name switch
            {
                "uuid_num_to_string" => () => SqlOf(ctx, ctx.From<IT286bUuidEntity>().Select(x => new { V = SqlFunctions.ClickHouse.uuid_num_to_string(x.Text) })),
                "uuid_string_to_num" => () => SqlOf(ctx, ctx.From<IT286bUuidEntity>().Select(x => new { V = SqlFunctions.ClickHouse.uuid_string_to_num(x.Text) })),
                "uuid_to_num" => () => SqlOf(ctx, ctx.From<IT286bUuidEntity>().Select(x => new { V = SqlFunctions.ClickHouse.uuid_to_num(x.Uuid) })),
                "uuid_v7_to_datetime" => () => SqlOf(ctx, ctx.From<IT286bUuidEntity>().Select(x => new { V = SqlFunctions.ClickHouse.uuid_v7_to_datetime(x.Uuid) })),
                "to_uuid" => () => SqlOf(ctx, ctx.From<IT286bUuidEntity>().Select(x => new { V = SqlFunctions.ClickHouse.to_uuid(x.Text) })),
                "to_uuid_or_default" => () => SqlOf(ctx, ctx.From<IT286bUuidEntity>().Select(x => new { V = SqlFunctions.ClickHouse.to_uuid_or_default(x.Text, x.Uuid) })),
                "to_uuid_or_null" => () => SqlOf(ctx, ctx.From<IT286bUuidEntity>().Select(x => new { V = SqlFunctions.ClickHouse.to_uuid_or_null(x.Text) })),
                "ulid_string_to_datetime" => () => SqlOf(ctx, ctx.From<IT286bUuidEntity>().Select(x => new { V = SqlFunctions.ClickHouse.ulid_string_to_datetime(x.Text) })),
                "generate_snowflake_id" => () => SqlOf(ctx, ctx.From<IT286bUuidEntity>().Select(x => new { V = SqlFunctions.ClickHouse.generate_snowflake_id() })),
                "date_time_to_snowflake_id" => () => SqlOf(ctx, ctx.From<IT286bUuidEntity>().Select(x => new { V = SqlFunctions.ClickHouse.date_time_to_snowflake_id(x.At) })),
                "date_time64_to_snowflake_id" => () => SqlOf(ctx, ctx.From<IT286bUuidEntity>().Select(x => new { V = SqlFunctions.ClickHouse.date_time64_to_snowflake_id(x.At) })),
                "date_time_to_uuid_v7" => () => SqlOf(ctx, ctx.From<IT286bUuidEntity>().Select(x => new { V = SqlFunctions.ClickHouse.date_time_to_uuid_v7(x.At) })),
                "snowflake_id_to_date_time" => () => SqlOf(ctx, ctx.From<IT286bUuidEntity>().Select(x => new { V = SqlFunctions.ClickHouse.snowflake_id_to_date_time(x.U64) })),
                "snowflake_id_to_date_time64" => () => SqlOf(ctx, ctx.From<IT286bUuidEntity>().Select(x => new { V = SqlFunctions.ClickHouse.snowflake_id_to_date_time64(x.U64) })),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
            };

            FluentActions.Invoking(query).Should().Throw<NotSupportedException>(
                "'{0}' is ClickHouse-only and must be refused by every other provider", name)
                .WithMessage("*not supported by this provider*");
        }
    }
}
