using System.Data.Common;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// #299/#327: inline string/Guid constants are rendered through the provider's string-literal policy.
/// ClickHouse keeps single-quoted literals with backslash escapes: an apostrophe is <c>\'</c>, a
/// backslash is <c>\\</c> and every C0 character is a fixed-width <c>\xNN</c> escape. Each case pins the
/// dialect token and the decoded payload. Captured values take the parameter path; those assertions are
/// plan-time only and cannot observe execution-time lowering. No database is touched.
/// </summary>
public class InlineStringLiteralSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    /// <summary>Builds the predicate the literal lambdas use, for values a lambda cannot spell readably.</summary>
    private static string SqlForConstant(IDataContext ctx, string value)
    {
        var parameter = Expression.Parameter(typeof(IComplexEntity), "x");
        var predicate = Expression.Lambda<Func<IComplexEntity, bool>>(
            Expression.Equal(Expression.Property(parameter, nameof(IComplexEntity.String)), Expression.Constant(value)),
            parameter);

        return SqlOf(ctx, ctx.From<IComplexEntity>().Where(predicate).Select(x => new { x.Id }));
    }

    /// <summary>Decodes the <c>\'</c>, <c>\\</c> and <c>\xNN</c> escapes of a ClickHouse literal.</summary>
    private static string DecodeClickHouseLiteral(string sql)
    {
        const string prefix = "somestring = '";
        var start = sql.IndexOf(prefix, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, "a ClickHouse value is rendered as a single-quoted literal");
        start += prefix.Length;

        var builder = new StringBuilder();
        for (var i = start; i < sql.Length; i++)
        {
            var c = sql[i];
            if (c == '\'')
                break;

            if (c != '\\')
            {
                builder.Append(c);
                continue;
            }

            i++;
            var escape = sql[i];
            switch (escape)
            {
                case '\\':
                    builder.Append('\\');
                    break;
                case '\'':
                    builder.Append('\'');
                    break;
                case 'x':
                    builder.Append((char)Convert.ToInt32(sql.Substring(i + 1, 2), 16));
                    i += 2;
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected ClickHouse escape '\\{escape}'.");
            }
        }

        return builder.ToString();
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineApostrophe_ShouldBackslashEscapeTheEmbeddedQuote()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // A C# literal inside the lambda is an Expression.Constant, so it takes the inline literal path.
        var sql = SqlOf(ctx, e.Where(x => x.String == "it's (a) test").Select(x => new { x.Id }));

        sql.Should().Contain("somestring = 'it\\'s (a) test'",
            "a literal apostrophe is backslash-escaped inside the ClickHouse string literal");
        sql.Should().NotContain("'it's (a) test'",
            "the malformed three-apostrophe form terminates the literal early");
        DecodeClickHouseLiteral(sql).Should().Be("it's (a) test", "the escape decodes to the original value");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineUnicode_ShouldRemainVerbatim()
    {
        using var ctx = ClickHouseTestContext.Create();

        SqlForConstant(ctx, "привет").Should().Contain("somestring = 'привет'");
        SqlForConstant(ctx, "😀").Should().Contain("somestring = '😀'");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineEmptyString_ShouldRenderAnEmptyLiteral()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String == "").Select(x => new { x.Id }))
            .Should().Contain("somestring = ''");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineLikeMetacharacters_ShouldRemainOrdinaryData()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var percent = SqlOf(ctx, e.Where(x => x.String == "%").Select(x => new { x.Id }));
        var underscore = SqlOf(ctx, e.Where(x => x.String == "_").Select(x => new { x.Id }));

        percent.Should().Contain("somestring = '%'");
        underscore.Should().Contain("somestring = '_'");
        percent.Should().NotContain("'\\%'", "equality literals are not LIKE patterns and must not be wildcard-escaped");
        underscore.Should().NotContain("'\\_'");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineApostropheWithStatementTerminator_ShouldStayInsideTheLiteral()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.String == "it's; --").Select(x => new { x.Id }));

        sql.Should().Contain("somestring = 'it\\'s; --'",
            "the whole text is data: the quote is backslash-escaped, the semicolon and comment marker stay inside the literal");
        sql.Should().NotContain("'it's; --'");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineControlCharacters_ShouldRemainVerbatim()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.String == "a\r\nb\tc").Select(x => new { x.Id }));

        // Every C0 character is carried as a fixed-width hex escape, so CR, LF and TAB are data and are
        // neither escaped away nor stripped.
        sql.Should().Contain("'a\\x0d\\x0ab\\x09c'", "CR, LF and TAB are data and are neither escaped nor stripped");
        DecodeClickHouseLiteral(sql).Should().Be("a\r\nb\tc", "the escapes decode to the original value");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void CapturedString_ShouldRemainAParameter()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var captured = "it's (a) test";

        var prepared = Prepare(ctx, e.Where(x => x.String == captured).Select(x => new { x.Id }));
        var sql = Normalize(prepared.DbCommand.CommandText);

        // Plan-time assertion only: this inspects the prepared command text and proves the value is
        // bound through a parameter slot rather than spliced. Execution-time lowering is not observable
        // here and is covered by the execution/live stream.
        sql.Should().Contain("@captured", "a captured value is bound, not spliced into the statement");
        sql.Should().NotContain("it''s", "the bound value stays raw; escaping is a literal-only concern");
        sql.Should().NotContain("it's");
        prepared.DbCommand.Parameters.Cast<DbParameter>().Single(p => p.ParameterName == "captured")
            .Value.Should().Be("it's (a) test");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineNullString_ShouldRenderTheNullKeyword()
    {
        using var ctx = ClickHouseTestContext.Create();
        var parameter = Expression.Parameter(typeof(IComplexEntity), "x");
        var projection = Expression.Lambda<Func<IComplexEntity, string?>>(
            Expression.Constant(null, typeof(string)), parameter);

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>().Select(projection));

        sql.Should().Contain("null");
        sql.Should().NotContain("'null'", "a null string is the SQL null keyword, never the four-character text 'null'");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineGuidLiteral_ShouldKeepTheQuotedCanonicalForm()
    {
        using var ctx = ClickHouseTestContext.Create();

        // The string/Guid arm is shared: a Guid constant keeps the canonical 'D' formatting and stays
        // enclosed in single quotes after the escaping substitution (observed pre-fix output).
        var guid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var parameter = Expression.Parameter(typeof(IComplexEntity), "x");
        var projection = Expression.Lambda<Func<IComplexEntity, Guid>>(
            Expression.Constant(guid, typeof(Guid)), parameter);

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>().Select(projection));

        sql.Should().Contain("'00112233-4455-6677-8899-aabbccddeeff'", "the Guid literal remains a quoted string");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void CapturedGuid_ShouldBeExtractedAsAParameter()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var guid = Guid.Parse("aabbccdd-1122-3344-5566-778899aabbcc");

        var prepared = Prepare(ctx, e.Where(x => x.Id == 1L).Select(x => new { G = guid }));
        var sql = Normalize(prepared.DbCommand.CommandText);

        sql.Should().NotContain("aabbccdd-1122-3344-5566-778899aabbcc",
            "a captured Guid is bound, never rendered as a literal");
        prepared.DbCommand.Parameters.Cast<DbParameter>().Single(p => p.ParameterName == "guid")
            .Value.Should().Be(guid);
    }

    [Fact]
    [Trait("Issue", "327")]
    public void InlineNul_ShouldRenderHexEscape()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // U+0000 is a C0 character like any other and travels as the fixed-width hex escape.
        var sql = SqlOf(ctx, e.Where(x => x.String == "a\u0000b").Select(x => new { x.Id }));

        sql.Should().Contain("where somestring = 'a\\x00b'");
        sql.Should().NotContain("\u0000", "the statement text never carries a raw NUL");
        DecodeClickHouseLiteral(sql).Should().Be("a\u0000b", "the escape decodes to the original value");
    }

    [Fact]
    [Trait("Issue", "327")]
    public void InlinePlainValue_ShouldKeepTheQuotedForm()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String == "abc").Select(x => new { x.Id }))
            .Should().Contain("where somestring = 'abc'");

        var plain = SqlForConstant(ctx, "abc");
        DecodeClickHouseLiteral(plain).Should().Be("abc");
        plain.Should().NotContainEquivalentOf("escape", "ClickHouse never emits an ESCAPE clause");
    }

    [Theory]
    [Trait("Issue", "327")]
    [InlineData("a\\b", @"'a\\b'")]
    [InlineData("\\'", @"'\\\''")]
    [InlineData("\\\\'", @"'\\\\\''")]
    [InlineData("a\\", @"'a\\'")]
    [InlineData("a''b", @"'a\'\'b'")]
    public void InlineHostileCorpus_ShouldRenderTheIntendedLiteral(string value, string expectedLiteral)
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlForConstant(ctx, value);

        sql.Should().Contain("somestring = " + expectedLiteral,
            "a ClickHouse literal doubles a backslash and backslash-escapes an apostrophe");
        DecodeClickHouseLiteral(sql).Should().Be(value, "the escapes decode to the original value");
    }

    [Fact]
    [Trait("Issue", "327")]
    public void InlineStatementBoundaryPayload_ShouldStayInsideTheLiteral()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlForConstant(ctx, "x'); SELECT 1; --");

        sql.Should().Contain(@"somestring = 'x\'); SELECT 1; --'",
            "the escaped apostrophe keeps the injected terminator inside the literal");
        sql.Should().NotContain("'x');", "an early-closing literal would expose the injected statement");
        DecodeClickHouseLiteral(sql).Should().Be("x'); SELECT 1; --");
    }

    [Fact]
    [Trait("Issue", "327")]
    public void InlineEveryC0CharacterExceptNul_ShouldUseTheFixedWidthHexEscape()
    {
        using var ctx = ClickHouseTestContext.Create();

        for (var code = 1; code <= 0x1f; code++)
        {
            var value = ((char)code).ToString();
            var hex = code.ToString("x2", CultureInfo.InvariantCulture);
            var sql = SqlForConstant(ctx, value);

            sql.Should().Contain("somestring = '\\x" + hex + "'",
                $"U+{code:X4} is transported as the lowercase fixed-width hex escape");
            DecodeClickHouseLiteral(sql).Should().Be(value);
        }
    }

    /// <summary>
    /// Renders a selected constant <c>char</c>. The single-row/single-column lane skips the row
    /// materializer (char is not a materialisable result type), so the statement text is observable
    /// without a database — exactly the seam this regression pins.
    /// </summary>
    private static string SqlForChar(IDataContext ctx, char value)
    {
        var parameter = Expression.Parameter(typeof(IComplexEntity), "x");
        var projection = Expression.Lambda<Func<IComplexEntity, char>>(
            Expression.Constant(value, typeof(char)), parameter);
        var command = ctx.From<IComplexEntity>().Select(projection);
        command.SingleRow = true;

        return Normalize(((DbPreparedQueryCommand<char>)ctx.GetPreparedQueryCommand(
            command, false, false, CancellationToken.None)).DbCommand.CommandText);
    }

    [Theory]
    [Trait("Issue", "327")]
    [InlineData('a', "'a'")]
    [InlineData('\'', "'\\''")]
    [InlineData('\\', "'\\\\'")]
    public void InlineCharConstant_ShouldUseTheStringPolicy(char value, string expectedLiteral)
    {
        using var ctx = ClickHouseTestContext.Create();

        SqlForChar(ctx, value).Should().Contain(
            "select " + expectedLiteral,
            "a char constant is a one-character string and takes the ClickHouse backslash-escape policy");
    }

    [Fact]
    [Trait("Issue", "327")]
    public void InlineDefaultChar_ShouldUseTheFixedWidthHexRecipe()
    {
        using var ctx = ClickHouseTestContext.Create();

        SqlForChar(ctx, default).Should().Contain(@"select '\x00'",
            "default(char) is one U+0000, spelled as the fixed-width hex escape");
    }

    [Fact]
    [Trait("Issue", "327")]
    public void InlineCharC0Character_ShouldUseTheFixedWidthHexEscape()
    {
        using var ctx = ClickHouseTestContext.Create();

        SqlForChar(ctx, '\u0001').Should().Contain(@"select '\x01'");
    }


    [Fact]
    [Trait("Issue", "327")]
    public void CapturedCharParameter_ShouldBindAsAOneCharacterString()
    {
        using var ctx = ClickHouseTestContext.CreateClickHouse();

        // ClickHouse has no char type: the driver's type map rejects System.Char, so the binding seam
        // normalizes the value to the equivalent one-character string (the TableParameterBinder rule).
        ctx.CreateParam("p", 'a').Value.Should().Be("a");
        ctx.CreateParam("p", 'П').Value.Should().Be("П");
        ctx.CreateParam("p", default(char)).Value.Should().Be("\0");
        ctx.CreateParam("p", null).Value.Should().BeNull("null keeps its existing binding");
        ctx.CreateParam("p", 42).Value.Should().Be(42, "non-char values are unchanged");
    }
}
