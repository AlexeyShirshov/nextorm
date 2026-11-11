using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClickHouse.Driver;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T286b D12 (F12 UUID/ULID): live execution of the ClickHouse-only UUID, ULID and Snowflake members
/// against the pinned <c>25.8-alpine</c> (25.8.33.6) server. The tests pin the exact representations
/// (<see cref="Guid"/> for the native UUID type, 32 hex digits for the binary 16-byte form,
/// <see cref="ulong"/> for the Snowflake id, <see cref="DateTime"/> for the DateTime64(3) timestamps),
/// the textual/binary UUID round trip, the parsing and <c>OrDefault</c>/<c>OrNull</c> distinction,
/// malformed-input outcomes, nil/zero values, the full Snowflake range (including the 32-bit
/// <c>DateTime</c> wrap past 2106), the millisecond precision of the vendor format, the UUIDv7 round
/// trip, and the NULL flow.
/// </summary>
[Collection("ClickHouse")]
public sealed class T286bF12UuidUlidFunctionTests : ProviderTestSuite
{
    /// <summary>
    /// T359 (#349): the fixture table is seeded per test instance. Several tests query
    /// <c>t286b_f12_entity</c> without seeding it themselves (they relied on a sibling test having
    /// run first), which only held while a reused container still carried the table; on a fresh
    /// container the first test in the class failed with <c>Code: 60 UNKNOWN_TABLE</c>. Seeding in the
    /// constructor removes the order dependency without changing what any test asserts.
    /// </summary>
    public T286bF12UuidUlidFunctionTests() => Seed();

    private const string SampleUuid = "61f0c404-5cb3-11e7-907b-a6006ad3dba0";
    private const string SampleUuidHex = "61F0C4045CB311E7907BA6006AD3DBA0";
    private const string SampleUlid = "01ARZ3NDEKTSV4RRFFQ69G5FAV";

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

        [Column("at64")]
        DateTime At64 { get; set; }
    }

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private static T Scalar<T>(string expression)
    {
        using var context = ClickHouseTestProvider.Instance.CreateContext();
        using var result = context.ExecuteRaw($"select {expression} as v");
        return result.Read<T>().Single();
    }

    private static void Seed()
    {
        using var connection = new ClickHouseConnection(ClickHouseContainer.ConnectionString);
        connection.Open();
        foreach (var statement in new[]
                 {
                     "drop table if exists t286b_f12_entity",
                     "create table t286b_f12_entity (id Int32, text Nullable(String), u64 UInt64, "
                     + "uuid UUID, at DateTime, at64 DateTime64(3)) engine = Memory",
                     "insert into t286b_f12_entity (id, text, u64, uuid, at, at64) values "
                     + "(1, '61f0c404-5cb3-11e7-907b-a6006ad3dba0', 4194304, "
                     + "'61f0c404-5cb3-11e7-907b-a6006ad3dba0', '2024-01-01 00:00:00', '2024-01-01 00:00:00.123'), "
                     + "(2, NULL, 0, '00000000-0000-0000-0000-000000000000', '1970-01-01 00:00:00', "
                     + "'1970-01-01 00:00:00.000'), "
                     + "(3, 'not-a-uuid', 18446744073709551615, "
                     + "'01890a5d-ac96-774b-bcce-b302099a8057', '2106-02-07 06:28:15', '2109-05-15 07:35:11.103')"
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    [Fact]
    public void UuidNumAndStringToNum_ShouldRoundTripTheBinaryUuid()
    {
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bUuidEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Num = SqlFunctions.ClickHouse.uuid_string_to_num(x.Text),
                Back = SqlFunctions.ClickHouse.uuid_num_to_string(SampleUuidHex),
                Short = SqlFunctions.ClickHouse.uuid_num_to_string("0011"),
                Zero = SqlFunctions.ClickHouse.uuid_num_to_string(
                    "00000000000000000000000000000000")
            })
            .First();

        using (new AssertionScope())
        {
            r.Num.Should().Be(SampleUuidHex, "variant 1 (the default) is the textual/big-endian order");
            r.Back.Should().Be(SampleUuid, "the hex -> UUID -> text pair round-trips");
            r.Short.Should().Be("00110000-0000-0000-0000-000000000000",
                "a short input is zero-padded by the server's toFixedString");
            r.Zero.Should().Be("00000000-0000-0000-0000-000000000000");

            Scalar<string>($"hex(UUIDStringToNum('{SampleUuid}'))").Should().Be(SampleUuidHex);
            Scalar<string>("hex(UUIDStringToNum('nope'))")
                .Should().Be(new string('0', 32), "malformed input is sixteen zero bytes, not an exception");
            Scalar<string>("hex(UUIDStringToNum('61f0c4045cb311e7907ba6006ad3dba0'))")
                .Should().Be(new string('0', 32), "hyphens are required");
        }

        FluentActions.Invoking(() => Scalar<string>(
                "UUIDNumToString(toFixedString(unhex('001122334455667788990011223344556677'), 16))"))
            .Should().Throw<ClickHouseServerException>().WithMessage("*TOO_LARGE_STRING_SIZE*",
                "a binary value longer than sixteen bytes is rejected by the server");
    }

    [Fact]
    public void UuidToNum_ShouldReturnTheVendorBytes()
    {
        Seed();
        var uuid = Guid.Parse(SampleUuid);
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bUuidEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Num = SqlFunctions.ClickHouse.uuid_to_num(x.Uuid),
                Constant = SqlFunctions.ClickHouse.uuid_to_num(uuid)
            })
            .First();

        using (new AssertionScope())
        {
            r.Num.Should().Be(SampleUuidHex);
            r.Constant.Should().Be(SampleUuidHex, "a captured Guid binds as the native UUID type");
            Scalar<string>("hex(UUIDToNum(toUUID('00000000-0000-0000-0000-000000000000')))")
                .Should().Be(new string('0', 32), "the nil UUID is sixteen zero bytes");
        }
    }

    [Fact]
    public void ToUuid_ShouldParseAndRefuseMalformedInput()
    {
        var expected = Guid.Parse(SampleUuid);

        using (new AssertionScope())
        {
            Scalar<Guid>($"toUUID('{SampleUuid}')").Should().Be(expected);
            Scalar<Guid>($"toUUID('{SampleUuid.ToUpperInvariant()}')").Should().Be(expected,
                "upper-case input is accepted");
            Scalar<Guid?>("toUUID(CAST(NULL AS Nullable(String)))").Should().BeNull();
        }

        FluentActions.Invoking(() => Scalar<Guid>("toUUID('not-a-uuid')"))
            .Should().Throw<ClickHouseServerException>().WithMessage("*CANNOT_PARSE_UUID*");
        FluentActions.Invoking(() => Scalar<Guid>($"toUUID(' {SampleUuid}')"))
            .Should().Throw<ClickHouseServerException>().WithMessage("*CANNOT_PARSE*");
    }

    [Fact]
    public void ToUuidOrDefault_ShouldReturnTheDefaultForMalformedInput()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var fallback = Guid.Parse("00000000-0000-0000-0000-000000000001");

        var r = ctx.From<IT286bUuidEntity>()
            .Where(x => x.Id == 3)
            .Select(x => new
            {
                Malformed = SqlFunctions.ClickHouse.to_uuid_or_default(x.Text, fallback),
                Valid = SqlFunctions.ClickHouse.to_uuid_or_default(SampleUuid, fallback)
            })
            .First();

        using (new AssertionScope())
        {
            r.Malformed.Should().Be(fallback, "a malformed string yields the declared default");
            r.Valid.Should().Be(Guid.Parse(SampleUuid));
            Scalar<Guid?>("toUUIDOrDefault(CAST(NULL AS Nullable(String)), toUUID('00000000-0000-0000-0000-000000000001'))")
                .Should().Be(fallback, "a NULL string also yields the default");
        }
    }

    [Fact]
    public void ToUuidOrNull_ShouldReturnNullForMalformedInput()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bUuidEntity>()
            .Where(x => x.Id == 3)
            .Select(x => new
            {
                Malformed = SqlFunctions.ClickHouse.to_uuid_or_null(x.Text),
                Valid = SqlFunctions.ClickHouse.to_uuid_or_null(SampleUuid)
            })
            .First();

        using (new AssertionScope())
        {
            r.Malformed.Should().BeNull("a malformed string yields NULL, unlike to_uuid which raises");
            r.Valid.Should().Be(Guid.Parse(SampleUuid));
            Scalar<Guid?>("toUUIDOrNull(CAST(NULL AS Nullable(String)))").Should().BeNull();
        }
    }

    [Fact]
    public void UuidV7ToDateTime_ShouldDecodeVersionSevenAndTreatNilAsEpoch()
    {
        using (new AssertionScope())
        {
            Scalar<DateTime>("UUIDv7ToDateTime(toUUID('01890a5d-ac96-774b-bcce-b302099a8057'))")
                .Should().Be(new DateTime(2023, 6, 30, 3, 34, 18, 518));
            Scalar<DateTime>("UUIDv7ToDateTime(toUUID('00000000-0000-0000-0000-000000000000'))")
                .Should().Be(new DateTime(1970, 1, 1), "the nil UUID decodes to the epoch");
            Scalar<DateTime>("UUIDv7ToDateTime(toUUID('00000000-03e8-7000-8000-000000000000'))")
                .Should().Be(new DateTime(1970, 1, 1, 0, 0, 1), "the first 48 bits are milliseconds");
            Scalar<DateTime>($"UUIDv7ToDateTime(toUUID('{SampleUuid}'))")
                .Should().Be(new DateTime(1970, 1, 1), "a non-v7 value is decoded without a version check");
            Scalar<DateTime?>("UUIDv7ToDateTime(CAST(NULL AS Nullable(UUID)))").Should().BeNull();
        }
    }

    [Fact]
    public void UlidStringToDateTime_ShouldDecodeAndRejectBadLength()
    {
        using (new AssertionScope())
        {
            Scalar<DateTime>($"ULIDStringToDateTime('{SampleUlid}')")
                .Should().Be(new DateTime(2016, 7, 30, 23, 54, 10, 259));
            Scalar<DateTime>($"ULIDStringToDateTime('{SampleUlid.ToLowerInvariant()}')")
                .Should().Be(new DateTime(2016, 7, 30, 23, 54, 10, 259), "either case is accepted");
            Scalar<DateTime>("ULIDStringToDateTime('00000000000000000000000000')")
                .Should().Be(new DateTime(1970, 1, 1), "the nil ULID decodes to the epoch");
            // The 48-bit ULID timestamp range reaches year 10889, beyond DateTime64(3): the server
            // saturates the all-'Z' maximum at its 2299 limit, whose underlying Int64 the driver
            // cannot materialize as System.DateTime. It raises instead of returning a wrong instant;
            // every in-range ULID above decodes exactly.
            FluentActions.Invoking(() => Scalar<DateTime>("ULIDStringToDateTime('7ZZZZZZZZZZZZZZZZZZZZZZZZZ')"))
                .Should().Throw<ArgumentOutOfRangeException>("an out-of-range ULID timestamp is never truncated");
            Scalar<DateTime?>("ULIDStringToDateTime(CAST(NULL AS Nullable(String)))").Should().BeNull();
        }

        FluentActions.Invoking(() => Scalar<DateTime>("ULIDStringToDateTime('not-a-ulid')"))
            .Should().Throw<ClickHouseServerException>().WithMessage("*ILLEGAL_COLUMN*");
    }

    [Fact]
    public void GenerateSnowflakeId_ShouldProduceDistinctNonZeroIds()
    {
        var first = Scalar<ulong>("generateSnowflakeID()");
        var second = Scalar<ulong>("generateSnowflakeID()");

        using (new AssertionScope())
        {
            first.Should().NotBe(0UL);
            second.Should().NotBe(0UL);
            first.Should().NotBe(second, "the vendor timestamp component advances between calls");
        }
    }

    [Fact]
    public void DateTimeToSnowflakeId_ShouldEncodeMilliseconds()
    {
        using (new AssertionScope())
        {
            Scalar<ulong>("dateTimeToSnowflakeID(toDateTime('2024-01-01 00:00:00'))")
                .Should().Be(7147375873228800000UL);
            Scalar<ulong>("dateTimeToSnowflakeID(toDateTime('1970-01-01 00:00:00'))").Should().Be(0UL);
            Scalar<ulong>("dateTimeToSnowflakeID(toDateTime('2106-02-07 06:28:15'))")
                .Should().Be(18014398505287680000UL);

            // The vendor format is (milliseconds << 22): the low 22 bits are always zero.
            Scalar<ulong>("bitAnd(dateTimeToSnowflakeID(toDateTime('2024-01-01 00:00:00')), 4194303)")
                .Should().Be(0UL);
        }
    }

    [Fact]
    public void DateTime64ToSnowflakeId_ShouldTruncateSubMillisecondDigits()
    {
        using (new AssertionScope())
        {
            const ulong expected = 7147375873744699392UL;
            Scalar<ulong>("dateTime64ToSnowflakeID(toDateTime64('2024-01-01 00:00:00.123', 3))")
                .Should().Be(expected);
            Scalar<ulong>("dateTime64ToSnowflakeID(toDateTime64('2024-01-01 00:00:00.123456', 6))")
                .Should().Be(expected, "the vendor format carries milliseconds and truncates the rest");
            Scalar<ulong>("dateTime64ToSnowflakeID(toDateTime64('1970-01-01 00:00:00.000', 3))").Should().Be(0UL);
        }
    }

    [Fact]
    public void SnowflakeIdToDateTime_ShouldDecodeAndWrapPast2106()
    {
        using (new AssertionScope())
        {
            Scalar<DateTime>("snowflakeIDToDateTime(toUInt64(4194304))")
                .Should().Be(new DateTime(1970, 1, 1), "the 32-bit DateTime truncates the 1 ms component");
            Scalar<DateTime>("snowflakeIDToDateTime(toUInt64(4294967296))")
                .Should().Be(new DateTime(1970, 1, 1, 0, 0, 1));
            Scalar<DateTime>("snowflakeIDToDateTime(toUInt64(0))").Should().Be(new DateTime(1970, 1, 1));

            // The server's DateTime is 32-bit and wraps past 2106 instead of raising; the 64-bit form
            // below is the exact one for the full id range.
            Scalar<DateTime>("snowflakeIDToDateTime(toUInt64(18446744073709551615))")
                .Should().Be(new DateTime(1973, 4, 8, 1, 6, 55));
        }
    }

    [Fact]
    public void SnowflakeIdToDateTime64_ShouldDecodeTheFullRange()
    {
        using (new AssertionScope())
        {
            Scalar<DateTime>("snowflakeIDToDateTime64(toUInt64(4194304))")
                .Should().Be(new DateTime(1970, 1, 1, 0, 0, 0, 1), "2^22 is one millisecond");
            Scalar<DateTime>("snowflakeIDToDateTime64(toUInt64(0))")
                .Should().Be(new DateTime(1970, 1, 1));
            Scalar<DateTime>("snowflakeIDToDateTime64(toUInt64(18446744073709551615))")
                .Should().Be(new DateTime(2109, 5, 15, 7, 35, 11, 103));
        }
    }

    [Fact]
    public void DateTimeToUuidV7_ShouldRoundTripTheTimestamp()
    {
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bUuidEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Generated = SqlFunctions.ClickHouse.date_time_to_uuid_v7(x.At),
                Decoded = SqlFunctions.ClickHouse.uuid_v7_to_datetime(
                    SqlFunctions.ClickHouse.date_time_to_uuid_v7(x.At))
            })
            .First();

        using (new AssertionScope())
        {
            r.Generated.Should().NotBe(Guid.Empty);
            r.Generated.ToString("D").Should().MatchRegex(
                "^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$",
                "the generated UUID carries version 7 and the RFC variant bits");
            r.Decoded.Should().Be(new DateTime(2024, 1, 1), "UUIDv7ToDateTime(dateTimeToUUIDv7(x)) is x");

            Scalar<DateTime>("UUIDv7ToDateTime(dateTimeToUUIDv7(toDateTime('1970-01-01 00:00:01')))")
                .Should().Be(new DateTime(1970, 1, 1, 0, 0, 1));
        }
    }

    [Fact]
    public void UuidMembers_ShouldReturnNullForNullInput()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bUuidEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                Num = SqlFunctions.ClickHouse.uuid_string_to_num(x.Text),
                NumText = SqlFunctions.ClickHouse.uuid_num_to_string(x.Text),
                Parsed = SqlFunctions.ClickHouse.to_uuid(x.Text),
                OrNull = SqlFunctions.ClickHouse.to_uuid_or_null(x.Text),
                Ulid = SqlFunctions.ClickHouse.ulid_string_to_datetime(x.Text),
                V7Time = SqlFunctions.ClickHouse.uuid_v7_to_datetime(x.Uuid)
            })
            .First();

        using (new AssertionScope())
        {
            r.Num.Should().BeNull();
            r.NumText.Should().BeNull();
            r.Parsed.Should().BeNull();
            r.OrNull.Should().BeNull();
            r.Ulid.Should().BeNull();
            r.V7Time.Should().Be(new DateTime(1970, 1, 1), "the nil UUID is not NULL: it decodes to the epoch");
        }
    }

    [Fact]
    public void Projection_ShouldMaterialiseEveryDeliveredMember()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bUuidEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                NumToText = SqlFunctions.ClickHouse.uuid_num_to_string(SampleUuidHex),
                TextToNum = SqlFunctions.ClickHouse.uuid_string_to_num(x.Text),
                UuidToNum = SqlFunctions.ClickHouse.uuid_to_num(x.Uuid),
                V7Time = SqlFunctions.ClickHouse.uuid_v7_to_datetime(x.Uuid),
                Parsed = SqlFunctions.ClickHouse.to_uuid(x.Text),
                Defaulted = SqlFunctions.ClickHouse.to_uuid_or_default(x.Text, x.Uuid),
                OrNull = SqlFunctions.ClickHouse.to_uuid_or_null(x.Text),
                UlidTime = SqlFunctions.ClickHouse.ulid_string_to_datetime(SampleUlid),
                Generated = SqlFunctions.ClickHouse.generate_snowflake_id(),
                FromDateTime = SqlFunctions.ClickHouse.date_time_to_snowflake_id(x.At),
                FromDateTime64 = SqlFunctions.ClickHouse.date_time64_to_snowflake_id(x.At64),
                ToUuidV7 = SqlFunctions.ClickHouse.date_time_to_uuid_v7(x.At),
                FlakeTime = SqlFunctions.ClickHouse.snowflake_id_to_date_time(x.U64),
                FlakeTime64 = SqlFunctions.ClickHouse.snowflake_id_to_date_time64(x.U64)
            })
            .First();

        using (new AssertionScope())
        {
            r.NumToText.Should().Be(SampleUuid);
            r.TextToNum.Should().Be(SampleUuidHex);
            r.UuidToNum.Should().Be(SampleUuidHex);
            r.V7Time.Should().Be(new DateTime(1970, 1, 1));
            r.Parsed.Should().Be(Guid.Parse(SampleUuid));
            r.Defaulted.Should().Be(Guid.Parse(SampleUuid));
            r.OrNull.Should().Be(Guid.Parse(SampleUuid));
            r.UlidTime.Should().Be(new DateTime(2016, 7, 30, 23, 54, 10, 259));
            r.Generated.Should().NotBe(0UL);
            r.FromDateTime.Should().Be(7147375873228800000UL);
            r.FromDateTime64.Should().Be(7147375873744699392UL);
            r.ToUuidV7.ToString("D")[14].Should().Be('7');
            r.FlakeTime.Should().Be(new DateTime(1970, 1, 1));
            r.FlakeTime64.Should().Be(new DateTime(1970, 1, 1, 0, 0, 0, 1));
        }
    }
}
