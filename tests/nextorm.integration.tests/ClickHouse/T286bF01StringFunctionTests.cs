using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T286b D1 (F01 String): live execution of the ClickHouse-only string functions <c>overlay</c>,
/// <c>initcap</c>, <c>soundex</c>, the Base64 and hex pairs, <c>editDistance</c>/
/// <c>jaroSimilarity</c>/<c>byteHammingDistance</c>, <c>substringIndex</c>, <c>countSubstrings</c>,
/// <c>extractGroups</c> and the four <c>normalizeUTF8*</c> forms against the pinned
/// <c>25.8-alpine</c> server. Each member is executed at least once through the real translation
/// path; nullability, empty/Unicode/binary boundaries and the server-side malformed-input refusals
/// are asserted rather than substituted.
/// </summary>
public sealed class T286bF01StringFunctionTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private T Scalar<T>(string expression)
    {
        using var context = Provider.CreateContext();
        using var result = context.ExecuteRaw($"select {expression} as v");
        return result.Read<T>().Single();
    }

    [Fact]
    public void StringFunctions_ShouldExecuteAgainstTheLiveServer()
    {
        using (new AssertionScope())
        {
            // overlay: 1-based offset, replacement-length default, explicit length, insertion and append.
            Scalar<string>("overlay('ClickHouse', '***', 6)").Should().Be("Click***se");
            Scalar<string>("overlay('abcdef', 'XY', 2, 3)").Should().Be("aXYef");
            Scalar<string>("overlay('abcdef', 'XY', 2, 1)").Should().Be("aXYcdef");
            Scalar<string>("overlay('abcdef', 'XY', 2, 0)").Should().Be("aXYbcdef", "a zero length inserts");
            Scalar<string>("overlay('abcdef', 'XY', 100)").Should().Be("abcdefXY", "an offset past the end appends");

            // initcap: ASCII words are title-cased; non-ASCII letters keep their case.
            Scalar<string>("initcap('hello world')").Should().Be("Hello World");
            Scalar<string>("initcap('hello-world_foo')").Should().Be("Hello-World_Foo");
            Scalar<string>("initcap('ПРИВЕТ МИР')").Should().Be("ПРИВЕТ МИР", "the case mapping is ASCII-only");

            // soundex: stable four-character codes; input without an ASCII letter maps to 0000.
            Scalar<string>("soundex('Robert')").Should().Be("R163");
            Scalar<string>("soundex('Rupert')").Should().Be("R163");
            Scalar<string>("soundex('dadfasd')").Should().Be("D312");

            // Base64 and hex pairs: text round-trips through the native spellings.
            Scalar<string>("base64Encode('abc')").Should().Be("YWJj");
            Scalar<string>("base64Decode('YWJj')").Should().Be("abc");
            Scalar<string>("base64Encode(base64Decode('YWJj'))").Should().Be("YWJj");
            Scalar<string>("hex('abc')").Should().Be("616263");
            Scalar<string>("unhex('616263')").Should().Be("abc");
            Scalar<string>("hex(unhex('616263'))").Should().Be("616263");
            Scalar<string>("hex(unhex('abc'))").Should().Be("0ABC", "an odd digit count is left-padded with a zero");

            // editDistance is measured over UTF-8 bytes; jaroSimilarity is a Float64 in [0, 1].
            Scalar<long>("editDistance('kitten', 'sitting')").Should().Be(3);
            Scalar<long>("editDistance('ёж', 'еж')").Should().Be(2, "a multi-byte character counts per byte");
            Scalar<double>("jaroSimilarity('kitten', 'sitting')").Should().BeApproximately(0.746031746031746, 1e-12);
            Scalar<double>("jaroSimilarity('a', 'a')").Should().Be(1d);
            Scalar<long>("byteHammingDistance('abc', 'abd')").Should().Be(1);
            Scalar<long>("byteHammingDistance('abc', '')").Should().Be(3, "the empty input is zero-padded");

            // substringIndex: positive counts from the left, negative from the right, zero is empty.
            Scalar<string>("substringIndex('www.clickhouse.com', '.', 2)").Should().Be("www.clickhouse");
            Scalar<string>("substringIndex('www.clickhouse.com', '.', -2)").Should().Be("clickhouse.com");
            Scalar<string>("substringIndex('www.clickhouse.com', '.', 0)").Should().Be("");
            Scalar<string>("substringIndex('dadfasd', 'd', 2)").Should().Be("da");

            // countSubstrings: non-overlapping occurrences, with and without the 1-based start.
            Scalar<long>("countSubstrings('Hello, world! Hello', 'Hello')").Should().Be(2);
            Scalar<long>("countSubstrings('aaaa', 'aa')").Should().Be(2, "the occurrences do not overlap");
            Scalar<long>("countSubstrings('abcabc', 'abc', 2)").Should().Be(1);
            Scalar<long>("countSubstrings('abcabc', 'abc', 7)").Should().Be(0, "a start past the end counts nothing");
            Scalar<long>("countSubstrings('dadfasd', 'd')").Should().Be(3);

            // extractGroups: the capture groups of the first RE2 match, an array result. The array is
            // read through the typed projection because ClickHouse cannot return a nullable array.
            var groups = _sut.SimpleEntity
                .Where(x => x.Id == 1)
                .Select(x => new
                {
                    Digits = SqlFunctions.ClickHouse.extract_groups("2020-01-01", "(\\\\d+)-(\\\\d+)-(\\\\d+)"),
                    Pair = SqlFunctions.ClickHouse.extract_groups("dadfasd", "(d)(a)"),
                    None = SqlFunctions.ClickHouse.extract_groups("no digits", "(\\\\d+)")
                })
                .First();
            groups.Digits.Should().Equal("2020", "01", "01");
            groups.Pair.Should().Equal("d", "a");
            groups.None.Should().BeEmpty("a non-match yields []");

            // normalizeUTF8*: the four documented Unicode forms.
            Scalar<string>("normalizeUTF8NFC('e' || char(204) || char(129))").Should().Be("é");
            Scalar<string>("hex(normalizeUTF8NFC('e' || char(204) || char(129)))").Should().Be("C3A9");
            Scalar<string>("hex(normalizeUTF8NFD('é'))").Should().Be("65CC81");
            Scalar<string>("hex(normalizeUTF8NFKC(char(239) || char(172) || char(128)))")
                .Should().Be("6666", "the ff ligature composes to two ASCII letters");
            Scalar<string>("hex(normalizeUTF8NFKD(char(239) || char(172) || char(128)))").Should().Be("6666");
        }
    }

    [Fact]
    public void NullArguments_ShouldProduceNullNeverAnEmptyOrDefaultValue()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                O = SqlFunctions.ClickHouse.overlay(null, "a", 1),
                O4 = SqlFunctions.ClickHouse.overlay("abc", "XY", 2, null),
                I = SqlFunctions.ClickHouse.initcap(null),
                S = SqlFunctions.ClickHouse.soundex(null),
                Be = SqlFunctions.ClickHouse.base64_encode(null),
                Bd = SqlFunctions.ClickHouse.base64_decode(null),
                H = SqlFunctions.ClickHouse.hex(null),
                U = SqlFunctions.ClickHouse.unhex(null),
                Ed = SqlFunctions.ClickHouse.edit_distance(null, "a"),
                Js = SqlFunctions.ClickHouse.jaro_similarity(null, "a"),
                Bh = SqlFunctions.ClickHouse.byte_hamming_distance(null, "a"),
                Si = SqlFunctions.ClickHouse.substring_index(null, ".", 2),
                Cs = SqlFunctions.ClickHouse.count_substrings(null, "a"),
                Nfc = SqlFunctions.ClickHouse.normalize_utf8_nfc(null),
                Nfd = SqlFunctions.ClickHouse.normalize_utf8_nfd(null),
                Nfkc = SqlFunctions.ClickHouse.normalize_utf8_nfkc(null),
                Nfkd = SqlFunctions.ClickHouse.normalize_utf8_nfkd(null)
            })
            .First();

        using (new AssertionScope())
        {
            r.O.Should().BeNull("overlay(NULL, ...) is NULL");
            r.O4.Should().Be("aXY", "a null optional length selects the 3-argument form, so two characters are replaced");
            r.I.Should().BeNull();
            r.S.Should().BeNull("soundex(NULL) is NULL, never 0000");
            r.Be.Should().BeNull();
            r.Bd.Should().BeNull();
            r.H.Should().BeNull();
            r.U.Should().BeNull();
            r.Ed.Should().BeNull("editDistance(NULL, ...) is NULL, never 0");
            r.Js.Should().BeNull();
            r.Bh.Should().BeNull();
            r.Si.Should().BeNull();
            r.Cs.Should().BeNull("countSubstrings(NULL, ...) is NULL, never 0");
            r.Nfc.Should().BeNull();
            r.Nfd.Should().BeNull();
            r.Nfkc.Should().BeNull();
            r.Nfkd.Should().BeNull();
        }
    }

    [Fact]
    public void EmptyAndMappedValues_ShouldKeepTheDeclaredClrShapes()
    {
        using (new AssertionScope())
        {
            // Empty values are data, not null.
            Scalar<string>("initcap('')").Should().Be("");
            Scalar<string>("base64Encode('')").Should().Be("");
            Scalar<string>("base64Decode('')").Should().Be("");
            Scalar<string>("hex('')").Should().Be("");
            Scalar<string>("unhex('')").Should().Be("");
            Scalar<string>("soundex('')").Should().Be("0000");
            Scalar<string>("substringIndex('abc', '.', 0)").Should().Be("");
            Scalar<long>("editDistance('', '')").Should().Be(0);
            Scalar<double>("jaroSimilarity('', '')").Should().Be(0d);
            Scalar<long>("byteHammingDistance('', '')").Should().Be(0);
            Scalar<long>("countSubstrings('abc', 'x')").Should().Be(0);
            Scalar<long>("countSubstrings('abc', '')").Should().Be(0);
            Scalar<string>("normalizeUTF8NFC('')").Should().Be("");
            Scalar<string>("normalizeUTF8NFD('')").Should().Be("");
            Scalar<string>("normalizeUTF8NFKC('')").Should().Be("");
            Scalar<string>("normalizeUTF8NFKD('')").Should().Be("");
        }

        // The mapped column path materialises the declared CLR shapes: string, long?, double? and
        // string[] for the array-returning extractGroups (never a substituted default).
        var row = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Cap = SqlFunctions.ClickHouse.initcap(x.String),
                Sound = SqlFunctions.ClickHouse.soundex(x.String),
                Hex = SqlFunctions.ClickHouse.hex(x.String),
                B64 = SqlFunctions.ClickHouse.base64_encode(x.String),
                RoundTrip = SqlFunctions.ClickHouse.base64_decode(SqlFunctions.ClickHouse.base64_encode(x.String)),
                UnhexRoundTrip = SqlFunctions.ClickHouse.unhex(SqlFunctions.ClickHouse.hex(x.String)),
                Overlaid = SqlFunctions.ClickHouse.overlay(x.String, "zz", 2, 2),
                Ed = SqlFunctions.ClickHouse.edit_distance(x.String, "dadfasd"),
                Js = SqlFunctions.ClickHouse.jaro_similarity(x.String, "dadfasd"),
                Bh = SqlFunctions.ClickHouse.byte_hamming_distance(x.String, "dadfasd"),
                Si = SqlFunctions.ClickHouse.substring_index(x.String, "d", 2),
                Cs = SqlFunctions.ClickHouse.count_substrings(x.String, "d"),
                Eg = SqlFunctions.ClickHouse.extract_groups(x.RequiredString, "(s)(d)"),
                Nfc = SqlFunctions.ClickHouse.normalize_utf8_nfc(x.String),
                Nfd = SqlFunctions.ClickHouse.normalize_utf8_nfd(x.String)
            })
            .First();

        using (new AssertionScope())
        {
            row.Cap.Should().Be("Dadfasd");
            row.Sound.Should().Be("D312");
            row.Hex.Should().Be("64616466617364");
            row.B64.Should().Be("ZGFkZmFzZA==");
            row.RoundTrip.Should().Be("dadfasd");
            row.UnhexRoundTrip.Should().Be("dadfasd");
            row.Overlaid.Should().Be("dzzfasd");
            row.Ed.Should().Be(0L);
            row.Js.Should().Be(1d);
            row.Bh.Should().Be(0L);
            row.Si.Should().Be("da");
            row.Cs.Should().Be(3L);
            row.Eg.Should().Equal("s", "d");
            row.Nfc.Should().Be("dadfasd");
            row.Nfd.Should().Be("dadfasd");
        }

        // The nullable column keeps NULL through every member instead of a default.
        var nullRow = _sut.ComplexEntity
            .Where(x => x.Id == 3)
            .Select(x => new
            {
                Cap = SqlFunctions.ClickHouse.initcap(x.String),
                Hex = SqlFunctions.ClickHouse.hex(x.String),
                Ed = SqlFunctions.ClickHouse.edit_distance(x.String, "a"),
                Js = SqlFunctions.ClickHouse.jaro_similarity(x.String, "a"),
                Cs = SqlFunctions.ClickHouse.count_substrings(x.String, "a"),
                Nfc = SqlFunctions.ClickHouse.normalize_utf8_nfc(x.String)
            })
            .First();

        using (new AssertionScope())
        {
            nullRow.Cap.Should().BeNull();
            nullRow.Hex.Should().BeNull();
            nullRow.Ed.Should().BeNull();
            nullRow.Js.Should().BeNull();
            nullRow.Cs.Should().BeNull();
            nullRow.Nfc.Should().BeNull();
        }

        // ClickHouse cannot materialise a Nullable(Array(String)): extractGroups over a nullable
        // column is refused by the server with Code 43 instead of silently yielding an empty array.
        FluentActions.Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 1)
                .Select(x => new { Eg = SqlFunctions.ClickHouse.extract_groups(x.String, "(a)") })
                .First())
            .Should().Throw<Exception>()
            .WithMessage("*Array(String) cannot be inside Nullable*",
                "a nullable-array result is not representable and must not be substituted with []");
    }

    [Fact]
    public void NullLiteralsAndPredicates_ShouldTranslateAndExecute()
    {
        // A null optional argument is emitted as the shorter form, and the functions stay predicates.
        var r = _sut.ComplexEntity
            .Where(x => SqlFunctions.ClickHouse.edit_distance(x.String, "dadfasd") == 0
                && SqlFunctions.ClickHouse.count_substrings(x.String, "d") == 3)
            .Select(x => new
            {
                Id = x.Id,
                O = SqlFunctions.ClickHouse.overlay("abcdef", "XY", 2, null),
                Cs = SqlFunctions.ClickHouse.count_substrings("abcabc", "abc", null)
            })
            .ToList();

        r.Should().ContainSingle("only row 1 has the string 'dadfasd'");
        r[0].Id.Should().Be(1L);
        r[0].O.Should().Be("aXYdef", "a null length is the 3-argument overlay, whose default length is the replacement length");
        r[0].Cs.Should().Be(2L, "a null start is the 2-argument countSubstrings");
    }

    [Fact]
    public void MalformedInput_ShouldBeRejectedByTheServer()
    {
        using var context = Provider.CreateContext();

        FluentActions.Invoking(() => context.ExecuteRaw("select base64Decode('!!!') as v").Read<string>().ToList())
            .Should().Throw<Exception>()
            .WithMessage("*base64Decode*", "invalid Base64 is a server-side data error, not a silent default");

        FluentActions.Invoking(() => context.ExecuteRaw("select substringIndex('abc', 'ab', 1) as v").Read<string>().ToList())
            .Should().Throw<Exception>()
            .WithMessage("*single character*", "the delimiter must be one character");

        FluentActions.Invoking(() => context.ExecuteRaw("select extractGroups('abc', '') as v").Read<string[]>().ToList())
            .Should().Throw<Exception>()
            .WithMessage("*needle*", "an empty extraction pattern is rejected");

        FluentActions.Invoking(() => context.ExecuteRaw("select countSubstrings('abcabc', 'abc', -1) as v").Read<long>().ToList())
            .Should().Throw<Exception>()
            .WithMessage("*Illegal type*", "a negative start is not an unsigned position");
    }
}
