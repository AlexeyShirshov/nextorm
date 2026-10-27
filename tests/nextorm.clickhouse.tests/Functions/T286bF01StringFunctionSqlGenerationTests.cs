using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.SqlServer;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// T286b D1 (F01 String): the ClickHouse-only string functions <c>overlay</c>, <c>initcap</c>,
/// <c>soundex</c>, the Base64 and hex pairs, <c>editDistance</c>/<c>jaroSimilarity</c>/
/// <c>byteHammingDistance</c>, <c>substringIndex</c>, <c>countSubstrings</c>, <c>extractGroups</c> and
/// the four <c>normalizeUTF8*</c> forms. Every member is exposed through
/// <see cref="ClickHouseFunctions"/> only, so the tests pin the exact <see cref="IScalarFunctions"/>
/// arm, the optional trailing arguments, the projection and predicate placement, the declared CLR
/// shapes and the real refusal of the other dialects. These tests never open a connection.
/// </summary>
public class T286bF01StringFunctionSqlGenerationTests
{
    private static readonly IScalarFunctions ClickHouseScalars = ClickHouseDialect.Instance.ScalarFunctions!;

    /// <summary>One row per render arm: the 17 names plus the optional-argument forms.</summary>
    private static readonly (string Name, string[] Args, string Expected)[] Arms =
    [
        ("overlay", ["s", "'XY'", "2"], "overlay(s, 'XY', 2)"),
        ("overlay", ["s", "'XY'", "2", "3"], "overlay(s, 'XY', 2, 3)"),
        ("initcap", ["s"], "initcap(s)"),
        ("soundex", ["s"], "soundex(s)"),
        ("base64_encode", ["s"], "base64Encode(s)"),
        ("base64_decode", ["s"], "base64Decode(s)"),
        ("hex", ["s"], "hex(s)"),
        ("unhex", ["s"], "unhex(s)"),
        ("edit_distance", ["a", "b"], "toInt64(editDistance(a, b))"),
        ("jaro_similarity", ["a", "b"], "jaroSimilarity(a, b)"),
        ("byte_hamming_distance", ["a", "b"], "toInt64(byteHammingDistance(a, b))"),
        ("substring_index", ["s", "'.'", "2"], "substringIndex(s, '.', 2)"),
        ("count_substrings", ["s", "'a'"], "toInt64(countSubstrings(s, 'a'))"),
        ("count_substrings", ["s", "'a'", "3"], "toInt64(countSubstrings(s, 'a', 3))"),
        ("extract_groups", ["s", "'(a)'"], "extractGroups(s, '(a)')"),
        ("normalize_utf8_nfc", ["s"], "normalizeUTF8NFC(s)"),
        ("normalize_utf8_nfd", ["s"], "normalizeUTF8NFD(s)"),
        ("normalize_utf8_nfkc", ["s"], "normalizeUTF8NFKC(s)"),
        ("normalize_utf8_nfkd", ["s"], "normalizeUTF8NFKD(s)")
    ];

    private static readonly string[] MemberNames = Arms.Select(arm => arm.Name).Distinct().ToArray();

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    [Fact]
    public void Render_ShouldEmitTheExactClickHouseArmForEveryMemberAndVariant()
    {
        using (new AssertionScope())
        {
            foreach (var (name, args, expected) in Arms)
                ClickHouseScalars.Render(name, args).Should().Be(
                    expected, "'{0}({1})' is the ClickHouse arm", name, string.Join(", ", args));
        }
    }

    [Fact]
    public void Supports_ShouldAcceptTheCompleteStringSliceAndRejectCaseVariants()
    {
        MemberNames.Should().HaveCount(17, "the F01 slice adds 17 ClickHouse-only string names");

        using (new AssertionScope())
        {
            foreach (var name in MemberNames)
                ClickHouseScalars.Supports(name).Should().BeTrue("'{0}' is part of the ClickHouse inventory", name);

            ClickHouseScalars.Supports("OVERLAY").Should().BeFalse("names are case-sensitive CLR member names");
            ClickHouseScalars.Supports("base64encode").Should().BeFalse("the name must match the snake_case member exactly");
            ClickHouseScalars.Supports("normalizeUTF8NFC").Should().BeFalse("the table is keyed by the CLR member name");
            ClickHouseScalars.Supports("base64Encode").Should().BeFalse("the native spelling is not the table key");
        }
    }

    [Fact]
    public void Render_ShouldThrowForTheNativeSpellingInsteadOfTheMemberName()
    {
        FluentActions.Invoking(() => ClickHouseScalars.Render("base64Encode", ["x"]))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The base64Encode function is not supported by ClickHouse.");

        FluentActions.Invoking(() => ClickHouseScalars.Render("normalizeUTF8NFC", ["x"]))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The normalizeUTF8NFC function is not supported by ClickHouse.");
    }

    [Fact]
    public void DeclaredShapes_ShouldMatchTheNativeResultTypes()
    {
        using (new AssertionScope())
        {
            Shape("overlay", 4).Should().Be(typeof(string), "overlay returns String");
            Shape("initcap", 1).Should().Be(typeof(string), "initcap returns String");
            Shape("soundex", 1).Should().Be(typeof(string), "soundex returns String");
            Shape("base64_encode", 1).Should().Be(typeof(string), "base64Encode returns String");
            Shape("base64_decode", 1).Should().Be(typeof(string), "base64Decode returns String");
            Shape("hex", 1).Should().Be(typeof(string), "hex returns String");
            Shape("unhex", 1).Should().Be(typeof(string), "unhex returns String");
            Shape("edit_distance", 2).Should().Be(typeof(long?), "UInt64 is surfaced as long?");
            Shape("jaro_similarity", 2).Should().Be(typeof(double?), "Float64 is surfaced as double?");
            Shape("byte_hamming_distance", 2).Should().Be(typeof(long?), "UInt64 is surfaced as long?");
            Shape("substring_index", 3).Should().Be(typeof(string), "substringIndex returns String");
            Shape("count_substrings", 3).Should().Be(typeof(long?), "UInt64 is surfaced as long?");
            Shape("extract_groups", 2).Should().Be(typeof(string[]), "Array(String) is surfaced as string[]");
            Shape("normalize_utf8_nfc", 1).Should().Be(typeof(string), "normalizeUTF8NFC returns String");
            Shape("normalize_utf8_nfd", 1).Should().Be(typeof(string), "normalizeUTF8NFD returns String");
            Shape("normalize_utf8_nfkc", 1).Should().Be(typeof(string), "normalizeUTF8NFKC returns String");
            Shape("normalize_utf8_nfkd", 1).Should().Be(typeof(string), "normalizeUTF8NFKD returns String");
        }

        static Type Shape(string member, int arity) => typeof(ClickHouseFunctions)
            .GetMethods()
            .Single(method => method.Name == member
                && method.GetParameters().Length == arity
                // The declared shape is the one ClickHouseFunctions itself declares: a same-name member
                // inherited from CommonFunctions (the portable base64 pair) is a different contract.
                && method.DeclaringType == typeof(ClickHouseFunctions))
            .ReturnType;
    }

    [Fact]
    public void Projection_ShouldRenderEveryMemberOverAStringColumn()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                O = SqlFunctions.ClickHouse.overlay(x.String, "XY", 2),
                O4 = SqlFunctions.ClickHouse.overlay(x.String, "XY", 2, 3),
                I = SqlFunctions.ClickHouse.initcap(x.String),
                S = SqlFunctions.ClickHouse.soundex(x.String),
                Be = SqlFunctions.ClickHouse.base64_encode(x.String),
                Bd = SqlFunctions.ClickHouse.base64_decode(x.String),
                H = SqlFunctions.ClickHouse.hex(x.String),
                U = SqlFunctions.ClickHouse.unhex(x.String),
                Ed = SqlFunctions.ClickHouse.edit_distance(x.String, "abc"),
                Js = SqlFunctions.ClickHouse.jaro_similarity(x.String, "abc"),
                Bh = SqlFunctions.ClickHouse.byte_hamming_distance(x.String, "abc"),
                Si = SqlFunctions.ClickHouse.substring_index(x.String, ".", 2),
                Cs = SqlFunctions.ClickHouse.count_substrings(x.String, "a"),
                Cs3 = SqlFunctions.ClickHouse.count_substrings(x.String, "a", 2),
                Eg = SqlFunctions.ClickHouse.extract_groups(x.String, "(a)"),
                Nfc = SqlFunctions.ClickHouse.normalize_utf8_nfc(x.String),
                Nfd = SqlFunctions.ClickHouse.normalize_utf8_nfd(x.String),
                Nfkc = SqlFunctions.ClickHouse.normalize_utf8_nfkc(x.String),
                Nfkd = SqlFunctions.ClickHouse.normalize_utf8_nfkd(x.String)
            }));

        using (new AssertionScope())
        {
            sql.Should().Contain("overlay(somestring, 'XY', 2) as `O`");
            sql.Should().Contain("overlay(somestring, 'XY', 2, 3) as `O4`");
            sql.Should().Contain("initcap(somestring) as `I`");
            sql.Should().Contain("soundex(somestring) as `S`");
            sql.Should().Contain("base64Encode(somestring) as `Be`");
            sql.Should().Contain("base64Decode(somestring) as `Bd`");
            sql.Should().Contain("hex(somestring) as `H`");
            sql.Should().Contain("unhex(somestring) as `U`");
            sql.Should().Contain("toInt64(editDistance(somestring, 'abc')) as `Ed`");
            sql.Should().Contain("jaroSimilarity(somestring, 'abc') as `Js`");
            sql.Should().Contain("toInt64(byteHammingDistance(somestring, 'abc')) as `Bh`");
            sql.Should().Contain("substringIndex(somestring, '.', 2) as `Si`");
            sql.Should().Contain("toInt64(countSubstrings(somestring, 'a')) as `Cs`");
            sql.Should().Contain("toInt64(countSubstrings(somestring, 'a', 2)) as `Cs3`");
            sql.Should().Contain("extractGroups(somestring, '(a)') as `Eg`");
            sql.Should().Contain("normalizeUTF8NFC(somestring) as `Nfc`");
            sql.Should().Contain("normalizeUTF8NFD(somestring) as `Nfd`");
            sql.Should().Contain("normalizeUTF8NFKC(somestring) as `Nfkc`");
            sql.Should().Contain("normalizeUTF8NFKD(somestring) as `Nfkd`");
        }
    }

    [Fact]
    public void SingleNullArgument_ShouldRenderNullInsteadOfDroppingTheOnlyArgument()
    {
        // A one-argument function called with null has a real NULL operand, not an omitted optional
        // argument: the translator must keep it instead of rendering an empty call.
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                I = SqlFunctions.ClickHouse.initcap(null),
                H = SqlFunctions.ClickHouse.hex(null),
                Be = SqlFunctions.ClickHouse.base64_encode(null),
                Nfc = SqlFunctions.ClickHouse.normalize_utf8_nfc(null),
                L = SqlFunctions.ClickHouse.lower_utf8(null)
            }));

        using (new AssertionScope())
        {
            sql.Should().Contain("initcap(null) as `I`");
            sql.Should().Contain("hex(null) as `H`");
            sql.Should().Contain("base64Encode(null) as `Be`");
            sql.Should().Contain("normalizeUTF8NFC(null) as `Nfc`");
            sql.Should().Contain("lowerUTF8(null) as `L`");
        }
    }

    [Fact]
    public void OptionalArguments_ShouldBeDroppedOnlyWhenTheDefaultIsUsed()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                NoLength = SqlFunctions.ClickHouse.overlay(x.String, "XY", 2, null),
                WithLength = SqlFunctions.ClickHouse.overlay(x.String, "XY", 2, 0),
                NoStart = SqlFunctions.ClickHouse.count_substrings(x.String, "a", null),
                WithStart = SqlFunctions.ClickHouse.count_substrings(x.String, "a", 4)
            }));

        using (new AssertionScope())
        {
            sql.Should().Contain("overlay(somestring, 'XY', 2) as `NoLength`", "a null length is the 3-argument form");
            sql.Should().NotContain("overlay(somestring, 'XY', 2, NULL)");
            sql.Should().Contain("overlay(somestring, 'XY', 2, 0) as `WithLength`", "a zero length is emitted, not dropped");
            sql.Should().Contain("toInt64(countSubstrings(somestring, 'a')) as `NoStart`", "a null start is the 2-argument form");
            sql.Should().Contain("toInt64(countSubstrings(somestring, 'a', 4)) as `WithStart`");
        }
    }

    [Fact]
    public void Predicate_ShouldKeepTheFunctionsInTheWhereClause()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => SqlFunctions.ClickHouse.edit_distance(x.String, "abc") > 0
                && SqlFunctions.ClickHouse.jaro_similarity(x.String, "abc") > 0.5
                && SqlFunctions.ClickHouse.count_substrings(x.String, "a") > 0)
            .Select(x => x.Id));

        using (new AssertionScope())
        {
            sql.Should().Contain("where");
            sql.Should().Contain("toInt64(editDistance(somestring, 'abc')) > cast(0 as Int64)");
            sql.Should().Contain("jaroSimilarity(somestring, 'abc') > 0.5");
            sql.Should().Contain("toInt64(countSubstrings(somestring, 'a')) > cast(0 as Int64)");
        }
    }

    [Fact]
    public void QuotedIdentifiers_ShouldPreserveTheColumnAndAlias()
    {
        using var ctx = ClickHouseTestContext.CreateQuoted();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => x.Id == 1)
            .Select(x => new { H = SqlFunctions.ClickHouse.hex(x.String) }));

        sql.Should().Contain("hex(`somestring`) as `H`", "the argument stays a quoted column and the alias is preserved");
    }

    [Fact]
    public void PostgresAndSqlServer_ShouldReportEveryNameUnsupportedAndRejectTheCalls()
    {
        IScalarFunctions[] others =
        [
            PostgresDialect.Instance.ScalarFunctions!,
            SqlServerDialect.Instance.ScalarFunctions!
        ];

        using (new AssertionScope())
        {
            foreach (var functions in others)
            {
                foreach (var name in MemberNames)
                    functions.Supports(name).Should().BeFalse("'{0}' is ClickHouse-only", name);
            }
        }

        using var postgres = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        using var sqlServer = new SqlServerDataContext(
            "Server=localhost;Database=nextorm;User Id=sa;Password=nextorm;TrustServerCertificate=True",
            new DataContextBuilder());

        using (new AssertionScope())
        {
            foreach (var name in MemberNames)
            {
                AssertRejected(postgres, name);
                AssertRejected(sqlServer, name);
            }
        }

        static void AssertRejected(IDataContext ctx, string name)
        {
            static string Query(IDataContext ctx, string name) => name switch
            {
                "overlay" => SqlOf(ctx, ctx.From<IComplexEntity>().Select(x => SqlFunctions.ClickHouse.overlay(x.String, "XY", 2))),
                "initcap" => SqlOf(ctx, ctx.From<IComplexEntity>().Select(x => SqlFunctions.ClickHouse.initcap(x.String))),
                "soundex" => SqlOf(ctx, ctx.From<IComplexEntity>().Select(x => SqlFunctions.ClickHouse.soundex(x.String))),
                "base64_encode" => SqlOf(ctx, ctx.From<IComplexEntity>().Select(x => SqlFunctions.ClickHouse.base64_encode(x.String))),
                "base64_decode" => SqlOf(ctx, ctx.From<IComplexEntity>().Select(x => SqlFunctions.ClickHouse.base64_decode(x.String))),
                "hex" => SqlOf(ctx, ctx.From<IComplexEntity>().Select(x => SqlFunctions.ClickHouse.hex(x.String))),
                "unhex" => SqlOf(ctx, ctx.From<IComplexEntity>().Select(x => SqlFunctions.ClickHouse.unhex(x.String))),
                "edit_distance" => SqlOf(ctx, ctx.From<IComplexEntity>().Select(x => SqlFunctions.ClickHouse.edit_distance(x.String, "a"))),
                "jaro_similarity" => SqlOf(ctx, ctx.From<IComplexEntity>().Select(x => SqlFunctions.ClickHouse.jaro_similarity(x.String, "a"))),
                "byte_hamming_distance" => SqlOf(ctx, ctx.From<IComplexEntity>().Select(x => SqlFunctions.ClickHouse.byte_hamming_distance(x.String, "a"))),
                "substring_index" => SqlOf(ctx, ctx.From<IComplexEntity>().Select(x => SqlFunctions.ClickHouse.substring_index(x.String, ".", 2))),
                "count_substrings" => SqlOf(ctx, ctx.From<IComplexEntity>().Select(x => SqlFunctions.ClickHouse.count_substrings(x.String, "a"))),
                "extract_groups" => SqlOf(ctx, ctx.From<IComplexEntity>().Select(x => SqlFunctions.ClickHouse.extract_groups(x.String, "(a)"))),
                "normalize_utf8_nfc" => SqlOf(ctx, ctx.From<IComplexEntity>().Select(x => SqlFunctions.ClickHouse.normalize_utf8_nfc(x.String))),
                "normalize_utf8_nfd" => SqlOf(ctx, ctx.From<IComplexEntity>().Select(x => SqlFunctions.ClickHouse.normalize_utf8_nfd(x.String))),
                "normalize_utf8_nfkc" => SqlOf(ctx, ctx.From<IComplexEntity>().Select(x => SqlFunctions.ClickHouse.normalize_utf8_nfkc(x.String))),
                "normalize_utf8_nfkd" => SqlOf(ctx, ctx.From<IComplexEntity>().Select(x => SqlFunctions.ClickHouse.normalize_utf8_nfkd(x.String))),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, "not part of the F01 slice")
            };

            FluentActions.Invoking(() => Query(ctx, name)).Should().Throw<NotSupportedException>()
                .WithMessage($"The {name} function is not supported by this provider.");
        }
    }
}
