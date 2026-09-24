using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// SQL generation for the C# string semantics surface: the format subset, ordinal
/// <see cref="StringComparison"/>, collation and the fail-fast contract. No database is touched.
/// </summary>
public class StringSemanticsSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static void AssertUnsupported<T>(IDataContext ctx, QueryCommand<T> cmd, string messageFragment)
    {
        var act = () => SqlOf(ctx, cmd);
        act.Should().Throw<NotSupportedException>().WithMessage($"*{messageFragment}*");
    }

    [Fact]
    public void ToString_DateFormat_ShouldRenderToChar()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = x.Datetime!.Value.ToString("yyyy-MM-dd") }))
            .Should().Be("select to_char(dt, 'YYYY-MM-DD') as \"F\" from complex_entity");
    }

    [Fact]
    public void Interpolation_DateFormat_ShouldRenderToChar()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = $"{x.Datetime:yyyy-MM-dd}" }))
            .Should().Be("select to_char(dt, 'YYYY-MM-DD') as \"F\" from complex_entity");
    }

    [Fact]
    public void StringFormat_LiteralAndFixedNumber_ShouldConcat()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = string.Format("Id={0:F2}", x.Id) }))
            .Should().Be("select ('Id='||to_char(id, 'FM9999999999999990.' || repeat('0', 2))) as \"F\" from complex_entity");
    }

    [Fact]
    public void Collate_ShouldRenderCollateClause()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = SqlFunctions.Sql.collate(x.String, "C") }))
            .Should().Be("select somestring collate \"C\" as \"F\" from complex_entity");
    }

    [Fact]
    public void CompareOrdinalInWhere_ShouldRenderSignedCase()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => string.CompareOrdinal(x.String, "a") > 0).Select(x => new { x.Id }))
            .Should().Be("select id from complex_entity\n where (case when somestring collate \"C\" < 'a' collate \"C\" then -1 when somestring collate \"C\" > 'a' collate \"C\" then 1 else 0 end > 0)");
    }

    [Fact]
    public void ContainsOrdinal_ShouldRenderBinaryLike()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String!.Contains("a", StringComparison.Ordinal)).Select(x => new { x.Id }))
            .Should().Be("select id from complex_entity\n where somestring collate \"C\" like '%a%'");
    }

    [Fact]
    public void CompareWithoutComparison_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(ctx, e.Where(x => string.Compare(x.String, "a") > 0).Select(x => new { x.Id }), "culture-sensitive");
    }

    [Fact]
    public void ToUpperWithCulture_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(ctx, e.Select(x => new { F = x.String!.ToUpper(System.Globalization.CultureInfo.GetCultureInfo("tr-TR")) }), "InvariantCulture");
    }

    [Fact]
    public void UnsupportedNumberSpecifier_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(ctx, e.Select(x => new { F = string.Format("{0:N2}", x.Id) }), "not supported by this provider");
    }

    [Fact]
    public void EqualsOrdinalIgnoreCase_ShouldCaseFoldAndUseBinary()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String!.Equals("a", StringComparison.OrdinalIgnoreCase)).Select(x => new { x.Id }))
            .Should().Contain("lower(somestring) collate \"C\" = lower('a') collate \"C\"");
    }

    [Fact]
    public void StartsWithOrdinalIgnoreCase_ShouldCaseFoldBothSides()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String!.StartsWith("a", StringComparison.OrdinalIgnoreCase)).Select(x => new { x.Id }))
            .Should().Contain("lower(somestring) collate \"C\" like lower('a%')");
    }

    [Fact]
    public void TrimChar_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(ctx, e.Select(x => new { F = x.String!.Trim('x') }), "whitespace only");
    }

    [Fact]
    public void FormatWithCurrentCultureProvider_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(
            ctx,
            e.Select(x => new { F = string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0}", x.Id) }),
            "InvariantCulture");
    }

    [Fact]
    public void ToUpperInvariant_ShouldMapToUpper()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = x.String!.ToUpperInvariant() }))
            .Should().Be("select upper(somestring) as \"F\" from complex_entity");
    }

    [Fact]
    public void IndexOfWithCount_ShouldThrow()
    {
        // IndexOf(string, int startIndex, int count) has no primitive here; the count must not be
        // mistaken for the start index.
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(ctx, e.Where(x => x.String!.IndexOf("a", 1, 2) > 0).Select(x => new { x.Id }), "IndexOf");
    }
}
