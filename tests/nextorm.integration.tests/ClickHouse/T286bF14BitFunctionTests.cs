using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClickHouse.Driver;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T286b D14 (F14 Bit): live execution of the ClickHouse-only bit family against the pinned
/// <c>25.8-alpine</c> (25.8.33.6) server. The tests pin the width/signedness contract (UInt8 →
/// byte, UInt16 → exact <c>toInt32</c> widening, UInt32 → uint, UInt64 → ulong), zero and boundary
/// values, popcount and the leading/trailing zero runs, the vendor's shift-count semantics (including
/// the exact-width quirk on UInt32/UInt64), the modulo-width rotations, the boolean <c>bitTest*</c>
/// results and their out-of-range rejection, the string Hamming form, the NULL matrix, and the fact
/// that ClickHouse has no <c>&amp;</c>/<c>|</c>/<c>^</c>/<c>~</c> bit operators.
/// </summary>
[Collection("ClickHouse")]
public sealed class T286bF14BitFunctionTests : ProviderTestSuite
{
    [SqlTable("t286b_f14_entity")]
    internal interface IT286bBitEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("u8")]
        byte? U8 { get; set; }

        [Column("u16")]
        ushort? U16 { get; set; }

        [Column("u32")]
        uint? U32 { get; set; }

        [Column("u64")]
        ulong? U64 { get; set; }

        [Column("text")]
        string? Text { get; set; }
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
                     "drop table if exists t286b_f14_entity",
                     "create table t286b_f14_entity (id Int32, u8 Nullable(UInt8), " +
                     "u16 Nullable(UInt16), u32 Nullable(UInt32), u64 Nullable(UInt64), " +
                     "text Nullable(String)) engine = Memory",
                     "insert into t286b_f14_entity (id, u8, u16, u32, u64, text) values " +
                     "(1, 12, 32769, 2147483649, 9223372036854775809, 'abc'), " +
                     "(2, 0, 0, 0, 0, 'abd'), " +
                     "(3, 255, 65535, 4294967295, 18446744073709551615, ''), " +
                     "(4, NULL, NULL, NULL, NULL, NULL)"
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    [Fact]
    public void AllMembers_ShouldExecuteOnThePinnedServer()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bBitEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                And = SqlFunctions.ClickHouse.bit_and(x.U64, 18446744073709551615UL),
                Or = SqlFunctions.ClickHouse.bit_or(x.U8, 15UL),
                Xor = SqlFunctions.ClickHouse.bit_xor(x.U8, 10UL),
                Not = SqlFunctions.ClickHouse.bit_not(x.U8),
                ShiftLeft = SqlFunctions.ClickHouse.bit_shift_left(x.U8, 2),
                ShiftRight = SqlFunctions.ClickHouse.bit_shift_right(x.U8, 2),
                RotateLeft = SqlFunctions.ClickHouse.bit_rotate_left(x.U8, 1),
                RotateRight = SqlFunctions.ClickHouse.bit_rotate_right(x.U8, 1),
                Count = SqlFunctions.ClickHouse.bit_count(x.U8),
                TestSet = SqlFunctions.ClickHouse.bit_test(x.U8, 2),
                TestClear = SqlFunctions.ClickHouse.bit_test(x.U8, 1),
                TestAll = SqlFunctions.ClickHouse.bit_test_all(x.U8, 2, 3),
                TestAny = SqlFunctions.ClickHouse.bit_test_any(x.U8, 0, 1),
                HammingNumber = SqlFunctions.ClickHouse.bit_hamming_distance(x.U32, 0UL),
                HammingText = SqlFunctions.ClickHouse.bit_hamming_distance(x.Text, "abd")
            })
            .First();

        using (new AssertionScope())
        {
            r.And.Should().Be(9223372036854775809UL);
            r.Or.Should().Be(15UL);
            r.Xor.Should().Be(6UL);
            r.Not.Should().Be((byte)243);
            r.ShiftLeft.Should().Be((byte)48);
            r.ShiftRight.Should().Be((byte)3);
            r.RotateLeft.Should().Be((byte)24);
            r.RotateRight.Should().Be((byte)6);
            r.Count.Should().Be((byte)2);
            r.TestSet.Should().BeTrue();
            r.TestClear.Should().BeFalse();
            r.TestAll.Should().BeTrue();
            r.TestAny.Should().BeFalse();
            r.HammingNumber.Should().Be((byte)2);
            r.HammingText.Should().Be(3UL, "'c' (0x63) and 'd' (0x64) differ in three bits");
        }
    }

    [Fact]
    public void Widths_ShouldKeepTheDeclaredClrTypeAndWidenUInt16Exactly()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bBitEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Not8 = SqlFunctions.ClickHouse.bit_not(x.U8),
                Not16 = SqlFunctions.ClickHouse.bit_not(x.U16),
                Not32 = SqlFunctions.ClickHouse.bit_not(x.U32),
                Not64 = SqlFunctions.ClickHouse.bit_not(x.U64),
                Shift16 = SqlFunctions.ClickHouse.bit_shift_left(x.U16, 1),
                Rotate16 = SqlFunctions.ClickHouse.bit_rotate_left(x.U16, 1),
                Shift64 = SqlFunctions.ClickHouse.bit_shift_left(x.U64, 1),
                Rotate64 = SqlFunctions.ClickHouse.bit_rotate_left(x.U64, 1)
            })
            .First();

        using (new AssertionScope())
        {
            r.Not8.Should().Be((byte)243);
            r.Not16.Should().Be(32766, "UInt16 is widened exactly to int");
            r.Not32.Should().Be(2147483646U);
            r.Not64.Should().Be(9223372036854775806UL);

            // UInt16 keeps the vendor's 16-bit width, so the left shift truncates at 65536.
            r.Shift16.Should().Be(2);
            r.Rotate16.Should().Be(3);
            r.Shift64.Should().Be(2UL);
            r.Rotate64.Should().Be(3UL);
        }
    }

    [Fact]
    public void ZeroAndAllOnes_ShouldPinTheBoundaryValues()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var zero = ctx.From<IT286bBitEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                Not8 = SqlFunctions.ClickHouse.bit_not(x.U8),
                Not16 = SqlFunctions.ClickHouse.bit_not(x.U16),
                Not32 = SqlFunctions.ClickHouse.bit_not(x.U32),
                Not64 = SqlFunctions.ClickHouse.bit_not(x.U64),
                Count8 = SqlFunctions.ClickHouse.bit_count(x.U8),
                Count64 = SqlFunctions.ClickHouse.bit_count(x.U64),
                And = SqlFunctions.ClickHouse.bit_and(x.U64, 18446744073709551615UL),
                Test = SqlFunctions.ClickHouse.bit_test(x.U8, 0)
            })
            .First();

        var ones = ctx.From<IT286bBitEntity>()
            .Where(x => x.Id == 3)
            .Select(x => new
            {
                Not8 = SqlFunctions.ClickHouse.bit_not(x.U8),
                Not16 = SqlFunctions.ClickHouse.bit_not(x.U16),
                Not32 = SqlFunctions.ClickHouse.bit_not(x.U32),
                Not64 = SqlFunctions.ClickHouse.bit_not(x.U64),
                Count8 = SqlFunctions.ClickHouse.bit_count(x.U8),
                Count16 = SqlFunctions.ClickHouse.bit_count(x.U16),
                Count32 = SqlFunctions.ClickHouse.bit_count(x.U32),
                Count64 = SqlFunctions.ClickHouse.bit_count(x.U64),
                Test7 = SqlFunctions.ClickHouse.bit_test(x.U8, 7),
                TestAll = SqlFunctions.ClickHouse.bit_test_all(x.U8, 0, 7),
                Hamming = SqlFunctions.ClickHouse.bit_hamming_distance(x.U64, 0UL)
            })
            .First();

        using (new AssertionScope())
        {
            zero.Not8.Should().Be((byte)255);
            zero.Not16.Should().Be(65535);
            zero.Not32.Should().Be(uint.MaxValue);
            zero.Not64.Should().Be(ulong.MaxValue);
            zero.Count8.Should().Be((byte)0);
            zero.Count64.Should().Be((byte)0);
            zero.And.Should().Be(0UL);
            zero.Test.Should().BeFalse();

            ones.Not8.Should().Be((byte)0);
            ones.Not16.Should().Be(0);
            ones.Not32.Should().Be(0U);
            ones.Not64.Should().Be(0UL);
            ones.Count8.Should().Be((byte)8);
            ones.Count16.Should().Be((byte)16);
            ones.Count32.Should().Be((byte)32);
            ones.Count64.Should().Be((byte)64);
            ones.Test7.Should().BeTrue();
            ones.TestAll.Should().BeTrue();
            ones.Hamming.Should().Be((byte)64);
        }
    }

    [Fact]
    public void Shifts_ShouldUseTheVendorCountSemantics()
    {
        Seed();

        using (new AssertionScope())
        {
            // UInt8/UInt16: a count equal to the width already yields 0.
            Scalar<byte>("bitShiftLeft(toUInt8(1), 0)").Should().Be(1);
            Scalar<byte>("bitShiftLeft(toUInt8(1), 7)").Should().Be(128);
            Scalar<byte>("bitShiftLeft(toUInt8(1), 8)").Should().Be(0);
            Scalar<byte>("bitShiftLeft(toUInt8(1), 9)").Should().Be(0);
            Scalar<byte>("bitShiftLeft(toUInt8(1), 255)").Should().Be(0);
            Scalar<byte>("bitShiftRight(toUInt8(128), 7)").Should().Be(1);
            Scalar<byte>("bitShiftRight(toUInt8(128), 8)").Should().Be(0);

            Scalar<int>("toInt32(bitShiftLeft(toUInt16(1), 15))").Should().Be(32768);
            Scalar<int>("toInt32(bitShiftLeft(toUInt16(1), 16))").Should().Be(0);

            // UInt32/UInt64: 25.8.33.6 returns the value unchanged for a count exactly equal to the
            // width and 0 for every larger count (a vendor quirk, not the C# shift contract).
            Scalar<uint>("bitShiftLeft(toUInt32(1), 31)").Should().Be(2147483648U);
            Scalar<uint>("bitShiftLeft(toUInt32(1), 32)").Should().Be(1U);
            Scalar<uint>("bitShiftLeft(toUInt32(1), 33)").Should().Be(0U);
            Scalar<uint>("bitShiftLeft(toUInt32(1), 255)").Should().Be(0U);
            Scalar<uint>("bitShiftRight(toUInt32(0x80000001), 32)").Should().Be(2147483649U);
            Scalar<uint>("bitShiftRight(toUInt32(0x80000001), 33)").Should().Be(0U);

            Scalar<ulong>("bitShiftLeft(toUInt64(1), 63)").Should().Be(9223372036854775808UL);
            Scalar<ulong>("bitShiftLeft(toUInt64(1), 64)").Should().Be(1UL);
            Scalar<ulong>("bitShiftLeft(toUInt64(1), 65)").Should().Be(0UL);
            Scalar<ulong>("bitShiftRight(toUInt64(0x8000000000000001), 64)")
                .Should().Be(9223372036854775809UL);
            Scalar<ulong>("bitShiftRight(toUInt64(0x8000000000000001), 65)").Should().Be(0UL);

            // A negative shift count is not representable through the byte parameter; a raw signed
            // count is rejected by the server rather than wrapping.
            FluentActions.Invoking(() => Scalar<byte>("bitShiftLeft(toUInt8(1), toInt8(-1))"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*negative*");
        }
    }

    [Fact]
    public void Rotations_ShouldWrapModuloTheWidth()
    {
        Seed();

        using (new AssertionScope())
        {
            Scalar<byte>("bitRotateLeft(toUInt8(1), 0)").Should().Be(1);
            Scalar<byte>("bitRotateLeft(toUInt8(1), 8)").Should().Be(1);
            Scalar<byte>("bitRotateLeft(toUInt8(1), 9)").Should().Be(2);
            Scalar<byte>("bitRotateLeft(toUInt8(1), 255)").Should().Be(128);
            Scalar<byte>("bitRotateRight(toUInt8(1), 255)").Should().Be(2);
            Scalar<byte>("bitRotateLeft(toUInt8(129), 1)").Should().Be(3);
            Scalar<byte>("bitRotateRight(toUInt8(129), 1)").Should().Be(192);

            Scalar<int>("toInt32(bitRotateLeft(toUInt16(1), 16))").Should().Be(1);
            Scalar<int>("toInt32(bitRotateLeft(toUInt16(1), 17))").Should().Be(2);
            Scalar<uint>("bitRotateLeft(toUInt32(1), 33)").Should().Be(2U);
            Scalar<ulong>("bitRotateLeft(toUInt64(1), 65)").Should().Be(2UL);
            Scalar<ulong>("bitRotateLeft(toUInt64(1), 128)").Should().Be(1UL);
        }
    }

    [Fact]
    public void PopcountAndBitTest_ShouldPinLeadingAndTrailingZeroRuns()
    {
        Seed();

        using (new AssertionScope())
        {
            // One set bit: bitCount is 1 and the boundary positions prove the leading/trailing runs.
            Scalar<byte>("bitCount(toUInt8(1))").Should().Be(1);
            Scalar<byte>("bitTest(toUInt8(1), 0)").Should().Be(1);
            Scalar<byte>("bitTest(toUInt8(1), 7)").Should().Be(0);
            Scalar<byte>("bitCount(toUInt8(128))").Should().Be(1);
            Scalar<byte>("bitTest(toUInt8(128), 7)").Should().Be(1);
            Scalar<byte>("bitTest(toUInt8(128), 0)").Should().Be(0);
            Scalar<byte>("bitCount(toUInt32(2147483648))").Should().Be(1);
            Scalar<byte>("bitCount(toUInt64(9223372036854775808))").Should().Be(1);
            Scalar<byte>("bitTest(toUInt64(9223372036854775808), 63)").Should().Be(1);
            Scalar<byte>("bitTest(toUInt64(9223372036854775808), 0)").Should().Be(0);

            // The vendor has no leading/trailing-zero-count function on the pinned image; the runs are
            // pinned through bitCount/bitTest only.
            Scalar<ulong>("count() from system.functions where name in " +
                "('bitCountLeadingZeros','bitCountTrailingZeros')").Should().Be(0UL);
        }
    }

    [Fact]
    public void BitTestFamily_ShouldRejectOutOfRangePositions()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        using (new AssertionScope())
        {
            FluentActions.Invoking(() => ctx.From<IT286bBitEntity>()
                    .Where(x => x.Id == 1)
                    .Select(x => new { V = SqlFunctions.ClickHouse.bit_test(x.U8, 8) })
                    .First())
                .Should().Throw<ClickHouseServerException>().WithMessage("*PARAMETER_OUT_OF_BOUND*");

            FluentActions.Invoking(() => Scalar<byte>("bitTest(toUInt64(1), 64)"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*PARAMETER_OUT_OF_BOUND*");

            FluentActions.Invoking(() => Scalar<byte>("bitTestAll(toUInt8(5))"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*TOO_FEW_ARGUMENTS*");
        }
    }

    [Fact]
    public void BitOperatorsAndSignedness_ShouldBePinnedAsVendorFacts()
    {
        Seed();

        using (new AssertionScope())
        {
            // ClickHouse has no symbolic bit operators: the function spelling is the only form.
            FluentActions.Invoking(() => Scalar<byte>("toUInt8(12) & toUInt8(10)"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*SYNTAX_ERROR*");
            FluentActions.Invoking(() => Scalar<byte>("toUInt8(12) | toUInt8(10)"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*SYNTAX_ERROR*");
            FluentActions.Invoking(() => Scalar<byte>("toUInt8(12) ^ toUInt8(10)"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*SYNTAX_ERROR*");
            FluentActions.Invoking(() => Scalar<byte>("~toUInt8(12)"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*SYNTAX_ERROR*");

            // The vendor accepts signed widths and keeps their width; the typed surface is
            // deliberately unsigned-only (the shared reader has no sbyte reader and signed/unsigned
            // mixing is not a portable contract), so these are raw pins, not API calls.
            Scalar<int>("bitNot(toInt32(0))").Should().Be(-1);
            Scalar<int>("bitAnd(toInt32(-1), toInt32(1))").Should().Be(1);
            Scalar<int>("bitShiftRight(toInt32(-1), 1)").Should().Be(-1);
            Scalar<int>("toInt32(bitAnd(toUInt8(1), toUInt16(1)))").Should().Be(1);
        }
    }

    [Fact]
    public void NullMatrix_ShouldPropagateNullableOperands()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bBitEntity>()
            .Where(x => x.Id == 4)
            .Select(x => new
            {
                And = SqlFunctions.ClickHouse.bit_and(x.U64, x.U64),
                Not = SqlFunctions.ClickHouse.bit_not(x.U8),
                Shift = SqlFunctions.ClickHouse.bit_shift_left(x.U8, 1),
                Rotate = SqlFunctions.ClickHouse.bit_rotate_right(x.U16, 1),
                Count = SqlFunctions.ClickHouse.bit_count(x.U8),
                Test = SqlFunctions.ClickHouse.bit_test(x.U8, 0),
                TestAll = SqlFunctions.ClickHouse.bit_test_all(x.U8, 0),
                TestAny = SqlFunctions.ClickHouse.bit_test_any(x.U8, 0),
                HammingNumber = SqlFunctions.ClickHouse.bit_hamming_distance(x.U64, x.U64),
                HammingText = SqlFunctions.ClickHouse.bit_hamming_distance(x.Text, x.Text)
            })
            .First();

        using (new AssertionScope())
        {
            r.And.Should().BeNull();
            r.Not.Should().BeNull();
            r.Shift.Should().BeNull();
            r.Rotate.Should().BeNull();
            r.Count.Should().BeNull();
            r.Test.Should().BeNull();
            r.TestAll.Should().BeNull();
            r.TestAny.Should().BeNull();
            r.HammingNumber.Should().BeNull();
            r.HammingText.Should().BeNull();
        }
    }

    [Fact]
    public void HammingStrings_ShouldCountDifferingBitsWithZeroPadding()
    {
        Seed();

        using (new AssertionScope())
        {
            Scalar<ulong>("bitHammingDistance('', '')").Should().Be(0UL);
            Scalar<ulong>("bitHammingDistance('abc', 'abd')").Should().Be(3UL);
            Scalar<ulong>("bitHammingDistance('a', '')").Should().Be(3UL);
            Scalar<byte>("bitHammingDistance(CAST(NULL AS Nullable(String)), 'abc') is NULL").Should().Be(1);
        }
    }
}
