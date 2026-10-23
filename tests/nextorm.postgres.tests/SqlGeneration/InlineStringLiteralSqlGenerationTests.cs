using System.Data.Common;
using System.Globalization;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// #299/#327: inline string/Guid constants are rendered through the provider's string-literal policy.
/// Each case pins the dialect representation and, where the transport encodes the value, the decoded
/// payload, so every hostile input (apostrophes, backslashes, statement terminators, C0 characters)
/// stays data. Captured values take the parameter path; those assertions are plan-time only and cannot
/// observe execution-time lowering. No database is touched.
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

    /// <summary>Asserts the value appears only as an <c>E'…'</c> literal.</summary>
    private static void ShouldRenderOnlyEStringLiteral(string sql, string value)
    {
        var token = "E'" + value + "'";

        sql.Should().Contain(token, "PostgreSQL ordinary text literals use the explicit E-string form");
        sql.Replace(token, string.Empty, StringComparison.Ordinal)
            .Should().NotContain("'" + value + "'", "the plain escape-ambiguous spelling must not appear");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineApostrophe_ShouldDoubleTheEmbeddedQuote()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // A C# literal inside the lambda is an Expression.Constant, so it takes the inline literal path.
        var sql = SqlOf(ctx, e.Where(x => x.String == "it's (a) test").Select(x => new { x.Id }));

        sql.Should().Contain("somestring = E'it\\'s (a) test'",
            "the apostrophe is backslash-escaped inside the PostgreSQL E-string literal");
        sql.Should().NotContain("'it's (a) test'",
            "the malformed three-apostrophe form terminates the literal early and is a parser error in PostgreSQL");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineBackslash_ShouldRemainVerbatim()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.String == @"a\b").Select(x => new { x.Id }));

        sql.Should().Contain("somestring = E'a\\\\b'",
            "a backslash is ordinary data in a PostgreSQL literal: the E-string doubles it and the server reads one backslash");
        sql.Should().NotContain("E'a\\b'",
            "an unescaped backslash would be the backspace escape, not a literal backslash");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineUnicode_ShouldRemainVerbatim()
    {
        using var ctx = PostgresTestContext.Create();

        ShouldRenderOnlyEStringLiteral(SqlForConstant(ctx, "привет"), "привет");
        ShouldRenderOnlyEStringLiteral(SqlForConstant(ctx, "😀"), "😀");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineEmptyString_ShouldRenderAnEmptyLiteral()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String == "").Select(x => new { x.Id }))
            .Should().Contain("somestring = E''");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineLikeMetacharacters_ShouldRemainOrdinaryData()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var percent = SqlOf(ctx, e.Where(x => x.String == "%").Select(x => new { x.Id }));
        var underscore = SqlOf(ctx, e.Where(x => x.String == "_").Select(x => new { x.Id }));

        percent.Should().Contain("somestring = E'%'");
        underscore.Should().Contain("somestring = E'_'");
        percent.Should().NotContain("'\\%'", "equality literals are not LIKE patterns and must not be wildcard-escaped");
        underscore.Should().NotContain("'\\_'");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineApostropheWithStatementTerminator_ShouldStayInsideTheLiteral()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.String == "it's; --").Select(x => new { x.Id }));

        sql.Should().Contain("somestring = E'it\\'s; --'",
            "the whole text is data: the quote is backslash-escaped, the semicolon and comment marker stay inside the literal");
        sql.Should().NotContain("'it's; --'");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineControlCharacters_ShouldRemainVerbatim()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.String == "a\r\nb\tc").Select(x => new { x.Id }));

        // The E-string carries the control characters as fixed-width hex escapes; CR, LF and TAB are data
        // and are neither escaped away, replaced nor stripped.
        sql.Should().Contain("E'a\\x0d\\x0ab\\x09c'", "CR, LF and TAB are data and are neither escaped nor stripped");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void CapturedString_ShouldRemainAParameter()
    {
        using var ctx = PostgresTestContext.Create();
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
        using var ctx = PostgresTestContext.Create();
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
        using var ctx = PostgresTestContext.Create();

        // The string/Guid arm is shared: a Guid constant keeps the canonical 'D' formatting and stays
        // enclosed in single quotes after the escaping substitution (observed pre-fix output).
        var guid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var parameter = Expression.Parameter(typeof(IComplexEntity), "x");
        var projection = Expression.Lambda<Func<IComplexEntity, Guid>>(
            Expression.Constant(guid, typeof(Guid)), parameter);

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>().Select(projection));

        ShouldRenderOnlyEStringLiteral(sql, "00112233-4455-6677-8899-aabbccddeeff");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void CapturedGuid_ShouldBeExtractedAsAParameter()
    {
        using var ctx = PostgresTestContext.Create();
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
    public void InlineNul_ShouldBeRejectedBeforeRendering()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Where(x => x.String == "nul\u0000marker").Select(x => new { x.Id }));

        var exception = act.Should().Throw<NotSupportedException>(
            "PostgreSQL text cannot represent U+0000 and the value must never reach the server").Which;

        exception.Message.Should().Contain("PostgreSQL");
        exception.Message.Should().Contain("U+0000");
        exception.Message.Should().NotContain("marker", "the exception must not echo the supplied value");
    }

    [Theory]
    [Trait("Issue", "327")]
    [InlineData("a\\b", @"E'a\\b'")]
    [InlineData("\\'", @"E'\\\''")]
    [InlineData("\\\\'", @"E'\\\\\''")]
    [InlineData("a\\", @"E'a\\'")]
    [InlineData("a''b", @"E'a\'\'b'")]
    public void InlineHostileCorpus_ShouldRenderTheIntendedLiteral(string value, string expectedLiteral)
    {
        using var ctx = PostgresTestContext.Create();

        SqlForConstant(ctx, value).Should().Contain(
            "somestring = " + expectedLiteral,
            "the PostgreSQL E-string escapes a backslash by doubling it and an apostrophe by a backslash");
    }

    [Fact]
    [Trait("Issue", "327")]
    public void InlineStatementBoundaryPayload_ShouldStayInsideTheLiteral()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = SqlForConstant(ctx, "x'); SELECT 1; --");

        sql.Should().Contain(@"somestring = E'x\'); SELECT 1; --'",
            "the escaped apostrophe keeps the injected terminator inside the literal");
        sql.Should().NotContain(@"E'x');", "an early-closing literal would expose the injected statement");
    }

    [Fact]
    [Trait("Issue", "327")]
    public void InlineEveryC0CharacterExceptNul_ShouldUseTheLowercaseHexEscape()
    {
        using var ctx = PostgresTestContext.Create();

        for (var code = 1; code <= 0x1f; code++)
        {
            var hex = code.ToString("x2", CultureInfo.InvariantCulture);

            SqlForConstant(ctx, ((char)code).ToString()).Should().Contain(
                "somestring = E'\\x" + hex + "'",
                $"U+{code:X4} is transported as the lowercase fixed-width E-string escape");
        }
    }
}
