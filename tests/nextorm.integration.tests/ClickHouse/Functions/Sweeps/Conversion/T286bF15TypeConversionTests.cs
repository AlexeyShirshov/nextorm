using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T286b D15 (F15 Type conversion): live execution of the ClickHouse conversion family against the
/// pinned <c>25.8-alpine</c> (25.8.33.6) server. The tests pin the vendor's wrap/truncate rules for
/// every integer width (positive and negative overflow), the text-parse failure modes (invalid text
/// and the empty string), the OrNull/OrDefault fallbacks, the NULL matrix, the Float32/Float64
/// special values and range saturation, the Decimal32/Decimal64 scale truncation and digit bound, the
/// string/bool/FixedString/LowCardinality shapes, the checked <c>accurateCast*</c> distinction from
/// <c>to*</c>, the little-endian bit patterns of <c>reinterpretAs*</c>, parameter binding, and the
/// deterministic refusals that never reach the server.
/// </summary>
[Collection("ClickHouse")]
public sealed class T286bF15TypeConversionTests : ProviderTestSuite
{
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

        [Column("txt")]
        string Txt { get; set; }

        [Column("dec")]
        decimal? Dec { get; set; }

        [Column("dte")]
        DateTime? Dte { get; set; }
    }

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private static void Seed()
    {
        using var connection = new ClickHouseConnection(ClickHouseContainer.ConnectionString);
        connection.Open();
        foreach (var statement in new[]
                 {
                     "drop table if exists t286b_f15_entity",
                     "create table t286b_f15_entity (id Int32, i64 Nullable(Int64), u64 Nullable(UInt64), " +
                     "f64 Nullable(Float64), text Nullable(String), txt String, dec Nullable(Decimal(18, 2)), " +
                     "dte Nullable(DateTime)) engine = Memory",
                     "insert into t286b_f15_entity (id, i64, u64, f64, text, txt, dec, dte) values " +
                     "(1, 300, 18446744073709551615, 12.7, '127', '127', 12.34, '2024-01-02 03:04:05'), " +
                     "(2, -1, 0, -12.7, 'abc', 'abc', NULL, NULL), " +
                     "(3, 0, 1, 1.5, '', '', 0.00, '1970-01-01 00:00:00'), " +
                     "(4, NULL, NULL, NULL, NULL, '', NULL, NULL), " +
                     "(5, 128, 256, 0.5, '12.9', '12.9', 12.999, NULL), " +
                     "(6, -129, 65536, -1e40, '300', '300', 9999999999999999.99, NULL), " +
                     "(7, 32768, 4294967296, 12.0, 'nan', 'nan', NULL, NULL), " +
                     "(8, -32769, 1, 1e40, 'inf', 'inf', NULL, NULL), " +
                     "(9, 2147483648, 1, 0.3333333333333333, 'yes', 'yes', NULL, NULL), " +
                     "(10, -2147483649, 1, 2.5, 'off', 'off', NULL, NULL), " +
                     "(11, 9223372036854775807, 1, 1.0, 'true', 'true', NULL, NULL), " +
                     "(12, -9223372036854775808, 81985529216486895, 1.5, 'abcd', 'abcd', NULL, NULL), " +
                     "(13, 1, 4607182418800017408, 1.0, '0123456789abcdef', '0123456789abcdef', NULL, NULL), " +
                     "(14, 1704164645, 19723, NULL, '99999999999999999.99', '99999999999999999.99', NULL, NULL), " +
                     "(15, 1065353216, 1, 12.34, '1.2345678901', '1.2345678901', NULL, NULL)"
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    [Fact]
    public void AllDeliveredFamilies_ShouldExecuteOnThePinnedServer()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bConversionEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                I8 = SqlFunctions.ClickHouse.to_int8(x.I64),
                I16 = SqlFunctions.ClickHouse.to_int16(x.I64),
                I32 = SqlFunctions.ClickHouse.to_int32(x.I64),
                I64v = SqlFunctions.ClickHouse.to_int64(x.I64),
                U8 = SqlFunctions.ClickHouse.to_uint8(x.U64),
                U16 = SqlFunctions.ClickHouse.to_uint16(x.U64),
                U32 = SqlFunctions.ClickHouse.to_uint32(x.U64),
                U64v = SqlFunctions.ClickHouse.to_uint64(x.U64),
                F32 = SqlFunctions.ClickHouse.to_float32(x.F64),
                F64v = SqlFunctions.ClickHouse.to_float64(x.F64),
                D32 = SqlFunctions.ClickHouse.to_decimal32(x.Dec, 2),
                D64 = SqlFunctions.ClickHouse.to_decimal64(x.Dec, 2),
                Str = SqlFunctions.ClickHouse.to_string(x.I64),
                Flag = SqlFunctions.ClickHouse.to_bool(x.I64),
                Fixed = SqlFunctions.ClickHouse.to_fixed_string(x.Text, 5),
                Low = SqlFunctions.ClickHouse.to_low_cardinality(x.Text),
                Checked = SqlFunctions.ClickHouse.accurate_cast_int32(x.I64),
                CheckedOrNull = SqlFunctions.ClickHouse.accurate_cast_int8_or_null(x.I64),
                CheckedOrZero = SqlFunctions.ClickHouse.accurate_cast_int8_or_default(x.I64)
            })
            .First();

        using (new AssertionScope())
        {
            r.I8.Should().Be((short)44, "300 wraps within Int8");
            r.I16.Should().Be((short)300);
            r.I32.Should().Be(300);
            r.I64v.Should().Be(300L);
            r.U8.Should().Be((byte)255, "the UInt64 maximum wraps to 255");
            r.U16.Should().Be(65535);
            r.U32.Should().Be(4294967295U);
            r.U64v.Should().Be(18446744073709551615UL);
            r.F32.Should().Be(12.7f);
            r.F64v.Should().Be(12.7);
            r.D32.Should().Be(12.34m);
            r.D64.Should().Be(12.34m);
            r.Str.Should().Be("300");
            r.Flag.Should().BeTrue();
            r.Fixed.Should().Be("127\0\0");
            r.Low.Should().Be("127");
            r.Checked.Should().Be(300);
            r.CheckedOrNull.Should().BeNull("300 is not representable as Int8");
            r.CheckedOrZero.Should().Be((short)0);
        }
    }

    [Fact]
    public void IntegerOverflow_ShouldWrapAtEveryWidth()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        T Pick<T>(int id, Expression<Func<IT286bConversionEntity, T>> selector)
        {
            // A fresh context per projection: the distinct Select expressions must never share a
            // cached plan (an or_null arm must not stand in for the throwing arm).
            using var local = Provider.CreateContext();
            return local.From<IT286bConversionEntity>().Where(x => x.Id == id).Select(selector).ToList().Single();
        }

        using (new AssertionScope())
        {
            Pick(5, x => SqlFunctions.ClickHouse.to_int8(x.I64)).Should().Be((short)-128);
            Pick(6, x => SqlFunctions.ClickHouse.to_int8(x.I64)).Should().Be((short)127);
            Pick(7, x => SqlFunctions.ClickHouse.to_int16(x.I64)).Should().Be((short)-32768);
            Pick(8, x => SqlFunctions.ClickHouse.to_int16(x.I64)).Should().Be((short)32767);
            Pick(9, x => SqlFunctions.ClickHouse.to_int32(x.I64)).Should().Be(-2147483648);
            Pick(10, x => SqlFunctions.ClickHouse.to_int32(x.I64)).Should().Be(2147483647);
            Pick(11, x => SqlFunctions.ClickHouse.to_int64(x.I64)).Should().Be(long.MaxValue);
            Pick(12, x => SqlFunctions.ClickHouse.to_int64(x.I64)).Should().Be(long.MinValue);

            Pick(5, x => SqlFunctions.ClickHouse.to_uint8(x.U64)).Should().Be((byte)0);
            Pick(2, x => SqlFunctions.ClickHouse.to_uint8(x.I64)).Should().Be((byte)255, "-1 wraps to 255");
            Pick(2, x => SqlFunctions.ClickHouse.to_uint16(x.I64)).Should().Be(65535);
            Pick(2, x => SqlFunctions.ClickHouse.to_uint32(x.I64)).Should().Be(4294967295U);
            Pick(2, x => SqlFunctions.ClickHouse.to_uint64(x.I64)).Should().Be(18446744073709551615UL);
            Pick(6, x => SqlFunctions.ClickHouse.to_uint16(x.U64)).Should().Be(0);
            Pick(7, x => SqlFunctions.ClickHouse.to_uint32(x.U64)).Should().Be(0U);
        }
    }

    [Fact]
    public void TextParsing_ShouldThrowOrFallBack()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        T Pick<T>(int id, Expression<Func<IT286bConversionEntity, T>> selector)
        {
            // A fresh context per projection: the distinct Select expressions must never share a
            // cached plan (an or_null arm must not stand in for the throwing arm).
            using var local = Provider.CreateContext();
            return local.From<IT286bConversionEntity>().Where(x => x.Id == id).Select(selector).ToList().Single();
        }

        using (new AssertionScope())
        {
            Pick(1, x => SqlFunctions.ClickHouse.to_int8(x.Txt)).Should().Be((short)127);
            Pick(1, x => SqlFunctions.ClickHouse.to_int8_or_null(x.Txt)).Should().Be((short)127);
            Pick(1, x => SqlFunctions.ClickHouse.to_int8_or_default(x.Txt)).Should().Be((short)127);

            Pick(2, x => SqlFunctions.ClickHouse.to_int8_or_null(x.Txt)).Should().BeNull("'abc' is not a number");
            Pick(2, x => SqlFunctions.ClickHouse.to_int8_or_default(x.Txt)).Should().Be((short)0);
            Pick(2, x => SqlFunctions.ClickHouse.to_int8_or_default(x.Txt, (short)7)).Should().Be((short)7);
            Pick(2, x => SqlFunctions.ClickHouse.to_float64_or_null(x.Txt)).Should().BeNull();
            Pick(2, x => SqlFunctions.ClickHouse.to_float64_or_default(x.Txt)).Should().Be(0d);
            Pick(2, x => SqlFunctions.ClickHouse.to_decimal64_or_null(x.Txt, 2)).Should().BeNull();
            Pick(2, x => SqlFunctions.ClickHouse.to_decimal64_or_default(x.Txt, 2)).Should().Be(0m);

            Pick(3, x => SqlFunctions.ClickHouse.to_int8_or_null(x.Txt)).Should().BeNull("the empty string is too short");
            Pick(3, x => SqlFunctions.ClickHouse.to_int8_or_default(x.Txt)).Should().Be((short)0);
            Pick(5, x => SqlFunctions.ClickHouse.to_int8_or_null(x.Txt)).Should().BeNull("'12.9' is not an integer");

            FluentActions.Invoking(() => Pick(2, x => SqlFunctions.ClickHouse.to_int8(x.Txt)))
                .Should().Throw<Exception>().WithMessage("*CANNOT_PARSE_TEXT*");
            FluentActions.Invoking(() => Pick(3, x => SqlFunctions.ClickHouse.to_int8(x.Txt)))
                .Should().Throw<Exception>().WithMessage("*ATTEMPT_TO_READ_AFTER_EOF*");
            FluentActions.Invoking(() => Pick(5, x => SqlFunctions.ClickHouse.to_int8(x.Txt)))
                .Should().Throw<Exception>().WithMessage("*CANNOT_PARSE_TEXT*");
            FluentActions.Invoking(() => Pick(2, x => SqlFunctions.ClickHouse.to_float64(x.Txt)))
                .Should().Throw<Exception>().WithMessage("*CANNOT_PARSE_TEXT*");
            FluentActions.Invoking(() => Pick(3, x => SqlFunctions.ClickHouse.to_float64(x.Txt)))
                .Should().Throw<Exception>().WithMessage("*CANNOT_PARSE_NUMBER*");
            FluentActions.Invoking(() => Pick(2, x => SqlFunctions.ClickHouse.to_bool(x.Txt)))
                .Should().Throw<Exception>().WithMessage("*CANNOT_PARSE_BOOL*");
            FluentActions.Invoking(() => Pick(3, x => SqlFunctions.ClickHouse.to_bool(x.Txt)))
                .Should().Throw<Exception>().WithMessage("*CANNOT_PARSE_BOOL*");
        }
    }

    [Fact]
    public void NullMatrix_ShouldPropagateOrDefault()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bConversionEntity>()
            .Where(x => x.Id == 4)
            .Select(x => new
            {
                I8 = SqlFunctions.ClickHouse.to_int8(x.I64),
                I8OrNull = SqlFunctions.ClickHouse.to_int8_or_null(x.Text),
                I8OrZero = SqlFunctions.ClickHouse.to_int8_or_default(x.I64),
                I8OrDefault = SqlFunctions.ClickHouse.to_int8_or_default(x.I64, (short)7),
                Str = SqlFunctions.ClickHouse.to_string(x.I64),
                Flag = SqlFunctions.ClickHouse.to_bool(x.I64),
                Fixed = SqlFunctions.ClickHouse.to_fixed_string(x.Text, 5),
                Low = SqlFunctions.ClickHouse.to_low_cardinality(x.Text),
                Dec = SqlFunctions.ClickHouse.to_decimal32(x.Dec, 2),
                DecOrNull = SqlFunctions.ClickHouse.to_decimal32_or_null(x.Text, 2),
                DecOrZero = SqlFunctions.ClickHouse.to_decimal32_or_default(x.Dec, 2),
                CheckedOrNull = SqlFunctions.ClickHouse.accurate_cast_int8_or_null(x.I64),
                CheckedOrZero = SqlFunctions.ClickHouse.accurate_cast_int8_or_default(x.I64),
                F32 = SqlFunctions.ClickHouse.to_float32(x.F64),
                Bits = SqlFunctions.ClickHouse.reinterpret_as_uint32(x.Txt)
            })
            .First();

        using (new AssertionScope())
        {
            r.I8.Should().BeNull();
            r.I8OrNull.Should().BeNull();
            r.I8OrZero.Should().Be((short)0);
            r.I8OrDefault.Should().Be((short)7, "the explicit default replaces a NULL input");
            r.Str.Should().BeNull();
            r.Flag.Should().BeNull();
            r.Fixed.Should().BeNull();
            r.Low.Should().BeNull();
            r.Dec.Should().BeNull();
            r.DecOrNull.Should().BeNull();
            r.DecOrZero.Should().Be(0m);
            r.CheckedOrNull.Should().BeNull();
            r.CheckedOrZero.Should().Be((short)0);
            r.F32.Should().BeNull();
            r.Bits.Should().Be(0U, "reinterpretAs* reads the zero bytes of a NULL source, it does not propagate NULL");
        }
    }

    [Fact]
    public void FloatConversions_ShouldHandleSpecialValuesAndRange()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        T Pick<T>(int id, Expression<Func<IT286bConversionEntity, T>> selector)
        {
            // A fresh context per projection: the distinct Select expressions must never share a
            // cached plan (an or_null arm must not stand in for the throwing arm).
            using var local = Provider.CreateContext();
            return local.From<IT286bConversionEntity>().Where(x => x.Id == id).Select(selector).ToList().Single();
        }

        using (new AssertionScope())
        {
            Pick(7, x => SqlFunctions.ClickHouse.to_float32(x.Text)).Should().Be(float.NaN);
            Pick(7, x => SqlFunctions.ClickHouse.to_float64(x.Text)).Should().Be(double.NaN);
            Pick(8, x => SqlFunctions.ClickHouse.to_float32(x.Text)).Should().Be(float.PositiveInfinity);
            Pick(8, x => SqlFunctions.ClickHouse.to_float64(x.Text)).Should().Be(double.PositiveInfinity);
            Pick(6, x => SqlFunctions.ClickHouse.to_float32(x.F64)).Should().Be(float.NegativeInfinity,
                "a Float64 value below the Float32 range saturates instead of raising");
            Pick(8, x => SqlFunctions.ClickHouse.to_float32(x.F64)).Should().Be(float.PositiveInfinity);

            Pick(1, x => SqlFunctions.ClickHouse.to_int8(x.F64)).Should().Be((short)12, "12.7 truncates toward zero");
            Pick(2, x => SqlFunctions.ClickHouse.to_int8(x.F64)).Should().Be((short)-12, "-12.7 truncates toward zero");
            Pick(3, x => SqlFunctions.ClickHouse.to_int32(x.F64)).Should().Be(1);
            Pick(5, x => SqlFunctions.ClickHouse.to_float64(x.F64)).Should().Be(0.5);
        }
    }

    [Fact]
    public void DecimalConversions_ShouldTruncateToScaleAndBoundTheDigits()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        T Pick<T>(int id, Expression<Func<IT286bConversionEntity, T>> selector)
        {
            // A fresh context per projection: the distinct Select expressions must never share a
            // cached plan (an or_null arm must not stand in for the throwing arm).
            using var local = Provider.CreateContext();
            return local.From<IT286bConversionEntity>().Where(x => x.Id == id).Select(selector).ToList().Single();
        }

        using (new AssertionScope())
        {
            Pick(1, x => SqlFunctions.ClickHouse.to_decimal32(x.Dec, 2)).Should().Be(12.34m);
            Pick(1, x => SqlFunctions.ClickHouse.to_decimal64(x.Dec, 2)).Should().Be(12.34m);
            Pick(5, x => SqlFunctions.ClickHouse.to_decimal32(x.Dec, 2)).Should().Be(12.99m,
                "12.999 is truncated, not rounded");
            Pick(6, x => SqlFunctions.ClickHouse.to_decimal64(x.Dec, 2)).Should().Be(9999999999999999.99m);
            Pick(1, x => SqlFunctions.ClickHouse.to_decimal32(x.Txt, 2)).Should().Be(127.00m);
            Pick(5, x => SqlFunctions.ClickHouse.to_decimal64(x.Txt, 2)).Should().Be(12.90m);
            Pick(2, x => SqlFunctions.ClickHouse.to_decimal32_or_default(x.Txt, 2, 7.5m)).Should().Be(7.50m);

            FluentActions.Invoking(() => Pick(2, x => SqlFunctions.ClickHouse.to_decimal32(x.Txt, 2)))
                .Should().Throw<Exception>().WithMessage("*CANNOT_PARSE_TEXT*");
            FluentActions.Invoking(() => Pick(3, x => SqlFunctions.ClickHouse.to_decimal32(x.Txt, 2)))
                .Should().Throw<Exception>().WithMessage("*ATTEMPT_TO_READ_AFTER_EOF*");
            FluentActions.Invoking(() => Pick(14, x => SqlFunctions.ClickHouse.to_decimal64(x.Txt, 2)))
                .Should().Throw<Exception>().WithMessage("*ARGUMENT_OUT_OF_BOUND*");
            FluentActions.Invoking(() => Pick(15, x => SqlFunctions.ClickHouse.to_decimal32(x.Txt, 9)))
                .Should().Throw<Exception>().WithMessage("*ARGUMENT_OUT_OF_BOUND*");
        }
    }

    [Fact]
    public void ToString_ShouldPinTheVendorFormatting()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        T Pick<T>(int id, Expression<Func<IT286bConversionEntity, T>> selector)
        {
            // A fresh context per projection: the distinct Select expressions must never share a
            // cached plan (an or_null arm must not stand in for the throwing arm).
            using var local = Provider.CreateContext();
            return local.From<IT286bConversionEntity>().Where(x => x.Id == id).Select(selector).ToList().Single();
        }

        using (new AssertionScope())
        {
            Pick(1, x => SqlFunctions.ClickHouse.to_string(x.I64)).Should().Be("300");
            Pick(9, x => SqlFunctions.ClickHouse.to_string(x.F64)).Should().Be("0.3333333333333333",
                "Float64 uses the shortest round-trip representation");
            Pick(2, x => SqlFunctions.ClickHouse.to_string(x.F64)).Should().Be("-12.7");
            Pick(1, x => SqlFunctions.ClickHouse.to_string(x.Dec)).Should().Be("12.34");
            Pick(1, x => SqlFunctions.ClickHouse.to_string(x.Dte)).Should().Be("2024-01-02 03:04:05");
            Pick(1, x => SqlFunctions.ClickHouse.to_string(x.Text)).Should().Be("127");
            Pick(12, x => SqlFunctions.ClickHouse.to_string(x.U64)).Should().Be("81985529216486895");
            Pick(11, x => SqlFunctions.ClickHouse.to_string(SqlFunctions.ClickHouse.to_bool(x.Txt)))
                .Should().Be("true");
        }
    }

    [Fact]
    public void ToBool_ShouldPinTheAcceptedTextAndNumericForms()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        T Pick<T>(int id, Expression<Func<IT286bConversionEntity, T>> selector)
        {
            // A fresh context per projection: the distinct Select expressions must never share a
            // cached plan (an or_null arm must not stand in for the throwing arm).
            using var local = Provider.CreateContext();
            return local.From<IT286bConversionEntity>().Where(x => x.Id == id).Select(selector).ToList().Single();
        }

        using (new AssertionScope())
        {
            Pick(1, x => SqlFunctions.ClickHouse.to_bool(x.I64)).Should().BeTrue();
            Pick(3, x => SqlFunctions.ClickHouse.to_bool(x.I64)).Should().BeFalse();
            Pick(5, x => SqlFunctions.ClickHouse.to_bool(x.F64)).Should().BeTrue("0.5 is non-zero");
            Pick(9, x => SqlFunctions.ClickHouse.to_bool(x.Txt)).Should().BeTrue("'yes' is an accepted form");
            Pick(10, x => SqlFunctions.ClickHouse.to_bool(x.Txt)).Should().BeFalse("'off' is an accepted form");
            Pick(11, x => SqlFunctions.ClickHouse.to_bool(x.Txt)).Should().BeTrue("'true' is an accepted form");
        }
    }

    [Fact]
    public void FixedStringAndLowCardinality_ShouldPinTheShape()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        T Pick<T>(int id, Expression<Func<IT286bConversionEntity, T>> selector)
        {
            // A fresh context per projection: the distinct Select expressions must never share a
            // cached plan (an or_null arm must not stand in for the throwing arm).
            using var local = Provider.CreateContext();
            return local.From<IT286bConversionEntity>().Where(x => x.Id == id).Select(selector).ToList().Single();
        }

        using (new AssertionScope())
        {
            var fixedValue = Pick(1, x => SqlFunctions.ClickHouse.to_fixed_string(x.Txt, 5));
            fixedValue.Should().Be("127\0\0");
            fixedValue!.Length.Should().Be(5);
            Pick(3, x => SqlFunctions.ClickHouse.to_fixed_string(x.Txt, 2)).Should().Be("\0\0");
            Pick(1, x => SqlFunctions.ClickHouse.to_low_cardinality(x.Txt)).Should().Be("127");

            FluentActions.Invoking(() => Pick(1, x => SqlFunctions.ClickHouse.to_fixed_string(x.Txt, 2)))
                .Should().Throw<Exception>().WithMessage("*TOO_LARGE_STRING_SIZE*");

            // A bare NULL literal has type Nullable(Nothing), which the vendor rejects for LowCardinality.
            FluentActions.Invoking(() => ctx.From<IT286bConversionEntity>()
                    .Where(x => x.Id == 1)
                    .Select(x => new { V = SqlFunctions.ClickHouse.to_low_cardinality<string>(null) })
                    .First())
                .Should().Throw<Exception>().WithMessage("*LowCardinality*");
        }
    }

    [Fact]
    public void AccurateCast_ShouldBeCheckedUnlikeTo()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        T Pick<T>(int id, Expression<Func<IT286bConversionEntity, T>> selector)
        {
            // A fresh context per projection: the distinct Select expressions must never share a
            // cached plan (an or_null arm must not stand in for the throwing arm).
            using var local = Provider.CreateContext();
            return local.From<IT286bConversionEntity>().Where(x => x.Id == id).Select(selector).ToList().Single();
        }

        using (new AssertionScope())
        {
            Pick(1, x => SqlFunctions.ClickHouse.to_int8(x.I64)).Should().Be((short)44);
            FluentActions.Invoking(() => Pick(1, x => SqlFunctions.ClickHouse.accurate_cast_int8(x.I64)))
                .Should().Throw<Exception>().WithMessage("*CANNOT_CONVERT_TYPE*");
            Pick(1, x => SqlFunctions.ClickHouse.accurate_cast_int8_or_null(x.I64)).Should().BeNull();
            Pick(1, x => SqlFunctions.ClickHouse.accurate_cast_int8_or_default(x.I64)).Should().Be((short)0);

            Pick(7, x => SqlFunctions.ClickHouse.accurate_cast_int8(x.F64)).Should().Be((short)12,
                "12.0 is an exact Float64 value");
            FluentActions.Invoking(() => Pick(1, x => SqlFunctions.ClickHouse.accurate_cast_int8(x.F64)))
                .Should().Throw<Exception>().WithMessage("*CANNOT_CONVERT_TYPE*");
            FluentActions.Invoking(() => Pick(2, x => SqlFunctions.ClickHouse.accurate_cast_uint8(x.I64)))
                .Should().Throw<Exception>().WithMessage("*CANNOT_CONVERT_TYPE*");
            FluentActions.Invoking(() => Pick(7, x => SqlFunctions.ClickHouse.accurate_cast_uint32(x.U64)))
                .Should().Throw<Exception>().WithMessage("*CANNOT_CONVERT_TYPE*");

            Pick(3, x => SqlFunctions.ClickHouse.accurate_cast_float32(x.F64)).Should().Be(1.5f);
            FluentActions.Invoking(() => Pick(15, x => SqlFunctions.ClickHouse.accurate_cast_float32(x.F64)))
                .Should().Throw<Exception>().WithMessage("*CANNOT_CONVERT_TYPE*");

            Pick(1, x => SqlFunctions.ClickHouse.accurate_cast_int8(x.Txt)).Should().Be((short)127);
            Pick(2, x => SqlFunctions.ClickHouse.accurate_cast_int8_or_null(x.Txt)).Should().BeNull();
            Pick(2, x => SqlFunctions.ClickHouse.accurate_cast_int8_or_default(x.Txt)).Should().Be((short)0);

            Pick(1, x => SqlFunctions.ClickHouse.accurate_cast_decimal32(x.Dec, 2)).Should().Be(12.34m);
            Pick(6, x => SqlFunctions.ClickHouse.accurate_cast_decimal64(x.Dec, 2))
                .Should().Be(9999999999999999.99m);
        }
    }

    [Fact]
    public void Reinterpret_ShouldPreserveBitPatterns()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        T Pick<T>(int id, Expression<Func<IT286bConversionEntity, T>> selector)
        {
            // A fresh context per projection: the distinct Select expressions must never share a
            // cached plan (an or_null arm must not stand in for the throwing arm).
            using var local = Provider.CreateContext();
            return local.From<IT286bConversionEntity>().Where(x => x.Id == id).Select(selector).ToList().Single();
        }

        using (new AssertionScope())
        {
            // 'abcd' = 0x61 0x62 0x63 0x64, little-endian 0x64636261.
            Pick(12, x => SqlFunctions.ClickHouse.reinterpret_as_uint32(x.Txt)).Should().Be(1684234849U);
            Pick(12, x => SqlFunctions.ClickHouse.reinterpret_as_int32(x.Txt)).Should().Be(1684234849);
            Pick(12, x => SqlFunctions.ClickHouse.reinterpret_as_uint16(x.Txt)).Should().Be(25185);
            Pick(12, x => SqlFunctions.ClickHouse.reinterpret_as_int16(x.Txt)).Should().Be((short)25185);
            Pick(12, x => SqlFunctions.ClickHouse.reinterpret_as_uint8(x.Txt)).Should().Be((byte)97);
            Pick(12, x => SqlFunctions.ClickHouse.reinterpret_as_int8(x.Txt)).Should().Be((short)97);

            // 0x0123456789ABCDEF round-trips through the string form byte for byte (little-endian).
            Pick(12, x => SqlFunctions.ClickHouse.reinterpret_as_uint64(
                    SqlFunctions.ClickHouse.reinterpret_as_string(x.U64))).Should().Be(81985529216486895UL);
            Pick(12, x => SqlFunctions.ClickHouse.reinterpret_as_uint64(
                    SqlFunctions.ClickHouse.reinterpret_as_fixed_string(x.U64))).Should().Be(81985529216486895UL);

            // Float bit patterns.
            Pick(15, x => SqlFunctions.ClickHouse.reinterpret_as_float32(
                    SqlFunctions.ClickHouse.to_uint32(x.I64))).Should().Be(1.0f);
            Pick(13, x => SqlFunctions.ClickHouse.reinterpret_as_float64(x.U64)).Should().Be(1.0);
            Pick(15, x => SqlFunctions.ClickHouse.reinterpret_as_uint32(
                    SqlFunctions.ClickHouse.reinterpret_as_float32(
                        SqlFunctions.ClickHouse.to_uint32(x.I64)))).Should().Be(1065353216U);
            Pick(11, x => SqlFunctions.ClickHouse.reinterpret_as_uint64(
                    SqlFunctions.ClickHouse.to_float64(x.F64))).Should().Be(4607182418800017408UL);

            // Date/DateTime reinterpretation of the numeric storage.
            Pick(14, x => SqlFunctions.ClickHouse.reinterpret_as_date(x.U64)).Should().Be(new DateTime(2024, 1, 1));
            Pick(14, x => SqlFunctions.ClickHouse.reinterpret_as_date_time(x.I64))
                .Should().Be(new DateTime(2024, 1, 2, 3, 4, 5));

            // UUID reinterpretation of the 16-byte text form.
            Pick(13, x => SqlFunctions.ClickHouse.reinterpret_as_uuid(x.Txt))
                .Should().Be(new Guid("37363534-3332-3130-6665-646362613938"));
        }
    }

    [Fact]
    public void ParameterisedSources_ShouldBindAndConvert()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var number = 127L;
        var text = "12";
        var r = ctx.From<IT286bConversionEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                FromNumber = SqlFunctions.ClickHouse.to_int8(number),
                FromText = SqlFunctions.ClickHouse.to_int8(text),
                NumberText = SqlFunctions.ClickHouse.to_string(number),
                DecimalFromNumber = SqlFunctions.ClickHouse.to_decimal64(number, 2)
            })
            .First();

        using (new AssertionScope())
        {
            r.FromNumber.Should().Be((short)127);
            r.FromText.Should().Be((short)12);
            r.NumberText.Should().Be("127");
            r.DecimalFromNumber.Should().Be(127.00m);
        }
    }

    [Fact]
    public void Projection_ShouldDeclareTheExactClrResultTypes()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bConversionEntity>()
            .Where(x => x.Id == 13)
            .Select(x => new
            {
                I8 = SqlFunctions.ClickHouse.to_int8(x.I64),
                U16 = SqlFunctions.ClickHouse.to_uint16(x.I64),
                U64 = SqlFunctions.ClickHouse.to_uint64(x.U64),
                F32 = SqlFunctions.ClickHouse.to_float32(x.F64),
                D64 = SqlFunctions.ClickHouse.to_decimal64(x.Dec, 2),
                Str = SqlFunctions.ClickHouse.to_string(x.I64),
                Flag = SqlFunctions.ClickHouse.to_bool(x.I64),
                Bits = SqlFunctions.ClickHouse.reinterpret_as_uint32(x.Txt),
                Dt = SqlFunctions.ClickHouse.reinterpret_as_date_time(x.I64),
                Id = SqlFunctions.ClickHouse.reinterpret_as_uuid(x.Txt)
            })
            .First();

        var type = r.GetType();
        using (new AssertionScope())
        {
            type.GetProperty("I8")!.PropertyType.Should().Be(typeof(short?));
            type.GetProperty("U16")!.PropertyType.Should().Be(typeof(int?));
            type.GetProperty("U64")!.PropertyType.Should().Be(typeof(ulong?));
            type.GetProperty("F32")!.PropertyType.Should().Be(typeof(float?));
            type.GetProperty("D64")!.PropertyType.Should().Be(typeof(decimal?));
            type.GetProperty("Str")!.PropertyType.Should().Be(typeof(string));
            type.GetProperty("Flag")!.PropertyType.Should().Be(typeof(bool?));
            type.GetProperty("Bits")!.PropertyType.Should().Be(typeof(uint?));
            type.GetProperty("Dt")!.PropertyType.Should().Be(typeof(DateTime?));
            type.GetProperty("Id")!.PropertyType.Should().Be(typeof(Guid?));
        }
    }

    [Fact]
    public void Refusals_ShouldNeverReachTheServer()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var e = ctx.From<IT286bConversionEntity>();

        using (new AssertionScope())
        {
            void Refused<T>(string name, Func<QueryCommand<T>> query)
                => FluentActions.Invoking(() => Prepare(ctx, query()))
                    .Should().Throw<NotSupportedException>()
                    .WithMessage($"The {name} function is refused:*");

            Refused("to_int128", () => e.Select(x => new { V = SqlFunctions.ClickHouse.to_int128(x.I64) }));
            Refused("to_uint128", () => e.Select(x => new { V = SqlFunctions.ClickHouse.to_uint128(x.I64) }));
            Refused("to_int256", () => e.Select(x => new { V = SqlFunctions.ClickHouse.to_int256(x.I64) }));
            Refused("to_uint256", () => e.Select(x => new { V = SqlFunctions.ClickHouse.to_uint256(x.I64) }));
            Refused("to_decimal128", () => e.Select(x => new { V = SqlFunctions.ClickHouse.to_decimal128(x.Dec, 2) }));
            Refused("to_decimal256", () => e.Select(x => new { V = SqlFunctions.ClickHouse.to_decimal256(x.Dec, 2) }));
            Refused("to_interval_nanosecond", () => e.Select(x => new { V = SqlFunctions.ClickHouse.to_interval_nanosecond(x.I64) }));
            Refused("to_interval_microsecond", () => e.Select(x => new { V = SqlFunctions.ClickHouse.to_interval_microsecond(x.I64) }));
            Refused("to_interval_millisecond", () => e.Select(x => new { V = SqlFunctions.ClickHouse.to_interval_millisecond(x.I64) }));
            Refused("to_interval_second", () => e.Select(x => new { V = SqlFunctions.ClickHouse.to_interval_second(x.I64) }));
            Refused("to_interval_minute", () => e.Select(x => new { V = SqlFunctions.ClickHouse.to_interval_minute(x.I64) }));
            Refused("to_interval_hour", () => e.Select(x => new { V = SqlFunctions.ClickHouse.to_interval_hour(x.I64) }));
            Refused("to_interval_day", () => e.Select(x => new { V = SqlFunctions.ClickHouse.to_interval_day(x.I64) }));
            Refused("to_interval_week", () => e.Select(x => new { V = SqlFunctions.ClickHouse.to_interval_week(x.I64) }));
            Refused("to_interval_month", () => e.Select(x => new { V = SqlFunctions.ClickHouse.to_interval_month(x.I64) }));
            Refused("to_interval_quarter", () => e.Select(x => new { V = SqlFunctions.ClickHouse.to_interval_quarter(x.I64) }));
            Refused("to_interval_year", () => e.Select(x => new { V = SqlFunctions.ClickHouse.to_interval_year(x.I64) }));
            Refused("reinterpret_as_uint128", () => e.Select(x => new { V = SqlFunctions.ClickHouse.reinterpret_as_uint128(x.I64) }));
            Refused("reinterpret_as_uint256", () => e.Select(x => new { V = SqlFunctions.ClickHouse.reinterpret_as_uint256(x.I64) }));
            Refused("reinterpret_as_int128", () => e.Select(x => new { V = SqlFunctions.ClickHouse.reinterpret_as_int128(x.I64) }));
            Refused("reinterpret_as_int256", () => e.Select(x => new { V = SqlFunctions.ClickHouse.reinterpret_as_int256(x.I64) }));
        }
    }

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
}
