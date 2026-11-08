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
/// T286b D10 (F10 Hash): the ClickHouse-only hash members. The tests pin every rendered arm
/// (the seven digest functions on <c>lower(hex(...))</c>, the unsigned 64-bit hashes without the
/// legacy <c>toInt64</c> lowering, the signed <c>javaHash</c>/<c>hiveHash</c>, the <c>UInt32</c>
/// <c>intHash32</c>, the <c>URLHash</c> level form, and the n-gram/word-shingle family with its
/// <c>UInt8</c> size parameters and its fixed <c>hashnum</c>), the declared CLR result shapes and
/// their nullability, the existing hash surface, and the two deterministic refusals
/// (<c>ngramSimHashArg</c>, <c>wordShingleSimHashArg</c>). They never open a connection.
/// </summary>
public class T286bF10HashFunctionSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [SqlTable("t286b_f10_entity")]
    internal interface IT286bHashEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("text")]
        string? Text { get; set; }

        [Column("url")]
        string? Url { get; set; }

        [Column("number")]
        long Number { get; set; }
    }

    /// <summary>The delivered catalog names, one per non-refused member.</summary>
    private static readonly string[] DeliveredNames =
    [
        "sha224", "sha384", "sha512_256", "blake3", "md4", "ripemd160", "keccak256",
        "farm_hash64", "metro_hash64", "java_hash", "hive_hash", "half_md5", "url_hash",
        "wy_hash64", "int_hash32", "int_hash64", "ngram_min_hash", "ngram_min_hash_arg",
        "ngram_sim_hash", "word_shingle_min_hash", "word_shingle_min_hash_arg",
        "word_shingle_sim_hash"
    ];

    [Fact]
    public void Render_ShouldEmitTheExactClickHouseArmForEveryDeliveredMember()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bHashEntity>();

        using (new AssertionScope())
        {
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.sha224(x.Text) }))
                .Should().Contain("lower(hex(SHA224(text))) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.sha384(x.Text) }))
                .Should().Contain("lower(hex(SHA384(text))) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.sha512_256(x.Text) }))
                .Should().Contain("lower(hex(SHA512_256(text))) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.blake3(x.Text) }))
                .Should().Contain("lower(hex(BLAKE3(text))) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.md4(x.Text) }))
                .Should().Contain("lower(hex(MD4(text))) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.ripemd160(x.Text) }))
                .Should().Contain("lower(hex(RIPEMD160(text))) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.keccak256(x.Text) }))
                .Should().Contain("lower(hex(keccak256(text))) as `V`");

            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.farm_hash64(x.Text) }))
                .Should().Contain("farmHash64(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.metro_hash64(x.Text) }))
                .Should().Contain("metroHash64(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.java_hash(x.Text) }))
                .Should().Contain("javaHash(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.hive_hash(x.Text) }))
                .Should().Contain("hiveHash(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.half_md5(x.Text) }))
                .Should().Contain("halfMD5(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.wy_hash64(x.Text) }))
                .Should().Contain("wyHash64(text) as `V`");

            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.url_hash(x.Url) }))
                .Should().Contain("URLHash(url) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.url_hash(x.Url, 1) }))
                .Should().Contain("URLHash(url, 1) as `V`");

            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.int_hash32(x.Number) }))
                .Should().Contain("intHash32(number) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.int_hash64(x.Number) }))
                .Should().Contain("intHash64(number) as `V`");

            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.ngram_min_hash(x.Text) }))
                .Should().Contain("ngramMinHash(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.ngram_sim_hash(x.Text) }))
                .Should().Contain("ngramSimHash(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.ngram_min_hash_arg(x.Text) }))
                .Should().Contain("ngramMinHashArg(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.word_shingle_min_hash(x.Text) }))
                .Should().Contain("wordShingleMinHash(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.word_shingle_min_hash_arg(x.Text) }))
                .Should().Contain("wordShingleMinHashArg(text) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.word_shingle_sim_hash(x.Text) }))
                .Should().Contain("wordShingleSimHash(text) as `V`");
        }
    }

    [Fact]
    public void Digests_ShouldKeepTheLowercaseHexContractAndBindCapturedValues()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bHashEntity>();
        var value = "abc";

        var command = Prepare(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.sha224(value) }));

        Normalize(command.DbCommand.CommandText).Should().Contain("lower(hex(SHA224(@value))) as `V`");
        command.DbCommandParams.Cast<System.Data.Common.DbParameter>().Should().ContainSingle()
            .Which.Value.Should().Be(value);

        // The new digest members use the identical spelling contract, so the rendering is stable
        // for a captured value as well as for a column.
        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.ClickHouse.sha224(value),
            B = SqlFunctions.ClickHouse.md4(value),
            C = SqlFunctions.ClickHouse.keccak256(value)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("lower(hex(SHA224(@value))) as `A`");
            sql.Should().Contain("lower(hex(MD4(@value))) as `B`");
            sql.Should().Contain("lower(hex(keccak256(@value))) as `C`");
            sql.Should().Contain("lower(hex(");
        }
    }

    [Fact]
    public void DeclaredShapes_ShouldMatchTheNativeResultTypes()
    {
        using (new AssertionScope())
        {
            ReturnType("sha224", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("sha384", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("sha512_256", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("blake3", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("md4", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("ripemd160", [typeof(string)]).Should().Be(typeof(string));
            ReturnType("keccak256", [typeof(string)]).Should().Be(typeof(string));

            ReturnType("farm_hash64", [typeof(string)]).Should().Be(typeof(ulong?),
                "farmHash64 returns UInt64, kept unsigned");
            ReturnType("metro_hash64", [typeof(string)]).Should().Be(typeof(ulong?));
            ReturnType("half_md5", [typeof(string)]).Should().Be(typeof(ulong?));
            ReturnType("wy_hash64", [typeof(string)]).Should().Be(typeof(ulong?));
            ReturnType("url_hash", [typeof(string)]).Should().Be(typeof(ulong?));
            ReturnType("url_hash", [typeof(string), typeof(byte)]).Should().Be(typeof(ulong?));
            ReturnType("java_hash", [typeof(string)]).Should().Be(typeof(int?),
                "javaHash returns Int32 and can be negative");
            ReturnType("hive_hash", [typeof(string)]).Should().Be(typeof(int?));
            ReturnType("int_hash32", [typeof(long)]).Should().Be(typeof(uint?),
                "intHash32 returns UInt32, never a negative Int32");
            ReturnType("int_hash64", [typeof(long)]).Should().Be(typeof(ulong?));

            ReturnType("ngram_min_hash", [typeof(string), typeof(byte)]).Should().Be(typeof(Tuple<ulong, ulong>));
            ReturnType("word_shingle_min_hash", [typeof(string), typeof(byte)]).Should().Be(typeof(Tuple<ulong, ulong>));
            ReturnType("ngram_sim_hash", [typeof(string), typeof(byte)]).Should().Be(typeof(ulong?));
            ReturnType("word_shingle_sim_hash", [typeof(string), typeof(byte)]).Should().Be(typeof(ulong?));
            ReturnType("ngram_min_hash_arg", [typeof(string), typeof(byte)]).Should().Be(typeof(Tuple<
                Tuple<string, string, string, string, string, string>,
                Tuple<string, string, string, string, string, string>>));
            ReturnType("word_shingle_min_hash_arg", [typeof(string), typeof(byte)]).Should().Be(typeof(Tuple<
                Tuple<string, string, string, string, string, string>,
                Tuple<string, string, string, string, string, string>>));
        }

        static Type ReturnType(string name, Type[] arguments)
            => typeof(ClickHouseFunctions)
                .GetMethod(name, BindingFlags.Public | BindingFlags.Instance, arguments)!
                .ReturnType;
    }

    [Fact]
    public void UnsignedHashes_ShouldNotBeLoweredToASignedInt64()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bHashEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Farm = SqlFunctions.ClickHouse.farm_hash64(x.Text),
            Metro = SqlFunctions.ClickHouse.metro_hash64(x.Text),
            Half = SqlFunctions.ClickHouse.half_md5(x.Text),
            Url = SqlFunctions.ClickHouse.url_hash(x.Url),
            Wy = SqlFunctions.ClickHouse.wy_hash64(x.Text),
            I32 = SqlFunctions.ClickHouse.int_hash32(x.Number),
            I64 = SqlFunctions.ClickHouse.int_hash64(x.Number)
        }));

        using (new AssertionScope())
        {
            // The shipped xxHash/cityHash/murmur members keep their historical toInt64 lowering;
            // the F10 members must not inherit it, or UInt64 values would be reinterpreted.
            sql.Should().NotContain("toInt64(farmHash64");
            sql.Should().NotContain("toInt64(metroHash64");
            sql.Should().NotContain("toInt64(halfMD5");
            sql.Should().NotContain("toInt64(URLHash");
            sql.Should().NotContain("toInt64(wyHash64");
            sql.Should().NotContain("toInt32(intHash32");
            sql.Should().NotContain("toInt64(intHash64");
        }
    }

    [Fact]
    public void NgramAndShingleMembers_ShouldRenderTheUInt8SizesAndOmitHashnum()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bHashEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            NmDefault = SqlFunctions.ClickHouse.ngram_min_hash(x.Text),
            NmTwo = SqlFunctions.ClickHouse.ngram_min_hash(x.Text, 2),
            NaTwo = SqlFunctions.ClickHouse.ngram_min_hash_arg(x.Text, 2),
            NsTwo = SqlFunctions.ClickHouse.ngram_sim_hash(x.Text, 2),
            WmThree = SqlFunctions.ClickHouse.word_shingle_min_hash(x.Text, 3),
            WaThree = SqlFunctions.ClickHouse.word_shingle_min_hash_arg(x.Text, 3),
            WsThree = SqlFunctions.ClickHouse.word_shingle_sim_hash(x.Text, 3)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("ngramMinHash(text) as `NmDefault`", "the default ngramsize is the vendor's 3");
            sql.Should().Contain("ngramMinHash(text, cast(2 as UInt8)) as `NmTwo`");
            sql.Should().Contain("ngramMinHashArg(text, cast(2 as UInt8)) as `NaTwo`");
            sql.Should().Contain("ngramSimHash(text, cast(2 as UInt8)) as `NsTwo`");
            sql.Should().Contain("wordShingleMinHash(text, cast(3 as UInt8)) as `WmThree`");
            sql.Should().Contain("wordShingleMinHashArg(text, cast(3 as UInt8)) as `WaThree`");
            sql.Should().Contain("wordShingleSimHash(text, cast(3 as UInt8)) as `WsThree`");

            // hashnum is the arity of the arg-returning shape and is fixed at the vendor default 6;
            // it is never part of the rendered call.
            sql.Should().NotContain(", 6)");
            sql.Should().NotContain("hashnum");
        }
    }

    [Fact]
    public void OptionalSizes_ShouldBeDroppedOnlyWhenTheDefaultIsUsed()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bHashEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Omitted = SqlFunctions.ClickHouse.ngram_min_hash(x.Text, null),
            ExplicitZero = SqlFunctions.ClickHouse.ngram_min_hash(x.Text, 0)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("ngramMinHash(text) as `Omitted`", "a null size is the one-argument form");
            sql.Should().NotContain("ngramMinHash(text, NULL)");
            sql.Should().Contain("ngramMinHash(text, cast(0 as UInt8)) as `ExplicitZero`",
                "an explicit zero is emitted, not dropped");
        }
    }

    [Fact]
    public void Projection_ShouldRenderEveryDeliveredMemberInOneSelect()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bHashEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            S224 = SqlFunctions.ClickHouse.sha224(x.Text),
            S384 = SqlFunctions.ClickHouse.sha384(x.Text),
            S512 = SqlFunctions.ClickHouse.sha512_256(x.Text),
            B3 = SqlFunctions.ClickHouse.blake3(x.Text),
            M4 = SqlFunctions.ClickHouse.md4(x.Text),
            R160 = SqlFunctions.ClickHouse.ripemd160(x.Text),
            K256 = SqlFunctions.ClickHouse.keccak256(x.Text),
            Farm = SqlFunctions.ClickHouse.farm_hash64(x.Text),
            Metro = SqlFunctions.ClickHouse.metro_hash64(x.Text),
            Java = SqlFunctions.ClickHouse.java_hash(x.Text),
            Hive = SqlFunctions.ClickHouse.hive_hash(x.Text),
            Half = SqlFunctions.ClickHouse.half_md5(x.Text),
            Url = SqlFunctions.ClickHouse.url_hash(x.Url, 0),
            Wy = SqlFunctions.ClickHouse.wy_hash64(x.Text),
            I32 = SqlFunctions.ClickHouse.int_hash32(x.Number),
            I64 = SqlFunctions.ClickHouse.int_hash64(x.Number),
            Nm = SqlFunctions.ClickHouse.ngram_min_hash(x.Text, 3),
            Na = SqlFunctions.ClickHouse.ngram_min_hash_arg(x.Text, 3),
            Ns = SqlFunctions.ClickHouse.ngram_sim_hash(x.Text, 3),
            Wm = SqlFunctions.ClickHouse.word_shingle_min_hash(x.Text, 3),
            Wa = SqlFunctions.ClickHouse.word_shingle_min_hash_arg(x.Text, 3),
            Ws = SqlFunctions.ClickHouse.word_shingle_sim_hash(x.Text, 3)
        }));

        foreach (var name in new[]
                 {
                     "S224", "S384", "S512", "B3", "M4", "R160", "K256", "Farm", "Metro", "Java",
                     "Hive", "Half", "Url", "Wy", "I32", "I64", "Nm", "Na", "Ns", "Wm", "Wa", "Ws"
                 })
            sql.Should().Contain($"as `{name}`");
    }

    [Fact]
    public void Predicate_ShouldKeepTheHashesInTheWhereClause()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bHashEntity>();

        SqlOf(ctx, e
                .Where(x => SqlFunctions.ClickHouse.java_hash(x.Text) == 96354
                            && SqlFunctions.ClickHouse.int_hash32(x.Number) > 0
                            && SqlFunctions.ClickHouse.ngram_sim_hash(x.Text) != 0)
                .Select(x => new { x.Id }))
            .Should().Match("*where*javaHash(text) = 96354*intHash32(number) > 0*ngramSimHash(text) != 0*");
    }

    [Fact]
    public void AlreadyCoveredHashSurface_ShouldKeepTheExistingArms()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bHashEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Md5 = SqlFunctions.ClickHouse.md5(x.Text),
            Sha1 = SqlFunctions.ClickHouse.sha1(x.Text),
            Sha256 = SqlFunctions.ClickHouse.sha256(x.Text),
            Sha512 = SqlFunctions.ClickHouse.sha512(x.Text),
            Xx32 = SqlFunctions.ClickHouse.xx_hash32(x.Text),
            Xx64 = SqlFunctions.ClickHouse.xx_hash64(x.Text),
            Xxh3 = SqlFunctions.ClickHouse.xxh3(x.Text),
            City = SqlFunctions.ClickHouse.city_hash64(x.Text),
            Sip64 = SqlFunctions.ClickHouse.sip_hash64(x.Text),
            Sip128 = SqlFunctions.ClickHouse.sip_hash128(x.Text),
            M2 = SqlFunctions.ClickHouse.murmur_hash2_32(x.Text),
            M3 = SqlFunctions.ClickHouse.murmur_hash3_128(x.Text)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("lower(hex(MD5(text))) as `Md5`");
            sql.Should().Contain("lower(hex(SHA1(text))) as `Sha1`");
            sql.Should().Contain("lower(hex(SHA256(text))) as `Sha256`");
            sql.Should().Contain("lower(hex(SHA512(text))) as `Sha512`");
            sql.Should().Contain("toInt32(xxHash32(text)) as `Xx32`");
            sql.Should().Contain("toInt64(xxHash64(text)) as `Xx64`");
            sql.Should().Contain("toInt64(xxh3(text)) as `Xxh3`");
            sql.Should().Contain("toInt64(cityHash64(text)) as `City`");
            sql.Should().Contain("toInt64(sipHash64(text)) as `Sip64`");
            sql.Should().Contain("lower(hex(sipHash128(text))) as `Sip128`");
            sql.Should().Contain("toInt32(murmurHash2_32(text)) as `M2`");
            sql.Should().Contain("lower(hex(murmurHash3_128(text))) as `M3`");
        }
    }

    [Fact]
    public void RefusedMembers_ShouldFailClosedWithThePreciseReasonOnEveryProvider()
    {
        using var clickhouse = ClickHouseTestContext.Create();
        using var postgres = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        using var sqlServer = new SqlServerDataContext(
            "Server=localhost;Database=nextorm;User Id=sa;Password=nextorm;TrustServerCertificate=True",
            new DataContextBuilder());

        using (new AssertionScope())
        {
            foreach (var ctx in new IDataContext[] { clickhouse, postgres, sqlServer })
            {
                FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bHashEntity>()
                        .Select(x => new { V = SqlFunctions.ClickHouse.ngram_sim_hash_arg(x.Text, 3) })))
                    .Should().Throw<NotSupportedException>()
                    .WithMessage("*ngramSimHashArg*ngramMinHashArg*UNKNOWN_FUNCTION*");

                FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bHashEntity>()
                        .Select(x => new { V = SqlFunctions.ClickHouse.word_shingle_sim_hash_arg(x.Text, 3) })))
                    .Should().Throw<NotSupportedException>()
                    .WithMessage("*wordShingleSimHashArg*wordShingleMinHashArg*UNKNOWN_FUNCTION*");
            }
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

        static void AssertRejected(IDataContext ctx, string name, string message = "*not supported by this provider*")
        {
            Func<string> query = name switch
            {
                "sha224" => () => SqlOf(ctx, ctx.From<IT286bHashEntity>().Select(x => new { V = SqlFunctions.ClickHouse.sha224(x.Text) })),
                "sha384" => () => SqlOf(ctx, ctx.From<IT286bHashEntity>().Select(x => new { V = SqlFunctions.ClickHouse.sha384(x.Text) })),
                "sha512_256" => () => SqlOf(ctx, ctx.From<IT286bHashEntity>().Select(x => new { V = SqlFunctions.ClickHouse.sha512_256(x.Text) })),
                "blake3" => () => SqlOf(ctx, ctx.From<IT286bHashEntity>().Select(x => new { V = SqlFunctions.ClickHouse.blake3(x.Text) })),
                "md4" => () => SqlOf(ctx, ctx.From<IT286bHashEntity>().Select(x => new { V = SqlFunctions.ClickHouse.md4(x.Text) })),
                "ripemd160" => () => SqlOf(ctx, ctx.From<IT286bHashEntity>().Select(x => new { V = SqlFunctions.ClickHouse.ripemd160(x.Text) })),
                "keccak256" => () => SqlOf(ctx, ctx.From<IT286bHashEntity>().Select(x => new { V = SqlFunctions.ClickHouse.keccak256(x.Text) })),
                "farm_hash64" => () => SqlOf(ctx, ctx.From<IT286bHashEntity>().Select(x => new { V = SqlFunctions.ClickHouse.farm_hash64(x.Text) })),
                "metro_hash64" => () => SqlOf(ctx, ctx.From<IT286bHashEntity>().Select(x => new { V = SqlFunctions.ClickHouse.metro_hash64(x.Text) })),
                "java_hash" => () => SqlOf(ctx, ctx.From<IT286bHashEntity>().Select(x => new { V = SqlFunctions.ClickHouse.java_hash(x.Text) })),
                "hive_hash" => () => SqlOf(ctx, ctx.From<IT286bHashEntity>().Select(x => new { V = SqlFunctions.ClickHouse.hive_hash(x.Text) })),
                "half_md5" => () => SqlOf(ctx, ctx.From<IT286bHashEntity>().Select(x => new { V = SqlFunctions.ClickHouse.half_md5(x.Text) })),
                "url_hash" => () => SqlOf(ctx, ctx.From<IT286bHashEntity>().Select(x => new { V = SqlFunctions.ClickHouse.url_hash(x.Url, 1) })),
                "wy_hash64" => () => SqlOf(ctx, ctx.From<IT286bHashEntity>().Select(x => new { V = SqlFunctions.ClickHouse.wy_hash64(x.Text) })),
                "int_hash32" => () => SqlOf(ctx, ctx.From<IT286bHashEntity>().Select(x => new { V = SqlFunctions.ClickHouse.int_hash32(x.Number) })),
                "int_hash64" => () => SqlOf(ctx, ctx.From<IT286bHashEntity>().Select(x => new { V = SqlFunctions.ClickHouse.int_hash64(x.Number) })),
                "ngram_min_hash" => () => SqlOf(ctx, ctx.From<IT286bHashEntity>().Select(x => new { V = SqlFunctions.ClickHouse.ngram_min_hash(x.Text) })),
                "ngram_min_hash_arg" => () => SqlOf(ctx, ctx.From<IT286bHashEntity>().Select(x => new { V = SqlFunctions.ClickHouse.ngram_min_hash_arg(x.Text) })),
                "ngram_sim_hash" => () => SqlOf(ctx, ctx.From<IT286bHashEntity>().Select(x => new { V = SqlFunctions.ClickHouse.ngram_sim_hash(x.Text) })),
                "word_shingle_min_hash" => () => SqlOf(ctx, ctx.From<IT286bHashEntity>().Select(x => new { V = SqlFunctions.ClickHouse.word_shingle_min_hash(x.Text) })),
                "word_shingle_min_hash_arg" => () => SqlOf(ctx, ctx.From<IT286bHashEntity>().Select(x => new { V = SqlFunctions.ClickHouse.word_shingle_min_hash_arg(x.Text) })),
                "word_shingle_sim_hash" => () => SqlOf(ctx, ctx.From<IT286bHashEntity>().Select(x => new { V = SqlFunctions.ClickHouse.word_shingle_sim_hash(x.Text) })),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
            };

            FluentActions.Invoking(query).Should().Throw<NotSupportedException>(
                "'{0}' is ClickHouse-only and must be refused by every other provider: {1}", name, message)
                .WithMessage(message);
        }
    }
}
