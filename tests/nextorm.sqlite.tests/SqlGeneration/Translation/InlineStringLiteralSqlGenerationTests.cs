using System.Data.Common;
using System.Linq.Expressions;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

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

    /// <summary>Decodes the UTF-8 payload of the <c>CAST(X'…' AS TEXT)</c> transport.</summary>
    private static string DecodeTextBlobLiteral(string sql)
    {
        const string prefix = "CAST(X'";
        var start = sql.IndexOf(prefix, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, "the NUL-bearing value is transported as a blob cast to text");
        start += prefix.Length;
        var end = sql.IndexOf('\'', start);

        return Encoding.UTF8.GetString(Convert.FromHexString(sql[start..end]));
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineApostrophe_ShouldDoubleTheEmbeddedQuote()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // A C# literal inside the lambda is an Expression.Constant, so it takes the inline literal path.
        var sql = SqlOf(ctx, e.Where(x => x.String == "it's (a) test").Select(x => new { x.Id }));

        sql.Should().Contain("somestring = 'it''s (a) test'",
            "a literal apostrophe is doubled inside the SQL string literal");
        sql.Should().NotContain("'it's (a) test'",
            "the malformed three-apostrophe form terminates the literal early");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineBackslash_ShouldRemainVerbatim()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.String == @"a\b").Select(x => new { x.Id }));

        sql.Should().Contain("somestring = 'a\\b'",
            "a backslash is ordinary data in a SQLite literal and is neither removed nor duplicated here");
        sql.Should().NotContain("'a\\\\b'", "the literal renderer must not invent escape sequences");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineUnicode_ShouldRemainVerbatim()
    {
        using var ctx = SqliteTestContext.Create();

        SqlForConstant(ctx, "привет").Should().Contain("somestring = 'привет'");
        SqlForConstant(ctx, "😀").Should().Contain("somestring = '😀'");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineEmptyString_ShouldRenderAnEmptyLiteral()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String == "").Select(x => new { x.Id }))
            .Should().Contain("somestring = ''");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineLikeMetacharacters_ShouldRemainOrdinaryData()
    {
        using var ctx = SqliteTestContext.Create();
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
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.String == "it's; --").Select(x => new { x.Id }));

        sql.Should().Contain("somestring = 'it''s; --'",
            "the whole text is data: the quote is doubled, the semicolon and comment marker stay inside the literal");
        sql.Should().NotContain("'it's; --'");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineControlCharacters_ShouldRemainVerbatim()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.String == "a\r\nb\tc").Select(x => new { x.Id }));

        // Normalize collapses CRLF to LF for the assertion, so the LF and TAB must survive verbatim.
        sql.Should().Contain("'a\nb\tc'", "CR, LF and TAB are data and are neither escaped nor stripped");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void CapturedString_ShouldRemainAParameter()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var captured = "it's (a) test";

        var prepared = Prepare(ctx, e.Where(x => x.String == captured).Select(x => new { x.Id }));
        var sql = Normalize(prepared.DbCommand.CommandText);

        // Plan-time assertion only: this inspects the prepared command text and proves the value is
        // bound through a parameter slot rather than spliced. Execution-time lowering is not observable
        // here and is covered by the execution/live stream.
        sql.Should().Contain("$captured",
            "the SQLite parameter marker is '$' and a captured value is bound, not spliced into the statement");
        sql.Should().NotContain("it''s", "the bound value stays raw; escaping is a literal-only concern");
        sql.Should().NotContain("it's");
        prepared.DbCommand.Parameters.Cast<DbParameter>().Single(p => p.ParameterName == "captured")
            .Value.Should().Be("it's (a) test");
    }

    [Fact]
    [Trait("Issue", "299")]
    public void InlineNullString_ShouldRenderTheNullKeyword()
    {
        using var ctx = SqliteTestContext.Create();
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
        using var ctx = SqliteTestContext.Create();

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
        using var ctx = SqliteTestContext.Create();
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
    public void InlineNul_ShouldRenderUtf8BlobCastToText()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // SQLite truncates a text literal at an embedded NUL, so the value travels as a UTF-8 blob.
        var sql = SqlOf(ctx, e.Where(x => x.String == "a\u0000b").Select(x => new { x.Id }));

        sql.Should().Contain("where somestring = CAST(X'610062' AS TEXT)");
        sql.Should().NotContain("\u0000", "the statement text never carries a raw NUL");
        DecodeTextBlobLiteral(sql).Should().Be("a\u0000b", "the decoded payload is the original value byte for byte");
    }

    [Theory]
    [Trait("Issue", "327")]
    [InlineData("a\\b", "'a\\b'")]
    [InlineData("\\'", "'\\'''")]
    [InlineData("\\\\'", "'\\\\'''")]
    [InlineData("a\\", "'a\\'")]
    [InlineData("a''b", "'a''''b'")]
    public void InlineHostileCorpus_ShouldRenderTheIntendedLiteral(string value, string expectedLiteral)
    {
        using var ctx = SqliteTestContext.Create();

        SqlForConstant(ctx, value).Should().Contain(
            "somestring = " + expectedLiteral,
            "SQLite has no backslash escape: apostrophes are doubled and every other character is verbatim");
    }

    [Fact]
    [Trait("Issue", "327")]
    public void InlineStatementBoundaryPayload_ShouldStayInsideTheLiteral()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = SqlForConstant(ctx, "x'); SELECT 1; --");

        sql.Should().Contain("somestring = 'x''); SELECT 1; --'",
            "the doubled apostrophe keeps the injected terminator inside the literal");
        sql.Should().NotContain("'x');", "an early-closing literal would expose the injected statement");
    }

    [Fact]
    [Trait("Issue", "327")]
    public void InlineEveryC0CharacterExceptNul_ShouldRemainVerbatim()
    {
        using var ctx = SqliteTestContext.Create();

        for (var code = 1; code <= 0x1f; code++)
        {
            var value = ((char)code).ToString();

            SqlForConstant(ctx, value).Should().Contain(
                "somestring = '" + value + "'",
                $"U+{code:X4} is ordinary data in a SQLite literal");
        }
    }

    [Fact]
    [Trait("Issue", "327")]
    public void InlinePlainValue_ShouldKeepTheQuotedForm()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String == "abc").Select(x => new { x.Id }))
            .Should().Contain("where somestring = 'abc'");
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
    [InlineData('\'', "''''")]
    [InlineData('\\', "'\\'")]
    public void InlineCharConstant_ShouldUseTheStringPolicy(char value, string expectedLiteral)
    {
        using var ctx = SqliteTestContext.Create();

        SqlForChar(ctx, value).Should().Contain(
            "select " + expectedLiteral,
            "a char constant is a one-character string and takes the SQLite string-literal policy");
    }

    [Fact]
    [Trait("Issue", "327")]
    public void InlineDefaultChar_ShouldUseTheNulBlobRecipe()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = SqlForChar(ctx, default);
        sql.Should().Contain("select CAST(X'00' AS TEXT)", "default(char) is one U+0000, not an empty value");
        DecodeTextBlobLiteral(sql).Should().Be("\0");
    }

    [Fact]
    [Trait("Issue", "327")]
    public void InlineCharC0Character_ShouldRemainVerbatim()
    {
        using var ctx = SqliteTestContext.Create();
        var value = '\u0001';

        SqlForChar(ctx, value).Should().Contain("select '" + value + "'");
    }
}
