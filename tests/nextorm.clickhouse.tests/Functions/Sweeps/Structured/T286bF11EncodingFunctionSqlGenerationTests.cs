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
/// T286b D11 (F11 Encoding): the ClickHouse-only encoding members. The tests pin every rendered arm,
/// the explicit binary/text shape contract (a binary server result is wrapped in <c>hex(...)</c>, never
/// surfaced as a UTF-8-decoded string), the padding and URL-safe alphabets, the Bech32 witness version,
/// the fixed two-component Hilbert/Morton inverse, the <c>params</c> flattening of <c>sqidEncode</c>,
/// the declared CLR result shapes, the already-covered <c>hex</c>/<c>unhex</c>/Base64 surface, and the
/// fail-closed provider guard. They never open a connection.
/// </summary>
public class T286bF11EncodingFunctionSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [SqlTable("t286b_f11_entity")]
    internal interface IT286bEncodingEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("text")]
        string? Text { get; set; }

        [Column("text_nn")]
        string TextNonNull { get; set; }

        [Column("bits")]
        string? Bits { get; set; }

        [Column("u64")]
        ulong U64 { get; set; }
    }

    /// <summary>The nineteen delivered catalog names, one per newly shipped member.</summary>
    private static readonly string[] DeliveredNames =
    [
        "bin", "unbin", "base32_encode", "base32_decode", "base58_encode", "base58_decode",
        "base64_url_encode", "base64_url_decode", "bech32_encode", "bech32_decode",
        "bitmask_to_array", "bitmask_to_list", "bit_positions_to_array",
        "sqid_encode", "sqid_decode", "hilbert_encode", "hilbert_decode", "morton_encode", "morton_decode"
    ];

    [Fact]
    public void Render_ShouldEmitTheExactClickHouseArmForEveryDeliveredMember()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bEncodingEntity>();

        using (new AssertionScope())
        {
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bin(x.U64) }))
                .Should().Contain("bin(u64) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.unbin(x.Bits) }))
                .Should().Contain("hex(unbin(bits)) as `V`");

            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.base32_encode(x.Text) }))
                .Should().Contain("base32Encode(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.base32_decode(x.Text) }))
                .Should().Contain("hex(base32Decode(text)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.base58_encode(x.Text) }))
                .Should().Contain("base58Encode(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.base58_decode(x.Text) }))
                .Should().Contain("hex(base58Decode(text)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.base64_url_encode(x.Text) }))
                .Should().Contain("base64URLEncode(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.base64_url_decode(x.Text) }))
                .Should().Contain("hex(base64URLDecode(text)) as `V`");

            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bech32_encode(x.Text, x.Bits) }))
                .Should().Contain("bech32Encode(text, bits) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bech32_decode(x.TextNonNull) }))
                .Should().Contain(
                    "tuple(tupleElement(bech32Decode(text_nn), 1), hex(tupleElement(bech32Decode(text_nn), 2))) as `V`");

            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bitmask_to_array(x.U64) }))
                .Should().Contain("bitmaskToArray(toUInt64(u64)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bitmask_to_list(x.U64) }))
                .Should().Contain("bitmaskToList(u64) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bit_positions_to_array(x.U64) }))
                .Should().Contain("bitPositionsToArray(u64) as `V`");

            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.sqid_encode(x.U64, x.U64) }))
                .Should().Contain("sqidEncode(u64, u64) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.sqid_decode(x.TextNonNull) }))
                .Should().Contain("sqidDecode(text_nn) as `V`");

            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.hilbert_encode(x.U64) }))
                .Should().Contain("hilbertEncode(u64) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.hilbert_encode(x.U64, x.U64) }))
                .Should().Contain("hilbertEncode(u64, u64) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.hilbert_decode(x.U64) }))
                .Should().Contain("hilbertDecode(2, u64) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.morton_encode(x.U64, x.U64) }))
                .Should().Contain("mortonEncode(u64, u64) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.morton_decode(x.U64) }))
                .Should().Contain("mortonDecode(2, u64) as `V`");
        }
    }

    [Fact]
    public void BinaryResults_ShouldBeWrappedInHexAndNeverSurfaceRawBytes()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bEncodingEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            U = SqlFunctions.ClickHouse.unbin(x.Bits),
            B32 = SqlFunctions.ClickHouse.base32_decode(x.Text),
            B58 = SqlFunctions.ClickHouse.base58_decode(x.Text),
            B64 = SqlFunctions.ClickHouse.base64_url_decode(x.Text),
            Bech = SqlFunctions.ClickHouse.bech32_decode(x.TextNonNull)
        }));

        using (new AssertionScope())
        {
            // A CLR string result of a binary server value would decode arbitrary bytes as UTF-8 and
            // lose the data, so every binary result is the explicit upper-case hex of the bytes.
            sql.Should().Contain("hex(unbin(bits)) as `U`");
            sql.Should().Contain("hex(base32Decode(text)) as `B32`");
            sql.Should().Contain("hex(base58Decode(text)) as `B58`");
            sql.Should().Contain("hex(base64URLDecode(text)) as `B64`");
            sql.Should().Contain("hex(tupleElement(bech32Decode(text_nn), 2))", "the binary data component is surfaced as hex");

            sql.Should().NotContain("base32Decode(text) as", "the decoded bytes must never be surfaced raw");
            sql.Should().NotContain("base58Decode(text) as", "the decoded bytes must never be surfaced raw");
            sql.Should().NotContain("base64URLDecode(text) as", "the decoded bytes must never be surfaced raw");
            sql.Should().NotContain("unbin(bits) as", "the decoded bytes must never be surfaced raw");
        }
    }

    [Fact]
    public void Encoders_ShouldKeepTheVendorTextualAlphabetsAndPadding()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bEncodingEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Bin = SqlFunctions.ClickHouse.bin(x.U64),
            B32 = SqlFunctions.ClickHouse.base32_encode(x.Text),
            B58 = SqlFunctions.ClickHouse.base58_encode(x.Text),
            B64Url = SqlFunctions.ClickHouse.base64_url_encode(x.Text)
        }));

        using (new AssertionScope())
        {
            // The encoders keep the vendor spelling: no toBase32/toBase58 lowering and no padding or
            // URL-safe conversion invented on the library side (the server owns both).
            sql.Should().Contain("bin(u64) as `Bin`");
            sql.Should().Contain("base32Encode(text) as `B32`");
            sql.Should().Contain("base58Encode(text) as `B58`");
            sql.Should().Contain("base64URLEncode(text) as `B64Url`");
            sql.Should().NotContain("replace(", "the URL-safe alphabet is the vendor's, never a library rewrite");
        }
    }

    [Fact]
    public void DeclaredShapes_ShouldMatchTheNativeResultTypes()
    {
        using (new AssertionScope())
        {
            ReturnType("bin", [typeof(ulong?)]).Should().Be(typeof(string));
            ReturnType("unbin", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("base32_encode", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("base32_decode", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("base58_encode", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("base58_decode", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("base64_url_encode", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("base64_url_decode", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("bech32_encode", [typeof(string), typeof(string), typeof(int?)]).Should().Be(typeof(string));
            ReturnType("bech32_decode", [typeof(string)]).Should().Be(typeof(Tuple<string, string>));

            ReturnType("bitmask_to_array", [typeof(ulong)]).Should().Be(typeof(ulong[]),
                "bitmaskToArray returns Array(UInt64) for a UInt64 argument");
            ReturnType("bitmask_to_list", [typeof(ulong?)]).Should().Be(typeof(string));
            ReturnType("bit_positions_to_array", [typeof(ulong)]).Should().Be(typeof(ulong[]),
                "bitPositionsToArray returns Array(UInt64)");

            ReturnType("sqid_encode", [typeof(ulong[])]).Should().Be(typeof(string));
            ReturnType("sqid_decode", [typeof(string)]).Should().Be(typeof(ulong[]),
                "sqidDecode returns Array(UInt64)");
            ReturnType("hilbert_encode", [typeof(ulong)]).Should().Be(typeof(ulong));
            ReturnType("hilbert_encode", [typeof(ulong), typeof(ulong)]).Should().Be(typeof(ulong));
            ReturnType("hilbert_decode", [typeof(ulong)]).Should().Be(typeof(Tuple<ulong, ulong>));
            ReturnType("morton_encode", [typeof(ulong), typeof(ulong)]).Should().Be(typeof(ulong));
            ReturnType("morton_decode", [typeof(ulong)]).Should().Be(typeof(Tuple<ulong, ulong>));
        }

        static Type ReturnType(string name, Type[] arguments)
            => typeof(ClickHouseFunctions)
                .GetMethod(name, BindingFlags.Public | BindingFlags.Instance, arguments)!
                .ReturnType;
    }

    [Fact]
    public void Bech32_ShouldRenderTheHrpDataPairAndTheWitnessVersion()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bEncodingEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Default = SqlFunctions.ClickHouse.bech32_encode(x.Text, x.Bits),
            Bech32 = SqlFunctions.ClickHouse.bech32_encode(x.Text, x.Bits, 0),
            Bech32m = SqlFunctions.ClickHouse.bech32_encode(x.Text, x.Bits, 2),
            Decoded = SqlFunctions.ClickHouse.bech32_decode(x.TextNonNull)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("bech32Encode(text, bits) as `Default`", "the omitted witver keeps the two-argument form");
            sql.Should().Contain("bech32Encode(text, bits, 0) as `Bech32`", "witver 0 is inlined, not bound");
            sql.Should().Contain("bech32Encode(text, bits, 2) as `Bech32m`");
            sql.Should().Contain("hex(tupleElement(bech32Decode(text_nn), 2))",
                "the binary data component is surfaced as hex");
        }

        // The witness version is a 5-bit compile-time constant, so an invalid value fails before the
        // provider gate and on every provider.
        using var postgres = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        using (new AssertionScope())
        {
            foreach (var provider in new IDataContext[] { ctx, postgres })
            {
                FluentActions.Invoking(() => SqlOf(provider, provider.From<IT286bEncodingEntity>()
                        .Select(x => new { V = SqlFunctions.ClickHouse.bech32_encode(x.Text, x.Bits, 32) })))
                    .Should().Throw<NotSupportedException>()
                    .WithMessage("*witness version*0..31*");
            }
        }
    }

    [Fact]
    public void Sqid_ShouldFlattenInlineValuesAndRefuseAnEmptyOrCapturedArray()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bEncodingEntity>();

        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.sqid_encode(x.U64) }))
            .Should().Contain("sqidEncode(u64) as `V`", "a single params value renders one argument");
        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.sqid_encode(1, 2, 3) }))
            .Should().Contain("sqidEncode(1, 2, 3) as `V`", "the params array is flattened into positional arguments");

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.sqid_encode() })))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*at least one value*NUMBER_OF_ARGUMENTS_DOESNT_MATCH*");

        var captured = new ulong[] { 1, 2, 3 };
        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.sqid_encode(captured) })))
            .Should().Throw<NotSupportedException>("a captured array cannot be expanded into positional SQL arguments");
    }

    [Fact]
    public void HilbertAndMorton_ShouldRenderTheFixedTwoDimensionalInverse()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bEncodingEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            H1 = SqlFunctions.ClickHouse.hilbert_encode(x.U64),
            H2 = SqlFunctions.ClickHouse.hilbert_encode(x.U64, x.U64),
            HD = SqlFunctions.ClickHouse.hilbert_decode(x.U64),
            M = SqlFunctions.ClickHouse.morton_encode(x.U64, x.U64),
            MD = SqlFunctions.ClickHouse.morton_decode(x.U64)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("hilbertEncode(u64) as `H1`");
            sql.Should().Contain("hilbertEncode(u64, u64) as `H2`");
            // The vendor result arity follows a runtime size argument, so the declared two-component
            // tuple is rendered with the explicit size 2; no size parameter is surfaced to CLR.
            sql.Should().Contain("hilbertDecode(2, u64) as `HD`");
            sql.Should().Contain("mortonEncode(u64, u64) as `M`");
            sql.Should().Contain("mortonDecode(2, u64) as `MD`");
        }
    }

    [Fact]
    public void CapturedValues_ShouldBindAsParameters()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bEncodingEntity>();
        var value = "abc";

        var command = Prepare(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.base32_decode(value) }));

        Normalize(command.DbCommand.CommandText).Should().Contain("hex(base32Decode(@value)) as `V`");
        command.DbCommandParams.Cast<System.Data.Common.DbParameter>().Should().ContainSingle()
            .Which.Value.Should().Be(value);

        var hrp = "bc";
        var decoded = SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.bech32_encode(hrp, value, 0) }));
        decoded.Should().Contain("bech32Encode(@hrp, @value, 0) as `V`");
    }

    [Fact]
    public void Projection_ShouldRenderEveryDeliveredMemberInOneSelect()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bEncodingEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Bin = SqlFunctions.ClickHouse.bin(x.U64),
            Unbin = SqlFunctions.ClickHouse.unbin(x.Bits),
            B32E = SqlFunctions.ClickHouse.base32_encode(x.Text),
            B32D = SqlFunctions.ClickHouse.base32_decode(x.Text),
            B58E = SqlFunctions.ClickHouse.base58_encode(x.Text),
            B58D = SqlFunctions.ClickHouse.base58_decode(x.Text),
            B64UE = SqlFunctions.ClickHouse.base64_url_encode(x.Text),
            B64UD = SqlFunctions.ClickHouse.base64_url_decode(x.Text),
            BechE = SqlFunctions.ClickHouse.bech32_encode(x.Text, x.Bits),
            BechD = SqlFunctions.ClickHouse.bech32_decode(x.TextNonNull),
            BitArr = SqlFunctions.ClickHouse.bitmask_to_array(x.U64),
            BitList = SqlFunctions.ClickHouse.bitmask_to_list(x.U64),
            BitPos = SqlFunctions.ClickHouse.bit_positions_to_array(x.U64),
            SqidE = SqlFunctions.ClickHouse.sqid_encode(x.U64, x.U64),
            SqidD = SqlFunctions.ClickHouse.sqid_decode(x.TextNonNull),
            HEnc = SqlFunctions.ClickHouse.hilbert_encode(x.U64, x.U64),
            HDec = SqlFunctions.ClickHouse.hilbert_decode(x.U64),
            MEnc = SqlFunctions.ClickHouse.morton_encode(x.U64, x.U64),
            MDec = SqlFunctions.ClickHouse.morton_decode(x.U64)
        }));

        using (new AssertionScope())
        {
            foreach (var alias in new[]
                     {
                         "Bin", "Unbin", "B32E", "B32D", "B58E", "B58D", "B64UE", "B64UD", "BechE",
                         "BechD", "BitArr", "BitList", "BitPos", "SqidE", "SqidD", "HEnc", "HDec",
                         "MEnc", "MDec"
                     })
                sql.Should().Contain($"as `{alias}`");
        }
    }

    [Fact]
    public void AlreadyCoveredEncodingSurface_ShouldKeepTheExistingArms()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bEncodingEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            H = SqlFunctions.ClickHouse.hex(x.Text),
            Uh = SqlFunctions.ClickHouse.unhex(x.Text),
            B64E = SqlFunctions.ClickHouse.base64_encode(x.Text),
            B64D = SqlFunctions.ClickHouse.base64_decode(x.Text)
        }));

        using (new AssertionScope())
        {
            // The four shipped encoding members are verified drift: their arms are unchanged and are
            // not claimed as new delivery. hex/unhex are textual; base64Decode is documented as a
            // UTF-8 decode (use base64_url_decode/hex for lossless binary).
            sql.Should().Contain("hex(text) as `H`");
            sql.Should().Contain("unhex(text) as `Uh`");
            sql.Should().Contain("base64Encode(text) as `B64E`");
            sql.Should().Contain("base64Decode(text) as `B64D`");
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
                "bin" => () => SqlOf(ctx, ctx.From<IT286bEncodingEntity>().Select(x => new { V = SqlFunctions.ClickHouse.bin(x.U64) })),
                "unbin" => () => SqlOf(ctx, ctx.From<IT286bEncodingEntity>().Select(x => new { V = SqlFunctions.ClickHouse.unbin(x.Bits) })),
                "base32_encode" => () => SqlOf(ctx, ctx.From<IT286bEncodingEntity>().Select(x => new { V = SqlFunctions.ClickHouse.base32_encode(x.Text) })),
                "base32_decode" => () => SqlOf(ctx, ctx.From<IT286bEncodingEntity>().Select(x => new { V = SqlFunctions.ClickHouse.base32_decode(x.Text) })),
                "base58_encode" => () => SqlOf(ctx, ctx.From<IT286bEncodingEntity>().Select(x => new { V = SqlFunctions.ClickHouse.base58_encode(x.Text) })),
                "base58_decode" => () => SqlOf(ctx, ctx.From<IT286bEncodingEntity>().Select(x => new { V = SqlFunctions.ClickHouse.base58_decode(x.Text) })),
                "base64_url_encode" => () => SqlOf(ctx, ctx.From<IT286bEncodingEntity>().Select(x => new { V = SqlFunctions.ClickHouse.base64_url_encode(x.Text) })),
                "base64_url_decode" => () => SqlOf(ctx, ctx.From<IT286bEncodingEntity>().Select(x => new { V = SqlFunctions.ClickHouse.base64_url_decode(x.Text) })),
                "bech32_encode" => () => SqlOf(ctx, ctx.From<IT286bEncodingEntity>().Select(x => new { V = SqlFunctions.ClickHouse.bech32_encode(x.Text, x.Bits) })),
                "bech32_decode" => () => SqlOf(ctx, ctx.From<IT286bEncodingEntity>().Select(x => new { V = SqlFunctions.ClickHouse.bech32_decode(x.TextNonNull) })),
                "bitmask_to_array" => () => SqlOf(ctx, ctx.From<IT286bEncodingEntity>().Select(x => new { V = SqlFunctions.ClickHouse.bitmask_to_array(x.U64) })),
                "bitmask_to_list" => () => SqlOf(ctx, ctx.From<IT286bEncodingEntity>().Select(x => new { V = SqlFunctions.ClickHouse.bitmask_to_list(x.U64) })),
                "bit_positions_to_array" => () => SqlOf(ctx, ctx.From<IT286bEncodingEntity>().Select(x => new { V = SqlFunctions.ClickHouse.bit_positions_to_array(x.U64) })),
                "sqid_encode" => () => SqlOf(ctx, ctx.From<IT286bEncodingEntity>().Select(x => new { V = SqlFunctions.ClickHouse.sqid_encode(x.U64) })),
                "sqid_decode" => () => SqlOf(ctx, ctx.From<IT286bEncodingEntity>().Select(x => new { V = SqlFunctions.ClickHouse.sqid_decode(x.TextNonNull) })),
                "hilbert_encode" => () => SqlOf(ctx, ctx.From<IT286bEncodingEntity>().Select(x => new { V = SqlFunctions.ClickHouse.hilbert_encode(x.U64) })),
                "hilbert_decode" => () => SqlOf(ctx, ctx.From<IT286bEncodingEntity>().Select(x => new { V = SqlFunctions.ClickHouse.hilbert_decode(x.U64) })),
                "morton_encode" => () => SqlOf(ctx, ctx.From<IT286bEncodingEntity>().Select(x => new { V = SqlFunctions.ClickHouse.morton_encode(x.U64, x.U64) })),
                "morton_decode" => () => SqlOf(ctx, ctx.From<IT286bEncodingEntity>().Select(x => new { V = SqlFunctions.ClickHouse.morton_decode(x.U64) })),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
            };

            FluentActions.Invoking(query).Should().Throw<NotSupportedException>(
                "'{0}' is ClickHouse-only and must be refused by every other provider", name)
                .WithMessage("*not supported by this provider*");
        }
    }
}
