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
/// T286b D14 (F14 Bit): the ClickHouse bit family. The tests pin every vendor spelling, the exact
/// width/signedness contract (UInt8 → <see cref="byte"/>, UInt16 → <see cref="int"/> with an exact
/// <c>toInt32</c> widening because the shared row reader has no <c>ushort</c> reader, UInt32 →
/// <see cref="uint"/>, UInt64 → <see cref="ulong"/>), the exact <c>toUInt64</c> widening of the
/// width-insensitive operations, the boolean <c>bitTest*</c> results, the flattened inline bit
/// positions, the UInt16 result widening, the null-operand rendering, and the fail-closed provider
/// guard. No connection is opened.
/// </summary>
public class T286bF14BitFunctionSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

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

    private static readonly string[] DeliveredNames =
    [
        "bit_and", "bit_or", "bit_xor", "bit_not",
        "bit_shift_left", "bit_shift_right", "bit_rotate_left", "bit_rotate_right",
        "bit_count", "bit_test", "bit_test_all", "bit_test_any", "bit_hamming_distance"
    ];

    [Fact]
    public void Render_ShouldEmitTheExactClickHouseArmForEveryDeliveredMember()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bBitEntity>();

        using (new AssertionScope())
        {
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_and(x.U64, x.U64) }))
                .Should().Contain("bitAnd(toUInt64(u64), toUInt64(u64)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_or(x.U64, x.U64) }))
                .Should().Contain("bitOr(toUInt64(u64), toUInt64(u64)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_xor(x.U64, x.U64) }))
                .Should().Contain("bitXor(toUInt64(u64), toUInt64(u64)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_not(x.U8) }))
                .Should().Contain("bitNot(u8) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_shift_left(x.U8, 1) }))
                .Should().Contain("bitShiftLeft(u8, 1) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_shift_right(x.U32, 3) }))
                .Should().Contain("bitShiftRight(u32, 3) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_rotate_left(x.U64, 4) }))
                .Should().Contain("bitRotateLeft(u64, 4) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_rotate_right(x.U16, 5) }))
                .Should().Contain("toInt32(bitRotateRight(u16, 5)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_count(x.U8) }))
                .Should().Contain("bitCount(toUInt64(u8)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_test(x.U8, 3) }))
                .Should().Contain("bitTest(u8, 3) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_test_all(x.U64, 0, 63) }))
                .Should().Contain("bitTestAll(u64, 0, 63) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_test_any(x.U16, 1, 15) }))
                .Should().Contain("bitTestAny(u16, 1, 15) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_hamming_distance(x.U32, x.U32) }))
                .Should().Contain("bitHammingDistance(toUInt64(u32), toUInt64(u32)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_hamming_distance(x.Text, x.Text) }))
                .Should().Contain("bitHammingDistance(text, text) as `V`");
        }
    }

    [Fact]
    public void Widening_ShouldKeepUInt16ExactAndWidenTheWidthInsensitiveOperations()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bBitEntity>();

        using (new AssertionScope())
        {
            // UInt16 has no reader in the shared materializer: the vendor result is widened exactly.
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_not(x.U16) }))
                .Should().Contain("toInt32(bitNot(u16))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_shift_left(x.U16, 2) }))
                .Should().Contain("toInt32(bitShiftLeft(u16, 2))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_shift_right(x.U16, 2) }))
                .Should().Contain("toInt32(bitShiftRight(u16, 2))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_rotate_left(x.U16, 2) }))
                .Should().Contain("toInt32(bitRotateLeft(u16, 2))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_rotate_right(x.U16, 2) }))
                .Should().Contain("toInt32(bitRotateRight(u16, 2))");

            // The width-preserving members do not widen UInt8/UInt32/UInt64 results.
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_not(x.U8) }))
                .Should().NotContain("toInt32(");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_not(x.U32) }))
                .Should().NotContain("toInt32(");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_not(x.U64) }))
                .Should().NotContain("toInt32(");
        }
    }

    [Fact]
    public void ByteAndUIntInputs_ShouldAlsoBeWidenedExactlyToUInt64()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bBitEntity>();

        using (new AssertionScope())
        {
            // A byte/uint column passed to the ulong? overload is widened with toUInt64; the dropped
            // CLR conversion must not leave the operand at its narrow SQL width.
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_and(x.U8, x.U8) }))
                .Should().Contain("bitAnd(toUInt64(u8), toUInt64(u8))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_or(x.U32, x.U32) }))
                .Should().Contain("bitOr(toUInt64(u32), toUInt64(u32))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_count(x.U16) }))
                .Should().Contain("bitCount(toUInt64(u16))");
        }
    }

    [Fact]
    public void DeclaredShapes_ShouldMatchTheNativeWidthsAndSignedness()
    {
        var byName = typeof(ClickHouseFunctions).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name is "bit_not" or "bit_hamming_distance" or "bit_count" or "bit_test")
            .ToArray();

        using (new AssertionScope())
        {
            byName.Where(m => m.Name == "bit_not")
                .Select(m => (
                    Input: Nullable.GetUnderlyingType(m.GetParameters()[0].ParameterType)!,
                    Result: Nullable.GetUnderlyingType(m.ReturnType)!))
                .Should().BeEquivalentTo(new[]
                {
                    (typeof(byte), typeof(byte)),
                    (typeof(ushort), typeof(int)),
                    (typeof(uint), typeof(uint)),
                    (typeof(ulong), typeof(ulong))
                });

            byName.Where(m => m.Name == "bit_count").Should().ContainSingle()
                .Which.ReturnType.Should().Be(typeof(byte?));

            byName.Where(m => m.Name == "bit_test")
                .Should().HaveCount(4)
                .And.OnlyContain(m => m.ReturnType == typeof(bool?));

            byName.Where(m => m.Name == "bit_hamming_distance")
                .Select(m => m.ReturnType)
                .Should().BeEquivalentTo(new[] { typeof(byte?), typeof(ulong?) });

            typeof(ClickHouseFunctions).GetMethod("bit_and")!.ReturnType.Should().Be(typeof(ulong?));
            typeof(ClickHouseFunctions).GetMethod("bit_or")!.ReturnType.Should().Be(typeof(ulong?));
            typeof(ClickHouseFunctions).GetMethod("bit_xor")!.ReturnType.Should().Be(typeof(ulong?));
        }
    }

    [Fact]
    public void BitTestFamily_ShouldFlattenInlinePositionsAndRefuseAnEmptyOrCapturedList()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bBitEntity>();
        byte[] captured = [0, 2];

        using (new AssertionScope())
        {
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_test_all(x.U8, 0, 2) }))
                .Should().Contain("bitTestAll(u8, 0, 2)");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_test_any(x.U32, 0, 31) }))
                .Should().Contain("bitTestAny(u32, 0, 31)");

            Action emptyAll = () => SqlOf(ctx, e.Select(x => new
            {
                V = SqlFunctions.ClickHouse.bit_test_all(x.U8, new byte[] { })
            }));
            emptyAll.Should().Throw<NotSupportedException>().WithMessage("*at least one bit position*");

            Action capturedAny = () => SqlOf(ctx, e.Select(x => new
            {
                V = SqlFunctions.ClickHouse.bit_test_any(x.U8, captured)
            }));
            capturedAny.Should().Throw<NotSupportedException>().WithMessage("*inline constants*");
        }
    }

    [Fact]
    public void NullOperands_ShouldRenderNullInsteadOfBeingDropped()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bBitEntity>();

        using (new AssertionScope())
        {
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_and(x.U64, null) }))
                .Should().Contain("bitAnd(toUInt64(u64), toUInt64(null))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_hamming_distance(x.Text, null) }))
                .Should().Contain("bitHammingDistance(text, null)");
        }
    }

    [Fact]
    public void CapturedShiftCountAndBitPosition_ShouldBindAsParameters()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bBitEntity>();
        byte shift = 3;
        byte position = 5;

        using (new AssertionScope())
        {
            var prepared = Prepare(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_shift_left(x.U8, shift) }));
            Normalize(prepared.DbCommand.CommandText).Should().Contain("bitShiftLeft(u8, @");
            prepared.DbCommandParams.Cast<System.Data.Common.DbParameter>()
                .Should().ContainSingle(p => Equals(p.Value, shift));

            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_test(x.U8, position) }))
                .Should().Contain("@");
        }
    }

    [Fact]
    public void Projection_ShouldRenderEveryDeliveredMemberInOneSelect()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bBitEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            And = SqlFunctions.ClickHouse.bit_and(x.U64, x.U64),
            Or = SqlFunctions.ClickHouse.bit_or(x.U64, x.U64),
            Xor = SqlFunctions.ClickHouse.bit_xor(x.U64, x.U64),
            Not = SqlFunctions.ClickHouse.bit_not(x.U8),
            ShiftLeft = SqlFunctions.ClickHouse.bit_shift_left(x.U16, 1),
            ShiftRight = SqlFunctions.ClickHouse.bit_shift_right(x.U32, 1),
            RotateLeft = SqlFunctions.ClickHouse.bit_rotate_left(x.U64, 1),
            RotateRight = SqlFunctions.ClickHouse.bit_rotate_right(x.U8, 1),
            Count = SqlFunctions.ClickHouse.bit_count(x.U16),
            Test = SqlFunctions.ClickHouse.bit_test(x.U32, 1),
            TestAll = SqlFunctions.ClickHouse.bit_test_all(x.U64, 0, 63),
            TestAny = SqlFunctions.ClickHouse.bit_test_any(x.U8, 7, 6),
            Hamming = SqlFunctions.ClickHouse.bit_hamming_distance(x.U32, x.U32),
            HammingText = SqlFunctions.ClickHouse.bit_hamming_distance(x.Text, x.Text)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("bitAnd(");
            sql.Should().Contain("bitOr(");
            sql.Should().Contain("bitXor(");
            sql.Should().Contain("bitNot(");
            sql.Should().Contain("bitShiftLeft(");
            sql.Should().Contain("bitShiftRight(");
            sql.Should().Contain("bitRotateLeft(");
            sql.Should().Contain("bitRotateRight(");
            sql.Should().Contain("bitCount(");
            sql.Should().Contain("bitTest(");
            sql.Should().Contain("bitTestAll(");
            sql.Should().Contain("bitTestAny(");
            sql.Should().Contain("bitHammingDistance(");
        }
    }

    [Fact]
    public void Predicate_ShouldUseBitCountAndBitTest()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bBitEntity>();

        using (new AssertionScope())
        {
            SqlOf(ctx, e.Where(x => SqlFunctions.ClickHouse.bit_count(x.U8) > 2)
                    .Select(x => new { x.Id }))
                .Should().Contain("where (cast(bitCount(toUInt64(u8)) as Int32) > 2)");
            SqlOf(ctx, e.Where(x => SqlFunctions.ClickHouse.bit_test(x.U8, 1) == true)
                    .Select(x => new { x.Id }))
                .Should().Contain("bitTest(u8, 1)");
        }
    }

    // #281 T347 (D2a/D2b): PostgreSQL 17 ships bit_count, so PostgresScalarFunctions advertises it — the
    // earlier "ClickHouse-only" expectation for the PostgreSQL half is now factually wrong. The ClickHouse
    // member renders the PostgreSQL arm instead of throwing; the ClickHouse argument widening
    // (toUInt64) is part of the composed SQL and is recorded here exactly as measured.
    [Fact]
    public void Postgres_ShouldSupportBitCountAndRenderThePostgresSpelling()
    {
        var postgres = PostgresDialect.Instance.ScalarFunctions!;

        using (new AssertionScope())
        {
            postgres.Supports("bit_count").Should().BeTrue("PostgreSQL 17 has bit_count (#281 T347)");

            foreach (var name in DeliveredNames.Except(["bit_count"]))
                postgres.Supports(name).Should().BeFalse("'{0}' is ClickHouse-only", name);
        }

        using var ctx = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        SqlOf(ctx, ctx.From<IT286bBitEntity>().Select(x => new { V = SqlFunctions.ClickHouse.bit_count(x.U8) }))
            .Should().Contain("bit_count(toUInt64(u8))",
                "the measured Postgres spelling keeps the ClickHouse toUInt64 widening");
    }

    [Fact]
    public void PostgresAndSqlServer_ShouldRejectEveryNameOutsideTheSharedSet()
    {
        using var postgres = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        using var sqlServer = new SqlServerDataContext(
            "Server=localhost;Database=nextorm;User Id=sa;Password=nextorm;TrustServerCertificate=True",
            new DataContextBuilder());

        using (new AssertionScope())
        {
            foreach (var name in DeliveredNames.Except(["bit_count"]))
                AssertRejected(postgres, name);
            foreach (var name in DeliveredNames)
                AssertRejected(sqlServer, name);
        }

        static void AssertRejected(IDataContext ctx, string name)
        {
            Func<string> query = name switch
            {
                "bit_and" => () => SqlOf(ctx, ctx.From<IT286bBitEntity>().Select(x => new { V = SqlFunctions.ClickHouse.bit_and(x.U64, x.U64) })),
                "bit_or" => () => SqlOf(ctx, ctx.From<IT286bBitEntity>().Select(x => new { V = SqlFunctions.ClickHouse.bit_or(x.U64, x.U64) })),
                "bit_xor" => () => SqlOf(ctx, ctx.From<IT286bBitEntity>().Select(x => new { V = SqlFunctions.ClickHouse.bit_xor(x.U64, x.U64) })),
                "bit_not" => () => SqlOf(ctx, ctx.From<IT286bBitEntity>().Select(x => new { V = SqlFunctions.ClickHouse.bit_not(x.U8) })),
                "bit_shift_left" => () => SqlOf(ctx, ctx.From<IT286bBitEntity>().Select(x => new { V = SqlFunctions.ClickHouse.bit_shift_left(x.U8, 1) })),
                "bit_shift_right" => () => SqlOf(ctx, ctx.From<IT286bBitEntity>().Select(x => new { V = SqlFunctions.ClickHouse.bit_shift_right(x.U8, 1) })),
                "bit_rotate_left" => () => SqlOf(ctx, ctx.From<IT286bBitEntity>().Select(x => new { V = SqlFunctions.ClickHouse.bit_rotate_left(x.U8, 1) })),
                "bit_rotate_right" => () => SqlOf(ctx, ctx.From<IT286bBitEntity>().Select(x => new { V = SqlFunctions.ClickHouse.bit_rotate_right(x.U8, 1) })),
                "bit_count" => () => SqlOf(ctx, ctx.From<IT286bBitEntity>().Select(x => new { V = SqlFunctions.ClickHouse.bit_count(x.U8) })),
                "bit_test" => () => SqlOf(ctx, ctx.From<IT286bBitEntity>().Select(x => new { V = SqlFunctions.ClickHouse.bit_test(x.U8, 1) })),
                "bit_test_all" => () => SqlOf(ctx, ctx.From<IT286bBitEntity>().Select(x => new { V = SqlFunctions.ClickHouse.bit_test_all(x.U8, 1) })),
                "bit_test_any" => () => SqlOf(ctx, ctx.From<IT286bBitEntity>().Select(x => new { V = SqlFunctions.ClickHouse.bit_test_any(x.U8, 1) })),
                "bit_hamming_distance" => () => SqlOf(ctx, ctx.From<IT286bBitEntity>().Select(x => new { V = SqlFunctions.ClickHouse.bit_hamming_distance(x.U64, x.U64) })),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
            };

            query.Should().Throw<NotSupportedException>()
                .WithMessage($"The {name} function is not supported by this provider.");
        }
    }

    [Fact]
    public void Supports_ShouldAcceptEveryDeliveredNameAndRefuseTheVendorSpellings()
    {
        var scalars = ClickHouseDialect.Instance.ScalarFunctions!;

        using (new AssertionScope())
        {
            foreach (var name in DeliveredNames)
                scalars.Supports(name).Should().BeTrue($"{name} is a delivered ClickHouse scalar name");

            foreach (var vendorSpelling in new[] { "bitAnd", "bitCount", "bitTestAll" })
                scalars.Supports(vendorSpelling).Should().BeFalse("the dialect table is keyed by the CLR member name");
        }
    }
}
