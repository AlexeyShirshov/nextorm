using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Executes the cross-provider scalar functions against ClickHouse. ClickHouse does not derive the
/// shared <see cref="CommonTestSuite"/> (its divergences live in <see cref="ClickHouseIntegrationTests"/>),
/// so the same shared assertions are pinned here for the container-backed provider.
/// The dialect's scalar-function table is also swept through its public surface
/// (<see cref="ClickHouseDialect.ScalarFunctions"/>): every accepted name, every render arm, the
/// negative/edge contracts, and a live execution per function family.
/// </summary>
public sealed class ClickHouseScalarFunctionsTests : ProviderTestSuite
{
    private static readonly string[] SupportedNames =
    [
        "left", "right", "lpad", "rpad", "repeat", "reverse", "space",
        "concat_ws", "translate", "ascii", "char",
        "lower_utf8", "upper_utf8", "trim_left", "trim_right", "trim_both",
        "replace_regexp_one", "replace_regexp_all", "match", "extract", "extract_all",
        "split_by_string", "split_by_regexp", "split_by_whitespace",
        "format_date_time", "parse_date_time", "parse_date_time_best_effort",
        "now", "today", "yesterday",
        "array_concat", "array_flatten", "array_uniq", "array_intersect",
        "array_union", "array_except", "array_symmetric_difference",
        "map", "map_keys", "map_values", "map_contains_key", "map_contains_value",
        "map_add", "map_concat", "map_filter", "map_apply", "map_all", "map_exists",
        "map_sort",
        "group_bitmap", "group_bitmap_and", "group_bitmap_or", "group_bitmap_xor",
        "sum_map", "sum_map_filtered",
        "md5", "sha1", "sha256", "sha512",
        "xx_hash32", "xx_hash64", "xxh3", "city_hash64",
        "sip_hash64", "sip_hash128",
        "murmur_hash2_32", "murmur_hash2_64", "murmur_hash3_32", "murmur_hash3_64",
        "murmur_hash3_128", "generate_ulid",
        "bit_length", "octet_length", "degrees", "radians", "pi"
    ];

    // Every Render arm of ClickHouseScalarFunctions, written out literally from the dialect contract
    // (src/nextorm.clickhouse/ClickHouseDialect.cs): one row per name, plus a second row for the
    // optional pad form of lpad/rpad. 76 names -> 78 arms.
    private static readonly (string Name, string[] Args, string Expected)[] RenderArms =
    [
        ("left", ["s", "2"], "leftUTF8(s, 2)"),
        ("right", ["s", "2"], "rightUTF8(s, 2)"),
        ("lpad", ["s", "3", "'0'"], "leftPadUTF8(s, 3, '0')"),
        ("lpad", ["s", "3"], "leftPadUTF8(s, 3, ' ')"),
        ("rpad", ["s", "3", "'0'"], "rightPadUTF8(s, 3, '0')"),
        ("rpad", ["s", "3"], "rightPadUTF8(s, 3, ' ')"),
        ("repeat", ["s", "2"], "repeat(s, 2)"),
        ("reverse", ["s"], "reverseUTF8(s)"),
        ("space", ["2"], "space(2)"),
        ("concat_ws", ["sep", "a", "b"], "concatWithSeparator(sep, a, b)"),
        ("translate", ["s", "from", "to"], "translate(s, from, to)"),
        ("ascii", ["s"], "ascii(s)"),
        ("char", ["65"], "char(65)"),
        ("lower_utf8", ["s"], "lowerUTF8(s)"),
        ("upper_utf8", ["s"], "upperUTF8(s)"),
        ("trim_left", ["s"], "trimLeft(s)"),
        ("trim_right", ["s"], "trimRight(s)"),
        ("trim_both", ["s"], "trimBoth(s)"),
        ("replace_regexp_one", ["s", "pat", "rep"], "replaceRegexpOne(s, pat, rep)"),
        ("replace_regexp_all", ["s", "pat", "rep"], "replaceRegexpAll(s, pat, rep)"),
        ("match", ["s", "pat"], "match(s, pat)"),
        ("extract", ["s", "pat"], "extract(s, pat)"),
        ("extract_all", ["s", "pat"], "extractAll(s, pat)"),
        ("split_by_string", ["sep", "s"], "splitByString(sep, s)"),
        ("split_by_regexp", ["pat", "s"], "splitByRegexp(pat, s)"),
        ("split_by_whitespace", ["s"], "splitByWhitespace(s)"),
        ("format_date_time", ["dt", "fmt"], "formatDateTime(dt, fmt)"),
        ("parse_date_time", ["s", "fmt"], "parseDateTime(s, fmt)"),
        ("parse_date_time_best_effort", ["s"], "parseDateTimeBestEffort(s)"),
        ("now", [], "now()"),
        ("today", [], "today()"),
        ("yesterday", [], "yesterday()"),
        ("array_concat", ["a", "b"], "arrayConcat(a, b)"),
        ("array_flatten", ["a"], "arrayFlatten(a)"),
        ("array_uniq", ["a"], "toInt64(arrayUniq(a))"),
        ("array_intersect", ["a", "b"], "arrayIntersect(a, b)"),
        ("array_union", ["a", "b"], "arrayUnion(a, b)"),
        ("array_except", ["a", "b"], "arrayExcept(a, b)"),
        ("array_symmetric_difference", ["a", "b"], "arraySymmetricDifference(a, b)"),
        ("map", ["k", "v"], "map(k, v)"),
        ("map_keys", ["m"], "mapKeys(m)"),
        ("map_values", ["m"], "mapValues(m)"),
        ("map_contains_key", ["m", "k"], "mapContainsKey(m, k)"),
        ("map_contains_value", ["m", "v"], "mapContainsValue(m, v)"),
        ("map_add", ["a", "b"], "mapAdd(a, b)"),
        ("map_concat", ["a", "b", "c"], "mapConcat(a, b, c)"),
        ("map_filter", ["m", "f"], "mapFilter(m, f)"),
        ("map_apply", ["m", "f"], "mapApply(m, f)"),
        ("map_all", ["m", "f"], "mapAll(m, f)"),
        ("map_exists", ["m", "f"], "mapExists(m, f)"),
        ("map_sort", ["m"], "mapSort(m)"),
        ("group_bitmap", ["a"], "groupBitmap(a)"),
        ("group_bitmap_and", ["a"], "groupBitmapAnd(a)"),
        ("group_bitmap_or", ["a"], "groupBitmapOr(a)"),
        ("group_bitmap_xor", ["a"], "groupBitmapXor(a)"),
        ("sum_map", ["k", "v"], "sumMap(k, v)"),
        ("sum_map_filtered", ["f", "k", "v"], "sumMapFiltered(f)(k, v)"),
        ("md5", ["s"], "MD5(s)"),
        ("sha1", ["s"], "SHA1(s)"),
        ("sha256", ["s"], "SHA256(s)"),
        ("sha512", ["s"], "SHA512(s)"),
        ("xx_hash32", ["s"], "toInt32(xxHash32(s))"),
        ("xx_hash64", ["s"], "toInt64(xxHash64(s))"),
        ("xxh3", ["s"], "toInt64(xxh3(s))"),
        ("city_hash64", ["s"], "toInt64(cityHash64(s))"),
        ("sip_hash64", ["s"], "toInt64(sipHash64(s))"),
        ("sip_hash128", ["s"], "sipHash128(s)"),
        ("murmur_hash2_32", ["s"], "toInt32(murmurHash2_32(s))"),
        ("murmur_hash2_64", ["s"], "toInt64(murmurHash2_64(s))"),
        ("murmur_hash3_32", ["s"], "toInt32(murmurHash3_32(s))"),
        ("murmur_hash3_64", ["s"], "toInt64(murmurHash3_64(s))"),
        ("murmur_hash3_128", ["s"], "murmurHash3_128(s)"),
        ("generate_ulid", [], "generateULID()"),
        ("bit_length", ["s"], "length(s) * 8"),
        ("octet_length", ["s"], "length(s)"),
        ("degrees", ["x"], "degrees(x)"),
        ("radians", ["x"], "radians(x)"),
        ("pi", [], "pi()")
    ];

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private static IScalarFunctions Functions => ClickHouseDialect.Instance.ScalarFunctions;

    private static string Sql(string name, params string[] args) => Functions.Render(name, args);

    [Fact]
    public void Supports_ShouldAcceptTheCompleteInventoryAndNothingElse()
    {
        SupportedNames.Should().HaveCount(76, "the ClickHouse table accepts 76 scalar-function names");
        SupportedNames.Should().OnlyHaveUniqueItems();

        using (new AssertionScope())
        {
            foreach (var name in SupportedNames)
                Functions.Supports(name).Should().BeTrue("'{0}' is part of the ClickHouse inventory", name);
        }

        // The render table and the predicate must agree name-for-name: no accepted name without an
        // arm, no arm for a name the dialect does not accept.
        RenderArms.Select(arm => arm.Name).Distinct().Should().BeEquivalentTo(SupportedNames);
        RenderArms.Should().HaveCount(78, "76 names plus the two optional pad forms of lpad/rpad");
    }

    [Fact]
    public void Render_ShouldEmitTheContractSqlForEveryArm()
    {
        using (new AssertionScope())
        {
            foreach (var (name, args, expected) in RenderArms)
                Sql(name, args).Should().Be(
                    expected, "'{0}({1})' is the ClickHouse arm of that name", name, string.Join(", ", args));
        }
    }

    [Fact]
    public void Supports_ShouldRejectUnknownNamesEmptyAndCaseVariants()
    {
        Functions.Supports("bogus").Should().BeFalse("the name is not in the table");
        Functions.Supports("").Should().BeFalse("the empty name is not in the table");
        Functions.Supports(null!).Should().BeFalse("the name pattern never matches a null name");
        Functions.Supports("ABS").Should().BeFalse("names are matched case-sensitively against the CLR names");
    }

    [Fact]
    public void Render_ShouldThrowNotSupportedForUnknownNamesEmptyAndCaseVariants()
    {
        FluentActions.Invoking(() => Sql("bogus", ["x"]))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The bogus function is not supported by ClickHouse.");

        FluentActions.Invoking(() => Sql("", []))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The  function is not supported by ClickHouse.");

        FluentActions.Invoking(() => Sql("ABS", ["x"]))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The ABS function is not supported by ClickHouse.");
    }

    [Fact]
    public void Render_ShouldSurfaceAMissingArgumentWithoutArityValidation()
    {
        // The table indexes its arguments directly and has no arity guard: a short argument list fails
        // on the list indexer of the missing position (the array-backed argument list surfaces it as an
        // argument-out-of-range failure).
        FluentActions.Invoking(() => Sql("translate", new[] { "a", "b" }))
            .Should().Throw<ArgumentOutOfRangeException>("translate reads args[2] and only two were supplied");

        FluentActions.Invoking(() => Sql("bit_length", new[] { "a", "b" }))
            .Should().NotThrow("bit_length reads args[0] and ignores a surplus argument");
    }

    [Fact]
    public void Render_ShouldIgnoreSurplusArgumentsOfFixedArityArms()
    {
        Sql("now", ["x"]).Should().Be("now()", "zero-argument arms never read the list");
        Sql("pi", ["x", "y"]).Should().Be("pi()", "zero-argument arms never read the list");
    }

    [Fact]
    public void Render_ShouldRenderANullArgumentElementAsAnEmptyPosition()
    {
        // The contract has no null guard on the argument elements: they are interpolated into the call,
        // so a null element leaves an empty position rather than throwing.
        Sql("left", [(string)null!, "2"]).Should().Be("leftUTF8(, 2)");
    }

    [Fact]
    public void ScalarFunctions_ShouldExecuteAgainstTheLiveServer()
    {
        using var context = Provider.CreateContext();

        T Scalar<T>(string expression)
        {
            using var result = context.ExecuteRaw($"select {expression} as v");
            return result.Read<T>().Single();
        }

        // Numeric family: pi()/degrees()/radians().
        Scalar<double>(Sql("degrees", Sql("pi", [])))
            .Should().BeApproximately(180d, 1e-9, "degrees(pi()) is 180 (proves degrees + pi)");
        Scalar<double>(Sql("radians", "180"))
            .Should().BeApproximately(Math.PI, 1e-12, "radians(180) is pi");

        // String family: left/right are UTF-8 character based; pads, repeat, reverse, space, concat_ws,
        // translate, case mapping, trims, regexp replace/extract.
        Scalar<string>(Sql("left", "'abcdef'", "3")).Should().Be("abc", "left -> leftUTF8");
        Scalar<string>(Sql("right", "'Привет'", "3")).Should().Be("вет", "right counts characters, not bytes");
        Scalar<string>(Sql("lpad", "'7'", "3", "'0'")).Should().Be("007", "the three-argument lpad arm");
        Scalar<string>(Sql("rpad", "'7'", "3")).Should().Be("7  ", "the two-argument rpad arm pads with a space");
        Scalar<string>(Sql("repeat", "'ab'", "2")).Should().Be("abab", "repeat");
        Scalar<string>(Sql("reverse", "'abc'")).Should().Be("cba", "reverse -> reverseUTF8");
        Scalar<string>(Sql("space", "2")).Should().Be("  ", "space");
        Scalar<string>(Sql("concat_ws", "'-'", "'a'", "'b'", "'c'")).Should().Be("a-b-c", "concat_ws -> concatWithSeparator");
        Scalar<string>(Sql("translate", "'abc'", "'ab'", "'xy'")).Should().Be("xyc", "translate");
        Scalar<string>(Sql("lower_utf8", "'ПРИВЕТ'")).Should().Be("привет", "lower_utf8 -> lowerUTF8");
        Scalar<string>(Sql("upper_utf8", "'привет'")).Should().Be("ПРИВЕТ", "upper_utf8 -> upperUTF8");
        Scalar<string>(Sql("trim_both", "'  x  '")).Should().Be("x", "trim_both -> trimBoth");
        Scalar<string>(Sql("trim_left", "'  x'")).Should().Be("x", "trim_left -> trimLeft");
        Scalar<string>(Sql("trim_right", "'x  '")).Should().Be("x", "trim_right -> trimRight");
        Scalar<string>(Sql("replace_regexp_one", "'a1b2'", "'[0-9]'", "'#'")).Should().Be("a#b2", "replace_regexp_one replaces the first match");
        Scalar<string>(Sql("replace_regexp_all", "'a1b2'", "'[0-9]'", "'#'")).Should().Be("a#b#", "replace_regexp_all replaces every match");
        Scalar<string>(Sql("extract", "'abc123'", "'[0-9]+'")).Should().Be("123", "extract returns the first match");

        // Date/time family: parse + format round-trip, and the best-effort parser.
        Scalar<string>(Sql(
                "format_date_time",
                Sql("parse_date_time", "'2024-01-02 03:04:05'", "'%Y-%m-%d %H:%i:%S'"),
                "'%Y-%m-%d'"))
            .Should().Be("2024-01-02", "parseDateTime + formatDateTime (proves both arms)");
        Scalar<string>(Sql(
                "format_date_time", Sql("parse_date_time_best_effort", "'2024-01-02'"), "'%Y-%m-%d'"))
            .Should().Be("2024-01-02", "parseDateTimeBestEffort");

        // Array family: the distinct-count arm casts UInt64 to Int64; the concat arm feeds it two arrays.
        Scalar<long>(Sql(
                "array_uniq", Sql("array_concat", "[1, 2]", "[2, 3]")))
            .Should().Be(3, "arrayUniq(arrayConcat(...)) counts distinct values (proves both arms)");

        // Conditional/predicate family: match and mapContainsKey return UInt8 truth values.
        Scalar<int>(Sql("match", "'abcdef'", "'^abc'")).Should().Be(1, "match is a predicate");
        Scalar<int>(Sql("map_contains_key", Sql("map", "'a'", "1", "'b'", "2"), "'a'"))
            .Should().Be(1, "mapContainsKey is a predicate over the map arm");

        // Map family: mapKeys feeds the byte-counting length arm.
        Scalar<int>(Sql("octet_length", Sql("map_keys", Sql("map", "'a'", "1", "'b'", "2"))))
            .Should().Be(2, "mapKeys(map(...)) has two keys");

        // Bit family: bit_length is eight times the byte length; octet_length is the byte length.
        Scalar<int>(Sql("bit_length", "'abc'")).Should().Be(24, "bit_length is length * 8");
        Scalar<int>(Sql("octet_length", "'Привет'")).Should().Be(12, "octet_length counts UTF-8 bytes");

        // Type-conversion family: ascii maps a character to its code point, char maps it back.
        Scalar<int>(Sql("ascii", "'A'")).Should().Be(65, "ascii returns the code point");
        Scalar<string>(Sql("char", "65")).Should().Be("A", "char returns the character");

        // Hash/uuid family: the digests are the documented fixed binary strings, so the test reads them
        // back as hex; generateULID is a 26-character Crockford ULID.
        Scalar<string>($"hex({Sql("md5", "'abc'")})").Should().Be(
            "900150983CD24FB0D6963F7D28E17F72", "MD5('abc') is the 16-byte digest 900150983cd24fb0d6963f7d28e17f72");
        Scalar<string>($"hex({Sql("sha256", "'abc'")})").Should().Be(
            "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD",
            "SHA256('abc') is the 32-byte digest ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
        Scalar<string>(Sql("generate_ulid", []))
            .Should().MatchRegex("^[0-9A-HJKMNP-TV-Z]{26}$", "generateULID produces a 26-character ULID");
    }

    [Fact]
    public void ScalarFunctions_ShouldCompute()
    {
        var cyrillicLeft = "Привет";
        var cyrillicRight = "Привет";

        var r = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => new
            {
                L = SqlFunctions.Sql.left("abcdef", 3),
                R = SqlFunctions.Sql.right("abcdef", 3),
                Lru = SqlFunctions.Sql.left(cyrillicLeft, 3),
                Rru = SqlFunctions.Sql.right(cyrillicRight, 3),
                Lp = SqlFunctions.Sql.lpad("7", 3, "0"),
                Rp = SqlFunctions.Sql.rpad("7", 3, "0"),
                Rep = SqlFunctions.Sql.repeat("ab", 2),
                Rev = SqlFunctions.Sql.reverse("abc"),
                Sp = SqlFunctions.Sql.space(2),
                Cw = SqlFunctions.Sql.concat_ws("-", "a", "b", "c"),
                Tr = SqlFunctions.Sql.translate("abc", "ab", "xy"),
                Asc = SqlFunctions.Sql.ascii("A"),
                Ch = SqlFunctions.Sql.@char(65)
            })
            .First();

        r.L.Should().Be("abc");
        r.R.Should().Be("def");
        r.Lru.Should().Be("При");
        r.Rru.Should().Be("вет");
        r.Lp.Should().Be("007");
        r.Rp.Should().Be("700");
        r.Rep.Should().Be("abab");
        r.Rev.Should().Be("cba");
        r.Sp.Should().Be("  ");
        r.Cw.Should().Be("a-b-c");
        r.Tr.Should().Be("xyc");
        r.Asc.Should().Be(65);
        r.Ch.Should().Be("A");
    }

    [Fact]
    public void ScalarFunctions_ConcatWsNull_ShouldReturnNull()
    {
        // ClickHouse's concatWithSeparator returns NULL when any argument is NULL, unlike the other
        // providers' concat_ws which skips null arguments.
        var r = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => new { V = SqlFunctions.Sql.concat_ws("-", "a", null, "b") })
            .First();

        r.V.Should().BeNull();
    }

    /// <summary>
    /// Deferred ledger row <c>C8-D3-GROUPING-SETS-WITH-TOTALS</c>: the only public chain that reaches
    /// the <c>GROUPING SETS</c> + <c>WITH TOTALS</c> guard of
    /// <c>src/nextorm.core/DataContext/SqlBuilder.cs:275-276</c> — ClickHouse reports both
    /// <c>SupportsGroupingSets</c> (<c>ClickHouseDialect.cs:424</c>) and <c>SupportsGroupByWithTotals</c>,
    /// and <c>WithTotals</c> is its own public extension
    /// (<c>ClickHouseEntityBuilderExtensions.cs:97</c>). The grouping-set half executes live over the
    /// seeded three-row table (the <c>(tinyval)</c> group and the empty-set grand total); adding
    /// <c>WITH TOTALS</c> is rejected with the documented message. The engine itself rejects the
    /// general multi-set form with <c>NOT_IMPLEMENTED</c> ("WITH TOTALS and GROUPING SETS are not
    /// supported together"), so the guard mirrors the server and is not a product defect.
    /// </summary>
    [Fact]
    public void GroupingSets_WithTotals_ShouldBeRejectedByTheDocumentedCombinationGuard()
    {
        var groupingSets = _sut.ComplexEntity
            .GroupByGroupingSets(x => new { x.TinyInt }, [0], [])
            .Select(g => new { g.TinyInt, Count = SqlFunctions.Sql.count() })
            .ToList();

        groupingSets.Should().HaveCount(2, "one (tinyval) group plus the empty-set grand total");
        groupingSets.Select(r => (r.TinyInt, r.Count)).Should().BeEquivalentTo(
            new[] { ((byte)0, 3), ((byte)2, 3) },
            "all three seeded rows share tinyval 2, so the (tinyval) set counts three and the empty set "
            + "repeats the count over the default key");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .GroupByGroupingSets(x => new { x.TinyInt }, [0], [])
                .WithTotals()
                .Select(g => SqlFunctions.Sql.count())
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("GROUP BY ... WITH TOTALS cannot be combined with GROUPING SETS.");

        // Server-level evidence that the guard mirrors the engine: ClickHouse itself rejects the
        // general multi-set form with NOT_IMPLEMENTED. If the engine ever learns the combination this
        // fact fails, so the guard is revisited instead of silently refusing a supported shape.
        using var context = Provider.CreateContext();
        FluentActions.Invoking(() => context.ExecuteRaw(
                "select number % 2 as a, number % 3 as b, count() as c from numbers(6) "
                + "group by grouping sets ((a), (b)) with totals").Read<int>().ToList())
            .Should().Throw<Exception>()
            .WithMessage("*WITH TOTALS and GROUPING SETS are not supported together*");
    }
}
