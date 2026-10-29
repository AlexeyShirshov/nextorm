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
/// T286b D15 (F15 Type conversion): the ClickHouse conversion family. The tests pin every vendor
/// spelling (103 render arms over 91 delivered names), the exact result widening of the narrow
/// targets (Int8 → Int16, UInt16 → Int32, reinterpretAsInt8 → Int16), the OrNull/OrDefault variants,
/// the constant scale/length rules, the checked <c>accurateCast*</c> arms that must not collapse into
/// the <c>to*</c> arms, the quoted decimal target type, and the deterministic 128/256-bit, Decimal128/256
/// and Interval refusals. No connection is opened.
/// </summary>
public class T286bF15TypeConversionSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [SqlTable("t286b_f15_entity")]
    internal interface IT286bConversionEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("i64")]
        long? I64 { get; set; }

        [Column("u64")]
        ulong? U64 { get; set; }

        [Column("f64")]
        double? F64 { get; set; }

        [Column("text")]
        string? Text { get; set; }

        [Column("dec")]
        decimal? Dec { get; set; }

        [Column("dte")]
        DateTime? Dte { get; set; }
    }

    private static readonly string[] DeliveredNames =
    [
        "to_int8", "to_int8_or_null", "to_int8_or_default",
        "to_int16", "to_int16_or_null", "to_int16_or_default",
        "to_int32", "to_int32_or_null", "to_int32_or_default",
        "to_int64", "to_int64_or_null", "to_int64_or_default",
        "to_uint8", "to_uint8_or_null", "to_uint8_or_default",
        "to_uint16", "to_uint16_or_null", "to_uint16_or_default",
        "to_uint32", "to_uint32_or_null", "to_uint32_or_default",
        "to_uint64", "to_uint64_or_null", "to_uint64_or_default",
        "to_float32", "to_float32_or_null", "to_float32_or_default",
        "to_float64", "to_float64_or_null", "to_float64_or_default",
        "to_decimal32", "to_decimal32_or_null", "to_decimal32_or_default",
        "to_decimal64", "to_decimal64_or_null", "to_decimal64_or_default",
        "to_string", "to_bool", "to_fixed_string", "to_low_cardinality",
        "accurate_cast_int8", "accurate_cast_int8_or_null", "accurate_cast_int8_or_default",
        "accurate_cast_int16", "accurate_cast_int16_or_null", "accurate_cast_int16_or_default",
        "accurate_cast_int32", "accurate_cast_int32_or_null", "accurate_cast_int32_or_default",
        "accurate_cast_int64", "accurate_cast_int64_or_null", "accurate_cast_int64_or_default",
        "accurate_cast_uint8", "accurate_cast_uint8_or_null", "accurate_cast_uint8_or_default",
        "accurate_cast_uint16", "accurate_cast_uint16_or_null", "accurate_cast_uint16_or_default",
        "accurate_cast_uint32", "accurate_cast_uint32_or_null", "accurate_cast_uint32_or_default",
        "accurate_cast_uint64", "accurate_cast_uint64_or_null", "accurate_cast_uint64_or_default",
        "accurate_cast_float32", "accurate_cast_float32_or_null", "accurate_cast_float32_or_default",
        "accurate_cast_float64", "accurate_cast_float64_or_null", "accurate_cast_float64_or_default",
        "accurate_cast_decimal32", "accurate_cast_decimal32_or_null", "accurate_cast_decimal32_or_default",
        "accurate_cast_decimal64", "accurate_cast_decimal64_or_null", "accurate_cast_decimal64_or_default",
        "reinterpret_as_uint8", "reinterpret_as_uint16", "reinterpret_as_uint32", "reinterpret_as_uint64",
        "reinterpret_as_int8", "reinterpret_as_int16", "reinterpret_as_int32", "reinterpret_as_int64",
        "reinterpret_as_float32", "reinterpret_as_float64", "reinterpret_as_date", "reinterpret_as_date_time",
        "reinterpret_as_string", "reinterpret_as_fixed_string", "reinterpret_as_uuid"
    ];

    private static readonly string[] RefusedNames =
    [
        "to_int128", "to_uint128", "to_int256", "to_uint256", "to_decimal128", "to_decimal256",
        "to_interval_nanosecond", "to_interval_microsecond", "to_interval_millisecond",
        "to_interval_second", "to_interval_minute", "to_interval_hour", "to_interval_day",
        "to_interval_week", "to_interval_month", "to_interval_quarter", "to_interval_year",
        "reinterpret_as_uint128", "reinterpret_as_uint256", "reinterpret_as_int128", "reinterpret_as_int256"
    ];

    /// <summary>
    /// Every declared render arm, written out literally from the dialect contract: 91 names and 103
    /// arms (the two OrDefault names each carry the one-argument and the two-argument form).
    /// </summary>
    private static readonly (string Name, string[] Args, string Expected)[] RenderArms =
    [
        ("to_int8", ["a"], "toInt16(toInt8(a))"),
        ("to_int8_or_null", ["a"], "toInt16(toInt8OrNull(a))"),
        ("to_int8_or_default", ["a"], "toInt16(toInt8OrDefault(a))"),
        ("to_int8_or_default", ["a", "b"], "toInt16(toInt8OrDefault(a, toInt8(b)))"),
        ("to_int16", ["a"], "toInt16(a)"),
        ("to_int16_or_null", ["a"], "toInt16OrNull(a)"),
        ("to_int16_or_default", ["a"], "toInt16OrDefault(a)"),
        ("to_int16_or_default", ["a", "b"], "toInt16OrDefault(a, toInt16(b))"),
        ("to_int32", ["a"], "toInt32(a)"),
        ("to_int32_or_null", ["a"], "toInt32OrNull(a)"),
        ("to_int32_or_default", ["a"], "toInt32OrDefault(a)"),
        ("to_int32_or_default", ["a", "b"], "toInt32OrDefault(a, toInt32(b))"),
        ("to_int64", ["a"], "toInt64(a)"),
        ("to_int64_or_null", ["a"], "toInt64OrNull(a)"),
        ("to_int64_or_default", ["a"], "toInt64OrDefault(a)"),
        ("to_int64_or_default", ["a", "b"], "toInt64OrDefault(a, toInt64(b))"),
        ("to_uint8", ["a"], "toUInt8(a)"),
        ("to_uint8_or_null", ["a"], "toUInt8OrNull(a)"),
        ("to_uint8_or_default", ["a"], "toUInt8OrDefault(a)"),
        ("to_uint8_or_default", ["a", "b"], "toUInt8OrDefault(a, toUInt8(b))"),
        ("to_uint16", ["a"], "toInt32(toUInt16(a))"),
        ("to_uint16_or_null", ["a"], "toInt32(toUInt16OrNull(a))"),
        ("to_uint16_or_default", ["a"], "toInt32(toUInt16OrDefault(a))"),
        ("to_uint16_or_default", ["a", "b"], "toInt32(toUInt16OrDefault(a, toUInt16(b)))"),
        ("to_uint32", ["a"], "toUInt32(a)"),
        ("to_uint32_or_null", ["a"], "toUInt32OrNull(a)"),
        ("to_uint32_or_default", ["a"], "toUInt32OrDefault(a)"),
        ("to_uint32_or_default", ["a", "b"], "toUInt32OrDefault(a, toUInt32(b))"),
        ("to_uint64", ["a"], "toUInt64(a)"),
        ("to_uint64_or_null", ["a"], "toUInt64OrNull(a)"),
        ("to_uint64_or_default", ["a"], "toUInt64OrDefault(a)"),
        ("to_uint64_or_default", ["a", "b"], "toUInt64OrDefault(a, toUInt64(b))"),
        ("to_float32", ["a"], "toFloat32(a)"),
        ("to_float32_or_null", ["a"], "toFloat32OrNull(a)"),
        ("to_float32_or_default", ["a"], "toFloat32OrDefault(a)"),
        ("to_float32_or_default", ["a", "b"], "toFloat32OrDefault(a, toFloat32(b))"),
        ("to_float64", ["a"], "toFloat64(a)"),
        ("to_float64_or_null", ["a"], "toFloat64OrNull(a)"),
        ("to_float64_or_default", ["a"], "toFloat64OrDefault(a)"),
        ("to_float64_or_default", ["a", "b"], "toFloat64OrDefault(a, toFloat64(b))"),
        ("to_decimal32", ["a", "2"], "toDecimal32(a, 2)"),
        ("to_decimal32_or_null", ["a", "2"], "toDecimal32OrNull(a, 2)"),
        ("to_decimal32_or_default", ["a", "2"], "toDecimal32OrDefault(a, 2)"),
        ("to_decimal32_or_default", ["a", "2", "b"], "toDecimal32OrDefault(a, 2, toDecimal32(b, 2))"),
        ("to_decimal64", ["a", "2"], "toDecimal64(a, 2)"),
        ("to_decimal64_or_null", ["a", "2"], "toDecimal64OrNull(a, 2)"),
        ("to_decimal64_or_default", ["a", "2"], "toDecimal64OrDefault(a, 2)"),
        ("to_decimal64_or_default", ["a", "2", "b"], "toDecimal64OrDefault(a, 2, toDecimal64(b, 2))"),
        ("to_string", ["a"], "toString(a)"),
        ("to_bool", ["a"], "toBool(a)"),
        ("to_fixed_string", ["a", "4"], "toFixedString(a, 4)"),
        ("to_low_cardinality", ["a"], "toLowCardinality(a)"),
        ("accurate_cast_int8", ["a"], "toInt16(accurateCast(a, 'Int8'))"),
        ("accurate_cast_int8_or_null", ["a"], "toInt16(accurateCastOrNull(a, 'Int8'))"),
        ("accurate_cast_int8_or_default", ["a"], "toInt16(accurateCastOrDefault(a, 'Int8'))"),
        ("accurate_cast_int16", ["a"], "accurateCast(a, 'Int16')"),
        ("accurate_cast_int16_or_null", ["a"], "accurateCastOrNull(a, 'Int16')"),
        ("accurate_cast_int16_or_default", ["a"], "accurateCastOrDefault(a, 'Int16')"),
        ("accurate_cast_int32", ["a"], "accurateCast(a, 'Int32')"),
        ("accurate_cast_int32_or_null", ["a"], "accurateCastOrNull(a, 'Int32')"),
        ("accurate_cast_int32_or_default", ["a"], "accurateCastOrDefault(a, 'Int32')"),
        ("accurate_cast_int64", ["a"], "accurateCast(a, 'Int64')"),
        ("accurate_cast_int64_or_null", ["a"], "accurateCastOrNull(a, 'Int64')"),
        ("accurate_cast_int64_or_default", ["a"], "accurateCastOrDefault(a, 'Int64')"),
        ("accurate_cast_uint8", ["a"], "accurateCast(a, 'UInt8')"),
        ("accurate_cast_uint8_or_null", ["a"], "accurateCastOrNull(a, 'UInt8')"),
        ("accurate_cast_uint8_or_default", ["a"], "accurateCastOrDefault(a, 'UInt8')"),
        ("accurate_cast_uint16", ["a"], "toInt32(accurateCast(a, 'UInt16'))"),
        ("accurate_cast_uint16_or_null", ["a"], "toInt32(accurateCastOrNull(a, 'UInt16'))"),
        ("accurate_cast_uint16_or_default", ["a"], "toInt32(accurateCastOrDefault(a, 'UInt16'))"),
        ("accurate_cast_uint32", ["a"], "accurateCast(a, 'UInt32')"),
        ("accurate_cast_uint32_or_null", ["a"], "accurateCastOrNull(a, 'UInt32')"),
        ("accurate_cast_uint32_or_default", ["a"], "accurateCastOrDefault(a, 'UInt32')"),
        ("accurate_cast_uint64", ["a"], "accurateCast(a, 'UInt64')"),
        ("accurate_cast_uint64_or_null", ["a"], "accurateCastOrNull(a, 'UInt64')"),
        ("accurate_cast_uint64_or_default", ["a"], "accurateCastOrDefault(a, 'UInt64')"),
        ("accurate_cast_float32", ["a"], "accurateCast(a, 'Float32')"),
        ("accurate_cast_float32_or_null", ["a"], "accurateCastOrNull(a, 'Float32')"),
        ("accurate_cast_float32_or_default", ["a"], "accurateCastOrDefault(a, 'Float32')"),
        ("accurate_cast_float64", ["a"], "accurateCast(a, 'Float64')"),
        ("accurate_cast_float64_or_null", ["a"], "accurateCastOrNull(a, 'Float64')"),
        ("accurate_cast_float64_or_default", ["a"], "accurateCastOrDefault(a, 'Float64')"),
        ("accurate_cast_decimal32", ["a", "2"], "accurateCast(a, 'Decimal(9, 2)')"),
        ("accurate_cast_decimal32_or_null", ["a", "2"], "accurateCastOrNull(a, 'Decimal(9, 2)')"),
        ("accurate_cast_decimal32_or_default", ["a", "2"], "accurateCastOrDefault(a, 'Decimal(9, 2)')"),
        ("accurate_cast_decimal64", ["a", "2"], "accurateCast(a, 'Decimal(18, 2)')"),
        ("accurate_cast_decimal64_or_null", ["a", "2"], "accurateCastOrNull(a, 'Decimal(18, 2)')"),
        ("accurate_cast_decimal64_or_default", ["a", "2"], "accurateCastOrDefault(a, 'Decimal(18, 2)')"),
        ("reinterpret_as_uint8", ["a"], "reinterpretAsUInt8(a)"),
        ("reinterpret_as_uint16", ["a"], "toInt32(reinterpretAsUInt16(a))"),
        ("reinterpret_as_uint32", ["a"], "reinterpretAsUInt32(a)"),
        ("reinterpret_as_uint64", ["a"], "reinterpretAsUInt64(a)"),
        ("reinterpret_as_int8", ["a"], "toInt16(reinterpretAsInt8(a))"),
        ("reinterpret_as_int16", ["a"], "reinterpretAsInt16(a)"),
        ("reinterpret_as_int32", ["a"], "reinterpretAsInt32(a)"),
        ("reinterpret_as_int64", ["a"], "reinterpretAsInt64(a)"),
        ("reinterpret_as_float32", ["a"], "reinterpretAsFloat32(a)"),
        ("reinterpret_as_float64", ["a"], "reinterpretAsFloat64(a)"),
        ("reinterpret_as_date", ["a"], "reinterpretAsDate(a)"),
        ("reinterpret_as_date_time", ["a"], "reinterpretAsDateTime(a)"),
        ("reinterpret_as_string", ["a"], "reinterpretAsString(a)"),
        ("reinterpret_as_fixed_string", ["a"], "reinterpretAsFixedString(a)"),
        ("reinterpret_as_uuid", ["a"], "reinterpretAsUUID(a)")
    ];

    [Fact]
    public void Render_ShouldEmitTheExactArmForEveryDeliveredMember()
    {
        var scalars = ClickHouseDialect.Instance.ScalarFunctions!;

        using (new AssertionScope())
        {
            RenderArms.Should().HaveCount(103);
            RenderArms.Select(a => a.Name).Distinct().Should().BeEquivalentTo(DeliveredNames);
            foreach (var (name, args, expected) in RenderArms)
                scalars.Render(name, args).Should().Be(expected, "'{0}' is the ClickHouse arm", name);
        }
    }

    [Fact]
    public void Widening_ShouldKeepTheNarrowTargetsExact()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bConversionEntity>();

        using (new AssertionScope())
        {
            // The shared row reader has no sbyte/ushort accessor: Int8 and UInt16 are widened exactly.
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_int8(x.I64) }))
                .Should().Contain("toInt16(toInt8(i64)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_uint16(x.I64) }))
                .Should().Contain("toInt32(toUInt16(i64)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.reinterpret_as_int8(x.I64) }))
                .Should().Contain("toInt16(reinterpretAsInt8(i64)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.reinterpret_as_uint16(x.I64) }))
                .Should().Contain("toInt32(reinterpretAsUInt16(i64)) as `V`");

            // The wider targets are already materializable and are not wrapped.
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_int16(x.I64) }))
                .Should().Contain("toInt16(i64) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_uint32(x.I64) }))
                .Should().Contain("toUInt32(i64) as `V`");
        }
    }

    [Fact]
    public void OrDefault_ShouldRenderTheVendorFallback()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bConversionEntity>();

        using (new AssertionScope())
        {
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_int8_or_default(x.I64) }))
                .Should().Contain("toInt16(toInt8OrDefault(i64)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_int8_or_default(x.I64, (short)7) }))
                .Should().Contain("toInt16(toInt8OrDefault(i64, toInt8(7))) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_uint16_or_default(x.I64, 300) }))
                .Should().Contain("toInt32(toUInt16OrDefault(i64, toUInt16(300))) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_decimal32_or_default(x.Dec, 2) }))
                .Should().Contain("toDecimal32OrDefault(dec, 2) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_decimal32_or_default(x.Dec, 2, 7.5m) }))
                .Should().Contain("toDecimal32OrDefault(dec, 2, toDecimal32(7.5, 2)) as `V`");
        }
    }

    [Fact]
    public void DecimalScale_ShouldRequireAConstantInRange()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bConversionEntity>();

        using (new AssertionScope())
        {
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_decimal32(x.Dec, 9) }))
                .Should().Contain("toDecimal32(dec, 9)");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_decimal64(x.Dec, 18) }))
                .Should().Contain("toDecimal64(dec, 18)");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.accurate_cast_decimal32(x.Dec, 2) }))
                .Should().Contain("accurateCast(dec, 'Decimal(9, 2)')");

            FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_decimal32(x.Dec, 10) })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*scale must be a compile-time constant in 0..9*");
            FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_decimal64(x.Dec, -1) })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*scale must be a compile-time constant in 0..18*");

            var captured = 2;
            FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_decimal32(x.Dec, captured) })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*scale must be a compile-time constant in 0..9*");
        }
    }

    [Fact]
    public void FixedStringLength_ShouldRequireAConstant()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bConversionEntity>();

        using (new AssertionScope())
        {
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_fixed_string(x.Text, 5) }))
                .Should().Contain("toFixedString(text, 5)");

            FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_fixed_string(x.Text, 0) })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*length must be 1..255*");

            var capturedLength = (byte)4;
            FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_fixed_string(x.Text, capturedLength) })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*length must be a compile-time constant*");
        }
    }

    [Fact]
    public void NarrowDefaults_ShouldRejectAConstantOutsideTheVendorTarget()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bConversionEntity>();

        using (new AssertionScope())
        {
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_int8_or_default(x.I64, (short)127) }))
                .Should().Contain("toInt8OrDefault(i64, toInt8(127))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_uint16_or_default(x.I64, 65535) }))
                .Should().Contain("toUInt16OrDefault(i64, toUInt16(65535))");

            FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_int8_or_default(x.I64, (short)200) })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*default must fit the vendor Int8 target (-128..127)*");
            FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_uint16_or_default(x.I64, -1) })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*default must fit the vendor UInt16 target (0..65535)*");
        }
    }

    [Fact]
    public void AccurateCast_ShouldBeCheckedAndNotCollapseIntoTheToArm()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bConversionEntity>();

        using (new AssertionScope())
        {
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.accurate_cast_int8(x.I64) }))
                .Should().Contain("toInt16(accurateCast(i64, 'Int8')) as `V`")
                .And.NotContain("toInt8(");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.accurate_cast_int8_or_null(x.I64) }))
                .Should().Contain("accurateCastOrNull(i64, 'Int8')");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.accurate_cast_int8_or_default(x.I64) }))
                .Should().Contain("accurateCastOrDefault(i64, 'Int8')");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.accurate_cast_decimal64(x.Dec, 2) }))
                .Should().Contain("accurateCast(dec, 'Decimal(18, 2)')");
        }
    }

    [Fact]
    public void Refusals_ShouldThrowWithThePreciseReason()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bConversionEntity>();

        using (new AssertionScope())
        {
            foreach (var name in RefusedNames)
            {
                Func<string> query = name switch
                {
                    "to_int128" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_int128(x.I64) })),
                    "to_uint128" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_uint128(x.I64) })),
                    "to_int256" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_int256(x.I64) })),
                    "to_uint256" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_uint256(x.I64) })),
                    "to_decimal128" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_decimal128(x.Dec, 2) })),
                    "to_decimal256" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_decimal256(x.Dec, 2) })),
                    "to_interval_nanosecond" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_interval_nanosecond(x.I64) })),
                    "to_interval_microsecond" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_interval_microsecond(x.I64) })),
                    "to_interval_millisecond" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_interval_millisecond(x.I64) })),
                    "to_interval_second" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_interval_second(x.I64) })),
                    "to_interval_minute" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_interval_minute(x.I64) })),
                    "to_interval_hour" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_interval_hour(x.I64) })),
                    "to_interval_day" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_interval_day(x.I64) })),
                    "to_interval_week" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_interval_week(x.I64) })),
                    "to_interval_month" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_interval_month(x.I64) })),
                    "to_interval_quarter" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_interval_quarter(x.I64) })),
                    "to_interval_year" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_interval_year(x.I64) })),
                    "reinterpret_as_uint128" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.reinterpret_as_uint128(x.I64) })),
                    "reinterpret_as_uint256" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.reinterpret_as_uint256(x.I64) })),
                    "reinterpret_as_int128" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.reinterpret_as_int128(x.I64) })),
                    "reinterpret_as_int256" => () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.reinterpret_as_int256(x.I64) })),
                    _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
                };

                query.Should().Throw<NotSupportedException>()
                    .WithMessage($"The {name} function is refused:*");
            }
        }
    }

    [Fact]
    public void DeclaredShapes_ShouldMatchTheDeclaredClrTypes()
    {
        var methods = typeof(ClickHouseFunctions).GetMethods(BindingFlags.Public | BindingFlags.Instance);

        static MethodInfo One(string name, int parameters)
            => typeof(ClickHouseFunctions).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(m => m.Name == name && m.GetParameters().Length == parameters && m.IsGenericMethod);

        static MethodInfo Plain(string name, int parameters)
            => typeof(ClickHouseFunctions).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(m => m.Name == name && m.GetParameters().Length == parameters && !m.IsGenericMethod);

        using (new AssertionScope())
        {
            One("to_int8", 1).ReturnType.Should().Be(typeof(short?));
            One("to_int16", 1).ReturnType.Should().Be(typeof(short?));
            One("to_int32", 1).ReturnType.Should().Be(typeof(int?));
            One("to_int64", 1).ReturnType.Should().Be(typeof(long?));
            One("to_uint8", 1).ReturnType.Should().Be(typeof(byte?));
            One("to_uint16", 1).ReturnType.Should().Be(typeof(int?));
            One("to_uint32", 1).ReturnType.Should().Be(typeof(uint?));
            One("to_uint64", 1).ReturnType.Should().Be(typeof(ulong?));
            One("to_float32", 1).ReturnType.Should().Be(typeof(float?));
            One("to_float64", 1).ReturnType.Should().Be(typeof(double?));
            One("to_string", 1).ReturnType.Should().Be(typeof(string));
            One("to_bool", 1).ReturnType.Should().Be(typeof(bool?));
            One("to_fixed_string", 2).ReturnType.Should().Be(typeof(string));
            One("to_low_cardinality", 1).ReturnType.Should().Be(typeof(string));
            One("to_decimal32", 2).ReturnType.Should().Be(typeof(decimal?));
            One("to_decimal64", 2).ReturnType.Should().Be(typeof(decimal?));
            One("reinterpret_as_uuid", 1).ReturnType.Should().Be(typeof(Guid?));
            One("reinterpret_as_uint16", 1).ReturnType.Should().Be(typeof(int?));
            One("reinterpret_as_int8", 1).ReturnType.Should().Be(typeof(short?));

            Plain("to_int8_or_null", 1).GetParameters()[0].ParameterType.Should().Be(typeof(string));
            One("to_fixed_string", 2).GetParameters()[1].ParameterType.Should().Be(typeof(byte));
            One("to_decimal32", 2).GetParameters()[1].ParameterType.Should().Be(typeof(int));
            One("to_uint16_or_default", 2).GetParameters()[1].ParameterType.Should().Be(typeof(int));

            methods.Where(m => m.Name == "to_int128").Should().ContainSingle()
                .Which.ReturnType.Should().Be(typeof(object));
        }
    }

    [Fact]
    public void ProviderGuard_ShouldFailClosedOnPostgresAndSqlServer()
    {
        var postgres = PostgresDialect.Instance.ScalarFunctions;
        var sqlServer = SqlServerDialect.Instance.ScalarFunctions;

        using (new AssertionScope())
        {
            foreach (var name in DeliveredNames)
            {
                (postgres?.Supports(name) ?? false).Should().BeFalse("'{0}' is ClickHouse-only", name);
                (sqlServer?.Supports(name) ?? false).Should().BeFalse("'{0}' is ClickHouse-only", name);
            }
        }

        using var postgresContext = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        using var sqlServerContext = new SqlServerDataContext(
            "Server=localhost;Database=nextorm;User Id=sa;Password=nextorm;TrustServerCertificate=True",
            new DataContextBuilder());

        foreach (var ctx in new IDataContext[] { postgresContext, sqlServerContext })
        {
            using (new AssertionScope())
            {
                FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bConversionEntity>()
                        .Select(x => new { V = SqlFunctions.ClickHouse.to_int8(x.I64) })))
                    .Should().Throw<NotSupportedException>()
                    .WithMessage("The to_int8 function is not supported by this provider.");
                FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bConversionEntity>()
                        .Select(x => new { V = SqlFunctions.ClickHouse.to_string(x.I64) })))
                    .Should().Throw<NotSupportedException>()
                    .WithMessage("The to_string function is not supported by this provider.");
                FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bConversionEntity>()
                        .Select(x => new { V = SqlFunctions.ClickHouse.accurate_cast_int8(x.I64) })))
                    .Should().Throw<NotSupportedException>()
                    .WithMessage("The accurate_cast_int8 function is not supported by this provider.");
                FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bConversionEntity>()
                        .Select(x => new { V = SqlFunctions.ClickHouse.reinterpret_as_uint8(x.Text) })))
                    .Should().Throw<NotSupportedException>()
                    .WithMessage("The reinterpret_as_uint8 function is not supported by this provider.");
            }
        }
    }

    [Fact]
    public void NullOperands_ShouldRenderNullInsteadOfBeingDropped()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bConversionEntity>();

        using (new AssertionScope())
        {
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_int8<long?>(null) }))
                .Should().Contain("toInt16(toInt8(null))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_string<long?>(null) }))
                .Should().Contain("toString(null)");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_bool<long?>(null) }))
                .Should().Contain("toBool(null)");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.to_decimal32<long?>(null, 2) }))
                .Should().Contain("toDecimal32(null, 2)");
        }
    }

    [Fact]
    public void Projection_ShouldRenderEveryFamilyGroupInOneSelect()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bConversionEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            I8 = SqlFunctions.ClickHouse.to_int8(x.I64),
            I16 = SqlFunctions.ClickHouse.to_int16(x.I64),
            U16 = SqlFunctions.ClickHouse.to_uint16(x.I64),
            U64 = SqlFunctions.ClickHouse.to_uint64(x.U64),
            F32 = SqlFunctions.ClickHouse.to_float32(x.F64),
            Dec = SqlFunctions.ClickHouse.to_decimal64(x.Dec, 2),
            Str = SqlFunctions.ClickHouse.to_string(x.I64),
            Flag = SqlFunctions.ClickHouse.to_bool(x.I64),
            Fixed = SqlFunctions.ClickHouse.to_fixed_string(x.Text, 4),
            Low = SqlFunctions.ClickHouse.to_low_cardinality(x.Text),
            Checked = SqlFunctions.ClickHouse.accurate_cast_int32(x.I64),
            CheckedOrNull = SqlFunctions.ClickHouse.accurate_cast_int32_or_null(x.I64),
            Bits = SqlFunctions.ClickHouse.reinterpret_as_uint32(x.Text),
            Dt = SqlFunctions.ClickHouse.reinterpret_as_date_time(x.I64)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("toInt16(toInt8(i64)) as `I8`");
            sql.Should().Contain("toInt16(i64) as `I16`");
            sql.Should().Contain("toInt32(toUInt16(i64)) as `U16`");
            sql.Should().Contain("toUInt64(u64) as `U64`");
            sql.Should().Contain("toFloat32(f64) as `F32`");
            sql.Should().Contain("toDecimal64(dec, 2) as `Dec`");
            sql.Should().Contain("toString(i64) as `Str`");
            sql.Should().Contain("toBool(i64) as `Flag`");
            sql.Should().Contain("toFixedString(text, 4) as `Fixed`");
            sql.Should().Contain("toLowCardinality(text) as `Low`");
            sql.Should().Contain("accurateCast(i64, 'Int32') as `Checked`");
            sql.Should().Contain("accurateCastOrNull(i64, 'Int32') as `CheckedOrNull`");
            sql.Should().Contain("reinterpretAsUInt32(text) as `Bits`");
            sql.Should().Contain("reinterpretAsDateTime(i64) as `Dt`");
        }
    }
}
