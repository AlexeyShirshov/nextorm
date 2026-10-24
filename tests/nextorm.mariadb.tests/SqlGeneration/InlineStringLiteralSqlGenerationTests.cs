using System.Data.Common;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// #299/#327: inline string/Guid constants are rendered through the provider's string-literal policy.
/// MariaDB transports an ordinary value as a charset-introduced UTF-8 hex literal, independent of the
/// session backslash mode, so no backslash-escaped quoted token is emitted for these values. Each case
/// pins the dialect token and the decoded payload. Captured values take the parameter path; those
/// assertions are plan-time only and cannot observe execution-time lowering. No database is touched.
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

    /// <summary>Decodes the UTF-8 payload of the <c>_utf8mb4 X'…'</c> transport.</summary>
    private static string DecodeHexLiteral(string sql)
    {
        const string prefix = "_utf8mb4 X'";
        var start = sql.IndexOf(prefix, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, "a MySQL-family value is transported as a charset-introduced hex literal");
        start += prefix.Length;
        var end = sql.IndexOf('\'', start);
        var hex = sql[start..end];

        hex.Should().MatchRegex("^[0-9a-f]*$", "the payload is lowercase UTF-8 hex");

        return Encoding.UTF8.GetString(Convert.FromHexString(hex));
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineApostrophe_ShouldUseTheModeIndependentHexLiteral()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // A C# literal inside the lambda is an Expression.Constant, so it takes the inline literal path.
        var sql = SqlOf(ctx, e.Where(x => x.String == "it's (a) test").Select(x => new { x.Id }));

        sql.Should().Contain("somestring = _utf8mb4 X'69742773202861292074657374'",
            "an ordinary value is transported as a charset-introduced UTF-8 hex literal");
        sql.Should().NotContain("'it's (a) test'",
            "the malformed three-apostrophe form terminates the literal early");
        DecodeHexLiteral(sql).Should().Be("it's (a) test", "the hex payload is the original value byte for byte");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineUnicode_ShouldRemainVerbatim()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String == "привет").Select(x => new { x.Id }))
            .Should().Contain("_utf8mb4 X'd0bfd180d0b8d0b2d0b5d182'");
        SqlOf(ctx, e.Where(x => x.String == "😀").Select(x => new { x.Id }))
            .Should().Contain("_utf8mb4 X'f09f9880'");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineEmptyString_ShouldRenderAnEmptyLiteral()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String == "").Select(x => new { x.Id }))
            .Should().Contain("somestring = _utf8mb4 X''");

        DecodeHexLiteral(SqlForConstant(ctx, string.Empty)).Should().BeEmpty(
            "the empty value is an empty hex payload, not a missing literal");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineLikeMetacharacters_ShouldRemainOrdinaryData()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var percent = SqlOf(ctx, e.Where(x => x.String == "%").Select(x => new { x.Id }));
        var underscore = SqlOf(ctx, e.Where(x => x.String == "_").Select(x => new { x.Id }));

        percent.Should().Contain("somestring = _utf8mb4 X'25'");
        underscore.Should().Contain("somestring = _utf8mb4 X'5f'");
        percent.Should().NotContain("'\\%'", "equality literals are not LIKE patterns and must not be wildcard-escaped");
        underscore.Should().NotContain("'\\_'");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineApostropheWithStatementTerminator_ShouldStayInsideTheLiteral()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.String == "it's; --").Select(x => new { x.Id }));

        sql.Should().Contain("somestring = _utf8mb4 X'697427733b202d2d'",
            "the whole text is data: the hex payload carries the quote, the semicolon and the comment marker");
        sql.Should().NotContain("'it's; --'");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineControlCharacters_ShouldRemainVerbatim()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.String == "a\r\nb\tc").Select(x => new { x.Id }));

        // The hex payload carries CR, LF and TAB as bytes, so none of them is escaped away or stripped.
        sql.Should().Contain("_utf8mb4 X'610d0a620963'", "CR, LF and TAB are data and are neither escaped nor stripped");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void CapturedString_ShouldRemainAParameter()
    {
        using var ctx = MariaDbTestContext.Create();
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
        using var ctx = MariaDbTestContext.Create();
        var parameter = Expression.Parameter(typeof(IComplexEntity), "x");
        var projection = Expression.Lambda<Func<IComplexEntity, string?>>(
            Expression.Constant(null, typeof(string)), parameter);

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>().Select(projection));

        sql.Should().Contain("null");
        sql.Should().NotContain("'null'", "a null string is the SQL null keyword, never the four-character text 'null'");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineGuidLiteral_ShouldUseTheModeIndependentHexLiteral()
    {
        using var ctx = MariaDbTestContext.Create();

        // The string/Guid arm is shared: a Guid constant keeps the canonical 'D' formatting and is
        // transported through the same charset-introduced hex form as any other value.
        var guid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var parameter = Expression.Parameter(typeof(IComplexEntity), "x");
        var projection = Expression.Lambda<Func<IComplexEntity, Guid>>(
            Expression.Constant(guid, typeof(Guid)), parameter);

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>().Select(projection));

        sql.Should().Contain("_utf8mb4 X'30303131323233332d343435352d363637372d383839392d616162626363646465656666'",
            "the Guid literal is transported through the same charset-introduced hex form");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void CapturedGuid_ShouldBeExtractedAsAParameter()
    {
        using var ctx = MariaDbTestContext.Create();
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
    public void InlineNul_ShouldRenderUtf8HexLiteral()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // U+0000 is an ordinary byte in the UTF-8 payload, so the value is transported whole.
        var sql = SqlOf(ctx, e.Where(x => x.String == "a\u0000b").Select(x => new { x.Id }));

        sql.Should().Contain("where somestring = _utf8mb4 X'610062'");
        DecodeHexLiteral(sql).Should().Be("a\u0000b", "the NUL byte round-trips inside the hex payload");
    }

    [Fact]
    [Trait("Issue", "327")]
    public void InlinePlainValue_ShouldKeepTheMeasuredHexForm()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String == "abc").Select(x => new { x.Id }))
            .Should().Contain("where somestring = _utf8mb4 X'616263'");

        DecodeHexLiteral(SqlForConstant(ctx, "abc")).Should().Be("abc");
    }

    [Theory]
    [Trait("Issue", "327")]
    [InlineData("a\\b", "_utf8mb4 X'615c62'")]
    [InlineData("\\'", "_utf8mb4 X'5c27'")]
    [InlineData("\\\\'", "_utf8mb4 X'5c5c27'")]
    [InlineData("a\\", "_utf8mb4 X'615c'")]
    [InlineData("a''b", "_utf8mb4 X'61272762'")]
    public void InlineHostileCorpus_ShouldUseTheModeIndependentHexLiteral(string value, string expectedLiteral)
    {
        using var ctx = MariaDbTestContext.Create();

        var sql = SqlForConstant(ctx, value);

        sql.Should().Contain("somestring = " + expectedLiteral,
            "the UTF-8 hex transport does not depend on the session backslash mode");
        sql.Should().NotContain("\\", "no backslash-escaped quoted token is emitted for a content value");
        DecodeHexLiteral(sql).Should().Be(value, "the hex payload is the original value byte for byte");
    }

    [Fact]
    [Trait("Issue", "327")]
    public void InlineStatementBoundaryPayload_ShouldStayInsideTheHexLiteral()
    {
        using var ctx = MariaDbTestContext.Create();

        var sql = SqlForConstant(ctx, "x'); SELECT 1; --");

        sql.Should().Contain("somestring = _utf8mb4 X'7827293b2053454c45435420313b202d2d'",
            "the quote travels as byte 0x27 inside the hex payload, so it cannot terminate the literal");
        sql.Should().NotContain("'x');", "an early-closing literal would expose the injected statement");
        DecodeHexLiteral(sql).Should().Be("x'); SELECT 1; --", "the decoded payload is the original hostile text");
    }

    [Fact]
    [Trait("Issue", "327")]
    public void InlineEveryC0CharacterExceptNul_ShouldUseTheUtf8HexPayload()
    {
        using var ctx = MariaDbTestContext.Create();

        for (var code = 1; code <= 0x1f; code++)
        {
            var value = ((char)code).ToString();
            var hex = code.ToString("x2", CultureInfo.InvariantCulture);
            var sql = SqlForConstant(ctx, value);

            sql.Should().Contain("somestring = _utf8mb4 X'" + hex + "'",
                $"U+{code:X4} is an ordinary byte in the UTF-8 payload");
            DecodeHexLiteral(sql).Should().Be(value);
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
    [InlineData('a', "_utf8mb4 X'61'")]
    [InlineData('\'', "_utf8mb4 X'27'")]
    [InlineData('\\', "_utf8mb4 X'5c'")]
    public void InlineCharConstant_ShouldUseTheStringPolicy(char value, string expectedLiteral)
    {
        using var ctx = MariaDbTestContext.Create();

        SqlForChar(ctx, value).Should().Contain(
            "select " + expectedLiteral,
            "a char constant is a one-character value and takes the mode-independent MariaDB hex policy");
    }

    [Fact]
    [Trait("Issue", "327")]
    public void InlineDefaultChar_ShouldUseTheHexNulRecipe()
    {
        using var ctx = MariaDbTestContext.Create();

        SqlForChar(ctx, default).Should().Contain("select _utf8mb4 X'00'",
            "default(char) is one U+0000, not an empty literal");
    }

    [Fact]
    [Trait("Issue", "327")]
    public void InlineCharC0Character_ShouldUseTheHexRecipe()
    {
        using var ctx = MariaDbTestContext.Create();

        SqlForChar(ctx, '\u0001').Should().Contain("select _utf8mb4 X'01'");
    }
}
