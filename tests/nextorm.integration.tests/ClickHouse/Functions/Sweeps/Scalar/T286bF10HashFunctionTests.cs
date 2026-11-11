using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClickHouse.Driver;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T286b D10 (F10 Hash): live execution of the ClickHouse-only hash members against the pinned
/// <c>25.8-alpine</c> (25.8.33.6) server. The tests pin the digest hex contract (lowercase, leading
/// zeros, empty and Unicode input), the vendor width and signedness of the non-cryptographic hashes
/// (<c>UInt64</c>/<c>UInt32</c>/<c>Int32</c>, never a <c>toInt64</c> reinterpretation), the
/// <c>URLHash</c> trailing-separator and level rules, the n-gram/word-shingle tuple shapes (two
/// <c>UInt64</c> hashes; two six-component string tuples with empty-string padding), the
/// <c>Nullable</c>-argument refusal of the tuple-returning members, the NULL flow of every scalar
/// result, and the deterministic refusals of the two members that exist in no ClickHouse version
/// (<c>ngramSimHashArg</c>, <c>wordShingleSimHashArg</c>).
/// </summary>
[Collection("ClickHouse")]
public sealed class T286bF10HashFunctionTests : ProviderTestSuite
{
    [SqlTable("t286b_f10_entity")]
    internal interface IT286bHashEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("text")]
        string? Text { get; set; }

        [Column("text_nn")]
        string TextNonNull { get; set; }

        [Column("url")]
        string? Url { get; set; }

        [Column("number")]
        long Number { get; set; }
    }

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private const string ShingleText =
        "ClickHouse® is a column-oriented database management system (DBMS) for online analytical " +
        "processing of queries (OLAP).";

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
                     "drop table if exists t286b_f10_entity",
                     "create table t286b_f10_entity (id Int32, text Nullable(String), text_nn String, "
                     + "url Nullable(String), number Int64) engine = Memory",
                     "insert into t286b_f10_entity (id, text, text_nn, url, number) values "
                     + "(1, 'abc', 'abc', 'https://www.clickhouse.com', 42), "
                     + "(2, NULL, '', NULL, 0), "
                     + "(3, '', '', 'https://www.clickhouse.com/', -1), "
                     + "(4, 'Привет', 'Привет', 'https://www.clickhouse.com/docs', 9223372036854775807)"
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    [Fact]
    public void Digests_ShouldMatchThePinnedHexVectors()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bHashEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Sha224 = SqlFunctions.ClickHouse.sha224(x.Text),
                Sha384 = SqlFunctions.ClickHouse.sha384(x.Text),
                Sha512_256 = SqlFunctions.ClickHouse.sha512_256(x.Text),
                Blake3 = SqlFunctions.ClickHouse.blake3(x.Text),
                Md4 = SqlFunctions.ClickHouse.md4(x.Text),
                Ripemd160 = SqlFunctions.ClickHouse.ripemd160(x.Text),
                Keccak256 = SqlFunctions.ClickHouse.keccak256(x.Text),
                Sha224Empty = SqlFunctions.ClickHouse.sha224(""),
                Md4Empty = SqlFunctions.ClickHouse.md4(""),
                Sha384Empty = SqlFunctions.ClickHouse.sha384(""),
                Blake3Empty = SqlFunctions.ClickHouse.blake3(""),
                Keccak256Empty = SqlFunctions.ClickHouse.keccak256("")
            })
            .First();

        using (new AssertionScope())
        {
            r.Sha224.Should().Be("23097d223405d8228642a477bda255b32aadbce4bda0b3f7e36c9da7");
            r.Sha384.Should().Be(
                "cb00753f45a35e8bb5a03d699ac65007272c32ab0eded1631a8b605a43ff5bed" +
                "8086072ba1e7cc2358baeca134c825a7");
            r.Sha512_256.Should().Be("53048e2681941ef99b2e29b76b4c7dabe4c2d0c634fc6d46e0e2f13107e7af23");
            r.Blake3.Should().Be("6437b3ac38465133ffb63b75273a8db548c558465d79db03fd359c6cd5bd9d85");
            r.Md4.Should().Be("a448017aaf21d8525fc10ae87aa6729d");
            r.Ripemd160.Should().Be("8eb208f7e05d987a9b044a8e98c6b087f15a0bfc");
            r.Keccak256.Should().Be("4e03657aea45a94fc7d47ba826c8d667c0d1e6e33a64a036ec44f58fa12d6c45");

            // The empty input is a real digest, never an empty string: the leading zeros survive the
            // hex rendering because the server returns a fixed-length byte string.
            r.Sha224Empty.Should().Be("d14a028c2a3a2bc9476102bb288234c415a2b01f828ea62ac5b3e42f");
            r.Md4Empty.Should().Be("31d6cfe0d16ae931b73c59d7e0c089c0");
            r.Sha384Empty.Should().Be(
                "38b060a751ac96384cd9327eb1b1e36a21fdb71114be07434c0cc7bf63f6e1da" +
                "274edebfe76f65fbd51ad2f14898b95b");
            r.Blake3Empty.Should().Be("af1349b9f5f9a1a6a0404dea36dcc9499bcb25c9adc112b7cc9a93cae41f3262");
            r.Keccak256Empty.Should().Be(
                "c5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470");
        }
    }

    [Fact]
    public void Digests_ShouldHashUnicodeAsUtf8AndReturnNullForNullInput()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var unicode = ctx.From<IT286bHashEntity>()
            .Where(x => x.Id == 4)
            .Select(x => new
            {
                Sha224 = SqlFunctions.ClickHouse.sha224(x.Text),
                Md4 = SqlFunctions.ClickHouse.md4(x.Text)
            })
            .First();
        var nulls = ctx.From<IT286bHashEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                Sha224 = SqlFunctions.ClickHouse.sha224(x.Text),
                Md4 = SqlFunctions.ClickHouse.md4(x.Text),
                Keccak256 = SqlFunctions.ClickHouse.keccak256(x.Text)
            })
            .First();

        using (new AssertionScope())
        {
            unicode.Sha224.Should().Be("891cfb544eb6f3c212190705f7229d91db6cecd4718ea65e0fa1b112");
            unicode.Md4.Should().Be("091641d28dd4056a7dd0c9a0b4f8711e");

            nulls.Sha224.Should().BeNull("hash of NULL is NULL");
            nulls.Md4.Should().BeNull();
            nulls.Keccak256.Should().BeNull();
        }
    }

    [Fact]
    public void UnsignedHashes_ShouldKeepTheVendorUInt64Values()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bHashEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Farm = SqlFunctions.ClickHouse.farm_hash64(x.Text),
                Metro = SqlFunctions.ClickHouse.metro_hash64(x.Text),
                Half = SqlFunctions.ClickHouse.half_md5(x.Text),
                Wy = SqlFunctions.ClickHouse.wy_hash64(x.Text),
                FarmEmpty = SqlFunctions.ClickHouse.farm_hash64(""),
                MetroEmpty = SqlFunctions.ClickHouse.metro_hash64(""),
                HalfEmpty = SqlFunctions.ClickHouse.half_md5(""),
                WyEmpty = SqlFunctions.ClickHouse.wy_hash64(""),
                Int64Hash = SqlFunctions.ClickHouse.int_hash64(x.Number),
                Int64Negative = SqlFunctions.ClickHouse.int_hash64(-1L)
            })
            .First();

        using (new AssertionScope())
        {
            r.Farm.Should().Be(2640714258260161385UL);
            r.Metro.Should().Be(983152225323542437UL);
            r.Half.Should().Be(10376663631224000432UL);
            r.Wy.Should().Be(13006551795127091151UL);
            r.FarmEmpty.Should().Be(11160318154034397263UL);
            r.MetroEmpty.Should().Be(13381918016739805347UL);
            r.HalfEmpty.Should().Be(15284527576400310788UL);
            r.WyEmpty.Should().Be(4808886099364463827UL);
            r.Int64Hash.Should().Be(11490350930367293593UL);
            r.Int64Negative.Should().Be(14600443904207254319UL);
        }
    }

    [Fact]
    public void SignedHashes_ShouldKeepTheVendorInt32Values()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bHashEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Java = SqlFunctions.ClickHouse.java_hash(x.Text),
                Hive = SqlFunctions.ClickHouse.hive_hash(x.Text),
                JavaNegative = SqlFunctions.ClickHouse.java_hash("Hello, world!"),
                HiveNegative = SqlFunctions.ClickHouse.hive_hash("Hello, world!"),
                JavaEmpty = SqlFunctions.ClickHouse.java_hash(""),
                HiveEmpty = SqlFunctions.ClickHouse.hive_hash("")
            })
            .First();

        using (new AssertionScope())
        {
            r.Java.Should().Be(96354);
            r.Hive.Should().Be(96354);
            // javaHash is signed and can be negative; hiveHash is javaHash with the sign bit cleared.
            r.JavaNegative.Should().Be(-1880044555);
            r.HiveNegative.Should().Be(267439093);
            r.JavaEmpty.Should().Be(0);
            r.HiveEmpty.Should().Be(0);
        }
    }

    [Fact]
    public void UrlHash_ShouldNormalizeTheTrailingSeparatorAndUseTheLevel()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bHashEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Plain = SqlFunctions.ClickHouse.url_hash("https://www.clickhouse.com"),
                Slash = SqlFunctions.ClickHouse.url_hash("https://www.clickhouse.com/"),
                Question = SqlFunctions.ClickHouse.url_hash("https://www.clickhouse.com?"),
                Hash = SqlFunctions.ClickHouse.url_hash("https://www.clickhouse.com#"),
                LevelZero = SqlFunctions.ClickHouse.url_hash("https://www.clickhouse.com/docs", 0),
                LevelOne = SqlFunctions.ClickHouse.url_hash("https://www.clickhouse.com/docs", 1),
                Column = SqlFunctions.ClickHouse.url_hash(x.Url)
            })
            .First();
        var nullUrl = ctx.From<IT286bHashEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new { V = SqlFunctions.ClickHouse.url_hash(x.Url) })
            .First();

        using (new AssertionScope())
        {
            r.Plain.Should().Be(13614512636072854701UL);
            r.Slash.Should().Be(r.Plain, "one trailing '/' is ignored");
            r.Question.Should().Be(r.Plain, "one trailing '?' is ignored");
            r.Hash.Should().Be(r.Plain, "one trailing '#' is ignored");
            r.LevelZero.Should().Be(r.Plain, "level 0 is the whole normalized URL");
            r.LevelOne.Should().Be(13167253331440520598UL);
            r.Column.Should().Be(r.Plain);
            nullUrl.V.Should().BeNull();
        }
    }

    [Fact]
    public void IntHash_ShouldHashTheIntegerArgument()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bHashEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Int32 = SqlFunctions.ClickHouse.int_hash32(x.Number),
                Int64 = SqlFunctions.ClickHouse.int_hash64(x.Number),
                Int32Zero = SqlFunctions.ClickHouse.int_hash32(0L)
            })
            .First();

        using (new AssertionScope())
        {
            r.Int32.Should().Be(1228623923U);
            r.Int64.Should().Be(11490350930367293593UL);
            r.Int32Zero.Should().Be(4249604106U);
        }
    }

    [Fact]
    public void NgramMinHash_ShouldReturnTheTwoHashTuple()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bHashEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Default = SqlFunctions.ClickHouse.ngram_min_hash("ClickHouse"),
                SizeTwo = SqlFunctions.ClickHouse.ngram_min_hash("ClickHouse", 2),
                Short = SqlFunctions.ClickHouse.ngram_min_hash("abc", 2),
                Column = SqlFunctions.ClickHouse.ngram_min_hash(x.TextNonNull)
            })
            .First();

        using (new AssertionScope())
        {
            // The default ngramsize is the vendor's 3.
            r.Default.Should().Be(Tuple.Create(18333312859352735453UL, 9054248444481805918UL));
            r.SizeTwo.Should().Be(Tuple.Create(4107141120268157302UL, 7767933285988254157UL));
            r.Short.Should().Be(Tuple.Create(10225156251682811963UL, 3566440066242257242UL));
            // Row 1 holds 'abc'; the column form carries the same vendor default ngramsize (3).
            r.Column.Should().Be(Tuple.Create(14538029575209348823UL, 14538029575209348823UL));
        }
    }

    [Fact]
    public void NgramMinHashArg_ShouldReturnTwoSixComponentTuples()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bHashEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Default = SqlFunctions.ClickHouse.ngram_min_hash_arg("ClickHouse"),
                SizeTwo = SqlFunctions.ClickHouse.ngram_min_hash_arg("ClickHouse", 2),
                Padded = SqlFunctions.ClickHouse.ngram_min_hash_arg("ab", 2),
                Empty = SqlFunctions.ClickHouse.ngram_min_hash_arg("")
            })
            .First();

        using (new AssertionScope())
        {
            r.Default.Should().Be(Tuple.Create(
                Tuple.Create("ous", "ick", "lic", "Hou", "kHo", "use"),
                Tuple.Create("Hou", "lic", "ick", "ous", "ckH", "Cli")));
            r.SizeTwo.Should().Be(Tuple.Create(
                Tuple.Create("us", "ic", "Ho", "li", "ck", "ou"),
                Tuple.Create("Ho", "ic", "us", "se", "Cl", "kH")));

            // hashnum is fixed at the vendor default 6: a short input pads both tuples with empty
            // strings, so the declared six-component shape always holds.
            r.Padded.Item1.Item1.Should().Be("ab");
            r.Padded.Item1.Item2.Should().BeEmpty();
            r.Padded.Item1.Item6.Should().BeEmpty("the declared six-component shape is always full");
            r.Padded.Item2.Item1.Should().Be("ab");
            r.Padded.Item2.Item6.Should().BeEmpty();
            r.Empty.Item1.Should().Be(Tuple.Create("", "", "", "", "", ""));
            r.Empty.Item2.Should().Be(Tuple.Create("", "", "", "", "", ""));
        }
    }

    [Fact]
    public void NgramSimHash_ShouldReturnTheVendorUInt64()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bHashEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Default = SqlFunctions.ClickHouse.ngram_sim_hash("ClickHouse"),
                SizeTwo = SqlFunctions.ClickHouse.ngram_sim_hash("abc def ghi", 2),
                Empty = SqlFunctions.ClickHouse.ngram_sim_hash("")
            })
            .First();

        using (new AssertionScope())
        {
            r.Default.Should().Be(1627567969UL);
            r.SizeTwo.Should().Be(2522426947UL);
            // The empty input is all bits set, not zero: the simhash of no n-grams is UInt64.MaxValue.
            r.Empty.Should().Be(ulong.MaxValue);
            r.Empty.Should().Be(18446744073709551615UL);
        }
    }

    [Fact]
    public void WordShingleMinHash_ShouldReturnTheTuple()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bHashEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Documented = SqlFunctions.ClickHouse.word_shingle_min_hash(ShingleText),
                FourWords = SqlFunctions.ClickHouse.word_shingle_min_hash("abcd efgh ijkl mnop"),
                Empty = SqlFunctions.ClickHouse.word_shingle_min_hash("")
            })
            .First();

        using (new AssertionScope())
        {
            r.Documented.Should().Be(Tuple.Create(16452112859864147620UL, 5844417301642981317UL));
            r.FourWords.Should().NotBeNull();
            r.FourWords!.Item1.Should().NotBe(0UL);
            r.Empty.Should().Be(Tuple.Create(0UL, 0UL), "no shingle means the zero pair, never a random value");
        }
    }

    [Fact]
    public void WordShingleMinHashArg_ShouldReturnTwoSixComponentTuples()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bHashEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Documented = SqlFunctions.ClickHouse.word_shingle_min_hash_arg(ShingleText),
                FourWords = SqlFunctions.ClickHouse.word_shingle_min_hash_arg("abcd efgh ijkl mnop"),
                Padded = SqlFunctions.ClickHouse.word_shingle_min_hash_arg("abcd")
            })
            .First();

        using (new AssertionScope())
        {
            r.Documented.Should().Be(Tuple.Create(
                Tuple.Create(
                    "ClickHouse® is a column-oriented",
                    "database management system",
                    "analytical processing of queries",
                    "management system (DBMS",
                    "DBMS) for online analytical",
                    "system (DBMS) for online"),
                Tuple.Create(
                    "database management system",
                    "ClickHouse® is a column-oriented",
                    "online analytical processing",
                    "column-oriented database",
                    "processing of queries (OLAP",
                    "oriented database management")));

            // Two shingles fill two of the six hashnum slots; the remaining slots are empty strings.
            r.FourWords.Should().Be(Tuple.Create(
                Tuple.Create("efgh ijkl mnop", "abcd efgh ijkl", "", "", "", ""),
                Tuple.Create("abcd efgh ijkl", "efgh ijkl mnop", "", "", "", "")));

            r.Padded.Item1.Should().Be(Tuple.Create("abcd", "", "", "", "", ""));
            r.Padded.Item2.Should().Be(Tuple.Create("abcd", "", "", "", "", ""));
        }
    }

    [Fact]
    public void WordShingle_ShouldTreatOnlyTokensOfFourOrMoreCharactersAsWords()
    {
        // Observed vendor behavior of the ASCII word splitter (25.8.33.6): a run of fewer than four
        // characters is not a word, and a shingle is the source substring spanning its words. So a
        // text made only of three-character tokens has no shingle at all and yields the zero pair,
        // while a single four-character token yields exactly one shingle equal to that token.
        Seed();

        using (new AssertionScope())
        {
            Scalar<ulong>("tupleElement(wordShingleMinHash('aaa bbb ccc'), 1)").Should().Be(0UL);
            Scalar<ulong>("wordShingleSimHash('aaa bbb ccc')").Should().Be(0UL);
            Scalar<ulong>("tupleElement(wordShingleMinHash('aaa bbbb ccc'), 1)")
                .Should().Be(4303294151403509773UL, "the single four-character token is the only word");
            Scalar<string>("tupleElement(wordShingleMinHashArg('aaa bbbb ccc'), 1).1")
                .Should().Be("bbbb");
        }
    }

    [Fact]
    public void NullableArgumentColumn_ShouldBeRefusedByTheServerForTheTupleMembers()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        using (new AssertionScope())
        {
            // A Tuple cannot be inside Nullable, so the vendor function itself rejects a Nullable
            // argument column; the library must not hide that behind a silent default.
            FluentActions.Invoking(() => ctx.From<IT286bHashEntity>()
                    .Where(x => x.Id == 1)
                    .Select(x => new { V = SqlFunctions.ClickHouse.ngram_min_hash(x.Text) })
                    .First())
                .Should().Throw<ClickHouseServerException>()
                .WithMessage("*Nested type Tuple*inside Nullable*");

            FluentActions.Invoking(() => ctx.From<IT286bHashEntity>()
                    .Where(x => x.Id == 1)
                    .Select(x => new { V = SqlFunctions.ClickHouse.word_shingle_min_hash(x.Text) })
                    .First())
                .Should().Throw<ClickHouseServerException>()
                .WithMessage("*Nested type Tuple*inside Nullable*");

            FluentActions.Invoking(() => ctx.From<IT286bHashEntity>()
                    .Where(x => x.Id == 1)
                    .Select(x => new { V = SqlFunctions.ClickHouse.ngram_min_hash_arg(x.Text) })
                    .First())
                .Should().Throw<ClickHouseServerException>()
                .WithMessage("*Nested type Tuple*inside Nullable*");
        }
    }

    [Fact]
    public void Nulls_ShouldFlowThroughEveryScalarHashResult()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bHashEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                Farm = SqlFunctions.ClickHouse.farm_hash64(x.Text),
                Metro = SqlFunctions.ClickHouse.metro_hash64(x.Text),
                Java = SqlFunctions.ClickHouse.java_hash(x.Text),
                Hive = SqlFunctions.ClickHouse.hive_hash(x.Text),
                Half = SqlFunctions.ClickHouse.half_md5(x.Text),
                Wy = SqlFunctions.ClickHouse.wy_hash64(x.Text),
                Int32 = SqlFunctions.ClickHouse.int_hash32(x.Number),
                NgramSim = SqlFunctions.ClickHouse.ngram_sim_hash(x.Text),
                WordSim = SqlFunctions.ClickHouse.word_shingle_sim_hash(x.Text)
            })
            .First();

        using (new AssertionScope())
        {
            r.Farm.Should().BeNull();
            r.Metro.Should().BeNull();
            r.Java.Should().BeNull();
            r.Hive.Should().BeNull();
            r.Half.Should().BeNull();
            r.Wy.Should().BeNull();
            r.Int32.Should().NotBeNull("the number column of row 2 is the non-null 0");
            r.NgramSim.Should().BeNull();
            r.WordSim.Should().BeNull();
        }
    }

    [Fact]
    public void Refusals_ShouldNeverReachTheServer()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        using (new AssertionScope())
        {
            FluentActions.Invoking(() => ctx.From<IT286bHashEntity>()
                    .Where(x => x.Id == 1)
                    .Select(x => new { V = SqlFunctions.ClickHouse.ngram_sim_hash_arg(x.TextNonNull) })
                    .First())
                .Should().Throw<NotSupportedException>()
                .WithMessage("*ngramSimHashArg*ngramMinHashArg*UNKNOWN_FUNCTION*");

            FluentActions.Invoking(() => ctx.From<IT286bHashEntity>()
                    .Where(x => x.Id == 1)
                    .Select(x => new { V = SqlFunctions.ClickHouse.word_shingle_sim_hash_arg(x.TextNonNull) })
                    .First())
                .Should().Throw<NotSupportedException>()
                .WithMessage("*wordShingleSimHashArg*wordShingleMinHashArg*UNKNOWN_FUNCTION*");
        }

        // The pinned server proves both absences: the names are missing from system.functions and the
        // engine rejects them with UNKNOWN_FUNCTION. The delivered set is present exactly once each.
        Scalar<ulong>("(select count() from system.functions where name in "
                      + "('ngramSimHashArg', 'wordShingleSimHashArg'))").Should().Be(0UL);
        Scalar<ulong>("(select count() from system.functions where name in "
                      + "('SHA224', 'SHA384', 'SHA512_256', 'BLAKE3', 'MD4', 'RIPEMD160', 'keccak256', "
                      + "'farmHash64', 'metroHash64', 'javaHash', 'hiveHash', 'halfMD5', 'URLHash', "
                      + "'wyHash64', 'intHash32', 'intHash64', 'ngramMinHash', 'ngramMinHashArg', "
                      + "'ngramSimHash', 'wordShingleMinHash', 'wordShingleMinHashArg', "
                      + "'wordShingleSimHash'))").Should().Be(22UL);

        FluentActions.Invoking(() =>
            {
                using var result = ctx.ExecuteRaw("select ngramSimHashArg('ClickHouse')");
                result.Read<Tuple<ulong, ulong>>().Single();
            })
            .Should().Throw<ClickHouseServerException>().WithMessage("*UNKNOWN_FUNCTION*");

        FluentActions.Invoking(() =>
            {
                using var result = ctx.ExecuteRaw("select wordShingleSimHashArg('ClickHouse')");
                result.Read<Tuple<ulong, ulong>>().Single();
            })
            .Should().Throw<ClickHouseServerException>().WithMessage("*UNKNOWN_FUNCTION*");
    }

    [Fact]
    public void Projection_ShouldMaterialiseEveryDeliveredMember()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bHashEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
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
                Url = SqlFunctions.ClickHouse.url_hash(x.Url, 1),
                Wy = SqlFunctions.ClickHouse.wy_hash64(x.Text),
                I32 = SqlFunctions.ClickHouse.int_hash32(x.Number),
                I64 = SqlFunctions.ClickHouse.int_hash64(x.Number),
                Nm = SqlFunctions.ClickHouse.ngram_min_hash(x.TextNonNull, 3),
                Na = SqlFunctions.ClickHouse.ngram_min_hash_arg(x.TextNonNull, 3),
                Ns = SqlFunctions.ClickHouse.ngram_sim_hash(x.TextNonNull, 3),
                Wm = SqlFunctions.ClickHouse.word_shingle_min_hash(x.TextNonNull, 3),
                Wa = SqlFunctions.ClickHouse.word_shingle_min_hash_arg(x.TextNonNull, 3),
                Ws = SqlFunctions.ClickHouse.word_shingle_sim_hash(x.TextNonNull, 3)
            })
            .First();

        using (new AssertionScope())
        {
            r.S224.Should().Be("23097d223405d8228642a477bda255b32aadbce4bda0b3f7e36c9da7");
            r.M4.Should().Be("a448017aaf21d8525fc10ae87aa6729d");
            r.Farm.Should().Be(2640714258260161385UL);
            r.Java.Should().Be(96354);
            r.Half.Should().Be(10376663631224000432UL);
            r.Url.Should().Be(11160318154034397263UL);
            r.I32.Should().Be(1228623923U);
            r.Nm.Should().Be(Tuple.Create(14538029575209348823UL, 14538029575209348823UL));
            r.Na.Item1.Item1.Should().Be("abc");
            r.Na.Item2.Item1.Should().Be("abc");
            r.Ns.Should().Be(3384066120UL);
            r.Wm.Should().Be(Tuple.Create(0UL, 0UL), "'abc' has no four-character word");
            r.Wa.Item1.Should().Be(Tuple.Create("", "", "", "", "", ""));
            r.Ws.Should().Be(0UL);
        }
    }
}
