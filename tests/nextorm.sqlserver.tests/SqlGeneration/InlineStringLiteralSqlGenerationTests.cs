using System.Data.Common;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

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

    /// <summary>Asserts the value appears only as a Unicode literal: the <c>N</c> prefix is pinned.</summary>
    private static void ShouldRenderOnlyUnicodeLiteral(string sql, string value)
    {
        var token = "N'" + value + "'";

        sql.Should().Contain(token, "SQL Server text literals are always Unicode and carry the N prefix");
        sql.Replace(token, string.Empty, StringComparison.Ordinal)
            .Should().NotContain("'" + value + "'", "the plain non-Unicode spelling must not appear");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineApostrophe_ShouldDoubleTheEmbeddedQuote()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // A C# literal inside the lambda is an Expression.Constant, so it takes the inline literal path.
        var sql = SqlOf(ctx, e.Where(x => x.String == "it's (a) test").Select(x => new { x.Id }));

        sql.Should().Contain("somestring = N'it''s (a) test'",
            "a literal apostrophe is doubled inside the SQL string literal");
        sql.Should().NotContain("'it's (a) test'",
            "the malformed three-apostrophe form terminates the literal early");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineBackslash_ShouldRemainVerbatim()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.String == @"a\b").Select(x => new { x.Id }));

        sql.Should().Contain("somestring = N'a\\b'",
            "a backslash is ordinary data in a SQL Server literal and is neither removed nor duplicated here");
        sql.Should().NotContain("'a\\\\b'", "the literal renderer must not invent escape sequences");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineUnicode_ShouldRemainVerbatim()
    {
        using var ctx = SqlServerTestContext.Create();

        ShouldRenderOnlyUnicodeLiteral(SqlForConstant(ctx, "привет"), "привет");
        ShouldRenderOnlyUnicodeLiteral(SqlForConstant(ctx, "😀"), "😀");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineEmptyString_ShouldRenderAnEmptyLiteral()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String == "").Select(x => new { x.Id }))
            .Should().Contain("somestring = N''");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineLikeMetacharacters_ShouldRemainOrdinaryData()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var percent = SqlOf(ctx, e.Where(x => x.String == "%").Select(x => new { x.Id }));
        var underscore = SqlOf(ctx, e.Where(x => x.String == "_").Select(x => new { x.Id }));

        percent.Should().Contain("somestring = N'%'");
        underscore.Should().Contain("somestring = N'_'");
        percent.Should().NotContain("'\\%'", "equality literals are not LIKE patterns and must not be wildcard-escaped");
        underscore.Should().NotContain("'\\_'");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineApostropheWithStatementTerminator_ShouldStayInsideTheLiteral()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.String == "it's; --").Select(x => new { x.Id }));

        sql.Should().Contain("somestring = N'it''s; --'",
            "the whole text is data: the quote is doubled, the semicolon and comment marker stay inside the literal");
        sql.Should().NotContain("'it's; --'");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineControlCharacters_ShouldRemainVerbatim()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.String == "a\r\nb\tc").Select(x => new { x.Id }));

        // Normalize collapses CRLF to LF for the assertion, so the LF and TAB must survive verbatim.
        sql.Should().Contain("N'a\nb\tc'", "CR, LF and TAB are data and are neither escaped nor stripped");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void CapturedString_ShouldRemainAParameter()
    {
        using var ctx = SqlServerTestContext.Create();
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
        using var ctx = SqlServerTestContext.Create();
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
        using var ctx = SqlServerTestContext.Create();

        // The string/Guid arm is shared: a Guid constant keeps the canonical 'D' formatting and stays
        // enclosed in single quotes after the escaping substitution (observed pre-fix output).
        var guid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var parameter = Expression.Parameter(typeof(IComplexEntity), "x");
        var projection = Expression.Lambda<Func<IComplexEntity, Guid>>(
            Expression.Constant(guid, typeof(Guid)), parameter);

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>().Select(projection));

        ShouldRenderOnlyUnicodeLiteral(sql, "00112233-4455-6677-8899-aabbccddeeff");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void CapturedGuid_ShouldBeExtractedAsAParameter()
    {
        using var ctx = SqlServerTestContext.Create();
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
    public void InlineNul_ShouldSpliceNcharZero()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.String == "a\u0000b").Select(x => new { x.Id }));

        sql.Should().Contain("where somestring = N'a' + NCHAR(0) + N'b'");
        sql.Should().NotContain("\u0000", "the statement text never carries a raw NUL");
    }

    [Theory]
    [Trait("Issue", "327")]
    [InlineData("a\\b", "N'a\\b'")]
    [InlineData("\\'", "N'\\'''")]
    [InlineData("\\\\'", "N'\\\\'''")]
    [InlineData("a\\", "N'a\\'")]
    [InlineData("a''b", "N'a''''b'")]
    public void InlineHostileCorpus_ShouldRenderTheIntendedLiteral(string value, string expectedLiteral)
    {
        using var ctx = SqlServerTestContext.Create();

        SqlForConstant(ctx, value).Should().Contain(
            "somestring = " + expectedLiteral,
            "SQL Server has no backslash escape: apostrophes are doubled inside the Unicode literal and every other character is verbatim");
    }

    [Fact]
    [Trait("Issue", "327")]
    public void InlineStatementBoundaryPayload_ShouldStayInsideTheLiteral()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlForConstant(ctx, "x'); SELECT 1; --");

        sql.Should().Contain("somestring = N'x''); SELECT 1; --'",
            "the doubled apostrophe keeps the injected terminator inside the Unicode literal");
        sql.Should().NotContain("N'x');", "an early-closing literal would expose the injected statement");
    }

    [Fact]
    [Trait("Issue", "327")]
    public void InlineEveryC0CharacterExceptNul_ShouldRemainVerbatim()
    {
        using var ctx = SqlServerTestContext.Create();

        for (var code = 1; code <= 0x1f; code++)
        {
            var value = ((char)code).ToString();

            SqlForConstant(ctx, value).Should().Contain(
                "somestring = N'" + value + "'",
                $"U+{code:X4} is ordinary data in a SQL Server literal");
        }
    }

    [Fact]
    [Trait("Issue", "327")]
    public void InlineLongUnicodeValue_ShouldStayOneUnbrokenLiteral()
    {
        using var ctx = SqlServerTestContext.Create();
        var value = new string('ж', 4096);

        var sql = SqlForConstant(ctx, value);

        sql.Should().Contain("somestring = N'" + value + "'",
            "the whole 4096-character Unicode value is one literal with the N prefix");
        sql.Should().NotContain("' + N'", "the literal is neither split nor truncated");
    }
}
