using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClickHouse.Driver;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T286b D11 (F11 Encoding): live execution of the ClickHouse-only encoding members against the
/// pinned <c>25.8-alpine</c> (25.8.33.6) server. The tests pin the binary/text split (every binary
/// result is the upper-case hex of the server bytes), the vendor padding and URL-safe alphabets,
/// round trips for every encode/decode pair, empty and Unicode input, the deterministic malformed-input
/// outcomes (throwing decoders and the Bech32 empty pair), the bitmask/Sqid value vectors, the
/// two-dimensional Hilbert/Morton inverse, the NULL flow of the nullable members, and the server
/// refusal of a <c>Nullable</c> argument for the array-returning members.
/// </summary>
public sealed class T286bF11EncodingFunctionTests : ProviderTestSuite
{
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
                     "drop table if exists t286b_f11_entity",
                     "create table t286b_f11_entity (id Int32, text Nullable(String), text_nn String, "
                     + "bits Nullable(String), u64 UInt64) engine = Memory",
                     "insert into t286b_f11_entity (id, text, text_nn, bits, u64) values "
                     + "(1, 'abc', 'abc', '00101010', 42), "
                     + "(2, NULL, '', NULL, 0), "
                     + "(3, '', '', '', 10), "
                     + "(4, 'Привет', 'Привет', '101010', 18446744073709551615)"
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    [Fact]
    public void BinAndUnbin_ShouldRoundTripAndKeepTheValueWidth()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        // The vendor width follows the value's significant bytes (at least one byte), not the CLR
        // parameter type: 42 is one byte, the 64-bit maximum is eight bytes.
        Scalar<string>("bin(toUInt64(42))").Should().Be("00101010");
        Scalar<string>("bin(toUInt64(0))").Should().Be("00000000");
        Scalar<string>("bin(toUInt64(18446744073709551615))").Should().Be(new string('1', 64));

        // unbin returns a BLOB; the member surfaces it as upper-case hex, so the pair round-trips.
        Scalar<string>("hex(unbin(bin(toUInt64(42))))").Should().Be("2A");
        Scalar<string>("hex(unbin(bin(toUInt64(18446744073709551615))))").Should().Be("FFFFFFFFFFFFFFFF");
        Scalar<string>("unbin('')").Should().BeEmpty("an empty bit string is an empty BLOB");

        var r = ctx.From<IT286bEncodingEntity>()
            .Where(x => x.Id == 3)
            .Select(x => new
            {
                Bin = SqlFunctions.ClickHouse.bin(x.U64),
                Unbin = SqlFunctions.ClickHouse.unbin(x.Bits)
            })
            .First();

        using (new AssertionScope())
        {
            r.Bin.Should().Be("00001010", "the column value 10 is one significant byte");
            r.Unbin.Should().BeEmpty("the empty bit string of row 3 is an empty BLOB");
        }
    }

    [Fact]
    public void BinAndUnbin_ShouldTreatOddLengthAndNonBinaryDigitsDeterministically()
    {
        using (new AssertionScope())
        {
            // The vendor parses the digits from the right and left-pads an odd-length input.
            Scalar<string>("hex(unbin('101'))").Should().Be("05", "binary 101 is 5");
            Scalar<string>("hex(unbin('1'))").Should().Be("01");
            // Characters other than '0'/'1' are not rejected by the vendor; the tested outcome is
            // recorded so the member never claims validation it does not perform.
            Scalar<string>("hex(unbin('012'))").Should().Be("03");
            Scalar<string>("hex(unbin('abc'))").Should().Be("07");
        }
    }

    [Fact]
    public void Base32_ShouldRoundTripWithPaddingAndRejectMalformedInput()
    {
        using (new AssertionScope())
        {
            Scalar<string>("base32Encode('a')").Should().Be("ME======");
            Scalar<string>("base32Encode('ab')").Should().Be("MFRA====");
            Scalar<string>("base32Encode('abc')").Should().Be("MFRGG===");
            Scalar<string>("base32Encode('abcd')").Should().Be("MFRGGZA=");
            Scalar<string>("base32Encode('abcde')").Should().Be("MFRGGZDF");
            Scalar<string>("base32Encode('')").Should().BeEmpty();
            Scalar<string>("base32Encode('Привет')").Should().Be("2CP5DAGQXDILFUFV2GBA====");

            Scalar<string>("hex(base32Decode('MFRGG==='))").Should().Be("616263");
            Scalar<string>("hex(base32Decode('mfrgg==='))").Should().Be("616263", "either case is accepted");
            Scalar<string>("hex(base32Decode(base32Encode('Привет')))").Should().Be("D09FD180D0B8D0B2D0B5D182");
            Scalar<string>("hex(base32Decode(base32Encode(unhex('00FF'))))").Should().Be("00FF",
                "the binary round-trip is lossless through the explicit hex form");
            Scalar<string>("base32Decode('')").Should().BeEmpty();
        }

        FluentActions.Invoking(() => Scalar<string>("base32Decode('!!!!')"))
            .Should().Throw<ClickHouseServerException>().WithMessage("*INCORRECT_DATA*");
        FluentActions.Invoking(() => Scalar<string>("base32Decode('MFRGG')"))
            .Should().Throw<ClickHouseServerException>().WithMessage("*INCORRECT_DATA*");
    }

    [Fact]
    public void Base58_ShouldRoundTripAndPreserveLeadingZeros()
    {
        using (new AssertionScope())
        {
            Scalar<string>("base58Encode('abc')").Should().Be("ZiCa");
            Scalar<string>("base58Encode('')").Should().BeEmpty();
            Scalar<string>("base58Encode('Привет')").Should().Be("4wLoLsz5SDpGAPpSV");

            Scalar<string>("hex(base58Decode('ZiCa'))").Should().Be("616263");
            Scalar<string>("hex(base58Decode('1'))").Should().Be("00", "a leading '1' is a zero byte");
            Scalar<string>("hex(base58Decode('11'))").Should().Be("0000");
            Scalar<string>("hex(base58Decode('1z'))").Should().Be("0039");
            Scalar<string>("hex(base58Decode(base58Encode('Привет')))").Should().Be("D09FD180D0B8D0B2D0B5D182");
            Scalar<string>("hex(base58Decode(base58Encode(unhex('00FF'))))").Should().Be("00FF");
            Scalar<string>("base58Decode('')").Should().BeEmpty();
        }

        FluentActions.Invoking(() => Scalar<string>("base58Decode('0OIl')"))
            .Should().Throw<ClickHouseServerException>().WithMessage("*INCORRECT_DATA*");
    }

    [Fact]
    public void Base64Url_ShouldRoundTripPaddedAndUnpaddedAndRejectMalformedInput()
    {
        using (new AssertionScope())
        {
            Scalar<string>("base64URLEncode('a')").Should().Be("YQ");
            Scalar<string>("base64URLEncode('ab')").Should().Be("YWI");
            Scalar<string>("base64URLEncode('abc')").Should().Be("YWJj");
            Scalar<string>("base64URLEncode('ab?>')").Should().Be("YWI_Pg");
            Scalar<string>("base64URLEncode('')").Should().BeEmpty();

            Scalar<string>("hex(base64URLDecode('YWI_Pg=='))").Should().Be("61623F3E");
            Scalar<string>("hex(base64URLDecode('YWI_Pg'))").Should().Be("61623F3E", "unpadded input is accepted");
            Scalar<string>("hex(base64URLDecode('YWI-Pg'))").Should().Be("61623E3E", "'-' is the URL-safe value 62");
            Scalar<string>("hex(base64URLDecode(base64URLEncode('Привет')))").Should().Be("D09FD180D0B8D0B2D0B5D182");
            Scalar<string>("hex(base64URLDecode(base64URLEncode(unhex('00FF'))))").Should().Be("00FF");
            Scalar<string>("base64URLDecode('')").Should().BeEmpty();
        }

        FluentActions.Invoking(() => Scalar<string>("base64URLDecode('!!!!')"))
            .Should().Throw<ClickHouseServerException>().WithMessage("*INCORRECT_DATA*");
    }

    [Fact]
    public void Bech32_ShouldRoundTripAndTreatInvalidInputAsTheEmptyPair()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var roundTrip = ctx.From<IT286bEncodingEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Text = SqlFunctions.ClickHouse.bech32_decode(
                    SqlFunctions.ClickHouse.bech32_encode("bc", "abc")!),
                Binary = SqlFunctions.ClickHouse.bech32_decode(
                    SqlFunctions.ClickHouse.bech32_encode("bc", SqlFunctions.ClickHouse.unhex("00FF"))!),
                Bech32 = SqlFunctions.ClickHouse.bech32_encode("bc", "abc", 0),
                Bech32m2 = SqlFunctions.ClickHouse.bech32_encode("bc", "abc", 2),
                EmptyData = SqlFunctions.ClickHouse.bech32_encode("bc", "")
            })
            .First();

        using (new AssertionScope())
        {
            roundTrip.Text.Should().Be(Tuple.Create("bc", "616263"));
            roundTrip.Binary.Should().Be(Tuple.Create("bc", "00FF"),
                "the binary data component is surfaced as hex");
            roundTrip.Bech32.Should().Be("bc1qv93xx0n9gss", "witver 0 selects Bech32");
            roundTrip.Bech32m2.Should().Be("bc1zv93xxxhazf0", "witver 2 selects Bech32m");
            roundTrip.EmptyData.Should().Be("bc1pdg93mv");

            // The vendor does not validate the checksum or the alphabet on decode: an address it cannot
            // decode is the empty pair, not an exception.
            var invalid = ctx.From<IT286bEncodingEntity>()
                .Where(x => x.Id == 1)
                .Select(x => new
                {
                    Text = SqlFunctions.ClickHouse.bech32_decode("abc"),
                    Checksum = SqlFunctions.ClickHouse.bech32_decode("bc1pv93xx5r38mh"),
                    Empty = SqlFunctions.ClickHouse.bech32_decode("")
                })
                .First();

            invalid.Text.Should().Be(Tuple.Create("", ""));
            invalid.Checksum.Should().Be(Tuple.Create("", ""), "a corrupted checksum decodes to the empty pair");
            invalid.Empty.Should().Be(Tuple.Create("", ""));
            Scalar<string>("bech32Encode('BC', 'abc')").Should().BeEmpty("the human-readable part must be lower-case");
        }
    }

    [Fact]
    public void HexAndBase64_ShouldKeepTheAlreadyCoveredBehaviour()
    {
        using (new AssertionScope())
        {
            Scalar<string>("hex('abc')").Should().Be("616263");
            Scalar<string>("hex('Привет')").Should().Be("D09FD180D0B8D0B2D0B5D182");
            Scalar<string>("hex('')").Should().BeEmpty();
            Scalar<string>("unhex('414243')").Should().Be("ABC");
            Scalar<string>("unhex('')").Should().BeEmpty();
            Scalar<string>("base64Encode('abc')").Should().Be("YWJj");
            Scalar<string>("base64Decode('YWJj')").Should().Be("abc");
        }
    }

    [Fact]
    public void BitmaskFunctions_ShouldReturnTheVendorVectors()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bEncodingEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Ten = SqlFunctions.ClickHouse.bitmask_to_array(10UL),
                Zero = SqlFunctions.ClickHouse.bitmask_to_array(0UL),
                Big = SqlFunctions.ClickHouse.bitmask_to_array(1099511627776UL),
                TenList = SqlFunctions.ClickHouse.bitmask_to_list(10UL),
                ZeroList = SqlFunctions.ClickHouse.bitmask_to_list(0UL),
                TenPositions = SqlFunctions.ClickHouse.bit_positions_to_array(10UL),
                ZeroPositions = SqlFunctions.ClickHouse.bit_positions_to_array(0UL),
                BigPositions = SqlFunctions.ClickHouse.bit_positions_to_array(1099511627776UL)
            })
            .First();

        using (new AssertionScope())
        {
            r.Ten.Should().Equal(2UL, 8UL);
            r.Zero.Should().BeEmpty();
            r.Big.Should().Equal(1099511627776UL);
            r.TenList.Should().Be("2,8");
            r.ZeroList.Should().BeEmpty();
            r.TenPositions.Should().Equal(1UL, 3UL);
            r.ZeroPositions.Should().BeEmpty();
            r.BigPositions.Should().Equal(40UL);
        }
    }

    [Fact]
    public void BitmaskAndSqidFunctions_ShouldRefuseNullableInput()
    {
        using (new AssertionScope())
        {
            FluentActions.Invoking(() => Scalar<ulong>(
                    "length(bitmaskToArray(toUInt64(CAST(NULL AS Nullable(UInt64)))))"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*ILLEGAL_TYPE_OF_ARGUMENT*");
            FluentActions.Invoking(() => Scalar<ulong>(
                    "length(bitPositionsToArray(CAST(NULL AS Nullable(UInt64))))"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*ILLEGAL_TYPE_OF_ARGUMENT*");
            FluentActions.Invoking(() => Scalar<ulong>("length(sqidDecode(CAST(NULL AS Nullable(String))))"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*ILLEGAL_TYPE_OF_ARGUMENT*");
            FluentActions.Invoking(() => Scalar<string>("bech32Decode(CAST(NULL AS Nullable(String))).1"))
                .Should().Throw<ClickHouseServerException>().WithMessage("*ILLEGAL_TYPE_OF_ARGUMENT*");
        }
    }

    [Fact]
    public void Sqid_ShouldEncodeAndDecodeArbitrarilyManyValues()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bEncodingEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                One = SqlFunctions.ClickHouse.sqid_encode(1UL),
                Three = SqlFunctions.ClickHouse.sqid_encode(1UL, 2UL, 3UL),
                Max = SqlFunctions.ClickHouse.sqid_encode(18446744073709551615UL),
                DecodedThree = SqlFunctions.ClickHouse.sqid_decode("86Rf07"),
                DecodedEmpty = SqlFunctions.ClickHouse.sqid_decode(""),
                DecodedArbitrary = SqlFunctions.ClickHouse.sqid_decode("abcdef"),
                RoundTrip = SqlFunctions.ClickHouse.sqid_decode(
                    SqlFunctions.ClickHouse.sqid_encode(18446744073709551615UL))
            })
            .First();

        using (new AssertionScope())
        {
            r.One.Should().Be("Uk");
            r.Three.Should().Be("86Rf07");
            r.Max.Should().Be("eIkvoXH40Lmd");
            r.DecodedThree.Should().Equal(1UL, 2UL, 3UL);
            r.DecodedEmpty.Should().BeEmpty();
            r.DecodedArbitrary.Should().Equal(new ulong[] { 626678339UL },
                "an arbitrary string decodes deterministically instead of raising");
            r.RoundTrip.Should().Equal(18446744073709551615UL);
        }
    }

    [Fact]
    public void HilbertEncodeAndDecode_ShouldRoundTripTheTwoDimensionalCode()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bEncodingEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Encode = SqlFunctions.ClickHouse.hilbert_encode(3UL, 4UL),
                Zero = SqlFunctions.ClickHouse.hilbert_encode(0UL, 0UL),
                Max = SqlFunctions.ClickHouse.hilbert_encode(4294967295UL, 4294967295UL),
                OneDimensional = SqlFunctions.ClickHouse.hilbert_encode(3UL),
                Decoded = SqlFunctions.ClickHouse.hilbert_decode(
                    SqlFunctions.ClickHouse.hilbert_encode(3UL, 4UL))
            })
            .First();

        using (new AssertionScope())
        {
            r.Encode.Should().Be(31UL);
            r.Zero.Should().Be(0UL);
            r.Max.Should().Be(12297829382473034410UL);
            r.OneDimensional.Should().Be(3UL);
            r.Decoded.Should().Be(Tuple.Create(3UL, 4UL));
        }
    }

    [Fact]
    public void MortonEncodeAndDecode_ShouldRoundTripTheTwoDimensionalCode()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bEncodingEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Encode = SqlFunctions.ClickHouse.morton_encode(3UL, 4UL),
                One = SqlFunctions.ClickHouse.morton_encode(1UL, 1UL),
                Max = SqlFunctions.ClickHouse.morton_encode(4294967295UL, 4294967295UL),
                Decoded = SqlFunctions.ClickHouse.morton_decode(
                    SqlFunctions.ClickHouse.morton_encode(3UL, 4UL))
            })
            .First();

        using (new AssertionScope())
        {
            r.Encode.Should().Be(37UL);
            r.One.Should().Be(3UL);
            r.Max.Should().Be(18446744073709551615UL);
            r.Decoded.Should().Be(Tuple.Create(3UL, 4UL));
        }
    }

    [Fact]
    public void EncodingMembers_ShouldReturnNullForNullInput()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bEncodingEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                Bin = SqlFunctions.ClickHouse.bin(x.U64),
                Unbin = SqlFunctions.ClickHouse.unbin(x.Bits),
                B32E = SqlFunctions.ClickHouse.base32_encode(x.Text),
                B32D = SqlFunctions.ClickHouse.base32_decode(x.Text),
                B58E = SqlFunctions.ClickHouse.base58_encode(x.Text),
                B58D = SqlFunctions.ClickHouse.base58_decode(x.Text),
                B64UE = SqlFunctions.ClickHouse.base64_url_encode(x.Text),
                B64UD = SqlFunctions.ClickHouse.base64_url_decode(x.Text),
                BitList = SqlFunctions.ClickHouse.bitmask_to_list(x.U64),
                BechE = SqlFunctions.ClickHouse.bech32_encode(x.Text, x.Bits)
            })
            .First();

        using (new AssertionScope())
        {
            // bin of the non-null 0 is the eight-zero string, not NULL.
            r.Bin.Should().Be("00000000");
            r.Unbin.Should().BeNull();
            r.B32E.Should().BeNull();
            r.B32D.Should().BeNull();
            r.B58E.Should().BeNull();
            r.B58D.Should().BeNull();
            r.B64UE.Should().BeNull();
            r.B64UD.Should().BeNull();
            r.BitList.Should().BeEmpty("bitmaskToList(0) is the empty string, not NULL");
            r.BechE.Should().BeNull("both Bech32 inputs are NULL on row 2");
        }
    }

    [Fact]
    public void Projection_ShouldMaterialiseEveryDeliveredMember()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bEncodingEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Bin = SqlFunctions.ClickHouse.bin(x.U64),
                Unbin = SqlFunctions.ClickHouse.unbin(x.Bits),
                B32E = SqlFunctions.ClickHouse.base32_encode(x.Text),
                B32D = SqlFunctions.ClickHouse.base32_decode(SqlFunctions.ClickHouse.base32_encode(x.TextNonNull)!),
                B58E = SqlFunctions.ClickHouse.base58_encode(x.TextNonNull),
                B58D = SqlFunctions.ClickHouse.base58_decode(SqlFunctions.ClickHouse.base58_encode(x.TextNonNull)!),
                B64UE = SqlFunctions.ClickHouse.base64_url_encode(x.TextNonNull),
                B64UD = SqlFunctions.ClickHouse.base64_url_decode(
                    SqlFunctions.ClickHouse.base64_url_encode(x.TextNonNull)!),
                BechE = SqlFunctions.ClickHouse.bech32_encode("bc", x.TextNonNull),
                BechD = SqlFunctions.ClickHouse.bech32_decode(
                    SqlFunctions.ClickHouse.bech32_encode("bc", x.TextNonNull)!),
                BitArr = SqlFunctions.ClickHouse.bitmask_to_array(x.U64),
                BitList = SqlFunctions.ClickHouse.bitmask_to_list(x.U64),
                BitPos = SqlFunctions.ClickHouse.bit_positions_to_array(x.U64),
                SqidE = SqlFunctions.ClickHouse.sqid_encode(x.U64, x.U64),
                SqidD = SqlFunctions.ClickHouse.sqid_decode(
                    SqlFunctions.ClickHouse.sqid_encode(x.U64, x.U64)),
                HEnc = SqlFunctions.ClickHouse.hilbert_encode(x.U64, x.U64),
                HDec = SqlFunctions.ClickHouse.hilbert_decode(
                    SqlFunctions.ClickHouse.hilbert_encode(x.U64, x.U64)),
                MEnc = SqlFunctions.ClickHouse.morton_encode(x.U64, x.U64),
                MDec = SqlFunctions.ClickHouse.morton_decode(
                    SqlFunctions.ClickHouse.morton_encode(x.U64, x.U64))
            })
            .First();

        using (new AssertionScope())
        {
            r.Bin.Should().Be("00101010");
            r.Unbin.Should().Be("2A");
            r.B32E.Should().Be("MFRGG===");
            r.B32D.Should().Be("616263");
            r.B58E.Should().Be("ZiCa");
            r.B58D.Should().Be("616263");
            r.B64UE.Should().Be("YWJj");
            r.B64UD.Should().Be("616263");
            r.BechE.Should().Be("bc1pv93xx5r38mg");
            r.BechD.Should().Be(Tuple.Create("bc", "616263"));
            r.BitArr.Should().Equal(2UL, 8UL, 32UL);
            r.BitList.Should().Be("2,8,32");
            r.BitPos.Should().Equal(1UL, 3UL, 5UL);
            r.SqidE.Should().NotBeNullOrEmpty();
            r.SqidD.Should().Equal(42UL, 42UL);
            r.HEnc.Should().BeGreaterThan(0UL);
            r.HDec.Should().Be(Tuple.Create(42UL, 42UL));
            r.MEnc.Should().Be(3276UL);
            r.MDec.Should().Be(Tuple.Create(42UL, 42UL));
        }
    }
}
